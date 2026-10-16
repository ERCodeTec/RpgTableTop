using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Vtt.Application.Mapas;
using Vtt.Application.Personagens;

namespace Vtt.Api.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class FotoDoPersonagemTests(VttApiFactory factory)
{
    [Fact]
    public async Task EnviarFoto_DefineAImagem_ETrocar_ApagaAAnterior()
    {
        var jogador = await factory.CriarSessaoAsync();
        var personagem = await jogador.CriarPersonagemAsync();

        var primeira = await EnviarFotoAsync(jogador, personagem.Id);
        Assert.NotNull(primeira.ImagemId);
        Assert.Equal(HttpStatusCode.OK, (await jogador.Client.GetAsync($"/api/imagens/{primeira.ImagemId}")).StatusCode);

        var segunda = await EnviarFotoAsync(jogador, personagem.Id, ImagensDeTeste.Webp());
        Assert.NotNull(segunda.ImagemId);
        Assert.NotEqual(primeira.ImagemId, segunda.ImagemId);
        Assert.Equal(HttpStatusCode.NotFound, (await jogador.Client.GetAsync($"/api/imagens/{primeira.ImagemId}")).StatusCode);

        var listado = await jogador.Client.GetFromJsonAsync<PersonagemDto>($"/api/personagens/{personagem.Id}", ApiTestHelpers.Json);
        Assert.Equal(segunda.ImagemId, listado!.ImagemId);
    }

    [Fact]
    public async Task RemoverFoto_ZeraAImagem_EApagaOArquivo()
    {
        var jogador = await factory.CriarSessaoAsync();
        var personagem = await jogador.CriarPersonagemAsync();
        var comFoto = await EnviarFotoAsync(jogador, personagem.Id);

        var resposta = await jogador.Client.DeleteAsync($"/api/personagens/{personagem.Id}/foto");

        resposta.EnsureSuccessStatusCode();
        Assert.Null((await resposta.Content.ReadFromJsonAsync<PersonagemDto>(ApiTestHelpers.Json))!.ImagemId);
        Assert.Equal(HttpStatusCode.NotFound, (await jogador.Client.GetAsync($"/api/imagens/{comFoto.ImagemId}")).StatusCode);
    }

