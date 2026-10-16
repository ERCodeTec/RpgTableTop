using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Vtt.Api.Infrastructure;
using Vtt.Application.Imagens;

namespace Vtt.Api.Controllers;

/// <summary>Baixar e excluir imagens. Enviar e listar ficam em <see cref="MesasController"/> (/api/mesas/{id}/imagens).</summary>
[ApiController]
[Authorize]
[Route("api/imagens")]
public class ImagensController(IImagemService imagens) : ControllerBase
{
    /// <summary>O arquivo da imagem, para qualquer membro da mesa.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Baixar(Guid id, CancellationToken cancellationToken) => DevolverAsync(id, miniatura: false, cancellationToken);

    /// <summary>A miniatura de 256 px usada na galeria.</summary>
    [HttpGet("{id:guid}/miniatura")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IActionResult> BaixarMiniatura(Guid id, CancellationToken cancellationToken) => DevolverAsync(id, miniatura: true, cancellationToken);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await imagens.ExcluirAsync(User.ObterUsuarioId(), id, cancellationToken);
        return resultado.Sucesso ? NoContent() : this.Problema(resultado.Erro!);
    }

    private async Task<IActionResult> DevolverAsync(Guid id, bool miniatura, CancellationToken cancellationToken)
    {
        var resultado = await imagens.AbrirAsync(User.ObterUsuarioId(), id, miniatura, cancellationToken);
        if (!resultado.Sucesso)
        {
            return this.Problema(resultado.Erro!);
        }

        var arquivo = resultado.Valor!;

        // O conteúdo de um id nunca muda, então o navegador pode guardá-lo; "private" porque só membros o veem.
        Response.Headers.CacheControl = "private, max-age=31536000, immutable";
        Response.Headers.XContentTypeOptions = "nosniff";

        // Com o ETag, o File responde 304 sozinho quando o navegador já tem a imagem.
        return File(arquivo.Conteudo, arquivo.ContentType, lastModified: null, entityTag: EntityTagHeaderValue.Parse(arquivo.ETag));
    }
}
