using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vtt.Api.Infrastructure;
using Vtt.Application.Mapas;

namespace Vtt.Api.Controllers;

/// <summary>Operações do Mestre sobre uma estrutura. Criar fica em <see cref="MapasController"/> (/api/mapas/{id}/estruturas).</summary>
[ApiController]
[Authorize]
[Route("api/estruturas")]
public class EstruturasController(IEstruturaService estruturas) : ControllerBase
{
    [HttpPut("{id:guid}")]
    [ProducesResponseType<EstruturaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Editar(Guid id, EstruturaRequest request, CancellationToken cancellationToken)
    {
        var resultado = await estruturas.EditarAsync(User.ObterUsuarioId(), id, request, cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
    }

    /// <summary>Abre ou fecha uma porta.</summary>
    [HttpPut("{id:guid}/porta")]
    [ProducesResponseType<EstruturaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DefinirPorta(Guid id, PortaRequest request, CancellationToken cancellationToken)
    {
        var resultado = await estruturas.DefinirPortaAsync(User.ObterUsuarioId(), id, request.Aberta!.Value, cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remover(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await estruturas.RemoverAsync(User.ObterUsuarioId(), id, cancellationToken);
        return resultado.Sucesso ? NoContent() : this.Problema(resultado.Erro!);
    }
}
