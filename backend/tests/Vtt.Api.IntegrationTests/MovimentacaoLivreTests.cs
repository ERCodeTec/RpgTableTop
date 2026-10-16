using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;
using Vtt.Api.Hubs;
using Vtt.Application.Mapas;
using Vtt.Application.Mesas;
using Vtt.Domain.Mesas;
using static Vtt.Api.IntegrationTests.ImagemTestHelpers;

namespace Vtt.Api.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class MovimentacaoLivreTests(VttApiFactory factory)
{
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(5);

    private static Task<HttpResponseMessage> DefinirMovimentacaoAsync(Sessao sessao, Guid mesaId, ModoDeMovimentacao? modo) =>
        sessao.Client.PutAsJsonAsync($"/api/mesas/{mesaId}/movimentacao", new MovimentacaoRequest(modo), ApiTestHelpers.Json);

    /// <summary>
    /// Mesa com Mestre, Alice e Bob e um mapa ativo de 10x8, com a movimentação livre. Os tokens dos
    /// jogadores nascem em (0,0) e (1,0), com o deslocamento padrão de 6.
    /// </summary>
    private async Task<(Sessao Mestre, Sessao Alice, Sessao Bob, Guid MesaId, MapaEstadoDto Estado)> MontarMesaLivreAsync()
    {
        var mestre = await factory.CriarSessaoAsync("Mestre");
        var mesa = await mestre.CriarMesaAsync();
        var (alice, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Alice");
        var (bob, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Bob");
        var estado = await mestre.CriarMapaAtivoAsync(mesa.Id);
        (await DefinirMovimentacaoAsync(mestre, mesa.Id, ModoDeMovimentacao.Livre)).EnsureSuccessStatusCode();
        return (mestre, alice, bob, mesa.Id, estado);
    }

    private async Task<HubConnection> ConectarAsync(Sessao sessao, Guid mesaId)
    {
        var conexao = factory.CriarConexaoDoHub(sessao);
        await conexao.StartAsync();
        await conexao.InvokeAsync<LobbyDto>("EntrarNaMesa", mesaId);
        return conexao;
    }

    [Fact]
    public async Task Mesa_ComecaPorBlocos_ESoOMestreTrocaAMovimentacao_AvisandoOLobby()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (jogador, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id);
        var conexao = factory.CriarConexaoDoHub(jogador);
        var eventos = Channel.CreateUnbounded<string>();
        conexao.On<Guid, ModoDeMovimentacao>("MovimentacaoAlterada", (_, modo) => eventos.Writer.TryWrite($"MovimentacaoAlterada:{modo}"));
        await conexao.StartAsync();
        await using var _1 = conexao;
        await conexao.InvokeAsync<LobbyDto>("EntrarNaMesa", mesa.Id);

        Assert.Equal(ModoDeMovimentacao.PorBlocos, mesa.Movimentacao);
        Assert.Equal(HttpStatusCode.Forbidden, (await DefinirMovimentacaoAsync(jogador, mesa.Id, ModoDeMovimentacao.Livre)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await DefinirMovimentacaoAsync(mestre, mesa.Id, null)).StatusCode);

        var resposta = await DefinirMovimentacaoAsync(mestre, mesa.Id, ModoDeMovimentacao.Livre);
        resposta.EnsureSuccessStatusCode();
        var detalhe = await resposta.Content.ReadFromJsonAsync<MesaDetalheDto>(ApiTestHelpers.Json);

        Assert.Equal(ModoDeMovimentacao.Livre, detalhe!.Movimentacao);
        Assert.Equal(ModoDeMovimentacao.Livre, (await jogador.ObterMesaAsync(mesa.Id))!.Movimentacao);
        using var limite = new CancellationTokenSource(Espera);
        Assert.Equal("MovimentacaoAlterada:Livre", await eventos.Reader.ReadAsync(limite.Token));
    }

    [Fact]
    public async Task Livre_OJogadorArrastaOProprioTokenParaLonge_SemGastarMovimento()
    {
        var (_, alice, _, mesaId, estado) = await MontarMesaLivreAsync();
        await using var conexao = await ConectarAsync(alice, mesaId);
        var token = estado.TokenDe(alice)!;

        var arrastado = await conexao.MoverTokenAsync(token, 9, 7);

        Assert.True(arrastado.Aceito);
        Assert.Equal((9, 7, 6), (arrastado.Token!.X, arrastado.Token.Y, arrastado.Token.MovimentoRestante));
    }

    [Fact]
    public async Task Livre_EstruturasECelulasOcupadasBarram_EOcultosNaoContamParaOJogador()
    {
        var (mestre, alice, _, mesaId, estado) = await MontarMesaLivreAsync();
        var mapaId = estado.Mapa.Id;
        await mestre.CriarEstruturaAsync(mapaId, Estrutura(x: 5, y: 5));
        await mestre.CriarTokenAsync(mapaId, "Goblin A", x: 3, y: 3);
        var oculto = await mestre.CriarTokenAsync(mapaId, "Goblin B", x: 7, y: 7);
        (await mestre.Client.PutAsJsonAsync($"/api/tokens/{oculto.Id}/exibicao", new ExibicaoRequest(false, null), ApiTestHelpers.Json))
            .EnsureSuccessStatusCode();
        await using var conexao = await ConectarAsync(alice, mesaId);
        var token = estado.TokenDe(alice)!;

        var naParede = await conexao.MoverTokenAsync(token, 5, 5);
        var noGoblin = await conexao.MoverTokenAsync(token, 3, 3);
        var sobreOOculto = await conexao.MoverTokenAsync(token, 7, 7);

        Assert.False(naParede.Aceito);
        Assert.Contains("estrutura bloqueando", naParede.Motivo);
        Assert.False(noGoblin.Aceito);
        Assert.Contains("Já existe um token", noGoblin.Motivo);
        // Como no movimento por blocos: barrar ali denunciaria que há algo escondido.
        Assert.True(sobreOOculto.Aceito);
        Assert.Equal((7, 7), (sobreOOculto.Token!.X, sobreOOculto.Token.Y));
    }

    [Fact]
    public async Task Livre_OJogadorNaoArrastaOTokenDeOutro_NemOProprioCaido()
    {
        var (mestre, alice, bob, mesaId, estado) = await MontarMesaLivreAsync();
        await using var conexao = await ConectarAsync(alice, mesaId);
        var tokenDaAlice = estado.TokenDe(alice)!;

        var doBob = await conexao.MoverTokenAsync(estado.TokenDe(bob)!, 4, 4);
        (await mestre.Client.PutAsJsonAsync($"/api/tokens/{tokenDaAlice.Id}/hp", new HpRequest(null, 0, null))).EnsureSuccessStatusCode();
        var caido = (await mestre.ObterEstadoDoMapaAsync(estado.Mapa.Id)).TokenDe(alice)!;
        var arrastandoCaido = await conexao.MoverTokenAsync(caido, 4, 4);

        Assert.False(doBob.Aceito);
        Assert.Contains("seu próprio token", doBob.Motivo);
        Assert.False(arrastandoCaido.Aceito);
        Assert.Contains("caído", arrastandoCaido.Motivo);
    }

    [Fact]
    public async Task VoltandoParaPorBlocos_OJogadorNaoArrastaMais()
    {
        var (mestre, alice, _, mesaId, estado) = await MontarMesaLivreAsync();
        (await DefinirMovimentacaoAsync(mestre, mesaId, ModoDeMovimentacao.PorBlocos)).EnsureSuccessStatusCode();
        await using var conexao = await ConectarAsync(alice, mesaId);

        var arrastando = await conexao.MoverTokenAsync(estado.TokenDe(alice)!, 9, 7);

        Assert.False(arrastando.Aceito);
        Assert.Contains("células destacadas", arrastando.Motivo);
    }
}
