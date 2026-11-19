# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Mesa Virtual (VTT): virtual tabletop para D&D 5e. Backend ASP.NET Core 10 + EF Core + PostgreSQL 17 + SignalR; frontend React 19 + TypeScript + Vite + TanStack Query + Zustand + react-three-fiber. Em produção, um único container .NET serve a API, o Hub e o build do frontend (`wwwroot`). Todo o código, nomes de identificadores, mensagens e docs estão em **português** — mantenha esse padrão.

## Comandos

```bash
# Tudo via Docker (app em http://localhost:8080)
cp .env.example .env   # trocar POSTGRES_PASSWORD e JWT_SECRET
docker compose up --build

# Desenvolvimento com hot reload
docker compose up -d postgres
cd backend/src/Vtt.Api && dotnet run          # API em :5080 (ConnectionStrings:Default e Jwt:Secret via dotnet user-secrets)
cd frontend && API_PROXY_TARGET=http://localhost:5080 npm run dev   # :5173; sem a variável, o proxy aponta para :8080

# Backend: testes de integração (exigem Docker rodando — Testcontainers sobe um PostgreSQL real)
cd backend && dotnet test
dotnet test --filter "FullyQualifiedName~TokensTests"            # uma classe
dotnet test --filter "FullyQualifiedName~TokensTests.NomeDoTeste" # um teste

# Frontend
cd frontend
npm run typecheck
npm test                         # vitest run
npx vitest run src/features/mapa/geometria.test.ts   # um arquivo
npm run build                    # tsc --noEmit && vite build

# Migration nova
cd backend
dotnet ef migrations add <Nome> --project src/Vtt.Infrastructure --startup-project src/Vtt.Api --output-dir Persistence/Migrations
```

Convenções do usuário: **não fazer commits** (o usuário commita) e **uma migration por funcionalidade**. As migrations são aplicadas na subida quando `DATABASE_AUTO_MIGRATE=true`.

## Arquitetura do backend

Clean architecture em `backend/src`:
- **Vtt.Domain** — entidades e enums, sem dependências (`Mapas/`, `Mesas/`, `Personagens/`, `Imagens/`, `Biblioteca/`).
- **Vtt.Application** — serviços por feature (`MapaService`, `TokenService`, `NevoaService`, `IniciativaService`...), DTOs, validadores FluentValidation e interfaces (`IAppDbContext`, `IMapaNotifier`, `IMesaNotifier`, `IFileStorage`). Os serviços usam o `IAppDbContext` direto (sem repositórios).
- **Vtt.Infrastructure** — `AppDbContext` + configurations, migrations, Identity, JWT, ImageSharp, armazenamento local de uploads.
- **Vtt.Api** — controllers finos, `Hubs/` (SignalR), `Program.cs`.

Padrões que atravessam várias camadas:
- **`Resultado` / `Resultado<T>` + `Erro`** (`Application/Common/Resultado.cs`): serviços nunca lançam para erros de negócio; devolvem `Erro.NaoEncontrado` (404), `Proibido` (403), `Conflito` (409) ou `Invalido(campo, ...)` (400). O controller faz `resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!)` (`Api/Infrastructure/ResultadoExtensions.cs`).
- **Autorização no serviço**, nunca no cliente: cada operação recebe `usuarioId` (`User.ObterUsuarioId()`) e confere o papel contra o banco (`MapaAcesso.ObterPapelAsync`, `MesaAcesso`). Quem não é membro recebe 404; jogador recebe 403 em ações do Mestre e 404 para o que não pode saber que existe (mapas em preparação, tokens ocultos). Revisão completa em `docs/revisao-de-autorizacao.md`.
- **Tempo real**: mudanças passam pela REST e o serviço notifica depois de gravar (`IMapaNotifier`/`IMesaNotifier`, implementados em `Api/Hubs/MapaNotifier.cs` e `MesaNotifier.cs`). A exceção é o movimento de tokens, feito por métodos do `MesaHub` (`MoverToken`, `MoverPorBlocos`, `DesfazerMovimento`, `ConcluirMovimento`...) que respondem `{ aceito, motivo, token }`.
- **Projeções por destinatário** (`ProjecoesDoToken` em `Application/Mapas/MapaDtos.cs`): cada evento de token tem até três payloads — Mestre, dono e demais jogadores — enviados a grupos SignalR separados por papel. Ocultar/revelar vira `TokenRemovido`/`TokenCriado` para os jogadores. Ao adicionar campos a tokens, garanta que a projeção do jogador não vaze dados.
- **Concorrência**: tokens e estruturas têm `Versao` (409 em conflito); colisão de células é decidida pelo banco (índice único). O alcance do movimento por blocos é BFS em 8 direções validado no servidor.
- Presença e rate limiting do Hub ficam em memória do processo (uma instância só).

