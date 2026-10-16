# Revisão de autorização

Feita na Etapa 5, percorrendo todos os controllers e o Hub. Legenda:

- **Membro:** qualquer pessoa da mesa (Mestre ou jogador). Quem não é membro recebe 404.
- **Mestre:** o jogador recebe 403; quem não é membro, 404.
- **Dono:** só o próprio usuário; para os demais o recurso se comporta como inexistente (404).

Em todas as linhas a autenticação é o `[Authorize]` do controller ou do Hub (JWT). A coluna "Onde é validado" é a checagem de papel, sempre feita no servidor contra o banco.

## REST

| Endpoint | Quem pode | Onde é validado |
|---|---|---|
| `POST /api/auth/register` | anônimo, com limite por IP | `[EnableRateLimiting]`; validador do request |
| `POST /api/auth/login` | anônimo, com limite por IP | `[EnableRateLimiting]`; bloqueio após 10 senhas erradas (Identity) |
| `POST /api/auth/renovar` | usuário com token válido | `[Authorize]`; `AuthService.RenovarAsync` |
| `GET /api/auth/me` | usuário logado | `[Authorize]` |
| `GET /api/personagens` | dono | `PersonagemService` filtra por `UsuarioId` |
| `POST /api/personagens` | usuário logado | cria sempre para quem chama |
| `GET /api/personagens/{id}` | dono | `PersonagemService.ObterAsync` |
| `PUT /api/personagens/{id}` | dono | `PersonagemService.AtualizarAsync` |
| `DELETE /api/personagens/{id}` | dono | `PersonagemService.ExcluirAsync` |
| `GET /api/mesas` | usuário logado | `MesaService.ListarAsync` lista só as mesas de quem chama |
| `POST /api/mesas` | usuário logado | quem cria vira o Mestre |
| `GET /api/mesas/{id}` | membro | `MesaService.ObterAsync` |
| `PUT /api/mesas/{id}` | Mestre | `MesaAcesso.ExigirMestreAsync` |
| `DELETE /api/mesas/{id}` | Mestre | `MesaAcesso.ExigirMestreAsync` |
| `PUT /api/mesas/{id}/sessao` | Mestre | `MesaAcesso.ExigirMestreAsync` |
| `PUT /api/mesas/{id}/movimentacao` | Mestre | `MesaAcesso.ExigirMestreAsync` |
| `PUT /api/mesas/{id}/meu-personagem` | membro, com personagem próprio | `MesaService.DefinirMeuPersonagemAsync`; jogador bloqueado com a sessão ativa |
| `PUT /api/mesas/{id}/membros/{usuarioId}/personagem` | Mestre | `MesaAcesso.ExigirMestreAsync`; o personagem precisa ser do membro |
| `GET /api/mesas/{id}/membros/{usuarioId}/personagens` | Mestre | `MesaAcesso.ExigirMestreAsync` |
| `DELETE /api/mesas/{id}/membros/{usuarioId}` | Mestre remove; jogador só a si mesmo | `MesaService.RemoverMembroAsync` |
| `POST /api/mesas/{id}/convites` | Mestre | `MesaAcesso.ExigirMestreAsync` |
| `GET /api/mesas/{id}/convites` | Mestre | `MesaAcesso.ExigirMestreAsync` |
| `GET /api/convites/{codigo}` | usuário logado com o código | `ConviteService.PreVisualizarAsync`; mostra só nome da mesa, do Mestre e quantidade de jogadores |
| `POST /api/convites/{codigo}/aceitar` | usuário logado com o código | `ConviteService.AceitarAsync`; convite válido e personagem próprio |
| `DELETE /api/convites/{codigo}` | Mestre | `MesaAcesso.ExigirMestreAsync` |
| `POST /api/mesas/{id}/mapas` | Mestre | `MesaAcesso.ExigirMestreAsync` |
| `GET /api/mesas/{id}/mapas` | membro | `MapaService.ListarAsync`; o jogador recebe só o mapa ativo |
| `PUT /api/mapas/{id}` | Mestre | `MapaService.ObterComoMestreAsync` |
| `DELETE /api/mapas/{id}` | Mestre | `MapaService.ObterComoMestreAsync` |
| `PUT /api/mapas/{id}/ativar` | Mestre | `MapaService.ObterComoMestreAsync` |
| `PUT /api/mapas/{id}/fundo` | Mestre | `MapaService.ObterComoMestreAsync`; imagem da mesma mesa |
| `PUT /api/mapas/{id}/nevoa` | Mestre | `NevoaService.ObterComoMestreAsync` |
| `PUT /api/mapas/{id}/nevoa/revelada` | Mestre | `NevoaService.ObterComoMestreAsync`; camada do tamanho do mapa |
| `DELETE /api/mapas/{id}/nevoa/memoria` | Mestre | `NevoaService.ObterComoMestreAsync` |
| `GET /api/mapas/{id}/estado` | membro | `MapaService.ObterEstadoAsync`; jogador só no mapa ativo, com os tokens filtrados pela visão dele e, da névoa, só as camadas comuns e a dele |
| `POST /api/mapas/{id}/tokens` | Mestre | `MesaAcesso.ExigirMestreAsync` em `TokenService.CriarAsync` |
| `POST /api/mapas/{id}/estruturas` | Mestre | `MesaAcesso.ExigirMestreAsync` em `EstruturaService.CriarAsync` |
| `PUT /api/tokens/{id}` | Mestre | `TokenService.ObterComoMestreAsync` |
| `PUT /api/tokens/{id}/hp` | Mestre | `TokenService.ObterComoMestreAsync` |
| `PUT /api/tokens/{id}/exibicao` | Mestre | `TokenService.ObterComoMestreAsync` |
| `POST /api/tokens/{id}/duplicar` | Mestre | `TokenService.ObterComoMestreAsync` |
| `DELETE /api/tokens/{id}` | Mestre | `TokenService.ObterComoMestreAsync` |
| `PUT /api/estruturas/{id}` | Mestre | `EstruturaService.ObterComoMestreAsync` |
| `PUT /api/estruturas/{id}/porta` | Mestre | `EstruturaService.ObterComoMestreAsync` |
| `DELETE /api/estruturas/{id}` | Mestre | `EstruturaService.ObterComoMestreAsync` |
| `POST /api/mesas/{id}/imagens` | Mestre, com limite por usuário | `MesaAcesso.ExigirMestreAsync` em `ImagemService.EnviarAsync`; `[EnableRateLimiting]` |
| `GET /api/mesas/{id}/imagens` | Mestre | `MesaAcesso.ExigirMestreAsync` |
| `GET /api/imagens/{id}` | membro | `ImagemService.AbrirAsync` |
| `GET /api/imagens/{id}/miniatura` | membro | `ImagemService.AbrirAsync` |
| `DELETE /api/imagens/{id}` | Mestre | `MesaAcesso.ExigirMestreAsync` em `ImagemService.ExcluirAsync` |
| `GET /health` | anônimo | sem dados: responde só se a API e o banco estão no ar |

