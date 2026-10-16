using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vtt.Api.Infrastructure;
using Vtt.Application.Common;
using Vtt.Application.Iniciativas;

namespace Vtt.Api.Controllers;

/// <summary>A iniciativa da mesa. Cada mudança chega a todos pelo evento IniciativaAlterada.</summary>
[ApiController]
[Authorize]
[Route("api/mesas/{mesaId:guid}/iniciativa")]
public class IniciativaController(IIniciativaService iniciativa) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IniciativaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obter(Guid mesaId, CancellationToken cancellationToken) =>
        Responder(await iniciativa.ObterAsync(User.ObterUsuarioId(), mesaId, cancellationToken));

    /// <summary>Só o Mestre: pede a iniciativa de novo, apagando os valores anteriores.</summary>
    [HttpPost("solicitar")]
    [ProducesResponseType<IniciativaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Solicitar(Guid mesaId, CancellationToken cancellationToken) =>
        Responder(await iniciativa.SolicitarAsync(User.ObterUsuarioId(), mesaId, cancellationToken));

    /// <summary>O jogador informa a própria iniciativa, uma única vez por pedido.</summary>
    [HttpPost("minha")]
    [ProducesResponseType<IniciativaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> InformarMinha(Guid mesaId, ValorDeIniciativaRequest request, CancellationToken cancellationToken) =>
        Responder(await iniciativa.InformarMinhaAsync(User.ObterUsuarioId(), mesaId, request.Valor!.Value, cancellationToken));

    [HttpPut("jogadores/{usuarioId:guid}")]
    [ProducesResponseType<IniciativaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DefinirDoJogador(Guid mesaId, Guid usuarioId, ValorDeIniciativaRequest request, CancellationToken cancellationToken) =>
        Responder(await iniciativa.DefinirDoJogadorAsync(User.ObterUsuarioId(), mesaId, usuarioId, request.Valor!.Value, cancellationToken));

    [HttpPut("grupos")]
    [ProducesResponseType<IniciativaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DefinirDoGrupo(Guid mesaId, ValorDeGrupoRequest request, CancellationToken cancellationToken) =>
        Responder(await iniciativa.DefinirDoGrupoAsync(User.ObterUsuarioId(), mesaId, request.Grupo!, request.Valor!.Value, cancellationToken));

    [HttpPost("iniciar")]
    [ProducesResponseType<IniciativaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Iniciar(Guid mesaId, CancellationToken cancellationToken) =>
        Responder(await iniciativa.IniciarAsync(User.ObterUsuarioId(), mesaId, cancellationToken));

    /// <summary>O dono do token da vez, ou o Mestre, passa a vez ao próximo.</summary>
    [HttpPost("encerrar-vez")]
    [ProducesResponseType<IniciativaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EncerrarVez(Guid mesaId, EncerrarVezRequest request, CancellationToken cancellationToken) =>
        Responder(await iniciativa.EncerrarVezAsync(User.ObterUsuarioId(), mesaId, request.TokenId!.Value, cancellationToken));

    /// <summary>Só o Mestre: encerra o combate e apaga os valores.</summary>
    [HttpDelete]
    [ProducesResponseType<IniciativaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EncerrarCombate(Guid mesaId, CancellationToken cancellationToken) =>
        Responder(await iniciativa.EncerrarCombateAsync(User.ObterUsuarioId(), mesaId, cancellationToken));

    private IActionResult Responder(Resultado<IniciativaDto> resultado) =>
        resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
}
