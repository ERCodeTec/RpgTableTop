namespace Vtt.Application.Imagens;

/// <summary>Limites dos uploads. Nos opcionais, null é sem limite (o padrão).</summary>
public class UploadOptions
{
    public const string Secao = "Uploads";

    public long? TamanhoMaximoBytes { get; set; }

    /// <summary>Maior largura ou altura aceita, em pixels. Fica alto só para uma imagem gigante não esgotar a memória.</summary>
    public int DimensaoMaxima { get; set; } = 16384;

    /// <summary>
    /// Soma dos pixels de todos os quadros de um GIF animado (largura × altura × quadros). Cada quadro
    /// é decodificado na memória ao gravar a imagem: o padrão ocupa até ~1 GB nesse momento.
    /// </summary>
    public long PixelsMaximosDaAnimacao { get; set; } = 256L * 1024 * 1024;

    public long? CotaBytesPorMesa { get; set; }

    public int? CotaImagensPorMesa { get; set; }

    /// <summary>A galeria pessoal da biblioteca de tokens, por usuário.</summary>
    public long? CotaBytesPorUsuario { get; set; }

    public int? CotaImagensPorUsuario { get; set; }
}
