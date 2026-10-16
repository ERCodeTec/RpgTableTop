using System.Net.Http.Headers;
using System.Net.Http.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using Vtt.Application.Imagens;
using Vtt.Application.Mapas;
using Vtt.Domain.Mapas;

namespace Vtt.Api.IntegrationTests;

/// <summary>Imagens geradas na hora, para os testes não dependerem de arquivos no repositório.</summary>
public static class ImagensDeTeste
{
    public static byte[] Png(int largura = 32, int altura = 24) => Gerar(largura, altura, new PngEncoder());

    public static byte[] Webp(int largura = 32, int altura = 24) => Gerar(largura, altura, new WebpEncoder());

    /// <summary>GIF animado com <paramref name="quadros"/> quadros.</summary>
    public static byte[] GifAnimado(int quadros = 3, int largura = 32, int altura = 24)
    {
        using var imagem = new Image<Rgba32>(largura, altura, Color.SteelBlue);
        for (var i = 1; i < quadros; i++)
        {
            using var quadro = new Image<Rgba32>(largura, altura, i % 2 == 0 ? Color.SteelBlue : Color.Goldenrod);
            imagem.Frames.AddFrame(quadro.Frames.RootFrame);
        }

        using var saida = new MemoryStream();
        imagem.Save(saida, new GifEncoder());
        return saida.ToArray();
    }

    /// <summary>JPEG com EXIF (fabricante da câmera e GPS), como uma foto de celular.</summary>
    public static byte[] JpegComExif(int largura = 32, int altura = 24)
    {
        using var imagem = new Image<Rgba32>(largura, altura, Color.SeaGreen);
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Make, "CameraSecreta");
        exif.SetValue(ExifTag.GPSLatitudeRef, "S");
        imagem.Metadata.ExifProfile = exif;
        using var saida = new MemoryStream();
        imagem.Save(saida, new JpegEncoder());
        return saida.ToArray();
    }

    /// <summary>PNG de ruído, que não comprime: serve para ocupar bytes da cota.</summary>
    public static byte[] PngPesado(int lado = 96)
    {
        var aleatorio = new Random(7);
        using var imagem = new Image<Rgba32>(lado, lado);
        for (var y = 0; y < lado; y++)
        {
            for (var x = 0; x < lado; x++)
            {
                imagem[x, y] = new Rgba32((byte)aleatorio.Next(256), (byte)aleatorio.Next(256), (byte)aleatorio.Next(256));
            }
        }

        using var saida = new MemoryStream();
        imagem.Save(saida, new PngEncoder());
        return saida.ToArray();
    }

    private static byte[] Gerar(int largura, int altura, IImageEncoder codificador)
    {
        using var imagem = new Image<Rgba32>(largura, altura, Color.SteelBlue);
        using var saida = new MemoryStream();
        imagem.Save(saida, codificador);
        return saida.ToArray();
    }
}

public static class ImagemTestHelpers
{
    /// <summary>Envia o arquivo como o navegador faria. Nome e Content-Type são o que o cliente declara, verdadeiros ou não.</summary>
    public static async Task<HttpResponseMessage> EnviarImagemAsync(
        this Sessao sessao, Guid mesaId, byte[] conteudo, string nome = "mapa.png", string contentType = "image/png")
    {
        using var formulario = new MultipartFormDataContent();
        var arquivo = new ByteArrayContent(conteudo);
        arquivo.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        formulario.Add(arquivo, "arquivo", nome);
        return await sessao.Client.PostAsync($"/api/mesas/{mesaId}/imagens", formulario);
    }

    public static async Task<ImagemDto> CriarImagemAsync(this Sessao mestre, Guid mesaId, byte[]? conteudo = null, string nome = "mapa.png")
    {
        var resposta = await mestre.EnviarImagemAsync(mesaId, conteudo ?? ImagensDeTeste.Png(), nome);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<ImagemDto>(ApiTestHelpers.Json))!;
    }

    public static EstruturaRequest Estrutura(
        TipoEstrutura tipo = TipoEstrutura.Parede, int x = 0, int y = 0, int largura = 1, int profundidade = 1,
        double altura = 2, int rotacao = 0, Guid? textura = null, bool? bloqueia = true, bool? portaAberta = null, int? versao = null) =>
        new(tipo, x, y, largura, profundidade, altura, rotacao, "#6b6f76", textura, bloqueia, portaAberta, versao);

    public static Task<HttpResponseMessage> PostarEstruturaAsync(this Sessao sessao, Guid mapaId, EstruturaRequest request) =>
        sessao.Client.PostAsJsonAsync($"/api/mapas/{mapaId}/estruturas", request, ApiTestHelpers.Json);

    public static async Task<EstruturaDto> CriarEstruturaAsync(this Sessao mestre, Guid mapaId, EstruturaRequest? request = null)
    {
        var resposta = await mestre.PostarEstruturaAsync(mapaId, request ?? Estrutura());
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<EstruturaDto>(ApiTestHelpers.Json))!;
    }

    public static async Task<EstruturaDto> DefinirPortaAsync(this Sessao mestre, Guid estruturaId, bool aberta)
    {
        var resposta = await mestre.Client.PutAsJsonAsync($"/api/estruturas/{estruturaId}/porta", new PortaRequest(aberta));
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<EstruturaDto>(ApiTestHelpers.Json))!;
    }
}
