using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vtt.Api.Infrastructure;
using Vtt.Application.Mapas;

namespace Vtt.Api.Controllers;

/// <summary>Pastas da aba Mapas, só do Mestre: listar e criar na mesa, renomear e excluir, e mover um mapa.</summary>
[ApiController]
[Authorize]
[Route("api")]
public class PastasDeMapasController(IPastaDeMapasService pastas) : ControllerBase
{
    [HttpGet("mesas/{mesaId:guid}/pastas-de-mapas")]
    [ProducesResponseType<IReadOnlyList<PastaDeMapasDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Listar(Guid mesaId, CancellationToken cancellationToken)
    {
        var resultado = await pastas.ListarAsync(User.ObterUsuarioId(), mesaId, cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
    }

    [HttpPost("mesas/{mesaId:guid}/pastas-de-mapas")]
    [ProducesResponseType<PastaDeMapasDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Criar(Guid mesaId, PastaDeMapasRequest request, CancellationToken cancellationToken)
    {
        var resultado = await pastas.CriarAsync(User.ObterUsuarioId(), mesaId, request, cancellationToken);
        return resultado.Sucesso ? StatusCode(StatusCodes.Status201Created, resultado.Valor) : this.Problema(resultado.Erro!);
    }

    [HttpPut("pastas-de-mapas/{id:guid}")]
    [ProducesResponseType<PastaDeMapasDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Renomear(Guid id, PastaDeMapasRequest request, CancellationToken cancellationToken)
    {
        var resultado = await pastas.RenomearAsync(User.ObterUsuarioId(), id, request, cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
    }

    /// <summary>Os mapas da pasta ficam em "Sem pasta".</summary>
    [HttpDelete("pastas-de-mapas/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await pastas.ExcluirAsync(User.ObterUsuarioId(), id, cancellationToken);
        return resultado.Sucesso ? NoContent() : this.Problema(resultado.Erro!);
    }

    /// <summary>Põe o mapa em uma pasta da mesma mesa; pastaId nulo tira de qualquer pasta.</summary>
    [HttpPut("mapas/{mapaId:guid}/pasta")]
    [ProducesResponseType<MapaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DefinirPastaDoMapa(Guid mapaId, PastaDoMapaRequest request, CancellationToken cancellationToken)
    {
        var resultado = await pastas.DefinirPastaDoMapaAsync(User.ObterUsuarioId(), mapaId, request, cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
    }
}
