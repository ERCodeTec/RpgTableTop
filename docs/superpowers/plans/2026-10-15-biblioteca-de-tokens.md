# Biblioteca de tokens: plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Modelos de tokens (monstros, NPCs, vilões) que pertencem ao usuário, organizados em categorias livres, com
imagem pessoal opcional e colocados em qualquer mapa onde o usuário seja Mestre.

**Architecture:** Duas entidades novas (`CategoriaDeModelo` e `ModeloDeToken`), no padrão de `Personagem`.
`ImagemUpload` ganha um segundo escopo (pessoal), e a imagem é copiada para a mesa uma vez ao colocar o modelo no
mapa. A colocação passa pelo `TokenService`, que reaproveita a busca de células livres e a notificação em tempo real.

**Tech Stack:** ASP.NET Core 10, EF Core e Npgsql (snake_case), FluentValidation, xUnit com Testcontainers; React,
TanStack Query, Zustand e Vite.

**Spec:** `docs/superpowers/specs/2026-10-15-biblioteca-de-tokens-design.md`

## Global Constraints

- Recursos de outro usuário devolvem 404, nunca 403.
- Limites: 50 categorias e 500 modelos por usuário; 100 imagens e 200 MB pessoais (`Uploads:CotaImagensPorUsuario`,
  `Uploads:CotaBytesPorUsuario`); quantidade por colocação de 1 a 20.
- Categoria: 1 a 40 caracteres, única por usuário sem diferenciar maiúsculas.
- O tipo do modelo é só `Inimigo` ou `Npc`.
- As mensagens ao usuário ficam em português, com acentuação correta.
- Os caminhos das imagens de mesa que já existem não mudam.

## Review Focus

1. Nome do modelo perto de 60 caracteres com quantidade 20: o sufixo " 20" não pode estourar o limite do token
   (teste na Tarefa 4).
2. Colocação simultânea do mesmo modelo com imagem na mesma mesa: uma cópia só (índice único e releitura; o código
   está na Tarefa 4).
3. Imagem pessoal usada como `imagemId` num token comum ou num fundo de mapa tem que ser rejeitada, porque
   `ImagemEhDaMesaAsync` já filtra por `MesaId` (teste na Tarefa 1).
4. Excluir a cópia da imagem na mesa e colocar de novo deve gerar uma cópia nova (teste na Tarefa 4).
5. Categoria renomeada para o nome de outra só mudando maiúsculas: 409 (teste na Tarefa 2).

---

### Tarefa 1: Imagens com escopo pessoal

**Files:**
- Modify: `backend/src/Vtt.Domain/Imagens/ImagemUpload.cs` (`MesaId?`, `UsuarioId?`, `OrigemImagemId?`, miniatura
  derivada do caminho, `CaminhoPessoal`)
- Modify: `backend/src/Vtt.Infrastructure/Persistence/Configurations/ImagemConfiguration.cs` (check constraint, FK de
  usuário em cascata, índice único filtrado)
- Modify: `backend/src/Vtt.Application/Imagens/{ImagemService,IImagemService,ImagemDtos,UploadOptions}.cs`
- Create: `backend/src/Vtt.Api/Controllers/BibliotecaImagensController.cs` (`POST`/`GET /api/biblioteca/imagens`)
- Create: migration `ImagensPessoais`
- Test: `backend/tests/Vtt.Api.IntegrationTests/ImagensPessoaisTests.cs`

**Interfaces:**
- Produces: `IImagemService.EnviarPessoalAsync(usuarioId, nome, stream, tamanho, ct) -> Resultado<ImagemDto>` e
  `ListarPessoaisAsync(usuarioId, ct) -> GaleriaDto`.
- Produces: `IImagemService.ObterCopiaNaMesaAsync(Guid usuarioId, Guid imagemPessoalId, Guid mesaId, ct) -> Resultado<Guid>`.
- Produces: `ImagemDto.MesaId` passa a ser `Guid?`.
- O núcleo comum da gravação fica em `GravarAsync(ImagemUpload modelo, byte[] dados, ct)`.

- [ ] Escrever `ImagensPessoaisTests`:
  - enviar e listar;
  - o dono baixa o arquivo e a miniatura, e outro usuário recebe 404;
  - outro usuário não exclui;
  - uma imagem pessoal não serve de `imagemId` em `POST /api/mapas/{id}/tokens` (400).
- [ ] Rodar o teste e ver falhar.
- [ ] Implementar a entidade, a configuração, o serviço, o controller e a migration.
- [ ] Rodar `ImagensPessoaisTests` e `ImagensTests` e ver passar.

### Tarefa 2: Categorias e modelos (CRUD)

