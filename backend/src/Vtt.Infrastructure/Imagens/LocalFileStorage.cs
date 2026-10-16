using Microsoft.Extensions.Options;
using Vtt.Application.Common;

namespace Vtt.Infrastructure.Imagens;

/// <summary>Arquivos em uma pasta do disco local (o volume de uploads do Docker).</summary>
public class LocalFileStorage(IOptions<StorageOptions> opcoes) : IFileStorage
{
    private readonly string _raiz = Path.GetFullPath(opcoes.Value.UploadsPath);

    public async Task SalvarAsync(string caminho, ReadOnlyMemory<byte> conteudo, CancellationToken cancellationToken = default)
    {
        var destino = Resolver(caminho);
        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);

        // Grava em um temporário e renomeia: ninguém lê um arquivo pela metade.
        var temporario = destino + ".tmp";
        await File.WriteAllBytesAsync(temporario, conteudo, cancellationToken);
        File.Move(temporario, destino, overwrite: true);
    }

    public Task<Stream?> AbrirAsync(string caminho, CancellationToken cancellationToken = default)
    {
        var origem = Resolver(caminho);
        Stream? conteudo = File.Exists(origem)
            ? new FileStream(origem, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan)
            : null;
        return Task.FromResult(conteudo);
    }

    public Task ExcluirAsync(string caminho, CancellationToken cancellationToken = default)
    {
        File.Delete(Resolver(caminho));
        return Task.CompletedTask;
    }

    public Task ExcluirPastaAsync(string pasta, CancellationToken cancellationToken = default)
    {
        var alvo = Resolver(pasta);
        if (Directory.Exists(alvo))
        {
            Directory.Delete(alvo, recursive: true);
        }

        return Task.CompletedTask;
    }

    /// <summary>Caminho absoluto dentro da raiz; qualquer tentativa de sair dela é erro de programação.</summary>
    private string Resolver(string caminho)
    {
        var completo = Path.GetFullPath(Path.Combine(_raiz, caminho));
        if (!completo.StartsWith(_raiz + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Caminho fora da pasta de uploads.");
        }

        return completo;
    }
}
