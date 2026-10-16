using Microsoft.EntityFrameworkCore;
using Vtt.Application.Common;
using Vtt.Application.Mapas;
using Vtt.Application.Mesas;
using Vtt.Domain.Mapas;
using Vtt.Domain.Mesas;

namespace Vtt.Application.Iniciativas;

public class IniciativaService(IAppDbContext db, TurnoDaMesa turno, MovimentoDaRodada movimento, TimeProvider relogio) : IIniciativaService
{
    private const string SemPedido = "O Mestre ainda não pediu a iniciativa.";
    private const string JaJogou = "Já jogou nesta rodada: a iniciativa só pode mudar a partir da próxima.";

    public async Task<Resultado<IniciativaDto>> ObterAsync(Guid usuarioId, Guid mesaId, CancellationToken cancellationToken = default)
    {
        var papel = await db.ObterPapelAsync(usuarioId, mesaId, cancellationToken);
        return papel is null ? Erro.NaoEncontrado() : await turno.ObterAsync(mesaId, papel.Value, cancellationToken);
    }

    public async Task<Resultado<IniciativaDto>> SolicitarAsync(Guid usuarioId, Guid mesaId, CancellationToken cancellationToken = default)
    {
        var erro = await db.ExigirMestreAsync(usuarioId, mesaId, cancellationToken);
        if (erro is not null)
        {
            return erro;
        }

        await ReiniciarAsync(mesaId, EstadoDaIniciativa.Coletando, cancellationToken);
        await turno.AvisarAsync(mesaId, cancellationToken);
        return await turno.ObterAsync(mesaId, PapelNaMesa.Mestre, cancellationToken);
    }

    public async Task<Resultado<IniciativaDto>> InformarMinhaAsync(Guid usuarioId, Guid mesaId, int valor, CancellationToken cancellationToken = default)
    {
        var papel = await db.ObterPapelAsync(usuarioId, mesaId, cancellationToken);
        if (papel is null)
        {
            return Erro.NaoEncontrado();
        }

        if (papel == PapelNaMesa.Mestre)
        {
            return Erro.Conflito("O Mestre define a iniciativa dos jogadores e dos grupos pelo painel.");
        }

        if (await EstadoAsync(mesaId, cancellationToken) == EstadoDaIniciativa.Inativa)
        {
            return Erro.Conflito(SemPedido);
        }

        const string JaInformou = "Você já informou a sua iniciativa.";
        if (await db.ValoresDeIniciativa.AnyAsync(v => v.MesaId == mesaId && v.UsuarioId == usuarioId, cancellationToken))
        {
            return Erro.Conflito(JaInformou);
        }

        var novo = new ValorDeIniciativa
        {
            Id = Guid.NewGuid(),
            MesaId = mesaId,
            UsuarioId = usuarioId,
            Valor = valor,
            InformadoEm = relogio.GetUtcNow()
        };
        db.ValoresDeIniciativa.Add(novo);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Índice único: um segundo envio chegou ao mesmo tempo.
            db.ValoresDeIniciativa.Entry(novo).State = EntityState.Detached;
            return Erro.Conflito(JaInformou);
        }

