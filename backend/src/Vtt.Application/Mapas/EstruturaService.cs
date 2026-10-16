using Microsoft.EntityFrameworkCore;
using Vtt.Application.Common;
using Vtt.Application.Mesas;
using Vtt.Domain.Mapas;

namespace Vtt.Application.Mapas;

public class EstruturaService(IAppDbContext db, IMapaNotifier notifier) : IEstruturaService
{
    private const string AlteradaPorOutro = "A estrutura foi alterada ao mesmo tempo por outra ação. Tente de novo.";

    public async Task<Resultado<EstruturaDto>> CriarAsync(Guid usuarioId, Guid mapaId, EstruturaRequest request, CancellationToken cancellationToken = default)
    {
        var mapa = await db.Mapas.AsNoTracking().FirstOrDefaultAsync(m => m.Id == mapaId, cancellationToken);
        if (mapa is null)
        {
            return Erro.NaoEncontrado();
        }

        var erro = await db.ExigirMestreAsync(usuarioId, mapa.MesaId, cancellationToken)
            ?? await ValidarAsync(mapa, request, texturaAtual: null, cancellationToken);
        if (erro is not null)
        {
            return erro;
        }

        var estrutura = new Estrutura { Id = Guid.NewGuid(), MapaId = mapaId };
        Aplicar(estrutura, request);

        db.Estruturas.Add(estrutura);
        await db.SaveChangesAsync(cancellationToken);

        var dto = EstruturaDto.De(estrutura);
        await notifier.EstruturaAdicionadaAsync(AlcanceDoMapa.De(mapa, usuarioId), dto);
        return dto;
    }

    public async Task<Resultado<EstruturaDto>> EditarAsync(Guid usuarioId, Guid estruturaId, EstruturaRequest request, CancellationToken cancellationToken = default)
    {
        var (estrutura, mapa, erro) = await ObterComoMestreAsync(usuarioId, estruturaId, cancellationToken);
        if (estrutura is null)
        {
            return erro!;
        }

        if (request.Versao is { } versao && versao != estrutura.Versao)
        {
            return Erro.Conflito(AlteradaPorOutro);
        }

        erro = await ValidarAsync(mapa!, request, estrutura.TexturaImagemId, cancellationToken);
        if (erro is not null)
        {
            return erro;
        }

        Aplicar(estrutura, request);
        return await GravarAsync(estrutura, mapa!, usuarioId, cancellationToken);
    }

    public async Task<Resultado<EstruturaDto>> DefinirPortaAsync(Guid usuarioId, Guid estruturaId, bool aberta, CancellationToken cancellationToken = default)
    {
        var (estrutura, mapa, erro) = await ObterComoMestreAsync(usuarioId, estruturaId, cancellationToken);
        if (estrutura is null)
        {
            return erro!;
        }

        if (estrutura.Tipo != TipoEstrutura.Porta)
        {
            return Erro.Conflito("Só portas abrem e fecham.");
        }

        if (estrutura.PortaAberta == aberta)
        {
            return EstruturaDto.De(estrutura);
        }

        estrutura.PortaAberta = aberta;
        return await GravarAsync(estrutura, mapa!, usuarioId, cancellationToken);
    }

    public async Task<Resultado> RemoverAsync(Guid usuarioId, Guid estruturaId, CancellationToken cancellationToken = default)
    {
        var (estrutura, mapa, erro) = await ObterComoMestreAsync(usuarioId, estruturaId, cancellationToken);
        if (estrutura is null)
        {
            return erro!;
        }

        await db.Estruturas.Where(e => e.Id == estruturaId).ExecuteDeleteAsync(cancellationToken);
        await notifier.EstruturaRemovidaAsync(AlcanceDoMapa.De(mapa!, usuarioId), estrutura.MapaId, estruturaId);
        return Resultado.Ok;
    }

    private async Task<Resultado<EstruturaDto>> GravarAsync(Estrutura estrutura, Mapa mapa, Guid usuarioId, CancellationToken cancellationToken)
    {
        estrutura.Versao++;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Erro.Conflito(AlteradaPorOutro);
        }

        var dto = EstruturaDto.De(estrutura);
        await notifier.EstruturaAtualizadaAsync(AlcanceDoMapa.De(mapa, usuarioId), dto);
        return dto;
    }

    /// <summary>O que o validador do request não alcança: caber no mapa e a textura ser da mesa.</summary>
    private async Task<Erro?> ValidarAsync(Mapa mapa, EstruturaRequest request, Guid? texturaAtual, CancellationToken cancellationToken)
    {
        if (request.X + request.Largura > mapa.Largura || request.Y + request.Profundidade > mapa.Altura)
        {
            return Erro.Invalido("x", "A estrutura fica fora do mapa.");
        }

        if (request.TexturaImagemId is { } imagemId
            && imagemId != texturaAtual
            && !await db.ImagemEhDaMesaAsync(imagemId, mapa.MesaId, cancellationToken))
        {
            return Erro.Invalido("texturaImagemId", "Escolha uma imagem da galeria desta mesa.");
        }

        return null;
    }

    private async Task<(Estrutura? Estrutura, Mapa? Mapa, Erro? Erro)> ObterComoMestreAsync(Guid usuarioId, Guid estruturaId, CancellationToken cancellationToken)
    {
        var estrutura = await db.Estruturas.FirstOrDefaultAsync(e => e.Id == estruturaId, cancellationToken);
        if (estrutura is null)
        {
            return (null, null, Erro.NaoEncontrado());
        }

        var mapa = await db.Mapas.AsNoTracking().FirstAsync(m => m.Id == estrutura.MapaId, cancellationToken);
        var erro = await db.ExigirMestreAsync(usuarioId, mapa.MesaId, cancellationToken);
        return erro is null ? (estrutura, mapa, null) : (null, null, erro);
    }

    // O request já passou pelo EstruturaRequestValidator; aqui só normalizamos.
    private static void Aplicar(Estrutura estrutura, EstruturaRequest request)
    {
        estrutura.Tipo = request.Tipo!.Value;
        estrutura.X = request.X;
        estrutura.Y = request.Y;
        estrutura.Largura = request.Largura;
        estrutura.Profundidade = request.Profundidade;
        estrutura.Altura = Math.Round(request.Altura, 2);
        estrutura.RotacaoGraus = request.RotacaoGraus;
        estrutura.Cor = string.IsNullOrEmpty(request.Cor) ? Estrutura.CorPadrao : request.Cor.ToUpperInvariant();
        estrutura.TexturaImagemId = request.TexturaImagemId;
        estrutura.TexturaPadrao = request.TexturaPadrao;
        estrutura.BloqueiaMovimento = estrutura.Tipo != TipoEstrutura.Entrada && (request.BloqueiaMovimento ?? true);
        estrutura.PortaAberta = estrutura.Tipo == TipoEstrutura.Porta && (request.PortaAberta ?? false);
    }
}
