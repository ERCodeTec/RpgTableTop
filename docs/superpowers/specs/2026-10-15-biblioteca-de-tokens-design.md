# Biblioteca de tokens: modelos de monstros e NPCs reutilizáveis entre mesas

## Objetivo

O Mestre cria modelos de tokens (monstros, NPCs, vilões) uma vez, organiza-os em categorias que ele mesmo define e
os coloca em qualquer mapa de qualquer mesa da qual seja Mestre. A biblioteca pertence ao usuário, como os
personagens, e não a uma mesa.

## Decisões confirmadas com o usuário

- **Categorias livres.** O Mestre cria, renomeia e exclui as próprias categorias (ex.: "NPCs", "Vilões", "Inimigos",
  "Mortos-vivos").
- **Imagem própria.** O modelo pode ter uma imagem enviada pelo Mestre a uma galeria pessoal, além dos ícones
  genéricos e da cor da borda.
- **Abordagem A para imagens.** A imagem pessoal mora fora das mesas. Ao colocar o modelo num mapa, o servidor copia o
  arquivo para a mesa uma única vez e o token aponta para a cópia. Autorização, cota e exclusão das imagens da mesa
  continuam como estão.
- **Colocar no mapa: lista e quantidade.** Na aba Tokens do painel do Mestre, "Da biblioteca" abre a lista filtrável
  por categoria. O Mestre escolhe um modelo e a quantidade, e os tokens surgem nas primeiras células livres,
  numerados.

## Decisões tomadas sem consulta

- **O token é uma cópia.** Colocar um modelo cria tokens independentes. Editar ou excluir o modelo depois não muda
  nenhum token que já esteja num mapa.
- **Tipo no mapa separado da categoria.** Cada modelo tem `Tipo` (Inimigo ou Npc), porque o tipo decide o
  comportamento no mapa (exibição padrão do HP, ícones válidos). A categoria só organiza. `Personagem` não é aceito:
  token de personagem continua nascendo do personagem do jogador.
- **"Sem categoria".** `CategoriaId` é opcional. Modelos sem categoria aparecem num grupo "Sem categoria". Excluir uma
  categoria move os modelos dela para "Sem categoria" (o diálogo de confirmação diz quantos são) em vez de bloquear.
- **Sem categorias padrão no banco.** Com a biblioteca vazia, a página oferece um botão "Criar categorias sugeridas"
  (NPCs, Vilões, Inimigos). Assim, quem apagar as próprias categorias não as vê voltarem sozinhas.
- **Categorias em ordem alfabética.** Não há ordenação manual.
- **Tudo ou nada ao colocar.** Se o mapa não tiver células livres para todas as cópias, nada é criado e a resposta é
  409 com a mensagem "O mapa só tem N células livres".
- **Numeração continua.** Com quantidade maior que 1, os nomes ficam "Goblin 1", "Goblin 2"… e começam depois do maior
  "Goblin N" que já exista no mapa. Com quantidade 1, o token leva o nome do modelo sem número. O nome base é cortado
  para caber o sufixo no limite de 60 caracteres.
- **Opção "colocar ocultos".** O diálogo de colocar tem a caixa "Ocultos dos jogadores" (padrão: desmarcada), que
  cria os tokens com `Visivel = false`, para emboscadas.
- **Limites.** 50 categorias e 500 modelos por usuário. Galeria pessoal: 100 imagens e 200 MB por usuário
  (`Uploads:CotaImagensPorUsuario`, `Uploads:CotaBytesPorUsuario`). Quantidade por colocação: 1 a 20.

## Domínio

### `CategoriaDeModelo` (`Vtt.Domain/Biblioteca`)

| Campo | Regra |
|---|---|
| `Id` | |
| `UsuarioId` | dono |
| `Nome` | 1 a 40 caracteres, aparado; único por usuário sem diferenciar maiúsculas (índice único em `(UsuarioId, lower(Nome))`) |
| `CriadaEm` | |

### `ModeloDeToken` (`Vtt.Domain/Biblioteca`)

