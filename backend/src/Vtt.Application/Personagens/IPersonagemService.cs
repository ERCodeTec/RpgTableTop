namespace Vtt.Application.Personagens;

/// <summary>
/// Todas as operações são restritas ao dono: personagens de outros usuários se comportam como inexistentes.
/// </summary>
public interface IPersonagemService
{
    Task<IReadOnlyList<PersonagemDto>> ListarAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    Task<PersonagemDto?> ObterAsync(Guid usuarioId, Guid id, CancellationToken cancellationToken = default);

    Task<PersonagemDto> CriarAsync(Guid usuarioId, PersonagemRequest request, CancellationToken cancellationToken = default);

    Task<PersonagemDto?> AtualizarAsync(Guid usuarioId, Guid id, PersonagemRequest request, CancellationToken cancellationToken = default);

    Task<bool> ExcluirAsync(Guid usuarioId, Guid id, CancellationToken cancellationToken = default);
}
