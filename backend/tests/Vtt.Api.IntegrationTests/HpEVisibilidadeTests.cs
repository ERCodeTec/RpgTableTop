using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Vtt.Application.Auth;
using Vtt.Application.Mapas;
using Vtt.Domain.Mapas;

namespace Vtt.Api.IntegrationTests;

/// <summary>
/// Escuta todos os eventos do mapa e guarda os payloads como JSON bruto. Assim os testes de
/// vazamento enxergam exatamente o que saiu do servidor, e não o que um tipo do cliente deixaria ver.
/// </summary>
public sealed class Espiao
{
    private readonly ConcurrentQueue<(string Evento, string Json)> _recebidos = new();

    public Espiao(HubConnection conexao)
    {
        conexao.On<Guid, JsonElement>("TokenCriado", (_, token) => Guardar("TokenCriado", token));
        conexao.On<Guid, JsonElement>("TokenAtualizado", (_, token) => Guardar("TokenAtualizado", token));
        conexao.On<Guid, Guid, JsonElement>("HpAlterado", (_, _, hp) => Guardar("HpAlterado", hp));
        conexao.On<Guid, JsonElement>("MapaAtivoAlterado", (_, estado) => Guardar("MapaAtivoAlterado", estado));
        conexao.On<Guid, Guid, Guid, int, int, int>("TokenMovido",
            (_, _, tokenId, x, y, versao) => Guardar("TokenMovido", JsonSerializer.SerializeToElement(new { tokenId, x, y, versao })));
        conexao.On<Guid, Guid, Guid>("TokenRemovido",
            (_, _, tokenId) => Guardar("TokenRemovido", JsonSerializer.SerializeToElement(new { tokenId })));
    }

    public IReadOnlyList<(string Evento, string Json)> Recebidos => [.. _recebidos];

    public void Guardar(string evento, JsonElement payload) => _recebidos.Enqueue((evento, payload.GetRawText()));

    /// <summary>Os eventos recebidos, na ordem, no formato "Evento" ou "Evento:campo=valor".</summary>
    public IEnumerable<string> Nomes => _recebidos.Select(recebido => recebido.Evento);

    /// <summary>Espera o próximo evento com o nome dado e devolve o payload.</summary>
    public async Task<JsonElement> EsperarAsync(string evento, int aPartirDe = 0)
    {
        var limite = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < limite)
        {
            var achado = _recebidos.Skip(aPartirDe).FirstOrDefault(recebido => recebido.Evento == evento);
            if (achado.Json is not null)
            {
                return JsonDocument.Parse(achado.Json).RootElement;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException($"O evento {evento} não chegou. Recebidos: {string.Join(", ", Nomes)}");
    }

    /// <summary>Dá tempo para qualquer evento em trânsito chegar antes de conferir o que não deveria ter chegado.</summary>
    public static Task AguardarSilencioAsync() => Task.Delay(500);
}

[Collection(ApiCollection.Nome)]
public class HpEVisibilidadeTests(VttApiFactory factory)
{
    private async Task<(Sessao Mestre, Sessao Alice, Sessao Bob, Guid MesaId, MapaDto Mapa)> MontarMesaAsync()
    {
        var mestre = await factory.CriarSessaoAsync("Mestre");
        var mesa = await mestre.CriarMesaAsync();
        var (alice, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Alice");
        var (bob, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Bob");
        var estado = await mestre.CriarMapaAtivoAsync(mesa.Id);
        return (mestre, alice, bob, mesa.Id, estado.Mapa);
    }

    private async Task<(HubConnection Conexao, Espiao Espiao, JsonElement Estado)> EspiarAsync(Sessao sessao, Guid mesaId)
    {
        var conexao = factory.CriarConexaoDoHub(sessao);
        var espiao = new Espiao(conexao);
        await conexao.StartAsync();
        var lobby = await conexao.InvokeAsync<JsonElement>("EntrarNaMesa", mesaId);
        espiao.Guardar("EntrarNaMesa", lobby);
        return (conexao, espiao, lobby);
    }

    private static async Task<TokenDto> TokenAsync(Sessao mestre, Guid mapaId, string nome, int hp, int x, int y, ExibicaoHp exibicao, bool visivel = true)
    {
        var resposta = await mestre.Client.PostAsJsonAsync(
            $"/api/mapas/{mapaId}/tokens", new CriarTokenRequest(TipoToken.Inimigo, nome, hp, hp, "goblin", null, x, y), ApiTestHelpers.Json);
        resposta.EnsureSuccessStatusCode();
        var token = (await resposta.Content.ReadFromJsonAsync<TokenDto>(ApiTestHelpers.Json))!;
        return await ExibicaoAsync(mestre, token.Id, visivel, exibicao);
    }

    private static async Task<TokenDto> ExibicaoAsync(Sessao mestre, Guid tokenId, bool? visivel, ExibicaoHp? exibicao = null)
    {
        var resposta = await mestre.Client.PutAsJsonAsync($"/api/tokens/{tokenId}/exibicao", new ExibicaoRequest(visivel, exibicao), ApiTestHelpers.Json);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<TokenDto>(ApiTestHelpers.Json))!;
    }

    private static async Task<TokenDto> HpAsync(Sessao mestre, Guid tokenId, int? delta = null, int? hpAtual = null, int? hpMaximo = null)
    {
        var resposta = await mestre.Client.PutAsJsonAsync($"/api/tokens/{tokenId}/hp", new HpRequest(delta, hpAtual, hpMaximo));
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<TokenDto>(ApiTestHelpers.Json))!;
    }

