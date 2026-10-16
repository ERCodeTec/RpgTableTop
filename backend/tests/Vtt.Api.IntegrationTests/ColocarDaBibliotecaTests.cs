using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Vtt.Application.Imagens;
using Vtt.Domain.Mapas;

namespace Vtt.Api.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class ColocarDaBibliotecaTests(VttApiFactory factory)
{
    private async Task<(Sessao Mestre, Guid MesaId, Guid MapaId)> CriarMapaAsync(int largura = 10, int altura = 8)
    {
        var mestre = await factory.CriarSessaoAsync("Mestre");
        var mesa = await mestre.CriarMesaAsync();
        var estado = await mestre.CriarMapaAtivoAsync(mesa.Id, largura, altura);
        return (mestre, mesa.Id, estado.Mapa.Id);
    }

    [Fact]
    public async Task Colocar_CriaTokensComOsDadosDoModelo_EHpCheio()
    {
        var (mestre, _, mapaId) = await CriarMapaAsync();
        var modelo = await mestre.CriarModeloAsync(BibliotecaTestHelpers.Modelo(
            "Taverneiro", TipoToken.Npc, "npc", 12, deslocamento: 5, visao: 3, exibicaoHp: ExibicaoHp.Oculto));

        var tokens = await mestre.ColocarAsync(mapaId, modelo.Id);

        var token = Assert.Single(tokens);
        Assert.Equal(("Taverneiro", TipoToken.Npc, "npc", 12, 12, "#AA3322", 5, 3, ExibicaoHp.Oculto, true),
            (token.Nome, token.Tipo, token.Icone, token.HpAtual, token.HpMaximo, token.CorBorda, token.Deslocamento, token.Visao, token.ExibicaoHp, token.Visivel));
        Assert.Equal((0, 0), (token.X, token.Y));
        Assert.Null(token.DonoUsuarioId);
        Assert.Single((await mestre.ObterEstadoDoMapaAsync(mapaId)).Tokens);
    }

    [Fact]
    public async Task VariasCopias_SaoNumeradas_EContinuamANumeracaoDoMapa()
    {
        var (mestre, _, mapaId) = await CriarMapaAsync();
        var modelo = await mestre.CriarModeloAsync();

        var primeiras = await mestre.ColocarAsync(mapaId, modelo.Id, 3);
        var proximas = await mestre.ColocarAsync(mapaId, modelo.Id, 2);

        Assert.Equal(["Goblin 1", "Goblin 2", "Goblin 3"], primeiras.Select(t => t.Nome));
        Assert.Equal(["Goblin 4", "Goblin 5"], proximas.Select(t => t.Nome));
        Assert.Equal(5, primeiras.Concat(proximas).Select(t => (t.X, t.Y)).Distinct().Count());
    }

    [Fact]
    public async Task NomeLongo_CortaParaCaberONumero()
    {
        var (mestre, _, mapaId) = await CriarMapaAsync();
        var nome = new string('x', Token.NomeTamanhoMaximo);
        var modelo = await mestre.CriarModeloAsync(BibliotecaTestHelpers.Modelo(nome));

        var tokens = await mestre.ColocarAsync(mapaId, modelo.Id, 20);

        Assert.All(tokens, token => Assert.True(token.Nome.Length <= Token.NomeTamanhoMaximo));
        Assert.EndsWith(" 20", tokens[^1].Nome);
    }

