using Microsoft.EntityFrameworkCore;
using Vtt.Application.Common;
using Vtt.Infrastructure.Persistence;

namespace Vtt.Infrastructure.Identity;

public class UsuarioDirectory(AppDbContext db) : IUsuarioDirectory
{
    public async Task<IReadOnlyDictionary<Guid, string>> ObterNomesDeExibicaoAsync(
        IReadOnlyCollection<Guid> usuarioIds, CancellationToken cancellationToken = default)
    {
        return await db.Users
            .AsNoTracking()
            .Where(usuario => usuarioIds.Contains(usuario.Id))
            .ToDictionaryAsync(usuario => usuario.Id, usuario => usuario.NomeExibicao, cancellationToken);
    }
}