| Campo | Regra |
|---|---|
| `Id`, `UsuarioId` | |
| `CategoriaId?` | categoria do mesmo usuário; FK com `ON DELETE SET NULL` |
| `Tipo` | `Inimigo` ou `Npc` |
| `Nome` | 1 a `Token.NomeTamanhoMaximo` |
| `HpMaximo` | `Token.HpMaximoMinimo`…`Token.HpMaximoLimite` |
| `Icone` | `Token.IconeValido(Tipo, Icone)` |
| `ImagemId?` | imagem **pessoal** do mesmo usuário; FK com `ON DELETE RESTRICT` |
| `CorBorda` | mesmo formato hexadecimal validado nos tokens |
| `Deslocamento` | `Personagem.DeslocamentoMinimo`…`DeslocamentoLimite`, padrão 6 |
| `Visao` | 0…`Personagem.VisaoLimite`, padrão 0 |
| `ExibicaoHp` | padrão `Token.ExibicaoHpPadrao(Tipo)` |
| `CriadoEm`, `AtualizadoEm` | |

Um método do domínio, `ModeloDeToken.CriarToken(Guid mapaId, string nome, int x, int y, Guid? imagemDaMesaId, bool visivel)`,
monta o `Token` com `HpAtual = HpMaximo`, `MovimentoRestante = Deslocamento`, `PersonagemId` e `DonoUsuarioId`
nulos. Assim, a cópia dos campos fica num só lugar.

### `ImagemUpload` passa a ter dois escopos

- `MesaId` fica opcional e entra `UsuarioId?`. Um check constraint exige que exatamente um dos dois esteja
  preenchido.
- Entra `OrigemImagemId?`, sem FK porque a imagem pessoal de origem pode ser excluída depois. Ele é preenchido na
  cópia que vai para a mesa, e o índice único filtrado em `(MesaId, OrigemImagemId) WHERE OrigemImagemId IS NOT NULL`
  garante uma cópia por mesa.
- Pastas: as da mesa continuam em `{mesaId:N}/`. As pessoais ficam em `usuarios/{usuarioId:N}/`.
  `CaminhoDaMiniatura` passa a ser derivado de `CaminhoArmazenado` (mesma pasta, `{id:N}.mini.webp`) em vez de
  `MesaId`. O formato dos caminhos que já existem não muda.
- `EnviadaPorUsuarioId` continua como está. Na imagem pessoal, ele é igual a `UsuarioId`.

## Imagens pessoais

`IImagemService` ganha as operações pessoais. O núcleo de validação e gravação (tamanho, magic bytes, dimensões,
animação, recodificação, gravação e limpeza em caso de falha) é extraído do `EnviarAsync` atual para um método
privado comum. Só a cota e o caminho mudam conforme o escopo.

| Operação | Regra |
|---|---|
| Enviar pessoal | usuário autenticado; cota pessoal |
| Listar pessoal | só as do próprio usuário, com uso e limites, como `GaleriaDto` |
| Baixar | imagem de mesa: membro da mesa (como hoje). Imagem pessoal: só o dono. Para os demais, 404 |
| Excluir | imagem de mesa: como hoje. Imagem pessoal: só o dono, com 409 enquanto algum modelo a usar ("modelo \"Goblin\"") |

### Cópia para a mesa

`ObterCopiaNaMesaAsync(Guid imagemPessoalId, Guid mesaId)`, em uso interno pelo serviço da biblioteca:

1. Se já existe imagem na mesa com `OrigemImagemId = imagemPessoalId`, devolve essa.
2. Se não existe, confere a cota da mesa (409 com a mesma mensagem do upload), copia o arquivo principal e a
   miniatura pelo `IFileStorage` (`AbrirAsync` e `SalvarAsync`) e insere a linha com os mesmos metadados,
   `MesaId = mesaId`, `EnviadaPorUsuarioId = Mestre` e `OrigemImagemId`.
3. Se duas colocações simultâneas copiarem a mesma imagem, o índice único rejeita a segunda. Nesse caso, apagam-se os
   arquivos dela e relê-se a cópia vencedora.

A cópia é uma imagem comum da mesa: aparece na galeria da mesa, conta na cota da mesa e só pode ser excluída quando
não estiver em uso. Se o Mestre excluí-la, a próxima colocação copia de novo.

## API

Tudo com `[Authorize]`, DTOs com validação no padrão de `PersonagemRequestValidator`, erros por `Resultado` e
`this.Problema`.