Nos endpoints de token (`/api/tokens/{id}...`), o jogador recebe 403 só para tokens que ele vê; para um token oculto ou de um mapa em preparação, recebe 404, como se o id não existisse.

## Hub (`/hubs/mesa`)

| Método | Quem pode | Onde é validado |
|---|---|---|
| conexão | usuário logado | `[Authorize]` no Hub; o token chega em `?access_token=`, aceito só nas rotas `/hubs` |
| `EntrarNaMesa(mesaId)` | membro | `MesaService.ObterAsync`; o papel define o grupo (Mestre ou jogadores) e o estado devolvido é filtrado pela visão de quem chama |
| `SairDoLobby(mesaId)` | qualquer conexão | age só sobre a própria conexão |
| `MoverToken(tokenId, x, y, versao)` | Mestre; o jogador só no próprio token e com a movimentação da mesa em `Livre` (senão, movimento rejeitado) | `TokenService.MoverAsync`; limites, célula livre, estruturas (para o jogador) e versão |
| `MoverPorBlocos(tokenId, x, y, versao)`, `DesfazerMovimento(tokenId, versao)`, `ConcluirMovimento(tokenId, versao)` | Mestre: qualquer token. Jogador: só o próprio, em mapa ativo | `TokenService.MoverPorBlocosAsync` e afins; limites, célula livre, estruturas, alcance pelo movimento restante, pendência e versão |
| `ExplorarCelulas(mapaId, celulas)` | Jogador, no mapa ativo com a névoa ligada; grava só na memória do grupo ou na dele. Do Mestre, é ignorado | `NevoaService.ExplorarAsync`; camada do tamanho do mapa; limite dos movimentos (acima dele, a chamada é ignorada) |
| `NovaRodada(mapaId)`, `RestaurarMovimento(tokenId)` | Só o Mestre | `TokenService.NovaRodadaAsync` / `RestaurarMovimentoAsync` (`ExigirMestreAsync`, `ObterComoMestreAsync`) |