    [Fact]
    public async Task ArquivoQueNaoEImagem_ERecusado()
    {
        var jogador = await factory.CriarSessaoAsync();
        var personagem = await jogador.CriarPersonagemAsync();

        var resposta = await PutFotoAsync(jogador, personagem.Id, "não é imagem"u8.ToArray());

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task OutroUsuario_NaoEnviaNemRemoveAFoto()
    {
        var dono = await factory.CriarSessaoAsync();
        var outro = await factory.CriarSessaoAsync();
        var personagem = await dono.CriarPersonagemAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await PutFotoAsync(outro, personagem.Id, ImagensDeTeste.Png())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outro.Client.DeleteAsync($"/api/personagens/{personagem.Id}/foto")).StatusCode);
    }

    [Fact]
    public async Task TokenDoJogador_EspelhaAFoto_QueAMesaInteiraBaixa_EQuemEstaForaNao()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (alice, personagem) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Alice");
        var (bob, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Bob");
        var estranho = await factory.CriarSessaoAsync();
        var mapa = (await mestre.CriarMapaAtivoAsync(mesa.Id)).Mapa;

        var comFoto = await EnviarFotoAsync(alice, personagem.Id);

        var token = (await bob.ObterEstadoDoMapaAsync(mapa.Id)).TokenDe(alice)!;
        Assert.Equal(comFoto.ImagemId, token.ImagemId);
        foreach (var sessao in new[] { mestre, bob })
        {
            Assert.Equal(HttpStatusCode.OK, (await sessao.Client.GetAsync($"/api/imagens/{token.ImagemId}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await sessao.Client.GetAsync($"/api/imagens/{token.ImagemId}/miniatura")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.NotFound, (await estranho.Client.GetAsync($"/api/imagens/{token.ImagemId}")).StatusCode);

        // Oculto, o token some para os outros jogadores, e a foto junto; o Mestre continua vendo.
        (await mestre.Client.PutAsJsonAsync($"/api/tokens/{token.Id}/exibicao", new { visivel = false }, ApiTestHelpers.Json)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await bob.Client.GetAsync($"/api/imagens/{token.ImagemId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await mestre.Client.GetAsync($"/api/imagens/{token.ImagemId}")).StatusCode);

        (await alice.Client.DeleteAsync($"/api/personagens/{personagem.Id}/foto")).EnsureSuccessStatusCode();
        Assert.Null((await mestre.ObterEstadoDoMapaAsync(mapa.Id)).TokenDe(alice)!.ImagemId);
    }

    [Fact]
    public async Task JogadorQueEntraDepois_GanhaTokenComAFoto()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var mapa = (await mestre.CriarMapaAtivoAsync(mesa.Id)).Mapa;
        var jogador = await factory.CriarSessaoAsync("Alice");
        var personagem = await jogador.CriarPersonagemAsync();
        var comFoto = await EnviarFotoAsync(jogador, personagem.Id);
        var convite = await mestre.GerarConviteAsync(mesa.Id);

        (await jogador.AceitarConviteAsync(convite.Codigo, personagem.Id)).EnsureSuccessStatusCode();

        Assert.Equal(comFoto.ImagemId, (await mestre.ObterEstadoDoMapaAsync(mapa.Id)).TokenDe(jogador)!.ImagemId);
    }

    [Fact]
    public async Task OMestre_NaoTrocaAImagemDoTokenDoJogador()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (jogador, personagem) = await factory.AdicionarJogadorAsync(mestre, mesa.Id);
        var mapa = (await mestre.CriarMapaAtivoAsync(mesa.Id)).Mapa;
        var comFoto = await EnviarFotoAsync(jogador, personagem.Id);
        var daMesa = await mestre.CriarImagemAsync(mesa.Id);
        var token = (await mestre.ObterEstadoDoMapaAsync(mapa.Id)).TokenDe(jogador)!;

        var resposta = await mestre.Client.PutAsJsonAsync(
            $"/api/tokens/{token.Id}", new EditarTokenRequest(token.Nome, 5, token.HpMaximo!.Value, token.Icone, token.CorBorda, null, daMesa.Id), ApiTestHelpers.Json);

        resposta.EnsureSuccessStatusCode();
        Assert.Equal(comFoto.ImagemId, (await mestre.ObterEstadoDoMapaAsync(mapa.Id)).TokenDe(jogador)!.ImagemId);
    }

    [Fact]
    public async Task ExcluirOPersonagem_ApagaAFoto()
    {
        var jogador = await factory.CriarSessaoAsync();
        var personagem = await jogador.CriarPersonagemAsync();
        var comFoto = await EnviarFotoAsync(jogador, personagem.Id);

        (await jogador.Client.DeleteAsync($"/api/personagens/{personagem.Id}")).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NotFound, (await jogador.Client.GetAsync($"/api/imagens/{comFoto.ImagemId}")).StatusCode);
    }

    [Fact]
    public async Task AFotoEmUso_NaoSaiPelaGaleria()
    {
        var jogador = await factory.CriarSessaoAsync();
        var personagem = await jogador.CriarPersonagemAsync();
        var comFoto = await EnviarFotoAsync(jogador, personagem.Id);

        var resposta = await jogador.Client.DeleteAsync($"/api/imagens/{comFoto.ImagemId}");

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }

    private static async Task<HttpResponseMessage> PutFotoAsync(Sessao sessao, Guid personagemId, byte[] conteudo)
    {
        using var formulario = new MultipartFormDataContent();
        var arquivo = new ByteArrayContent(conteudo);
        arquivo.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        formulario.Add(arquivo, "arquivo", "retrato.png");
        return await sessao.Client.PutAsync($"/api/personagens/{personagemId}/foto", formulario);
    }

    private static async Task<PersonagemDto> EnviarFotoAsync(Sessao sessao, Guid personagemId, byte[]? conteudo = null)
    {
        var resposta = await PutFotoAsync(sessao, personagemId, conteudo ?? ImagensDeTeste.Png());
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<PersonagemDto>(ApiTestHelpers.Json))!;
    }
}
