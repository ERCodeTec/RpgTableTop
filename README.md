# Mesa Virtual (VTT)

Virtual tabletop para mesas de D&D 5e entre amigos. O Mestre cria mapas e os jogadores interagem em tempo real.

**Etapa 1:** cadastro e login, e CRUD de personagens com ícone por classe.
**Etapa 2:** mesas, convites por link, entrada escolhendo o personagem e lobby em tempo real (SignalR).
**Etapa 3:** mapa 2D em grid, tokens e movimento sincronizado em tempo real.
**Etapa 4:** upload de imagens (fundo de mapa, tokens, texturas), estruturas com editor e câmera até a visão isométrica.
**Etapa 5:** HP em tempo real, tokens ocultos sem vazamento de dados, reconexão robusta, backup e o guia para abrir a mesa aos amigos pelo ngrok.
**Etapa 6 (atual):** névoa de guerra opcional por mapa, com a visão de cada personagem vinda da ficha.

## Stack

- **Backend:** ASP.NET Core 10, EF Core, ASP.NET Core Identity, JWT, FluentValidation, SignalR, ImageSharp
- **Banco:** PostgreSQL 17
- **Frontend:** React 19, TypeScript, Vite, TanStack Query, React Router, react-three-fiber (three.js), Zustand
- **Infra:** Docker Compose; a API serve o build do frontend no mesmo host e porta

## Subir com Docker

Pré-requisito: Docker em execução.

```bash
cp .env.example .env
# edite o .env: troque POSTGRES_PASSWORD e JWT_SECRET (openssl rand -base64 48)
docker compose up --build
```

| O quê | Endereço |
|---|---|
| Aplicação | http://localhost:8080 |
| Swagger (só com `ASPNETCORE_ENVIRONMENT=Development`) | http://localhost:8080/swagger |
| Health check | http://localhost:8080/health |
| PostgreSQL (só na sua máquina) | `localhost:5432`, banco `vtt`, usuário `vtt` |

Se a porta 8080 ou a 5432 já estiver em uso, troque `API_PORT` ou `POSTGRES_PORT` no `.env`.

Para parar: `docker compose down`. Para apagar também o banco e os uploads: `docker compose down -v`.

`docker compose ps` mostra a saúde dos dois serviços. A API fica `healthy` quando ela e o banco respondem, e os dois reiniciam sozinhos se caírem (`restart: unless-stopped`).

## Variáveis do `.env`

| Variável | Para que serve |
|---|---|
| `POSTGRES_PASSWORD` | Senha do banco. Só vale na primeira subida do volume. |
| `JWT_SECRET` | Chave que assina os tokens; mínimo de 32 caracteres. Trocar invalida os logins existentes. |
| `JWT_ISSUER` | Emissor gravado nos tokens. |
| `JWT_EXPIRATION_MINUTES` | Validade do login (padrão: 720). |
| `PUBLIC_BASE_URL` | URL pública do sistema, usada nos links de convite. Vazia (o padrão), o link usa o host da requisição, o que já funciona com o ngrok. |
| `NGROK_AUTHTOKEN` | Token da sua conta do ngrok. Só é usado com `--profile ngrok`. |
| `NGROK_DOMAIN` | Domínio fixo do ngrok, sem `https://`. Vazia, o ngrok escolhe o endereço. |
| `ASPNETCORE_ENVIRONMENT` | `Development` liga o Swagger; use `Production` ao expor na internet. |
| `DATABASE_AUTO_MIGRATE` | `true` aplica as migrations quando a API sobe. |
| `API_PORT`, `POSTGRES_PORT`, `NGROK_INSPECT_PORT` | Portas publicadas na sua máquina (aplicação, banco e painel do ngrok). |

Os limites de upload têm padrões que servem para uma mesa entre amigos. Para mudá-los, acrescente as variáveis ao serviço `api` do `docker-compose.yml`:

| Variável | Padrão | Para que serve |
|---|---|---|
| `Uploads__TamanhoMaximoBytes` | 5242880 (5 MB) | Tamanho máximo de cada arquivo. |
| `Uploads__DimensaoMaxima` | 4096 | Maior largura ou altura aceita, em pixels. |
| `Uploads__PixelsMaximosDaAnimacao` | 67108864 | Soma dos pixels de todos os quadros de um GIF animado (largura × altura × quadros). Cada quadro é decodificado na memória ao gravar; o padrão ocupa até ~256 MB nesse momento. |
| `Uploads__CotaBytesPorMesa` | 209715200 (200 MB) | Espaço total de imagens por mesa. |
| `Uploads__CotaImagensPorMesa` | 100 | Quantidade de imagens por mesa. |
| `Uploads__CotaBytesPorUsuario` | 209715200 (200 MB) | Espaço da galeria pessoal da biblioteca de tokens, por usuário. |
| `Uploads__CotaImagensPorUsuario` | 100 | Quantidade de imagens na galeria pessoal, por usuário. |
| `RateLimiting__UploadsPorMinuto` | 30 | Uploads por usuário a cada minuto. |

As imagens ficam no volume `uploads` (em `/app/uploads` no container); `docker compose down -v` apaga esse volume junto com o banco.

## Testar com dois usuários

Cada usuário precisa de uma sessão de navegador separada, porque o login fica no `localStorage`:

1. Em uma janela normal, crie a conta do Mestre, crie uma mesa e clique em **Gerar link de convite**.
2. Em uma **janela anônima** (ou em outro navegador), cole o link. Crie a conta do jogador, crie um personagem e entre na mesa.
3. Deixe as duas janelas lado a lado no lobby: entradas, presença, troca de personagem e início de sessão aparecem na outra sem recarregar.

Para um terceiro usuário, use outro navegador ou outro perfil; duas janelas anônimas do mesmo navegador compartilham o login.

### Mapa e tokens

Com o Mestre em uma janela e o jogador na outra, as duas na aba **Mapa** da mesa:

