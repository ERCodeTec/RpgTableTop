# Etapa 3: mapa em grid, tokens e movimento sincronizado

Data: 2026-10-09. Estado: implementado.

## Objetivo

O Mestre cria e ativa mapas em grid e gerencia tokens. Cada jogador da mesa recebe um token do seu personagem ativo e o move com snap ao grid. Todos veem os movimentos em tempo real. O servidor é a única autoridade sobre quem move o quê, limites do mapa, célula livre e versão.

## Fora do escopo

- Upload de imagens, estruturas e inclinação isométrica (Etapa 4).
- Edição de HP em tempo real pelos jogadores e filtro de visibilidade (Etapa 5). Os campos `HpAtual`, `HpMaximo` e `Visivel` já existem no modelo; `Visivel` não tem efeito nesta etapa.
- Preview efêmero do arraste para os outros jogadores.
- Regras de deslocamento por turno.
- Suíte de testes no frontend.

## Decisões confirmadas com o usuário

1. "Jogador na mesa" significa **membro da mesa**, online ou não. O token não some quando a conexão cai.
2. Tokens de jogador são **geridos só pelo servidor**: um por jogador, sem duplicar nem excluir pelo Mestre. O `POST` com `Tipo=Personagem` cria um token avulso com ícone de classe e sem dono.
3. Editar o personagem **sincroniza** o token; excluir o personagem remove o token.
4. **Sem preview** de arraste nesta etapa.

## Decisões tomadas sem consulta

- Operações do Mestre via REST + `IHubContext`, como na Etapa 2: as regras ficam nos serviços e o Hub só transporta. Apenas `MoverToken` é método do Hub.
- Zustand para o estado do mapa no cliente: permite que cada token assine só a própria fatia, sem re-render do canvas.
- Só membros com papel `Jogador` ganham token automático.
- Ao ativar outro mapa, o token do jogador é movido para ele (preserva `HpAtual`) e reposicionado no spawn.
- Eventos de mapa inativo vão só ao Mestre, para não revelar encontros em preparação.
- A concorrência do movimento é decidida pelo banco (índice único e versão), sem estado em memória.
- `MoverToken` responde ao chamador com um resultado, em vez de um evento separado de rejeição.
- Mapa cheio: criar e duplicar devolvem 409; o token automático é pulado com log, sem impedir a entrada do jogador.
- Excluir o mapa ativo é permitido; a mesa fica sem mapa ativo.
- Sem `@react-three/drei`: a câmera ortográfica é nativa do r3f e o pan/zoom é próprio.
- Arrastar o fundo com o botão principal (ou um dedo) também desloca o mapa, além do botão do meio e dos dois dedos.
- O token sob o ponteiro é achado pela célula, sem raycast contra as malhas: como não há sobreposição, é exato na visão superior. A Etapa 4, com tokens em pé, vai precisar de raycast.
- `Visivel` não aparece na interface enquanto não tiver efeito.
- Sem repositório git na pasta: spec e plano ficam como arquivos, sem commit.

## Modelo de dados

Migration `MapasETokens`.

### Mapa (tabela `mapas`)

| Campo | Tipo | Regra |
|---|---|---|
| Id | Guid | gerado pelo servidor |
| MesaId | Guid | FK para `mesas`, cascata |
| Nome | string | obrigatório, até 80 |
| Largura | int | 5 a 100 células |
| Altura | int | 5 a 100 células |
| Ativo | bool | padrão false |
| CorFundo | string | `#RRGGBB`, padrão `#1B2A2F` |
| CriadoEm | DateTimeOffset | |

- Índice único parcial em `mesa_id` com filtro `ativo`: no máximo um mapa ativo por mesa.
- Check constraints para largura e altura entre 5 e 100.

### Token (tabela `tokens`)

| Campo | Tipo | Regra |
|---|---|---|
| Id | Guid | gerado pelo servidor |
| MapaId | Guid | FK para `mapas`, cascata |
| Tipo | enum `TipoToken` | `Personagem`, `Inimigo`, `Npc`; gravado como texto |
| Nome | string | obrigatório, até 60 |
| HpAtual | int | 0 a `HpMaximo` |
| HpMaximo | int | 1 a 999 |
| X, Y | int | célula, dentro do mapa |
| CorBorda | string | `#RRGGBB` |
| Icone | string | classe de D&D para `Personagem`; chave genérica para os demais |
| PersonagemId | Guid? | FK para `personagens`, `SetNull` |
| DonoUsuarioId | Guid? | preenchido só nos tokens automáticos de jogador |
| Visivel | bool | padrão true, sem efeito nesta etapa |
| Versao | int | começa em 1, incrementa a cada alteração, token de concorrência |

