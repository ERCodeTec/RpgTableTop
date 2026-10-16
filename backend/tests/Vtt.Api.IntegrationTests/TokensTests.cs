using System.Net;
using System.Net.Http.Json;
using Vtt.Application.Mapas;
using Vtt.Domain.Mapas;

namespace Vtt.Api.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class TokensTests(VttApiFactory factory)
{
    private static readonly EditarTokenRequest Edicao = new("Chefe goblin", 3, 12, "esqueleto", "#00ff00", false);

    [Fact]
    public async Task Mestre_CriaEditaERemoveToken()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var mapa = await mestre.CriarMapaAsync(mesa.Id);

        var criado = await mestre.CriarTokenAsync(mapa.Id, "Goblin", x: 4, y: 3);
        var edicao = await mestre.Client.PutAsJsonAsync($"/api/tokens/{criado.Id}", Edicao);
        var editado = (await edicao.Content.ReadFromJsonAsync<TokenDto>(ApiTestHelpers.Json))!;

        Assert.Equal((TipoToken.Inimigo, "Goblin", 7, 7, 4, 3, 1), (criado.Tipo, criado.Nome, criado.HpAtual, criado.HpMaximo, criado.X, criado.Y, criado.Versao));
        Assert.True(criado.Visivel);
        Assert.Null(criado.DonoUsuarioId);
        Assert.Equal(HttpStatusCode.OK, edicao.StatusCode);
        Assert.Equal(("Chefe goblin", 3, 12, "esqueleto", "#00FF00", false), (editado.Nome, editado.HpAtual, editado.HpMaximo, editado.Icone, editado.CorBorda, editado.Visivel));
        Assert.Equal((4, 3, 2), (editado.X, editado.Y, editado.Versao));

