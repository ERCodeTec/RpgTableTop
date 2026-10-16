# Etapa 4: imagens, estruturas e câmera isométrica

Data: 2026-10-09. Estado: implementado. A seção "Diferenças na implementação", no fim, lista o que mudou em relação a este texto.

## Objetivo

O Mestre envia imagens (fundo de mapa, token, textura de estrutura), monta o cenário com paredes, portas, pilares e obstáculos em um modo de edição, e cada usuário inclina e gira a própria câmera até a visão isométrica. Estruturas que bloqueiam impedem o movimento dos jogadores, com validação no servidor.

## Fora do escopo

- Edição de HP em tempo real pelos jogadores e filtro de visibilidade (Etapa 5).
- Regras de deslocamento ou checagem de caminho: o bloqueio vale só para a célula de destino.
- Sincronizar a câmera entre usuários.
- Suíte de testes no frontend.

## Decisões confirmadas com o usuário

1. Paredes ocupam **células inteiras**: toda estrutura é um retângulo de células.
2. **O Mestre ignora o bloqueio** ao mover tokens; só jogadores são barrados.
3. Estrutura bloqueante (ou porta fechada) sobre um token é **permitida**: o token fica onde está e pode sair, mas não voltar.
4. **Desfazer/refazer simples** entra nesta etapa.

## Decisões tomadas sem consulta

- Texturas pela opção (a): `fetch` com Bearer convertido em blob URL, para não criar um segundo mecanismo de autenticação só para imagens.
- Excluir imagem em uso é **rejeitado** (409) com a lista de onde ela é usada; desvincular em silêncio apagaria o fundo de um mapa sem o Mestre perceber.
- Cada ação do editor é uma chamada REST com tela otimista e `Versao`, como o resto das operações do Mestre.
- A imagem é recodificada no formato de entrada (PNG mantém transparência); a miniatura é WebP. Acima de 4096 px a imagem é rejeitada, não reduzida.
- O Mestre pode definir imagem em token de jogador, já que só ele faz upload.
- Três endpoints além dos do enunciado: `GET /api/imagens/{id}/miniatura`, `PUT /api/mapas/{id}/fundo` e `PUT /api/estruturas/{id}/porta`.
- "Isométrica" combina 45° de inclinação com a visão girada 45°, que é o que produz a grade em losango.
- Qualidade gráfica e ângulo da câmera ficam no `localStorage`.
- `CenaDoMapa.tsx` é dividido em arquivos menores (refatoração avisada), mantendo o comportamento da Etapa 3.
- Sem repositório git na pasta: a spec fica como arquivo, sem commit.

## Entrega em fases

Cada fase termina com testes passando antes da seguinte.

1. Uploads e uso de imagens em mapa e token.
2. Estruturas, colisão e modo de edição.
3. Câmera, billboards e qualidade gráfica.

---

## Fase 1: uploads e imagens

### Modelo de dados (migration `ImagensEFundoDoMapa`)

**ImagemUpload** (tabela `imagens`)

| Campo | Tipo | Regra |
|---|---|---|
| Id | Guid | gerado pelo servidor; também é o nome do arquivo |
| MesaId | Guid | FK para `mesas`, cascata |
| EnviadaPorUsuarioId | Guid | FK para `usuarios`, `Restrict` |
| NomeOriginal | string | até 120, sanitizado, só para exibição |
| ContentType | string | `image/png`, `image/jpeg` ou `image/webp`, definido pelo conteúdo |
| TamanhoBytes | long | tamanho do arquivo recodificado |
| Largura, Altura | int | em pixels |
| CaminhoArmazenado | string | relativo à raiz de uploads: `{mesaId}/{id}.{ext}` |
| CriadaEm | DateTimeOffset | |

**Mapa** ganha `ImagemFundoId` (nullable), `FundoEscala` (double, padrão 1, de 0,05 a 20) e `FundoOffsetX`/`FundoOffsetY` (double, em células, padrão 0). Escala 1 significa imagem com a largura do mapa, mantendo a proporção.

**Token** ganha `ImagemId` (nullable).

