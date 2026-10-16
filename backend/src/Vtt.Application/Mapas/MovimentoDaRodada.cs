using Microsoft.EntityFrameworkCore;
using Vtt.Application.Common;
using Vtt.Domain.Mapas;

namespace Vtt.Application.Mapas;

/// <summary>A virada da rodada no movimento por blocos: usada pela "Nova rodada" do Mestre e pela iniciativa.</summary>
public class MovimentoDaRodada(IAppDbContext db, IMapaNotifier notifier)
{
    /// <summary>
    /// Devolve o movimento inteiro, a Disparada e descarta as pendências, em um só UPDATE (não disputa a versão
    /// com um movimento em andamento: quem chegar depois recebe versão desatualizada). Avisa só os
    /// tokens que mudaram.
    /// </summary>
    public async Task RestaurarAsync(AlcanceDoMapa alcance, IQueryable<Token> tokens, CancellationToken cancellationToken)
    {
        var alterados = tokens.Where(t => t.MovimentoRestante != t.Deslocamento || t.Disparada || t.OrigemPendenteX != null);
        var ids = await alterados.Select(t => t.Id).ToListAsync(cancellationToken);
        if (ids.Count == 0)
        {
            return;
        }

        await db.Tokens
            .Where(t => ids.Contains(t.Id))
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.MovimentoRestante, t => t.Deslocamento)
                    .SetProperty(t => t.Disparada, false)
                    .SetProperty(t => t.OrigemPendenteX, (int?)null)
                    .SetProperty(t => t.OrigemPendenteY, (int?)null)
                    .SetProperty(t => t.CustoPendente, 0)
                    .SetProperty(t => t.Versao, t => t.Versao + 1),
                cancellationToken);

        await AvisarAsync(alcance, ids, cancellationToken);
    }

    /// <summary>Confirma o movimento pendente do token, se houver: o gasto fica e não dá mais para desfazer.</summary>
    public async Task ConcluirPendenteAsync(AlcanceDoMapa alcance, Guid tokenId, CancellationToken cancellationToken)
    {
        var alterados = await db.Tokens
            .Where(t => t.Id == tokenId && t.OrigemPendenteX != null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.OrigemPendenteX, (int?)null)
                    .SetProperty(t => t.OrigemPendenteY, (int?)null)
                    .SetProperty(t => t.CustoPendente, 0)
                    .SetProperty(t => t.Versao, t => t.Versao + 1),
                cancellationToken);
        if (alterados > 0)
        {
            await AvisarAsync(alcance, [tokenId], cancellationToken);
        }
    }

    private async Task AvisarAsync(AlcanceDoMapa alcance, List<Guid> ids, CancellationToken cancellationToken)
    {
        var atualizados = await db.Tokens.AsNoTracking().Where(t => ids.Contains(t.Id)).ToListAsync(cancellationToken);
        foreach (var token in atualizados)
        {
            await notifier.MovimentoAlteradoAsync(alcance, ProjecoesDoToken.De(token));
        }
    }
}
