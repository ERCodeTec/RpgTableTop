using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vtt.Api.Infrastructure;
using Vtt.Application.Mapas;

namespace Vtt.Api.Controllers;

/// <summary>Criar e listar mapas de uma mesa ficam em <see cref="MesasController"/> (/api/mesas/{id}/mapas).</summary>
[ApiController]
[Authorize]
[Route("api/mapas")]
public class MapasController(IMapaService mapas, ITokenService tokens, IEstruturaService estruturas, INevoaService nevoa) : ControllerBase
{
    [HttpPut("{id:guid}")]
    [ProducesResponseType<MapaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Atualizar(Guid id, MapaRequest request, CancellationToken cancellationToken)
    {
        var resultado = await mapas.AtualizarAsync(User.ObterUsuarioId(), id, request, cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await mapas.ExcluirAsync(User.ObterUsuarioId(), id, cancellationToken);
        return resultado.Sucesso ? NoContent() : this.Problema(resultado.Erro!);
    }

    /// <summary>Define o mapa ativo da mesa, desativando o anterior.</summary>
    [HttpPut("{id:guid}/ativar")]
    [ProducesResponseType<MapaEstadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Ativar(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await mapas.AtivarAsync(User.ObterUsuarioId(), id, cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
    }

    /// <summary>Estado completo: o mapa e os tokens. Para o jogador, só o mapa ativo existe.</summary>
    [HttpGet("{id:guid}/estado")]
    [ProducesResponseType<MapaEstadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterEstado(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await mapas.ObterEstadoAsync(User.ObterUsuarioId(), id, cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
    }

    /// <summary>Define ou remove a imagem de fundo e o ajuste dela ao grid.</summary>
    [HttpPut("{id:guid}/fundo")]
    [ProducesResponseType<MapaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DefinirFundo(Guid id, FundoRequest request, CancellationToken cancellationToken)
    {
        var resultado = await mapas.DefinirFundoAsync(User.ObterUsuarioId(), id, request, cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
    }

    /// <summary>Liga, desliga ou troca o modo da névoa de guerra.</summary>
    [HttpPut("{id:guid}/nevoa")]
    [ProducesResponseType<MapaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DefinirNevoa(Guid id, NevoaRequest request, CancellationToken cancellationToken)
    {
        var resultado = await nevoa.DefinirModoAsync(User.ObterUsuarioId(), id, request, cancellationToken);
        return resultado.Sucesso ? Ok(resultado.Valor) : this.Problema(resultado.Erro!);
    }

    /// <summary>O pincel do Mestre: revela as células para todos, ou as cobre.</summary>
    [HttpPut("{id:guid}/nevoa/revelada")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevelarCelulas(Guid id, RevelarCelulasRequest request, CancellationToken cancellationToken)
    {
        var resultado = await nevoa.RevelarAsync(User.ObterUsuarioId(), id, request, cancellationToken);
        return resultado.Sucesso ? NoContent() : this.Problema(resultado.Erro!);
    }

    /// <summary>Esquece o que os jogadores exploraram; o que o Mestre revelou à mão fica.</summary>
    [HttpDelete("{id:guid}/nevoa/memoria")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LimparMemoria(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await nevoa.LimparMemoriaAsync(User.ObterUsuarioId(), id, cancellationToken);
        return resultado.Sucesso ? NoContent() : this.Problema(resultado.Erro!);
    }

    [HttpPost("{id:guid}/estruturas")]
    [ProducesResponseType<EstruturaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CriarEstrutura(Guid id, EstruturaRequest request, CancellationToken cancellationToken)
    {
        var resultado = await estruturas.CriarAsync(User.ObterUsuarioId(), id, request, cancellationToken);
        return resultado.Sucesso
            ? StatusCode(StatusCodes.Status201Created, resultado.Valor)
            : this.Problema(resultado.Erro!);
    }

    [HttpPost("{id:guid}/tokens")]
    [ProducesResponseType<TokenDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CriarToken(Guid id, CriarTokenRequest request, CancellationToken cancellationToken)
    {
        var resultado = await tokens.CriarAsync(User.ObterUsuarioId(), id, request, cancellationToken);
        return resultado.Sucesso
            ? StatusCode(StatusCodes.Status201Created, resultado.Valor)
            : this.Problema(resultado.Erro!);
    }

    [HttpPost("{id:guid}/tokens/da-biblioteca")]
    [ProducesResponseType<IReadOnlyList<TokenDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CriarTokensDaBiblioteca(Guid id, CriarDaBibliotecaRequest request, CancellationToken cancellationToken)
    {
        var resultado = await tokens.CriarDaBibliotecaAsync(User.ObterUsuarioId(), id, request, cancellationToken);
        return resultado.Sucesso
            ? StatusCode(StatusCodes.Status201Created, resultado.Valor)
            : this.Problema(resultado.Erro!);
    }
}