Todos os métodos têm limite de chamadas por conexão (`LimitadorDoHub`) e as mensagens de cliente, tamanho máximo de 32 kB.

## Eventos enviados pelo servidor

| Evento | Quem recebe |
|---|---|
| Lobby (`MembroEntrou`, `MembroSaiu`, `MembroOnline`, `MembroOffline`, `PersonagemAtivoAlterado`, `SessaoAlterada`, `MovimentacaoAlterada`, `MesaExcluida`) | o grupo da mesa; não trazem HP |
| `RemovidoDaMesa` | só o usuário removido |
| `TokenCriado`, `TokenAtualizado`, `TokenMovido`, `TokenRemovido`, `HpAlterado` | cada público recebe a própria projeção: Mestre (tudo), dono (tudo do próprio token) e demais jogadores (nada de tokens ocultos; HP conforme a exibição). Montado em `ProjecoesDoToken`, enviado por `MapaNotifier` |
| `MapaAtivoAlterado` | cada usuário conectado recebe o estado montado para ele |
| `MapaAtualizado`, `EstruturaAdicionada`, `EstruturaAtualizada`, `EstruturaRemovida` | o grupo da mesa; em mapa em preparação, só o grupo do Mestre |
| `MemoriaDeNevoaAlterada` | camadas comuns (memória do grupo e revelada): o grupo da mesa. Memória individual: só o Mestre e o jogador dono |

## O que a revisão mudou

- **Lobby:** os dados de membro deixaram de trazer o HP máximo do personagem. Era o mesmo número do token do jogador, visível mesmo com o HP do token oculto. O teste de vazamento acusou.
- **Endpoints de token:** para o jogador, token oculto ou de mapa em preparação passou a responder 404 em vez de 403, para o código de erro não confirmar que o id existe.
- **Limite de requisições:** passou a rodar depois da autenticação (Etapa 4), o que permitiu o limite de upload por usuário.

## Pontos conhecidos, aceitos para o MVP

- **Névoa de guerra é visual:** o servidor continua enviando ao jogador todos os tokens visíveis e todas as estruturas do mapa ativo, e é o cliente que esconde o que está fora da vista. Quem abrir as ferramentas do navegador vê o mapa inteiro, e um jogador pode mandar ao `ExplorarCelulas` células que não viu (a memória dele, ou a do grupo, fica revelada). Para esconder de verdade, como nos tokens ocultos, o servidor teria de calcular a visão e filtrar o que envia.

- **Estruturas e mapas em preparação:** um jogador que acertasse o id (um GUID aleatório) de uma estrutura ou de um mapa em preparação receberia 403 em vez de 404 nas rotas de edição. Nenhum dado é devolvido.
- **Cadastro aberto:** qualquer pessoa com o endereço cria conta, mas não vê nem entra em mesa alguma sem convite.
- **Token de login sem revogação:** um token vale até vencer (12 horas por padrão), mesmo que a pessoa seja removida da mesa; a remoção, porém, é conferida a cada chamada, então ela perde o acesso à mesa na hora.