| Método e rota | Corpo e resposta |
|---|---|
| `GET /api/biblioteca/categorias` | `CategoriaDto[]` com `{ id, nome, quantidadeDeModelos }` |
| `POST /api/biblioteca/categorias` | `{ nome }` → 201 `CategoriaDto`; 409 se o nome repetir ou o limite for atingido |
| `POST /api/biblioteca/categorias/sugeridas` | cria as que faltarem entre NPCs, Vilões e Inimigos → `CategoriaDto[]` |
| `PUT /api/biblioteca/categorias/{id}` | `{ nome }` → `CategoriaDto` |
| `DELETE /api/biblioteca/categorias/{id}` | 204; os modelos ficam sem categoria |
| `GET /api/biblioteca/modelos` | `ModeloDeTokenDto[]`, ordenados por nome |
| `GET /api/biblioteca/modelos/{id}` | `ModeloDeTokenDto` |
| `POST /api/biblioteca/modelos` | `ModeloDeTokenRequest` → 201 |
| `PUT /api/biblioteca/modelos/{id}` | `ModeloDeTokenRequest` → `ModeloDeTokenDto` |
| `DELETE /api/biblioteca/modelos/{id}` | 204 |
| `POST /api/biblioteca/imagens` | multipart `arquivo` → 201 `ImagemDto` |
| `GET /api/biblioteca/imagens` | `GaleriaDto` |
| `POST /api/mapas/{id}/tokens/da-biblioteca` | `{ modeloId, quantidade, visivel? }` → 201 `TokenDto[]` |

Um recurso de outro usuário devolve 404, nunca 403, para não revelar que existe. Baixar e excluir imagens pessoais
usam as rotas que já existem em `/api/imagens/{id}`.

### Colocar no mapa (`TokenService.CriarDaBibliotecaAsync`)

1. Carrega o mapa (404 se não existir) e chama `ExigirMestreAsync` na mesa dele, como o `CriarAsync`: quem não é membro recebe 404 e o jogador recebe 403.
2. Carrega o modelo com `UsuarioId = usuário`. Se não encontrar, devolve 404.
3. Valida `quantidade` entre 1 e 20.
4. Calcula as células livres com `CelulasIndisponiveisAsync(somenteVisiveis: false)` e vai tomando a próxima com
   `Grade.PrimeiraLivre`. Se não houver células para todos, devolve 409 sem gravar nada.
5. Se o modelo tem imagem, obtém a cópia na mesa (seção anterior).
6. Monta os nomes com numeração e cria os tokens com `ModeloDeToken.CriarToken`.
7. Grava tudo num único `SaveChanges` e notifica cada token pelo caminho que já existe (`ProjecoesDoToken` e
   `IMapaNotifier`): os jogadores recebem os visíveis e o Mestre recebe todos.

## Frontend

### Página `/biblioteca` (`features/biblioteca/`)

- Entra o item "Biblioteca" no menu do `Layout`, entre "Mesas" e "Personagens".
- No topo: abas por categoria ("Todas", cada categoria com contagem, "Sem categoria"), busca por nome, "Nova
  categoria" e "Novo modelo".
- No menu de cada aba de categoria: renomear e excluir (com `ConfirmarDialogo` informando quantos modelos ficam sem
  categoria).
- Grade de cards no estilo de `PersonagemCard`: disco com `TokenIcone` (imagem ou ícone, com a borda), nome, tipo,
  HP, deslocamento e visão, e as ações editar e excluir.
- Biblioteca vazia: texto explicativo e os botões "Criar categorias sugeridas" e "Novo modelo".
- `ModeloFormDialogo`: categoria (select com opção "Sem categoria"), tipo, nome, HP máximo, ícone, imagem, cor,
  deslocamento, visão e exibição do HP.
- Os campos em comum com `TokenFormDialogo` (tipo, ícone, imagem, cor, deslocamento, visão) são extraídos para
  `CamposDoToken`, usado pelos dois formulários. Esse é o único refactor feito no formulário que já existe.
- `SeletorDeImagem` passa a receber a fonte (`{ mesaId }` ou `'pessoal'`). No modo pessoal, ele lista
  `/api/biblioteca/imagens` e permite enviar uma imagem nova direto do seletor, porque a página da biblioteca não tem
  aba de imagens.
- `bibliotecaApi.ts` segue o padrão de `personagensApi.ts`, com chaves de query `['biblioteca', 'categorias']`,
  `['biblioteca', 'modelos']` e `['biblioteca', 'imagens']`.

### Painel do Mestre

- Na aba Tokens, ao lado de "Novo token", entra o botão "Da biblioteca".
- `ColocarDaBibliotecaDialogo`: filtro por categoria, busca, lista com miniatura, campo de quantidade (1 a 20, padrão
  1), caixa "Ocultos dos jogadores" e o botão "Colocar".
