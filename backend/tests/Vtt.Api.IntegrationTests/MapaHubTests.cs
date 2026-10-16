using System.Net.Http.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Vtt.Api.Hubs;
using Vtt.Application.Mapas;

namespace Vtt.Api.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class MapaHubTests(VttApiFactory factory)
{
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Silencio = TimeSpan.FromMilliseconds(400);

    /// <summary>Registra em ordem os eventos do mapa que a conexão recebe (os do lobby são ignorados).</summary>
    private static ChannelReader<string> Escutar(HubConnection conexao)
    {
        var eventos = Channel.CreateUnbounded<string>();
        conexao.On<Guid, TokenDto>("TokenCriado", (_, token) => eventos.Writer.TryWrite($"TokenCriado:{token.Nome}"));
        conexao.On<Guid, Guid, Guid, int, int, int>("TokenMovido",
            (_, _, tokenId, x, y, versao) => eventos.Writer.TryWrite($"TokenMovido:{tokenId}:{x},{y}:v{versao}"));
        // HP como o destinatário o vê: "atual/percentual", com o número vazio para quem só vê a barra.
        conexao.On<Guid, TokenDto>("TokenAtualizado", (_, token) => eventos.Writer.TryWrite($"TokenAtualizado:{token.Nome}:{token.HpAtual}/{token.HpPercentual}"));
        conexao.On<Guid, Guid, Guid>("TokenRemovido", (_, _, tokenId) => eventos.Writer.TryWrite($"TokenRemovido:{tokenId}"));
        conexao.On<Guid, MapaEstadoDto?>("MapaAtivoAlterado",
            (_, estado) => eventos.Writer.TryWrite($"MapaAtivoAlterado:{estado?.Mapa.Nome}:{estado?.Tokens.Count}"));
        conexao.On<Guid, MapaDto>("MapaAtualizado", (_, mapa) => eventos.Writer.TryWrite($"MapaAtualizado:{mapa.Nome}"));
        return eventos.Reader;
    }

    private static async Task<string> ProximoAsync(ChannelReader<string> eventos)
    {
        using var limite = new CancellationTokenSource(Espera);
        return await eventos.ReadAsync(limite.Token);
    }

    private static async Task AssertNadaChegaAsync(ChannelReader<string> eventos)
    {
        await Task.Delay(Silencio);
        Assert.False(eventos.TryRead(out var inesperado), $"Evento inesperado: {inesperado}");
    }

    private async Task<(HubConnection Conexao, ChannelReader<string> Eventos, LobbyDto Lobby)> ConectarAsync(Sessao sessao, Guid mesaId)
    {
        var conexao = factory.CriarConexaoDoHub(sessao);
        var eventos = Escutar(conexao);
        await conexao.StartAsync();
        var lobby = await conexao.InvokeAsync<LobbyDto>("EntrarNaMesa", mesaId);
        return (conexao, eventos, lobby);
    }

