using Vtt.Application.Common;

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

    /// <summary>
    /// Envia a foto do personagem (uma só): substitui a anterior, que é apagada se não tiver outro uso.
    /// A imagem entra na galeria pessoal e passa pelas mesmas regras e cotas dela.
    /// </summary>
    /// <param name="nomeDoArquivo">O nome informado pelo cliente; é sanitizado e usado só para exibição.</param>
    Task<Resultado<PersonagemDto>> DefinirFotoAsync(Guid usuarioId, Guid id, string? nomeDoArquivo, Stream conteudo, long tamanho, CancellationToken cancellationToken = default);

    /// <summary>Tira a foto: o token volta ao ícone da classe.</summary>
    Task<PersonagemDto?> RemoverFotoAsync(Guid usuarioId, Guid id, CancellationToken cancellationToken = default);
}