- Quando a requisição dá certo, os tokens devolvidos entram no `mapaStore` por `aplicarToken` (como no duplicar), o
  primeiro fica selecionado e o diálogo fecha. Os erros 409 (mapa cheio, cota da mesa) aparecem no próprio diálogo.
- Com a biblioteca vazia, o diálogo mostra um link para `/biblioteca`.

## Erros e casos de borda

- Modelo excluído enquanto o diálogo de colocar está aberto: a colocação devolve 404, o diálogo mostra "Este modelo
  não existe mais" e recarrega a lista.
- Imagem pessoal cujo arquivo sumiu do armazenamento: a cópia falha com log de erro e 404 "A imagem do modelo não foi
  encontrada". Nenhum token é criado.
- Cota da mesa estourada pela cópia: 409 sem criar tokens. A mensagem sugere colocar sem imagem (editar o modelo) ou
  liberar espaço na mesa.
- Mesa excluída: as cópias somem junto com a pasta da mesa (`ExcluirPastaAsync`, como hoje), e a galeria pessoal não é
  afetada.

## Testes

São testes de integração em `Vtt.Api.IntegrationTests`, com `VttApiFactory`, `ApiTestHelpers` e `ImagemTestHelpers`:

- `BibliotecaCategoriasTests`:
  - CRUD;
  - nome repetido sem diferenciar maiúsculas dá 409;
  - excluir a categoria deixa os modelos sem categoria;
  - as sugeridas não duplicam;
  - outro usuário recebe 404.
- `BibliotecaModelosTests`:
  - CRUD;
  - tipo `Personagem` é rejeitado;
  - ícone inválido para o tipo é rejeitado;
  - imagem de mesa ou de outro usuário é rejeitada;
  - categoria de outro usuário é rejeitada;
  - outro usuário recebe 404.
- `ImagensPessoaisTests`:
  - enviar e listar;
  - cota pessoal;
  - o dono baixa e outro usuário recebe 404;
  - excluir em uso por modelo dá 409.
- `ColocarDaBibliotecaTests`:
  - cria N tokens com os campos do modelo e HP cheio;
  - a numeração continua a partir dos nomes que já existem;
  - um jogador da mesa recebe 403 e quem não é membro recebe 404;
  - modelo de outro usuário dá 404;
  - mapa sem espaço dá 409 e nenhum token é criado;
  - imagem é copiada uma vez para N tokens e em duas colocações;
  - membros da mesa baixam a cópia;
  - `visivel: false` não chega aos jogadores pelo hub;
  - editar ou excluir o modelo não altera os tokens.
- Os testes atuais de `ImagensTests` continuam passando, e um teste novo confirma que a miniatura das imagens de mesa
  segue no mesmo caminho.
- Frontend: `npm run build` e `npm test`, seguidos de um roteiro manual no README (criar categoria, criar modelo com
  imagem, colocar 4 cópias em duas mesas diferentes).

## Fora do escopo

- Salvar um token que já está no mapa como modelo ("Salvar na biblioteca").
- Compartilhar a biblioteca com outros Mestres, ou importar e exportar.
- Arrastar da lista para o mapa e escolher a célula ao clicar.
- Estatísticas de ficha (CA, ataques, perícias): o modelo guarda só o que o token usa.

## Diferenças na implementação

- **Uma migration só.** As mudanças de imagens e da biblioteca estão juntas em `BibliotecaDeTokens`. O índice único de
  nome de categoria é criado por SQL (`lower(nome)`), porque o EF não modela índice sobre expressão.
- **`CamposDoToken` parcial.** Só o seletor de ícone (`SeletorDeIcone`) e o campo de imagem (`CampoDeImagem`) foram
  extraídos. Os campos numéricos ficaram em cada formulário, porque têm rótulos e dicas diferentes (o modelo não tem
  HP atual).
- **Enviar imagem pelo seletor.** O botão "Enviar imagem" do `SeletorDeImagem` vale para as duas fontes (mesa e
  pessoal), não só para a pessoal.
- **Link para a biblioteca em outra aba.** No diálogo de colocar, o link da biblioteca vazia abre outra aba, para o
  Mestre não sair da mesa.
- **Correção encontrada pelos testes.** Um token inserido com HP "Oculto" era gravado como "Barra", porque a coluna
  tem padrão no banco e `Oculto` é o valor 0 do enum. `TokenConfiguration` ganhou `HasSentinel` fora do enum, sem
  mudança de schema.
