using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Vtt.Api.Hubs;
using Vtt.Application.Mapas;
using Vtt.Application.Personagens;
using Vtt.Domain.Mapas;
using Vtt.Domain.Personagens;

namespace Vtt.Api.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class NevoaTests(VttApiFactory factory)
{
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Silencio = TimeSpan.FromMilliseconds(400);

    // Mapa de 10x8 = 80 células = 10 bytes.
    private const int Largura = 10;
    private const int Altura = 8;

    /// <summary>Uma camada de 10x8 com as células dadas acesas.</summary>
    private static byte[] Camada(params (int X, int Y)[] celulas)
    {
        var bits = new byte[(Largura * Altura + 7) / 8];
        foreach (var (x, y) in celulas)
        {
            var i = y * Largura + x;
            bits[i >> 3] |= (byte)(1 << (i & 7));
        }

        return bits;
    }

    private static ChannelReader<MemoriaDeNevoaDto> Escutar(HubConnection conexao)
    {
        var eventos = Channel.CreateUnbounded<MemoriaDeNevoaDto>();
        conexao.On<Guid, MemoriaDeNevoaDto>("MemoriaDeNevoaAlterada", (_, memoria) => eventos.Writer.TryWrite(memoria));
        return eventos.Reader;
    }

    private static async Task<MemoriaDeNevoaDto> ProximaAsync(ChannelReader<MemoriaDeNevoaDto> eventos)
    {
        using var limite = new CancellationTokenSource(Espera);
        return await eventos.ReadAsync(limite.Token);
    }

    private static async Task AssertNadaChegaAsync(ChannelReader<MemoriaDeNevoaDto> eventos)
    {
        await Task.Delay(Silencio);
        Assert.False(eventos.TryRead(out var inesperado), $"Evento inesperado: {inesperado?.Tipo}/{inesperado?.UsuarioId}");
    }

    private async Task<(HubConnection Conexao, ChannelReader<MemoriaDeNevoaDto> Eventos)> ConectarAsync(Sessao sessao, Guid mesaId)
    {
        var conexao = factory.CriarConexaoDoHub(sessao);
        var eventos = Escutar(conexao);
        await conexao.StartAsync();
        await conexao.InvokeAsync<LobbyDto>("EntrarNaMesa", mesaId);
        return (conexao, eventos);
    }

