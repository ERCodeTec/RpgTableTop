using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Vtt.Api.Hubs;
using Vtt.Application.Mesas;
using Vtt.Domain.Mesas;

namespace Vtt.Api.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class MesaHubTests(VttApiFactory factory)
{
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Silencio = TimeSpan.FromMilliseconds(400);

    /// <summary>Registra em ordem os nomes dos eventos (e o id do usuário, quando houver) que a conexão recebe.</summary>
    private static ChannelReader<string> Escutar(HubConnection conexao)
    {
        var eventos = Channel.CreateUnbounded<string>();
        conexao.On<Guid, MembroDto>("MembroEntrou", (_, membro) => eventos.Writer.TryWrite($"MembroEntrou:{membro.UsuarioId}"));
        conexao.On<Guid, Guid>("MembroSaiu", (_, usuarioId) => eventos.Writer.TryWrite($"MembroSaiu:{usuarioId}"));
        conexao.On<Guid, Guid>("MembroOnline", (_, usuarioId) => eventos.Writer.TryWrite($"MembroOnline:{usuarioId}"));
        conexao.On<Guid, Guid>("MembroOffline", (_, usuarioId) => eventos.Writer.TryWrite($"MembroOffline:{usuarioId}"));
        conexao.On<Guid, Guid, PersonagemResumoDto?>("PersonagemAtivoAlterado",
            (_, usuarioId, personagem) => eventos.Writer.TryWrite($"PersonagemAtivoAlterado:{usuarioId}:{personagem?.Nome}"));
        conexao.On<Guid, bool>("SessaoAlterada", (_, ativa) => eventos.Writer.TryWrite($"SessaoAlterada:{ativa}"));
        conexao.On<Guid>("MesaExcluida", mesaId => eventos.Writer.TryWrite($"MesaExcluida:{mesaId}"));
        conexao.On<Guid>("RemovidoDaMesa", mesaId => eventos.Writer.TryWrite($"RemovidoDaMesa:{mesaId}"));
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

    private async Task<(HubConnection Conexao, ChannelReader<string> Eventos, LobbyDto Lobby)> ConectarAoLobbyAsync(Sessao sessao, Guid mesaId)
    {
        var conexao = factory.CriarConexaoDoHub(sessao);
        var eventos = Escutar(conexao);
        await conexao.StartAsync();
        var lobby = await conexao.InvokeAsync<LobbyDto>("EntrarNaMesa", mesaId);
        return (conexao, eventos, lobby);
    }

    [Fact]
    public async Task EntrarNaMesa_DevolveOEstadoDoLobbyComQuemEstaOnline()
    {
        var mestre = await factory.CriarSessaoAsync("Mestre Edu");
        var mesa = await mestre.CriarMesaAsync("Lobby");
        var (jogador, personagem) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Alice");
        var (conexaoDoMestre, _, lobbyDoMestre) = await ConectarAoLobbyAsync(mestre, mesa.Id);
        await using var _1 = conexaoDoMestre;

        var (conexaoDoJogador, _, lobbyDoJogador) = await ConectarAoLobbyAsync(jogador, mesa.Id);
        await using var _2 = conexaoDoJogador;

        Assert.Equal([mestre.UsuarioId], lobbyDoMestre.UsuariosOnline);
        Assert.Equal("Lobby", lobbyDoJogador.Mesa.Nome);
        Assert.False(lobbyDoJogador.Mesa.SessaoAtiva);
        Assert.Equal(PapelNaMesa.Jogador, lobbyDoJogador.Mesa.MeuPapel);
        Assert.Equal(2, lobbyDoJogador.Mesa.Membros.Count);
        Assert.Equal(personagem.Id, lobbyDoJogador.Mesa.Membros.Single(m => m.UsuarioId == jogador.UsuarioId).PersonagemAtivo!.Id);
        Assert.Equal(new[] { mestre.UsuarioId, jogador.UsuarioId }.Order(), lobbyDoJogador.UsuariosOnline.Order());
    }

    [Fact]
    public async Task NaoMembro_NaoEntraNoGrupoNemRecebeEventos_EOsMembrosRecebemMembroEntrou()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (conexaoDoMestre, eventosDoMestre, _) = await ConectarAoLobbyAsync(mestre, mesa.Id);
        await using var _1 = conexaoDoMestre;

        var estranho = await factory.CriarSessaoAsync();
        await using var conexaoDoEstranho = factory.CriarConexaoDoHub(estranho);
        var eventosDoEstranho = Escutar(conexaoDoEstranho);
        await conexaoDoEstranho.StartAsync();

        var erro = await Assert.ThrowsAsync<HubException>(() => conexaoDoEstranho.InvokeAsync<LobbyDto>("EntrarNaMesa", mesa.Id));
        Assert.Contains("Mesa não encontrada", erro.Message);

        var (novoJogador, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Novata");

        Assert.Equal($"MembroEntrou:{novoJogador.UsuarioId}", await ProximoAsync(eventosDoMestre));
        await AssertNadaChegaAsync(eventosDoEstranho);
        await AssertNadaChegaAsync(eventosDoMestre);
    }

    [Fact]
    public async Task SemToken_AConexaoERecusada_ETokenNaQueryStringSoValeNoHub()
    {
        var sessao = await factory.CriarSessaoAsync();
        var anonimo = factory.CreateClient();

        var semToken = await anonimo.PostAsync("/hubs/mesa/negotiate?negotiateVersion=1", null);
        var comTokenNaUrl = await anonimo.PostAsync($"/hubs/mesa/negotiate?negotiateVersion=1&access_token={sessao.Auth.Token}", null);
        var apiComTokenNaUrl = await anonimo.GetAsync($"/api/mesas?access_token={sessao.Auth.Token}");

        Assert.Equal(HttpStatusCode.Unauthorized, semToken.StatusCode);
        Assert.Equal(HttpStatusCode.OK, comTokenNaUrl.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, apiComTokenNaUrl.StatusCode);
    }

    [Fact]
    public async Task Presenca_ContaConexoes_SoFicaOfflineQuandoAUltimaCai()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (jogador, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id);
        var (conexaoDoMestre, eventosDoMestre, _) = await ConectarAoLobbyAsync(mestre, mesa.Id);
        await using var _1 = conexaoDoMestre;

        var (primeiraAba, _, _) = await ConectarAoLobbyAsync(jogador, mesa.Id);
        Assert.Equal($"MembroOnline:{jogador.UsuarioId}", await ProximoAsync(eventosDoMestre));

        var (segundaAba, _, _) = await ConectarAoLobbyAsync(jogador, mesa.Id);
        await AssertNadaChegaAsync(eventosDoMestre);

        await primeiraAba.DisposeAsync();
        await AssertNadaChegaAsync(eventosDoMestre);

        await segundaAba.DisposeAsync();
        Assert.Equal($"MembroOffline:{jogador.UsuarioId}", await ProximoAsync(eventosDoMestre));
    }

