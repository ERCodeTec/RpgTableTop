namespace Vtt.Application.Auth;

public interface IAuthService
{
    Task<ResultadoAuth> RegistrarAsync(RegistrarRequest request, CancellationToken cancellationToken = default);

    Task<ResultadoAuth> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>Um token novo para quem já tem um válido; null se o usuário não existe mais.</summary>
    Task<AuthResponse?> RenovarAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    Task<UsuarioDto?> ObterUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}
