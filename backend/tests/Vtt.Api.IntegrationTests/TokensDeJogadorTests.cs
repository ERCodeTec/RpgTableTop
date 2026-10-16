using System.Net.Http.Json;
using Vtt.Application.Mapas;
using Vtt.Application.Mesas;
using Vtt.Application.Personagens;
using Vtt.Domain.Mapas;
using Vtt.Domain.Personagens;

namespace Vtt.Api.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class TokensDeJogadorTests(VttApiFactory factory)
{
    [Fact]
    public async Task AtivarMapa_CriaOTokenDeCadaJogador_NoPontoDeSpawn_EOMestreNaoGanhaToken()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (alice, personagemDaAlice) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Alice");
        var (bob, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Bob");
        var mapa = await mestre.CriarMapaAsync(mesa.Id);
        await mestre.CriarTokenAsync(mapa.Id, x: 0, y: 0);

        var estado = await mestre.AtivarMapaAsync(mapa.Id);

        Assert.Equal(3, estado.Tokens.Count);
        var daAlice = estado.TokenDe(alice)!;
        Assert.Equal(TipoToken.Personagem, daAlice.Tipo);
        Assert.Equal(personagemDaAlice.Nome, daAlice.Nome);
        Assert.Equal("Mago", daAlice.Icone);
        Assert.Equal(personagemDaAlice.CorBorda, daAlice.CorBorda);
        Assert.Equal((personagemDaAlice.HpMaximo, personagemDaAlice.HpMaximo), (daAlice.HpAtual, daAlice.HpMaximo));
        Assert.Equal(personagemDaAlice.Id, daAlice.PersonagemId);
        // (0,0) já estava ocupada pelo token do Mestre; os jogadores entram na ordem em que chegaram à mesa.
        Assert.Equal((1, 0), (daAlice.X, daAlice.Y));
        Assert.Equal((2, 0), (estado.TokenDe(bob)!.X, estado.TokenDe(bob)!.Y));
        Assert.Null(estado.TokenDe(mestre));
    }

    [Fact]
    public async Task JogadorQueEntraComMapaAtivo_GanhaToken()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var mapa = (await mestre.CriarMapaAtivoAsync(mesa.Id)).Mapa;

        var (jogador, personagem) = await factory.AdicionarJogadorAsync(mestre, mesa.Id);

