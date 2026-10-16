using Vtt.Application.Common;
using Vtt.Domain.Mesas;

namespace Vtt.Application.Mesas;

/// <summary>
/// Em todas as operações, quem não é membro recebe NaoEncontrado (a mesa "não existe" para ele)
/// e o membro sem o papel exigido recebe Proibido.
/// </summary>
public interface IMesaService
{
    Task<MesaDetalheDto> CriarAsync(Guid usuarioId, MesaRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MesaResumoDto>> ListarAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    Task<Resultado<MesaDetalheDto>> ObterAsync(Guid usuarioId, Guid mesaId, CancellationToken cancellationToken = default);

    Task<bool> EhMembroAsync(Guid usuarioId, Guid mesaId, CancellationToken cancellationToken = default);

    Task<Resultado<MesaDetalheDto>> AtualizarAsync(Guid usuarioId, Guid mesaId, MesaRequest request, CancellationToken cancellationToken = default);

    Task<Resultado> ExcluirAsync(Guid usuarioId, Guid mesaId, CancellationToken cancellationToken = default);

    Task<Resultado<MesaDetalheDto>> DefinirSessaoAsync(Guid usuarioId, Guid mesaId, bool ativa, CancellationToken cancellationToken = default);

    Task<Resultado<MesaDetalheDto>> DefinirMovimentacaoAsync(Guid usuarioId, Guid mesaId, ModoDeMovimentacao modo, CancellationToken cancellationToken = default);

    /// <summary>O jogador troca o próprio personagem; bloqueado (Conflito) com a sessão ativa.</summary>
    Task<Resultado<MembroDto>> DefinirMeuPersonagemAsync(Guid usuarioId, Guid mesaId, Guid personagemId, CancellationToken cancellationToken = default);

    /// <summary>O Mestre define o personagem de um membro, a qualquer momento.</summary>
    Task<Resultado<MembroDto>> DefinirPersonagemDoMembroAsync(Guid usuarioId, Guid mesaId, Guid membroUsuarioId, Guid personagemId, CancellationToken cancellationToken = default);

    /// <summary>Personagens de um membro, para o Mestre escolher qual ele usará.</summary>
    Task<Resultado<IReadOnlyList<PersonagemResumoDto>>> ListarPersonagensDoMembroAsync(Guid usuarioId, Guid mesaId, Guid membroUsuarioId, CancellationToken cancellationToken = default);

    /// <summary>O Mestre remove um jogador, ou o jogador sai (quando os dois ids são iguais). O Mestre não sai.</summary>
    Task<Resultado> RemoverMembroAsync(Guid usuarioId, Guid mesaId, Guid membroUsuarioId, CancellationToken cancellationToken = default);
}