1. O jogador vê "Aguardando o Mestre". O Mestre clica em **Criar mapa** (ex.: 20 × 15); o mapa abre "Em preparação" e o jogador continua aguardando.
2. Ainda em preparação, o Mestre cria um inimigo em **Novo token**. Nada aparece para o jogador.
3. O Mestre clica em **Ativar**: o mapa aparece para o jogador, já com o token do personagem dele no canto superior esquerdo (anel dourado).
4. O jogador clica no próprio token: as células que ele alcança (6 por padrão = 30 ft, diagonal custa 1) ficam azuis, só na tela dele. Ao clicar em uma delas, o token anda, a barra sobre o mapa mostra "Movimento 3/6" e os botões **Concluir** e **Desfazer**. Desfazer volta o token e devolve o movimento; enquanto não concluir ou desfazer, ele não anda de novo.
5. O jogador tenta mover o inimigo: o mapa só se desloca. O Mestre arrasta qualquer token livremente (sem custo); um clique sem arrastar, em qualquer token, usa o mesmo movimento por blocos. **Nova rodada**, na aba Tokens, devolve o movimento a todos; **Restaurar movimento**, nos controles do token, a um só.
6. O Mestre e o jogador movem tokens diferentes para a mesma célula ao mesmo tempo: um deles volta à posição anterior com o aviso "Já existe um token nesta célula."
7. No painel do Mestre: **Duplicar** cria a cópia na célula livre mais próxima; **Editar**, **Remover** e **Editar HP** (no token de jogador) refletem na outra janela.
8. O jogador troca de personagem no Lobby (com a sessão encerrada) ou edita o personagem: o token muda de nome, ícone e cor sem sair do lugar.
9. O Mestre remove o jogador da mesa: o token dele some do mapa.
10. Pare a API (`docker compose stop api`) e suba de novo: as duas janelas mostram "Reconectando…" e voltam com o estado do servidor.

Navegação: arrastar o fundo, o botão do meio ou dois dedos desloca o mapa; arrastar com o botão direito gira (para os lados) e inclina (para cima e para baixo) a visão; scroll ou pinça aproxima; **Enquadrar** volta a mostrar o mapa inteiro; Esc cancela um arraste ou o movimento por blocos. O botão **?** (ou a tecla `?`) lista todos os atalhos.

### Imagens, estruturas e visão isométrica

Primeiro crie um arquivo falso, que tem extensão de imagem mas não é uma:

```bash
printf '#!/bin/sh\necho "isto nao e uma imagem"\n' > falso.png
```

Com o Mestre em uma janela e o jogador na outra, as duas na aba **Mapa**, com um mapa ativo:

1. No painel do Mestre, aba **Imagens**, clique em **Enviar imagens** e escolha de uma vez uma imagem de mapa, uma textura, um retrato e o `falso.png`. As três primeiras aparecem na galeria, com barra de progresso durante o envio; o `falso.png` fica com o aviso "O arquivo não é uma imagem PNG, JPEG, WebP ou GIF."
2. Renomeie uma foto `.jpg` para `.png` e envie: ela é aceita, porque o tipo vem do conteúdo. Uma imagem com mais de 5 MB ou de 4096 px é recusada com o motivo.
3. Na imagem do mapa, clique em **Usar como fundo**. Arraste no mapa para deslocar a imagem, ajuste a **Escala** até as células coincidirem com a grade e clique em **Salvar fundo**. O fundo aparece para o jogador.
4. Tente **Excluir** essa imagem: a exclusão é recusada, dizendo que ela é o fundo do mapa.
5. Clique em **Editar mapa**. Com **Parede**, arraste para desenhar um segmento; com **Porta** e **Pilar**, clique em uma célula. O jogador vê as estruturas surgirem.
6. Com **Selecionar**, clique na parede: arraste para mover, puxe uma alça amarela para redimensionar, e no painel troque a altura, a cor e a **Textura**. Teste **R** (gira), **Ctrl+D** (duplica), **Delete** (remove), **Ctrl+Z** e **Ctrl+Shift+Z**.
7. Volte para **Jogar**. O jogador clica no token: as células da parede e da porta fechada não ficam azuis, e o alcance contorna a parede (a diagonal não corta o canto entre duas células bloqueadas). O Mestre arrasta o mesmo token para dentro da parede e consegue.
8. O Mestre clica na porta: ela abre (fica baixa e translúcida) e o jogador passa. O clique do jogador na porta não faz nada.
9. Na aba **Tokens**, crie um token e escolha uma imagem em **Escolher da galeria**: o disco mostra a imagem com a borda colorida.
10. Em cada janela, clique em **Isométrica**: a câmera inclina e gira com transição suave, os tokens ficam em pé e a parede esconde o que está atrás dela. Uma janela não muda a visão da outra.
11. Ainda na visão isométrica, arraste um token e clique na porta: a célula certa é atingida. Gire com **Q** e **E** e repita.
12. Mova a **Inclinação** e troque a **Qualidade** para Baixa (sem sombras nem suavização); **Topo** volta à visão de cima.

No celular a qualidade começa em Baixa; a escolha de cada pessoa fica guardada no navegador dela.

### Névoa de guerra (três navegadores)

Três sessões, como na seção abaixo, todas na aba **Mapa**. O Mestre gera uma masmorra (**Gerar masmorra**) e a ativa.

1. Sem névoa (o padrão), os jogadores veem o mapa inteiro.
2. O Mestre abre a aba **Névoa** e escolhe **Compartilhada pelo grupo**: para os jogadores, tudo fica escuro menos o que os tokens deles veem; paredes e portas fechadas cortam a visão. O Mestre continua vendo tudo, com o resto sombreado.
3. Um jogador anda pela sala: o que ele deixa para trás fica escurecido, e um inimigo do Mestre fora da vista não aparece (nem é clicável). O outro jogador vê o mesmo.
4. O Mestre abre uma porta fechada: a sala do outro lado aparece conforme a linha de visão.
5. Na ficha do personagem (aba **Personagens**), troque **Visão** para 2: o alcance da visão do token diminui na hora.
6. Troque para **Individual por jogador**: cada jogador passa a ver só pelo próprio token e a ter a própria memória. Em **Ver como**, o Mestre escolhe um jogador e vê o que ele vê.
7. O Mestre edita um NPC e dá **Visão de aliado** 4: os jogadores enxergam também em volta dele.
8. **Revelar ou cobrir à mão**: com **Revelar**, arraste um retângulo; ele aparece para os jogadores. Com **Cobrir**, a área volta ao escuro, inclusive o explorado.
9. **Limpar memória**: o explorado some para todos; o que os tokens veem agora e o revelado à mão ficam.
10. Recarregue a janela de um jogador: a memória volta igual, vinda do servidor.

### HP, tokens ocultos e reconexão (três navegadores)