- Índice único em `(mapa_id, x, y)`: sem sobreposição.
- Índice único parcial em `(mapa_id, dono_usuario_id)` com filtro `dono_usuario_id IS NOT NULL`: um token por jogador.
- Check constraint `hp_atual >= 0 AND hp_atual <= hp_maximo`.

### Ícones genéricos

Chaves aceitas pelo servidor: `goblin`, `esqueleto`, `dragao`, `lobo`, `humanoide`, `besta`, `morto-vivo`, `npc`. Os SVGs são originais e vivem no frontend; o servidor conhece só as chaves.

## Backend

### Organização

- `Vtt.Domain/Mapas/`: `Mapa`, `Token`, `TipoToken`, `IconesGenericos`.
- `Vtt.Application/Mapas/`: `IMapaService`/`MapaService`, `ITokenService`/`TokenService`, `TokensDeJogador`, `IMapaNotifier`, `MapaDtos`, validadores, `Grade` (busca de célula livre).
- `Vtt.Infrastructure/Persistence/Configurations/MapaConfiguration.cs` e a migration.
- `Vtt.Api/Controllers/`: `MapasController` (rotas `/api/mapas/...`), `TokensController` (rotas `/api/tokens/...`); as rotas `/api/mesas/{id}/mapas` entram no `MesasController`.
- `Vtt.Api/Hubs/`: `MapaNotifier`, `LimitadorDeMovimento`; `MesaHub` e `IMesaClient` ganham o que é novo.

### Alterações em código existente

Todas são acréscimos; nenhum comportamento atual muda.

- `IAppDbContext` e `AppDbContext`: `DbSet<Mapa>` e `DbSet<Token>`.
- `ConviteService` (aceitar): cria o token do novo jogador.
- `MesaService`: trocar personagem atualiza o token; remover membro remove o token.
- `PersonagemService`: editar sincroniza os tokens ligados; excluir os remove.
- `LobbyDto`: novo campo `MapaAtivo` (`MapaEstadoDto?`).
- `MesaHub`: `EntrarNaMesa` preenche `MapaAtivo`; novo método `MoverToken`.
- `RateLimitingOptions`: `MovimentosPorSegundo` (padrão 20).

### TokensDeJogador

Único lugar com a regra do token automático. Usa `UPDATE`/`DELETE` diretos (`ExecuteUpdate`/`ExecuteDelete`), que não disputam a versão com um `MoverToken` em andamento. Criar e atualizar rodam logo depois de o serviço chamador gravar e nunca fazem a operação principal falhar; remover roda na mesma transação da saída do jogador ou da exclusão do personagem, e o aviso sai depois do commit.

| Gatilho | Efeito |
|---|---|
| Jogador aceita convite | cria o token no mapa ativo, se houver |
| Troca de personagem ativo | atualiza nome, ícone, cor, `HpMaximo` e `PersonagemId`; `HpAtual` volta ao máximo; posição mantida. Se não havia token, cria |
| Personagem editado | atualiza nome, ícone, cor e `HpMaximo`; `HpAtual` é limitado ao novo máximo |
| Personagem excluído | remove os tokens ligados a ele |
| Jogador sai ou é removido | remove o token |
| Mapa ativado | move os tokens dos jogadores para o novo mapa, no spawn; cria os que faltam |

Token criado: `Tipo=Personagem`, `Icone` = nome da classe, `HpAtual = HpMaximo`, `DonoUsuarioId` = jogador.

### Posicionamento

- Spawn: primeira célula livre varrendo linha a linha a partir de (0,0).
- Duplicar: célula livre mais próxima do original, em anéis de distância crescente; empate resolvido por ordem fixa (de cima para baixo, da esquerda para a direita).
- Sem célula livre: 409 em criar e duplicar; token automático pulado com log.

### Endpoints REST

Quem não é membro recebe 404; membro sem papel de Mestre recebe 403, como na Etapa 2.

