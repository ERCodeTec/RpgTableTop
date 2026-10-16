using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vtt.Api.Infrastructure;
using Vtt.Application.Biblioteca;

namespace Vtt.Api.Controllers;

/// <summary>Categorias e modelos da biblioteca de tokens do usuário. As imagens ficam em <see cref="BibliotecaImagensController"/>.</summary>
[ApiController]
[Authorize]
[Route("api/biblioteca")]
public class BibliotecaController(IBibliotecaService biblioteca) : ControllerBase
{
    [HttpGet("categorias")]
    [ProducesResponseType<IReadOnlyList<CategoriaDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarCategorias(CancellationToken cancellationToken) =>
        Ok(await biblioteca.ListarCategoriasAsync(User.ObterUsuarioId(), cancellationToken));

    [HttpPost("categorias")]
    [ProducesResponseType<CategoriaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CriarCategoria(CategoriaRequest request, CancellationToken cancellationToken)
    {
        var resultado = await biblioteca.CriarCategoriaAsync(User.ObterUsuarioId(), request, cancellationToken);
        return resultado.Sucesso ? StatusCode(StatusCodes.Status201Created, resultado.Valor) : this.Problema(resultado.Erro!);
    }

    /// <summary>Cria as que faltarem entre NPCs, Vilões e Inimigos e devolve todas as categorias.</summary>
    [HttpPost("categorias/sugeridas")]
    [ProducesResponseType<IReadOnlyList<CategoriaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CriarSugeridas(CancellationToken cancellationToken)
    {
        var resultado = await biblioteca.CriarCategoriasSugeridasAsync(User.ObterUsuarioId(), cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
    }

    [HttpPut("categorias/{id:guid}")]
    [ProducesResponseType<CategoriaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RenomearCategoria(Guid id, CategoriaRequest request, CancellationToken cancellationToken)
    {
        var resultado = await biblioteca.RenomearCategoriaAsync(User.ObterUsuarioId(), id, request, cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
    }

    [HttpDelete("categorias/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExcluirCategoria(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await biblioteca.ExcluirCategoriaAsync(User.ObterUsuarioId(), id, cancellationToken);
        return resultado.Sucesso ? NoContent() : this.Problema(resultado.Erro!);
    }

    [HttpGet("modelos")]
    [ProducesResponseType<IReadOnlyList<ModeloDeTokenDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarModelos(CancellationToken cancellationToken) =>
        Ok(await biblioteca.ListarModelosAsync(User.ObterUsuarioId(), cancellationToken));

    [HttpGet("modelos/{id:guid}")]
    [ProducesResponseType<ModeloDeTokenDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterModelo(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await biblioteca.ObterModeloAsync(User.ObterUsuarioId(), id, cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
    }

    [HttpPost("modelos")]
    [ProducesResponseType<ModeloDeTokenDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CriarModelo(ModeloDeTokenRequest request, CancellationToken cancellationToken)
    {
        var resultado = await biblioteca.CriarModeloAsync(User.ObterUsuarioId(), request, cancellationToken);
        return resultado.Sucesso
            ? CreatedAtAction(nameof(ObterModelo), new { id = resultado.Valor!.Id }, resultado.Valor)
            : this.Problema(resultado.Erro!);
    }

    [HttpPut("modelos/{id:guid}")]
    [ProducesResponseType<ModeloDeTokenDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AtualizarModelo(Guid id, ModeloDeTokenRequest request, CancellationToken cancellationToken)
    {
        var resultado = await biblioteca.AtualizarModeloAsync(User.ObterUsuarioId(), id, request, cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
    }

    [HttpDelete("modelos/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExcluirModelo(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await biblioteca.ExcluirModeloAsync(User.ObterUsuarioId(), id, cancellationToken);
        return resultado.Sucesso ? NoContent() : this.Problema(resultado.Erro!);
    }
}
