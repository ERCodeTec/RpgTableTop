using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Vtt.Api.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    public static Guid ObterUsuarioId(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(sub, out var id)
            ? id
            : throw new InvalidOperationException("Token sem identificador de usuário.");
    }
}
