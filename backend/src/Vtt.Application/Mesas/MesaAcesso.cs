using Microsoft.EntityFrameworkCore;
using Vtt.Application.Common;
using Vtt.Domain.Mesas;

namespace Vtt.Application.Mesas;

internal static class MesaAcesso
{
    /// <summary>
    /// null quando o usuário é Mestre da mesa. Caso contrário, o erro a devolver:
    /// NaoEncontrado para quem não é membro, Proibido para o jogador.
    /// </summary>
    public static async Task<Erro?> ExigirMestreAsync(
        this IAppDbContext db, Guid usuarioId, Guid mesaId, CancellationToken cancellationToken)
    {
        var papel = await db.MesaMembros
            .Where(membro => membro.MesaId == mesaId && membro.UsuarioId == usuarioId)
            .Select(membro => (PapelNaMesa?)membro.Papel)
            .FirstOrDefaultAsync(cancellationToken);

        return papel switch
        {
            null => Erro.NaoEncontrado(),
            PapelNaMesa.Mestre => null,
            _ => Erro.Proibido(SomenteMestre)
        };
    }

    public const string SomenteMestre = "Só o Mestre da mesa pode fazer isso.";
}
