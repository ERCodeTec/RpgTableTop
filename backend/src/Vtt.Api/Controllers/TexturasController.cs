using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vtt.Application.Mapas;

namespace Vtt.Api.Controllers;

/// <summary>O catálogo das texturas prontas de estrutura. Os PNGs são servidos pelo frontend, em /texturas.</summary>
[ApiController]
[Authorize]
[Route("api/texturas-prontas")]
public class TexturasController : ControllerBase
{
    /// <summary>Temas e texturas, com os tipos de estrutura e os papéis no gerador de cada uma.</summary>
    [HttpGet]
    [ProducesResponseType<CatalogoDeTexturasDto>(StatusCodes.Status200OK)]
    public IActionResult Listar()
    {
        // Só muda com uma versão nova da aplicação.
        Response.Headers.CacheControl = "private, max-age=3600";
        return Ok(CatalogoDeTexturasDto.Atual);
    }
}
