using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Vtt.Api.Infrastructure;
using Vtt.Application.Auth;

namespace Vtt.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("register")]
    [EnableRateLimiting(RateLimitingOptions.PoliticaAuth)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Registrar(RegistrarRequest request, CancellationToken cancellationToken)
    {
        var resultado = await auth.RegistrarAsync(request, cancellationToken);
        if (resultado.Sucesso)
        {
            return Ok(resultado.Resposta);
        }

        foreach (var (campo, mensagens) in resultado.Erros)
        {
            foreach (var mensagem in mensagens)
            {
                ModelState.AddModelError(campo, mensagem);
            }
        }

        return ValidationProblem(ModelState);
    }

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitingOptions.PoliticaAuth)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var resultado = await auth.LoginAsync(request, cancellationToken);
        return resultado.Sucesso
            ? Ok(resultado.Resposta)
            : Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "E-mail ou senha inválidos.");
    }

    /// <summary>
    /// Troca um token ainda válido por um novo, para a sessão não cair no meio de um jogo.
    /// Um token expirado não renova: a pessoa entra de novo.
    /// </summary>
    [HttpPost("renovar")]
    [Authorize]
    [EnableRateLimiting(RateLimitingOptions.PoliticaAuth)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Renovar(CancellationToken cancellationToken)
    {
        var resposta = await auth.RenovarAsync(User.ObterUsuarioId(), cancellationToken);
        return resposta is null ? Unauthorized() : Ok(resposta);
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UsuarioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var usuario = await auth.ObterUsuarioAsync(User.ObterUsuarioId(), cancellationToken);
        return usuario is null ? Unauthorized() : Ok(usuario);
    }
}
