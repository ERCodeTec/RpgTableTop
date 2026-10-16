using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vtt.Api.Infrastructure;
using Vtt.Application.Mesas;

namespace Vtt.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/convites/{codigo}")]
public class ConvitesController(IConviteService convites) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ConvitePreviaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PreVisualizar(string codigo, CancellationToken cancellationToken)
    {
        var resultado = await convites.PreVisualizarAsync(User.ObterUsuarioId(), codigo, cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
    }

    [HttpPost("aceitar")]
    [ProducesResponseType<AceitarConviteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Aceitar(string codigo, AceitarConviteRequest request, CancellationToken cancellationToken)
    {
        var resultado = await convites.AceitarAsync(User.ObterUsuarioId(), codigo, request.PersonagemId, cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revogar(string codigo, CancellationToken cancellationToken)
    {
        var resultado = await convites.RevogarAsync(User.ObterUsuarioId(), codigo, cancellationToken);
        return resultado.Sucesso ? NoContent() : this.Problema(resultado.Erro!);
    }
}