As FKs para `imagens` usam `Restrict`: o banco impede apagar uma imagem em uso mesmo se a checagem do serviço falhar.

### Armazenamento

- `IFileStorage` (Application): `SalvarAsync(caminho, conteudo)`, `AbrirAsync(caminho)`, `ExcluirAsync(caminho)` e `ExcluirPastaAsync(pasta)`.
- `LocalFileStorage` (Infrastructure): raiz em `Storage:UploadsPath` (padrão `uploads` ao lado da API; no Docker, `/app/uploads`, já montado como volume). Todo caminho é resolvido e precisa ficar dentro da raiz.
- O nome enviado pelo cliente nunca entra no caminho. Miniatura em `{mesaId}/{id}.mini.webp`.
- Excluir a mesa apaga a pasta `{mesaId}`.

### Processamento

`IImagemProcessor` (Application) com implementação ImageSharp (Infrastructure). Ordem, parando no primeiro erro:

1. Tamanho do arquivo até `Uploads:TamanhoMaximoBytes` (5 MB).
2. Tipo real pelos primeiros bytes: PNG, JPEG ou WebP. Extensão e Content-Type do cliente são ignorados.
3. Dimensões lidas do cabeçalho, sem decodificar os pixels, até `Uploads:DimensaoMaxima` (4096).
4. Cota da mesa: `Uploads:CotaImagensPorMesa` (100) e `Uploads:CotaBytesPorMesa` (200 MB).
5. Decodificar e recodificar no mesmo formato, sem EXIF, ICC, XMP nem dados após o fim da imagem.
6. Miniatura de 256 px no maior lado, em WebP.
7. Gravar os arquivos e depois a linha no banco; se o banco falhar, os arquivos são apagados.

`NomeOriginal` sanitizado: só o nome base, sem caracteres de controle nem separadores de caminho, limitado a 120 caracteres.

### Endpoints

| Método e rota | Quem | Comportamento |
|---|---|---|
| `POST /api/mesas/{id}/imagens` | Mestre | multipart, campo `arquivo`; 201 com os dados da imagem; 400 para tipo ou dimensão inválidos; 413 acima do tamanho; 409 com a cota estourada; 429 no rate limit |
| `GET /api/mesas/{id}/imagens` | Mestre | lista, mais recentes primeiro, com o uso atual da cota |
| `GET /api/imagens/{id}` | membro | o arquivo; `ETag`, `Cache-Control: private, max-age=31536000, immutable`, `X-Content-Type-Options: nosniff`; 304 com `If-None-Match` |
| `GET /api/imagens/{id}/miniatura` | membro | idem, para a miniatura |
| `DELETE /api/imagens/{id}` | Mestre | 204; 409 se em uso, dizendo onde |
| `PUT /api/mapas/{id}/fundo` | Mestre | define ou remove a imagem de fundo, escala e offsets; emite `MapaAtualizado` |

- Rate limit do upload: política própria, `RateLimiting:UploadsPorMinuto` (30) por usuário.
- O limite de corpo da requisição do upload fica um pouco acima de 5 MB; o padrão do restante da API não muda.
- `CriarTokenRequest` e `EditarTokenRequest` ganham `ImagemId` opcional. Imagem de fundo, de token e textura precisam ser da mesma mesa.

### Frontend

- `shared/imagens/`: cache de blob URLs por id (com contagem de uso e liberação), hook `useImagemUrl` e componente de miniatura.
- Galeria no painel do Mestre: miniaturas, uso da cota, upload por `XMLHttpRequest` com barra de progresso, excluir com confirmação. Erros do servidor aparecem junto ao arquivo enviado.
- Formulário de token: escolher ou remover imagem da galeria. Com imagem, o disco mostra a imagem recortada em círculo, com a borda colorida.
- Fundo do mapa: escolher na galeria e entrar em "Ajustar fundo", onde arrastar desloca e um controle deslizante escala; Salvar e Cancelar.
- A grade passa a desenhar só as linhas, transparente, sobre a cor de fundo e a imagem.

---

## Fase 2: estruturas, colisão e editor

### Modelo de dados (migration `Estruturas`)

**Estrutura** (tabela `estruturas`)

