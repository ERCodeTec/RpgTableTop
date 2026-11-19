using System.Net;
using System.Net.Http.Json;
using Vtt.Application.Mapas;

namespace Vtt.Api.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class PastasDeMapasTests(VttApiFactory factory)
{
    private static async Task<PastaDeMapasDto> CriarPastaAsync(Sessao mestre, Guid mesaId, string nome)
    {
        var resposta = await mestre.Client.PostAsJsonAsync($"/api/mesas/{mesaId}/pastas-de-mapas", new PastaDeMapasRequest(nome));
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<PastaDeMapasDto>(ApiTestHelpers.Json))!;
    }

    private static Task<HttpResponseMessage> MoverAsync(Sessao sessao, Guid mapaId, Guid? pastaId) =>
        sessao.Client.PutAsJsonAsync($"/api/mapas/{mapaId}/pasta", new PastaDoMapaRequest(pastaId));

    private static Task<List<MapaDto>?> ListarMapasAsync(Sessao sessao, Guid mesaId) =>
        sessao.Client.GetFromJsonAsync<List<MapaDto>>($"/api/mesas/{mesaId}/mapas", ApiTestHelpers.Json);

    [Fact]
    public async Task Mestre_CriaPastas_ListadasEmOrdemAlfabetica_ComNomeAparado()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();

        var criada = await CriarPastaAsync(mestre, mesa.Id, "  Masmorras  ");
        await CriarPastaAsync(mestre, mesa.Id, "Cidades");

        Assert.Equal("Masmorras", criada.Nome);
        Assert.Equal(0, criada.QuantidadeDeMapas);
        var pastas = await mestre.Client.GetFromJsonAsync<List<PastaDeMapasDto>>($"/api/mesas/{mesa.Id}/pastas-de-mapas", ApiTestHelpers.Json);
        Assert.Equal(["Cidades", "Masmorras"], pastas!.Select(p => p.Nome));
    }

    [Fact]
    public async Task Cor_TemPadrao_EhNormalizada_ETrocaAoRenomear()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();

        var semCor = await CriarPastaAsync(mestre, mesa.Id, "Sem cor");
        var resposta = await mestre.Client.PostAsJsonAsync($"/api/mesas/{mesa.Id}/pastas-de-mapas", new PastaDeMapasRequest("Azul", "#3366cc"));
        var azul = (await resposta.Content.ReadFromJsonAsync<PastaDeMapasDto>(ApiTestHelpers.Json))!;
        var renomear = await mestre.Client.PutAsJsonAsync($"/api/pastas-de-mapas/{azul.Id}", new PastaDeMapasRequest("Verde", "#22aa44"));

        Assert.Equal("#E2B23C", semCor.Cor);
        Assert.Equal("#3366CC", azul.Cor);
        Assert.Equal("#22AA44", (await renomear.Content.ReadFromJsonAsync<PastaDeMapasDto>(ApiTestHelpers.Json))!.Cor);
        var pastas = await mestre.Client.GetFromJsonAsync<List<PastaDeMapasDto>>($"/api/mesas/{mesa.Id}/pastas-de-mapas", ApiTestHelpers.Json);
        Assert.Equal("#22AA44", pastas!.Single(p => p.Id == azul.Id).Cor);
    }

    [Theory]
    [InlineData("vermelho")]
    [InlineData("#12345")]
    public async Task CriarPasta_ComCorInvalida_Retorna400(string cor)
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();

        var resposta = await mestre.Client.PostAsJsonAsync($"/api/mesas/{mesa.Id}/pastas-de-mapas", new PastaDeMapasRequest("Pasta", cor));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345678901234567890123456789012345678901")]
    public async Task CriarPasta_ComNomeInvalido_Retorna400(string nome)
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();

        var resposta = await mestre.Client.PostAsJsonAsync($"/api/mesas/{mesa.Id}/pastas-de-mapas", new PastaDeMapasRequest(nome));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task NomeRepetido_SemDiferenciarMaiusculas_Retorna409_AoCriarERenomear()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        await CriarPastaAsync(mestre, mesa.Id, "Masmorra");
        var outra = await CriarPastaAsync(mestre, mesa.Id, "Floresta");

        var criar = await mestre.Client.PostAsJsonAsync($"/api/mesas/{mesa.Id}/pastas-de-mapas", new PastaDeMapasRequest("masmorra"));
        var renomear = await mestre.Client.PutAsJsonAsync($"/api/pastas-de-mapas/{outra.Id}", new PastaDeMapasRequest(" MASMORRA "));

        Assert.Equal(HttpStatusCode.Conflict, criar.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, renomear.StatusCode);
    }

    [Fact]
    public async Task MesmoNome_EmMesasDiferentes_EhPermitido_ERenomearParaOProprioNomeTambem()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesaA = await mestre.CriarMesaAsync("A");
        var mesaB = await mestre.CriarMesaAsync("B");
        var pasta = await CriarPastaAsync(mestre, mesaA.Id, "Masmorra");
        await CriarPastaAsync(mestre, mesaB.Id, "Masmorra");

        var renomear = await mestre.Client.PutAsJsonAsync($"/api/pastas-de-mapas/{pasta.Id}", new PastaDeMapasRequest("masmorra"));

        Assert.Equal(HttpStatusCode.OK, renomear.StatusCode);
        Assert.Equal("masmorra", (await renomear.Content.ReadFromJsonAsync<PastaDeMapasDto>(ApiTestHelpers.Json))!.Nome);
    }

    [Fact]
    public async Task MoverMapa_ParaPasta_EDeVolta_SemLimiteDeMapas()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var pasta = await CriarPastaAsync(mestre, mesa.Id, "Capítulo 1");
        var mapas = new List<MapaDto>();
        for (var i = 0; i < 3; i++)
        {
            mapas.Add(await mestre.CriarMapaAsync(mesa.Id, $"Mapa {i}"));
        }

        foreach (var mapa in mapas)
        {
            var resposta = await MoverAsync(mestre, mapa.Id, pasta.Id);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            Assert.Equal(pasta.Id, (await resposta.Content.ReadFromJsonAsync<MapaDto>(ApiTestHelpers.Json))!.PastaId);
        }

        var pastas = await mestre.Client.GetFromJsonAsync<List<PastaDeMapasDto>>($"/api/mesas/{mesa.Id}/pastas-de-mapas", ApiTestHelpers.Json);
        Assert.Equal(3, pastas!.Single().QuantidadeDeMapas);

        Assert.Equal(HttpStatusCode.OK, (await MoverAsync(mestre, mapas[0].Id, null)).StatusCode);
        var lista = await ListarMapasAsync(mestre, mesa.Id);
        Assert.Null(lista!.Single(m => m.Id == mapas[0].Id).PastaId);
        Assert.Equal(pasta.Id, lista!.Single(m => m.Id == mapas[1].Id).PastaId);
    }

    [Fact]
    public async Task MapaNovo_NasceSemPasta()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();

        var mapa = await mestre.CriarMapaAsync(mesa.Id);

        Assert.Null(mapa.PastaId);
    }

    [Fact]
    public async Task MoverMapa_ParaPastaDeOutraMesa_Retorna400()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync("A");
        var outraMesa = await mestre.CriarMesaAsync("B");
        var pastaDeFora = await CriarPastaAsync(mestre, outraMesa.Id, "Alheia");
        var mapa = await mestre.CriarMapaAsync(mesa.Id);

        var resposta = await MoverAsync(mestre, mapa.Id, pastaDeFora.Id);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task ExcluirPasta_DeixaOsMapasSemPasta()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var pasta = await CriarPastaAsync(mestre, mesa.Id, "Temporária");
        var mapa = await mestre.CriarMapaAsync(mesa.Id);
        await MoverAsync(mestre, mapa.Id, pasta.Id);

        var resposta = await mestre.Client.DeleteAsync($"/api/pastas-de-mapas/{pasta.Id}");

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        var lista = await ListarMapasAsync(mestre, mesa.Id);
        Assert.Null(lista!.Single().PastaId);
        var pastas = await mestre.Client.GetFromJsonAsync<List<PastaDeMapasDto>>($"/api/mesas/{mesa.Id}/pastas-de-mapas", ApiTestHelpers.Json);
        Assert.Empty(pastas!);
    }

    [Fact]
    public async Task Jogador_Recebe403_ENaoMembro_Recebe404()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (jogador, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id);
        var estranho = await factory.CriarSessaoAsync();
        var pasta = await CriarPastaAsync(mestre, mesa.Id, "Segredos");
        var mapa = await mestre.CriarMapaAsync(mesa.Id);
        var request = new PastaDeMapasRequest("Outra");

        Assert.Equal(HttpStatusCode.Forbidden, (await jogador.Client.GetAsync($"/api/mesas/{mesa.Id}/pastas-de-mapas")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await jogador.Client.PostAsJsonAsync($"/api/mesas/{mesa.Id}/pastas-de-mapas", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await jogador.Client.PutAsJsonAsync($"/api/pastas-de-mapas/{pasta.Id}", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await jogador.Client.DeleteAsync($"/api/pastas-de-mapas/{pasta.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await MoverAsync(jogador, mapa.Id, pasta.Id)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await estranho.Client.GetAsync($"/api/mesas/{mesa.Id}/pastas-de-mapas")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await estranho.Client.PostAsJsonAsync($"/api/mesas/{mesa.Id}/pastas-de-mapas", request)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await estranho.Client.PutAsJsonAsync($"/api/pastas-de-mapas/{pasta.Id}", request)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await estranho.Client.DeleteAsync($"/api/pastas-de-mapas/{pasta.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await MoverAsync(estranho, mapa.Id, pasta.Id)).StatusCode);
    }
}