        Assert.Equal(HttpStatusCode.NoContent, (await mestre.Client.DeleteAsync($"/api/tokens/{criado.Id}")).StatusCode);
        Assert.Empty((await mestre.ObterEstadoDoMapaAsync(mapa.Id)).Tokens);
        Assert.Equal(HttpStatusCode.NotFound, (await mestre.Client.DeleteAsync($"/api/tokens/{criado.Id}")).StatusCode);
    }

    [Fact]
    public async Task Jogador_NaoCriaEditaDuplicaNemRemoveToken()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (jogador, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id);
        var estado = await mestre.CriarMapaAtivoAsync(mesa.Id);
        var token = await mestre.CriarTokenAsync(estado.Mapa.Id, x: 5, y: 5);

        Assert.Equal(HttpStatusCode.Forbidden, (await jogador.PostarTokenAsync(estado.Mapa.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await jogador.Client.PutAsJsonAsync($"/api/tokens/{token.Id}", Edicao)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await jogador.Client.PostAsync($"/api/tokens/{token.Id}/duplicar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await jogador.Client.DeleteAsync($"/api/tokens/{token.Id}")).StatusCode);
    }

    [Fact]
    public async Task CriarToken_SemPosicao_UsaAPrimeiraCelulaLivre()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var mapa = await mestre.CriarMapaAsync(mesa.Id);
        await mestre.CriarTokenAsync(mapa.Id, x: 0, y: 0);

        var segundo = await mestre.CriarTokenAsync(mapa.Id);
        var terceiro = await mestre.CriarTokenAsync(mapa.Id);

        Assert.Equal((1, 0), (segundo.X, segundo.Y));
        Assert.Equal((2, 0), (terceiro.X, terceiro.Y));
    }

    [Fact]
    public async Task CriarToken_EmCelulaOcupadaOuForaDoMapa_ERejeitado()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var mapa = await mestre.CriarMapaAsync(mesa.Id, largura: 10, altura: 8);
        await mestre.CriarTokenAsync(mapa.Id, x: 2, y: 2);

        Assert.Equal(HttpStatusCode.Conflict, (await mestre.PostarTokenAsync(mapa.Id, x: 2, y: 2)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await mestre.PostarTokenAsync(mapa.Id, x: 10, y: 0)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await mestre.PostarTokenAsync(mapa.Id, x: 0, y: -1)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await mestre.PostarTokenAsync(mapa.Id, x: 3, y: null)).StatusCode);
    }

    [Theory]
    [InlineData(TipoToken.Inimigo, "Mago")]
    [InlineData(TipoToken.Npc, "nao-existe")]
    [InlineData(TipoToken.Personagem, "goblin")]
    public async Task CriarToken_ComIconeQueNaoEDoTipo_Retorna400(TipoToken tipo, string icone)
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var mapa = await mestre.CriarMapaAsync(mesa.Id);

        var resposta = await mestre.PostarTokenAsync(mapa.Id, tipo: tipo, icone: icone);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Mestre_CriaTokenAvulsoDePersonagem_ComIconeDeClasseESemDono()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var mapa = await mestre.CriarMapaAsync(mesa.Id);

        var resposta = await mestre.PostarTokenAsync(mapa.Id, "Aliado", tipo: TipoToken.Personagem, icone: "Paladino");
        var token = (await resposta.Content.ReadFromJsonAsync<TokenDto>(ApiTestHelpers.Json))!;
        var comIconeGenerico = await mestre.Client.PutAsJsonAsync($"/api/tokens/{token.Id}", Edicao);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.Equal((TipoToken.Personagem, "Paladino"), (token.Tipo, token.Icone));
        Assert.Null(token.DonoUsuarioId);
        Assert.Null(token.PersonagemId);
        Assert.Equal(HttpStatusCode.BadRequest, comIconeGenerico.StatusCode);
    }

    [Fact]
    public async Task Duplicar_CriaACopiaNaCelulaLivreMaisProxima()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var mapa = await mestre.CriarMapaAsync(mesa.Id);
        var original = await mestre.CriarTokenAsync(mapa.Id, "Lobo", x: 0, y: 0);

        var primeira = await DuplicarAsync(mestre, original.Id);
        var segunda = await DuplicarAsync(mestre, original.Id);

        Assert.Equal(("Lobo", 7, 7, "goblin"), (primeira.Nome, primeira.HpAtual, primeira.HpMaximo, primeira.Icone));
        Assert.NotEqual(original.Id, primeira.Id);
        // No canto, o anel de distância 1 tem só três células; a ordem é de cima para baixo, da esquerda para a direita.
        Assert.Equal((1, 0), (primeira.X, primeira.Y));
        Assert.Equal((0, 1), (segunda.X, segunda.Y));
    }

    [Fact]
    public async Task TokenDeJogador_NaoEDuplicadoNemRemovido_ESoOHpAtualEAVisibilidadeSaoEditaveis()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (jogador, personagem) = await factory.AdicionarJogadorAsync(mestre, mesa.Id);
        var estado = await mestre.CriarMapaAtivoAsync(mesa.Id);
        var token = estado.TokenDe(jogador)!;

        var duplicar = await mestre.Client.PostAsync($"/api/tokens/{token.Id}/duplicar", null);
        var remover = await mestre.Client.DeleteAsync($"/api/tokens/{token.Id}");
        var editar = await mestre.Client.PutAsJsonAsync($"/api/tokens/{token.Id}", new EditarTokenRequest("Outro nome", 5, 99, "Bardo", "#000000", false));
        var hpAcimaDoMaximo = await mestre.Client.PutAsJsonAsync($"/api/tokens/{token.Id}", new EditarTokenRequest("Outro nome", 50, 99, "Bardo", "#000000", null));

        Assert.Equal(HttpStatusCode.Conflict, duplicar.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, remover.StatusCode);
        Assert.Equal(HttpStatusCode.OK, editar.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, hpAcimaDoMaximo.StatusCode);
        var editado = (await mestre.ObterEstadoDoMapaAsync(estado.Mapa.Id)).TokenDe(jogador)!;
        Assert.Equal((personagem.Nome, 5, personagem.HpMaximo, "Mago", false), (editado.Nome, editado.HpAtual, editado.HpMaximo, editado.Icone, editado.Visivel));
    }

    private static async Task<TokenDto> DuplicarAsync(Sessao mestre, Guid tokenId)
    {
        var resposta = await mestre.Client.PostAsync($"/api/tokens/{tokenId}/duplicar", null);
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<TokenDto>(ApiTestHelpers.Json))!;
    }
}
