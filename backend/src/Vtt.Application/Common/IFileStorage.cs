namespace Vtt.Application.Common;

/// <summary>
/// Guarda os arquivos enviados. Os caminhos são relativos à raiz do armazenamento e montados pelo
/// servidor; a implementação recusa qualquer caminho que saia dessa raiz.
/// </summary>
public interface IFileStorage
{
    Task SalvarAsync(string caminho, ReadOnlyMemory<byte> conteudo, CancellationToken cancellationToken = default);

    /// <returns>null se o arquivo não existir.</returns>
    Task<Stream?> AbrirAsync(string caminho, CancellationToken cancellationToken = default);

    /// <summary>Não falha se o arquivo já não existir.</summary>
    Task ExcluirAsync(string caminho, CancellationToken cancellationToken = default);

    /// <summary>Apaga a pasta com tudo o que há nela. Não falha se ela não existir.</summary>
    Task ExcluirPastaAsync(string pasta, CancellationToken cancellationToken = default);
}
