using Microsoft.EntityFrameworkCore;
using Vtt.Application.Common;
using Vtt.Domain.Mapas;
using Vtt.Domain.Mesas;

namespace Vtt.Application.Mapas;

internal static class MapaAcesso
{
    /// <summary>O papel do usuário na mesa, ou null se ele não for membro.</summary>
    public static Task<PapelNaMesa?> ObterPapelAsync(
        this IAppDbContext db, Guid usuarioId, Guid mesaId, CancellationToken cancellationToken) =>
        db.MesaMembros
            .Where(membro => membro.MesaId == mesaId && membro.UsuarioId == usuarioId)
            .Select(membro => (PapelNaMesa?)membro.Papel)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>true se a imagem existe e é da mesa. Imagens de outra mesa se comportam como inexistentes.</summary>
    public static Task<bool> ImagemEhDaMesaAsync(
        this IAppDbContext db, Guid imagemId, Guid mesaId, CancellationToken cancellationToken) =>
        db.Imagens.AnyAsync(imagem => imagem.Id == imagemId && imagem.MesaId == mesaId, cancellationToken);

    /// <summary>true se a célula está na área de uma estrutura que bloqueia (porta fechada conta, aberta não).</summary>
    public static Task<bool> CelulaBloqueadaAsync(
        this IAppDbContext db, Guid mapaId, int x, int y, CancellationToken cancellationToken) =>
        db.Estruturas.AnyAsync(
            e => e.MapaId == mapaId
                && x >= e.X && x < e.X + e.Largura
                && y >= e.Y && y < e.Y + e.Profundidade
                && (e.Tipo == TipoEstrutura.Porta ? !e.PortaAberta : e.BloqueiaMovimento),
            cancellationToken);

    /// <summary>
    /// As células onde um token não deve nascer sozinho: as que já têm token e as cobertas por
    /// estruturas que bloqueiam. Usado no ponto de spawn, ao duplicar e ao revelar.
    /// </summary>
    /// <param name="somenteVisiveis">
    /// true quando o resultado aparece para os jogadores (spawn, revelar): os tokens ocultos não
    /// contam, para a posição escolhida não denunciar onde eles estão.
    /// </param>
    public static async Task<HashSet<(int X, int Y)>> CelulasIndisponiveisAsync(
        this IAppDbContext db, Guid mapaId, bool somenteVisiveis, CancellationToken cancellationToken)
    {
        var celulas = await db.CelulasOcupadasAsync(mapaId, somenteVisiveis, cancellationToken);
        celulas.UnionWith(await db.CelulasBloqueadasAsync(mapaId, cancellationToken));
        return celulas;
    }

    /// <summary>As células cobertas por estruturas que bloqueiam (porta fechada conta, aberta não).</summary>
    public static async Task<HashSet<(int X, int Y)>> CelulasBloqueadasAsync(
        this IAppDbContext db, Guid mapaId, CancellationToken cancellationToken)
    {
        var estruturas = await db.Estruturas
            .AsNoTracking()
            .Where(estrutura => estrutura.MapaId == mapaId)
            .ToListAsync(cancellationToken);

        var celulas = new HashSet<(int X, int Y)>();
        foreach (var estrutura in estruturas.Where(estrutura => estrutura.Bloqueia))
        {
            for (var y = estrutura.Y; y < estrutura.Y + estrutura.Profundidade; y++)
            {
                for (var x = estrutura.X; x < estrutura.X + estrutura.Largura; x++)
                {
                    celulas.Add((x, y));
                }
            }
        }

        return celulas;
    }

    public static async Task<HashSet<(int X, int Y)>> CelulasOcupadasAsync(
        this IAppDbContext db, Guid mapaId, bool somenteVisiveis, CancellationToken cancellationToken)
    {
        var posicoes = await db.Tokens
            .Where(token => token.MapaId == mapaId && (!somenteVisiveis || token.Visivel))
            .Select(token => new { token.X, token.Y })
            .ToListAsync(cancellationToken);
        return posicoes.Select(posicao => (posicao.X, posicao.Y)).ToHashSet();
    }
}