    [Fact]
    public async Task SairDoLobby_DeixaOfflineSemFecharAConexao()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (jogador, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id);
        var (conexaoDoMestre, eventosDoMestre, _) = await ConectarAoLobbyAsync(mestre, mesa.Id);
        await using var _1 = conexaoDoMestre;
        var (conexaoDoJogador, eventosDoJogador, _) = await ConectarAoLobbyAsync(jogador, mesa.Id);
        await using var _2 = conexaoDoJogador;
        Assert.Equal($"MembroOnline:{jogador.UsuarioId}", await ProximoAsync(eventosDoMestre));

        await conexaoDoJogador.InvokeAsync("SairDoLobby", mesa.Id);

        Assert.Equal($"MembroOffline:{jogador.UsuarioId}", await ProximoAsync(eventosDoMestre));
        (await mestre.Client.PutAsJsonAsync($"/api/mesas/{mesa.Id}/sessao", new SessaoRequest(true))).EnsureSuccessStatusCode();
        Assert.Equal("SessaoAlterada:True", await ProximoAsync(eventosDoMestre));
        await AssertNadaChegaAsync(eventosDoJogador);
    }

    [Fact]
    public async Task AcoesPelaApiRest_ChegamAoGrupoComoEventos()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (jogador, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id);
        var reserva = await jogador.CriarPersonagemAsync("Reserva");
        var (conexaoDoMestre, eventosDoMestre, _) = await ConectarAoLobbyAsync(mestre, mesa.Id);
        await using var _1 = conexaoDoMestre;
        var (conexaoDoJogador, eventosDoJogador, _) = await ConectarAoLobbyAsync(jogador, mesa.Id);
        await using var _2 = conexaoDoJogador;
        Assert.Equal($"MembroOnline:{jogador.UsuarioId}", await ProximoAsync(eventosDoMestre));

        (await jogador.Client.PutAsJsonAsync($"/api/mesas/{mesa.Id}/meu-personagem", new DefinirPersonagemRequest(reserva.Id))).EnsureSuccessStatusCode();
        Assert.Equal($"PersonagemAtivoAlterado:{jogador.UsuarioId}:Reserva", await ProximoAsync(eventosDoMestre));
        Assert.Equal($"PersonagemAtivoAlterado:{jogador.UsuarioId}:Reserva", await ProximoAsync(eventosDoJogador));

        (await mestre.Client.PutAsJsonAsync($"/api/mesas/{mesa.Id}/sessao", new SessaoRequest(true))).EnsureSuccessStatusCode();
        Assert.Equal("SessaoAlterada:True", await ProximoAsync(eventosDoJogador));
        Assert.Equal("SessaoAlterada:True", await ProximoAsync(eventosDoMestre));

        // Repetir o mesmo estado não gera evento.
        (await mestre.Client.PutAsJsonAsync($"/api/mesas/{mesa.Id}/sessao", new SessaoRequest(true))).EnsureSuccessStatusCode();
        await AssertNadaChegaAsync(eventosDoJogador);

        (await mestre.Client.DeleteAsync($"/api/mesas/{mesa.Id}")).EnsureSuccessStatusCode();
        Assert.Equal($"MesaExcluida:{mesa.Id}", await ProximoAsync(eventosDoJogador));
        Assert.Equal($"MesaExcluida:{mesa.Id}", await ProximoAsync(eventosDoMestre));
    }

    [Fact]
    public async Task JogadorRemovido_RecebeAvisoProprio_SaiDoGrupo_EOsDemaisRecebemMembroSaiu()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (alice, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Alice");
        var (bob, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Bob");
        var (conexaoDaAlice, eventosDaAlice, _) = await ConectarAoLobbyAsync(alice, mesa.Id);
        await using var _1 = conexaoDaAlice;
        var (conexaoDoBob, eventosDoBob, _) = await ConectarAoLobbyAsync(bob, mesa.Id);
        await using var _2 = conexaoDoBob;
        Assert.Equal($"MembroOnline:{bob.UsuarioId}", await ProximoAsync(eventosDaAlice));

        (await mestre.Client.DeleteAsync($"/api/mesas/{mesa.Id}/membros/{bob.UsuarioId}")).EnsureSuccessStatusCode();

        Assert.Equal($"RemovidoDaMesa:{mesa.Id}", await ProximoAsync(eventosDoBob));
        Assert.Equal($"MembroSaiu:{bob.UsuarioId}", await ProximoAsync(eventosDaAlice));

        // Depois de removido, o Bob não escuta mais a mesa nem consegue voltar ao lobby.
        (await mestre.Client.PutAsJsonAsync($"/api/mesas/{mesa.Id}/sessao", new SessaoRequest(true))).EnsureSuccessStatusCode();
        Assert.Equal("SessaoAlterada:True", await ProximoAsync(eventosDaAlice));
        await AssertNadaChegaAsync(eventosDoBob);
        await Assert.ThrowsAsync<HubException>(() => conexaoDoBob.InvokeAsync<LobbyDto>("EntrarNaMesa", mesa.Id));

        // A queda da conexão dele não gera MembroOffline: ele já não está na mesa.
        await conexaoDoBob.DisposeAsync();
        await AssertNadaChegaAsync(eventosDaAlice);
    }

    [Fact]
    public async Task JogadorQueSai_NaoRecebeAvisoDeRemocao_EOsDemaisRecebemMembroSaiu()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (jogador, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id);
        var (conexaoDoMestre, eventosDoMestre, _) = await ConectarAoLobbyAsync(mestre, mesa.Id);
        await using var _1 = conexaoDoMestre;
        var (conexaoDoJogador, eventosDoJogador, _) = await ConectarAoLobbyAsync(jogador, mesa.Id);
        await using var _2 = conexaoDoJogador;
        Assert.Equal($"MembroOnline:{jogador.UsuarioId}", await ProximoAsync(eventosDoMestre));

        (await jogador.Client.DeleteAsync($"/api/mesas/{mesa.Id}/membros/{jogador.UsuarioId}")).EnsureSuccessStatusCode();

        Assert.Equal($"MembroSaiu:{jogador.UsuarioId}", await ProximoAsync(eventosDoMestre));
        await AssertNadaChegaAsync(eventosDoJogador);
    }
}
