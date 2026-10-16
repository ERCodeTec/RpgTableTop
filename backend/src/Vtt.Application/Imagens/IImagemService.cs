using Vtt.Application.Common;

namespace Vtt.Application.Imagens;

/// <summary>
/// Imagens de mesa: enviar, listar e excluir são do Mestre; qualquer membro da mesa baixa as imagens dela.
/// Imagens pessoais (a galeria da biblioteca de tokens): tudo é só do dono. Para os demais, a imagem não existe.
/// </summary>
public interface IImagemService
{
    /// <param name="nomeDoArquivo">O nome informado pelo cliente; é sanitizado e usado só para exibição.</param>
    Task<Resultado<ImagemDto>> EnviarAsync(Guid usuarioId, Guid mesaId, string? nomeDoArquivo, Stream conteudo, long tamanho, CancellationToken cancellationToken = default);

    Task<Resultado<ImagemDto>> EnviarPessoalAsync(Guid usuarioId, string? nomeDoArquivo, Stream conteudo, long tamanho, CancellationToken cancellationToken = default);

    Task<GaleriaDto> ListarPessoaisAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A cópia da imagem pessoal na mesa, criada na primeira vez e reaproveitada depois. Conflito se a
    /// cópia estourar a cota da mesa; NaoEncontrado se a imagem não for do usuário ou o arquivo sumiu.
    /// </summary>
    Task<Resultado<Guid>> ObterCopiaNaMesaAsync(Guid usuarioId, Guid imagemPessoalId, Guid mesaId, CancellationToken cancellationToken = default);

    Task<Resultado<GaleriaDto>> ListarAsync(Guid usuarioId, Guid mesaId, CancellationToken cancellationToken = default);

    Task<Resultado<ArquivoDeImagem>> AbrirAsync(Guid usuarioId, Guid imagemId, bool miniatura, CancellationToken cancellationToken = default);

    /// <summary>Conflito enquanto a imagem for fundo de mapa, imagem de token, textura de estrutura ou imagem de modelo.</summary>
    Task<Resultado> ExcluirAsync(Guid usuarioId, Guid imagemId, CancellationToken cancellationToken = default);
}
