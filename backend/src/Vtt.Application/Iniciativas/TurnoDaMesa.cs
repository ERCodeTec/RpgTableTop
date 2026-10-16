using Microsoft.EntityFrameworkCore;
using Vtt.Application.Common;
using Vtt.Application.Mapas;
using Vtt.Application.Mesas;
using Vtt.Domain.Mapas;
using Vtt.Domain.Mesas;

namespace Vtt.Application.Iniciativas;

/// <summary>
/// A vez na iniciativa: quem está jogando, a passagem para o próximo e a virada da rodada. Os
/// serviços chamam <see cref="ReavaliarAsync(Mapa, CancellationToken)"/> depois de gravar o que pode tirar o token da vez
/// (caiu, foi ocultado, saiu do mapa), e a vez segue sozinha para o próximo.
/// </summary>
public class TurnoDaMesa(IAppDbContext db, IMesaNotifier notifier, MovimentoDaRodada movimento)
{
    /// <summary>A iniciativa como o papel a vê.</summary>
    public async Task<IniciativaDto> ObterAsync(Guid mesaId, PapelNaMesa papel, CancellationToken cancellationToken = default)
    {
        var (mestre, jogadores) = await MontarAsync(mesaId, cancellationToken);
        return papel == PapelNaMesa.Mestre ? mestre : jogadores;
    }

    /// <summary>Manda a iniciativa atual à mesa: cada papel recebe a própria projeção.</summary>
    public async Task AvisarAsync(Guid mesaId, CancellationToken cancellationToken = default)
    {
        var (mestre, jogadores) = await MontarAsync(mesaId, cancellationToken);
        await notifier.IniciativaAlteradaAsync(mesaId, mestre, jogadores);
    }

    /// <summary>
    /// true quando o combate está valendo, a movimentação é por blocos e o token não é o da vez: aí ele
    /// não anda por blocos, nem pelo Mestre. O arraste livre do Mestre não passa por aqui.
    /// </summary>
    public Task<bool> ForaDaVezAsync(Guid mesaId, Guid tokenId, CancellationToken cancellationToken) =>
        db.Mesas.AnyAsync(
            mesa => mesa.Id == mesaId
                && mesa.Iniciativa == EstadoDaIniciativa.EmCombate
                && mesa.Movimentacao == ModoDeMovimentacao.PorBlocos
                && mesa.VezDoTokenId != tokenId,
            cancellationToken);

    /// <summary>Depois de uma mudança em um token do mapa: só interessa se o mapa é o ativo.</summary>
    public Task ReavaliarAsync(Mapa mapa, CancellationToken cancellationToken) =>
        mapa.Ativo ? ReavaliarAsync(mapa.MesaId, sempreAvisar: false, cancellationToken) : Task.CompletedTask;

    /// <summary>
    /// Confere se o token da vez ainda pode jogar; se não (caído, oculto, fora do mapa, sem iniciativa),
    /// a vez vai ao próximo. Também dá a vez a alguém quando ninguém a tinha. Avisa a mesa se a vez
    /// mudou, ou sempre, com <paramref name="sempreAvisar"/> (os valores mudaram).
    /// </summary>
    public async Task ReavaliarAsync(Guid mesaId, bool sempreAvisar, CancellationToken cancellationToken)
    {
        // Inativa, não há vez nem valores: nada a reavaliar ou avisar.
        var estado = await db.Mesas.Where(m => m.Id == mesaId).Select(m => (EstadoDaIniciativa?)m.Iniciativa).FirstOrDefaultAsync(cancellationToken);
        if (estado is null or EstadoDaIniciativa.Inativa)
        {
            return;
        }

        var mudou = await AvancarAsync(mesaId, encerrarAtual: false, cancellationToken);
        if (mudou || sempreAvisar)
        {
            await AvisarAsync(mesaId, cancellationToken);
        }
    }