| Campo | Tipo | Regra |
|---|---|---|
| Id | Guid | |
| MapaId | Guid | FK para `mapas`, cascata |
| Tipo | enum `TipoEstrutura` | `Parede`, `Porta`, `Pilar`, `Obstaculo`; texto |
| X, Y | int | célula do canto superior esquerdo |
| Largura, Profundidade | int | em células, mínimo 1; o retângulo fica dentro do mapa |
| Altura | double | de 0,1 a 3 células |
| RotacaoGraus | int | 0, 90, 180 ou 270 |
| Cor | string | `#RRGGBB` |
| TexturaImagemId | Guid? | FK para `imagens`, `Restrict` |
| BloqueiaMovimento | bool | padrão true |
| PortaAberta | bool | só tem efeito em `Porta` |
| Versao | int | token de concorrência |

- A área ocupada é sempre o retângulo `X, Y, Largura, Profundidade` nos eixos do mapa. `RotacaoGraus` orienta só o visual.
- Bloqueio efetivo: `Porta` bloqueia quando fechada; os demais tipos seguem `BloqueiaMovimento`.
- Estruturas podem se sobrepor entre si e a tokens.

### Regras no servidor

- `MoverToken`: se quem move é jogador e o destino está na área de uma estrutura com bloqueio efetivo, rejeita com motivo e estado atual do token. O Mestre não é barrado.
- Spawn, criação sem posição e duplicação de token pulam células bloqueadas. Criar token com posição explícita em célula bloqueada é permitido ao Mestre.
- Reduzir o mapa é 409 se alguma estrutura ficar fora dos novos limites.

### Endpoints e eventos

| Método e rota | Quem | Comportamento |
|---|---|---|
| `POST /api/mapas/{id}/estruturas` | Mestre | cria; 201 |
| `PUT /api/estruturas/{id}` | Mestre | edita tudo; leva `versao` e devolve 409 em conflito |
| `PUT /api/estruturas/{id}/porta` | Mestre | abre ou fecha; 409 se não for porta |
| `DELETE /api/estruturas/{id}` | Mestre | remove |

- Eventos: `EstruturaAdicionada(mesaId, estrutura)`, `EstruturaAtualizada(mesaId, estrutura)`, `EstruturaRemovida(mesaId, mapaId, estruturaId)`. Mapa em preparação: só o Mestre recebe.
- `MapaEstadoDto` ganha `Estruturas`; vale para `GET /api/mapas/{id}/estado`, `EntrarNaMesa`, ativação e `MapaAtivoAlterado`.

### Editor (só Mestre)

- Alternância "Jogar / Editar mapa" na barra do mapa.
- Ferramentas:
  - **Selecionar:** clique seleciona; arrastar move; alças nas bordas redimensionam.
  - **Parede:** arrastar cria um segmento reto de 1 célula de espessura no eixo dominante do arraste.
  - **Porta, Pilar, Obstáculo:** um clique cria 1×1.
- Painel lateral da estrutura selecionada: tipo, cor, textura (galeria), altura, bloqueio e, na porta, aberta/fechada.
- Atalhos: Delete remove, R gira (troca largura e profundidade em torno do centro e soma 90°), Ctrl+D duplica deslocando uma célula para a direita e para baixo (limitado ao mapa), Ctrl+Z desfaz, Ctrl+Shift+Z refaz, Esc cancela a ação em curso. Atalhos não disparam com o foco em um campo de texto.
- Snap ao grid em criar, mover e redimensionar. Pré-visualização durante o arraste.
- Desfazer/refazer: pilha local de até 50 ações, cada uma com a chamada inversa. Perde-se ao recarregar; se a inversa falhar (outra aba mudou a estrutura), a ação sai da pilha e um aviso explica.
- No modo Jogar, clicar em uma porta alterna aberta/fechada. Jogadores não têm essa ação.
- No modo Editar, tokens não são arrastados.

### Padrões por tipo

| Tipo | Altura | Cor | Bloqueia |
|---|---|---|---|
| Parede | 2 | `#6B6F76` | sim |
| Porta | 1,6 | `#8A5A2B` | quando fechada |
| Pilar | 2,5 | `#8C8C84` | sim |
| Obstáculo | 0,6 | `#5E7A4A` | sim |