Testes (`backend/tests/Vtt.Api.IntegrationTests`): só integração, via `VttApiFactory` (WebApplicationFactory + Testcontainers) compartilhada por `[Collection(ApiCollection.Nome)]`. Helpers de setup em `ApiTestHelpers`, `MesaTestHelpers`, `ImagemTestHelpers`, `BibliotecaTestHelpers`; limites (rate limit, cotas) são sobrescritos por propriedades virtuais da factory.

## Arquitetura do frontend

`frontend/src`: `app/` (rotas, layout, rota protegida), `features/<feature>/` (cada uma com seu `*Api.ts` de chamadas REST + chaves do TanStack Query), `shared/` (`api/http.ts`, `realtime/`, `ui/`, `imagens/`).

- O mapa (`features/mapa/`) é uma cena three.js (`CenaDoMapa`, `CameraDoMapa`, `TokenNoMapa`, `EstruturaNoMapa`, `NevoaNoMapa`). O estado vivo fica em stores Zustand (`mapaStore`, `editorStore`, `visaoStore`, `interacaoStore`, `pingStore`, `iniciativaStore`), não no TanStack Query.
- `useLobby` (mesas) abre a conexão SignalR; `useMapaTempoReal` registra os eventos do mapa sobre ela. Ao conectar/reconectar o cliente chama `EntrarNaMesa` e **substitui o estado local inteiro** pelo do servidor.
- Modos de interação do mapa separados em `modoDeJogo.ts`, `modoDeEdicao.ts`, `modoDeFundo.ts`, `modoDaNevoa.ts`; lógica pura testável em `geometria.ts`, `nevoa.ts`, `fundo.ts` (com `*.test.ts` ao lado).
- Névoa de guerra: o cliente do jogador calcula a visão e envia células exploradas (`ExplorarCelulas`, bitmap em base64); o servidor só une à memória.
- Câmera e qualidade gráfica são locais de cada usuário e nunca vão ao servidor.
- O Vite faz proxy de `/api`, `/hubs` (WebSocket) e `/health`.

## Docs e specs

- `README.md` — referência de rotas REST, eventos do Hub e roteiros de teste manual com dois/três navegadores.
- `specs/` — especificações de novas funcionalidades escritas pelo usuário; seguem a regra de reaproveitar padrões existentes e fazer mudanças mínimas, sem refatorações não pedidas.
- `docs/superpowers/` — specs e planos de design por etapa.
- `docs/deploy-vps.md` e `docs/ROTEIRO.md` — deploy em `vtt.ercode.tech` (VPS, atrás de proxy Caddy; `docker-compose.vps.yml` remove portas publicadas — nunca publique a 8080 na VPS).
- `scripts/backup.sh` / `restaurar.sh` — backup do banco e dos uploads.

## Instruções do projeto
Aplique sempre a skill "saida-objetiva" em todas as respostas deste projeto.

Resumo das regras:
- Comece direto pela resposta ou ação. Sem introdução, recapitulação ou fechamento.
- Frases curtas, palavras simples, voz ativa. Corte o que não muda a decisão.
- Planos: lista numerada, 1 linha por passo, máximo ~7 passos.
- Prompts: objetivo em 1 linha, depois bullets curtos (entradas, saída esperada, restrições).
- Explicações: ideia central em 1-2 frases + exemplo do dia a dia. Use conceito de programação só se ajudar. Sem jargão.
- Se houver algo crítico para o sistema (perda de dados, falha de segurança, ação irreversível, quebra, custo inesperado), mostre com "⚠️ CRÍTICO:" em 1 linha, sem aprofundar, e termine com "(posso detalhar)".
- Detalhe só se eu pedir.
