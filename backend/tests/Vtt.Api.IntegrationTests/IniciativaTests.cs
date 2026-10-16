using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Vtt.Api.Hubs;
using Vtt.Application.Iniciativas;
using Vtt.Application.Mapas;
using Vtt.Application.Mesas;
using Vtt.Domain.Mesas;

namespace Vtt.Api.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class IniciativaTests(VttApiFactory factory)
{
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Mesa com Mestre, Alice e Bob e um mapa ativo de 10x8. Os tokens dos jogadores nascem em
    /// (0,0) e (1,0); os goblins, numerados, na linha 5.
    /// </summary>
    private async Task<Cena> MontarAsync(int goblins = 3)
    {
        var mestre = await factory.CriarSessaoAsync("Mestre");
        var mesa = await mestre.CriarMesaAsync();
        var (alice, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Alice");
        var (bob, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Bob");
        var estado = await mestre.CriarMapaAtivoAsync(mesa.Id);
        var tokensDosGoblins = new List<TokenDto>();
        for (var i = 1; i <= goblins; i++)
        {
            tokensDosGoblins.Add(await mestre.CriarTokenAsync(estado.Mapa.Id, $"Goblin {i}", x: 4 + i, y: 5));
        }

        return new Cena(mestre, alice, bob, mesa.Id, estado.Mapa.Id, estado.TokenDe(alice)!, estado.TokenDe(bob)!, tokensDosGoblins);
    }

    private sealed record Cena(
        Sessao Mestre, Sessao Alice, Sessao Bob, Guid MesaId, Guid MapaId, TokenDto TokenDaAlice, TokenDto TokenDoBob, List<TokenDto> Goblins)
    {
        public string Url => $"/api/mesas/{MesaId}/iniciativa";
    }

    private static async Task<IniciativaDto> LerAsync(HttpResponseMessage resposta)
    {
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<IniciativaDto>(ApiTestHelpers.Json))!;
    }

    private static Task<HttpResponseMessage> SolicitarAsync(Cena cena) => cena.Mestre.Client.PostAsync($"{cena.Url}/solicitar", null);

    private static Task<HttpResponseMessage> InformarAsync(Cena cena, Sessao jogador, int valor) =>
        jogador.Client.PostAsJsonAsync($"{cena.Url}/minha", new ValorDeIniciativaRequest(valor), ApiTestHelpers.Json);

    private static Task<HttpResponseMessage> DefinirGrupoAsync(Cena cena, string grupo, int valor) =>
        cena.Mestre.Client.PutAsJsonAsync($"{cena.Url}/grupos", new ValorDeGrupoRequest(grupo, valor), ApiTestHelpers.Json);

    private static Task<HttpResponseMessage> IniciarAsync(Cena cena) => cena.Mestre.Client.PostAsync($"{cena.Url}/iniciar", null);

    private static Task<HttpResponseMessage> EncerrarVezAsync(Cena cena, Sessao quem, Guid tokenId) =>
        quem.Client.PostAsJsonAsync($"{cena.Url}/encerrar-vez", new EncerrarVezRequest(tokenId), ApiTestHelpers.Json);

    /// <summary>Alice 15, goblins 12 e Bob 10, já em combate.</summary>
    private static async Task<IniciativaDto> IniciarCombateAsync(Cena cena, int? bob = 10, int? goblins = 12)
    {
        (await SolicitarAsync(cena)).EnsureSuccessStatusCode();
        (await InformarAsync(cena, cena.Alice, 15)).EnsureSuccessStatusCode();
        if (bob is { } valorDoBob)
        {
            (await InformarAsync(cena, cena.Bob, valorDoBob)).EnsureSuccessStatusCode();
        }

        if (goblins is { } valorDosGoblins)
        {
            (await DefinirGrupoAsync(cena, "Goblin", valorDosGoblins)).EnsureSuccessStatusCode();
        }

        return await LerAsync(await IniciarAsync(cena));
    }

    private static async Task<TokenDto> AtualizarAsync(Sessao sessao, Guid mapaId, Guid tokenId) =>
        (await sessao.ObterEstadoDoMapaAsync(mapaId)).Tokens.Single(token => token.Id == tokenId);

    [Fact]
    public async Task Jogador_InformaSoUmaVez_ESoDepoisDoPedido()
    {
        var cena = await MontarAsync(goblins: 0);

        var antesDoPedido = await InformarAsync(cena, cena.Alice, 12);
        await SolicitarAsync(cena);
        var primeira = await InformarAsync(cena, cena.Alice, 12);
        var segunda = await InformarAsync(cena, cena.Alice, 18);
        var doMestre = await InformarAsync(cena, cena.Mestre, 5);
        var foraDoLimite = await InformarAsync(cena, cena.Bob, 500);

        Assert.Equal(HttpStatusCode.Conflict, antesDoPedido.StatusCode);
        var iniciativa = await LerAsync(primeira);
        Assert.Equal(EstadoDaIniciativa.Coletando, iniciativa.Estado);
        Assert.Equal(12, Assert.Single(iniciativa.Valores).Valor);
        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, doMestre.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, foraDoLimite.StatusCode);
    }