Use três sessões separadas: o Mestre em uma janela normal, um jogador em uma janela anônima e outro em um segundo navegador. Os três na aba **Mapa**, com um mapa ativo e um inimigo criado pelo Mestre.

1. **Dano e cura.** O Mestre seleciona o inimigo na aba **Tokens** e usa os botões `−5`, `+1` etc.: a barra sobre o token muda de tamanho e de cor nas três janelas. Os jogadores veem a barra, mas não os números.
2. **Valor livre.** No campo **Valor**, digite um dano maior que o HP e clique em **Dano**: o HP para em 0 e o token fica acinzentado, com um X e a palavra "caído", sem sair do mapa. Uma cura maior que o máximo para no máximo.
3. **O que os jogadores veem do HP.** Em **Jogadores veem do HP**, troque para "Barra e números" (os valores aparecem para os jogadores) e para "Nada" (a barra some para eles). Cada jogador sempre vê o HP completo do próprio token.
4. **Ocultar.** Clique em **Ocultar dos jogadores** (ou tecle `H`): o token some das janelas dos jogadores e fica translúcido, com a palavra "oculto", para o Mestre. **Revelar** o traz de volta.
5. **Célula de token oculto.** Com o inimigo oculto, um jogador move o próprio token por blocos para a célula dele: o movimento é aceito, sem aviso. Ao revelar, o inimigo aparece na célula livre ao lado.
6. **Token de jogador oculto.** O Mestre oculta o token de um jogador: o dono continua vendo e movendo o próprio token; o outro jogador deixa de vê-lo.
7. **Conferência de vazamento.** Em uma janela de jogador, abra as ferramentas do navegador (F12) → **Rede** → filtro **WS** → `mesa` → **Mensagens**. Repita os passos 1 a 6 e confira que nenhuma mensagem traz o nome do token oculto nem `hpAtual` com número em tokens que mostram só a barra.
8. **Queda de conexão.** Em uma janela de jogador, desligue o Wi-Fi (ou pare a API com `docker compose stop api`): aparece "A conexão com a mesa caiu", o indicador passa a "Reconectando…" e o mapa fica bloqueado. Depois de uns 40 segundos, o aviso muda para "Sem conexão com o servidor", com o botão **Tentar agora**.
9. **Volta.** Enquanto esse jogador está fora, o Mestre causa dano e move um token. Religue a rede (ou `docker compose start api`): a janela volta a "Ao vivo" sozinha, já com o HP e as posições novas.
10. **Celular.** Em um celular (ou com a janela estreita), o painel do Mestre fica atrás do botão **Painel do Mestre** e abre como gaveta lateral.

## Mapas e tokens (API)

| Método e rota | Quem | O que faz |
|---|---|---|
| `POST /api/mesas/{id}/mapas` | Mestre | cria um mapa (inativo) |
| `POST /api/mesas/{id}/mapas/procedural` | Mestre | cria um mapa (inativo) com uma masmorra gerada; `largura`, `altura` (12–100) e `semente` são opcionais, e a mesma semente gera o mesmo mapa |
| `GET /api/mesas/{id}/mapas` | membro | o Mestre vê todos; o jogador, só o ativo |
| `PUT /api/mapas/{id}` | Mestre | edita nome, dimensões e cor; 409 se algum token ficar fora dos novos limites |
| `DELETE /api/mapas/{id}` | Mestre | exclui o mapa e os tokens dele |
| `PUT /api/mapas/{id}/ativar` | Mestre | torna o mapa o ativo, desativa o anterior e traz os tokens dos jogadores |
| `GET /api/mapas/{id}/estado` | membro | mapa e tokens; para o jogador, só o mapa ativo existe |
| `POST /api/mapas/{id}/tokens` | Mestre | cria um token; sem `x` e `y`, na primeira célula livre |
| `PUT /api/tokens/{id}` | Mestre | edita o token; no token de jogador, só o HP atual |
| `POST /api/tokens/{id}/duplicar` | Mestre | copia para a célula livre mais próxima |
| `PUT /api/tokens/{id}/hp` | Mestre | dano e cura (`delta`), HP atual ou HP máximo; o resultado fica entre 0 e o máximo |
| `PUT /api/tokens/{id}/exibicao` | Mestre | oculta ou revela o token e define o que os jogadores veem do HP |
| `DELETE /api/tokens/{id}` | Mestre | remove o token |
| `PUT /api/mapas/{id}/fundo` | Mestre | define ou remove a imagem de fundo, a escala e o deslocamento |
| `PUT /api/mapas/{id}/nevoa` | Mestre | liga, desliga ou troca o modo da névoa (`Desligada`, `Compartilhada`, `Individual`) |
| `PUT /api/mapas/{id}/nevoa/revelada` | Mestre | o pincel: `{ celulas, revelar }` revela as células para todos ou as cobre (apagando também o explorado) |
| `DELETE /api/mapas/{id}/nevoa/memoria` | Mestre | esquece o que os jogadores exploraram; o revelado à mão fica |
| `POST /api/mapas/{id}/estruturas` | Mestre | cria uma estrutura (parede, porta, pilar, obstáculo ou chão) |
| `PUT /api/estruturas/{id}` | Mestre | edita a estrutura; leva a `versao` e devolve 409 em conflito |
| `PUT /api/estruturas/{id}/porta` | Mestre | abre ou fecha uma porta |
| `DELETE /api/estruturas/{id}` | Mestre | remove a estrutura |
| `POST /api/mesas/{id}/imagens` | Mestre | envia uma imagem (multipart, campo `arquivo`) |
| `GET /api/mesas/{id}/imagens` | Mestre | galeria da mesa e uso da cota |
| `GET /api/imagens/{id}` | membro | o arquivo, com `ETag` e cache privado |
| `GET /api/imagens/{id}/miniatura` | membro | miniatura de 256 px |
| `DELETE /api/imagens/{id}` | Mestre | exclui; 409 se a imagem estiver em uso |

Cada jogador da mesa com personagem ativo tem um token no mapa ativo, mantido pelo servidor: ele nasce quando o mapa é ativado ou o jogador entra na mesa, acompanha o personagem (troca e edição) e some quando o jogador sai. O Mestre não o duplica nem o remove.

### HP e visibilidade

O filtro é feito no servidor: o que um jogador não pode ver nunca sai para o navegador dele.

