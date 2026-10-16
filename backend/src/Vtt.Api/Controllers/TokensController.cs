using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vtt.Api.Infrastructure;
using Vtt.Application.Mapas;

namespace Vtt.Api.Controllers;

/// <summary>Operações do Mestre sobre um token. O movimento passa pelo Hub (MoverToken).</summary>
[ApiController]
[Authorize]
[Route("api/tokens")]
public class TokensController(ITokenService tokens) : ControllerBase
{
    [HttpPut("{id:guid}")]
    [ProducesResponseType<TokenDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Editar(Guid id, EditarTokenRequest request, CancellationToken cancellationToken)
    {
        var resultado = await tokens.EditarAsync(User.ObterUsuarioId(), id, request, cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
    }

    /// <summary>Dano, cura ou edição direta do HP. O HP atual fica sempre entre 0 e o máximo.</summary>
    [HttpPut("{id:guid}/hp")]
    [ProducesResponseType<TokenDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DefinirHp(Guid id, HpRequest request, CancellationToken cancellationToken)
    {
        var resultado = await tokens.DefinirHpAsync(User.ObterUsuarioId(), id, request, cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
    }

    /// <summary>Oculta ou revela o token e define o que os jogadores veem do HP dele.</summary>
    [HttpPut("{id:guid}/exibicao")]
    [ProducesResponseType<TokenDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DefinirExibicao(Guid id, ExibicaoRequest request, CancellationToken cancellationToken)
    {
        var resultado = await tokens.DefinirExibicaoAsync(User.ObterUsuarioId(), id, request, cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
    }

    /// <summary>Cria uma cópia na célula livre mais próxima.</summary>
    [HttpPost("{id:guid}/duplicar")]
    [ProducesResponseType<TokenDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Duplicar(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await tokens.DuplicarAsync(User.ObterUsuarioId(), id, cancellationToken);
        return resultado.Sucesso
            ? StatusCode(StatusCodes.Status201Created, resultado.Valor)
            : this.Problema(resultado.Erro!);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remover(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await tokens.RemoverAsync(User.ObterUsuarioId(), id, cancellationToken);
        return resultado.Sucesso ? NoContent() : this.Problema(resultado.Erro!);
    }
}