    /// <summary>Mesa com Mestre, Alice e Bob, um mapa ativo de 10x8 e os tokens dos dois jogadores.</summary>
    private async Task<(Sessao Mestre, Sessao Alice, Sessao Bob, Guid MesaId, MapaEstadoDto Estado)> MontarMesaAsync()
    {
        var mestre = await factory.CriarSessaoAsync("Mestre");
        var mesa = await mestre.CriarMesaAsync();
        var (alice, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Alice");
        var (bob, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Bob");
        var estado = await mestre.CriarMapaAtivoAsync(mesa.Id);
        return (mestre, alice, bob, mesa.Id, estado);
    }

    [Fact]
    public async Task EntrarNaMesa_DevolveOMapaAtivoComOsTokens_OuNuloSeNaoHouver()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (jogador, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id);
        await mestre.CriarMapaAsync(mesa.Id, "Em preparação");

        var (semMapa, _, lobbySemMapa) = await ConectarAsync(jogador, mesa.Id);
        await semMapa.DisposeAsync();
        var estado = await mestre.CriarMapaAtivoAsync(mesa.Id);
        await mestre.CriarTokenAsync(estado.Mapa.Id, "Goblin", x: 3, y: 3);
        var (comMapa, _, lobbyComMapa) = await ConectarAsync(jogador, mesa.Id);
        await using var _1 = comMapa;

        Assert.Null(lobbySemMapa.MapaAtivo);
        Assert.Equal(estado.Mapa.Id, lobbyComMapa.MapaAtivo!.Mapa.Id);
        Assert.True(lobbyComMapa.MapaAtivo.Mapa.Ativo);
        Assert.Equal(2, lobbyComMapa.MapaAtivo.Tokens.Count);
        Assert.NotNull(lobbyComMapa.MapaAtivo.TokenDe(jogador));
    }

    [Fact]
    public async Task DoisClientes_OMovimentoDeUmChegaAoOutro()
    {
        var (mestre, alice, _, mesaId, estado) = await MontarMesaAsync();
        var (conexaoDoMestre, eventosDoMestre, _) = await ConectarAsync(mestre, mesaId);
        await using var _1 = conexaoDoMestre;
        var (conexaoDaAlice, eventosDaAlice, _) = await ConectarAsync(alice, mesaId);
        await using var _2 = conexaoDaAlice;
        var token = estado.TokenDe(alice)!;

        var resultado = await conexaoDaAlice.MoverPorBlocosAsync(token, 6, 4);

        Assert.True(resultado.Aceito);
        Assert.Null(resultado.Motivo);
        Assert.Equal((6, 4, 2), (resultado.Token!.X, resultado.Token.Y, resultado.Token.Versao));
        Assert.Equal($"TokenMovido:{token.Id}:6,4:v2", await ProximoAsync(eventosDoMestre));
        Assert.Equal($"TokenMovido:{token.Id}:6,4:v2", await ProximoAsync(eventosDaAlice));
        var gravado = (await mestre.ObterEstadoDoMapaAsync(estado.Mapa.Id)).TokenDe(alice)!;
        Assert.Equal((6, 4, 2), (gravado.X, gravado.Y, gravado.Versao));
    }

    [Fact]
    public async Task Jogador_NaoMoveTokenDeOutro_MasOMestreMoveQualquerUm()
    {
        var (mestre, alice, bob, mesaId, estado) = await MontarMesaAsync();
        var (conexaoDaAlice, eventosDaAlice, _) = await ConectarAsync(alice, mesaId);
        await using var _1 = conexaoDaAlice;
        var (conexaoDoMestre, _, _) = await ConectarAsync(mestre, mesaId);
        await using var _2 = conexaoDoMestre;
        var doBob = estado.TokenDe(bob)!;
        var inimigo = await mestre.CriarTokenAsync(estado.Mapa.Id, x: 5, y: 5);
        Assert.Equal("TokenCriado:Goblin", await ProximoAsync(eventosDaAlice));

        var movendoODoBob = await conexaoDaAlice.MoverPorBlocosAsync(doBob, 7, 7);
        var movendoOInimigo = await conexaoDaAlice.MoverPorBlocosAsync(inimigo, 7, 7);

        Assert.False(movendoODoBob.Aceito);
        Assert.Contains("seu próprio token", movendoODoBob.Motivo);
        Assert.Equal((doBob.X, doBob.Y, doBob.Versao), (movendoODoBob.Token!.X, movendoODoBob.Token.Y, movendoODoBob.Token.Versao));
        Assert.False(movendoOInimigo.Aceito);
        await AssertNadaChegaAsync(eventosDaAlice);

        var peloMestre = await conexaoDoMestre.MoverTokenAsync(doBob, 7, 7);
        Assert.True(peloMestre.Aceito);
        Assert.Equal($"TokenMovido:{doBob.Id}:7,7:v2", await ProximoAsync(eventosDaAlice));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(10, 0)]
    [InlineData(0, 8)]
    public async Task MovimentoParaForaDoMapa_ERejeitado(int x, int y)
    {
        var (_, alice, _, mesaId, estado) = await MontarMesaAsync();
        var (conexao, eventos, _) = await ConectarAsync(alice, mesaId);
        await using var _1 = conexao;
        var token = estado.TokenDe(alice)!;

        var resultado = await conexao.MoverPorBlocosAsync(token, x, y);

        Assert.False(resultado.Aceito);
        Assert.Contains("fora do mapa", resultado.Motivo);
        Assert.Equal((token.X, token.Y, token.Versao), (resultado.Token!.X, resultado.Token.Y, resultado.Token.Versao));
        await AssertNadaChegaAsync(eventos);
    }

    [Fact]
    public async Task MovimentoParaCelulaOcupada_ERejeitado()
    {
        var (_, alice, bob, mesaId, estado) = await MontarMesaAsync();
        var (conexao, eventos, _) = await ConectarAsync(alice, mesaId);
        await using var _1 = conexao;
        var daAlice = estado.TokenDe(alice)!;
        var doBob = estado.TokenDe(bob)!;

        var resultado = await conexao.MoverPorBlocosAsync(daAlice, doBob.X, doBob.Y);

        Assert.False(resultado.Aceito);
        Assert.Contains("Já existe um token", resultado.Motivo);
        Assert.Equal((daAlice.X, daAlice.Y), (resultado.Token!.X, resultado.Token.Y));
        await AssertNadaChegaAsync(eventos);
    }

    [Fact]
    public async Task ConflitoDeVersao_RejeitaEDevolveOEstadoAtualDoToken()
    {
        var (mestre, alice, _, mesaId, estado) = await MontarMesaAsync();
        var (conexaoDaAlice, eventosDaAlice, _) = await ConectarAsync(alice, mesaId);
        await using var _1 = conexaoDaAlice;
        var (conexaoDoMestre, _, _) = await ConectarAsync(mestre, mesaId);
        await using var _2 = conexaoDoMestre;
        var token = estado.TokenDe(alice)!;
        Assert.True((await conexaoDoMestre.MoverTokenAsync(token, 3, 3)).Aceito);
        Assert.Equal($"TokenMovido:{token.Id}:3,3:v2", await ProximoAsync(eventosDaAlice));

        // A Alice ainda acha que o token está na versão 1.
        var atrasado = await conexaoDaAlice.MoverPorBlocosAsync(token, 8, 6);

        Assert.False(atrasado.Aceito);
        Assert.Contains("alterado por outra pessoa", atrasado.Motivo);
        Assert.Equal((3, 3, 2), (atrasado.Token!.X, atrasado.Token.Y, atrasado.Token.Versao));
        await AssertNadaChegaAsync(eventosDaAlice);

        // Com o estado devolvido, o movimento seguinte passa.
        Assert.True((await conexaoDaAlice.MoverPorBlocosAsync(atrasado.Token, 8, 6)).Aceito);
    }

    [Fact]
    public async Task MoverParaAPropriaCelula_EAceitoSemGerarEvento()
    {
        var (_, alice, _, mesaId, estado) = await MontarMesaAsync();
        var (conexao, eventos, _) = await ConectarAsync(alice, mesaId);
        await using var _1 = conexao;
        var token = estado.TokenDe(alice)!;

        var resultado = await conexao.MoverPorBlocosAsync(token, token.X, token.Y);

        Assert.True(resultado.Aceito);
        Assert.Equal(token.Versao, resultado.Token!.Versao);
        await AssertNadaChegaAsync(eventos);
    }

    [Fact]
    public async Task NaoMembro_NaoMoveNemRecebeEventosDoMapa()
    {
        var (mestre, alice, _, mesaId, estado) = await MontarMesaAsync();
        var (conexaoDaAlice, eventosDaAlice, _) = await ConectarAsync(alice, mesaId);
        await using var _1 = conexaoDaAlice;
        var estranho = await factory.CriarSessaoAsync("Estranho");
        await using var conexaoDoEstranho = factory.CriarConexaoDoHub(estranho);
        var eventosDoEstranho = Escutar(conexaoDoEstranho);
        await conexaoDoEstranho.StartAsync();
        var token = estado.TokenDe(alice)!;

        await Assert.ThrowsAsync<HubException>(() => conexaoDoEstranho.InvokeAsync<LobbyDto>("EntrarNaMesa", mesaId));
        var erro = await Assert.ThrowsAsync<HubException>(() => conexaoDoEstranho.MoverTokenAsync(token, 5, 5));
        var inexistente = await Assert.ThrowsAsync<HubException>(
            () => conexaoDoEstranho.InvokeAsync<MovimentoResultado>("MoverToken", Guid.NewGuid(), 5, 5, 1));

        Assert.Contains("Token não encontrado", erro.Message);
        Assert.Contains("Token não encontrado", inexistente.Message);

        Assert.True((await conexaoDaAlice.MoverPorBlocosAsync(token, 5, 5)).Aceito);
        await mestre.CriarTokenAsync(estado.Mapa.Id, x: 9, y: 7);
        Assert.Equal($"TokenMovido:{token.Id}:5,5:v2", await ProximoAsync(eventosDaAlice));
        Assert.Equal("TokenCriado:Goblin", await ProximoAsync(eventosDaAlice));
        await AssertNadaChegaAsync(eventosDoEstranho);
    }

    [Fact]
    public async Task AcoesDoMestrePelaApiRest_ChegamAoGrupoComoEventos()
    {
        var (mestre, alice, _, mesaId, estado) = await MontarMesaAsync();
        var (conexaoDaAlice, eventosDaAlice, _) = await ConectarAsync(alice, mesaId);
        await using var _1 = conexaoDaAlice;

        var token = await mestre.CriarTokenAsync(estado.Mapa.Id, "Lobo", x: 4, y: 4);
        Assert.Equal("TokenCriado:Lobo", await ProximoAsync(eventosDaAlice));

        (await mestre.Client.PutAsJsonAsync($"/api/tokens/{token.Id}", new EditarTokenRequest("Lobo ferido", 2, 7, "lobo", null, null))).EnsureSuccessStatusCode();
        // A Alice não é dona do lobo, que mostra só a barra: 2 de 7 chega como a faixa de 30%, sem o número.
        Assert.Equal("TokenAtualizado:Lobo ferido:/30", await ProximoAsync(eventosDaAlice));

        (await mestre.Client.PostAsync($"/api/tokens/{token.Id}/duplicar", null)).EnsureSuccessStatusCode();
        Assert.Equal("TokenCriado:Lobo ferido", await ProximoAsync(eventosDaAlice));

        (await mestre.Client.DeleteAsync($"/api/tokens/{token.Id}")).EnsureSuccessStatusCode();
        Assert.Equal($"TokenRemovido:{token.Id}", await ProximoAsync(eventosDaAlice));

        (await mestre.Client.PutAsJsonAsync($"/api/mapas/{estado.Mapa.Id}", new MapaRequest("Covil", 12, 9, null))).EnsureSuccessStatusCode();
        Assert.Equal("MapaAtualizado:Covil", await ProximoAsync(eventosDaAlice));

        // Alice, Bob e a cópia do lobo no primeiro mapa; no novo, só os tokens dos dois jogadores.
        var outro = await mestre.CriarMapaAsync(mesaId, "Ponte");
        await mestre.AtivarMapaAsync(outro.Id);
        Assert.Equal("MapaAtivoAlterado:Ponte:2", await ProximoAsync(eventosDaAlice));

        (await mestre.Client.DeleteAsync($"/api/mapas/{outro.Id}")).EnsureSuccessStatusCode();
        Assert.Equal("MapaAtivoAlterado::", await ProximoAsync(eventosDaAlice));
    }

    [Fact]
    public async Task MapaEmPreparacao_SoOMestreRecebeOsEventos_EOJogadorNaoMoveNele()
    {
        var (mestre, alice, _, mesaId, _) = await MontarMesaAsync();
        var (conexaoDoMestre, eventosDoMestre, _) = await ConectarAsync(mestre, mesaId);
        await using var _1 = conexaoDoMestre;
        var (conexaoDaAlice, eventosDaAlice, _) = await ConectarAsync(alice, mesaId);
        await using var _2 = conexaoDaAlice;
        var preparado = await mestre.CriarMapaAsync(mesaId, "Emboscada");

        var token = await mestre.CriarTokenAsync(preparado.Id, "Dragão", x: 2, y: 2);
        Assert.Equal("TokenCriado:Dragão", await ProximoAsync(eventosDoMestre));

        Assert.True((await conexaoDoMestre.MoverTokenAsync(token, 3, 2)).Aceito);
        Assert.Equal($"TokenMovido:{token.Id}:3,2:v2", await ProximoAsync(eventosDoMestre));

        (await mestre.Client.PutAsJsonAsync($"/api/mapas/{preparado.Id}", new MapaRequest("Emboscada na ponte", 10, 8, null))).EnsureSuccessStatusCode();
        Assert.Equal("MapaAtualizado:Emboscada na ponte", await ProximoAsync(eventosDoMestre));

        await AssertNadaChegaAsync(eventosDaAlice);
        var erro = await Assert.ThrowsAsync<HubException>(() => conexaoDaAlice.MoverTokenAsync(token, 4, 4, versaoEsperada: 2));
        Assert.Contains("Token não encontrado", erro.Message);
    }

    [Fact]
    public async Task TokensDeJogador_GeramEventos_AoEntrarTrocarDePersonagemESair()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var estado = await mestre.CriarMapaAtivoAsync(mesa.Id);
        var (conexaoDoMestre, eventosDoMestre, _) = await ConectarAsync(mestre, mesa.Id);
        await using var _1 = conexaoDoMestre;

        var (jogador, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id);
        Assert.Equal("TokenCriado:Lia", await ProximoAsync(eventosDoMestre));
        var token = (await mestre.ObterEstadoDoMapaAsync(estado.Mapa.Id)).TokenDe(jogador)!;

        var reserva = await jogador.CriarPersonagemAsync("Reserva");
        (await jogador.Client.PutAsJsonAsync($"/api/mesas/{mesa.Id}/meu-personagem", new Vtt.Application.Mesas.DefinirPersonagemRequest(reserva.Id))).EnsureSuccessStatusCode();
        Assert.Equal("TokenAtualizado:Reserva:20/100", await ProximoAsync(eventosDoMestre));

        (await mestre.Client.DeleteAsync($"/api/mesas/{mesa.Id}/membros/{jogador.UsuarioId}")).EnsureSuccessStatusCode();
        Assert.Equal($"TokenRemovido:{token.Id}", await ProximoAsync(eventosDoMestre));
    }
}