| | Mestre | Dono do token | Outros jogadores |
|---|---|---|---|
| Token oculto | vê | vê e move | não recebe nada: nem no estado, nem em eventos, nem em erros |
| Números de HP | sempre | sempre | só com "Barra e números" |
| Barra de HP (faixas de 5%) | sempre | sempre | com "Só a barra" ou "Barra e números" |
| Caído (HP 0) | vê | vê | vê |

- Só o Mestre altera HP. O HP atual fica entre 0 e o máximo; em 0 o token fica caído, mas continua no mapa.
- **O HP atual é da cena, não da ficha.** O HP máximo do personagem é copiado ao criar o token e acompanha a ficha; o HP atual do token não volta para o cadastro do personagem.
- Um token oculto não ocupa a célula para os jogadores, que podem parar em cima dele. Ao ser revelado em uma célula ocupada, ele vai para a célula livre mais próxima.
- Para um jogador, um token oculto responde como um token que não existe, em qualquer chamada.
- O lobby não mostra o HP máximo dos personagens dos outros: é o mesmo número do token.

### Imagens

- O tipo é detectado pelos primeiros bytes do arquivo (PNG, JPEG, WebP ou GIF); a extensão e o Content-Type enviados são ignorados.
- Toda imagem é decodificada e recodificada antes de ser gravada, o que remove EXIF e qualquer dado escondido depois da imagem. Nada do arquivo original é guardado como veio.
- O GIF animado mantém todos os quadros e anima no mapa (fundo e revestimento de estruturas), em navegadores com a API `ImageDecoder`; nos outros, aparece parado no primeiro quadro. Os demais formatos guardam só o primeiro quadro.
- O arquivo é salvo com um nome gerado pelo servidor (`{mesa}/{imagem}.{ext}`). O nome original aparece só na galeria, sanitizado.
- As imagens exigem login: o navegador as baixa com o token e as usa por uma URL local (blob). Por isso um link de imagem colado em outra aba não abre.
- Excluir uma imagem em uso (fundo de mapa, token ou textura) é recusado, com a lista de onde ela está.

### Estruturas

- Cada estrutura ocupa um retângulo de células inteiras. Porta fechada e estruturas marcadas como bloqueantes impedem que **jogadores** terminem o movimento nelas; o Mestre posiciona tokens em qualquer célula.
- O bloqueio vale para a célula de destino, não para o caminho: não há regra de deslocamento.
- O **chão** é uma área desenhada arrastando um retângulo no editor. Fica rente ao piso, por baixo dos tokens, cobrindo a grade e o fundo; aceita cor ou textura (um ladrilho por célula) e nasce sem bloquear. A altura não se aplica a ele.
- Um token que já estava em uma célula quando ela foi bloqueada continua lá e pode sair.
- O desfazer do editor é local e vale até recarregar a página.

### Texturas prontas

- `GET /api/texturas-prontas` devolve o catálogo: temas (masmorra, castelo, caverna, vila, interiores, natureza, neve, deserto, vulcão) e texturas com as categorias (`tema`, `estruturas` em que servem e `papeis` no gerador). O seletor do Mestre mostra só as do tipo da estrutura selecionada.
- `POST /api/mesas/{id}/mapas/procedural` aceita `tema` (um dos temas geráveis); o gerador escolhe piso, corredor, parede, rocha, porta, pilar, obstáculos e poças de perigo entre as texturas do tema.
- Os ladrilhos são dos pacotes da Kenney (CC0). Os pacotes completos ficam em `recursos/kenney/*.zip`; os PNGs em `frontend/public/texturas` e o catálogo em `backend/src/Vtt.Domain/Mapas/texturas-prontas.json` são gerados por:

  ```bash
  python3 scripts/texturas/montar_texturas.py
  ```

  Para incluir uma textura, acrescente uma linha em `CATALOGO` no script e rode-o de novo. O teste `TexturasTests` confere que o catálogo e os PNGs batem.

### Névoa de guerra

- **Opcional por mapa.** Nasce desligada; o Mestre escolhe na aba **Névoa** do painel. **Compartilhada:** cada jogador vê o que qualquer token do grupo vê, e o explorado é um só. **Individual:** cada um vê pelo próprio token, com a própria memória.
- **Alcance.** Vem da ficha (**Visão**, em células, contadas como o movimento: 12 = 60 ft) e acompanha o token do jogador. Um token do Mestre com **Visão de aliado** maior que 0 empresta a visão aos jogadores, nos dois modos.
- **Linha de visão.** Paredes, pilares e portas fechadas tapam a visão; porta aberta, chão e obstáculos com menos de 1 célula de altura, não. Quem está colado à parede vê a parede.
- **Três estados por célula.** À vista: tudo aparece. Explorada: o chão e as estruturas aparecem escurecidos, sem os tokens que estão lá agora. Nunca vista: escuro. O destaque do movimento por blocos só cobre o que o jogador já viu.
- **O Mestre vê tudo**, com o que o grupo não vê sombreado. Em **Ver como** ele vê a névoa do grupo ou de um jogador, só na tela dele. **Revelar ou cobrir à mão** abre o pincel: arrastar marca um retângulo, revelado para todos ou coberto ao soltar.
- **É visual.** O cálculo da visão fica no navegador; o servidor guarda só a memória (um bit por célula) e a repassa. Quem abrir as ferramentas do navegador vê o mapa inteiro. Veja [docs/revisao-de-autorizacao.md](docs/revisao-de-autorizacao.md).

### Biblioteca de tokens

- **É do usuário, não da mesa.** Em **Biblioteca** (menu do topo), o Mestre guarda modelos de monstros e NPCs em categorias que ele mesmo cria (NPCs, Vilões, Inimigos…). Com a biblioteca vazia, um botão cria essas três.
- **Colocar no mapa.** Na aba **Tokens** do painel, **Da biblioteca** abre a lista. O Mestre escolhe o modelo, a quantidade (1 a 20) e, se quiser, **ocultos dos jogadores**. Os tokens nascem com HP cheio nas primeiras células livres; com mais de um, os nomes ganham número e continuam a numeração do mapa (Goblin 3, Goblin 4…). Se não houver célula livre para todos, nenhum é criado.
- **O token é uma cópia.** Editar ou excluir o modelo não muda os tokens que já estão nos mapas.
- **Imagens.** O modelo usa imagens da galeria pessoal (`usuarios/{usuario}/{imagem}.{ext}`), que só o dono vê. Na primeira vez que o modelo vai para uma mesa, a imagem é copiada para a galeria dessa mesa, conta na cota dela, e todos os tokens do modelo naquela mesa usam essa cópia.
- **Excluir.** Excluir uma categoria deixa os modelos dela em "Sem categoria". Uma imagem pessoal usada por um modelo não pode ser excluída.

