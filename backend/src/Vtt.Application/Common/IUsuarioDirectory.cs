namespace Vtt.Application.Common;

/// <summary>Leitura de dados públicos dos usuários, sem expor o Identity à camada Application.</summary>
public interface IUsuarioDirectory
{
    Task<IReadOnlyDictionary<Guid, string>> ObterNomesDeExibicaoAsync(
        IReadOnlyCollection<Guid> usuarioIds, CancellationToken cancellationToken = default);
}