| Método e rota | Quem | Comportamento |
|---|---|---|
| `POST /api/mesas/{id}/mapas` | Mestre | cria mapa inativo; 201 |
| `GET /api/mesas/{id}/mapas` | membro | Mestre vê todos; jogador vê só o ativo (lista vazia se não houver) |
| `PUT /api/mapas/{id}` | Mestre | edita nome, dimensões e cor; 409 se algum token ficar fora dos novos limites |
| `DELETE /api/mapas/{id}` | Mestre | exclui com os tokens; se era o ativo, a mesa fica sem mapa ativo |
| `PUT /api/mapas/{id}/ativar` | Mestre | ativa, desativando o anterior na mesma transação; idempotente |
| `GET /api/mapas/{id}/estado` | membro | mapa + tokens; jogador recebe 404 se o mapa não for o ativo |
| `POST /api/mapas/{id}/tokens` | Mestre | cria token; sem `x`/`y`, usa o spawn; 409 se a célula estiver ocupada |
| `PUT /api/tokens/{id}` | Mestre | edita nome, HP, ícone, cor e `Visivel`; em token de jogador, só `HpAtual` e `Visivel` |
| `POST /api/tokens/{id}/duplicar` | Mestre | duplica na célula livre mais próxima; 409 para token de jogador |
| `DELETE /api/tokens/{id}` | Mestre | remove; 409 para token de jogador |

Ativação: transação explícita que desativa o anterior, ativa o novo e reposiciona os tokens dos jogadores. Duas ativações simultâneas: o índice único parcial recusa uma, que vira 409.

### Hub

`EntrarNaMesa(mesaId)` devolve `LobbyDto` com `MapaAtivo`.

`MoverToken(tokenId, x, y, versaoEsperada)` devolve `MovimentoResultado { Aceito, Motivo, Token }`:

1. Limite de taxa por conexão (token bucket, 20/s). Estourou: rejeita com motivo, sem tocar no banco.
2. Token inexistente ou de mesa da qual o chamador não é membro: `HubException("Token não encontrado.")`.
3. Jogador só move token cujo `DonoUsuarioId` é ele, e só em mapa ativo; Mestre move qualquer um, em qualquer mapa da mesa.
4. Destino fora dos limites: rejeita.
5. `versaoEsperada` diferente da atual: rejeita.
6. Destino ocupado por outro token: rejeita.
7. Grava posição e `Versao + 1`. Se o banco recusar por concorrência (versão ou índice único), rejeita com o motivo correspondente.

Toda rejeição avaliada traz em `Token` o estado atual, para o cliente se corrigir; a do limite de taxa vem sem ele, e o cliente apenas desfaz o movimento. Mover para a própria célula é aceito sem alterar nada. No aceite, o grupo recebe `TokenMovido`.

### Eventos para o cliente

| Evento | Argumentos |
|---|---|
| `TokenCriado` | `mesaId`, `token` |
| `TokenMovido` | `mesaId`, `mapaId`, `tokenId`, `x`, `y`, `versao` |
| `TokenAtualizado` | `mesaId`, `token` |
| `TokenRemovido` | `mesaId`, `mapaId`, `tokenId` |
| `MapaAtivoAlterado` | `mesaId`, `estado` (mapa + tokens, ou `null`) |
| `MapaAtualizado` | `mesaId`, `mapa` |

`TokenDto` inclui `mapaId`. Eventos de mapa ativo vão ao grupo da mesa; os de mapa inativo, só ao Mestre. Falha de envio é registrada em log e não desfaz a gravação, como no `MesaNotifier`.

## Frontend

### Dependências novas

`three`, `@react-three/fiber`, `zustand`, `@types/three` (dev).

### Organização (`features/mapa/`)

| Arquivo | Responsabilidade |
|---|---|
| `mapaApi.ts` | tipos e chamadas REST |
| `mapaStore.ts` | store Zustand: mapa em exibição, tokens por id, seleção, ações de sincronização |
| `useMapaTempoReal.ts` | liga os eventos do Hub ao store; expõe `moverToken` |
| `MapaAba.tsx` | aba: estados vazios, cena, painel do Mestre |
| `CenaDoMapa.tsx` | `Canvas` ortográfico top-down; pan, zoom com limites, arraste com fantasma e snap |
| `Grade.tsx` | plano único com shader de grade |
| `TokenNoMapa.tsx` | disco, borda, nome, destaque do próprio token, animação |
| `texturas.ts` | textura do disco e do nome, com cache |
| `TokenIcone.tsx` | disco do token em SVG: `ClassIcon` ou ícone genérico |
| `PainelDoMestre.tsx`, `MapaFormDialogo.tsx`, `TokenFormDialogo.tsx` | gestão de mapas e tokens |
| `shared/icones/IconeGenerico.tsx` | oito SVGs originais |
| `shared/ui/Toast.tsx` | toast curto |