Porta aberta é desenhada baixa e translúcida, para se distinguir da fechada em qualquer ângulo.

---

## Fase 3: câmera, billboards e qualidade

### Câmera

- Continua ortográfica. Estado local em um store próprio: alvo no chão, zoom, inclinação (0° a 45°) e rotação. Nada vai ao servidor.
- Controles sobre o mapa: deslizante de inclinação, "Topo" (0° e grade alinhada à tela), "Isométrica" (45° de inclinação, visão girada 45°), girar 90° para cada lado (botões e Q/E).
- Transições de 350 ms com suavização; com `prefers-reduced-motion`, imediatas.
- Pan, zoom e pinça continuam como na Etapa 3, projetando no plano do chão. O alvo fica limitado à área do mapa.
- Inclinação e rotação são lembradas no `localStorage`.

### Cena

- Luz ambiente e uma direcional fraca. Estruturas são caixas com material iluminado, com cor ou textura.
- Qualidade, lembrada no `localStorage` (padrão Média; Baixa em telas de toque estreitas):
  - **Baixa:** sem sombras, sem antialiasing, pixel ratio 1.
  - **Média:** antialiasing, pixel ratio até 2, sombra suave das estruturas no chão.
- Tokens são billboards: o corpo copia a orientação da câmera e sobe conforme a inclinação, ficando em pé com a base na célula. O nome acompanha.
- Profundidade: o disco do token usa recorte por alfa e grava profundidade, então uma parede alta à frente o esconde parcialmente, sem ordenação manual.
- Picking por raio da câmera contra as malhas de tokens e estruturas; a célula vem da interseção do raio com o chão. Vale para qualquer inclinação e rotação.

### Reorganização do frontend

`CenaDoMapa.tsx` é dividido, sem mudar o comportamento existente:

| Arquivo | Responsabilidade |
|---|---|
| `CenaDoMapa.tsx` | `Canvas`, luzes e composição |
| `cameraStore.ts`, `ControleDaCamera.tsx` | estado, animação, pan e zoom |
| `ControlesDeVisao.tsx` | inclinação, presets, rotação, qualidade |
| `picking.ts` | raio, chão, token e estrutura sob o ponteiro |
| `InteracaoDeJogo.tsx` | arraste de tokens, clique em porta |
| `InteracaoDeEdicao.tsx`, `editorStore.ts` | ferramentas, seleção, desfazer |
| `EstruturaNoMapa.tsx`, `FundoDoMapa.tsx` | malhas |

Arquivos da Etapa 3 que mudam: `TokenNoMapa` (billboard e imagem), `Grade` (transparência), `mapaStore` (estruturas), `useMapaTempoReal` (eventos), `PainelDoMestre` (abas Mapas, Tokens, Estruturas e Imagens), `MapaAba` (modo e controles).

---

## Mudanças em código das etapas anteriores

Acréscimos, sem alterar comportamento existente:

- `MapaService`: estado com estruturas, checagem de estruturas ao redimensionar, endpoint de fundo.
- `TokenService` e `TokensDeJogador`: célula livre considera bloqueio; `ImagemId`; bloqueio no `MoverAsync`.
- `MesaService`: excluir a mesa apaga a pasta de imagens.
- `IMapaNotifier`, `IMesaClient`, `MapaNotifier`: eventos de estrutura.
- `Program.cs`, `AppOptions.cs`, `appsettings.json`: opções de upload, rate limit e armazenamento.

## Tratamento de erros

- Upload: cada rejeição tem mensagem própria em português (tipo, tamanho, dimensões, cota).
- Arquivo ausente no disco para uma imagem existente no banco: 404 e log de erro.
- Falha ao carregar textura no cliente: o token cai no ícone, a estrutura na cor e o fundo na cor do mapa.

## Testes

Integração (PostgreSQL real; armazenamento em pasta temporária por execução):

