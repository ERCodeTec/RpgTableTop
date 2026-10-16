namespace Vtt.Application.Auth;

public record RegistrarRequest(string? Email, string? Senha, string? NomeExibicao);

public record LoginRequest(string? Email, string? Senha);

public record UsuarioDto(Guid Id, string Email, string NomeExibicao);

public record AuthResponse(string Token, DateTimeOffset ExpiraEm, UsuarioDto Usuario);
