using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Vtt.Api.Infrastructure;
using Vtt.Application.Imagens;
using Vtt.Application.Mapas;
using Vtt.Application.Mesas;

namespace Vtt.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/mesas")]
public class MesasController(IMesaService mesas, IConviteService convites, IMapaService mapas, IImagemService imagens) : ControllerBase
{
    /// <summary>
    /// Teto do corpo da requisição de upload, acima do qual o servidor nem lê o arquivo (413).
    /// O limite por arquivo, configurável e menor, é conferido pelo serviço.
    /// </summary>
    internal const long LimiteDoCorpoDoUpload = 32 * 1024 * 1024;

    [HttpPost]
    [ProducesResponseType<MesaDetalheDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Criar(MesaRequest request, CancellationToken cancellationToken)
    {
        var mesa = await mesas.CriarAsync(User.ObterUsuarioId(), request, cancellationToken);
        return CreatedAtAction(nameof(Obter), new { id = mesa.Id }, mesa);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MesaResumoDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken) =>
        Ok(await mesas.ListarAsync(User.ObterUsuarioId(), cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<MesaDetalheDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obter(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await mesas.ObterAsync(User.ObterUsuarioId(), id, cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<MesaDetalheDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(Guid id, MesaRequest request, CancellationToken cancellationToken)
    {
        var resultado = await mesas.AtualizarAsync(User.ObterUsuarioId(), id, request, cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await mesas.ExcluirAsync(User.ObterUsuarioId(), id, cancellationToken);
        return resultado.Sucesso ? NoContent() : this.Problema(resultado.Erro!);
    }

    [HttpPut("{id:guid}/sessao")]
    [ProducesResponseType<MesaDetalheDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DefinirSessao(Guid id, SessaoRequest request, CancellationToken cancellationToken)
    {
        var resultado = await mesas.DefinirSessaoAsync(User.ObterUsuarioId(), id, request.Ativa!.Value, cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
    }

    [HttpPut("{id:guid}/movimentacao")]
    [ProducesResponseType<MesaDetalheDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DefinirMovimentacao(Guid id, MovimentacaoRequest request, CancellationToken cancellationToken)
    {
        var resultado = await mesas.DefinirMovimentacaoAsync(User.ObterUsuarioId(), id, request.Modo!.Value, cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
    }

    [HttpPut("{id:guid}/meu-personagem")]
    [ProducesResponseType<MembroDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DefinirMeuPersonagem(Guid id, DefinirPersonagemRequest request, CancellationToken cancellationToken)
    {
        var resultado = await mesas.DefinirMeuPersonagemAsync(User.ObterUsuarioId(), id, request.PersonagemId, cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
    }

    [HttpPut("{id:guid}/membros/{usuarioId:guid}/personagem")]
    [ProducesResponseType<MembroDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DefinirPersonagemDoMembro(Guid id, Guid usuarioId, DefinirPersonagemRequest request, CancellationToken cancellationToken)
    {
        var resultado = await mesas.DefinirPersonagemDoMembroAsync(User.ObterUsuarioId(), id, usuarioId, request.PersonagemId, cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
    }

    /// <summary>Personagens de um jogador da mesa, para o Mestre escolher qual ele usará.</summary>
    [HttpGet("{id:guid}/membros/{usuarioId:guid}/personagens")]
    [ProducesResponseType<IReadOnlyList<PersonagemResumoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListarPersonagensDoMembro(Guid id, Guid usuarioId, CancellationToken cancellationToken)
    {
        var resultado = await mesas.ListarPersonagensDoMembroAsync(User.ObterUsuarioId(), id, usuarioId, cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
    }

    /// <summary>O Mestre remove um jogador; o jogador sai da mesa passando o próprio id.</summary>
    [HttpDelete("{id:guid}/membros/{usuarioId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoverMembro(Guid id, Guid usuarioId, CancellationToken cancellationToken)
    {
        var resultado = await mesas.RemoverMembroAsync(User.ObterUsuarioId(), id, usuarioId, cancellationToken);
        return resultado.Sucesso ? NoContent() : this.Problema(resultado.Erro!);
    }

    [HttpPost("{id:guid}/convites")]
    [ProducesResponseType<ConviteDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GerarConvite(Guid id, ConviteRequest? request, CancellationToken cancellationToken)
    {
        var resultado = await convites.GerarAsync(User.ObterUsuarioId(), id, request ?? new ConviteRequest(null, null), cancellationToken);
        return resultado.Sucesso
            ? StatusCode(StatusCodes.Status201Created, resultado.Valor)
            : this.Problema(resultado.Erro!);
    }

    [HttpGet("{id:guid}/convites")]
    [ProducesResponseType<IReadOnlyList<ConviteDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListarConvites(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await convites.ListarAsync(User.ObterUsuarioId(), id, cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
    }

    [HttpPost("{id:guid}/mapas")]
    [ProducesResponseType<MapaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CriarMapa(Guid id, MapaRequest request, CancellationToken cancellationToken)
    {
        var resultado = await mapas.CriarAsync(User.ObterUsuarioId(), id, request, cancellationToken);
        return resultado.Sucesso
            ? StatusCode(StatusCodes.Status201Created, resultado.Valor)
            : this.Problema(resultado.Erro!);
    }

    /// <summary>Cria um mapa com uma masmorra gerada a partir de uma semente (ao acaso, se ausente).</summary>
    [HttpPost("{id:guid}/mapas/procedural")]
    [ProducesResponseType<MapaEstadoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GerarMapa(Guid id, MapaProceduralRequest request, CancellationToken cancellationToken)
    {
        var resultado = await mapas.GerarAsync(User.ObterUsuarioId(), id, request, cancellationToken);
        return resultado.Sucesso
            ? StatusCode(StatusCodes.Status201Created, resultado.Valor)
            : this.Problema(resultado.Erro!);
    }

    /// <summary>
    /// Envia uma imagem para a galeria da mesa (multipart, campo "arquivo"). O tipo é detectado pelo
    /// conteúdo e a imagem é recodificada antes de ser gravada.
    /// </summary>
    [HttpPost("{id:guid}/imagens")]
    [EnableRateLimiting(RateLimitingOptions.PoliticaUpload)]
    [RequestSizeLimit(LimiteDoCorpoDoUpload)]
    [RequestFormLimits(MultipartBodyLengthLimit = LimiteDoCorpoDoUpload)]
    [ProducesResponseType<ImagemDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> EnviarImagem(Guid id, IFormFile? arquivo, CancellationToken cancellationToken)
    {
        await using var conteudo = arquivo?.OpenReadStream() ?? Stream.Null;
        var resultado = await imagens.EnviarAsync(
            User.ObterUsuarioId(), id, arquivo?.FileName, conteudo, arquivo?.Length ?? 0, cancellationToken);
        return resultado.Sucesso
            ? StatusCode(StatusCodes.Status201Created, resultado.Valor)
            : this.Problema(resultado.Erro!);
    }

    /// <summary>A galeria da mesa e o uso da cota, para o Mestre.</summary>
    [HttpGet("{id:guid}/imagens")]
    [ProducesResponseType<GaleriaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListarImagens(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await imagens.ListarAsync(User.ObterUsuarioId(), id, cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
    }

    /// <summary>O Mestre vê todos os mapas da mesa; o jogador, só o ativo.</summary>
    [HttpGet("{id:guid}/mapas")]
    [ProducesResponseType<IReadOnlyList<MapaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListarMapas(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await mapas.ListarAsync(User.ObterUsuarioId(), id, cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
    }
}
