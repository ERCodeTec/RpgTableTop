using System.ComponentModel.DataAnnotations;

namespace Vtt.Infrastructure.Auth;

public class JwtOptions
{
    public const string Secao = "Jwt";

    [Required(ErrorMessage = "Defina Jwt:Secret (variável JWT_SECRET).")]
    [MinLength(32, ErrorMessage = "Jwt:Secret (JWT_SECRET) deve ter pelo menos 32 caracteres.")]
    public string Secret { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = "vtt";

    [Range(1, 60 * 24 * 30)]
    public int ExpiracaoMinutos { get; set; } = 720;
}