**Files:**
- Create: `backend/src/Vtt.Domain/Biblioteca/{CategoriaDeModelo,ModeloDeToken}.cs`
- Create: `backend/src/Vtt.Application/Biblioteca/{BibliotecaDtos,BibliotecaValidators,IBibliotecaService,BibliotecaService}.cs`
- Create: `backend/src/Vtt.Infrastructure/Persistence/Configurations/BibliotecaConfiguration.cs`
- Modify: `IAppDbContext`, `AppDbContext` (`CategoriasDeModelo`, `ModelosDeToken`), `DependencyInjection` da Application
- Modify: `ImagemService.ExcluirAsync` (409 quando um modelo usa a imagem pessoal)
- Create: `backend/src/Vtt.Api/Controllers/BibliotecaController.cs`
- Create: migration `BibliotecaDeTokens`
- Test: `BibliotecaCategoriasTests.cs`, `BibliotecaModelosTests.cs`

**Interfaces:**
- Produces: `ModeloDeToken.CriarToken(Guid mapaId, string nome, int x, int y, Guid? imagemId, bool visivel) -> Token`.
- Produces: `CategoriaDto(Guid Id, string Nome, int QuantidadeDeModelos)`.
- Produces: `ModeloDeTokenDto(Id, CategoriaId?, Tipo, Nome, HpMaximo, Icone, ImagemId?, CorBorda, Deslocamento, Visao,
  ExibicaoHp, CriadoEm, AtualizadoEm)`.
- Produces: `ModeloDeTokenRequest(CategoriaId?, Tipo?, Nome?, HpMaximo, Icone?, ImagemId?, CorBorda?, Deslocamento = 6,
  Visao = 0, ExibicaoHp? = null)`.

- [ ] Escrever os testes de categorias:
  - CRUD;
  - duplicata sem diferenciar maiúsculas, ao criar e ao renomear;
  - excluir deixa os modelos sem categoria;
  - as sugeridas são idempotentes;
  - outro usuário recebe 404.
- [ ] Escrever os testes de modelos:
  - CRUD;
  - `Personagem` dá 400;
  - ícone inválido para o tipo dá 400;
  - imagem de mesa dá 400;
  - imagem pessoal de outro usuário dá 400;
  - categoria de outro usuário dá 400;
  - outro usuário recebe 404;
  - excluir imagem pessoal em uso dá 409.
- [ ] Ver os testes falharem, implementar e ver passarem.

### Tarefa 3: Colocar modelos no mapa

**Files:**
- Modify: `backend/src/Vtt.Application/Mapas/{ITokenService,TokenService,MapaDtos}.cs`
  (`CriarDaBibliotecaRequest(Guid? ModeloId, int Quantidade = 1, bool? Visivel = null)`, `CriarDaBibliotecaAsync`)
- Modify: `backend/src/Vtt.Api/Controllers/MapasController.cs` (`POST {id}/tokens/da-biblioteca`)
- Test: `ColocarDaBibliotecaTests.cs`

- [ ] Escrever os testes:
  - N tokens com HP cheio e os campos do modelo;
  - a numeração continua a partir dos nomes que já existem;
  - nome longo com sufixo cabe no limite;
  - jogador recebe 403 e quem não é membro recebe 404;
  - modelo alheio dá 404;
  - mapa cheio dá 409 sem criar nada;
  - imagem copiada uma vez para N tokens e em duas colocações, e copiada de novo depois que a cópia é excluída;
  - um membro baixa a cópia;
  - `visivel: false` não aparece no estado do jogador;
  - editar ou excluir o modelo não muda os tokens;
  - quantidade 0 ou 21 dá 400.
- [ ] Ver os testes falharem, implementar e ver passarem. Depois, rodar a suíte inteira.

### Tarefa 4: Frontend, página da biblioteca

**Files:**
- Create: `frontend/src/features/biblioteca/{bibliotecaApi.ts,BibliotecaPage.tsx,ModeloCard.tsx,ModeloFormDialogo.tsx,CategoriaDialogo.tsx}`
- Create: `frontend/src/features/mapa/CamposDoToken.tsx` (extraído do `TokenFormDialogo`)
- Modify: `TokenFormDialogo.tsx`, `SeletorDeImagem.tsx` (prop `fonte`), `app/App.tsx`, `app/Layout.tsx`, `styles.css`

- [ ] Implementar. Depois, rodar `npm run build` e `npm test`.

### Tarefa 5: Frontend, colocar pelo painel do Mestre

**Files:**
- Create: `frontend/src/features/mapa/ColocarDaBibliotecaDialogo.tsx`
- Modify: `PainelDoMestre.tsx`, `mapaApi.ts` (`colocarDaBiblioteca`)

- [ ] Implementar. Depois, rodar `npm run build` e `npm test`.

### Tarefa 6: Documentação

- [ ] No README: seção "Biblioteca de tokens (API)", roteiro manual e as novas variáveis `Uploads__CotaImagensPorUsuario` e
  `Uploads__CotaBytesPorUsuario`.
- [ ] Na spec: seção "Diferenças na implementação", se houver.
