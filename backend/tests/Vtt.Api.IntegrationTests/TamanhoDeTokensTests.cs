using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Vtt.Application.Biblioteca;
using Vtt.Application.Mapas;
using Vtt.Application.Personagens;
using Vtt.Domain.Mapas;
using Vtt.Domain.Personagens;
using static Vtt.Api.IntegrationTests.ImagemTestHelpers;

namespace Vtt.Api.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class TamanhoDeTokensTests(VttApiFactory factory)
{
    private static int Lado(TokenDto token) => Tamanhos.Celulas(token.Tamanho, token.LadoGargantuesco);

    private static bool Sobrepoem(TokenDto a, TokenDto b) => Tamanhos.Sobrepoem(a.X, a.Y, Lado(a), b.X, b.Y, Lado(b));

    private static Task<HttpResponseMessage> PostarAsync(
        Sessao mestre, Guid mapaId, TamanhoDeCriatura? tamanho, int? x = null, int? y = null, int? lado = null) =>
        mestre.Client.PostAsJsonAsync(
            $"/api/mapas/{mapaId}/tokens",
            new CriarTokenRequest(TipoToken.Inimigo, "Ogro", 30, 30, "goblin", "#AA3322", x, y, Tamanho: tamanho, LadoGargantuesco: lado),
            ApiTestHelpers.Json);

