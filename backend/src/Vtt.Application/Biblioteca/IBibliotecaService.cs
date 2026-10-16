using Vtt.Application.Common;

namespace Vtt.Application.Biblioteca;

/// <summary>
/// Categorias e modelos da biblioteca de tokens. Tudo é restrito ao dono: o que é de outro usuário
/// se comporta como inexistente.
/// </summary>
public interface IBibliotecaService
{
    Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Conflito se o nome repetir (sem diferenciar maiúsculas) ou o limite de categorias for atingido.</summary>
    Task<Resultado<CategoriaDto>> CriarCategoriaAsync(Guid usuarioId, CategoriaRequest request, CancellationToken cancellationToken = default);

    /// <summary>Cria as categorias sugeridas que ainda faltam e devolve todas as do usuário.</summary>
    Task<Resultado<IReadOnlyList<CategoriaDto>>> CriarCategoriasSugeridasAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    Task<Resultado<CategoriaDto>> RenomearCategoriaAsync(Guid usuarioId, Guid id, CategoriaRequest request, CancellationToken cancellationToken = default);

    /// <summary>Os modelos da categoria ficam sem categoria.</summary>
    Task<Resultado> ExcluirCategoriaAsync(Guid usuarioId, Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModeloDeTokenDto>> ListarModelosAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    Task<Resultado<ModeloDeTokenDto>> ObterModeloAsync(Guid usuarioId, Guid id, CancellationToken cancellationToken = default);

    Task<Resultado<ModeloDeTokenDto>> CriarModeloAsync(Guid usuarioId, ModeloDeTokenRequest request, CancellationToken cancellationToken = default);

    Task<Resultado<ModeloDeTokenDto>> AtualizarModeloAsync(Guid usuarioId, Guid id, ModeloDeTokenRequest request, CancellationToken cancellationToken = default);

    /// <summary>Os tokens já colocados nos mapas ficam como estão.</summary>
    Task<Resultado> ExcluirModeloAsync(Guid usuarioId, Guid id, CancellationToken cancellationToken = default);
}