| Rota | O que faz |
|---|---|
| `GET/POST /api/biblioteca/categorias`, `PUT/DELETE /api/biblioteca/categorias/{id}` | Categorias (nome único por usuário, sem diferenciar maiúsculas; até 50). |
| `POST /api/biblioteca/categorias/sugeridas` | Cria as que faltarem entre NPCs, Vilões e Inimigos. |
| `GET/POST /api/biblioteca/modelos`, `GET/PUT/DELETE /api/biblioteca/modelos/{id}` | Modelos (`Inimigo` ou `Npc`; até 500). |
| `GET/POST /api/biblioteca/imagens` | Galeria pessoal. Baixar e excluir usam `/api/imagens/{id}`. |
| `POST /api/mapas/{id}/tokens/da-biblioteca` | `{ modeloId, quantidade, visivel }`: só o Mestre da mesa, com um modelo dele. |

## Tempo real

- Hub em `/hubs/mesa`, autenticado com o mesmo JWT (enviado em `?access_token=` só nas rotas `/hubs`).
- O cliente chama `EntrarNaMesa(mesaId)` ao conectar e a cada reconexão, e recebe o estado completo do lobby e do mapa ativo; o estado local é substituído por inteiro.
- Eventos do lobby: `MembroEntrou`, `MembroSaiu`, `MembroOnline`, `MembroOffline`, `PersonagemAtivoAlterado`, `SessaoAlterada`, `MesaExcluida` e `RemovidoDaMesa` (só para quem foi removido).
- Eventos do mapa: `TokenCriado`, `TokenMovido`, `TokenAtualizado`, `TokenRemovido`, `EstruturaAdicionada`, `EstruturaAtualizada`, `EstruturaRemovida`, `MapaAtivoAlterado` (com o estado completo do novo mapa, ou `null`), `MapaAtualizado` e `MemoriaDeNevoaAlterada` (uma camada inteira da névoa; a memória individual vai só ao dono e ao Mestre). Os de um mapa em preparação vão só para o Mestre.
- **Névoa:** o cliente do jogador calcula o que os tokens dele veem e, quando aparece algo fora da memória, chama `ExplorarCelulas(mapaId, celulas)` (base64, um bit por célula). O servidor une à memória (do grupo ou do jogador) e avisa só se algo mudou.
- A câmera (inclinação, giro, zoom) e a qualidade gráfica são de cada usuário e nunca são enviadas ao servidor.
- As mudanças passam pela API REST e os serviços avisam o grupo depois de gravar. A exceção é o movimento, por métodos do Hub que respondem a quem chamou com `{ aceito, motivo, token }`: `MoverToken(tokenId, x, y, versaoEsperada)` (arraste livre, só do Mestre) e o movimento por blocos — `MoverPorBlocos(tokenId, x, y, versaoEsperada)`, `DesfazerMovimento(tokenId, versaoEsperada)` e `ConcluirMovimento(tokenId, versaoEsperada)` (dono ou Mestre). `NovaRodada(mapaId)` e `RestaurarMovimento(tokenId)` são só do Mestre.
- O servidor valida todo movimento: dono do token (o Mestre move qualquer um), limites do mapa, célula livre e versão. No movimento por blocos, também o alcance (BFS em 8 direções, custo 1 por passo, sem atravessar estruturas que bloqueiam nem tokens) contra o `MovimentoRestante` do token; o custo é descontado e a origem fica guardada até concluir ou desfazer. Na disputa entre dois movimentos simultâneos, quem decide é o banco (versão do token e índice único da célula).
- O movimento restante e a pendência chegam pelo evento `MovimentoAlterado`, só ao Mestre e ao dono do token.
- Os métodos de movimento são limitados a 20 chamadas por segundo por conexão, cada um (`RateLimiting__MovimentosPorSegundo`); os demais métodos do Hub, a 5 por segundo. Mensagens de cliente acima de 32 kB são recusadas.
- Os eventos de token (`TokenCriado`, `TokenAtualizado`, `TokenMovido`, `TokenRemovido`, `HpAlterado`) chegam a cada destinatário já filtrados. O Mestre fica em um grupo, os jogadores em outro, e o dono de um token recebe a versão dele.
- **Reconexão:** o cliente tenta voltar depois de 0, 2, 5, 10 e 20 segundos e, daí em diante, a cada 20 segundos. Ao reconectar, ao voltar de segundo plano e ao recuperar a rede, ele chama `EntrarNaMesa` e troca o estado local pelo do servidor. Sem conexão, o mapa e o painel ficam bloqueados.
- **Login:** o token é renovado sozinho quando passa de dois terços da validade (`POST /api/auth/renovar`). Quem ficou fora além da validade vai para o login e, entrando, volta à mesma mesa.
- **Limitação:** a presença (quem está online) e o limite de movimentos ficam na memória do processo. Reiniciar a API zera a presença, que se refaz quando os clientes reconectam; rodar mais de uma instância exigiria um backplane (ex.: Redis).

## Testes

Os testes de integração sobem a API em memória contra um PostgreSQL real em container (Testcontainers), então o Docker precisa estar em execução.

```bash
cd backend
dotnet test
```

## Desenvolvimento sem rebuild da imagem

Com hot reload no frontend e a API rodando pelo SDK:

```bash
# 1) só o banco
docker compose up -d postgres

# 2) API em http://localhost:5080 (segredos locais ficam fora do repositório)
cd backend/src/Vtt.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=vtt;Username=vtt;Password=<POSTGRES_PASSWORD do .env>"
dotnet user-secrets set "Jwt:Secret" "<uma chave com 32+ caracteres>"
dotnet run

# 3) frontend em http://localhost:5173, com proxy de /api para a API
cd frontend
npm install
API_PROXY_TARGET=http://localhost:5080 npm run dev
```

Sem `API_PROXY_TARGET`, o proxy do Vite aponta para `http://localhost:8080` (a API do Docker Compose).

### Migrations

```bash
cd backend
dotnet ef migrations add <Nome> --project src/Vtt.Infrastructure --startup-project src/Vtt.Api --output-dir Persistence/Migrations
```

