using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Vtt.Infrastructure.Identity;

namespace Vtt.Infrastructure.Auth;

public class JwtTokenService(IOptions<JwtOptions> options, TimeProvider relogio)
{
    private readonly JwtOptions _options = options.Value;

    public (string Token, DateTimeOffset ExpiraEm) Gerar(Usuario usuario)
    {
        var agora = relogio.GetUtcNow();
        var expiraEm = agora.AddMinutes(_options.ExpiracaoMinutos);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Issuer,
            IssuedAt = agora.UtcDateTime,
            NotBefore = agora.UtcDateTime,
            Expires = expiraEm.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, usuario.Email ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Name, usuario.NomeExibicao),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ]),
            SigningCredentials = new SigningCredentials(CriarChave(_options.Secret), SecurityAlgorithms.HmacSha256)
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expiraEm);
    }

    public static SymmetricSecurityKey CriarChave(string secret) => new(Encoding.UTF8.GetBytes(secret));
}