    [Fact]
    public async Task QuantidadeForaDaFaixa_EInvalida()
    {
        var (mestre, _, mapaId) = await CriarMapaAsync();
        var modelo = await mestre.CriarModeloAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await mestre.ColocarDaBibliotecaAsync(mapaId, modelo.Id, 0)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await mestre.ColocarDaBibliotecaAsync(mapaId, modelo.Id, 21)).StatusCode);
    }

    [Fact]
    public async Task MapaSemEspacoParaTodos_NaoCriaNenhum()
    {
        var (mestre, _, mapaId) = await CriarMapaAsync(largura: 5, altura: 5);
        var modelo = await mestre.CriarModeloAsync();
        await mestre.ColocarAsync(mapaId, modelo.Id, 20);

        var resposta = await mestre.ColocarDaBibliotecaAsync(mapaId, modelo.Id, 6);

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
        Assert.Contains("só tem 5 células livres", (await resposta.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);
        Assert.Equal(20, (await mestre.ObterEstadoDoMapaAsync(mapaId)).Tokens.Count);
    }

    [Fact]
    public async Task Jogador_ForaDaMesa_EModeloAlheio_SaoBarrados()
    {
        var (mestre, mesaId, mapaId) = await CriarMapaAsync();
        var (jogador, _) = await factory.AdicionarJogadorAsync(mestre, mesaId);
        var estranho = await factory.CriarSessaoAsync();
        var modeloDoMestre = await mestre.CriarModeloAsync();
        var modeloDoJogador = await jogador.CriarModeloAsync();
        var modeloDoEstranho = await estranho.CriarModeloAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await jogador.ColocarDaBibliotecaAsync(mapaId, modeloDoJogador.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await estranho.ColocarDaBibliotecaAsync(mapaId, modeloDoEstranho.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await mestre.ColocarDaBibliotecaAsync(mapaId, modeloDoEstranho.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await mestre.ColocarDaBibliotecaAsync(mapaId, modeloDoMestre.Id)).StatusCode);
    }

    [Fact]
    public async Task Imagem_ECopiadaUmaVezPorMesa_EOsMembrosBaixam()
    {
        var (mestre, mesaId, mapaId) = await CriarMapaAsync();
        var (jogador, _) = await factory.AdicionarJogadorAsync(mestre, mesaId);
        var outraMesa = await mestre.CriarMesaAsync("Outra");
        var outroMapa = await mestre.CriarMapaAsync(outraMesa.Id);
        var imagem = await mestre.CriarImagemPessoalAsync();
        var modelo = await mestre.CriarModeloAsync(BibliotecaTestHelpers.Modelo(imagemId: imagem.Id));

        var primeiros = await mestre.ColocarAsync(mapaId, modelo.Id, 3);
        var segundos = await mestre.ColocarAsync(mapaId, modelo.Id);
        var naOutraMesa = await mestre.ColocarAsync(outroMapa.Id, modelo.Id);
        var galeria = (await mestre.Client.GetFromJsonAsync<GaleriaDto>($"/api/mesas/{mesaId}/imagens", ApiTestHelpers.Json))!;

        var copia = Assert.Single(galeria.Imagens);
        Assert.NotEqual(imagem.Id, copia.Id);
        Assert.All(primeiros.Concat(segundos), token => Assert.Equal(copia.Id, token.ImagemId));
        Assert.NotEqual(copia.Id, Assert.Single(naOutraMesa).ImagemId);
        Assert.Equal(HttpStatusCode.OK, (await jogador.Client.GetAsync($"/api/imagens/{copia.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await jogador.Client.GetAsync($"/api/imagens/{imagem.Id}")).StatusCode);
    }

    [Fact]
    public async Task CopiaExcluidaDaMesa_ECopiadaDeNovo()
    {
        var (mestre, mesaId, mapaId) = await CriarMapaAsync();
        var imagem = await mestre.CriarImagemPessoalAsync();
        var modelo = await mestre.CriarModeloAsync(BibliotecaTestHelpers.Modelo(imagemId: imagem.Id));
        var token = Assert.Single(await mestre.ColocarAsync(mapaId, modelo.Id));
        (await mestre.Client.DeleteAsync($"/api/tokens/{token.Id}")).EnsureSuccessStatusCode();
        (await mestre.Client.DeleteAsync($"/api/imagens/{token.ImagemId}")).EnsureSuccessStatusCode();

        var denovo = Assert.Single(await mestre.ColocarAsync(mapaId, modelo.Id));
        var galeria = (await mestre.Client.GetFromJsonAsync<GaleriaDto>($"/api/mesas/{mesaId}/imagens", ApiTestHelpers.Json))!;

        Assert.Equal(Assert.Single(galeria.Imagens).Id, denovo.ImagemId);
        Assert.NotEqual(token.ImagemId, denovo.ImagemId);
    }

    [Fact]
    public async Task Ocultos_NaoChegamAoJogador()
    {
        var (mestre, mesaId, mapaId) = await CriarMapaAsync();
        var (jogador, _) = await factory.AdicionarJogadorAsync(mestre, mesaId);
        var modelo = await mestre.CriarModeloAsync();

        var tokens = await mestre.ColocarAsync(mapaId, modelo.Id, 2, visivel: false);
        var doJogador = await jogador.ObterEstadoDoMapaAsync(mapaId);

        Assert.All(tokens, token => Assert.False(token.Visivel));
        Assert.DoesNotContain(doJogador.Tokens, token => tokens.Any(t => t.Id == token.Id));
        Assert.Equal(3, (await mestre.ObterEstadoDoMapaAsync(mapaId)).Tokens.Count);
    }

    [Fact]
    public async Task EditarOuExcluirOModelo_NaoMudaOsTokens()
    {
        var (mestre, _, mapaId) = await CriarMapaAsync();
        var modelo = await mestre.CriarModeloAsync();
        var token = Assert.Single(await mestre.ColocarAsync(mapaId, modelo.Id));

        (await mestre.Client.PutAsJsonAsync($"/api/biblioteca/modelos/{modelo.Id}",
            BibliotecaTestHelpers.Modelo("Hobgoblin", hp: 30), ApiTestHelpers.Json)).EnsureSuccessStatusCode();
        var depoisDeEditar = (await mestre.ObterEstadoDoMapaAsync(mapaId)).Tokens.Single(t => t.Id == token.Id);
        (await mestre.Client.DeleteAsync($"/api/biblioteca/modelos/{modelo.Id}")).EnsureSuccessStatusCode();
        var depoisDeExcluir = (await mestre.ObterEstadoDoMapaAsync(mapaId)).Tokens.Single(t => t.Id == token.Id);

        Assert.Equal(("Goblin", 7), (depoisDeEditar.Nome, depoisDeEditar.HpMaximo));
        Assert.Equal(("Goblin", 7), (depoisDeExcluir.Nome, depoisDeExcluir.HpMaximo));
    }
}
