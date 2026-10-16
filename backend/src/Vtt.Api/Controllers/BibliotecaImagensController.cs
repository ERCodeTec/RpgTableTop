using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Vtt.Api.Infrastructure;
using Vtt.Application.Imagens;

namespace Vtt.Api.Controllers;

/// <summary>
/// A galeria pessoal, usada pelos modelos da biblioteca de tokens. Baixar e excluir ficam em
/// <see cref="ImagensController"/> (/api/imagens/{id}), que valem para os dois tipos de imagem.
/// </summary>
[ApiController]
[Authorize]
[Route("api/biblioteca/imagens")]
public class BibliotecaImagensController(IImagemService imagens) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting(RateLimitingOptions.PoliticaUpload)]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
    [ProducesResponseType<ImagemDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Enviar(IFormFile? arquivo, CancellationToken cancellationToken)
    {
        await using var conteudo = arquivo?.OpenReadStream() ?? Stream.Null;
        var resultado = await imagens.EnviarPessoalAsync(
            User.ObterUsuarioId(), arquivo?.FileName, conteudo, arquivo?.Length ?? 0, cancellationToken);
        return resultado.Sucesso
            ? StatusCode(StatusCodes.Status201Created, resultado.Valor)
            : this.Problema(resultado.Erro!);
    }

    [HttpGet]
    [ProducesResponseType<GaleriaDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken) =>
        Ok(await imagens.ListarPessoaisAsync(User.ObterUsuarioId(), cancellationToken));
}