        var token = (await jogador.ObterEstadoDoMapaAsync(mapa.Id)).TokenDe(jogador)!;
        Assert.Equal(personagem.Id, token.PersonagemId);
        Assert.Equal((0, 0), (token.X, token.Y));
    }

    [Fact]
    public async Task ComEntrada_OsJogadoresNascemNela_ETransbordamParaPerto()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (alice, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Alice");
        var (bob, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Bob");
        var mapa = await mestre.CriarMapaAsync(mesa.Id);
        // Uma entrada de uma célula só: o segundo jogador fica na livre mais próxima dela.
        var entrada = await mestre.CriarEstruturaAsync(
            mapa.Id, ImagemTestHelpers.Estrutura(TipoEstrutura.Entrada, x: 6, y: 5, altura: 0.1, bloqueia: true));
        Assert.False(entrada.Bloqueia);
        Assert.False(entrada.BloqueiaMovimento);

        var estado = await mestre.AtivarMapaAsync(mapa.Id);

        Assert.Equal((6, 5), (estado.TokenDe(alice)!.X, estado.TokenDe(alice)!.Y));
        var doBob = estado.TokenDe(bob)!;
        Assert.Equal(1, Math.Max(Math.Abs(doBob.X - 6), Math.Abs(doBob.Y - 5)));

        // Quem entra depois, com o mapa ativo, também vai para perto da entrada.
        var (carol, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Carol");
        var daCarol = (await mestre.ObterEstadoDoMapaAsync(mapa.Id)).TokenDe(carol)!;
        Assert.Equal(1, Math.Max(Math.Abs(daCarol.X - 6), Math.Abs(daCarol.Y - 5)));
    }

    [Fact]
    public async Task TrocarDePersonagem_AtualizaOToken_MantendoAPosicao_ERestaurandoOHp()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (jogador, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id);
        var mapa = (await mestre.CriarMapaAtivoAsync(mesa.Id)).Mapa;
        var token = (await mestre.ObterEstadoDoMapaAsync(mapa.Id)).TokenDe(jogador)!;
        var (conexao, _) = await factory.EntrarNaMesaAsync(jogador, mesa.Id);
        await using var _1 = conexao;
        var movido = (await conexao.MoverPorBlocosAsync(token, 4, 5)).Token!;
        (await mestre.Client.PutAsJsonAsync($"/api/tokens/{token.Id}", new EditarTokenRequest("x", 2, 20, "Mago", null, null))).EnsureSuccessStatusCode();
        var novo = await CriarPersonagemAsync(jogador, new PersonagemRequest("Thorin", ClasseDnd.Guerreiro, 5, 44, "#112233"));

        (await jogador.Client.PutAsJsonAsync($"/api/mesas/{mesa.Id}/meu-personagem", new DefinirPersonagemRequest(novo.Id))).EnsureSuccessStatusCode();

        var atualizado = (await mestre.ObterEstadoDoMapaAsync(mapa.Id)).TokenDe(jogador)!;
        Assert.Equal(token.Id, atualizado.Id);
        Assert.Equal((novo.Id, "Thorin", "Guerreiro", "#112233"), (atualizado.PersonagemId, atualizado.Nome, atualizado.Icone, atualizado.CorBorda));
        Assert.Equal((44, 44), (atualizado.HpAtual, atualizado.HpMaximo));
        Assert.Equal((4, 5), (atualizado.X, atualizado.Y));
        Assert.True(atualizado.Versao > movido.Versao);
    }

    [Fact]
    public async Task EditarOPersonagem_SincronizaOToken_ELimitaOHpAtualAoNovoMaximo()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (jogador, personagem) = await factory.AdicionarJogadorAsync(mestre, mesa.Id);
        var mapa = (await mestre.CriarMapaAtivoAsync(mesa.Id)).Mapa;

        (await jogador.Client.PutAsJsonAsync(
            $"/api/personagens/{personagem.Id}", new PersonagemRequest("Lia, a Sábia", ClasseDnd.Druida, 4, 12, "#00AA88"), ApiTestHelpers.Json)).EnsureSuccessStatusCode();

        var token = (await mestre.ObterEstadoDoMapaAsync(mapa.Id)).TokenDe(jogador)!;
        Assert.Equal(("Lia, a Sábia", "Druida", "#00AA88"), (token.Nome, token.Icone, token.CorBorda));
        // O personagem dos testes nasce com 20 de HP; o token estava cheio.
        Assert.Equal((12, 12), (token.HpAtual, token.HpMaximo));
    }

    [Fact]
    public async Task ExcluirOPersonagem_RemoveOToken()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (jogador, personagem) = await factory.AdicionarJogadorAsync(mestre, mesa.Id);
        var mapa = (await mestre.CriarMapaAtivoAsync(mesa.Id)).Mapa;

        (await jogador.Client.DeleteAsync($"/api/personagens/{personagem.Id}")).EnsureSuccessStatusCode();

        Assert.Empty((await mestre.ObterEstadoDoMapaAsync(mapa.Id)).Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task JogadorQueSaiOuERemovido_PerdeOToken(bool removidoPeloMestre)
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (alice, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Alice");
        var (bob, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Bob");
        var mapa = (await mestre.CriarMapaAtivoAsync(mesa.Id)).Mapa;

        var quemPede = removidoPeloMestre ? mestre : bob;
        (await quemPede.Client.DeleteAsync($"/api/mesas/{mesa.Id}/membros/{bob.UsuarioId}")).EnsureSuccessStatusCode();

        var estado = await mestre.ObterEstadoDoMapaAsync(mapa.Id);
        Assert.Null(estado.TokenDe(bob));
        Assert.NotNull(estado.TokenDe(alice));
    }

    [Fact]
    public async Task AtivarOutroMapa_MoveOTokenDoJogador_PreservandoOHpAtual()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (jogador, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id);
        var primeiro = (await mestre.CriarMapaAtivoAsync(mesa.Id)).Mapa;
        var token = (await mestre.ObterEstadoDoMapaAsync(primeiro.Id)).TokenDe(jogador)!;
        (await mestre.Client.PutAsJsonAsync($"/api/tokens/{token.Id}", new EditarTokenRequest("x", 6, 20, "Mago", null, null))).EnsureSuccessStatusCode();
        var segundo = await mestre.CriarMapaAsync(mesa.Id, "Segundo");
        await mestre.CriarTokenAsync(segundo.Id, x: 0, y: 0);

        var estadoDoSegundo = await mestre.AtivarMapaAsync(segundo.Id);

        var movido = estadoDoSegundo.TokenDe(jogador)!;
        Assert.Equal(token.Id, movido.Id);
        Assert.Equal(6, movido.HpAtual);
        Assert.Equal((1, 0), (movido.X, movido.Y));
        Assert.Empty((await mestre.ObterEstadoDoMapaAsync(primeiro.Id)).Tokens);
    }

    private static async Task<PersonagemDto> CriarPersonagemAsync(Sessao sessao, PersonagemRequest request)
    {
        var resposta = await sessao.Client.PostAsJsonAsync("/api/personagens", request, ApiTestHelpers.Json);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<PersonagemDto>(ApiTestHelpers.Json))!;
    }
}