`ImagensTests`
- arquivo `.png` com conteúdo que não é imagem é rejeitado;
- o tipo vem do conteúdo: JPEG enviado como `.png` é aceito como JPEG;
- arquivo acima do limite é rejeitado; imagem acima das dimensões é rejeitada;
- cota de quantidade e de bytes é aplicada (fábrica com limites baixos);
- EXIF não sobrevive ao reprocessamento; a miniatura existe e tem até 256 px;
- jogador não envia, não lista e não exclui; não-membro não baixa; jogador membro baixa;
- `ETag` e 304;
- excluir imagem em uso é 409; livre é 204 e o arquivo some do disco;
- imagem de outra mesa não é aceita em mapa, token ou estrutura;
- rate limit do upload.

`EstruturasTests`
- Mestre cria, edita, abre porta e remove; jogador não;
- validações de área, altura e rotação;
- conflito de versão no `PUT`;
- reduzir o mapa com estrutura de fora é 409;
- o estado do mapa inclui as estruturas.

`MapaHubTests` (acréscimos)
- estrutura bloqueante impede o `MoverToken` do jogador; o Mestre passa;
- porta fechada bloqueia e aberta permite;
- obstáculo com `BloqueiaMovimento=false` permite;
- spawn pula célula bloqueada;
- eventos de estrutura chegam ao grupo; em mapa em preparação, só ao Mestre;
- `EntrarNaMesa` traz as estruturas.

Navegador (Playwright, roteiro manual automatizado): upload real com progresso, arquivo falso `.png` rejeitado, fundo ajustado, parede desenhada, porta abrindo e fechando, desfazer, visão isométrica girada com arraste de token e clique em porta, e o roteiro da Etapa 3 de novo.

Frontend: `npm run typecheck` e `npm run build`.

## Entrega

Arquivos completos, as duas migrations, passo a passo de teste (incluindo como criar o arquivo falso `.png`), checklist de aceitação, lista de decisões e README atualizado.

## Observação de licença

O ImageSharp 3 usa a Six Labors Split License: gratuito para uso pessoal, projetos de código aberto e empresas pequenas. Para uma mesa entre amigos não há custo; reavaliar se o projeto virar produto comercial.

## Diferenças na implementação

- **ImageSharp fixado em 3.1.12.** A linha 4.x exige chave de licença em builds Release, o que quebraria o `dotnet publish` do Dockerfile. O NuGet aponta cinco vulnerabilidades na 3.1.12: quatro são de TIFF/BigTIFF e uma da leitura de perfis ICC. Nenhuma é alcançada aqui, porque só os decodificadores PNG, JPEG e WebP são registrados, o tipo é barrado antes pelos magic bytes e o perfil ICC é descartado sem ser lido. Os avisos NU1902/NU1903 continuam aparecendo no build, de propósito.
- **Uma migration só**, `ImagensEEstruturas`, em vez de duas.
- **Arquivo acima de 5 MB responde 400**, com o motivo no campo `arquivo`, como as outras recusas. O 413 fica para corpos acima de 32 MB, que o servidor nem lê.
- **O limitador de requisições passou a rodar depois da autenticação**, para o limite de upload ser por usuário. O limite de login e cadastro, por IP, não muda.
- **Controles de visão ficam em uma barra abaixo do mapa**, não sobrepostos a ele.
- **Arquivos do frontend**: a divisão ficou em `rig.ts` e `CameraDoMapa.tsx` (câmera), `picking.ts`, `Interacao.tsx` com `modoDeJogo.ts`, `modoDeEdicao.ts` e `modoDeFundo.ts`, `editorStore.ts` com `acoesDoEditor.ts`, e os painéis `PainelDeEstruturas`, `PainelDaEstrutura`, `GaleriaDeImagens` e `SeletorDeImagem`.
- **O picking é analítico**: o raio da câmera contra o disco de cada token e a caixa de cada estrutura, sem percorrer as malhas da cena.
- **O nome do token é desenhado por cima da cena**, sem teste de profundidade: continua legível, mas aparece mesmo quando o disco está atrás de uma parede.
- **A rotação da estrutura orienta a textura do topo**; as portas não têm folha desenhada.
- **O mapa continua enquadrado** quando a câmera inclina ou gira, enquanto o usuário não tiver deslocado nem aproximado.
