using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Vtt.Application.Common;
using Vtt.Application.Iniciativas;
using Vtt.Application.Mesas;
using Vtt.Domain.Mapas;
using Vtt.Domain.Mesas;

namespace Vtt.Application.Mapas;

public class MapaService(
    IAppDbContext db,
    IMapaNotifier notifier,
    TokensDeJogador tokensDeJogador,
    TurnoDaMesa turno,
    TimeProvider relogio) : IMapaService
{
    public async Task<Resultado<MapaDto>> CriarAsync(Guid usuarioId, Guid mesaId, MapaRequest request, CancellationToken cancellationToken = default)
    {
        var erro = await db.ExigirMestreAsync(usuarioId, mesaId, cancellationToken);
        if (erro is not null)
        {
            return erro;
        }

        var mapa = new Mapa
        {
            Id = Guid.NewGuid(),
            MesaId = mesaId,
            CriadoEm = relogio.GetUtcNow()
        };
        Aplicar(mapa, request);

        db.Mapas.Add(mapa);
        await db.SaveChangesAsync(cancellationToken);

        return MapaDto.De(mapa);
    }

    public async Task<Resultado<MapaEstadoDto>> GerarAsync(Guid usuarioId, Guid mesaId, MapaProceduralRequest request, CancellationToken cancellationToken = default)
    {
        var erro = await db.ExigirMestreAsync(usuarioId, mesaId, cancellationToken);
        if (erro is not null)
        {
            return erro;
        }

        var semente = request.Semente ?? Random.Shared.Next(1, 1_000_000);
        var tema = request.Tema ?? GeradorDeMasmorra.TemaPadrao;
        var mapa = new Mapa
        {
            Id = Guid.NewGuid(),
            MesaId = mesaId,
            CriadoEm = relogio.GetUtcNow(),
            Nome = string.IsNullOrWhiteSpace(request.Nome) ? GeradorDeMasmorra.NomeDe(semente, tema) : request.Nome.Trim(),
            Largura = request.Largura ?? GeradorDeMasmorra.LarguraPadrao,
            Altura = request.Altura ?? GeradorDeMasmorra.AlturaPadrao,
            CorFundo = Mapa.CorFundoPadrao
        };

        var estruturas = GeradorDeMasmorra.Gerar(mapa.Largura, mapa.Altura, semente, tema);
        foreach (var estrutura in estruturas)
        {
            estrutura.Id = Guid.NewGuid();
            estrutura.MapaId = mapa.Id;
        }

        // Um único SaveChanges: o mapa e as estruturas entram juntos ou nada entra.
        db.Mapas.Add(mapa);
        db.Estruturas.AddRange(estruturas);
        await db.SaveChangesAsync(cancellationToken);

        return Montar(mapa, new Cena([], estruturas, []), usuarioId, PapelNaMesa.Mestre);
    }

    public async Task<Resultado<IReadOnlyList<MapaDto>>> ListarAsync(Guid usuarioId, Guid mesaId, CancellationToken cancellationToken = default)
    {
        var papel = await db.ObterPapelAsync(usuarioId, mesaId, cancellationToken);
        if (papel is null)
        {
            return Erro.NaoEncontrado();
        }

        var mapas = await db.Mapas
            .AsNoTracking()
            .Where(mapa => mapa.MesaId == mesaId && (papel == PapelNaMesa.Mestre || mapa.Ativo))
            .OrderBy(mapa => mapa.CriadoEm)
            .ToListAsync(cancellationToken);

        return mapas.Select(MapaDto.De).ToList();
    }

    public async Task<Resultado<MapaDto>> AtualizarAsync(Guid usuarioId, Guid mapaId, MapaRequest request, CancellationToken cancellationToken = default)
    {
        var (mapa, erro) = await ObterComoMestreAsync(usuarioId, mapaId, cancellationToken);
        if (mapa is null)
        {
            return erro!;
        }

        var haTokenDeFora = await db.Tokens.AnyAsync(
            token => token.MapaId == mapaId && (token.X >= request.Largura || token.Y >= request.Altura), cancellationToken);
        if (haTokenDeFora)
        {
            return Erro.Conflito("Há tokens fora dos novos limites. Mova-os antes de reduzir o mapa.");
        }

        var haEstruturaDeFora = await db.Estruturas.AnyAsync(
            estrutura => estrutura.MapaId == mapaId
                && (estrutura.X + estrutura.Largura > request.Largura || estrutura.Y + estrutura.Profundidade > request.Altura),
            cancellationToken);
        if (haEstruturaDeFora)
        {
            return Erro.Conflito("Há estruturas fora dos novos limites. Mova-as ou exclua-as antes de reduzir o mapa.");
        }

        var (largura, altura) = (mapa.Largura, mapa.Altura);
        Aplicar(mapa, request);

        // A névoa guarda um bit por célula, linha a linha: com outra largura, as camadas são remontadas.
        var memorias = new List<MemoriaDeNevoa>();
        if ((largura, altura) != (mapa.Largura, mapa.Altura))
        {
            memorias = await db.MemoriasDeNevoa.Where(m => m.MapaId == mapaId).ToListAsync(cancellationToken);
            foreach (var memoria in memorias)
            {
                memoria.Celulas = MapaDeBits.Redimensionar(memoria.Celulas, largura, altura, mapa.Largura, mapa.Altura);
                memoria.Versao++;
            }
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Erro.Conflito("A névoa do mapa foi alterada ao mesmo tempo. Tente de novo.");
        }

        var dto = MapaDto.De(mapa);
        var alcance = AlcanceDoMapa.De(mapa, usuarioId);
        await notifier.MapaAtualizadoAsync(alcance, dto);
        if (memorias.Count > 0)
        {
            await notifier.MemoriasDeNevoaAlteradasAsync(alcance, memorias.Select(MemoriaDeNevoaDto.De).ToList());
        }

        return dto;
    }

    public async Task<Resultado<MapaDto>> DefinirFundoAsync(Guid usuarioId, Guid mapaId, FundoRequest request, CancellationToken cancellationToken = default)
    {
        var (mapa, erro) = await ObterComoMestreAsync(usuarioId, mapaId, cancellationToken);
        if (mapa is null)
        {
            return erro!;
        }

        if (request.ImagemId is { } imagemId && !await db.ImagemEhDaMesaAsync(imagemId, mapa.MesaId, cancellationToken))
        {
            return Erro.Invalido("imagemId", "Escolha uma imagem da galeria desta mesa.");
        }

        mapa.ImagemFundoId = request.ImagemId;
        mapa.FundoEscala = request.Escala;
        mapa.FundoOffsetX = request.OffsetX;
        mapa.FundoOffsetY = request.OffsetY;
        await db.SaveChangesAsync(cancellationToken);

        var dto = MapaDto.De(mapa);
        await notifier.MapaAtualizadoAsync(AlcanceDoMapa.De(mapa, usuarioId), dto);
        return dto;
    }

    public async Task<Resultado> ExcluirAsync(Guid usuarioId, Guid mapaId, CancellationToken cancellationToken = default)
    {
        var (mapa, erro) = await ObterComoMestreAsync(usuarioId, mapaId, cancellationToken);
        if (mapa is null)
        {
            return erro!;
        }

        // Os tokens e as estruturas saem em cascata no banco.
        db.Mapas.Remove(mapa);
        await db.SaveChangesAsync(cancellationToken);

        if (mapa.Ativo)
        {
            await notifier.MapaAtivoAlteradoAsync(mapa.MesaId, estadoPara: null);
            await turno.ReavaliarAsync(mapa.MesaId, sempreAvisar: true, cancellationToken);
        }

        return Resultado.Ok;
    }

    public async Task<Resultado<MapaEstadoDto>> AtivarAsync(Guid usuarioId, Guid mapaId, CancellationToken cancellationToken = default)
    {
        var (mapa, erro) = await ObterComoMestreAsync(usuarioId, mapaId, cancellationToken);
        if (mapa is null)
        {
            return erro!;
        }

        if (mapa.Ativo)
        {
            return await MontarEstadoAsync(mapa, usuarioId, PapelNaMesa.Mestre, cancellationToken);
        }

        try
        {
            await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

            // Primeiro desativa o anterior: o índice único não admite dois ativos nem por um instante.
            await db.Mapas
                .Where(outro => outro.MesaId == mapa.MesaId && outro.Ativo)
                .ExecuteUpdateAsync(s => s.SetProperty(outro => outro.Ativo, false), cancellationToken);

            mapa.Ativo = true;
            await db.SaveChangesAsync(cancellationToken);
            await tokensDeJogador.TrazerParaOMapaAsync(mapa, cancellationToken);

            await transacao.CommitAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is DbUpdateException or DbException)
        {
            // Outra ativação ou um token criado na mesma célula, ao mesmo tempo; a transação foi desfeita.
            return Erro.Conflito("O mapa foi alterado ao mesmo tempo por outra ação. Tente de novo.");
        }

        // Cada pessoa conectada recebe o novo mapa como ela o vê: sem tokens ocultos nem HP que não lhe cabe.
        var cena = await CarregarAsync(mapa.Id, cancellationToken);
        await notifier.MapaAtivoAlteradoAsync(
            mapa.MesaId,
            (destinatarioId, ehMestre) => Montar(mapa, cena, destinatarioId, ehMestre ? PapelNaMesa.Mestre : PapelNaMesa.Jogador));

        // A iniciativa segue no novo mapa: os valores ficam, e a ordem recomeça na rodada 1.
        await turno.ReiniciarNoMapaAtivoAsync(mapa.MesaId, cancellationToken);
        return Montar(mapa, cena, usuarioId, PapelNaMesa.Mestre);
    }

    public async Task<Resultado<MapaEstadoDto>> ObterEstadoAsync(Guid usuarioId, Guid mapaId, CancellationToken cancellationToken = default)
    {
        var mapa = await db.Mapas.AsNoTracking().FirstOrDefaultAsync(m => m.Id == mapaId, cancellationToken);
        if (mapa is null)
        {
            return Erro.NaoEncontrado();
        }

        var papel = await db.ObterPapelAsync(usuarioId, mapa.MesaId, cancellationToken);
        if (papel is null || (papel == PapelNaMesa.Jogador && !mapa.Ativo))
        {
            return Erro.NaoEncontrado();
        }

        return await MontarEstadoAsync(mapa, usuarioId, papel.Value, cancellationToken);
    }

    public async Task<MapaEstadoDto?> ObterEstadoAtivoAsync(Guid mesaId, Guid usuarioId, PapelNaMesa papel, CancellationToken cancellationToken = default)
    {
        var mapa = await db.Mapas.AsNoTracking().FirstOrDefaultAsync(m => m.MesaId == mesaId && m.Ativo, cancellationToken);
        return mapa is null ? null : await MontarEstadoAsync(mapa, usuarioId, papel, cancellationToken);
    }

    private async Task<(Mapa? Mapa, Erro? Erro)> ObterComoMestreAsync(Guid usuarioId, Guid mapaId, CancellationToken cancellationToken)
    {
        var mapa = await db.Mapas.FirstOrDefaultAsync(m => m.Id == mapaId, cancellationToken);
        if (mapa is null)
        {
            return (null, Erro.NaoEncontrado());
        }

        var erro = await db.ExigirMestreAsync(usuarioId, mapa.MesaId, cancellationToken);
        return erro is null ? (mapa, null) : (null, erro);
    }

    private async Task<MapaEstadoDto> MontarEstadoAsync(Mapa mapa, Guid usuarioId, PapelNaMesa papel, CancellationToken cancellationToken)
    {
        return Montar(mapa, await CarregarAsync(mapa.Id, cancellationToken), usuarioId, papel);
    }

    /// <summary>O que há em um mapa, lido do banco uma vez e montado para cada destinatário.</summary>
    private sealed record Cena(List<Token> Tokens, List<Estrutura> Estruturas, List<MemoriaDeNevoa> Memorias);

    private async Task<Cena> CarregarAsync(Guid mapaId, CancellationToken cancellationToken)
    {
        var tokens = await db.Tokens
            .AsNoTracking()
            .Where(token => token.MapaId == mapaId)
            .OrderBy(token => token.Nome)
            .ThenBy(token => token.Id)
            .ToListAsync(cancellationToken);

        var estruturas = await db.Estruturas
            .AsNoTracking()
            .Where(estrutura => estrutura.MapaId == mapaId)
            .OrderBy(estrutura => estrutura.Y)
            .ThenBy(estrutura => estrutura.X)
            .ThenBy(estrutura => estrutura.Id)
            .ToListAsync(cancellationToken);

        var memorias = await db.MemoriasDeNevoa
            .AsNoTracking()
            .Where(memoria => memoria.MapaId == mapaId)
            .ToListAsync(cancellationToken);

        return new Cena(tokens, estruturas, memorias);
    }

    /// <summary>
    /// O estado do mapa para um usuário: os tokens que ele não pode ver ficam de fora, o HP vem
    /// conforme a visão dele e, da névoa, só as camadas comuns e a dele (o Mestre recebe todas).
    /// </summary>
    private static MapaEstadoDto Montar(Mapa mapa, Cena cena, Guid usuarioId, PapelNaMesa papel) =>
        new(
            MapaDto.De(mapa),
            cena.Tokens.Select(token => TokenDto.Para(token, TokenDto.VisaoDe(token, usuarioId, papel))).OfType<TokenDto>().ToList(),
            cena.Estruturas.Select(EstruturaDto.De).ToList(),
            cena.Memorias.Select(MemoriaDeNevoaDto.De).Where(memoria => memoria.VisivelPara(usuarioId, papel)).ToList());

    // O request já passou pelo MapaRequestValidator; aqui só normalizamos.
    private static void Aplicar(Mapa mapa, MapaRequest request)
    {
        mapa.Nome = request.Nome!.Trim();
        mapa.Largura = request.Largura;
        mapa.Altura = request.Altura;
        mapa.CorFundo = string.IsNullOrEmpty(request.CorFundo) ? Mapa.CorFundoPadrao : request.CorFundo.ToUpperInvariant();
    }
}
