using System.Net;
using System.Net.Http.Json;
using Vtt.Application.Mapas;

namespace Vtt.Api.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class MapasTests(VttApiFactory factory)
{
    private static readonly MapaRequest Valido = new("Floresta", 12, 9, "#223344");

    [Fact]
    public async Task Mestre_CriaMapaInativo_ComOsDadosNormalizados()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();

        var resposta = await mestre.Client.PostAsJsonAsync($"/api/mesas/{mesa.Id}/mapas", new MapaRequest("  Floresta  ", 12, 9, "#aabbcc"));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var mapa = (await resposta.Content.ReadFromJsonAsync<MapaDto>(ApiTestHelpers.Json))!;
        Assert.Equal("Floresta", mapa.Nome);
        Assert.Equal((12, 9), (mapa.Largura, mapa.Altura));
        Assert.Equal("#AABBCC", mapa.CorFundo);
        Assert.False(mapa.Ativo);
    }

    [Theory]
    [InlineData(4, 10)]
    [InlineData(10, 101)]
    public async Task CriarMapa_ComDimensoesForaDaFaixa_Retorna400(int largura, int altura)
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();

        var resposta = await mestre.Client.PostAsJsonAsync($"/api/mesas/{mesa.Id}/mapas", new MapaRequest("Mapa", largura, altura, null));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Jogador_NaoCriaEditaExcluiNemAtiva_ENaoMembroNaoEnxerga()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (jogador, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id);
        var estranho = await factory.CriarSessaoAsync();
        var mapa = await mestre.CriarMapaAsync(mesa.Id);

        Assert.Equal(HttpStatusCode.Forbidden, (await jogador.Client.PostAsJsonAsync($"/api/mesas/{mesa.Id}/mapas", Valido)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await jogador.Client.PutAsJsonAsync($"/api/mapas/{mapa.Id}", Valido)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await jogador.Client.PutAsync($"/api/mapas/{mapa.Id}/ativar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await jogador.Client.DeleteAsync($"/api/mapas/{mapa.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await estranho.Client.PostAsJsonAsync($"/api/mesas/{mesa.Id}/mapas", Valido)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await estranho.Client.GetAsync($"/api/mesas/{mesa.Id}/mapas")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await estranho.Client.PutAsync($"/api/mapas/{mapa.Id}/ativar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await estranho.Client.GetAsync($"/api/mapas/{mapa.Id}/estado")).StatusCode);
    }

    [Fact]
    public async Task MestreVeTodosOsMapas_JogadorVeSoOAtivo()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (jogador, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id);
        var preparado = await mestre.CriarMapaAsync(mesa.Id, "Emboscada");
        var ativo = await mestre.CriarMapaAsync(mesa.Id, "Taverna");

        var antesDeAtivar = await jogador.Client.GetFromJsonAsync<List<MapaDto>>($"/api/mesas/{mesa.Id}/mapas", ApiTestHelpers.Json);
        await mestre.AtivarMapaAsync(ativo.Id);
        var doMestre = await mestre.Client.GetFromJsonAsync<List<MapaDto>>($"/api/mesas/{mesa.Id}/mapas", ApiTestHelpers.Json);
        var doJogador = await jogador.Client.GetFromJsonAsync<List<MapaDto>>($"/api/mesas/{mesa.Id}/mapas", ApiTestHelpers.Json);

        Assert.Empty(antesDeAtivar!);
        Assert.Equal(["Emboscada", "Taverna"], doMestre!.Select(m => m.Nome));
        Assert.Equal("Taverna", Assert.Single(doJogador!).Nome);

        // O estado de um mapa em preparação não existe para o jogador.
        Assert.Equal(HttpStatusCode.NotFound, (await jogador.Client.GetAsync($"/api/mapas/{preparado.Id}/estado")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await jogador.Client.GetAsync($"/api/mapas/{ativo.Id}/estado")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await mestre.Client.GetAsync($"/api/mapas/{preparado.Id}/estado")).StatusCode);
    }

    [Fact]
    public async Task AtivarMapa_DesativaOAnterior_EEhIdempotente()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var primeiro = await mestre.CriarMapaAsync(mesa.Id, "Primeiro");
        var segundo = await mestre.CriarMapaAsync(mesa.Id, "Segundo");

        await mestre.AtivarMapaAsync(primeiro.Id);
        await mestre.AtivarMapaAsync(segundo.Id);
        var repetido = await mestre.AtivarMapaAsync(segundo.Id);

        var mapas = await mestre.Client.GetFromJsonAsync<List<MapaDto>>($"/api/mesas/{mesa.Id}/mapas", ApiTestHelpers.Json);
        Assert.True(repetido.Mapa.Ativo);
        Assert.Equal("Segundo", Assert.Single(mapas!, m => m.Ativo).Nome);
    }

    [Fact]
    public async Task ReduzirDimensoes_ComTokenForaDosNovosLimites_Retorna409()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var mapa = await mestre.CriarMapaAsync(mesa.Id, largura: 10, altura: 8);
        await mestre.CriarTokenAsync(mapa.Id, x: 9, y: 2);

        var reduzindo = await mestre.Client.PutAsJsonAsync($"/api/mapas/{mapa.Id}", new MapaRequest("Caverna", 9, 8, null));
        var mantendoOToken = await mestre.Client.PutAsJsonAsync($"/api/mapas/{mapa.Id}", new MapaRequest("Gruta", 10, 5, null));

        Assert.Equal(HttpStatusCode.Conflict, reduzindo.StatusCode);
        Assert.Equal(HttpStatusCode.OK, mantendoOToken.StatusCode);
        var estado = await mestre.ObterEstadoDoMapaAsync(mapa.Id);
        Assert.Equal(("Gruta", 10, 5), (estado.Mapa.Nome, estado.Mapa.Largura, estado.Mapa.Altura));
    }

    [Fact]
    public async Task ExcluirMapaAtivo_LevaOsTokens_EDeixaAMesaSemMapaAtivo()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var estado = await mestre.CriarMapaAtivoAsync(mesa.Id);
        await mestre.CriarTokenAsync(estado.Mapa.Id);

        var resposta = await mestre.Client.DeleteAsync($"/api/mapas/{estado.Mapa.Id}");

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        Assert.Empty((await mestre.Client.GetFromJsonAsync<List<MapaDto>>($"/api/mesas/{mesa.Id}/mapas", ApiTestHelpers.Json))!);
        Assert.Equal(HttpStatusCode.NotFound, (await mestre.Client.GetAsync($"/api/mapas/{estado.Mapa.Id}/estado")).StatusCode);
    }
}
