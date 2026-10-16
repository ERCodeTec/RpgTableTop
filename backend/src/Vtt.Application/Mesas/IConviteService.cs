using Vtt.Application.Common;

namespace Vtt.Application.Mesas;

public interface IConviteService
{
    Task<Resultado<ConviteDto>> GerarAsync(Guid usuarioId, Guid mesaId, ConviteRequest request, CancellationToken cancellationToken = default);

    Task<Resultado<IReadOnlyList<ConviteDto>>> ListarAsync(Guid usuarioId, Guid mesaId, CancellationToken cancellationToken = default);

    Task<Resultado> RevogarAsync(Guid usuarioId, string codigo, CancellationToken cancellationToken = default);

    /// <summary>Convite inexistente, expirado, revogado ou esgotado: sempre o mesmo NaoEncontrado.</summary>
    Task<Resultado<ConvitePreviaDto>> PreVisualizarAsync(Guid usuarioId, string codigo, CancellationToken cancellationToken = default);

    Task<Resultado<AceitarConviteResponse>> AceitarAsync(Guid usuarioId, string codigo, Guid personagemId, CancellationToken cancellationToken = default);
}