public class MovimentoLimitadoFactory : VttApiFactory
{
    protected override int MovimentosPorSegundo => 1;

    protected override int ChamadasDoHubPorSegundo => 1;
}

public class MovimentoLimitadoTests(MovimentoLimitadoFactory factory) : IClassFixture<MovimentoLimitadoFactory>
{
    [Fact]
    public async Task MoverToken_AcimaDoLimiteDaConexao_ERejeitadoSemAlterarOToken()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var estado = await mestre.CriarMapaAtivoAsync(mesa.Id);
        var token = await mestre.CriarTokenAsync(estado.Mapa.Id, x: 0, y: 0);
        var (conexao, _) = await factory.EntrarNaMesaAsync(mestre, mesa.Id);
        await using var _1 = conexao;

        var primeiro = await conexao.MoverTokenAsync(token, 1, 0);
        var emSeguida = await conexao.MoverTokenAsync(primeiro.Token!, 2, 0);

        Assert.True(primeiro.Aceito);
        Assert.False(emSeguida.Aceito);
        Assert.Contains("Muitos movimentos", emSeguida.Motivo);
        Assert.Null(emSeguida.Token);
        var gravado = Assert.Single((await mestre.ObterEstadoDoMapaAsync(estado.Mapa.Id)).Tokens);
        Assert.Equal((1, 0, 2), (gravado.X, gravado.Y, gravado.Versao));

