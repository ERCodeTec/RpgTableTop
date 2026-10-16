namespace Vtt.Application.Imagens;

/// <summary>Os formatos aceitos, reconhecidos pelos primeiros bytes do arquivo (magic bytes).</summary>
public sealed record TipoDeImagem(string ContentType, string Extensao)
{
    public static readonly TipoDeImagem Png = new("image/png", "png");
    public static readonly TipoDeImagem Jpeg = new("image/jpeg", "jpg");
    public static readonly TipoDeImagem Webp = new("image/webp", "webp");
    public static readonly TipoDeImagem Gif = new("image/gif", "gif");

    private static readonly byte[] AssinaturaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// O tipo real do conteúdo, ou null se não for PNG, JPEG, WebP nem GIF. A extensão e o Content-Type
    /// informados pelo cliente não entram na conta.
    /// </summary>
    public static TipoDeImagem? Detectar(ReadOnlySpan<byte> dados)
    {
        if (dados.StartsWith(AssinaturaPng))
        {
            return Png;
        }

        if (dados.Length >= 3 && dados[0] == 0xFF && dados[1] == 0xD8 && dados[2] == 0xFF)
        {
            return Jpeg;
        }

        // RIFF <tamanho> WEBP
        if (dados.Length >= 12 && dados[..4].SequenceEqual("RIFF"u8) && dados.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return Webp;
        }

        // GIF87a ou GIF89a
        if (dados.StartsWith("GIF87a"u8) || dados.StartsWith("GIF89a"u8))
        {
            return Gif;
        }

        return null;
    }
}
