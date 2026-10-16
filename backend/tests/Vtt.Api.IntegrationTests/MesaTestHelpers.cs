using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Vtt.Api.Hubs;
using Vtt.Application.Auth;
using Vtt.Application.Mapas;
using Vtt.Application.Mesas;
using Vtt.Application.Personagens;
using Vtt.Domain.Mapas;
using Vtt.Domain.Personagens;

namespace Vtt.Api.IntegrationTests;

/// <summary>Um usuário cadastrado, com o cliente HTTP já autenticado como ele.</summary>
public record Sessao(HttpClient Client, AuthResponse Auth)
{
    public Guid UsuarioId => Auth.Usuario.Id;
}

public static class MesaTestHelpers
{
    public static async Task<Sessao> CriarSessaoAsync(this VttApiFactory factory, string nomeExibicao = "Jogador")
    {
        var client = factory.CreateClient();
        var auth = await client.RegistrarAsync(nomeExibicao: nomeExibicao);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return new Sessao(client, auth);
    }

    public static async Task<PersonagemDto> CriarPersonagemAsync(this Sessao sessao, string nome = "Lia")
    {
        var resposta = await sessao.Client.PostAsJsonAsync(
            "/api/personagens", new PersonagemRequest(nome, ClasseDnd.Mago, 3, 20, null), ApiTestHelpers.Json);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<PersonagemDto>(ApiTestHelpers.Json))!;
    }

    public static async Task<MesaDetalheDto> CriarMesaAsync(this Sessao sessao, string nome = "A Mina Perdida")
    {
        var resposta = await sessao.Client.PostAsJsonAsync("/api/mesas", new MesaRequest(nome, null));
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<MesaDetalheDto>(ApiTestHelpers.Json))!;
    }

    public static async Task<ConviteDto> GerarConviteAsync(this Sessao mestre, Guid mesaId, int? validadeHoras = null, int? usosMaximos = null)
    {
        var resposta = await mestre.Client.PostAsJsonAsync(
            $"/api/mesas/{mesaId}/convites", new ConviteRequest(validadeHoras, usosMaximos));
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<ConviteDto>(ApiTestHelpers.Json))!;
    }

    public static Task<HttpResponseMessage> AceitarConviteAsync(this Sessao sessao, string codigo, Guid personagemId) =>
        sessao.Client.PostAsJsonAsync($"/api/convites/{codigo}/aceitar", new AceitarConviteRequest(personagemId));

    /// <summary>Cria um jogador com personagem e o coloca na mesa por um convite novo.</summary>
    public static async Task<(Sessao Jogador, PersonagemDto Personagem)> AdicionarJogadorAsync(
        this VttApiFactory factory, Sessao mestre, Guid mesaId, string nomeExibicao = "Jogador")
    {
        var jogador = await factory.CriarSessaoAsync(nomeExibicao);
        var personagem = await jogador.CriarPersonagemAsync();
        var convite = await mestre.GerarConviteAsync(mesaId);
        (await jogador.AceitarConviteAsync(convite.Codigo, personagem.Id)).EnsureSuccessStatusCode();
        return (jogador, personagem);
    }

    public static Task<MesaDetalheDto?> ObterMesaAsync(this Sessao sessao, Guid mesaId) =>
        sessao.Client.GetFromJsonAsync<MesaDetalheDto>($"/api/mesas/{mesaId}", ApiTestHelpers.Json);

    public static async Task<MapaDto> CriarMapaAsync(this Sessao mestre, Guid mesaId, string nome = "Caverna", int largura = 10, int altura = 8)
    {
        var resposta = await mestre.Client.PostAsJsonAsync($"/api/mesas/{mesaId}/mapas", new MapaRequest(nome, largura, altura, null));
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<MapaDto>(ApiTestHelpers.Json))!;
    }

    public static async Task<MapaEstadoDto> AtivarMapaAsync(this Sessao mestre, Guid mapaId)
    {
        var resposta = await mestre.Client.PutAsync($"/api/mapas/{mapaId}/ativar", null);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<MapaEstadoDto>(ApiTestHelpers.Json))!;
    }

    /// <summary>Cria um mapa e já o torna o ativo da mesa.</summary>
    public static async Task<MapaEstadoDto> CriarMapaAtivoAsync(this Sessao mestre, Guid mesaId, int largura = 10, int altura = 8)
    {
        var mapa = await mestre.CriarMapaAsync(mesaId, largura: largura, altura: altura);
        return await mestre.AtivarMapaAsync(mapa.Id);
    }

    public static async Task<MapaEstadoDto> ObterEstadoDoMapaAsync(this Sessao sessao, Guid mapaId) =>
        (await sessao.Client.GetFromJsonAsync<MapaEstadoDto>($"/api/mapas/{mapaId}/estado", ApiTestHelpers.Json))!;

    public static Task<HttpResponseMessage> PostarTokenAsync(
        this Sessao sessao, Guid mapaId, string nome = "Goblin", int? x = null, int? y = null,
        TipoToken tipo = TipoToken.Inimigo, string icone = "goblin", int hp = 7) =>
        sessao.Client.PostAsJsonAsync(
            $"/api/mapas/{mapaId}/tokens", new CriarTokenRequest(tipo, nome, hp, hp, icone, "#AA3322", x, y), ApiTestHelpers.Json);

    public static async Task<TokenDto> CriarTokenAsync(this Sessao mestre, Guid mapaId, string nome = "Goblin", int? x = null, int? y = null)
    {
        var resposta = await mestre.PostarTokenAsync(mapaId, nome, x, y);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<TokenDto>(ApiTestHelpers.Json))!;
    }

    /// <summary>O token que o servidor mantém para o jogador no mapa, se houver.</summary>
    public static TokenDto? TokenDe(this MapaEstadoDto estado, Sessao jogador) =>
        estado.Tokens.SingleOrDefault(token => token.DonoUsuarioId == jogador.UsuarioId);

    /// <summary>Conecta ao Hub, entra na mesa e devolve a conexão com o estado recebido.</summary>
    public static async Task<(HubConnection Conexao, LobbyDto Lobby)> EntrarNaMesaAsync(this VttApiFactory factory, Sessao sessao, Guid mesaId)
    {
        var conexao = factory.CriarConexaoDoHub(sessao);
        await conexao.StartAsync();
        var lobby = await conexao.InvokeAsync<LobbyDto>("EntrarNaMesa", mesaId);
        return (conexao, lobby);
    }

    public static Task<MovimentoResultado> MoverTokenAsync(this HubConnection conexao, TokenDto token, int x, int y, int? versaoEsperada = null) =>
        conexao.InvokeAsync<MovimentoResultado>("MoverToken", token.Id, x, y, versaoEsperada ?? token.Versao);

    public static Task<MovimentoResultado> MoverPorBlocosAsync(this HubConnection conexao, TokenDto token, int x, int y, int? versaoEsperada = null) =>
        conexao.InvokeAsync<MovimentoResultado>("MoverPorBlocos", token.Id, x, y, versaoEsperada ?? token.Versao);

    public static Task<MovimentoResultado> DesfazerMovimentoAsync(this HubConnection conexao, TokenDto token, int? versaoEsperada = null) =>
        conexao.InvokeAsync<MovimentoResultado>("DesfazerMovimento", token.Id, versaoEsperada ?? token.Versao);

    public static Task<MovimentoResultado> ConcluirMovimentoAsync(this HubConnection conexao, TokenDto token, int? versaoEsperada = null) =>
        conexao.InvokeAsync<MovimentoResultado>("ConcluirMovimento", token.Id, versaoEsperada ?? token.Versao);

    public static Task<MovimentoResultado> DispararAsync(this HubConnection conexao, TokenDto token, int? versaoEsperada = null) =>
        conexao.InvokeAsync<MovimentoResultado>("Disparar", token.Id, versaoEsperada ?? token.Versao);

    public static Task<MovimentoResultado> DesfazerDisparadaAsync(this HubConnection conexao, TokenDto token, int? versaoEsperada = null) =>
        conexao.InvokeAsync<MovimentoResultado>("DesfazerDisparada", token.Id, versaoEsperada ?? token.Versao);

    /// <summary>
    /// Move por blocos e já conclui, como um jogador que anda e confirma. Devolve o resultado do
    /// movimento com o token já concluído quando aceito.
    /// </summary>
    public static async Task<MovimentoResultado> AndarAsync(this HubConnection conexao, TokenDto token, int x, int y)
    {
        var movimento = await conexao.MoverPorBlocosAsync(token, x, y);
        if (!movimento.Aceito || movimento.Token is not { MovimentoPendente: true } movido)
        {
            return movimento;
        }

        return await conexao.ConcluirMovimentoAsync(movido);
    }

    /// <summary>Cliente SignalR ligado ao servidor de teste em memória (long polling, pois não há socket real).</summary>
    public static HubConnection CriarConexaoDoHub(this VttApiFactory factory, Sessao sessao) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/mesa"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(sessao.Auth.Token);
            })
            .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(
                new System.Text.Json.Serialization.JsonStringEnumConverter()))
            .Build();
}