    /// <summary>
    /// Encerra a vez do token atual (concluindo o movimento pendente) e passa ao próximo; sem
    /// próximo, começa a rodada seguinte. false se outra ação passou a vez antes.
    /// </summary>
    public async Task<bool> EncerrarVezAsync(Guid mesaId, CancellationToken cancellationToken)
    {
        var mudou = await AvancarAsync(mesaId, encerrarAtual: true, cancellationToken);
        if (mudou)
        {
            await AvisarAsync(mesaId, cancellationToken);
        }

        return mudou;
    }

    /// <summary>
    /// Começo do combate ou troca do mapa ativo durante ele: ninguém jogou ainda, a rodada volta a 1
    /// e a vez é do primeiro da ordem no mapa ativo. Fora do combate, só avisa (a lista de grupos
    /// que os jogadores veem depende do mapa).
    /// </summary>
    public async Task ReiniciarNoMapaAtivoAsync(Guid mesaId, CancellationToken cancellationToken)
    {
        var estado = await db.Mesas.Where(m => m.Id == mesaId).Select(m => (EstadoDaIniciativa?)m.Iniciativa).FirstOrDefaultAsync(cancellationToken);
        if (estado is null or EstadoDaIniciativa.Inativa)
        {
            return;
        }

        await ZerarJogadasAsync(mesaId, cancellationToken);
        if (estado == EstadoDaIniciativa.EmCombate)
        {
            await db.Mesas
                .Where(m => m.Id == mesaId)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.Rodada, 1).SetProperty(m => m.VezDoTokenId, (Guid?)null), cancellationToken);
            await AvancarAsync(mesaId, encerrarAtual: false, cancellationToken);
        }

        await AvisarAsync(mesaId, cancellationToken);
    }

    /// <summary>A ordem atual no mapa ativo, para o serviço conferir regras (quem já jogou, se há alguém para jogar).</summary>
    public async Task<(Mapa? Mapa, List<Token> Tokens, IReadOnlyList<Token> Ordem)> CarregarOrdemAsync(Guid mesaId, CancellationToken cancellationToken)
    {
        var mapa = await db.Mapas.AsNoTracking().FirstOrDefaultAsync(m => m.MesaId == mesaId && m.Ativo, cancellationToken);
        if (mapa is null)
        {
            return (null, [], []);
        }

        var tokens = await db.Tokens.AsNoTracking().Where(t => t.MapaId == mapa.Id).ToListAsync(cancellationToken);
        var valores = await db.ValoresDeIniciativa.AsNoTracking().Where(v => v.MesaId == mesaId).ToListAsync(cancellationToken);
        return (mapa, tokens, OrdemDeIniciativa.Calcular(valores, tokens));
    }

    /// <summary>Ninguém jogou: limpa a marca em todos os tokens dos mapas da mesa.</summary>
    public Task ZerarJogadasAsync(Guid mesaId, CancellationToken cancellationToken) =>
        db.Tokens
            .Where(t => t.JogouNaRodada && db.Mapas.Any(m => m.Id == t.MapaId && m.MesaId == mesaId))
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.JogouNaRodada, false), cancellationToken);

    /// <summary>
    /// Escolhe a vez e grava, se ela mudou. A gravação só vale se a vez e a rodada ainda forem as
    /// lidas: de dois "Encerrar vez" ao mesmo tempo, só um passa a vez.
    /// </summary>
    private async Task<bool> AvancarAsync(Guid mesaId, bool encerrarAtual, CancellationToken cancellationToken)
    {
        var mesa = await db.Mesas
            .AsNoTracking()
            .Where(m => m.Id == mesaId)
            .Select(m => new { m.Iniciativa, m.Rodada, m.VezDoTokenId })
            .FirstOrDefaultAsync(cancellationToken);
        if (mesa is not { Iniciativa: EstadoDaIniciativa.EmCombate })
        {
            return false;
        }

        var (mapa, _, ordem) = await CarregarOrdemAsync(mesaId, cancellationToken);
        var atual = ordem.FirstOrDefault(token => token.Id == mesa.VezDoTokenId);
        if (!encerrarAtual && atual is { Caido: false })
        {
            return false;
        }

        if (encerrarAtual && atual is null)
        {
            return false;
        }

        if (encerrarAtual)
        {
            atual!.JogouNaRodada = true;
        }

        var proxima = OrdemDeIniciativa.Proxima(ordem, atual?.Id);
        var novaRodada = false;
        if (proxima is null && ordem.Any(token => !token.Caido))
        {
            // Todos já jogaram: a rodada vira e o primeiro da ordem joga de novo.
            novaRodada = true;
            proxima = ordem.First(token => !token.Caido);
        }

        var novaVez = proxima?.Id;
        var rodada = novaRodada ? mesa.Rodada + 1 : mesa.Rodada;
        if (!encerrarAtual && novaVez == mesa.VezDoTokenId)
        {
            return false;
        }

        var (vezLida, rodadaLida) = (mesa.VezDoTokenId, mesa.Rodada);
        var gravou = await db.Mesas
            .Where(m => m.Id == mesaId
                && m.Iniciativa == EstadoDaIniciativa.EmCombate
                && m.VezDoTokenId == vezLida
                && m.Rodada == rodadaLida)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.VezDoTokenId, novaVez).SetProperty(m => m.Rodada, rodada), cancellationToken);
        if (gravou == 0)
        {
            return false;
        }

        var alcance = AlcanceDoMapa.MesaInteira(mesaId);
        if (encerrarAtual)
        {
            await movimento.ConcluirPendenteAsync(alcance, atual!.Id, cancellationToken);
        }

        if (novaRodada)
        {
            await db.Tokens
                .Where(t => t.MapaId == mapa!.Id && t.JogouNaRodada)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.JogouNaRodada, false), cancellationToken);
            await movimento.RestaurarAsync(alcance, db.Tokens.Where(t => t.MapaId == mapa!.Id), cancellationToken);
        }
        else if (encerrarAtual)
        {
            await db.Tokens
                .Where(t => t.Id == atual!.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.JogouNaRodada, true), cancellationToken);
        }

        return true;
    }

    private async Task<(IniciativaDto Mestre, IniciativaDto Jogadores)> MontarAsync(Guid mesaId, CancellationToken cancellationToken)
    {
        var mesa = await db.Mesas
            .AsNoTracking()
            .Where(m => m.Id == mesaId)
            .Select(m => new { m.Iniciativa, m.Rodada, m.VezDoTokenId })
            .FirstAsync(cancellationToken);
        var valores = await db.ValoresDeIniciativa
            .AsNoTracking()
            .Where(v => v.MesaId == mesaId)
            .OrderBy(v => v.InformadoEm)
            .Select(v => new ValorDeIniciativaDto(v.UsuarioId, v.Grupo, v.Valor))
            .ToListAsync(cancellationToken);
        var tokens = await db.Tokens
            .AsNoTracking()
            .Where(t => db.Mapas.Any(m => m.Id == t.MapaId && m.MesaId == mesaId && m.Ativo))
            .Select(t => new { t.Id, t.Nome, t.Visivel, t.DonoUsuarioId, t.JogouNaRodada })
            .ToListAsync(cancellationToken);

        var mestre = new IniciativaDto(
            mesa.Iniciativa,
            mesa.Rodada,
            mesa.VezDoTokenId,
            valores,
            tokens.Where(t => t.JogouNaRodada).Select(t => t.Id).ToList());

        // Os jogadores não ficam sabendo de grupos que só têm tokens ocultos, nem de jogadas deles.
        var visiveis = tokens.Where(t => t.Visivel).ToList();
        var gruposVisiveis = visiveis
            .Where(t => t.DonoUsuarioId is null)
            .Select(t => GrupoDeIniciativa.De(t.Nome).Chave)
            .ToHashSet(StringComparer.Ordinal);
        var jogadores = mestre with
        {
            VezDoTokenId = visiveis.Any(t => t.Id == mesa.VezDoTokenId) ? mesa.VezDoTokenId : null,
            Valores = valores.Where(v => v.Grupo is null || gruposVisiveis.Contains(v.Grupo)).ToList(),
            JogaramNaRodada = visiveis.Where(t => t.JogouNaRodada).Select(t => t.Id).ToList()
        };
        return (mestre, jogadores);
    }
}
