using System.Net;
using System.Net.Http.Json;
using SixLabors.ImageSharp;
using Vtt.Application.Imagens;
using Vtt.Application.Mapas;
using Vtt.Domain.Mapas;

namespace Vtt.Api.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class ImagensPessoaisTests(VttApiFactory factory)
{
    [Fact]
    public async Task Enviar_ListarEBaixar_SoParaODono()
    {
        var mestre = await factory.CriarSessaoAsync("Mestre");
        var outro = await factory.CriarSessaoAsync("Outro");

        var resposta = await mestre.EnviarImagemPessoalAsync(ImagensDeTeste.Png(300, 200), "Goblin Chefe.png");
        var imagem = (await resposta.Content.ReadFromJsonAsync<ImagemDto>(ApiTestHelpers.Json))!;
        var galeria = (await mestre.Client.GetFromJsonAsync<GaleriaDto>("/api/biblioteca/imagens", ApiTestHelpers.Json))!;
        var galeriaDoOutro = (await outro.Client.GetFromJsonAsync<GaleriaDto>("/api/biblioteca/imagens", ApiTestHelpers.Json))!;
        var arquivo = await mestre.Client.GetAsync($"/api/imagens/{imagem.Id}");
        var miniatura = await mestre.Client.GetAsync($"/api/imagens/{imagem.Id}/miniatura");

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.Null(imagem.MesaId);
        Assert.Equal("Goblin Chefe.png", imagem.NomeOriginal);
        Assert.Equal(imagem.Id, Assert.Single(galeria.Imagens).Id);
        Assert.Equal(imagem.TamanhoBytes, galeria.BytesUsados);
        Assert.Empty(galeriaDoOutro.Imagens);
        Assert.Equal(HttpStatusCode.OK, arquivo.StatusCode);
        using var pequena = Image.Load(await miniatura.Content.ReadAsByteArrayAsync());
        Assert.Equal(256, pequena.Width);
        Assert.True(File.Exists(Path.Combine(factory.PastaDeUploads, "usuarios", mestre.UsuarioId.ToString("N"), $"{imagem.Id:N}.mini.webp")));

        Assert.Equal(HttpStatusCode.NotFound, (await outro.Client.GetAsync($"/api/imagens/{imagem.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outro.Client.GetAsync($"/api/imagens/{imagem.Id}/miniatura")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outro.Client.DeleteAsync($"/api/imagens/{imagem.Id}")).StatusCode);
    }

    [Fact]
    public async Task Dono_ExcluiImagemPessoal()
    {
        var mestre = await factory.CriarSessaoAsync("Mestre");
        var imagem = await mestre.CriarImagemPessoalAsync();

        var excluir = await mestre.Client.DeleteAsync($"/api/imagens/{imagem.Id}");

        Assert.Equal(HttpStatusCode.NoContent, excluir.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await mestre.Client.GetAsync($"/api/imagens/{imagem.Id}")).StatusCode);
        Assert.False(File.Exists(Path.Combine(factory.PastaDeUploads, "usuarios", mestre.UsuarioId.ToString("N"), $"{imagem.Id:N}.mini.webp")));
    }

    [Fact]
    public async Task ImagemPessoal_NaoServeDiretoParaTokenNemFundo()
    {
        var mestre = await factory.CriarSessaoAsync("Mestre");
        var mesa = await mestre.CriarMesaAsync();
        var mapa = await mestre.CriarMapaAsync(mesa.Id);
        var imagem = await mestre.CriarImagemPessoalAsync();

        var token = await mestre.Client.PostAsJsonAsync(
            $"/api/mapas/{mapa.Id}/tokens", new CriarTokenRequest(TipoToken.Npc, "Estátua", 5, 5, "npc", null, null, null, imagem.Id), ApiTestHelpers.Json);
        var fundo = await mestre.Client.PutAsJsonAsync($"/api/mapas/{mapa.Id}/fundo", new FundoRequest(imagem.Id, 1, 0, 0), ApiTestHelpers.Json);

        Assert.Equal(HttpStatusCode.BadRequest, token.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, fundo.StatusCode);
    }

    [Fact]
    public async Task ImagemDeMesa_ContinuaNoMesmoCaminho()
    {
        var mestre = await factory.CriarSessaoAsync("Mestre");
        var mesa = await mestre.CriarMesaAsync();

        var imagem = await mestre.CriarImagemAsync(mesa.Id);

        Assert.Equal(mesa.Id, imagem.MesaId);
        Assert.True(File.Exists(Path.Combine(factory.PastaDeUploads, mesa.Id.ToString("N"), $"{imagem.Id:N}.mini.webp")));
        Assert.True(File.Exists(Path.Combine(factory.PastaDeUploads, mesa.Id.ToString("N"), $"{imagem.Id:N}.png")));
    }
}

public class CotaPessoalBaixaFactory : VttApiFactory
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Uploads:CotaImagensPorUsuario", "1");
    }
}

public class CotaPessoalTests(CotaPessoalBaixaFactory factory) : IClassFixture<CotaPessoalBaixaFactory>
{
    [Fact]
    public async Task CotaPorUsuario_BarraAImagemSeguinte()
    {
        var mestre = await factory.CriarSessaoAsync();
        await mestre.CriarImagemPessoalAsync();

        var segunda = await mestre.EnviarImagemPessoalAsync();
        var deOutro = await (await factory.CriarSessaoAsync()).EnviarImagemPessoalAsync();

        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
        Assert.Equal(HttpStatusCode.Created, deOutro.StatusCode);
    }
}
