# Etapa 5: HP em tempo real, visibilidade, reconexão, polimento e operação

Data: 2026-10-09. Estado: implementado. A seção "Diferenças na implementação", no fim, lista o que mudou em relação a este texto.

## Objetivo

O Mestre controla o HP e a visibilidade dos tokens ao vivo, e nenhum dado que um jogador não deve ver sai do servidor para ele. A sessão sobrevive a quedas de rede, abas dormindo e login expirando. O projeto fica pronto para ser aberto aos amigos por ngrok, com backup e guia.

## Decisões confirmadas com o usuário

1. **Token oculto não ocupa a célula para os jogadores.** Eles podem parar em cima dele sem nenhuma pista. Ao ser revelado em uma célula ocupada, o token vai para a célula livre mais próxima.
2. **O Mestre pode ocultar o token de um jogador; o dono continua vendo e movendo o próprio token.**
3. **O login é renovado em silêncio** enquanto a pessoa usa o sistema; só volta ao login quem ficou fora além da validade, e retorna à mesma mesa.

## Decisões tomadas sem consulta

- **Filtro por projeção, com dois grupos por papel.** Cada evento de token é montado três vezes no servidor (Mestre, dono, demais jogadores) e enviado a `mesa:{id}:mestre`, às conexões do dono e a `mesa:{id}:jogadores`. O grupo garante que um payload de Mestre nunca seja endereçado a um jogador; a projeção garante que o payload de jogador nunca contenha o que ele não vê.
- **Jogador só recebe evento se o que ele vê mudou.** Dano em um token com HP oculto não gera nada para ele; em um token com barra, só quando a faixa muda.
- **A barra vai em faixas de 5%**, arredondadas para cima: o percentual exato deixaria deduzir o HP de criaturas com poucos pontos.
- **"Caído" é visível a todos**, inclusive com HP oculto: é um estado da peça no tabuleiro, não um número.
- **HP pelo REST**, como as demais ações do Mestre: `PUT /api/tokens/{id}/hp`, com soma atômica no banco para cliques rápidos não se perderem.
- **HP máximo de token de jogador não é editável pelo Mestre**: vem do personagem, como desde a Etapa 3.
- **O HP atual do token não volta para o cadastro do personagem.** O personagem guarda só o máximo; o atual é da cena.
- **Tokens criados sozinhos (spawn) ignoram tokens ocultos** ao procurar célula livre, pelo mesmo motivo do item 1. Os que o Mestre cria consideram todos.
- **Scripts de backup em bash e PowerShell.**

## HP

- `Token.ExibicaoHp`: `Oculto`, `Barra`, `Completo`. Padrão: `Completo` em tokens de `Personagem`, `Barra` nos demais. A migration aplica o padrão aos tokens existentes.
- `PUT /api/tokens/{id}/hp` (Mestre): `{ delta }` soma (dano negativo, cura positiva), `{ hpAtual }` define, `{ hpMaximo }` altera o máximo. O resultado fica sempre entre 0 e o máximo.
- Em 0 o token fica "caído": disco acinzentado com uma marca e o texto "Caído". Não é removido.
- Evento `HpAlterado(mesaId, mapaId, hp)` com `tokenId`, `hpAtual`, `hpMaximo`, `hpPercentual`, `caido` e `versao`, cada campo presente só para quem pode vê-lo.
- Barra de HP sob o token, de verde a vermelho, com transição animada. Números ao lado só para quem os recebe.

## O que cada um recebe de um token

| Campo | Mestre | Dono | Outros jogadores |
|---|---|---|---|
| Token com `Visivel = false` | sim | sim | **nada**, em nenhum evento, estado ou erro |
| `hpAtual`, `hpMaximo` | sim | sim | só com `Completo` |
| `hpPercentual` (faixas de 5%) | sim | sim | com `Barra` ou `Completo` |
| `caido` | sim | sim | sim |

- Vale para `GET /api/mapas/{id}/estado`, `EntrarNaMesa`, `MapaAtivoAlterado`, `TokenCriado`, `TokenAtualizado`, `TokenMovido`, `TokenRemovido`, `HpAlterado` e a resposta do `MoverToken`.
- `PUT /api/tokens/{id}/exibicao` (Mestre): `{ visivel, exibicaoHp }`. Ocultar envia `TokenRemovido` aos outros jogadores; revelar envia `TokenCriado`; Mestre e dono recebem `TokenAtualizado`.
- Jogador que tenta mover um token que não vê recebe "Token não encontrado", a mesma resposta de um id inexistente.
- O índice único da célula passa a valer só entre tokens visíveis.