    private async Task<(Sessao Mestre, Sessao Alice, Sessao Bob, Guid MesaId, Guid MapaId)> MontarMesaAsync(ModoDeNevoa modo)
    {
        var mestre = await factory.CriarSessaoAsync("Mestre");
        var mesa = await mestre.CriarMesaAsync();
        var (alice, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Alice");
        var (bob, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Bob");
        var estado = await mestre.CriarMapaAtivoAsync(mesa.Id, Largura, Altura);
        (await DefinirModoAsync(mestre, estado.Mapa.Id, modo)).EnsureSuccessStatusCode();
        return (mestre, alice, bob, mesa.Id, estado.Mapa.Id);
    }

    private static Task<HttpResponseMessage> DefinirModoAsync(Sessao sessao, Guid mapaId, ModoDeNevoa modo) =>
        sessao.Client.PutAsJsonAsync($"/api/mapas/{mapaId}/nevoa", new NevoaRequest(modo), ApiTestHelpers.Json);

    private static Task<HttpResponseMessage> PincelAsync(Sessao sessao, Guid mapaId, byte[] celulas, bool revelar) =>
        sessao.Client.PutAsJsonAsync($"/api/mapas/{mapaId}/nevoa/revelada", new RevelarCelulasRequest(celulas, revelar), ApiTestHelpers.Json);

    private static MemoriaDeNevoaDto? Camada(MapaEstadoDto estado, TipoDeMemoria tipo, Guid? usuarioId = null) =>
        estado.Memorias.SingleOrDefault(memoria => memoria.Tipo == tipo && memoria.UsuarioId == usuarioId);

    [Fact]
    public async Task Modo_SoOMestreDefine_EOsJogadoresRecebemNoMapa()
    {
        var (mestre, alice, _, _, mapaId) = await MontarMesaAsync(ModoDeNevoa.Compartilhada);

        var peloJogador = await DefinirModoAsync(alice, mapaId, ModoDeNevoa.Desligada);
        var invalido = await mestre.Client.PutAsJsonAsync($"/api/mapas/{mapaId}/nevoa", new { modo = (string?)null }, ApiTestHelpers.Json);

        Assert.Equal(HttpStatusCode.Forbidden, peloJogador.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);
        Assert.Equal(ModoDeNevoa.Compartilhada, (await alice.ObterEstadoDoMapaAsync(mapaId)).Mapa.Nevoa);
    }

    [Fact]
    public async Task Compartilhada_OQueUmJogadorExploraChegaATodos_EUneComOQueJaHavia()
    {
        var (mestre, alice, bob, mesaId, mapaId) = await MontarMesaAsync(ModoDeNevoa.Compartilhada);
        var (conexaoDaAlice, _) = await ConectarAsync(alice, mesaId);
        await using var _1 = conexaoDaAlice;
        var (conexaoDoBob, eventosDoBob) = await ConectarAsync(bob, mesaId);
        await using var _2 = conexaoDoBob;

        await conexaoDaAlice.InvokeAsync("ExplorarCelulas", mapaId, Camada((0, 0), (1, 0)));
        var primeira = await ProximaAsync(eventosDoBob);
        await conexaoDoBob.InvokeAsync("ExplorarCelulas", mapaId, Camada((1, 0), (9, 7)));
        var segunda = await ProximaAsync(eventosDoBob);

        // Repetir o que já está na memória não gera aviso.
        await conexaoDaAlice.InvokeAsync("ExplorarCelulas", mapaId, Camada((0, 0)));
        await AssertNadaChegaAsync(eventosDoBob);

        Assert.Equal((TipoDeMemoria.Explorada, (Guid?)null), (primeira.Tipo, primeira.UsuarioId));
        Assert.Equal(Camada((0, 0), (1, 0)), primeira.Celulas);
        Assert.Equal(Camada((0, 0), (1, 0), (9, 7)), segunda.Celulas);
        Assert.Equal(Camada((0, 0), (1, 0), (9, 7)), Camada(await mestre.ObterEstadoDoMapaAsync(mapaId), TipoDeMemoria.Explorada)!.Celulas);
    }

    [Fact]
    public async Task Individual_CadaJogadorTemAPropriaMemoria_QueSoEleEOMestreRecebem()
    {
        var (mestre, alice, bob, mesaId, mapaId) = await MontarMesaAsync(ModoDeNevoa.Individual);
        var (conexaoDoMestre, eventosDoMestre) = await ConectarAsync(mestre, mesaId);
        await using var _1 = conexaoDoMestre;
        var (conexaoDaAlice, eventosDaAlice) = await ConectarAsync(alice, mesaId);
        await using var _2 = conexaoDaAlice;
        var (conexaoDoBob, eventosDoBob) = await ConectarAsync(bob, mesaId);
        await using var _3 = conexaoDoBob;

        await conexaoDaAlice.InvokeAsync("ExplorarCelulas", mapaId, Camada((2, 2)));

        var daAlice = await ProximaAsync(eventosDaAlice);
        Assert.Equal(alice.UsuarioId, daAlice.UsuarioId);
        Assert.Equal(alice.UsuarioId, (await ProximaAsync(eventosDoMestre)).UsuarioId);
        await AssertNadaChegaAsync(eventosDoBob);

        var estadoDoBob = await bob.ObterEstadoDoMapaAsync(mapaId);
        var estadoDoMestre = await mestre.ObterEstadoDoMapaAsync(mapaId);
        Assert.Empty(estadoDoBob.Memorias);
        Assert.Equal(Camada((2, 2)), Camada(estadoDoMestre, TipoDeMemoria.Explorada, alice.UsuarioId)!.Celulas);
        Assert.NotNull(Camada(await alice.ObterEstadoDoMapaAsync(mapaId), TipoDeMemoria.Explorada, alice.UsuarioId));
    }

    [Fact]
    public async Task Explorar_ComANevoaDesligadaNaoGuarda_ECamadaDeOutroTamanhoERecusada()
    {
        var (mestre, alice, _, mesaId, mapaId) = await MontarMesaAsync(ModoDeNevoa.Desligada);
        var (conexao, _) = await ConectarAsync(alice, mesaId);
        await using var _1 = conexao;

        await conexao.InvokeAsync("ExplorarCelulas", mapaId, Camada((0, 0)));
        Assert.Empty((await mestre.ObterEstadoDoMapaAsync(mapaId)).Memorias);

        (await DefinirModoAsync(mestre, mapaId, ModoDeNevoa.Compartilhada)).EnsureSuccessStatusCode();
        var erro = await Assert.ThrowsAsync<HubException>(() => conexao.InvokeAsync("ExplorarCelulas", mapaId, new byte[3]));
        Assert.Contains("dimensões", erro.Message);
    }

    [Fact]
    public async Task Pincel_RevelaParaTodos_ECobrirApagaTambemOExplorado()
    {
        var (mestre, alice, bob, mesaId, mapaId) = await MontarMesaAsync(ModoDeNevoa.Individual);
        var (conexaoDaAlice, _) = await ConectarAsync(alice, mesaId);
        await using var _1 = conexaoDaAlice;
        var (conexaoDoBob, eventosDoBob) = await ConectarAsync(bob, mesaId);
        await using var _2 = conexaoDoBob;
        await conexaoDaAlice.InvokeAsync("ExplorarCelulas", mapaId, Camada((0, 0), (1, 1)));

        Assert.Equal(HttpStatusCode.Forbidden, (await PincelAsync(alice, mapaId, Camada((5, 5)), revelar: true)).StatusCode);
        (await PincelAsync(mestre, mapaId, Camada((5, 5), (6, 5)), revelar: true)).EnsureSuccessStatusCode();
        var revelada = await ProximaAsync(eventosDoBob);
        (await PincelAsync(mestre, mapaId, Camada((6, 5), (1, 1)), revelar: false)).EnsureSuccessStatusCode();

        Assert.Equal((TipoDeMemoria.Revelada, (Guid?)null), (revelada.Tipo, revelada.UsuarioId));
        Assert.Equal(Camada((5, 5), (6, 5)), revelada.Celulas);
        var estado = await mestre.ObterEstadoDoMapaAsync(mapaId);
        Assert.Equal(Camada((5, 5)), Camada(estado, TipoDeMemoria.Revelada)!.Celulas);
        Assert.Equal(Camada((0, 0)), Camada(estado, TipoDeMemoria.Explorada, alice.UsuarioId)!.Celulas);
    }

    [Fact]
    public async Task LimparMemoria_EsqueceOExplorado_MasMantemORevelado()
    {
        var (mestre, alice, _, mesaId, mapaId) = await MontarMesaAsync(ModoDeNevoa.Compartilhada);
        var (conexao, eventos) = await ConectarAsync(alice, mesaId);
        await using var _1 = conexao;
        await conexao.InvokeAsync("ExplorarCelulas", mapaId, Camada((3, 3)));
        await ProximaAsync(eventos);
        (await PincelAsync(mestre, mapaId, Camada((4, 4)), revelar: true)).EnsureSuccessStatusCode();
        await ProximaAsync(eventos);

        Assert.Equal(HttpStatusCode.Forbidden, (await alice.Client.DeleteAsync($"/api/mapas/{mapaId}/nevoa/memoria")).StatusCode);
        (await mestre.Client.DeleteAsync($"/api/mapas/{mapaId}/nevoa/memoria")).EnsureSuccessStatusCode();

        var limpa = await ProximaAsync(eventos);
        Assert.Equal(TipoDeMemoria.Explorada, limpa.Tipo);
        Assert.Equal(Camada(), limpa.Celulas);
        var estado = await alice.ObterEstadoDoMapaAsync(mapaId);
        Assert.Equal(Camada((4, 4)), Camada(estado, TipoDeMemoria.Revelada)!.Celulas);
    }

    [Fact]
    public async Task RedimensionarOMapa_MantemCadaCelulaNoLugar()
    {
        var (mestre, alice, _, mesaId, mapaId) = await MontarMesaAsync(ModoDeNevoa.Compartilhada);
        var (conexao, _) = await ConectarAsync(alice, mesaId);
        await using var _1 = conexao;
        await conexao.InvokeAsync("ExplorarCelulas", mapaId, Camada((9, 0), (2, 1)));

        (await mestre.Client.PutAsJsonAsync($"/api/mapas/{mapaId}", new MapaRequest("Caverna", 12, 8, null))).EnsureSuccessStatusCode();

        var esperado = new byte[(12 * 8 + 7) / 8];
        foreach (var i in new[] { 9, 1 * 12 + 2 })
        {
            esperado[i >> 3] |= (byte)(1 << (i & 7));
        }

        var estado = await mestre.ObterEstadoDoMapaAsync(mapaId);
        Assert.Equal(esperado, Camada(estado, TipoDeMemoria.Explorada)!.Celulas);
    }

    [Fact]
    public async Task Visao_VemDaFichaNoTokenDoJogador_EDoMestreNosDemais()
    {
        var mestre = await factory.CriarSessaoAsync("Mestre");
        var mesa = await mestre.CriarMesaAsync();
        var (alice, personagem) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Alice");
        var estado = await mestre.CriarMapaAtivoAsync(mesa.Id);
        Assert.Equal(Personagem.VisaoPadrao, estado.TokenDe(alice)!.Visao);

        (await alice.Client.PutAsJsonAsync($"/api/personagens/{personagem.Id}",
            new PersonagemRequest(personagem.Nome, personagem.Classe, personagem.Nivel, personagem.HpMaximo, null, Visao: 24),
            ApiTestHelpers.Json)).EnsureSuccessStatusCode();
        var invalida = await alice.Client.PutAsJsonAsync($"/api/personagens/{personagem.Id}",
            new PersonagemRequest(personagem.Nome, personagem.Classe, personagem.Nivel, personagem.HpMaximo, null, Visao: 61),
            ApiTestHelpers.Json);
        var tokenDaAlice = (await mestre.ObterEstadoDoMapaAsync(estado.Mapa.Id)).TokenDe(alice)!;

        Assert.Equal(HttpStatusCode.BadRequest, invalida.StatusCode);
        Assert.Equal(24, tokenDaAlice.Visao);

        var criado = await mestre.Client.PostAsJsonAsync($"/api/mapas/{estado.Mapa.Id}/tokens",
            new CriarTokenRequest(TipoToken.Npc, "Guia", 9, 9, "npc", null, 5, 5, Visao: 6), ApiTestHelpers.Json);
        criado.EnsureSuccessStatusCode();
        var guia = (await criado.Content.ReadFromJsonAsync<TokenDto>(ApiTestHelpers.Json))!;
        var inimigo = await mestre.CriarTokenAsync(estado.Mapa.Id, x: 6, y: 6);
        var paraAlice = await alice.ObterEstadoDoMapaAsync(estado.Mapa.Id);

        Assert.Equal(6, guia.Visao);
        Assert.Equal(0, inimigo.Visao);
        Assert.Equal(6, paraAlice.Tokens.Single(token => token.Id == guia.Id).Visao);
    }
}
