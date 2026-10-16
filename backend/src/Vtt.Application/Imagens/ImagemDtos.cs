using Vtt.Domain.Imagens;

namespace Vtt.Application.Imagens;

public record ImagemDto(
    Guid Id,
    Guid? MesaId,
    string NomeOriginal,
    string ContentType,
    long TamanhoBytes,
    int Largura,
    int Altura,
    DateTimeOffset CriadaEm)
{
    public static ImagemDto De(ImagemUpload imagem) => new(
        imagem.Id,
        imagem.MesaId,
        imagem.NomeOriginal,
        imagem.ContentType,
        imagem.TamanhoBytes,
        imagem.Largura,
        imagem.Altura,
        imagem.CriadaEm);
}

/// <summary>A galeria (da mesa ou a pessoal) e quanto da cota ela já usa.</summary>
public record GaleriaDto(
    IReadOnlyList<ImagemDto> Imagens,
    long BytesUsados,
    long CotaBytes,
    int CotaImagens,
    long TamanhoMaximoBytes,
    int DimensaoMaxima);

/// <summary>Um arquivo pronto para ser devolvido; quem recebe fecha o <paramref name="Conteudo"/>.</summary>
public record ArquivoDeImagem(Stream Conteudo, string ContentType, string ETag);
