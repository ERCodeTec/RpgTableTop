using Microsoft.AspNetCore.Identity;

namespace Vtt.Infrastructure.Identity;

public class Usuario : IdentityUser<Guid>
{
    public string NomeExibicao { get; set; } = string.Empty;
    public DateTimeOffset CriadoEm { get; set; }
}
