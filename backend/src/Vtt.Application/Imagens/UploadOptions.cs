namespace Vtt.Application.Imagens;

public class UploadOptions
{
    public const string Secao = "Uploads";

    public long TamanhoMaximoBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>Maior largura ou altura aceita, em pixels.</summary>
    public int DimensaoMaxima { get; set; } = 4096;

    /// <summary>
    /// Soma dos pixels de todos os quadros de um GIF animado (largura × altura × quadros). Cada quadro
    /// é decodificado na memória ao gravar a imagem: o padrão ocupa até ~256 MB nesse momento.
    /// </summary>
    public long PixelsMaximosDaAnimacao { get; set; } = 64L * 1024 * 1024;

    public long CotaBytesPorMesa { get; set; } = 200L * 1024 * 1024;

    public int CotaImagensPorMesa { get; set; } = 100;

    /// <summary>A galeria pessoal da biblioteca de tokens, por usuário.</summary>
    public long CotaBytesPorUsuario { get; set; } = 200L * 1024 * 1024;

    public int CotaImagensPorUsuario { get; set; } = 100;
}