    [Fact]
    public async Task Ordem_VaiDoMaiorAoMenor_OGrupoJogaNaSequencia_EARodadaVira()
    {
        var cena = await MontarAsync();

        var inicio = await IniciarCombateAsync(cena);
        Assert.Equal((EstadoDaIniciativa.EmCombate, 1, cena.TokenDaAlice.Id), (inicio.Estado, inicio.Rodada, inicio.VezDoTokenId));

        var esperada = new[] { cena.Goblins[0].Id, cena.Goblins[1].Id, cena.Goblins[2].Id, cena.TokenDoBob.Id };
        var vez = cena.TokenDaAlice.Id;
        foreach (var proxima in esperada)
        {
            var quem = vez == cena.TokenDaAlice.Id ? cena.Alice : cena.Mestre;
            var depois = await LerAsync(await EncerrarVezAsync(cena, quem, vez));
            Assert.Equal(proxima, depois.VezDoTokenId);
            Assert.Contains(vez, depois.JogaramNaRodada);
            vez = proxima;
        }

        var novaRodada = await LerAsync(await EncerrarVezAsync(cena, cena.Bob, cena.TokenDoBob.Id));
        Assert.Equal((2, cena.TokenDaAlice.Id), (novaRodada.Rodada, novaRodada.VezDoTokenId));
        Assert.Empty(novaRodada.JogaramNaRodada);
    }

    [Fact]
    public async Task ForaDaVez_NinguemAndaPorBlocos_MasOMestreArrastaLivre()
    {
        var cena = await MontarAsync(goblins: 1);
        await IniciarCombateAsync(cena);
        var (conexaoDoBob, _) = await factory.EntrarNaMesaAsync(cena.Bob, cena.MesaId);
        await using var _1 = conexaoDoBob;
        var (conexaoDoMestre, _) = await factory.EntrarNaMesaAsync(cena.Mestre, cena.MesaId);
        await using var _2 = conexaoDoMestre;
        var (conexaoDaAlice, _) = await factory.EntrarNaMesaAsync(cena.Alice, cena.MesaId);
        await using var _3 = conexaoDaAlice;

        var doBob = await conexaoDoBob.MoverPorBlocosAsync(cena.TokenDoBob, 1, 1);
        var doMestrePorBlocos = await conexaoDoMestre.MoverPorBlocosAsync(cena.Goblins[0], 5, 4);
        var arrasteDoMestre = await conexaoDoMestre.MoverTokenAsync(cena.Goblins[0], 8, 7);
        var daAlice = await conexaoDaAlice.MoverPorBlocosAsync(cena.TokenDaAlice, 0, 2);
        var encerrarDoBob = await EncerrarVezAsync(cena, cena.Bob, cena.TokenDaAlice.Id);

        Assert.False(doBob.Aceito);
        Assert.Equal("Aguarde a sua vez na iniciativa.", doBob.Motivo);
        Assert.False(doMestrePorBlocos.Aceito);
        Assert.True(arrasteDoMestre.Aceito);
        Assert.True(daAlice.Aceito);
        Assert.Equal(HttpStatusCode.Forbidden, encerrarDoBob.StatusCode);
    }

