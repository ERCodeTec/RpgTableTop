using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Vtt.Api.Hubs;

namespace Vtt.Api.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class PingTests(VttApiFactory factory)
{
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Silencio = TimeSpan.FromMilliseconds(400);

    private static ChannelReader<PingDto> Escutar(HubConnection conexao)
    {
        var pings = Channel.CreateUnbounded<PingDto>();
        conexao.On<Guid, PingDto>("PingNoMapa", (_, ping) => pings.Writer.TryWrite(ping));
        return pings.Reader;
    }

    private static async Task<PingDto> ProximoAsync(ChannelReader<PingDto> pings)
    {
        using var limite = new CancellationTokenSource(Espera);
        return await pings.ReadAsync(limite.Token);
    }

    private static async Task AssertNadaChegaAsync(ChannelReader<PingDto> pings)
    {
        await Task.Delay(Silencio);
        Assert.False(pings.TryRead(out var inesperado), $"Ping inesperado: {inesperado}");
    }

    private async Task<(HubConnection Conexao, ChannelReader<PingDto> Pings)> ConectarAsync(Sessao sessao, Guid mesaId)
    {
        var conexao = factory.CriarConexaoDoHub(sessao);
        var pings = Escutar(conexao);
        await conexao.StartAsync();
        await conexao.InvokeAsync<LobbyDto>("EntrarNaMesa", mesaId);
        return (conexao, pings);
    }

    [Fact]
    public async Task Jogador_Pinga_OsOutrosRecebem_QuemPingouNao_ESemFocar()
    {
        var mestre = await factory.CriarSessaoAsync("Mestre");
        var mesa = await mestre.CriarMesaAsync();
        var (alice, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Alice");
        var estado = await mestre.CriarMapaAtivoAsync(mesa.Id);
        var (conexaoDoMestre, pingsDoMestre) = await ConectarAsync(mestre, mesa.Id);
        await using var _1 = conexaoDoMestre;
        var (conexaoDaAlice, pingsDaAlice) = await ConectarAsync(alice, mesa.Id);
        await using var _2 = conexaoDaAlice;

        // Um jogador não leva a câmera dos outros, mesmo pedindo.
        await conexaoDaAlice.InvokeAsync("Pingar", mesa.Id, estado.Mapa.Id, 3.5, 2.25, true);

        Assert.Equal(new PingDto(estado.Mapa.Id, alice.UsuarioId, 3.5, 2.25, false), await ProximoAsync(pingsDoMestre));
        await AssertNadaChegaAsync(pingsDaAlice);
    }

    [Fact]
    public async Task Mestre_PingaComFoco_OsJogadoresRecebemOFoco()
    {
        var mestre = await factory.CriarSessaoAsync("Mestre");
        var mesa = await mestre.CriarMesaAsync();
        var (alice, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Alice");
        var estado = await mestre.CriarMapaAtivoAsync(mesa.Id);
        var (conexaoDoMestre, _) = await ConectarAsync(mestre, mesa.Id);
        await using var _1 = conexaoDoMestre;
        var (conexaoDaAlice, pingsDaAlice) = await ConectarAsync(alice, mesa.Id);
        await using var _2 = conexaoDaAlice;

        await conexaoDoMestre.InvokeAsync("Pingar", mesa.Id, estado.Mapa.Id, 1.0, 7.0, true);

        Assert.Equal(new PingDto(estado.Mapa.Id, mestre.UsuarioId, 1.0, 7.0, true), await ProximoAsync(pingsDaAlice));
    }

    [Fact]
    public async Task QuemNaoEntrouNaMesa_NaoPinga()
    {
        var mestre = await factory.CriarSessaoAsync("Mestre");
        var mesa = await mestre.CriarMesaAsync();
        var estado = await mestre.CriarMapaAtivoAsync(mesa.Id);
        var (conexaoDoMestre, pingsDoMestre) = await ConectarAsync(mestre, mesa.Id);
        await using var _1 = conexaoDoMestre;

        var estranho = await factory.CriarSessaoAsync();
        await using var conexaoDoEstranho = factory.CriarConexaoDoHub(estranho);
        await conexaoDoEstranho.StartAsync();

        var erro = await Assert.ThrowsAsync<HubException>(
            () => conexaoDoEstranho.InvokeAsync("Pingar", mesa.Id, estado.Mapa.Id, 1.0, 1.0, false));
        Assert.Contains("Entre na mesa", erro.Message);
        await AssertNadaChegaAsync(pingsDoMestre);
    }
}
