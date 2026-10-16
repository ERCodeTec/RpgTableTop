using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using Vtt.Application.Imagens;
using Vtt.Application.Mapas;
using Vtt.Domain.Mapas;

namespace Vtt.Api.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class ImagensTests(VttApiFactory factory)
{
    private async Task<(Sessao Mestre, Guid MesaId)> CriarMesaAsync()
    {
        var mestre = await factory.CriarSessaoAsync("Mestre");
        return (mestre, (await mestre.CriarMesaAsync()).Id);
    }

    internal static async Task<string> DetalheAsync(HttpResponseMessage resposta)
    {
        var problema = await resposta.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        return string.Join(" ", problema!.Errors.SelectMany(erro => erro.Value)) + problema.Detail;
    }

    [Fact]
    public async Task Mestre_EnviaPng_EOArquivoVoltaRecodificadoComMiniatura()
    {
        var (mestre, mesaId) = await CriarMesaAsync();

        var resposta = await mestre.EnviarImagemAsync(mesaId, ImagensDeTeste.Png(640, 480), "Caverna do Eco.png");
        var imagem = (await resposta.Content.ReadFromJsonAsync<ImagemDto>(ApiTestHelpers.Json))!;
        var arquivo = await mestre.Client.GetAsync($"/api/imagens/{imagem.Id}");
        var miniatura = await mestre.Client.GetAsync($"/api/imagens/{imagem.Id}/miniatura");

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.Equal(("Caverna do Eco.png", "image/png", 640, 480), (imagem.NomeOriginal, imagem.ContentType, imagem.Largura, imagem.Altura));
        Assert.True(imagem.TamanhoBytes > 0);

        Assert.Equal(HttpStatusCode.OK, arquivo.StatusCode);
        Assert.Equal("image/png", arquivo.Content.Headers.ContentType!.MediaType);
        Assert.Equal(imagem.TamanhoBytes, arquivo.Content.Headers.ContentLength);
        Assert.Equal("nosniff", arquivo.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.True(arquivo.Headers.CacheControl!.Private);
        Assert.NotNull(arquivo.Headers.ETag);
        using var decodificada = Image.Load(await arquivo.Content.ReadAsByteArrayAsync());
        Assert.Equal((640, 480), (decodificada.Width, decodificada.Height));

        Assert.Equal("image/webp", miniatura.Content.Headers.ContentType!.MediaType);
        using var pequena = Image.Load(await miniatura.Content.ReadAsByteArrayAsync());
        Assert.Equal((256, 192), (pequena.Width, pequena.Height));
    }

    [Fact]
    public async Task ArquivoPngQueNaoEImagem_ERejeitado()
    {
        var (mestre, mesaId) = await CriarMesaAsync();
        var texto = Encoding.UTF8.GetBytes("#!/bin/sh\necho isto nao e uma imagem\n");
        // Cabeçalho de PNG verdadeiro seguido de lixo: passa pelos magic bytes, mas não decodifica.
        var falsoPng = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.Concat(texto).ToArray();

        var comoTexto = await mestre.EnviarImagemAsync(mesaId, texto, "inocente.png");
        var comCabecalho = await mestre.EnviarImagemAsync(mesaId, falsoPng, "inocente.png");
        var vazio = await mestre.EnviarImagemAsync(mesaId, [], "vazio.png");

        Assert.Equal(HttpStatusCode.BadRequest, comoTexto.StatusCode);
        Assert.Contains("não é uma imagem PNG, JPEG, WebP ou GIF", await DetalheAsync(comoTexto));
        Assert.Equal(HttpStatusCode.BadRequest, comCabecalho.StatusCode);
        Assert.Contains("corrompido", await DetalheAsync(comCabecalho));
        Assert.Equal(HttpStatusCode.BadRequest, vazio.StatusCode);
        Assert.Empty((await mestre.Client.GetFromJsonAsync<GaleriaDto>($"/api/mesas/{mesaId}/imagens", ApiTestHelpers.Json))!.Imagens);
        Assert.False(Directory.Exists(Path.Combine(factory.PastaDeUploads, mesaId.ToString("N"))));
    }

    [Fact]
    public async Task OTipoVemDoConteudo_NaoDaExtensaoNemDoContentTypeEnviado()
    {
        var (mestre, mesaId) = await CriarMesaAsync();

        var jpegDisfarcado = await mestre.CriarImagemAsync(mesaId, ImagensDeTeste.JpegComExif(), "foto.png");
        var respostaWebp = await mestre.EnviarImagemAsync(mesaId, ImagensDeTeste.Webp(), "textura.exe", "application/octet-stream");
        var webp = (await respostaWebp.Content.ReadFromJsonAsync<ImagemDto>(ApiTestHelpers.Json))!;

        Assert.Equal("image/jpeg", jpegDisfarcado.ContentType);
        Assert.Equal("image/webp", webp.ContentType);
        var arquivo = await mestre.Client.GetAsync($"/api/imagens/{jpegDisfarcado.Id}");
        Assert.Equal("image/jpeg", arquivo.Content.Headers.ContentType!.MediaType);
        Assert.True(File.Exists(Path.Combine(factory.PastaDeUploads, mesaId.ToString("N"), $"{jpegDisfarcado.Id:N}.jpg")));
    }

    [Fact]
    public async Task GifAnimado_EAceito_EGuardadoComTodosOsQuadros()
    {
        var (mestre, mesaId) = await CriarMesaAsync();

        var gif = await mestre.CriarImagemAsync(mesaId, ImagensDeTeste.GifAnimado(quadros: 3, largura: 40, altura: 30), "tocha.gif");

        Assert.Equal(("image/gif", 40, 30), (gif.ContentType, gif.Largura, gif.Altura));
        var arquivo = await mestre.Client.GetAsync($"/api/imagens/{gif.Id}");
        Assert.Equal("image/gif", arquivo.Content.Headers.ContentType!.MediaType);
        using var guardada = Image.Load(await arquivo.Content.ReadAsByteArrayAsync());
        Assert.Equal(3, guardada.Frames.Count);
        var miniatura = await mestre.Client.GetAsync($"/api/imagens/{gif.Id}/miniatura");
        Assert.Equal("image/webp", miniatura.Content.Headers.ContentType!.MediaType);
        Assert.True(File.Exists(Path.Combine(factory.PastaDeUploads, mesaId.ToString("N"), $"{gif.Id:N}.gif")));
    }

    [Fact]
    public async Task SemLimitePadrao_ArquivoGrandeELargo_EAceito()
    {
        var (mestre, mesaId) = await CriarMesaAsync();
        var acimaDe5Mb = new byte[5 * 1024 * 1024 + 1];
        ImagensDeTeste.Png().CopyTo(acimaDe5Mb, 0);

        var grande = await mestre.EnviarImagemAsync(mesaId, acimaDe5Mb, "enorme.png");
        var larga = await mestre.EnviarImagemAsync(mesaId, ImagensDeTeste.Png(4097, 8), "larga.png");

        Assert.Equal(HttpStatusCode.Created, grande.StatusCode);
        Assert.Equal(HttpStatusCode.Created, larga.StatusCode);
    }

    [Fact]
    public async Task Reprocessamento_RemoveExif_EDescartaDadosEscondidosAposAImagem()
    {
        var (mestre, mesaId) = await CriarMesaAsync();
        var carga = Encoding.ASCII.GetBytes("<?php system($_GET['c']); ?>");
        var pngComCarga = ImagensDeTeste.Png().Concat(carga).ToArray();

        var foto = await mestre.CriarImagemAsync(mesaId, ImagensDeTeste.JpegComExif(), "foto.jpg");
        var poliglota = await mestre.CriarImagemAsync(mesaId, pngComCarga, "poliglota.png");

        var bytesDaFoto = await mestre.Client.GetByteArrayAsync($"/api/imagens/{foto.Id}");
        using var decodificada = Image.Load(bytesDaFoto);
        Assert.Null(decodificada.Metadata.ExifProfile);
        Assert.DoesNotContain("CameraSecreta", Encoding.Latin1.GetString(bytesDaFoto));

        var bytesDoPng = await mestre.Client.GetByteArrayAsync($"/api/imagens/{poliglota.Id}");
        Assert.DoesNotContain("<?php", Encoding.Latin1.GetString(bytesDoPng));
    }

    [Fact]
    public async Task NomeDoCliente_NuncaEntraNoCaminho_EESanitizadoParaExibicao()
    {
        var (mestre, mesaId) = await CriarMesaAsync();

        var imagem = await mestre.CriarImagemAsync(mesaId, nome: "..\\..\\etc/passwd<script>.png");

        Assert.Equal("passwdscript.png", imagem.NomeOriginal);
        var arquivos = Directory.GetFiles(factory.PastaDeUploads, "*", SearchOption.AllDirectories)
            .Where(caminho => caminho.Contains(imagem.Id.ToString("N")))
            .Select(Path.GetFileName)
            .Order()
            .ToList();
        Assert.Equal([$"{imagem.Id:N}.mini.webp", $"{imagem.Id:N}.png"], arquivos);
        Assert.All(
            Directory.GetFiles(factory.PastaDeUploads, "*", SearchOption.AllDirectories),
            caminho => Assert.DoesNotContain("passwd", caminho));
    }

    [Fact]
    public async Task Jogador_NaoEnviaNaoListaNemExclui_MasBaixa_ENaoMembroNaoBaixa()
    {
        var (mestre, mesaId) = await CriarMesaAsync();
        var (jogador, _) = await factory.AdicionarJogadorAsync(mestre, mesaId);
        var estranho = await factory.CriarSessaoAsync("Estranho");
        var anonimo = factory.CreateClient();
        var imagem = await mestre.CriarImagemAsync(mesaId);

        Assert.Equal(HttpStatusCode.Forbidden, (await jogador.EnviarImagemAsync(mesaId, ImagensDeTeste.Png())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await jogador.Client.GetAsync($"/api/mesas/{mesaId}/imagens")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await jogador.Client.DeleteAsync($"/api/imagens/{imagem.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await jogador.Client.GetAsync($"/api/imagens/{imagem.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await jogador.Client.GetAsync($"/api/imagens/{imagem.Id}/miniatura")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await estranho.EnviarImagemAsync(mesaId, ImagensDeTeste.Png())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await estranho.Client.GetAsync($"/api/imagens/{imagem.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await estranho.Client.GetAsync($"/api/imagens/{imagem.Id}/miniatura")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await estranho.Client.DeleteAsync($"/api/imagens/{imagem.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync($"/api/imagens/{imagem.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await mestre.Client.GetAsync($"/api/imagens/{imagem.Id}")).StatusCode);
    }

    [Fact]
    public async Task Download_ComOETagQueONavegadorJaTem_Responde304()
    {
        var (mestre, mesaId) = await CriarMesaAsync();
        var imagem = await mestre.CriarImagemAsync(mesaId);
        var primeira = await mestre.Client.GetAsync($"/api/imagens/{imagem.Id}");

        var pedido = new HttpRequestMessage(HttpMethod.Get, $"/api/imagens/{imagem.Id}");
        pedido.Headers.IfNoneMatch.Add(primeira.Headers.ETag!);
        var segunda = await mestre.Client.SendAsync(pedido);

        Assert.Equal(HttpStatusCode.NotModified, segunda.StatusCode);
        Assert.Empty(await segunda.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Galeria_ListaAsImagensDaMesa_ComOUsoDaCota()
    {
        var (mestre, mesaId) = await CriarMesaAsync();
        var primeira = await mestre.CriarImagemAsync(mesaId, nome: "a.png");
        var segunda = await mestre.CriarImagemAsync(mesaId, ImagensDeTeste.Webp(), "b.webp");

        var galeria = (await mestre.Client.GetFromJsonAsync<GaleriaDto>($"/api/mesas/{mesaId}/imagens", ApiTestHelpers.Json))!;

        Assert.Equal(new[] { primeira.Id, segunda.Id }.Order(), galeria.Imagens.Select(i => i.Id).Order());
        Assert.Equal(primeira.TamanhoBytes + segunda.TamanhoBytes, galeria.BytesUsados);
        Assert.Equal(((int?)null, (long?)null, (long?)null, 16384), (galeria.CotaImagens, galeria.CotaBytes, galeria.TamanhoMaximoBytes, galeria.DimensaoMaxima));
    }

    [Fact]
    public async Task ExcluirImagemEmUso_ERejeitado_ELivreSomeDoDisco()
    {
        var (mestre, mesaId) = await CriarMesaAsync();
        var mapa = await mestre.CriarMapaAsync(mesaId, "Caverna");
        var imagem = await mestre.CriarImagemAsync(mesaId);
        var caminho = Path.Combine(factory.PastaDeUploads, mesaId.ToString("N"), $"{imagem.Id:N}.png");

        (await mestre.Client.PutAsJsonAsync($"/api/mapas/{mapa.Id}/fundo", new FundoRequest(imagem.Id, 1, 0, 0))).EnsureSuccessStatusCode();
        var token = await CriarTokenComImagemAsync(mestre, mapa.Id, imagem.Id);
        var estrutura = await mestre.CriarEstruturaAsync(mapa.Id, ImagemTestHelpers.Estrutura(x: 3, y: 3, textura: imagem.Id));

        var emUso = await mestre.Client.DeleteAsync($"/api/imagens/{imagem.Id}");
        var motivo = (await emUso.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail;
        Assert.Equal(HttpStatusCode.Conflict, emUso.StatusCode);
        Assert.Contains("fundo do mapa \"Caverna\"", motivo);
        Assert.Contains("token \"Estátua\"", motivo);
        Assert.Contains("textura de 1 estrutura", motivo);
        Assert.True(File.Exists(caminho));

        // Depois de desvincular dos três lugares, a exclusão passa.
        (await mestre.Client.PutAsJsonAsync($"/api/mapas/{mapa.Id}/fundo", new FundoRequest(null, 1, 0, 0))).EnsureSuccessStatusCode();
        (await mestre.Client.PutAsJsonAsync($"/api/tokens/{token.Id}", new EditarTokenRequest("Estátua", 5, 5, "npc", null, null))).EnsureSuccessStatusCode();
        (await mestre.Client.DeleteAsync($"/api/estruturas/{estrutura.Id}")).EnsureSuccessStatusCode();
        var livre = await mestre.Client.DeleteAsync($"/api/imagens/{imagem.Id}");

        Assert.Equal(HttpStatusCode.NoContent, livre.StatusCode);
        Assert.False(File.Exists(caminho));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(caminho)!));
        Assert.Equal(HttpStatusCode.NotFound, (await mestre.Client.GetAsync($"/api/imagens/{imagem.Id}")).StatusCode);
    }

    [Fact]
    public async Task ImagemDeOutraMesa_NaoEAceitaEmFundoTokenNemEstrutura()
    {
        var (mestre, mesaId) = await CriarMesaAsync();
        var outraMesa = await mestre.CriarMesaAsync("Outra mesa");
        var deFora = await mestre.CriarImagemAsync(outraMesa.Id);
        var mapa = await mestre.CriarMapaAsync(mesaId);

        var fundo = await mestre.Client.PutAsJsonAsync($"/api/mapas/{mapa.Id}/fundo", new FundoRequest(deFora.Id, 1, 0, 0));
        var token = await mestre.Client.PostAsJsonAsync(
            $"/api/mapas/{mapa.Id}/tokens", new CriarTokenRequest(TipoToken.Npc, "Estátua", 5, 5, "npc", null, null, null, deFora.Id), ApiTestHelpers.Json);
        var estrutura = await mestre.PostarEstruturaAsync(mapa.Id, ImagemTestHelpers.Estrutura(textura: deFora.Id));
        var inexistente = await mestre.Client.PutAsJsonAsync($"/api/mapas/{mapa.Id}/fundo", new FundoRequest(Guid.NewGuid(), 1, 0, 0));

        Assert.Equal(HttpStatusCode.BadRequest, fundo.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, token.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, estrutura.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, inexistente.StatusCode);
    }

    [Fact]
    public async Task FundoDoMapa_GuardaImagemEscalaEDeslocamento_ESoOMestreAltera()
    {
        var (mestre, mesaId) = await CriarMesaAsync();
        var (jogador, _) = await factory.AdicionarJogadorAsync(mestre, mesaId);
        var mapa = (await mestre.CriarMapaAtivoAsync(mesaId)).Mapa;
        var imagem = await mestre.CriarImagemAsync(mesaId);

        var doJogador = await jogador.Client.PutAsJsonAsync($"/api/mapas/{mapa.Id}/fundo", new FundoRequest(imagem.Id, 1, 0, 0));
        var escalaInvalida = await mestre.Client.PutAsJsonAsync($"/api/mapas/{mapa.Id}/fundo", new FundoRequest(imagem.Id, 0, 0, 0));
        var resposta = await mestre.Client.PutAsJsonAsync($"/api/mapas/{mapa.Id}/fundo", new FundoRequest(imagem.Id, 1.5, -2.25, 0.5));
        // Editar nome e dimensões não mexe no fundo.
        (await mestre.Client.PutAsJsonAsync($"/api/mapas/{mapa.Id}", new MapaRequest("Renomeado", 12, 9, null))).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Forbidden, doJogador.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, escalaInvalida.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var visto = (await jogador.ObterEstadoDoMapaAsync(mapa.Id)).Mapa;
        Assert.Equal(("Renomeado", (Guid?)imagem.Id, 1.5, -2.25, 0.5), (visto.Nome, visto.ImagemFundoId, visto.FundoEscala, visto.FundoOffsetX, visto.FundoOffsetY));
    }

    [Fact]
    public async Task Token_GuardaAImagem_ADuplicataHerda_EEditarSemImagemARemove()
    {
        var (mestre, mesaId) = await CriarMesaAsync();
        var mapa = await mestre.CriarMapaAsync(mesaId);
        var imagem = await mestre.CriarImagemAsync(mesaId);

        var token = await CriarTokenComImagemAsync(mestre, mapa.Id, imagem.Id);
        var copia = (await (await mestre.Client.PostAsync($"/api/tokens/{token.Id}/duplicar", null)).Content.ReadFromJsonAsync<TokenDto>(ApiTestHelpers.Json))!;
        var semImagem = (await (await mestre.Client.PutAsJsonAsync(
            $"/api/tokens/{token.Id}", new EditarTokenRequest("Estátua", 5, 5, "npc", null, null))).Content.ReadFromJsonAsync<TokenDto>(ApiTestHelpers.Json))!;

        Assert.Equal(imagem.Id, token.ImagemId);
        Assert.Equal(imagem.Id, copia.ImagemId);
        Assert.Null(semImagem.ImagemId);
    }

    [Fact]
    public async Task ExcluirAMesa_ApagaAPastaDeImagensDela()
    {
        var (mestre, mesaId) = await CriarMesaAsync();
        var mapa = await mestre.CriarMapaAsync(mesaId);
        var imagem = await mestre.CriarImagemAsync(mesaId);
        (await mestre.Client.PutAsJsonAsync($"/api/mapas/{mapa.Id}/fundo", new FundoRequest(imagem.Id, 1, 0, 0))).EnsureSuccessStatusCode();
        var pasta = Path.Combine(factory.PastaDeUploads, mesaId.ToString("N"));
        Assert.True(Directory.Exists(pasta));

        var resposta = await mestre.Client.DeleteAsync($"/api/mesas/{mesaId}");

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        Assert.False(Directory.Exists(pasta));
    }

    private static async Task<TokenDto> CriarTokenComImagemAsync(Sessao mestre, Guid mapaId, Guid imagemId)
    {
        var resposta = await mestre.Client.PostAsJsonAsync(
            $"/api/mapas/{mapaId}/tokens", new CriarTokenRequest(TipoToken.Npc, "Estátua", 5, 5, "npc", null, null, null, imagemId), ApiTestHelpers.Json);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<TokenDto>(ApiTestHelpers.Json))!;
    }
}

public class CotaBaixaFactory : VttApiFactory
{
    protected override int? CotaImagensPorMesa => 2;

    protected override long? CotaBytesPorMesa => 6_000;
}

public class CotaDeImagensTests(CotaBaixaFactory factory) : IClassFixture<CotaBaixaFactory>
{
    [Fact]
    public async Task CotaDeQuantidade_BarraAImagemSeguinte_EExcluirLiberaEspaco()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var primeira = await mestre.CriarImagemAsync(mesa.Id);
        await mestre.CriarImagemAsync(mesa.Id);

        var terceira = await mestre.EnviarImagemAsync(mesa.Id, ImagensDeTeste.Png());
        (await mestre.Client.DeleteAsync($"/api/imagens/{primeira.Id}")).EnsureSuccessStatusCode();
        var depoisDeExcluir = await mestre.EnviarImagemAsync(mesa.Id, ImagensDeTeste.Png());

        Assert.Equal(HttpStatusCode.Conflict, terceira.StatusCode);
        Assert.Contains("já tem 2 imagens", (await terceira.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);
        Assert.Equal(HttpStatusCode.Created, depoisDeExcluir.StatusCode);
    }

    [Fact]
    public async Task CotaDeBytes_BarraAImagemQueEstouraria_ECadaMesaTemASua()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var outraMesa = await mestre.CriarMesaAsync("Outra");
        await mestre.CriarImagemAsync(mesa.Id);

        var pesada = await mestre.EnviarImagemAsync(mesa.Id, ImagensDeTeste.PngPesado());
        var naOutraMesa = await mestre.EnviarImagemAsync(outraMesa.Id, ImagensDeTeste.Png());

        Assert.Equal(HttpStatusCode.Conflict, pesada.StatusCode);
        Assert.Contains("passaria do limite", (await pesada.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);
        Assert.Equal(HttpStatusCode.Created, naOutraMesa.StatusCode);
        Assert.Single(Directory.GetFiles(Path.Combine(factory.PastaDeUploads, mesa.Id.ToString("N")), "*.png"));
    }
}

public class UploadLimitadoFactory : VttApiFactory
{
    public const int Limite = 2;

    protected override int? UploadsPorMinuto => Limite;
}

public class UploadLimitadoTests(UploadLimitadoFactory factory) : IClassFixture<UploadLimitadoFactory>
{
    [Fact]
    public async Task Upload_AcimaDoLimitePorUsuario_Retorna429_SemAfetarOutroUsuario()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var outroMestre = await factory.CriarSessaoAsync();
        var outraMesa = await outroMestre.CriarMesaAsync();

        for (var envio = 0; envio < UploadLimitadoFactory.Limite; envio++)
        {
            Assert.Equal(HttpStatusCode.Created, (await mestre.EnviarImagemAsync(mesa.Id, ImagensDeTeste.Png())).StatusCode);
        }

        var bloqueado = await mestre.EnviarImagemAsync(mesa.Id, ImagensDeTeste.Png());
        var doOutro = await outroMestre.EnviarImagemAsync(outraMesa.Id, ImagensDeTeste.Png());

        Assert.Equal(HttpStatusCode.TooManyRequests, bloqueado.StatusCode);
        Assert.Equal(HttpStatusCode.Created, doOutro.StatusCode);
        // O limite é só do upload: o restante da API segue respondendo.
        Assert.Equal(HttpStatusCode.OK, (await mestre.Client.GetAsync($"/api/mesas/{mesa.Id}/imagens")).StatusCode);
    }
}

public class AnimacaoLimitadaFactory : VttApiFactory
{
    /// <summary>Cabem 2 quadros de 40 × 30.</summary>
    protected override long? PixelsMaximosDaAnimacao => 40 * 30 * 2;
}

public class AnimacaoLimitadaTests(AnimacaoLimitadaFactory factory) : IClassFixture<AnimacaoLimitadaFactory>
{
    [Fact]
    public async Task GifComPixelsDemaisNaSomaDosQuadros_ERejeitado_SemAfetarImagensParadas()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();

        var cabe = await mestre.EnviarImagemAsync(mesa.Id, ImagensDeTeste.GifAnimado(quadros: 2, largura: 40, altura: 30), "dois.gif");
        var naoCabe = await mestre.EnviarImagemAsync(mesa.Id, ImagensDeTeste.GifAnimado(quadros: 3, largura: 40, altura: 30), "tres.gif");
        // O limite é da animação: uma imagem parada maior que o orçamento continua passando.
        var parada = await mestre.EnviarImagemAsync(mesa.Id, ImagensDeTeste.Png(100, 100), "parada.png");

        Assert.Equal(HttpStatusCode.Created, cabe.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, naoCabe.StatusCode);
        var problema = (await naoCabe.Content.ReadFromJsonAsync<ValidationProblemDetails>())!;
        var detalhe = string.Join(" ", problema.Errors.SelectMany(erro => erro.Value));
        Assert.Contains("3 quadros de 40 × 30 pixels; nesse tamanho, o máximo é 2 quadros", detalhe);
        Assert.Equal(HttpStatusCode.Created, parada.StatusCode);
    }
}

public class ArquivoLimitadoFactory : VttApiFactory
{
    protected override long? TamanhoMaximoBytes => 5L * 1024 * 1024;

    protected override int? DimensaoMaxima => 4096;
}

public class ArquivoLimitadoTests(ArquivoLimitadoFactory factory) : IClassFixture<ArquivoLimitadoFactory>
{
    [Fact]
    public async Task ArquivoAcimaDoLimite_OuComDimensoesAcimaDoMaximo_ERejeitado()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesaId = (await mestre.CriarMesaAsync()).Id;
        var acimaDe5Mb = new byte[5 * 1024 * 1024 + 1];
        ImagensDeTeste.Png().CopyTo(acimaDe5Mb, 0);

        var grande = await mestre.EnviarImagemAsync(mesaId, acimaDe5Mb, "enorme.png");
        var larga = await mestre.EnviarImagemAsync(mesaId, ImagensDeTeste.Png(4097, 8), "larga.png");
        var noLimite = await mestre.EnviarImagemAsync(mesaId, ImagensDeTeste.Png(4096, 8), "limite.png");

        Assert.Equal(HttpStatusCode.BadRequest, grande.StatusCode);
        Assert.Contains("limite de 5 MB", await ImagensTests.DetalheAsync(grande));
        Assert.Equal(HttpStatusCode.BadRequest, larga.StatusCode);
        Assert.Contains("4097 × 8", await ImagensTests.DetalheAsync(larga));
        Assert.Equal(HttpStatusCode.Created, noLimite.StatusCode);
    }
}
