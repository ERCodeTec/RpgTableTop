using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Vtt.Application.Auth;
using Vtt.Infrastructure.Identity;

namespace Vtt.Infrastructure.Auth;

public class AuthService(
    UserManager<Usuario> userManager,
    JwtTokenService tokens,
    TimeProvider relogio,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<ResultadoAuth> RegistrarAsync(RegistrarRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email!.Trim();
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            NomeExibicao = request.NomeExibicao!.Trim(),
            CriadoEm = relogio.GetUtcNow()
        };

        var resultado = await userManager.CreateAsync(usuario, request.Senha!);
        if (!resultado.Succeeded)
        {
            return ResultadoAuth.Falha(AgruparPorCampo(resultado.Errors));
        }

        logger.LogInformation("Usuário {UsuarioId} cadastrado", usuario.Id);
        return ResultadoAuth.Ok(CriarResposta(usuario));
    }

    public async Task<ResultadoAuth> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var usuario = await userManager.FindByEmailAsync(request.Email!.Trim());
        if (usuario is null)
        {
            return ResultadoAuth.CredenciaisInvalidas();
        }

        if (await userManager.IsLockedOutAsync(usuario))
        {
            logger.LogWarning("Login recusado: usuário {UsuarioId} bloqueado temporariamente", usuario.Id);
            return ResultadoAuth.CredenciaisInvalidas();
        }

        if (!await userManager.CheckPasswordAsync(usuario, request.Senha!))
        {
            await userManager.AccessFailedAsync(usuario);
            logger.LogWarning("Senha incorreta para o usuário {UsuarioId}", usuario.Id);
            return ResultadoAuth.CredenciaisInvalidas();
        }

        await userManager.ResetAccessFailedCountAsync(usuario);
        return ResultadoAuth.Ok(CriarResposta(usuario));
    }

    public async Task<AuthResponse?> RenovarAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var usuario = await userManager.FindByIdAsync(usuarioId.ToString());
        return usuario is null ? null : CriarResposta(usuario);
    }

    public async Task<UsuarioDto?> ObterUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var usuario = await userManager.FindByIdAsync(usuarioId.ToString());
        return usuario is null ? null : ParaDto(usuario);
    }

    private AuthResponse CriarResposta(Usuario usuario)
    {
        var (token, expiraEm) = tokens.Gerar(usuario);
        return new AuthResponse(token, expiraEm, ParaDto(usuario));
    }

    private static UsuarioDto ParaDto(Usuario usuario) =>
        new(usuario.Id, usuario.Email ?? string.Empty, usuario.NomeExibicao);

    private static Dictionary<string, string[]> AgruparPorCampo(IEnumerable<IdentityError> erros) =>
        erros
            .GroupBy(erro => erro.Code.StartsWith("Password", StringComparison.Ordinal) ? "senha" : "email")
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Select(erro => erro.Description).Distinct().ToArray());
}