    /// <summary>Todos os objetos do JSON que trazem algum número de HP, com o id do token a que se referem.</summary>
    private static IEnumerable<(string TokenId, string Campo)> NumerosDeHp(JsonElement elemento)
    {
        if (elemento.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in elemento.EnumerateArray().SelectMany(NumerosDeHp))
            {
                yield return item;
            }
        }
        else if (elemento.ValueKind == JsonValueKind.Object)
        {
            var id = elemento.TryGetProperty("tokenId", out var tokenId) ? tokenId.GetString()
                : elemento.TryGetProperty("id", out var proprio) && proprio.ValueKind == JsonValueKind.String ? proprio.GetString()
                : null;
            foreach (var propriedade in elemento.EnumerateObject())
            {
                if (propriedade.Name is "hpAtual" or "hpMaximo" or "hpPercentual" && propriedade.Value.ValueKind == JsonValueKind.Number)
                {
                    yield return (id ?? "?", propriedade.Name);
                }

                foreach (var item in NumerosDeHp(propriedade.Value))
                {
                    yield return item;
                }
            }
        }
    }

    [Fact]
    public async Task Vazamento_JogadorNuncaRecebeTokenOculto_NemNumerosDeHpQueNaoPodeVer()
    {
        var (mestre, alice, bob, mesaId, mapa) = await MontarMesaAsync();

        // Antes de a Alice conectar: um token oculto, um só com barra, um de HP oculto e um completo.
        var oculto = await TokenAsync(mestre, mapa.Id, "ZZ-EMBOSCADA-ZZ", 137, 5, 5, ExibicaoHp.Completo, visivel: false);
        var comBarra = await TokenAsync(mestre, mapa.Id, "Ogro", 211, 6, 6, ExibicaoHp.Barra);
        var hpOculto = await TokenAsync(mestre, mapa.Id, "Vulto", 223, 7, 7, ExibicaoHp.Oculto);
        var completo = await TokenAsync(mestre, mapa.Id, "Guarda", 31, 8, 7, ExibicaoHp.Completo);
        var daAlice = (await mestre.ObterEstadoDoMapaAsync(mapa.Id)).TokenDe(alice)!;
        var doBob = (await mestre.ObterEstadoDoMapaAsync(mapa.Id)).TokenDe(bob)!;
        // O token do Bob fica com HP oculto para o grupo e, depois, invisível.
        await ExibicaoAsync(mestre, doBob.Id, visivel: null, ExibicaoHp.Oculto);

        var (conexaoDaAlice, espiao, _) = await EspiarAsync(alice, mesaId);
        await using var _1 = conexaoDaAlice;
        var (conexaoDoMestre, _) = await factory.EntrarNaMesaAsync(mestre, mesaId);
        await using var _2 = conexaoDoMestre;

        // Tudo o que o Mestre pode fazer com o token oculto.
        (await mestre.Client.PutAsJsonAsync($"/api/tokens/{oculto.Id}", new EditarTokenRequest("ZZ-EMBOSCADA-ZZ chefe", 120, 137, "dragao", "#ff0000", null), ApiTestHelpers.Json)).EnsureSuccessStatusCode();
        await HpAsync(mestre, oculto.Id, delta: -20);
        await HpAsync(mestre, oculto.Id, hpAtual: 0);
        await ExibicaoAsync(mestre, oculto.Id, visivel: null, ExibicaoHp.Barra);
        var ocultoAgora = (await mestre.ObterEstadoDoMapaAsync(mapa.Id)).Tokens.Single(t => t.Id == oculto.Id);
        Assert.True((await conexaoDoMestre.MoverTokenAsync(ocultoAgora, 5, 6)).Aceito);
        var copia = (await (await mestre.Client.PostAsync($"/api/tokens/{oculto.Id}/duplicar", null)).Content.ReadFromJsonAsync<TokenDto>(ApiTestHelpers.Json))!;
        (await mestre.Client.DeleteAsync($"/api/tokens/{copia.Id}")).EnsureSuccessStatusCode();

        // HP nos tokens visíveis, e o token do Bob ficando invisível.
        await HpAsync(mestre, comBarra.Id, delta: -100);
        await HpAsync(mestre, hpOculto.Id, delta: -100);
        await HpAsync(mestre, completo.Id, delta: -10);
        await HpAsync(mestre, doBob.Id, delta: -3);
        await ExibicaoAsync(mestre, doBob.Id, visivel: false);
        await HpAsync(mestre, daAlice.Id, delta: -4);

        // Troca de mapa ativo, com o token oculto criado no novo mapa antes da ativação.
        var outroMapa = await mestre.CriarMapaAsync(mesaId, "Segundo");
        var ocultoNoOutro = await TokenAsync(mestre, outroMapa.Id, "ZZ-EMBOSCADA-ZZ 2", 137, 4, 4, ExibicaoHp.Completo, visivel: false);
        await mestre.AtivarMapaAsync(outroMapa.Id);
        await espiao.EsperarAsync("MapaAtivoAlterado");
        await Espiao.AguardarSilencioAsync();

        // O estado visto por REST e por uma nova entrada na mesa passa pelo mesmo crivo.
        espiao.Guardar("GET estado", await alice.Client.GetFromJsonAsync<JsonElement>($"/api/mapas/{outroMapa.Id}/estado"));
        var (reconectada, _, estadoAoReconectar) = await EspiarAsync(alice, mesaId);
        await reconectada.DisposeAsync();
        espiao.Guardar("EntrarNaMesa de novo", estadoAoReconectar);

        var recebidos = espiao.Recebidos;
        Assert.Contains(recebidos, r => r.Evento == "HpAlterado");
        Assert.Contains(recebidos, r => r.Evento == "MapaAtivoAlterado");

        // 1) Nada dos tokens ocultos: nem id, nem nome, em nenhum payload.
        string[] segredos = ["ZZ-EMBOSCADA", oculto.Id.ToString(), copia.Id.ToString(), ocultoNoOutro.Id.ToString()];
        foreach (var (evento, json) in recebidos)
        {
            Assert.All(segredos, segredo => Assert.False(json.Contains(segredo, StringComparison.OrdinalIgnoreCase), $"{evento} vazou '{segredo}': {json}"));
        }

        // 2) Números de HP só do próprio token e dos tokens com exibição Completo.
        var podemTerNumeros = new HashSet<string> { daAlice.Id.ToString(), completo.Id.ToString() };
        var podemTerBarra = new HashSet<string>(podemTerNumeros) { comBarra.Id.ToString() };
        foreach (var (evento, json) in recebidos)
        {
            foreach (var (tokenId, campo) in NumerosDeHp(JsonDocument.Parse(json).RootElement))
            {
                var permitidos = campo == "hpPercentual" ? podemTerBarra : podemTerNumeros;
                Assert.True(permitidos.Contains(tokenId), $"{evento} trouxe {campo} do token {tokenId}: {json}");
            }
        }

        // 3) O token do Bob sumiu para a Alice quando ficou invisível, sem evento de HP depois.
        Assert.Contains(recebidos, r => r.Evento == "TokenRemovido" && r.Json.Contains(doBob.Id.ToString()));

        // 4) E a Alice continuou recebendo o HP completo do próprio token.
        var hpDaAlice = recebidos.Last(r => r.Evento == "HpAlterado" && r.Json.Contains(daAlice.Id.ToString())).Json;
        Assert.Contains("\"hpAtual\":16", hpDaAlice);
        Assert.Contains("\"hpMaximo\":20", hpDaAlice);
    }

