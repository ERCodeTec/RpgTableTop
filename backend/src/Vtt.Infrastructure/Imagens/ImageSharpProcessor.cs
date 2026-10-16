using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using Vtt.Application.Imagens;

namespace Vtt.Infrastructure.Imagens;

public class ImageSharpProcessor : IImagemProcessor
{
    // Só os quatro formatos aceitos são registrados: nenhum outro decodificador chega a rodar.
    private static readonly Configuration Formatos = new(
        new PngConfigurationModule(), new JpegConfigurationModule(), new WebpConfigurationModule(), new GifConfigurationModule());

    private static readonly DecoderOptions Identificacao = new() { Configuration = Formatos };

    // O GIF é o único formato animado que o mapa reproduz: os outros ficam só com o primeiro quadro.
    // O total de pixels da animação já foi conferido pelo ImagemService antes de decodificar.
    private static readonly DecoderOptions Animado = new() { Configuration = Formatos };
    private static readonly DecoderOptions SoOPrimeiroQuadro = new() { Configuration = Formatos, MaxFrames = 1 };

    public (int Largura, int Altura, int Quadros)? LerDimensoes(byte[] dados, TipoDeImagem tipo)
    {
        try
        {
            var info = Image.Identify(Identificacao, dados);
            if (info.Metadata.DecodedImageFormat?.DefaultMimeType != tipo.ContentType)
            {
                return null;
            }

            var quadros = tipo == TipoDeImagem.Gif ? Math.Max(1, info.FrameMetadataCollection.Count) : 1;
            return (info.Width, info.Height, quadros);
        }
        catch (Exception ex) when (ex is ImageFormatException or NotSupportedException or InvalidDataException)
        {
            return null;
        }
    }

    public ImagemProcessada? Recodificar(byte[] dados, TipoDeImagem tipo, int ladoDaMiniatura)
    {
        try
        {
            using var imagem = Image.Load(tipo == TipoDeImagem.Gif ? Animado : SoOPrimeiroQuadro, dados);

            // Só os pixels seguem adiante.
            imagem.Metadata.ExifProfile = null;
            imagem.Metadata.IccProfile = null;
            imagem.Metadata.XmpProfile = null;
            imagem.Metadata.IptcProfile = null;

            var (largura, altura) = (imagem.Width, imagem.Height);

            using var principal = new MemoryStream();
            imagem.Save(principal, Codificador(tipo));

            using var primeiroQuadro = imagem.Frames.CloneFrame(0);
            primeiroQuadro.Mutate(contexto => contexto.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(ladoDaMiniatura, ladoDaMiniatura)
            }));
            using var miniatura = new MemoryStream();
            primeiroQuadro.Save(miniatura, new WebpEncoder { Quality = 80 });

            return new ImagemProcessada(principal.ToArray(), miniatura.ToArray(), largura, altura);
        }
        catch (Exception ex) when (ex is ImageFormatException or NotSupportedException or InvalidDataException)
        {
            return null;
        }
    }

    private static IImageEncoder Codificador(TipoDeImagem tipo) =>
        tipo == TipoDeImagem.Png ? new PngEncoder()
        : tipo == TipoDeImagem.Jpeg ? new JpegEncoder { Quality = 88 }
        : tipo == TipoDeImagem.Gif ? new GifEncoder()
        : new WebpEncoder { Quality = 88 };
}