        // Outra conexão tem o próprio limite.
        var (outra, _) = await factory.EntrarNaMesaAsync(mestre, mesa.Id);
        await using var _2 = outra;
        Assert.True((await outra.MoverTokenAsync(gravado, 2, 0)).Aceito);
    }

    [Fact]
    public async Task OsDemaisMetodosDoHub_TambemTemLimitePorConexao_EMensagemGrandeDemaisERecusada()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (conexao, _) = await factory.EntrarNaMesaAsync(mestre, mesa.Id);
        await using var _1 = conexao;

        var emSeguida = await Assert.ThrowsAsync<HubException>(() => conexao.InvokeAsync<LobbyDto>("EntrarNaMesa", mesa.Id));
        Assert.Contains("Muitas chamadas", emSeguida.Message);

        // Cada método tem o próprio limite: o de entrar na mesa estourou, o de sair ainda não.
        await conexao.InvokeAsync("SairDoLobby", mesa.Id);

        // Uma chamada com 100 kB de argumento passa do tamanho máximo (32 kB) e derruba a conexão.
        var (outra, _) = await factory.EntrarNaMesaAsync(mestre, mesa.Id);
        await using var _2 = outra;
        await Assert.ThrowsAnyAsync<Exception>(() => outra.InvokeAsync<LobbyDto>("EntrarNaMesa", new string('x', 100_000)));
    }
}