    [Fact]
    public async Task TokenOculto_ParaOJogador_ECoMoSeNaoExistisse_EmQualquerChamada()
    {
        var (mestre, alice, _, mesaId, mapa) = await MontarMesaAsync();
        var oculto = await TokenAsync(mestre, mapa.Id, "Assassino", 9, 5, 5, ExibicaoHp.Completo, visivel: false);
        var visivel = await TokenAsync(mestre, mapa.Id, "Guarda", 9, 6, 5, ExibicaoHp.Completo);
        var (conexao, _) = await factory.EntrarNaMesaAsync(alice, mesaId);
        await using var _1 = conexao;

        // No Hub, a mesma resposta de um id que não existe.
        var movendoOOculto = await Assert.ThrowsAsync<HubException>(() => conexao.MoverTokenAsync(oculto, 1, 1));
        var movendoInexistente = await Assert.ThrowsAsync<HubException>(
            () => conexao.InvokeAsync<MovimentoResultado>("MoverToken", Guid.NewGuid(), 1, 1, 1));
        Assert.Equal(movendoInexistente.Message, movendoOOculto.Message);

        // No REST, 404 para o oculto; para o visível, o 403 de sempre.
        Assert.Equal(HttpStatusCode.NotFound, (await alice.Client.PutAsJsonAsync($"/api/tokens/{oculto.Id}/hp", new HpRequest(-1, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.Client.PutAsJsonAsync($"/api/tokens/{oculto.Id}/exibicao", new ExibicaoRequest(true, null), ApiTestHelpers.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.Client.PostAsync($"/api/tokens/{oculto.Id}/duplicar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.Client.DeleteAsync($"/api/tokens/{oculto.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await alice.Client.PutAsJsonAsync($"/api/tokens/{visivel.Id}/hp", new HpRequest(-1, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await alice.Client.PutAsJsonAsync($"/api/tokens/{visivel.Id}/exibicao", new ExibicaoRequest(false, null), ApiTestHelpers.Json)).StatusCode);

        var estado = await alice.ObterEstadoDoMapaAsync(mapa.Id);
        Assert.DoesNotContain(estado.Tokens, t => t.Id == oculto.Id);
        Assert.Equal(9, (await mestre.ObterEstadoDoMapaAsync(mapa.Id)).Tokens.Single(t => t.Id == visivel.Id).HpAtual);
    }

    [Fact]
    public async Task TokenOculto_NaoOcupaACelulaParaOJogador_EAoSerReveladoVaiParaACelulaLivreMaisProxima()
    {
        var (mestre, alice, _, mesaId, mapa) = await MontarMesaAsync();
        var oculto = await TokenAsync(mestre, mapa.Id, "Armadilha", 5, 5, 5, ExibicaoHp.Barra, visivel: false);
        var (conexaoDaAlice, espiao, _) = await EspiarAsync(alice, mesaId);
        await using var _1 = conexaoDaAlice;
        var (conexaoDoMestre, _) = await factory.EntrarNaMesaAsync(mestre, mesaId);
        await using var _2 = conexaoDoMestre;
        var daAlice = (await mestre.ObterEstadoDoMapaAsync(mapa.Id)).TokenDe(alice)!;
        var guarda = await mestre.CriarTokenAsync(mapa.Id, "Guarda", x: 9, y: 7);

        // A Alice para em cima do token oculto sem nenhuma pista; o Mestre, que o vê, não coloca outro ali.
        var daAliceNaCelula = await conexaoDaAlice.MoverPorBlocosAsync(daAlice, 5, 5);
        var doMestreNaCelula = await conexaoDoMestre.MoverTokenAsync(guarda, 5, 5);
        Assert.True(daAliceNaCelula.Aceito);
        Assert.False(doMestreNaCelula.Aceito);

        var antes = espiao.Recebidos.Count;
        var revelado = await ExibicaoAsync(mestre, oculto.Id, visivel: true);
        var criado = await espiao.EsperarAsync("TokenCriado", antes);

        // (5,5) está com a Alice: o revelado vai para a vizinha livre mais próxima.
        Assert.Equal((4, 4), (revelado.X, revelado.Y));
        Assert.Equal(oculto.Id.ToString(), criado.GetProperty("id").GetString());
        Assert.Equal((4, 4), (criado.GetProperty("x").GetInt32(), criado.GetProperty("y").GetInt32()));
        Assert.Equal(JsonValueKind.Null, criado.GetProperty("hpAtual").ValueKind);
        Assert.Equal(100, criado.GetProperty("hpPercentual").GetInt32());

        // Ocultar de novo: para a Alice o token simplesmente some.
        antes = espiao.Recebidos.Count;
        await ExibicaoAsync(mestre, oculto.Id, visivel: false);
        var removido = await espiao.EsperarAsync("TokenRemovido", antes);
        Assert.Equal(oculto.Id.ToString(), removido.GetProperty("tokenId").GetString());
        Assert.DoesNotContain("TokenAtualizado", espiao.Nomes.Skip(antes));
    }

    [Fact]
    public async Task DonoDeTokenOculto_ContinuaVendoEMovendoOProprioToken_EOSpawnIgnoraTokensOcultos()
    {
        var mestre = await factory.CriarSessaoAsync("Mestre");
        var mesa = await mestre.CriarMesaAsync();
        var mapa = (await mestre.CriarMapaAtivoAsync(mesa.Id)).Mapa;
        // Um token oculto no ponto de spawn não desloca quem chega: a posição denunciaria onde ele está.
        await TokenAsync(mestre, mapa.Id, "Mímico", 5, 0, 0, ExibicaoHp.Barra, visivel: false);
        var (alice, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Alice");
        var (bob, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Bob");
        var daAlice = (await mestre.ObterEstadoDoMapaAsync(mapa.Id)).TokenDe(alice)!;
        Assert.Equal((0, 0), (daAlice.X, daAlice.Y));

        var (conexaoDaAlice, espiaoDaAlice, _) = await EspiarAsync(alice, mesa.Id);
        await using var _1 = conexaoDaAlice;
        var (conexaoDoBob, espiaoDoBob, _) = await EspiarAsync(bob, mesa.Id);
        await using var _2 = conexaoDoBob;

        var oculto = await ExibicaoAsync(mestre, daAlice.Id, visivel: false);
        var paraADona = await espiaoDaAlice.EsperarAsync("TokenAtualizado");
        await espiaoDoBob.EsperarAsync("TokenRemovido");

        Assert.False(paraADona.GetProperty("visivel").GetBoolean());
        Assert.Equal(20, paraADona.GetProperty("hpAtual").GetInt32());
        Assert.NotNull((await alice.ObterEstadoDoMapaAsync(mapa.Id)).TokenDe(alice));
        Assert.Null((await bob.ObterEstadoDoMapaAsync(mapa.Id)).TokenDe(alice));

        // A dona move o próprio token; o Bob não fica sabendo.
        var antesDoBob = espiaoDoBob.Recebidos.Count;
        var movimento = await conexaoDaAlice.MoverPorBlocosAsync(oculto, 3, 3);
        await HpAsync(mestre, daAlice.Id, delta: -5);
        await espiaoDaAlice.EsperarAsync("HpAlterado");
        await Espiao.AguardarSilencioAsync();

        Assert.True(movimento.Aceito);
        Assert.Equal(20, movimento.Token!.HpAtual);
        Assert.Empty(espiaoDoBob.Recebidos.Skip(antesDoBob));
        // E o Bob não consegue mover o token dela, com a resposta de um token que não existe.
        var doBob = await Assert.ThrowsAsync<HubException>(() => conexaoDoBob.MoverPorBlocosAsync(movimento.Token, 4, 4));
        Assert.Contains("Token não encontrado", doBob.Message);
    }

    [Fact]
    public async Task Hp_FicaEntreZeroEOMaximo_ESoOMestreAltera()
    {
        var (mestre, alice, _, _, mapa) = await MontarMesaAsync();
        var token = await TokenAsync(mestre, mapa.Id, "Troll", 40, 5, 5, ExibicaoHp.Completo);
        var daAlice = (await mestre.ObterEstadoDoMapaAsync(mapa.Id)).TokenDe(alice)!;

        var dano = await HpAsync(mestre, token.Id, delta: -12);
        var danoDemais = await HpAsync(mestre, token.Id, delta: -500);
        var cura = await HpAsync(mestre, token.Id, delta: 7);
        var curaDemais = await HpAsync(mestre, token.Id, delta: 500);
        var definido = await HpAsync(mestre, token.Id, hpAtual: 999);
        var maximoMenor = await HpAsync(mestre, token.Id, hpMaximo: 25);
        var maximoEAtual = await HpAsync(mestre, token.Id, hpAtual: 10, hpMaximo: 60);

        Assert.Equal((28, false, 70), (dano.HpAtual, dano.Caido, dano.HpPercentual));
        Assert.Equal((0, true, 0), (danoDemais.HpAtual, danoDemais.Caido, danoDemais.HpPercentual));
        Assert.Equal((7, false, 20), (cura.HpAtual, cura.Caido, cura.HpPercentual));
        Assert.Equal(40, curaDemais.HpAtual);
        Assert.Equal(40, definido.HpAtual);
        Assert.Equal((25, 25), (maximoMenor.HpAtual, maximoMenor.HpMaximo));
        Assert.Equal((10, 60), (maximoEAtual.HpAtual, maximoEAtual.HpMaximo));
        Assert.True(maximoEAtual.Versao > token.Versao);

        // Caído não remove o token.
        await HpAsync(mestre, token.Id, hpAtual: 0);
        Assert.Contains((await mestre.ObterEstadoDoMapaAsync(mapa.Id)).Tokens, t => t.Id == token.Id && t.Caido);

        // Pedidos inválidos, o jogador e o HP máximo do token de jogador.
        Assert.Equal(HttpStatusCode.BadRequest, (await mestre.Client.PutAsJsonAsync($"/api/tokens/{token.Id}/hp", new HpRequest(null, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await mestre.Client.PutAsJsonAsync($"/api/tokens/{token.Id}/hp", new HpRequest(-1, 5, null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await mestre.Client.PutAsJsonAsync($"/api/tokens/{token.Id}/hp", new HpRequest(null, null, 0))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await mestre.Client.PutAsJsonAsync($"/api/tokens/{daAlice.Id}/hp", new HpRequest(null, null, 50))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await alice.Client.PutAsJsonAsync($"/api/tokens/{daAlice.Id}/hp", new HpRequest(5, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await alice.Client.PutAsJsonAsync($"/api/tokens/{token.Id}/hp", new HpRequest(-5, null, null))).StatusCode);

        // O HP atual é da cena: o cadastro do personagem continua com o máximo dele, intocado.
        await HpAsync(mestre, daAlice.Id, delta: -7);
        var personagens = await alice.Client.GetFromJsonAsync<List<Vtt.Application.Personagens.PersonagemDto>>("/api/personagens", ApiTestHelpers.Json);
        Assert.Equal(20, Assert.Single(personagens!).HpMaximo);
    }

    [Fact]
    public async Task DanosSimultaneos_Somam_EmVezDeUmSobrescreverOOutro()
    {
        var (mestre, _, _, _, mapa) = await MontarMesaAsync();
        var token = await TokenAsync(mestre, mapa.Id, "Hidra", 100, 5, 5, ExibicaoHp.Completo);

        await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => HpAsync(mestre, token.Id, delta: -3)));

        var atual = (await mestre.ObterEstadoDoMapaAsync(mapa.Id)).Tokens.Single(t => t.Id == token.Id);
        Assert.Equal(64, atual.HpAtual);
    }

    [Fact]
    public async Task HpAlterado_ChegaACadaUmComoElePodeVer_ESoQuandoOQueEleVeMuda()
    {
        var (mestre, alice, _, mesaId, mapa) = await MontarMesaAsync();
        var comBarra = await TokenAsync(mestre, mapa.Id, "Ogro", 200, 5, 5, ExibicaoHp.Barra);
        var hpOculto = await TokenAsync(mestre, mapa.Id, "Vulto", 50, 6, 5, ExibicaoHp.Oculto);
        var completo = await TokenAsync(mestre, mapa.Id, "Guarda", 30, 7, 5, ExibicaoHp.Completo);
        var (conexaoDaAlice, daAlice, _) = await EspiarAsync(alice, mesaId);
        await using var _1 = conexaoDaAlice;
        var (conexaoDoMestre, doMestre, _) = await EspiarAsync(mestre, mesaId);
        await using var _2 = conexaoDoMestre;

        // Barra: 1 de dano em 200 não muda a faixa de 5%; 30 de dano muda.
        await HpAsync(mestre, comBarra.Id, delta: -1);
        var paraOMestre = await doMestre.EsperarAsync("HpAlterado");
        await Espiao.AguardarSilencioAsync();
        Assert.Equal(199, paraOMestre.GetProperty("hpAtual").GetInt32());
        Assert.DoesNotContain("HpAlterado", daAlice.Nomes);

        await HpAsync(mestre, comBarra.Id, delta: -30);
        var faixa = await daAlice.EsperarAsync("HpAlterado");
        Assert.Equal(comBarra.Id.ToString(), faixa.GetProperty("tokenId").GetString());
        Assert.Equal(JsonValueKind.Null, faixa.GetProperty("hpAtual").ValueKind);
        Assert.Equal(JsonValueKind.Null, faixa.GetProperty("hpMaximo").ValueKind);
        Assert.Equal(85, faixa.GetProperty("hpPercentual").GetInt32());
        Assert.False(faixa.GetProperty("caido").GetBoolean());

        // Oculto: dano não gera nada para a jogadora; cair, sim, e sem número nenhum.
        var antes = daAlice.Recebidos.Count;
        await HpAsync(mestre, hpOculto.Id, delta: -20);
        await Espiao.AguardarSilencioAsync();
        Assert.Empty(daAlice.Recebidos.Skip(antes));

        await HpAsync(mestre, hpOculto.Id, hpAtual: 0);
        var caiu = await daAlice.EsperarAsync("HpAlterado", antes);
        Assert.True(caiu.GetProperty("caido").GetBoolean());
        Assert.Equal(JsonValueKind.Null, caiu.GetProperty("hpAtual").ValueKind);
        Assert.Equal(JsonValueKind.Null, caiu.GetProperty("hpPercentual").ValueKind);

        // Completo: barra e números.
        antes = daAlice.Recebidos.Count;
        await HpAsync(mestre, completo.Id, delta: -9);
        var numeros = await daAlice.EsperarAsync("HpAlterado", antes);
        Assert.Equal((21, 30, 70), (numeros.GetProperty("hpAtual").GetInt32(), numeros.GetProperty("hpMaximo").GetInt32(), numeros.GetProperty("hpPercentual").GetInt32()));

        // Mudar a exibição muda o que a jogadora recebe do mesmo token.
        antes = daAlice.Recebidos.Count;
        await ExibicaoAsync(mestre, completo.Id, visivel: null, ExibicaoHp.Oculto);
        var semHp = await daAlice.EsperarAsync("TokenAtualizado", antes);
        Assert.Equal(JsonValueKind.Null, semHp.GetProperty("hpAtual").ValueKind);
        Assert.Equal(JsonValueKind.Null, semHp.GetProperty("hpPercentual").ValueKind);
    }

    [Fact]
    public async Task Reconexao_OEstadoDevolvidoTrazOQueMudouEnquantoOClienteEstavaFora()
    {
        var (mestre, alice, _, mesaId, mapa) = await MontarMesaAsync();
        var guarda = await TokenAsync(mestre, mapa.Id, "Guarda", 30, 5, 5, ExibicaoHp.Completo);
        var (primeira, lobbyInicial) = await factory.EntrarNaMesaAsync(alice, mesaId);
        Assert.Equal(30, lobbyInicial.MapaAtivo!.Tokens.Single(t => t.Id == guarda.Id).HpAtual);
        await primeira.DisposeAsync();

        // Enquanto a Alice está desconectada: HP, movimento, token novo, token oculto e estrutura.
        var (conexaoDoMestre, _) = await factory.EntrarNaMesaAsync(mestre, mesaId);
        await using var _1 = conexaoDoMestre;
        var ferido = await HpAsync(mestre, guarda.Id, delta: -11);
        Assert.True((await conexaoDoMestre.MoverTokenAsync(ferido, 8, 6)).Aceito);
        await TokenAsync(mestre, mapa.Id, "Lobo", 7, 2, 6, ExibicaoHp.Barra);
        await TokenAsync(mestre, mapa.Id, "Espreitador", 7, 3, 6, ExibicaoHp.Barra, visivel: false);
        await mestre.CriarEstruturaAsync(mapa.Id, ImagemTestHelpers.Estrutura(x: 1, y: 4, largura: 3));

        var (segunda, lobby) = await factory.EntrarNaMesaAsync(alice, mesaId);
        await using var _2 = segunda;

        var estado = lobby.MapaAtivo!;
        var guardaAgora = estado.Tokens.Single(t => t.Id == guarda.Id);
        Assert.Equal((19, 8, 6), (guardaAgora.HpAtual, guardaAgora.X, guardaAgora.Y));
        Assert.Contains(estado.Tokens, t => t.Nome == "Lobo" && t.HpAtual is null && t.HpPercentual == 100);
        Assert.DoesNotContain(estado.Tokens, t => t.Nome == "Espreitador");
        Assert.Single(estado.Estruturas);
    }

    [Fact]
    public async Task RenovarLogin_TrocaUmTokenValidoPorOutro_EOAntigoSemTokenNaoRenova()
    {
        var sessao = await factory.CriarSessaoAsync("Renovadora");
        var anonimo = factory.CreateClient();

        var resposta = await sessao.Client.PostAsync("/api/auth/renovar", null);
        var renovado = (await resposta.Content.ReadFromJsonAsync<AuthResponse>(ApiTestHelpers.Json))!;
        var comONovo = factory.CreateClient();
        comONovo.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", renovado.Token);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.NotEqual(sessao.Auth.Token, renovado.Token);
        Assert.Equal(sessao.UsuarioId, renovado.Usuario.Id);
        Assert.True(renovado.ExpiraEm >= sessao.Auth.ExpiraEm);
        Assert.Equal(HttpStatusCode.OK, (await comONovo.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsync("/api/auth/renovar", null)).StatusCode);

        anonimo.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", renovado.Token + "adulterado");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsync("/api/auth/renovar", null)).StatusCode);
    }

    [Fact]
    public async Task ExibicaoPadrao_ECompletoParaPersonagens_EBarraParaOsDemais()
    {
        var (mestre, alice, _, _, mapa) = await MontarMesaAsync();
        var inimigo = await mestre.CriarTokenAsync(mapa.Id, "Goblin", x: 5, y: 5);
        var avulso = (await (await mestre.PostarTokenAsync(mapa.Id, "Aliado", 6, 5, TipoToken.Personagem, "Paladino")).Content.ReadFromJsonAsync<TokenDto>(ApiTestHelpers.Json))!;
        var daAlice = (await mestre.ObterEstadoDoMapaAsync(mapa.Id)).TokenDe(alice)!;

        Assert.Equal(ExibicaoHp.Barra, inimigo.ExibicaoHp);
        Assert.Equal(ExibicaoHp.Completo, avulso.ExibicaoHp);
        Assert.Equal(ExibicaoHp.Completo, daAlice.ExibicaoHp);
        Assert.True(inimigo.Visivel);
    }
}
