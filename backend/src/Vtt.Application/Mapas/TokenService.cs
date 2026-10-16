using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using Vtt.Application.Common;
using Vtt.Application.Iniciativas;
using Vtt.Application.Imagens;
using Vtt.Application.Mesas;
using Vtt.Domain.Mapas;
using Vtt.Domain.Mesas;
using Vtt.Domain.Personagens;

namespace Vtt.Application.Mapas;

public class TokenService(
    IAppDbContext db, IMapaNotifier notifier, IImagemService imagens, MovimentoDaRodada rodada, TurnoDaMesa turno) : ITokenService
{
    private const string TokenNaoEncontrado = "Token não encontrado.";
    private const string CelulaOcupada = "Já existe um token nesta célula.";
    private const string ImagemDeOutraMesa = "Escolha uma imagem da galeria desta mesa.";
    private const string VersaoDesatualizada = "O token foi alterado por outra pessoa. A posição foi atualizada.";
    private const string AlteradoAoMesmoTempo = "O token foi alterado ao mesmo tempo por outra ação. Tente de novo.";
    private const string ForaDoMapa = "O destino fica fora do mapa.";
    private const string EstruturaBloqueando = "Há uma estrutura bloqueando esta célula.";
    private const string UseOsBlocos = "Clique no seu token e escolha uma das células destacadas para mover.";
    private const string TokenCaido = "O token está caído e não pode se mover.";
    private const string ForaDaVez = "Aguarde a sua vez na iniciativa.";

    public async Task<Resultado<TokenDto>> CriarAsync(Guid usuarioId, Guid mapaId, CriarTokenRequest request, CancellationToken cancellationToken = default)
    {
        var mapa = await db.Mapas.AsNoTracking().FirstOrDefaultAsync(m => m.Id == mapaId, cancellationToken);
        if (mapa is null)
        {
            return Erro.NaoEncontrado();
        }

        var erro = await db.ExigirMestreAsync(usuarioId, mapa.MesaId, cancellationToken);
        if (erro is not null)
        {
            return erro;
        }

        if (request.ImagemId is { } imagemId && !await db.ImagemEhDaMesaAsync(imagemId, mapa.MesaId, cancellationToken))
        {
            return Erro.Invalido("imagemId", ImagemDeOutraMesa);
        }

        // O Mestre vê os tokens ocultos, então para ele todos contam como ocupação.
        (int X, int Y) celula;
        if (request.X is { } x && request.Y is { } y)
        {
            if (!mapa.Contem(x, y))
            {
                return Erro.Invalido("x", "A posição fica fora do mapa.");
            }

            // Com a posição escolhida, o Mestre pode colocar o token até sobre uma estrutura.
            if ((await db.CelulasOcupadasAsync(mapaId, somenteVisiveis: false, cancellationToken)).Contains((x, y)))
            {
                return Erro.Conflito(CelulaOcupada);
            }

            celula = (x, y);
        }
        else if (Grade.PrimeiraLivre(mapa.Largura, mapa.Altura, await db.CelulasIndisponiveisAsync(mapaId, somenteVisiveis: false, cancellationToken)) is { } livre)
        {
            celula = livre;
        }
        else
        {
            return Erro.Conflito("O mapa está cheio: não há célula livre para o token.");
        }

        var tipo = request.Tipo!.Value;
        var token = new Token
        {
            Id = Guid.NewGuid(),
            MapaId = mapaId,
            Tipo = tipo,
            Nome = request.Nome!.Trim(),
            HpAtual = request.HpAtual,
            HpMaximo = request.HpMaximo,
            X = celula.X,
            Y = celula.Y,
            CorBorda = NormalizarCor(request.CorBorda),
            Icone = request.Icone!,
            ImagemId = request.ImagemId,
            ExibicaoHp = Token.ExibicaoHpPadrao(tipo),
            Deslocamento = request.Deslocamento ?? Personagem.DeslocamentoPadrao,
            MovimentoRestante = request.Deslocamento ?? Personagem.DeslocamentoPadrao,
            Visao = request.Visao ?? 0
        };

        return await InserirAsync(token, mapa, usuarioId, cancellationToken);
    }

    public async Task<Resultado<IReadOnlyList<TokenDto>>> CriarDaBibliotecaAsync(Guid usuarioId, Guid mapaId, CriarDaBibliotecaRequest request, CancellationToken cancellationToken = default)
    {
        var mapa = await db.Mapas.AsNoTracking().FirstOrDefaultAsync(m => m.Id == mapaId, cancellationToken);
        if (mapa is null)
        {
            return Erro.NaoEncontrado();
        }

        var erro = await db.ExigirMestreAsync(usuarioId, mapa.MesaId, cancellationToken);
        if (erro is not null)
        {
            return erro;
        }

        var modelo = await db.ModelosDeToken.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.ModeloId && m.UsuarioId == usuarioId, cancellationToken);
        if (modelo is null)
        {
            return Erro.NaoEncontrado("Este modelo não existe mais.");
        }

        // Como no CriarAsync: para o Mestre, os tokens ocultos também ocupam a célula.
        var indisponiveis = await db.CelulasIndisponiveisAsync(mapaId, somenteVisiveis: false, cancellationToken);
        var celulas = new List<(int X, int Y)>();
        while (celulas.Count < request.Quantidade && Grade.PrimeiraLivre(mapa.Largura, mapa.Altura, indisponiveis) is { } livre)
        {
            celulas.Add(livre);
            indisponiveis.Add(livre);
        }

        if (celulas.Count < request.Quantidade)
        {
            return Erro.Conflito(celulas.Count == 0
                ? "O mapa está cheio: não há célula livre para os tokens."
                : $"O mapa só tem {celulas.Count} {(celulas.Count == 1 ? "célula livre" : "células livres")}.");
        }

        Guid? imagemDaMesa = null;
        if (modelo.ImagemId is { } imagemPessoal)
        {
            var copia = await imagens.ObterCopiaNaMesaAsync(usuarioId, imagemPessoal, mapa.MesaId, cancellationToken);
            if (!copia.Sucesso)
            {
                return copia.Erro!;
            }

            imagemDaMesa = copia.Valor;
        }

        Guid? conteudoDaMesa = null;
        if (modelo.Tipo == TipoToken.Interacao && modelo.ConteudoImagemId is { } conteudoPessoal)
        {
            var copia = await imagens.ObterCopiaNaMesaAsync(usuarioId, conteudoPessoal, mapa.MesaId, cancellationToken);
            if (!copia.Sucesso)
            {
                return copia.Erro!;
            }

            conteudoDaMesa = copia.Valor;
        }

        var nomes = await NomesNumeradosAsync(mapaId, modelo.Nome, request.Quantidade, cancellationToken);
        var visivel = request.Visivel ?? true;
        var tokens = celulas
            .Select((celula, i) => modelo.CriarToken(mapaId, nomes[i], celula.X, celula.Y, imagemDaMesa, visivel, conteudoDaMesa))
            .ToList();

        db.Tokens.AddRange(tokens);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Índice único da célula: outro token chegou primeiro em alguma delas.
            foreach (var token in tokens)
            {
                db.Tokens.Entry(token).State = EntityState.Detached;
            }

            return Erro.Conflito("Outro token ocupou uma das células ao mesmo tempo. Tente de novo.");
        }

        var alcance = AlcanceDoMapa.De(mapa, usuarioId);
        foreach (var token in tokens)
        {
            await notifier.TokenCriadoAsync(alcance, ProjecoesDoToken.De(token));
        }

        await turno.ReavaliarAsync(mapa, cancellationToken);
        return new Resultado<IReadOnlyList<TokenDto>>(tokens.Select(TokenDto.De).ToList(), null);
    }

    public async Task<Resultado<TokenDto>> EditarAsync(Guid usuarioId, Guid tokenId, EditarTokenRequest request, CancellationToken cancellationToken = default)
    {
        var (token, mapa, erro) = await ObterComoMestreAsync(usuarioId, tokenId, cancellationToken);
        if (token is null)
        {
            return erro!;
        }

        // No token de um jogador, a imagem é a foto da ficha: o pedido não a troca.
        if (token.DonoUsuarioId is null
            && request.ImagemId is { } imagemId
            && imagemId != token.ImagemId
            && !await db.ImagemEhDaMesaAsync(imagemId, mapa!.MesaId, cancellationToken))
        {
            return Erro.Invalido("imagemId", ImagemDeOutraMesa);
        }

        var antes = ProjecoesDoToken.De(token);
        if (token.DonoUsuarioId is not null)
        {
            // Nome, ícone, cor, imagem e HP máximo vêm do personagem do jogador.
            if (request.HpAtual > token.HpMaximo)
            {
                return Erro.Invalido("hpAtual", "O HP atual deve ficar entre 0 e o HP máximo.");
            }
        }
        else
        {
            if (!Token.IconeValido(token.Tipo, request.Icone))
            {
                return Erro.Invalido("icone", RegrasDeToken.IconeInvalido);
            }

            token.Nome = request.Nome!.Trim();
            token.HpMaximo = request.HpMaximo;
            token.Icone = request.Icone!;
            token.CorBorda = NormalizarCor(request.CorBorda);
            if (request.Deslocamento is { } deslocamento)
            {
                token.DefinirDeslocamento(deslocamento);
            }

            token.Visao = request.Visao ?? token.Visao;
            token.ImagemId = request.ImagemId;
        }

        token.HpAtual = request.HpAtual;
        token.ExibicaoHp = request.ExibicaoHp ?? token.ExibicaoHp;
        if (token.EhInteracao)
        {
            // A Interação não tem HP nem movimento: o que vier no pedido é ignorado.
            token.AplicarValoresDeInteracao();
        }

        if (request.Visivel is { } visivel && visivel != token.Visivel)
        {
            erro = await AplicarVisibilidadeAsync(token, mapa!, visivel, cancellationToken);
            if (erro is not null)
            {
                return erro;
            }
        }

        return await GravarAlteracaoAsync(token, mapa!, usuarioId, antes, cancellationToken);
    }

    public async Task<Resultado<TokenDto>> DefinirHpAsync(Guid usuarioId, Guid tokenId, HpRequest request, CancellationToken cancellationToken = default)
    {
        var (token, mapa, erro) = await ObterComoMestreAsync(usuarioId, tokenId, cancellationToken);
        if (token is null)
        {
            return erro!;
        }

        if (token.EhInteracao)
        {
            return Erro.Invalido("delta", "Objetos de interação não têm HP.");
        }

        if (request.HpMaximo is not null && token.DonoUsuarioId is not null)
        {
            return Erro.Invalido("hpMaximo", "O HP máximo do token de um jogador vem do personagem dele.");
        }

        var antes = ProjecoesDoToken.De(token);
        var (novoMaximo, definido, delta) = (request.HpMaximo, request.HpAtual, request.Delta ?? 0);

        // A conta é feita pelo banco em um só UPDATE: dois cliques rápidos de dano somam, em vez de
        // o segundo sobrescrever o primeiro, e o resultado fica sempre entre 0 e o máximo.
        await db.Tokens
            .Where(t => t.Id == tokenId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.HpMaximo, t => novoMaximo ?? t.HpMaximo)
                    .SetProperty(t => t.HpAtual, t => Math.Max(0, Math.Min(novoMaximo ?? t.HpMaximo, definido ?? t.HpAtual + delta)))
                    .SetProperty(t => t.Versao, t => t.Versao + 1),
                cancellationToken);

        var entrada = db.Tokens.Entry(token);
        await entrada.ReloadAsync(cancellationToken);
        if (entrada.State == EntityState.Detached)
        {
            return Erro.NaoEncontrado();
        }

        await notifier.HpAlteradoAsync(AlcanceDoMapa.De(mapa!, usuarioId), antes, ProjecoesDoToken.De(token));
        await turno.ReavaliarAsync(mapa!, cancellationToken);
        return TokenDto.De(token);
    }

    public async Task<Resultado<TokenDto>> DefinirExibicaoAsync(Guid usuarioId, Guid tokenId, ExibicaoRequest request, CancellationToken cancellationToken = default)
    {
        var (token, mapa, erro) = await ObterComoMestreAsync(usuarioId, tokenId, cancellationToken);
        if (token is null)
        {
            return erro!;
        }

        var antes = ProjecoesDoToken.De(token);
        var mudouExibicao = request.ExibicaoHp is { } exibicao && exibicao != token.ExibicaoHp;
        var mudouVisibilidade = request.Visivel is { } pedido && pedido != token.Visivel;
        if (!mudouExibicao && !mudouVisibilidade)
        {
            return TokenDto.De(token);
        }

        token.ExibicaoHp = request.ExibicaoHp ?? token.ExibicaoHp;
        if (mudouVisibilidade)
        {
            erro = await AplicarVisibilidadeAsync(token, mapa!, request.Visivel!.Value, cancellationToken);
            if (erro is not null)
            {
                return erro;
            }
        }

        return await GravarAlteracaoAsync(token, mapa!, usuarioId, antes, cancellationToken);
    }

    public async Task<Resultado<TokenDto>> DuplicarAsync(Guid usuarioId, Guid tokenId, CancellationToken cancellationToken = default)
    {
        var (token, mapa, erro) = await ObterComoMestreAsync(usuarioId, tokenId, cancellationToken);
        if (token is null)
        {
            return erro!;
        }

        if (token.DonoUsuarioId is not null)
        {
            return Erro.Conflito("O token de um jogador não pode ser duplicado.");
        }

        var ocupadas = await db.CelulasIndisponiveisAsync(token.MapaId, somenteVisiveis: false, cancellationToken);
        if (Grade.LivreMaisProxima(mapa!.Largura, mapa.Altura, ocupadas, token.X, token.Y) is not { } celula)
        {
            return Erro.Conflito("O mapa está cheio: não há célula livre para a cópia.");
        }

        var copia = new Token
        {
            Id = Guid.NewGuid(),
            MapaId = token.MapaId,
            Tipo = token.Tipo,
            Nome = token.Nome,
            HpAtual = token.HpAtual,
            HpMaximo = token.HpMaximo,
            X = celula.X,
            Y = celula.Y,
            CorBorda = token.CorBorda,
            Icone = token.Icone,
            ImagemId = token.ImagemId,
            Visivel = token.Visivel,
            ExibicaoHp = token.ExibicaoHp,
            Deslocamento = token.Deslocamento,
            MovimentoRestante = token.Deslocamento,
            Visao = token.Visao,
            Descricao = token.Descricao,
            ConteudoImagemId = token.ConteudoImagemId,
            Revelado = token.Revelado
        };

        return await InserirAsync(copia, mapa, usuarioId, cancellationToken);
    }

    public async Task<Resultado<InteracaoDto>> ObterInteracaoAsync(Guid usuarioId, Guid tokenId, CancellationToken cancellationToken = default)
    {
        var token = await db.Tokens.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tokenId, cancellationToken);
        if (token is null)
        {
            return Erro.NaoEncontrado();
        }

        var mapa = await db.Mapas.AsNoTracking().FirstAsync(m => m.Id == token.MapaId, cancellationToken);
        var papel = await db.ObterPapelAsync(usuarioId, mapa.MesaId, cancellationToken);
        if (papel == PapelNaMesa.Mestre)
        {
            return token.EhInteracao ? InteracaoDto.De(token) : Erro.NaoEncontrado("Este token não é uma interação.");
        }

        // Para o jogador, um token oculto (ou de um mapa em preparação) é um id que não existe.
        if (papel is null || !mapa.Ativo || !token.Visivel)
        {
            return Erro.NaoEncontrado();
        }

        if (!token.EhInteracao)
        {
            return Erro.NaoEncontrado("Este token não é uma interação.");
        }

        return await db.JogadorPodeExaminarAsync(token, usuarioId, cancellationToken)
            ? InteracaoDto.De(token)
            : Erro.Proibido(AcessoAInteracao.Aproxime);
    }

    public async Task<Resultado<TokenDto>> EditarInteracaoAsync(Guid usuarioId, Guid tokenId, InteracaoRequest request, CancellationToken cancellationToken = default)
    {
        var (token, mapa, erro) = await ObterComoMestreAsync(usuarioId, tokenId, cancellationToken);
        if (token is null)
        {
            return erro!;
        }

        if (!token.EhInteracao)
        {
            return Erro.Invalido("tipo", "Este token não é uma interação.");
        }

        if (request.ConteudoImagemId is { } imagemId
            && imagemId != token.ConteudoImagemId
            && !await db.ImagemEhDaMesaAsync(imagemId, mapa!.MesaId, cancellationToken))
        {
            return Erro.Invalido("conteudoImagemId", ImagemDeOutraMesa);
        }

        var antes = ProjecoesDoToken.De(token);
        token.Descricao = string.IsNullOrWhiteSpace(request.Descricao) ? null : request.Descricao.Trim();
        token.ConteudoImagemId = request.ConteudoImagemId;
        token.Revelado = request.Revelado ?? token.Revelado;
        return await GravarAlteracaoAsync(token, mapa!, usuarioId, antes, cancellationToken);
    }

    public async Task<Resultado> RemoverAsync(Guid usuarioId, Guid tokenId, CancellationToken cancellationToken = default)
    {
        var (token, mapa, erro) = await ObterComoMestreAsync(usuarioId, tokenId, cancellationToken);
        if (token is null)
        {
            return erro!;
        }

        if (token.DonoUsuarioId is not null)
        {
            return Erro.Conflito("O token de um jogador só sai do mapa quando o jogador sai da mesa.");
        }

        // DELETE direto: remover não depende da versão, então não disputa com um movimento em andamento.
        await db.Tokens.Where(t => t.Id == tokenId).ExecuteDeleteAsync(cancellationToken);
        await notifier.TokenRemovidoAsync(AlcanceDoMapa.De(mapa!, usuarioId), ProjecoesDoToken.De(token));
        await turno.ReavaliarAsync(mapa!, cancellationToken);
        return Resultado.Ok;
    }

    public async Task<Resultado<MovimentoResultado>> MoverAsync(Guid usuarioId, Guid tokenId, int x, int y, int versaoEsperada, CancellationToken cancellationToken = default)
    {
        // O arraste livre não espera a vez: o do Mestre ajusta a cena a qualquer momento, e o do
        // jogador só existe com a movimentação livre, em que a iniciativa não vale.
        var (token, mapa, papel, visao, falha) = await ObterParaMoverAsync(usuarioId, tokenId, cancellationToken, exigirVez: false);
        if (token is null)
        {
            return falha!.Value;
        }

        // O arraste livre é do Mestre; o jogador só arrasta o próprio token se a mesa estiver com a
        // movimentação livre. Senão, anda pelo movimento por blocos, que cobra o custo.
        var jogador = papel == PapelNaMesa.Jogador;
        if (jogador)
        {
            var movimentacao = await db.Mesas
                .Where(mesa => mesa.Id == mapa!.MesaId)
                .Select(mesa => mesa.Movimentacao)
                .FirstAsync(cancellationToken);
            if (movimentacao != ModoDeMovimentacao.Livre)
            {
                return MovimentoResultado.Rejeitado(UseOsBlocos, token, visao);
            }
        }

        if (!mapa!.Contem(x, y))
        {
            return MovimentoResultado.Rejeitado(ForaDoMapa, token, visao);
        }

        if (token.Versao != versaoEsperada)
        {
            return MovimentoResultado.Rejeitado(VersaoDesatualizada, token, visao);
        }

        if (token.X == x && token.Y == y)
        {
            return MovimentoResultado.Ok(token, visao);
        }

        // O Mestre vê todos os tokens, então para ele todos ocupam, e as estruturas não o barram. Para o
        // jogador, como no movimento por blocos, um token oculto não ocupa e as estruturas barram.
        if (jogador)
        {
            var ocupadas = await db.CelulasOcupadasAsync(token.MapaId, somenteVisiveis: true, cancellationToken);
            ocupadas.Remove((token.X, token.Y));
            if (ocupadas.Contains((x, y)))
            {
                return MovimentoResultado.Rejeitado(CelulaOcupada, token, visao);
            }

            if ((await db.CelulasBloqueadasAsync(token.MapaId, cancellationToken)).Contains((x, y)))
            {
                return MovimentoResultado.Rejeitado(EstruturaBloqueando, token, visao);
            }
        }
        else if (await db.Tokens.AnyAsync(
                     t => t.MapaId == token.MapaId && t.Id != token.Id && t.X == x && t.Y == y,
                     cancellationToken))
        {
            return MovimentoResultado.Rejeitado(CelulaOcupada, token, visao);
        }

        token.X = x;
        token.Y = y;

        // O Mestre reposicionou o token: o movimento por blocos pendente deixa de poder ser desfeito.
        var tinhaPendencia = token.MovimentoPendente;
        token.ConcluirMovimento();
        return await GravarMovimentoAsync(token, mapa, usuarioId, visao, moveu: true, mudouMovimento: tinhaPendencia, cancellationToken);
    }

    public async Task<Resultado<MovimentoResultado>> MoverPorBlocosAsync(Guid usuarioId, Guid tokenId, int x, int y, int versaoEsperada, CancellationToken cancellationToken = default)
    {
        var (token, mapa, papel, visao, falha) = await ObterParaMoverAsync(usuarioId, tokenId, cancellationToken);
        if (token is null)
        {
            return falha!.Value;
        }

        if (!mapa!.Contem(x, y))
        {
            return MovimentoResultado.Rejeitado(ForaDoMapa, token, visao);
        }

        if (token.Versao != versaoEsperada)
        {
            return MovimentoResultado.Rejeitado(VersaoDesatualizada, token, visao);
        }

        if (token.MovimentoPendente)
        {
            return MovimentoResultado.Rejeitado("Conclua ou desfaça o movimento anterior antes de mover de novo.", token, visao);
        }

        if (token.X == x && token.Y == y)
        {
            return MovimentoResultado.Ok(token, visao);
        }

        // Para o jogador, um token oculto não ocupa a célula: barrar o movimento denunciaria que há
        // algo escondido ali. O Mestre vê todos, então para ele todos contam.
        var ocupadas = await db.CelulasOcupadasAsync(token.MapaId, somenteVisiveis: papel == PapelNaMesa.Jogador, cancellationToken);
        ocupadas.Remove((token.X, token.Y));
        if (ocupadas.Contains((x, y)))
        {
            return MovimentoResultado.Rejeitado(CelulaOcupada, token, visao);
        }

        var bloqueadas = await db.CelulasBloqueadasAsync(token.MapaId, cancellationToken);
        if (bloqueadas.Contains((x, y)))
        {
            return MovimentoResultado.Rejeitado(EstruturaBloqueando, token, visao);
        }

        var alcance = Alcance.Calcular(mapa.Largura, mapa.Altura, (token.X, token.Y), token.MovimentoRestante, bloqueadas, ocupadas);
        if (!alcance.TryGetValue((x, y), out var custo))
        {
            return MovimentoResultado.Rejeitado(
                token.MovimentoRestante == 0
                    ? "O token não tem mais movimento nesta rodada."
                    : $"Fora do alcance: restam {token.MovimentoRestante} de {token.MovimentoMaximo} células de movimento.",
                token,
                visao);
        }

        token.OrigemPendenteX = token.X;
        token.OrigemPendenteY = token.Y;
        token.CustoPendente = custo;
        token.MovimentoRestante -= custo;
        token.X = x;
        token.Y = y;
        return await GravarMovimentoAsync(token, mapa, usuarioId, visao, moveu: true, mudouMovimento: true, cancellationToken);
    }

    public async Task<Resultado<MovimentoResultado>> DesfazerMovimentoAsync(Guid usuarioId, Guid tokenId, int versaoEsperada, CancellationToken cancellationToken = default)
    {
        var (token, mapa, papel, visao, falha) = await ObterParaMoverAsync(usuarioId, tokenId, cancellationToken);
        if (token is null)
        {
            return falha!.Value;
        }

        if (token.Versao != versaoEsperada)
        {
            return MovimentoResultado.Rejeitado(VersaoDesatualizada, token, visao);
        }

        if (token is not { OrigemPendenteX: { } origemX, OrigemPendenteY: { } origemY })
        {
            return MovimentoResultado.Rejeitado("Não há movimento para desfazer.", token, visao);
        }

        // Mesma regra de ocupação do movimento: para o jogador, os ocultos não contam.
        var contaOcultos = papel == PapelNaMesa.Mestre;
        if (await db.Tokens.AnyAsync(
                t => t.MapaId == token.MapaId && t.Id != token.Id && t.X == origemX && t.Y == origemY && (contaOcultos || t.Visivel),
                cancellationToken))
        {
            return MovimentoResultado.Rejeitado("A célula de origem foi ocupada. Não dá para desfazer.", token, visao);
        }

        token.X = origemX;
        token.Y = origemY;
        token.MovimentoRestante = Math.Min(token.MovimentoMaximo, token.MovimentoRestante + token.CustoPendente);
        token.ConcluirMovimento();
        return await GravarMovimentoAsync(token, mapa!, usuarioId, visao, moveu: true, mudouMovimento: true, cancellationToken);
    }

    public async Task<Resultado<MovimentoResultado>> ConcluirMovimentoAsync(Guid usuarioId, Guid tokenId, int versaoEsperada, CancellationToken cancellationToken = default)
    {
        var (token, mapa, _, visao, falha) = await ObterParaMoverAsync(usuarioId, tokenId, cancellationToken);
        if (token is null)
        {
            return falha!.Value;
        }

        if (token.Versao != versaoEsperada)
        {
            return MovimentoResultado.Rejeitado(VersaoDesatualizada, token, visao);
        }

        if (!token.MovimentoPendente)
        {
            return MovimentoResultado.Ok(token, visao);
        }

        token.ConcluirMovimento();
        return await GravarMovimentoAsync(token, mapa!, usuarioId, visao, moveu: false, mudouMovimento: true, cancellationToken);
    }

    public async Task<Resultado<MovimentoResultado>> DispararAsync(Guid usuarioId, Guid tokenId, int versaoEsperada, CancellationToken cancellationToken = default)
    {
        var (token, mapa, _, visao, falha) = await ObterParaMoverAsync(usuarioId, tokenId, cancellationToken);
        if (token is null)
        {
            return falha!.Value;
        }

        if (token.Versao != versaoEsperada)
        {
            return MovimentoResultado.Rejeitado(VersaoDesatualizada, token, visao);
        }

        if (token.Disparada)
        {
            return MovimentoResultado.Rejeitado("A Disparada já foi usada nesta rodada.", token, visao);
        }

        token.Disparar();
        return await GravarMovimentoAsync(token, mapa!, usuarioId, visao, moveu: false, mudouMovimento: true, cancellationToken);
    }

    public async Task<Resultado<MovimentoResultado>> DesfazerDisparadaAsync(Guid usuarioId, Guid tokenId, int versaoEsperada, CancellationToken cancellationToken = default)
    {
        var (token, mapa, _, visao, falha) = await ObterParaMoverAsync(usuarioId, tokenId, cancellationToken);
        if (token is null)
        {
            return falha!.Value;
        }

        if (token.Versao != versaoEsperada)
        {
            return MovimentoResultado.Rejeitado(VersaoDesatualizada, token, visao);
        }

        if (!token.Disparada)
        {
            return MovimentoResultado.Rejeitado("A Disparada não foi usada nesta rodada.", token, visao);
        }

        if (!token.PodeDesfazerDisparada)
        {
            return MovimentoResultado.Rejeitado("O movimento extra da Disparada já foi gasto. Desfaça o movimento antes.", token, visao);
        }

        token.DesfazerDisparada();
        return await GravarMovimentoAsync(token, mapa!, usuarioId, visao, moveu: false, mudouMovimento: true, cancellationToken);
    }

    public async Task<Resultado> NovaRodadaAsync(Guid usuarioId, Guid mapaId, CancellationToken cancellationToken = default)
    {
        var mapa = await db.Mapas.AsNoTracking().FirstOrDefaultAsync(m => m.Id == mapaId, cancellationToken);
        if (mapa is null)
        {
            return Erro.NaoEncontrado();
        }

        var erro = await db.ExigirMestreAsync(usuarioId, mapa.MesaId, cancellationToken);
        if (erro is not null)
        {
            return erro;
        }

        if (mapa.Ativo && await db.Mesas.AnyAsync(m => m.Id == mapa.MesaId && m.Iniciativa == EstadoDaIniciativa.EmCombate, cancellationToken))
        {
            return Erro.Conflito("Durante o combate, a rodada avança pela iniciativa.");
        }

        await rodada.RestaurarAsync(AlcanceDoMapa.De(mapa, usuarioId), db.Tokens.Where(t => t.MapaId == mapaId), cancellationToken);
        return Resultado.Ok;
    }

    public async Task<Resultado<TokenDto>> RestaurarMovimentoAsync(Guid usuarioId, Guid tokenId, CancellationToken cancellationToken = default)
    {
        var (token, mapa, erro) = await ObterComoMestreAsync(usuarioId, tokenId, cancellationToken);
        if (token is null)
        {
            return erro!;
        }

        await rodada.RestaurarAsync(AlcanceDoMapa.De(mapa!, usuarioId), db.Tokens.Where(t => t.Id == tokenId), cancellationToken);
        var entrada = db.Tokens.Entry(token);
        await entrada.ReloadAsync(cancellationToken);
        return entrada.State == EntityState.Detached ? Erro.NaoEncontrado() : TokenDto.De(token);
    }

    /// <summary>
    /// Oculta ou revela. Ao revelar em uma célula onde os jogadores já veem outro token, este vai
    /// para a célula livre mais próxima: dois tokens visíveis nunca dividem uma célula.
    /// </summary>
    private async Task<Erro?> AplicarVisibilidadeAsync(Token token, Mapa mapa, bool visivel, CancellationToken cancellationToken)
    {
        token.Visivel = visivel;
        if (!visivel)
        {
            return null;
        }

        var ocupada = await db.Tokens.AnyAsync(
            t => t.MapaId == token.MapaId && t.Id != token.Id && t.Visivel && t.X == token.X && t.Y == token.Y, cancellationToken);
        if (!ocupada)
        {
            return null;
        }

        var indisponiveis = await db.CelulasIndisponiveisAsync(token.MapaId, somenteVisiveis: true, cancellationToken);
        if (Grade.LivreMaisProxima(mapa.Largura, mapa.Altura, indisponiveis, token.X, token.Y) is not { } livre)
        {
            return Erro.Conflito("Não há célula livre para revelar o token.");
        }

        token.X = livre.X;
        token.Y = livre.Y;
        token.ConcluirMovimento();
        return null;
    }

    private async Task<Resultado<TokenDto>> GravarAlteracaoAsync(Token token, Mapa mapa, Guid usuarioId, ProjecoesDoToken antes, CancellationToken cancellationToken)
    {
        token.Versao++;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Versão alterada por outra ação, ou a célula escolhida ao revelar acabou de ser ocupada.
            return Erro.Conflito(AlteradoAoMesmoTempo);
        }

        await notifier.TokenAlteradoAsync(AlcanceDoMapa.De(mapa, usuarioId), antes, ProjecoesDoToken.De(token));
        await turno.ReavaliarAsync(mapa, cancellationToken);
        return TokenDto.De(token);
    }

    private async Task<Resultado<MovimentoResultado>> RejeitarComEstadoAtualAsync(Token token, string motivo, VisaoDoToken visao, CancellationToken cancellationToken)
    {
        var entrada = db.Tokens.Entry(token);
        await entrada.ReloadAsync(cancellationToken);
        if (entrada.State == EntityState.Detached)
        {
            // Foi removido enquanto o movimento era gravado.
            return Erro.NaoEncontrado(TokenNaoEncontrado);
        }

        return MovimentoResultado.Rejeitado(motivo, token, visao);
    }

    /// <summary>
    /// Um nome sozinho fica como está. Vários ganham número, a partir do maior "Nome N" já no mapa, e o
    /// nome é cortado para o número caber no limite.
    /// </summary>
    private async Task<List<string>> NomesNumeradosAsync(Guid mapaId, string nome, int quantidade, CancellationToken cancellationToken)
    {
        if (quantidade == 1)
        {
            return [nome];
        }

        var numerados = new Regex($"^{Regex.Escape(nome)} (\\d{{1,6}})$");
        var existentes = await db.Tokens
            .Where(t => t.MapaId == mapaId && t.Nome.StartsWith(nome + " "))
            .Select(t => t.Nome)
            .ToListAsync(cancellationToken);
        var ultimo = existentes
            .Select(existente => numerados.Match(existente))
            .Where(encontrado => encontrado.Success)
            .Select(encontrado => int.Parse(encontrado.Groups[1].Value))
            .DefaultIfEmpty(0)
            .Max();

        return Enumerable.Range(ultimo + 1, quantidade)
            .Select(numero =>
            {
                var sufixo = $" {numero}";
                var baseDoNome = nome.Length + sufixo.Length <= Token.NomeTamanhoMaximo
                    ? nome
                    : nome[..(Token.NomeTamanhoMaximo - sufixo.Length)].TrimEnd();
                return baseDoNome + sufixo;
            })
            .ToList();
    }

    private async Task<Resultado<TokenDto>> InserirAsync(Token token, Mapa mapa, Guid usuarioId, CancellationToken cancellationToken)
    {
        db.Tokens.Add(token);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Índice único da célula: outro token chegou primeiro.
            return Erro.Conflito(CelulaOcupada);
        }

        await notifier.TokenCriadoAsync(AlcanceDoMapa.De(mapa, usuarioId), ProjecoesDoToken.De(token));
        await turno.ReavaliarAsync(mapa, cancellationToken);
        return TokenDto.De(token);
    }

    private async Task<(Token? Token, Mapa? Mapa, Erro? Erro)> ObterComoMestreAsync(Guid usuarioId, Guid tokenId, CancellationToken cancellationToken)
    {
        var token = await db.Tokens.FirstOrDefaultAsync(t => t.Id == tokenId, cancellationToken);
        if (token is null)
        {
            return (null, null, Erro.NaoEncontrado());
        }

        var mapa = await db.Mapas.AsNoTracking().FirstAsync(m => m.Id == token.MapaId, cancellationToken);

        // O jogador recebe Proibido só para tokens que ele vê; para os ocultos (e os de mapas em
        // preparação), a resposta é a de um id que não existe.
        var papel = await db.ObterPapelAsync(usuarioId, mapa.MesaId, cancellationToken);
        if (papel == PapelNaMesa.Mestre)
        {
            return (token, mapa, null);
        }

        var podeSaberQueExiste = papel == PapelNaMesa.Jogador && mapa.Ativo && (token.Visivel || token.DonoUsuarioId == usuarioId);
        return (null, null, podeSaberQueExiste ? Erro.Proibido(MesaAcesso.SomenteMestre) : Erro.NaoEncontrado());
    }

    /// <summary>
    /// O token e o mapa para um pedido de movimento, ou a falha: NaoEncontrado para quem não pode
    /// saber que o token existe; movimento rejeitado para o jogador diante do token visível de outro
    /// ou do próprio token caído.
    /// </summary>
    private async Task<(Token? Token, Mapa? Mapa, PapelNaMesa Papel, VisaoDoToken Visao, Resultado<MovimentoResultado>? Falha)> ObterParaMoverAsync(
        Guid usuarioId, Guid tokenId, CancellationToken cancellationToken, bool exigirVez = true)
    {
        var token = await db.Tokens.FirstOrDefaultAsync(t => t.Id == tokenId, cancellationToken);
        if (token is null)
        {
            return (null, null, default, default, Erro.NaoEncontrado(TokenNaoEncontrado));
        }

        var mapa = await db.Mapas.AsNoTracking().FirstAsync(m => m.Id == token.MapaId, cancellationToken);
        var papel = await db.ObterPapelAsync(usuarioId, mapa.MesaId, cancellationToken);
        if (papel is null || (papel == PapelNaMesa.Jogador && !mapa.Ativo))
        {
            return (null, null, default, default, Erro.NaoEncontrado(TokenNaoEncontrado));
        }

        var visao = TokenDto.VisaoDe(token, usuarioId, papel.Value);
        if (visao == VisaoDoToken.Jogador)
        {
            // Diante de um token oculto, a resposta é a mesma de um id que não existe.
            return (null, null, default, default, token.Visivel
                ? MovimentoResultado.Rejeitado("Você só pode mover o seu próprio token.", token, visao)
                : Erro.NaoEncontrado(TokenNaoEncontrado));
        }

        // Caído, o jogador não faz mais nada com o token: nem andar, nem desfazer ou concluir a
        // jogada, nem a Disparada. O Mestre continua movendo-o como quiser.
        if (papel == PapelNaMesa.Jogador && token.Caido)
        {
            return (null, null, default, default, MovimentoResultado.Rejeitado(TokenCaido, token, visao));
        }

        // Em combate, com a movimentação por blocos, só o token da vez anda (também para o Mestre).
        if (exigirVez && mapa.Ativo && await turno.ForaDaVezAsync(mapa.MesaId, token.Id, cancellationToken))
        {
            return (null, null, default, default, MovimentoResultado.Rejeitado(ForaDaVez, token, visao));
        }

        return (token, mapa, papel.Value, visao, null);
    }

    /// <summary>
    /// Grava a alteração de posição e/ou movimento e avisa: TokenMovido a quem vê o token e
    /// MovimentoAlterado ao Mestre e ao dono.
    /// </summary>
    private async Task<Resultado<MovimentoResultado>> GravarMovimentoAsync(
        Token token, Mapa mapa, Guid usuarioId, VisaoDoToken visao, bool moveu, bool mudouMovimento, CancellationToken cancellationToken)
    {
        token.Versao++;

        // As checagens antes daqui dão a mensagem certa no caso comum; quem decide a disputa entre
        // duas gravações simultâneas é o banco, pela versão e pelo índice único da célula.
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await RejeitarComEstadoAtualAsync(token, VersaoDesatualizada, visao, cancellationToken);
        }
        catch (DbUpdateException)
        {
            return await RejeitarComEstadoAtualAsync(token, CelulaOcupada, visao, cancellationToken);
        }

        var alcance = AlcanceDoMapa.De(mapa, usuarioId);
        var projecoes = ProjecoesDoToken.De(token);
        if (moveu)
        {
            await notifier.TokenMovidoAsync(alcance, projecoes);
        }

        if (mudouMovimento)
        {
            await notifier.MovimentoAlteradoAsync(alcance, projecoes);
        }

        return MovimentoResultado.Ok(token, visao);
    }

    private static string NormalizarCor(string? cor) =>
        string.IsNullOrEmpty(cor) ? Token.CorBordaPadrao : cor.ToUpperInvariant();
}
