namespace Vtt.Application.Imagens;

public record ImagemProcessada(byte[] Conteudo, byte[] Miniatura, int Largura, int Altura);

/// <summary>Decodifica e recodifica as imagens enviadas. Nada do arquivo original é gravado como veio.</summary>
public interface IImagemProcessor
{
    /// <summary>
    /// Lê só os cabeçalhos, sem decodificar os pixels: as dimensões e quantos quadros a imagem terá
    /// depois de recodificada (só o GIF guarda a animação; nos outros formatos, 1).
    /// null se o conteúdo não for uma imagem válida do tipo.
    /// </summary>
    (int Largura, int Altura, int Quadros)? LerDimensoes(byte[] dados, TipoDeImagem tipo);

    /// <summary>
    /// Recodifica no mesmo formato, sem metadados (EXIF, ICC, XMP) nem dados escondidos após a imagem,
    /// e gera a miniatura em WebP a partir do primeiro quadro. O GIF mantém todos os quadros; nos
    /// outros formatos, só o primeiro segue adiante. null se a imagem estiver corrompida.
    /// </summary>
    ImagemProcessada? Recodificar(byte[] dados, TipoDeImagem tipo, int ladoDaMiniatura);
}