    private static async Task<TokenDto> CriarAsync(Sessao mestre, Guid mapaId, TamanhoDeCriatura? tamanho, int? x = null, int? y = null)
    {
        var resposta = await PostarAsync(mestre, mapaId, tamanho, x, y);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<TokenDto>(ApiTestHelpers.Json))!;
    }

    private static Task<HttpResponseMessage> EditarAsync(Sessao mestre, TokenDto token, TamanhoDeCriatura tamanho, int? lado = null) =>
        mestre.Client.PutAsJsonAsync(
            $"/api/tokens/{token.Id}",
            new EditarTokenRequest(token.Nome, 30, 30, token.Icone, token.CorBorda, null, Tamanho: tamanho, LadoGargantuesco: lado),
            ApiTestHelpers.Json);

    /// <summary>Mesa só com o Mestre (sem tokens de jogador) e um mapa ativo de 10x8.</summary>
    private async Task<(Sessao Mestre, Guid MesaId, Guid MapaId)> MontarAsync()
    {
        var mestre = await factory.CriarSessaoAsync("Mestre");
        var mesa = await mestre.CriarMesaAsync();
        var estado = await mestre.CriarMapaAtivoAsync(mesa.Id);
        return (mestre, mesa.Id, estado.Mapa.Id);
    }

    [Fact]
    public async Task SemTamanho_TokenPersonagemEModeloNascemMedios()
    {
        var (mestre, _, mapaId) = await MontarAsync();

        var token = await CriarAsync(mestre, mapaId, tamanho: null);
        var personagem = await mestre.CriarPersonagemAsync();
        var modelo = await mestre.CriarModeloAsync();
        var colocado = Assert.Single(await mestre.ColocarAsync(mapaId, modelo.Id));

        Assert.Equal((TamanhoDeCriatura.Medio, (int?)null), (token.Tamanho, token.LadoGargantuesco));
        Assert.Equal(TamanhoDeCriatura.Medio, personagem.Tamanho);
        Assert.Equal(TamanhoDeCriatura.Medio, modelo.Tamanho);
        Assert.Equal(TamanhoDeCriatura.Medio, colocado.Tamanho);
    }

    [Fact]
    public async Task Minusculo_EGravadoComoMinusculo_EOcupaUmaCelula()
    {
        var (mestre, _, mapaId) = await MontarAsync();

        var minusculo = await CriarAsync(mestre, mapaId, TamanhoDeCriatura.Minusculo, 2, 2);
        var vizinho = await PostarAsync(mestre, mapaId, null, 3, 2);
        var emCima = await PostarAsync(mestre, mapaId, null, 2, 2);

        Assert.Equal(TamanhoDeCriatura.Minusculo, (await mestre.ObterEstadoDoMapaAsync(mapaId)).Tokens.Single(t => t.Id == minusculo.Id).Tamanho);
        Assert.Equal(HttpStatusCode.Created, vizinho.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, emCima.StatusCode);
    }

    [Fact]
    public async Task Grande_OcupaQuatroCelulas_NaoSobrepoeOutroToken_NemSaiDoMapa()
    {
        var (mestre, mesaId, mapaId) = await MontarAsync();
        await CriarAsync(mestre, mapaId, null, 3, 3);

        // (2,2) cobriria (3,3); (9,7) passaria da borda de 10x8.
        var sobre = await PostarAsync(mestre, mapaId, TamanhoDeCriatura.Grande, 2, 2);
        var naBorda = await PostarAsync(mestre, mapaId, TamanhoDeCriatura.Grande, 9, 7);
        var grande = await CriarAsync(mestre, mapaId, TamanhoDeCriatura.Grande, 5, 5);
        var aoLado = await PostarAsync(mestre, mapaId, null, 6, 6);

        Assert.Equal(HttpStatusCode.Conflict, sobre.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, naBorda.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, aoLado.StatusCode);

        var (conexao, _) = await factory.EntrarNaMesaAsync(mestre, mesaId);
        await using var _1 = conexao;
        var contraOOutro = await conexao.MoverTokenAsync(grande, 2, 3);
        var paraFora = await conexao.MoverTokenAsync(grande, 9, 0);
        var livre = await conexao.MoverTokenAsync(grande, 0, 0);

        Assert.False(contraOOutro.Aceito);
        Assert.False(paraFora.Aceito);
        Assert.True(livre.Aceito, livre.Motivo);
    }

    [Fact]
    public async Task PorBlocos_GrandeNaoPassaPorCorredorDeUmaCelula_EOMedioPassa()
    {
        var (mestre, mesaId, mapaId) = await MontarAsync();

        // Uma parede em x = 3 de alto a baixo, com uma passagem só em (3,4).
        await mestre.CriarEstruturaAsync(mapaId, Estrutura(x: 3, y: 0, profundidade: 4));
        await mestre.CriarEstruturaAsync(mapaId, Estrutura(x: 3, y: 5, profundidade: 3));
        var medio = await CriarAsync(mestre, mapaId, null, 0, 0);
        var grande = await CriarAsync(mestre, mapaId, TamanhoDeCriatura.Grande, 0, 5);

        var (conexao, _) = await factory.EntrarNaMesaAsync(mestre, mesaId);
        await using var _1 = conexao;
        var medioAtravessa = await conexao.MoverPorBlocosAsync(medio, 4, 4);
        // Do outro lado da parede, (5,5) é livre e está a 5 passos: só falta a passagem caber.
        var grandeAtravessa = await conexao.MoverPorBlocosAsync(grande, 5, 5);
        var grandeDoLadoDeCa = await conexao.MoverPorBlocosAsync(grande, 1, 2);

        Assert.True(medioAtravessa.Aceito, medioAtravessa.Motivo);
        Assert.False(grandeAtravessa.Aceito);
        Assert.Contains("Fora do alcance", grandeAtravessa.Motivo);
        Assert.True(grandeDoLadoDeCa.Aceito, grandeDoLadoDeCa.Motivo);
    }

    [Fact]
    public async Task PorBlocos_GrandeNaoParaComOEspacoSobreUmaEstrutura()
    {
        var (mestre, mesaId, mapaId) = await MontarAsync();
        await mestre.CriarEstruturaAsync(mapaId, Estrutura(x: 4, y: 1));
        var grande = await CriarAsync(mestre, mapaId, TamanhoDeCriatura.Grande, 0, 0);

        var (conexao, _) = await factory.EntrarNaMesaAsync(mestre, mesaId);
        await using var _1 = conexao;

        // A âncora (3,0) é livre, mas o espaço 2x2 cobriria a parede em (4,1).
        var resultado = await conexao.MoverPorBlocosAsync(grande, 3, 0);

        Assert.False(resultado.Aceito);
        Assert.Equal((0, 0), (resultado.Token!.X, resultado.Token.Y));
    }

    [Fact]
    public async Task Editar_ParaEnormeNoCanto_VaiParaOEspacoLivreMaisProximo()
    {
        var (mestre, _, mapaId) = await MontarAsync();
        var goblin = await CriarAsync(mestre, mapaId, null, 9, 7);

        var resposta = await EditarAsync(mestre, goblin, TamanhoDeCriatura.Enorme);
        resposta.EnsureSuccessStatusCode();
        var enorme = (await resposta.Content.ReadFromJsonAsync<TokenDto>(ApiTestHelpers.Json))!;

        Assert.Equal(TamanhoDeCriatura.Enorme, enorme.Tamanho);
        Assert.Equal((7, 5), (enorme.X, enorme.Y));
    }

    [Fact]
    public async Task Gargantuesco_TemLadoConfiguravel_DentroDosLimites()
    {
        var (mestre, _, mapaId) = await MontarAsync();

        var padrao = await CriarAsync(mestre, mapaId, TamanhoDeCriatura.Gargantuesco, 0, 0);
        var pequenoDemais = await PostarAsync(mestre, mapaId, TamanhoDeCriatura.Gargantuesco, 5, 0, lado: 3);
        var grandeDemais = await PostarAsync(mestre, mapaId, TamanhoDeCriatura.Gargantuesco, 5, 0, lado: 11);
        var maiorQueOMapa = await EditarAsync(mestre, padrao, TamanhoDeCriatura.Gargantuesco, lado: 10);
        var seis = await EditarAsync(mestre, padrao, TamanhoDeCriatura.Gargantuesco, lado: 6);
        var medio = await EditarAsync(mestre, padrao, TamanhoDeCriatura.Medio, lado: 6);

        Assert.Equal((TamanhoDeCriatura.Gargantuesco, (int?)4), (padrao.Tamanho, padrao.LadoGargantuesco));
        Assert.Equal(HttpStatusCode.BadRequest, pequenoDemais.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, grandeDemais.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, maiorQueOMapa.StatusCode);
        Assert.Equal((int?)6, (await seis.Content.ReadFromJsonAsync<TokenDto>(ApiTestHelpers.Json))!.LadoGargantuesco);

        // Fora do Gargantuesco, o lado não é guardado.
        Assert.Equal((int?)null, (await medio.Content.ReadFromJsonAsync<TokenDto>(ApiTestHelpers.Json))!.LadoGargantuesco);
    }

    [Fact]
    public async Task Gargantuesco_NaoCabeNoMapaCheio_ERecusado()
    {
        var mestre = await factory.CriarSessaoAsync("Mestre");
        var mesa = await mestre.CriarMesaAsync();
        var mapaId = (await mestre.CriarMapaAtivoAsync(mesa.Id, largura: 5, altura: 5)).Mapa.Id;
        await CriarAsync(mestre, mapaId, null, 2, 2);
        var goblin = await CriarAsync(mestre, mapaId, null, 0, 0);

        var resposta = await EditarAsync(mestre, goblin, TamanhoDeCriatura.Gargantuesco);

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }

    [Fact]
    public async Task FichaDoPersonagem_MudaOTamanhoDoToken_EOAcomodaSemSobrepor()
    {
        var mestre = await factory.CriarSessaoAsync("Mestre");
        var mesa = await mestre.CriarMesaAsync();
        var (alice, personagem) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Alice");
        var estado = await mestre.CriarMapaAtivoAsync(mesa.Id);
        Assert.Equal((0, 0), (estado.TokenDe(alice)!.X, estado.TokenDe(alice)!.Y));
        var goblin = await CriarAsync(mestre, estado.Mapa.Id, null, 1, 1);

        var resposta = await alice.Client.PutAsJsonAsync(
            $"/api/personagens/{personagem.Id}",
            new PersonagemRequest(personagem.Nome, personagem.Classe, personagem.Nivel, personagem.HpMaximo, null, Tamanho: TamanhoDeCriatura.Grande),
            ApiTestHelpers.Json);
        resposta.EnsureSuccessStatusCode();

        var daAlice = (await mestre.ObterEstadoDoMapaAsync(estado.Mapa.Id)).TokenDe(alice)!;
        Assert.Equal(TamanhoDeCriatura.Grande, (await resposta.Content.ReadFromJsonAsync<PersonagemDto>(ApiTestHelpers.Json))!.Tamanho);
        Assert.Equal(TamanhoDeCriatura.Grande, daAlice.Tamanho);
        Assert.False(Sobrepoem(daAlice, goblin));
    }

    [Fact]
    public async Task Spawn_DeUmPersonagemGrande_NaoSobrepoeOutroJogador()
    {
        var mestre = await factory.CriarSessaoAsync("Mestre");
        var mesa = await mestre.CriarMesaAsync();
        var alice = await factory.CriarSessaoAsync("Alice");
        var resposta = await alice.Client.PostAsJsonAsync(
            "/api/personagens",
            new PersonagemRequest("Grok", ClasseDnd.Barbaro, 3, 30, null, Tamanho: TamanhoDeCriatura.Grande),
            ApiTestHelpers.Json);
        resposta.EnsureSuccessStatusCode();
        var grok = (await resposta.Content.ReadFromJsonAsync<PersonagemDto>(ApiTestHelpers.Json))!;
        var convite = await mestre.GerarConviteAsync(mesa.Id);
        (await alice.AceitarConviteAsync(convite.Codigo, grok.Id)).EnsureSuccessStatusCode();
        var (bob, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Bob");

        var estado = await mestre.CriarMapaAtivoAsync(mesa.Id);

        var daAlice = estado.TokenDe(alice)!;
        var doBob = estado.TokenDe(bob)!;
        Assert.Equal(TamanhoDeCriatura.Grande, daAlice.Tamanho);
        Assert.False(Sobrepoem(daAlice, doBob));
    }

    [Fact]
    public async Task Interacao_PodeSerExaminadaDeQualquerCelulaDeUmTokenGrande()
    {
        var mestre = await factory.CriarSessaoAsync("Mestre");
        var mesa = await mestre.CriarMesaAsync();
        var (alice, personagem) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Alice");
        (await alice.Client.PutAsJsonAsync(
            $"/api/personagens/{personagem.Id}",
            new PersonagemRequest(personagem.Nome, personagem.Classe, personagem.Nivel, personagem.HpMaximo, null, Tamanho: TamanhoDeCriatura.Grande),
            ApiTestHelpers.Json)).EnsureSuccessStatusCode();
        var estado = await mestre.CriarMapaAtivoAsync(mesa.Id);
        var modelo = await mestre.CriarModeloAsync(BibliotecaTestHelpers.Modelo("Carta", TipoToken.Interacao, "pergaminho", hp: 1));
        var interacao = Assert.Single(await mestre.ColocarAsync(estado.Mapa.Id, modelo.Id));
        var (conexao, _) = await factory.EntrarNaMesaAsync(mestre, mesa.Id);
        await using var _1 = conexao;

        // A Alice ocupa (0..1, 0..1): em (2,1) a carta encosta na célula (1,1), longe da âncora (0,0).
        var aoLado = await conexao.MoverTokenAsync(interacao, 2, 1);
        var exameDePerto = await alice.Client.GetAsync($"/api/tokens/{interacao.Id}/interacao");
        var longe = await conexao.MoverTokenAsync(aoLado.Token!, 3, 1);
        var exameDeLonge = await alice.Client.GetAsync($"/api/tokens/{interacao.Id}/interacao");

        Assert.Equal(TamanhoDeCriatura.Grande, estado.TokenDe(alice)!.Tamanho);
        Assert.True(aoLado.Aceito, aoLado.Motivo);
        Assert.True(longe.Aceito, longe.Motivo);
        Assert.Equal(HttpStatusCode.OK, exameDePerto.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, exameDeLonge.StatusCode);
    }

    [Fact]
    public async Task ReduzirOMapa_ContaOEspacoInteiroDoToken()
    {
        var (mestre, _, mapaId) = await MontarAsync();
        await CriarAsync(mestre, mapaId, TamanhoDeCriatura.Grande, 7, 0);

        // A âncora (7,0) cabe em 8 de largura, mas o espaço vai até a coluna 8.
        var resposta = await mestre.Client.PutAsJsonAsync($"/api/mapas/{mapaId}", new MapaRequest("Caverna", 8, 8, null), ApiTestHelpers.Json);

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }
}