    [Fact]
    public async Task EncerrarVez_ConcluiOMovimento_EAViradaDaRodadaODevolve()
    {
        var cena = await MontarAsync(goblins: 1);
        await IniciarCombateAsync(cena, bob: null);
        var (conexao, _) = await factory.EntrarNaMesaAsync(cena.Alice, cena.MesaId);
        await using var _1 = conexao;

        var andou = await conexao.MoverPorBlocosAsync(cena.TokenDaAlice, 0, 3);
        Assert.True(andou.Aceito);
        await EncerrarVezAsync(cena, cena.Alice, cena.TokenDaAlice.Id);
        var concluido = await AtualizarAsync(cena.Alice, cena.MapaId, cena.TokenDaAlice.Id);
        var virada = await LerAsync(await EncerrarVezAsync(cena, cena.Mestre, cena.Goblins[0].Id));
        var restaurado = await AtualizarAsync(cena.Alice, cena.MapaId, cena.TokenDaAlice.Id);

        Assert.Equal((false, 3), (concluido.MovimentoPendente, concluido.MovimentoRestante));
        Assert.Equal((2, cena.TokenDaAlice.Id), (virada.Rodada, virada.VezDoTokenId));
        Assert.Equal(6, restaurado.MovimentoRestante);
    }

    [Fact]
    public async Task Caido_TemAVezPulada_ECairNaVezPassaAoProximo()
    {
        var cena = await MontarAsync();
        (await cena.Mestre.Client.PutAsJsonAsync(
            $"/api/tokens/{cena.Goblins[1].Id}/hp", new HpRequest(null, 0, null), ApiTestHelpers.Json)).EnsureSuccessStatusCode();
        await IniciarCombateAsync(cena);

        var vezDoGoblin1 = await LerAsync(await EncerrarVezAsync(cena, cena.Alice, cena.TokenDaAlice.Id));
        var pulouOCaido = await LerAsync(await EncerrarVezAsync(cena, cena.Mestre, cena.Goblins[0].Id));
        (await cena.Mestre.Client.PutAsJsonAsync(
            $"/api/tokens/{cena.Goblins[2].Id}/hp", new HpRequest(null, 0, null), ApiTestHelpers.Json)).EnsureSuccessStatusCode();
        var depoisDeCair = await LerAsync(await cena.Bob.Client.GetAsync(cena.Url));

        Assert.Equal(cena.Goblins[0].Id, vezDoGoblin1.VezDoTokenId);
        Assert.Equal(cena.Goblins[2].Id, pulouOCaido.VezDoTokenId);
        Assert.Equal(cena.TokenDoBob.Id, depoisDeCair.VezDoTokenId);
    }

    [Fact]
    public async Task Mestre_SoAlteraAIniciativaDeQuemAindaNaoJogou()
    {
        var cena = await MontarAsync();
        await IniciarCombateAsync(cena);
        await EncerrarVezAsync(cena, cena.Alice, cena.TokenDaAlice.Id);

        var daAlice = await cena.Mestre.Client.PutAsJsonAsync(
            $"{cena.Url}/jogadores/{cena.Alice.UsuarioId}", new ValorDeIniciativaRequest(1), ApiTestHelpers.Json);
        var dosGoblinsNaVez = await DefinirGrupoAsync(cena, "Goblin", 1);
        var doBob = await cena.Mestre.Client.PutAsJsonAsync(
            $"{cena.Url}/jogadores/{cena.Bob.UsuarioId}", new ValorDeIniciativaRequest(20), ApiTestHelpers.Json);
        var doBobPeloProprio = await cena.Bob.Client.PutAsJsonAsync(
            $"{cena.Url}/jogadores/{cena.Bob.UsuarioId}", new ValorDeIniciativaRequest(30), ApiTestHelpers.Json);

        Assert.Equal(HttpStatusCode.Conflict, daAlice.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, dosGoblinsNaVez.StatusCode);
        var atual = await LerAsync(doBob);
        Assert.Equal(20, atual.Valores.Single(v => v.UsuarioId == cena.Bob.UsuarioId).Valor);
        Assert.Equal(HttpStatusCode.Forbidden, doBobPeloProprio.StatusCode);
    }