## Backup e restauração

Com os containers no ar:

```bash
scripts/backup.sh              # cria backups/vtt-banco-<data>.dump e backups/vtt-uploads-<data>.tar.gz
scripts/backup.sh /caminho     # ou em outra pasta
```

O backup não interrompe a mesa. Guarde os dois arquivos juntos, fora da máquina (nuvem, HD externo).

Para restaurar, na máquina que vai receber os dados e com os containers no ar:

```bash
scripts/restaurar.sh backups/vtt-banco-<data>.dump backups/vtt-uploads-<data>.tar.gz
```

O script pede confirmação, **substitui** o banco e as imagens atuais pelos do backup, deixa a API fora do ar por alguns segundos e a sobe de novo. Para restaurar em uma máquina nova: clone o projeto, crie o `.env`, rode `docker compose up -d` e então o `restaurar.sh`. Use o mesmo `JWT_SECRET` se quiser que os logins abertos continuem valendo; a senha do banco pode ser outra.

No Windows há `scripts\backup.ps1` e `scripts\restaurar.ps1` (`-Banco` e `-Imagens`), com o mesmo resultado.

Para um backup automático diário no Linux ou no macOS, `crontab -e`:

```
0 4 * * * cd /caminho/do/projeto && scripts/backup.sh /caminho/dos/backups >> /caminho/dos/backups/backup.log 2>&1
```

## Abrir a mesa para os amigos com ngrok

O ngrok cria um endereço público `https://` que leva até a porta da aplicação na sua máquina. Seus amigos acessam pelo navegador; nada precisa ser instalado do lado deles.