## Reconexão e robustez

- Indicador com três estados: conectado, reconectando, desconectado. Fora de "conectado", o mapa e o painel ficam bloqueados com uma mensagem, e há um botão para tentar de novo.
- Reconexão automática com esperas de 0, 2, 5, 10 e 20 s, e depois a cada 20 s. Ao reconectar, `EntrarNaMesa` devolve o estado completo, que substitui o local.
- `POST /api/auth/renovar`: troca um token válido por um novo. O cliente renova quando passou de dois terços da validade e ao voltar o foco. Um 401 leva ao login, que devolve a pessoa à mesma página.
- Ao voltar de segundo plano ou de uma queda de rede, o cliente confere a conexão e ressincroniza.
- Limite de chamadas por conexão e por método do Hub, e tamanho máximo de mensagem de 32 kB.

## Polimento

- Painel do Mestre vira gaveta em telas estreitas; alvos de toque de pelo menos 44 px.
- Atalhos do Mestre no token selecionado, listados em um diálogo aberto por `?`: ocultar/revelar, duplicar, remover, dano e cura rápidos.
- Estados de carregamento, vazio e erro revisados; ações destrutivas com confirmação.
- "Caído" e os estados de conexão têm texto, não só cor.
- Logs: mesa criada, entrada e saída de jogadores, tentativas sem permissão e uploads rejeitados; nunca senhas ou tokens.
- Tabela de revisão de autorização de todos os endpoints e métodos do Hub.

## Operação

- `scripts/backup.sh` e `scripts/backup.ps1`: `pg_dump` pelo `docker compose exec` e o volume de uploads compactado, com data no nome. `scripts/restaurar.sh` e `.ps1` fazem o caminho inverso.
- Healthcheck do container da API no `docker-compose.yml`.
- Cabeçalho `ngrok-skip-browser-warning` nas chamadas da aplicação.
- Guia de ngrok no README, com os oito tópicos pedidos.

## Testes

- **Vazamento:** um jogador conectado nunca recebe dado de token oculto no estado inicial nem em evento algum, e nunca recebe números de HP fora de `Completo`, exceto do próprio token. O teste lê os payloads como JSON bruto, para não depender dos tipos do cliente.
- HP limitado entre 0 e o máximo, soma atômica, jogador não altera HP.
- Ocultar e revelar geram `TokenRemovido` e `TokenCriado` para os jogadores; revelar em célula ocupada realoca.
- Jogador para em cima de token oculto; não move token que não vê.
- Reconexão devolve o estado alterado enquanto o cliente estava fora.
- Renovação de token; limite por método do Hub.
- Suíte completa de todas as etapas.

## Diferenças na implementação

- **O lobby deixou de enviar o HP máximo dos personagens.** O teste de vazamento mostrou que a lista de membros (Etapa 2) entregava a todos o mesmo número do token de cada jogador. O campo agora vem nulo no lobby e nos eventos de membro; só a lista que o Mestre usa para escolher o personagem de um jogador o traz. Nenhuma tela usava esse dado.
- **Endpoints de token respondem 404 ao jogador** para tokens ocultos e de mapas em preparação, em vez de 403: o código de erro não confirma que o id existe.
- **A conexão é conferida de verdade**, e não pelos avisos `online`/`offline` do navegador: ao receber um deles, ao voltar de segundo plano e ao recuperar a rede, o cliente pede o estado ao servidor com prazo de 4 s e fecha a conexão se não houver resposta. O sinal de vida do SignalR passou a 5 s nos dois sentidos, para uma conexão morta ser percebida em até 15 s.
- **A barra de HP fica acima do disco**, não abaixo: abaixo ela afundaria no chão com a câmera inclinada. O estado vai escrito na etiqueta do token ("caído", "oculto"), junto com os números para quem os recebe.
- **Renovação do login a cada minuto e ao voltar o foco**, lendo a validade do próprio token; não há novo dado guardado no navegador.
- **O `.env.example` passou a trazer `ASPNETCORE_ENVIRONMENT=Production`**, para o Swagger não ir junto quando a mesa é exposta.
- **Scripts PowerShell escritos, mas não executados**: não há PowerShell na máquina em que a etapa foi desenvolvida. Os scripts bash foram testados de ponta a ponta.
- **Guia de ngrok não testado por um túnel real**: exige a conta e o authtoken do dono do projeto. O que o guia descreve sobre o comportamento da aplicação foi testado no deploy pelo Docker Compose em modo Production.
