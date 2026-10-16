using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Vtt.Api.Infrastructure;
using Vtt.Application.Personagens;

namespace Vtt.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/personagens")]
public class PersonagensController(IPersonagemService personagens) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PersonagemDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken) =>
        Ok(await personagens.ListarAsync(User.ObterUsuarioId(), cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PersonagemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obter(Guid id, CancellationToken cancellationToken)
    {
        var personagem = await personagens.ObterAsync(User.ObterUsuarioId(), id, cancellationToken);
        return personagem is null ? NotFound() : Ok(personagem);
    }

    [HttpPost]
    [ProducesResponseType<PersonagemDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Criar(PersonagemRequest request, CancellationToken cancellationToken)
    {
        var personagem = await personagens.CriarAsync(User.ObterUsuarioId(), request, cancellationToken);
        return CreatedAtAction(nameof(Obter), new { id = personagem.Id }, personagem);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<PersonagemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(Guid id, PersonagemRequest request, CancellationToken cancellationToken)
    {
        var personagem = await personagens.AtualizarAsync(User.ObterUsuarioId(), id, request, cancellationToken);
        return personagem is null ? NotFound() : Ok(personagem);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken cancellationToken) =>
        await personagens.ExcluirAsync(User.ObterUsuarioId(), id, cancellationToken) ? NoContent() : NotFound();

    /// <summary>Envia a foto do personagem, substituindo a anterior.</summary>
    [HttpPut("{id:guid}/foto")]
    [EnableRateLimiting(RateLimitingOptions.PoliticaUpload)]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
    [ProducesResponseType<PersonagemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> DefinirFoto(Guid id, IFormFile? arquivo, CancellationToken cancellationToken)
    {
        await using var conteudo = arquivo?.OpenReadStream() ?? Stream.Null;
        var resultado = await personagens.DefinirFotoAsync(
            User.ObterUsuarioId(), id, arquivo?.FileName, conteudo, arquivo?.Length ?? 0, cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
    }

    [HttpDelete("{id:guid}/foto")]
    [ProducesResponseType<PersonagemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoverFoto(Guid id, CancellationToken cancellationToken)
    {
        var personagem = await personagens.RemoverFotoAsync(User.ObterUsuarioId(), id, cancellationToken);
        return personagem is null ? NotFound() : Ok(personagem);
    }
}