    [Fact]
    public async Task Ocultos_FicamForaDaOrdem_EOsJogadoresNaoVeemOGrupoDeles()
    {
        var cena = await MontarAsync(goblins: 0);
        var orc = await cena.Mestre.CriarTokenAsync(cena.MapaId, "Orc", x: 5, y: 5);
        (await cena.Mestre.Client.PutAsJsonAsync(
            $"/api/tokens/{orc.Id}/exibicao", new ExibicaoRequest(false, null), ApiTestHelpers.Json)).EnsureSuccessStatusCode();
        await IniciarCombateAsync(cena, bob: null, goblins: null);
        (await DefinirGrupoAsync(cena, "Orc", 30)).EnsureSuccessStatusCode();

        var paraOMestre = await LerAsync(await cena.Mestre.Client.GetAsync(cena.Url));
        var paraOBob = await LerAsync(await cena.Bob.Client.GetAsync(cena.Url));
        var depoisDaAlice = await LerAsync(await EncerrarVezAsync(cena, cena.Alice, cena.TokenDaAlice.Id));

        Assert.Equal(cena.TokenDaAlice.Id, paraOMestre.VezDoTokenId);
        Assert.Contains(paraOMestre.Valores, v => v.Grupo == "orc");
        Assert.DoesNotContain(paraOBob.Valores, v => v.Grupo == "orc");
        Assert.Equal((2, cena.TokenDaAlice.Id), (depoisDaAlice.Rodada, depoisDaAlice.VezDoTokenId));
    }

    [Fact]
    public async Task OutroMapaAtivo_MantemOsValores_ERecomecaARodada()
    {
        var cena = await MontarAsync(goblins: 0);
        await IniciarCombateAsync(cena, goblins: null);
        await EncerrarVezAsync(cena, cena.Alice, cena.TokenDaAlice.Id);
        await EncerrarVezAsync(cena, cena.Bob, cena.TokenDoBob.Id);

        var outro = await cena.Mestre.CriarMapaAsync(cena.MesaId, "Salão");
        await cena.Mestre.AtivarMapaAsync(outro.Id);
        var iniciativa = await LerAsync(await cena.Alice.Client.GetAsync(cena.Url));

        Assert.Equal((EstadoDaIniciativa.EmCombate, 1, cena.TokenDaAlice.Id), (iniciativa.Estado, iniciativa.Rodada, iniciativa.VezDoTokenId));
        Assert.Equal(2, iniciativa.Valores.Count);
        Assert.Empty(iniciativa.JogaramNaRodada);
    }

    [Fact]
    public async Task MovimentacaoLivre_NaoImpoeAVez_ENovaRodadaFicaRecusadaEmCombate()
    {
        var cena = await MontarAsync(goblins: 0);
        await IniciarCombateAsync(cena, goblins: null);
        var (conexao, _) = await factory.EntrarNaMesaAsync(cena.Mestre, cena.MesaId);
        await using var _1 = conexao;

        var novaRodada = await Assert.ThrowsAsync<HubException>(() => conexao.InvokeAsync("NovaRodada", cena.MapaId));
        (await cena.Mestre.Client.PutAsJsonAsync(
            $"/api/mesas/{cena.MesaId}/movimentacao", new MovimentacaoRequest(ModoDeMovimentacao.Livre), ApiTestHelpers.Json)).EnsureSuccessStatusCode();
        var (conexaoDoBob, _) = await factory.EntrarNaMesaAsync(cena.Bob, cena.MesaId);
        await using var _2 = conexaoDoBob;
        var doBob = await conexaoDoBob.MoverTokenAsync(cena.TokenDoBob, 3, 3);

        Assert.Contains("iniciativa", novaRodada.Message);
        Assert.True(doBob.Aceito);
    }

    [Fact]
    public async Task Mudancas_ChegamPeloHub_EOLobbyTrazAIniciativa()
    {
        var cena = await MontarAsync(goblins: 0);
        var conexao = factory.CriarConexaoDoHub(cena.Alice);
        await using var _1 = conexao;
        var eventos = Channel.CreateUnbounded<IniciativaDto>();
        conexao.On<Guid, IniciativaDto>("IniciativaAlterada", (_, iniciativa) => eventos.Writer.TryWrite(iniciativa));
        await conexao.StartAsync();
        var lobby = await conexao.InvokeAsync<LobbyDto>("EntrarNaMesa", cena.MesaId);

        await SolicitarAsync(cena);
        using var limite = new CancellationTokenSource(Espera);
        var recebida = await eventos.Reader.ReadAsync(limite.Token);

        Assert.Equal(EstadoDaIniciativa.Inativa, lobby.Iniciativa.Estado);
        Assert.Equal(EstadoDaIniciativa.Coletando, recebida.Estado);
    }
}