        await turno.ReavaliarAsync(mesaId, sempreAvisar: true, cancellationToken);
        return await turno.ObterAsync(mesaId, PapelNaMesa.Jogador, cancellationToken);
    }

    public async Task<Resultado<IniciativaDto>> DefinirDoJogadorAsync(
        Guid usuarioId, Guid mesaId, Guid jogadorId, int valor, CancellationToken cancellationToken = default)
    {
        var erro = await ExigirMestreComPedidoAsync(usuarioId, mesaId, cancellationToken);
        if (erro is not null)
        {
            return erro;
        }

        if (await db.ObterPapelAsync(jogadorId, mesaId, cancellationToken) != PapelNaMesa.Jogador)
        {
            return Erro.NaoEncontrado("Este jogador não está na mesa.");
        }

        var (vez, tokens) = await CarregarVezAsync(mesaId, cancellationToken);
        if (tokens.Any(token => token.DonoUsuarioId == jogadorId && (token.JogouNaRodada || token.Id == vez)))
        {
            return Erro.Conflito(JaJogou);
        }

        return await GravarAsync(mesaId, v => v.UsuarioId == jogadorId, () => new ValorDeIniciativa { UsuarioId = jogadorId }, valor, cancellationToken);
    }

    public async Task<Resultado<IniciativaDto>> DefinirDoGrupoAsync(
        Guid usuarioId, Guid mesaId, string grupo, int valor, CancellationToken cancellationToken = default)
    {
        var erro = await ExigirMestreComPedidoAsync(usuarioId, mesaId, cancellationToken);
        if (erro is not null)
        {
            return erro;
        }

        var chave = GrupoDeIniciativa.De(grupo).Chave;
        var (vez, tokens) = await CarregarVezAsync(mesaId, cancellationToken);
        if (tokens.Any(token => token.DonoUsuarioId is null
                && GrupoDeIniciativa.De(token.Nome).Chave == chave
                && (token.JogouNaRodada || token.Id == vez)))
        {
            return Erro.Conflito(JaJogou);
        }

        return await GravarAsync(mesaId, v => v.Grupo == chave, () => new ValorDeIniciativa { Grupo = chave }, valor, cancellationToken);
    }

    public async Task<Resultado<IniciativaDto>> IniciarAsync(Guid usuarioId, Guid mesaId, CancellationToken cancellationToken = default)
    {
        var erro = await db.ExigirMestreAsync(usuarioId, mesaId, cancellationToken);
        if (erro is not null)
        {
            return erro;
        }

        var estado = await EstadoAsync(mesaId, cancellationToken);
        if (estado == EstadoDaIniciativa.Inativa)
        {
            return Erro.Conflito("Peça a iniciativa antes de iniciar o combate.");
        }

        if (estado == EstadoDaIniciativa.EmCombate)
        {
            return await turno.ObterAsync(mesaId, PapelNaMesa.Mestre, cancellationToken);
        }

        var (mapa, _, ordem) = await turno.CarregarOrdemAsync(mesaId, cancellationToken);
        if (mapa is null)
        {
            return Erro.Conflito("Ative um mapa antes de iniciar o combate.");
        }

        if (!ordem.Any(token => !token.Caido))
        {
            return Erro.Conflito("Ninguém no mapa ativo tem iniciativa ainda.");
        }

        // A primeira rodada começa com o movimento inteiro para todos.
        await db.Mesas
            .Where(m => m.Id == mesaId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Iniciativa, EstadoDaIniciativa.EmCombate), cancellationToken);
        await movimento.RestaurarAsync(AlcanceDoMapa.MesaInteira(mesaId), db.Tokens.Where(t => t.MapaId == mapa.Id), cancellationToken);
        await turno.ReiniciarNoMapaAtivoAsync(mesaId, cancellationToken);
        return await turno.ObterAsync(mesaId, PapelNaMesa.Mestre, cancellationToken);
    }

    public async Task<Resultado<IniciativaDto>> EncerrarVezAsync(Guid usuarioId, Guid mesaId, Guid tokenId, CancellationToken cancellationToken = default)
    {
        var papel = await db.ObterPapelAsync(usuarioId, mesaId, cancellationToken);
        if (papel is null)
        {
            return Erro.NaoEncontrado();
        }

        var mesa = await db.Mesas
            .AsNoTracking()
            .Where(m => m.Id == mesaId)
            .Select(m => new { m.Iniciativa, m.VezDoTokenId })
            .FirstAsync(cancellationToken);
        if (mesa.Iniciativa != EstadoDaIniciativa.EmCombate)
        {
            return Erro.Conflito("Não há combate em andamento.");
        }

        if (mesa.VezDoTokenId != tokenId)
        {
            return Erro.Conflito("Não é a vez deste token.");
        }

        if (papel == PapelNaMesa.Jogador
            && !await db.Tokens.AnyAsync(t => t.Id == tokenId && t.DonoUsuarioId == usuarioId, cancellationToken))
        {
            return Erro.Proibido("Só o dono do token ou o Mestre podem encerrar a vez.");
        }

        // Se outra ação já passou a vez, a resposta é o estado atual, como se este pedido tivesse vencido.
        await turno.EncerrarVezAsync(mesaId, cancellationToken);
        return await turno.ObterAsync(mesaId, papel.Value, cancellationToken);
    }

    public async Task<Resultado<IniciativaDto>> EncerrarCombateAsync(Guid usuarioId, Guid mesaId, CancellationToken cancellationToken = default)
    {
        var erro = await db.ExigirMestreAsync(usuarioId, mesaId, cancellationToken);
        if (erro is not null)
        {
            return erro;
        }

        await ReiniciarAsync(mesaId, EstadoDaIniciativa.Inativa, cancellationToken);
        await turno.AvisarAsync(mesaId, cancellationToken);
        return await turno.ObterAsync(mesaId, PapelNaMesa.Mestre, cancellationToken);
    }

    /// <summary>Apaga os valores e as jogadas e deixa a mesa no estado dado, sem vez nem rodada.</summary>
    private async Task ReiniciarAsync(Guid mesaId, EstadoDaIniciativa estado, CancellationToken cancellationToken)
    {
        await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.ValoresDeIniciativa.Where(v => v.MesaId == mesaId).ExecuteDeleteAsync(cancellationToken);
        await db.Mesas
            .Where(m => m.Id == mesaId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(m => m.Iniciativa, estado)
                    .SetProperty(m => m.Rodada, 0)
                    .SetProperty(m => m.VezDoTokenId, (Guid?)null),
                cancellationToken);
        await turno.ZerarJogadasAsync(mesaId, cancellationToken);
        await transacao.CommitAsync(cancellationToken);
    }

    private async Task<Resultado<IniciativaDto>> GravarAsync(
        Guid mesaId,
        System.Linq.Expressions.Expression<Func<ValorDeIniciativa, bool>> doDono,
        Func<ValorDeIniciativa> novo,
        int valor,
        CancellationToken cancellationToken)
    {
        var existente = await db.ValoresDeIniciativa.Where(v => v.MesaId == mesaId).FirstOrDefaultAsync(doDono, cancellationToken);
        if (existente is null)
        {
            existente = novo();
            existente.Id = Guid.NewGuid();
            existente.MesaId = mesaId;
            existente.InformadoEm = relogio.GetUtcNow();
            db.ValoresDeIniciativa.Add(existente);
        }

        existente.Valor = valor;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ValoresDeIniciativa.Entry(existente).State = EntityState.Detached;
            return Erro.Conflito("A iniciativa foi alterada ao mesmo tempo por outra ação. Tente de novo.");
        }

        await turno.ReavaliarAsync(mesaId, sempreAvisar: true, cancellationToken);
        return await turno.ObterAsync(mesaId, PapelNaMesa.Mestre, cancellationToken);
    }

    private async Task<Erro?> ExigirMestreComPedidoAsync(Guid usuarioId, Guid mesaId, CancellationToken cancellationToken)
    {
        var erro = await db.ExigirMestreAsync(usuarioId, mesaId, cancellationToken);
        if (erro is not null)
        {
            return erro;
        }

        return await EstadoAsync(mesaId, cancellationToken) == EstadoDaIniciativa.Inativa ? Erro.Conflito(SemPedido) : null;
    }

    /// <summary>A vez atual e os tokens do mapa ativo; fora do combate ninguém jogou, e nada fica travado.</summary>
    private async Task<(Guid? Vez, IReadOnlyList<Token> Tokens)> CarregarVezAsync(Guid mesaId, CancellationToken cancellationToken)
    {
        var mesa = await db.Mesas
            .AsNoTracking()
            .Where(m => m.Id == mesaId)
            .Select(m => new { m.Iniciativa, m.VezDoTokenId })
            .FirstAsync(cancellationToken);
        if (mesa.Iniciativa != EstadoDaIniciativa.EmCombate)
        {
            return (null, []);
        }

        var (_, tokens, _) = await turno.CarregarOrdemAsync(mesaId, cancellationToken);
        return (mesa.VezDoTokenId, tokens);
    }

    private Task<EstadoDaIniciativa> EstadoAsync(Guid mesaId, CancellationToken cancellationToken) =>
        db.Mesas.Where(m => m.Id == mesaId).Select(m => m.Iniciativa).FirstAsync(cancellationToken);
}
