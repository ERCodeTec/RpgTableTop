using Vtt.Application.Common;

namespace Vtt.Application.Mapas;

/// <summary>Estruturas do cenário. Todas as operações são do Mestre.</summary>
public interface IEstruturaService
{
    Task<Resultado<EstruturaDto>> CriarAsync(Guid usuarioId, Guid mapaId, EstruturaRequest request, CancellationToken cancellationToken = default);

    /// <summary>Conflito se a versão enviada não for a atual.</summary>
    Task<Resultado<EstruturaDto>> EditarAsync(Guid usuarioId, Guid estruturaId, EstruturaRequest request, CancellationToken cancellationToken = default);

    /// <summary>Abre ou fecha uma porta. Conflito se a estrutura não for uma porta.</summary>
    Task<Resultado<EstruturaDto>> DefinirPortaAsync(Guid usuarioId, Guid estruturaId, bool aberta, CancellationToken cancellationToken = default);

    Task<Resultado> RemoverAsync(Guid usuarioId, Guid estruturaId, CancellationToken cancellationToken = default);
}