### 1. Pré-requisitos

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) instalado e aberto.
- Conta gratuita em [ngrok.com](https://ngrok.com) e o authtoken dela (em *Your Authtoken*, no painel). O ngrok roda como um serviço do compose; não é preciso instalar o programa.

### 2. Configurar o `.env`

```bash
cp .env.example .env
```

Edite o `.env`:

- `POSTGRES_PASSWORD`: uma senha forte (sem `;` nem aspas).
- `JWT_SECRET`: gere com `openssl rand -base64 48`.
- `ASPNETCORE_ENVIRONMENT=Production` (desliga o Swagger).
- `NGROK_AUTHTOKEN`: o token da sua conta.
- `NGROK_DOMAIN`: opcional; o domínio fixo da conta, se quiser escolher qual usar.

### 3. Subir e pegar o endereço público

```bash
docker compose --profile ngrok up --build -d
docker compose --profile ngrok ps        # espere a API ficar "healthy"
```

O serviço `ngrok` sobe depois da API e encaminha para ela pela rede interna do compose. O endereço público aparece no painel, em `http://localhost:4040` (ou na porta de `NGROK_INSPECT_PORT`), ou com:

```bash
curl -s http://localhost:4040/api/tunnels | grep -o '"public_url":"[^"]*"'
```

Crie a sua conta de Mestre, gere o convite acessando pelo endereço público e mande o link. Com `PUBLIC_BASE_URL` vazia, o convite sai com o endereço pelo qual você está acessando; se gerar por `localhost`, o link sai com `localhost`. Para fixar, preencha `PUBLIC_BASE_URL=https://<endereço>` e rode `docker compose up -d api`.

Para fechar a mesa sem derrubar a aplicação: `docker compose --profile ngrok stop ngrok`. Sem `--profile ngrok`, o `docker compose up -d` sobe só o banco e a API.

### 4. O que o plano gratuito muda

- **Um túnel por vez.** A conta gratuita aceita uma sessão do ngrok de cada vez e tem um domínio fixo. Se outro projeto seu usa o mesmo token, pare o ngrok dele antes de subir este.
- **Endereço.** As contas atuais já recebem um domínio fixo (`<algo>.ngrok-free.dev`), usado automaticamente. Em contas sem ele, o endereço muda a cada subida do serviço `ngrok` e os links antigos deixam de abrir (os códigos dos convites continuam válidos; só o endereço muda).
- **Página de aviso do ngrok.** Na primeira vez que alguém abre o link em um navegador, o ngrok mostra uma página dizendo que o site é servido por ele, com o botão **Visit Site**. É normal: o amigo clica e segue para o login. O aviso volta a aparecer de tempos em tempos e não há como tirá-lo no plano gratuito para a abertura da página.
- **Dentro da aplicação o aviso não atrapalha.** Todas as chamadas da aplicação (API, upload e a negociação do tempo real) enviam o cabeçalho `ngrok-skip-browser-warning`, que pede ao ngrok a resposta de verdade no lugar da página de aviso.
- **Limites.** O plano gratuito tem teto de conexões e de tráfego por mês. Uma mesa de amigos fica bem abaixo, mas mapas muito pesados contam; prefira imagens JPEG ou WebP.

### 5. Conferir o WebSocket

O tempo real funciona melhor por WebSocket. Para conferir, em uma janela conectada pelo endereço do ngrok:

1. Abra as ferramentas do navegador (F12) → **Rede** → filtro **WS** e recarregue a página da mesa.
2. Deve aparecer uma linha `mesa?id=...` com status **101**. Em **Mensagens** passam os eventos da mesa.

Se o WebSocket não subir, o SignalR cai sozinho para *long polling*: a mesa funciona, mas com mais atraso. Sinais disso: nenhuma linha no filtro **WS** e, no filtro **Fetch/XHR**, pedidos `mesa?id=...` repetidos sem parar. Causas comuns: extensão de navegador ou antivírus que bloqueia WebSocket, rede corporativa ou de faculdade, e VPN. Teste em outra rede ou em uma janela anônima sem extensões. No painel do ngrok (`http://localhost:4040`), um pedido `GET /hubs/mesa` com resposta `101 Switching Protocols` confirma o WebSocket.

### 6. Checklist de segurança antes de mandar o link

- [ ] `JWT_SECRET` gerado por você, com 32 caracteres ou mais, e fora de qualquer repositório.
- [ ] `POSTGRES_PASSWORD` trocada (o valor do `.env.example` é só um modelo). Ela vale na primeira subida do volume; para trocar depois, faça backup, `docker compose down -v`, suba de novo e restaure.
- [ ] `ASPNETCORE_ENVIRONMENT=Production`: `https://<endereço>/swagger` deve responder 404.
- [ ] A porta do PostgreSQL não está exposta: o `docker-compose.yml` a publica só em `127.0.0.1`, e o ngrok encaminha apenas a API.
- [ ] O limite de tentativas de login e cadastro está ativo (10 por minuto por IP, o padrão). Onze tentativas seguidas de login com senha errada devem terminar em "Muitas tentativas".
- [ ] Você já criou a sua conta de Mestre. Qualquer pessoa com o endereço pode criar conta, mas só entra em uma mesa quem tem convite.
- [ ] Os convites têm validade ou limite de usos, e você revoga os que não usa mais.
- [ ] `scripts/backup.sh` rodou pelo menos uma vez e você sabe onde os arquivos estão.
- [ ] Ao terminar a sessão, pare o ngrok (`docker compose --profile ngrok stop ngrok`): o endereço deixa de responder.

### 7. Sessão de teste com três navegadores

Antes da primeira sessão de verdade, faça um ensaio pelo endereço do ngrok, não por `localhost`:

1. **Mestre** (janela normal): entre, crie a mesa, um mapa e um inimigo; ative o mapa; gere um convite.
2. **Jogador 1** (janela anônima) e **Jogador 2** (outro navegador, ou o celular pela rede móvel): abram o link do convite, passem pela página de aviso do ngrok, criem conta e personagem e entrem na mesa.
3. Confira, nas três janelas:
   - o lobby mostra os três como "Online";
   - cada jogador tem o próprio token, com anel dourado, e só consegue mover o dele;
   - um movimento aparece nas outras duas janelas em menos de um segundo;
   - dano do Mestre mexe a barra para todos, sem números para os jogadores;
   - um token oculto pelo Mestre some para os dois jogadores;
   - uma imagem enviada pelo Mestre como fundo aparece para os jogadores;
   - no celular, pinça e dois dedos aproximam e deslocam o mapa, e a visão isométrica funciona;
   - o passo 5 acima (WebSocket com status 101) em pelo menos uma janela de jogador.
4. Pare o serviço `ngrok` por alguns segundos e suba de novo com o mesmo domínio: as três janelas mostram "Reconectando…" e voltam sozinhas. Com endereço aleatório, o endereço muda e todos precisam do link novo.

### 8. Quando sair do ngrok: plano para uma VPS

Se a mesa virar fixa, uma VPS pequena (1 vCPU, 1 a 2 GB de memória) dá endereço próprio e funciona com a sua máquina desligada.

1. **Domínio:** registre um e aponte um registro `A` para o IP da VPS.
2. **Servidor:** instale o Docker, clone o projeto, crie o `.env` (segredos novos, `ASPNETCORE_ENVIRONMENT=Production`, `PUBLIC_BASE_URL=https://mesa.seudominio.com`).
3. **HTTPS:** coloque o [Caddy](https://caddyserver.com) na frente, como mais um serviço do compose, publicando as portas 80 e 443 e encaminhando para `api:8080`. Ele obtém e renova o certificado sozinho, e repassa WebSocket sem configuração extra. O `Caddyfile` inteiro:

   ```
   mesa.seudominio.com {
       reverse_proxy api:8080
   }
   ```

   Com o Caddy na frente, tire a linha `ports` do serviço `api`: só o Caddy fica exposto. O Traefik faz o mesmo papel, com rótulos nos serviços.
4. **Firewall:** libere só 22 (SSH), 80 e 443.
5. **Volumes:** os dados ficam nos volumes `vtt_postgres-data` e `vtt_uploads`, em `/var/lib/docker/volumes/` na VPS. Acrescente um volume para o Caddy guardar os certificados.
6. **Backups:** agende `scripts/backup.sh` no `cron` e copie os arquivos para fora da VPS (outro servidor ou armazenamento em nuvem). Ensaie uma restauração.
7. **Migração dos dados:** faça o backup na sua máquina, copie os dois arquivos para a VPS e rode `scripts/restaurar.sh` lá.

Com mais de uma instância da API seria preciso um *backplane* do SignalR (Redis), porque a presença e os limites de chamadas ficam na memória do processo. Para uma mesa, uma instância basta.

## Segurança

A revisão de autorização de todos os endpoints e métodos do Hub está em [docs/revisao-de-autorizacao.md](docs/revisao-de-autorizacao.md). Em resumo:

- Toda rota exige login, menos cadastro, login, `/health` e os arquivos estáticos da aplicação.
- O papel é conferido no serviço de cada operação, contra o banco, e nunca vem do cliente.
- Quem não é membro de uma mesa recebe 404 para tudo o que é dela; o jogador recebe 403 nas ações do Mestre, e 404 para o que ele não pode saber que existe (mapas em preparação, tokens ocultos).
- Os logs registram mesa criada e excluída, entrada e saída de jogadores, tentativas sem permissão e uploads recusados, sempre por id de usuário. Senhas e tokens de login não são registrados.

## O que ficou de fora e o que fazer depois

Fora do MVP, na ordem em que eu implementaria:

1. **Rolagem de dados.** Pequena e de uso constante: um método no Hub, resultado gerado no servidor e enviado ao grupo. Rolagens secretas do Mestre reaproveitam os grupos por papel desta etapa.
2. **Chat e registro da sessão.** A rolagem precisa de um lugar para aparecer; o chat nasce como esse registro, com mensagens de texto e sussurros ao Mestre.
3. **Iniciativa e turnos.** Ordem de combate com o token da vez destacado. Usa as rolagens e abre caminho para regras de deslocamento por turno, que hoje não existem (o bloqueio vale só para a célula de destino).
4. **Fog of war.** O maior dos quatro: exige visão por jogador calculada no servidor a partir das estruturas, e estende o filtro por destinatário desta etapa de "token inteiro" para "o que cada um enxerga". Fica por último porque depende de tudo o que veio antes estar estável.

Também ficaram de fora: testes automatizados do frontend; desfazer do editor persistido entre recarregamentos; várias instâncias da API (exigiria Redis); revogação de logins antes do vencimento.

## Estruturas

- Cada estrutura ocupa um retângulo de células inteiras. Porta fechada e estruturas marcadas como bloqueantes impedem que **jogadores** terminem o movimento nelas; o Mestre posiciona tokens em qualquer célula.
- O bloqueio vale para a célula de destino, não para o caminho: não há regra de deslocamento.
- Um token que já estava em uma célula quando ela foi bloqueada continua lá e pode sair.
- O desfazer do editor é local e vale até recarregar a página.

## Tempo real

- Hub em `/hubs/mesa`, autenticado com o mesmo JWT (enviado em `?access_token=` só nas rotas `/hubs`).
- O cliente chama `EntrarNaMesa(mesaId)` ao conectar e a cada reconexão, e recebe o estado completo do lobby e do mapa ativo; o estado local é substituído por inteiro.
- Eventos do lobby: `MembroEntrou`, `MembroSaiu`, `MembroOnline`, `MembroOffline`, `PersonagemAtivoAlterado`, `SessaoAlterada`, `MesaExcluida` e `RemovidoDaMesa` (só para quem foi removido).
- Eventos do mapa: `TokenCriado`, `TokenMovido`, `TokenAtualizado`, `TokenRemovido`, `EstruturaAdicionada`, `EstruturaAtualizada`, `EstruturaRemovida`, `MapaAtivoAlterado` (com o estado completo do novo mapa, ou `null`), `MapaAtualizado` e `MemoriaDeNevoaAlterada` (uma camada inteira da névoa; a memória individual vai só ao dono e ao Mestre). Os de um mapa em preparação vão só para o Mestre.
- **Névoa:** o cliente do jogador calcula o que os tokens dele veem e, quando aparece algo fora da memória, chama `ExplorarCelulas(mapaId, celulas)` (base64, um bit por célula). O servidor une à memória (do grupo ou do jogador) e avisa só se algo mudou.
- A câmera (inclinação, giro, zoom) e a qualidade gráfica são de cada usuário e nunca são enviadas ao servidor.
- As mudanças passam pela API REST e os serviços avisam o grupo depois de gravar. A exceção é o movimento, por métodos do Hub que respondem a quem chamou com `{ aceito, motivo, token }`: `MoverToken(tokenId, x, y, versaoEsperada)` (arraste livre, só do Mestre) e o movimento por blocos — `MoverPorBlocos(tokenId, x, y, versaoEsperada)`, `DesfazerMovimento(tokenId, versaoEsperada)` e `ConcluirMovimento(tokenId, versaoEsperada)` (dono ou Mestre). `NovaRodada(mapaId)` e `RestaurarMovimento(tokenId)` são só do Mestre.
- O servidor valida todo movimento: dono do token (o Mestre move qualquer um), limites do mapa, célula livre e versão. No movimento por blocos, também o alcance (BFS em 8 direções, custo 1 por passo, sem atravessar estruturas que bloqueiam nem tokens) contra o `MovimentoRestante` do token; o custo é descontado e a origem fica guardada até concluir ou desfazer. Na disputa entre dois movimentos simultâneos, quem decide é o banco (versão do token e índice único da célula).
- O movimento restante e a pendência chegam pelo evento `MovimentoAlterado`, só ao Mestre e ao dono do token.
- Os métodos de movimento são limitados a 20 chamadas por segundo por conexão, cada um (`RateLimiting__MovimentosPorSegundo`); os demais métodos do Hub, a 5 por segundo. Mensagens de cliente acima de 32 kB são recusadas.
- Os eventos de token (`TokenCriado`, `TokenAtualizado`, `TokenMovido`, `TokenRemovido`, `HpAlterado`) chegam a cada destinatário já filtrados. O Mestre fica em um grupo, os jogadores em outro, e o dono de um token recebe a versão dele.
- **Reconexão:** o cliente tenta voltar depois de 0, 2, 5, 10 e 20 segundos e, daí em diante, a cada 20 segundos. Ao reconectar, ao voltar de segundo plano e ao recuperar a rede, ele chama `EntrarNaMesa` e troca o estado local pelo do servidor. Sem conexão, o mapa e o painel ficam bloqueados.
- **Login:** o token é renovado sozinho quando passa de dois terços da validade (`POST /api/auth/renovar`). Quem ficou fora além da validade vai para o login e, entrando, volta à mesma mesa.
- **Limitação:** a presença (quem está online) e o limite de movimentos ficam na memória do processo. Reiniciar a API zera a presença, que se refaz quando os clientes reconectam; rodar mais de uma instância exigiria um backplane (ex.: Redis).

## Testes

Os testes de integração sobem a API em memória contra um PostgreSQL real em container (Testcontainers), então o Docker precisa estar em execução.

```bash
cd backend
dotnet test
```

## Desenvolvimento sem rebuild da imagem

Com hot reload no frontend e a API rodando pelo SDK:

```bash
# 1) só o banco
docker compose up -d postgres

# 2) API em http://localhost:5080 (segredos locais ficam fora do repositório)
cd backend/src/Vtt.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=vtt;Username=vtt;Password=<POSTGRES_PASSWORD do .env>"
dotnet user-secrets set "Jwt:Secret" "<uma chave com 32+ caracteres>"
dotnet run

# 3) frontend em http://localhost:5173, com proxy de /api para a API
cd frontend
npm install
API_PROXY_TARGET=http://localhost:5080 npm run dev
```

Sem `API_PROXY_TARGET`, o proxy do Vite aponta para `http://localhost:8080` (a API do Docker Compose).

### Migrations

```bash
cd backend
dotnet ef migrations add <Nome> --project src/Vtt.Infrastructure --startup-project src/Vtt.Api --output-dir Persistence/Migrations
```

## Expor com ngrok (resumo)

Preencha `NGROK_AUTHTOKEN` no `.env`, defina `ASPNETCORE_ENVIRONMENT=Production` e rode:

```bash
docker compose --profile ngrok up --build -d
```

O endereço público aparece em `http://localhost:4040`. A API já lê os cabeçalhos `X-Forwarded-*` do túnel. O passo a passo está em [Abrir a mesa para os amigos com ngrok](#abrir-a-mesa-para-os-amigos-com-ngrok).

## Estrutura

```
backend/
  src/
    Vtt.Domain/          entidades e enums, sem dependências
    Vtt.Application/     serviços por feature, DTOs, validadores, interfaces
    Vtt.Infrastructure/  EF Core, migrations, Identity, JWT
    Vtt.Api/             controllers, Hubs/ (SignalR), Program.cs, serve o frontend (wwwroot)
  tests/
    Vtt.Api.IntegrationTests/
frontend/
  src/
    app/        rotas, layout, rota protegida
    features/   auth/, personagens/, mesas/, mapa/
    shared/     api/, icones/, imagens/, realtime/, ui/
```