### Integração com o lobby

- `useLobby` ganha um parâmetro opcional com dois ganchos: registrar handlers extras antes do `EntrarNaMesa` e receber o estado sincronizado. Nada mais muda nele.
- A cada (re)conexão, o estado do mapa no store é substituído por inteiro pelo `MapaAtivo` recebido.
- `MesaPage` ganha as abas "Lobby" e "Mapa" (`?aba=mapa`). A cena é carregada sob demanda.

### Cena

- Câmera ortográfica olhando de cima. Pan com botão do meio ou dois dedos; zoom com scroll ou pinça, entre ver o mapa inteiro e poucas células.
- Grade em um único plano com shader.
- `frameloop="demand"`: só renderiza quando algo muda ou anima.
- Token memoizado, assinando só o próprio registro. A posição é animada no `useFrame` (cerca de 200 ms), sem re-render do React.
- O token é um grupo com o disco como filho, para virar billboard na Etapa 4 sem mudar quem o usa.
- Textura do disco: `ClassIcon` renderizado para SVG (personagem) ou ícone genérico, com cache por ícone e cor. Nome em textura de canvas, abaixo do disco. O token do próprio jogador tem um anel de destaque.

### Interação

- Jogador arrasta o próprio token; Mestre arrasta qualquer um.
- Durante o arraste: fantasma na célula de destino, verde se livre e vermelho se ocupada ou fora do mapa.
- Ao soltar em célula válida: movimento otimista e chamada a `MoverToken`. Rejeição: o token volta animado à posição do servidor e um toast mostra o motivo.
- Soltar em célula inválida cancela sem chamar o servidor.

### Painel do Mestre

- Mapas: criar, editar, excluir, abrir para preparar, ativar. Um mapa inativo aberto mostra o aviso "Em preparação".
- Tokens: criar (tipo, nome, HP, ícone, cor), editar, duplicar, remover. Em token de jogador, só HP atual fica editável e duplicar/remover ficam desabilitados.

### Estados vazios

- Jogador sem mapa ativo: "Aguardando o Mestre".
- Mestre sem mapa: botão para criar; com mapas mas nenhum ativo: botão para ativar.

## Tratamento de erros

- REST: `Resultado`/`Erro` traduzidos para ProblemDetails, como hoje.
- Hub: regra violada volta em `MovimentoResultado`; falta de acesso vira `HubException`.
- Cliente: erros REST aparecem no diálogo ou painel que originou a ação; rejeição de movimento aparece em toast.

## Testes

Integração, com PostgreSQL real via Testcontainers.

`MapasTests`:
- jogador não cria, edita, exclui nem ativa;
- jogador lista só o mapa ativo;
- reduzir dimensões com token fora dos novos limites é rejeitado;
- ativar um mapa desativa o anterior;
- excluir o mapa ativo deixa a mesa sem mapa ativo.

`TokensTests`:
- Mestre cria, edita, duplica e remove; jogador não;
- criar em célula ocupada é rejeitado;
- duplicar ocupa a célula livre mais próxima;
- token de jogador não é duplicado nem removido;
- ícone inválido para o tipo é rejeitado.

`TokensDeJogadorTests`:
- token criado ao ativar o mapa e ao aceitar convite com mapa ativo;
- trocar de personagem atualiza o token e mantém a posição;
- editar o personagem sincroniza; excluir remove;
- sair ou ser removido da mesa remove o token;
- ativar outro mapa move o token.

`MapaHubTests`:
- `EntrarNaMesa` devolve o mapa ativo com tokens;
- jogador não move token de outro;
- movimento fora dos limites e para célula ocupada é rejeitado;
- conflito de versão devolve o estado atual;
- não-membro não recebe eventos nem move;
- eventos de mapa inativo não chegam ao jogador;
- limite de taxa rejeita o excesso;
- dois clientes em paralelo: o `TokenMovido` de um chega ao outro.

Frontend: `npm run typecheck` e `npm run build`.

## Entrega

Arquivos completos, a migration, passo a passo de teste com dois navegadores, checklist de aceitação e a lista de decisões; README atualizado para a Etapa 3.
