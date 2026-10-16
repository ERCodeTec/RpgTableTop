using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Vtt.Application.Mesas;
using Vtt.Domain.Mesas;

namespace Vtt.Api.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class MesasTests(VttApiFactory factory)
{
    [Fact]
    public async Task Criar_TornaOCriadorMestreDaMesa()
    {
        var mestre = await factory.CriarSessaoAsync("Mestre Edu");

        var resposta = await mestre.Client.PostAsJsonAsync("/api/mesas", new MesaRequest("  Tumba da Aniquilação ", "  Selva e armadilhas  "));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var mesa = await resposta.Content.ReadFromJsonAsync<MesaDetalheDto>(ApiTestHelpers.Json);
        Assert.NotNull(mesa);
        Assert.Equal("Tumba da Aniquilação", mesa.Nome);
        Assert.Equal("Selva e armadilhas", mesa.Descricao);
        Assert.False(mesa.SessaoAtiva);
        Assert.Equal(PapelNaMesa.Mestre, mesa.MeuPapel);
        var membro = Assert.Single(mesa.Membros);
        Assert.Equal(mestre.UsuarioId, membro.UsuarioId);
        Assert.Equal("Mestre Edu", membro.NomeExibicao);
        Assert.Equal(PapelNaMesa.Mestre, membro.Papel);
        Assert.Null(membro.PersonagemAtivo);
    }

    [Theory]
    [InlineData("", "nome")]
    [InlineData("   ", "nome")]
    public async Task Criar_SemNome_Retorna400(string nome, string campo)
    {
        var mestre = await factory.CriarSessaoAsync();

        var resposta = await mestre.Client.PostAsJsonAsync("/api/mesas", new MesaRequest(nome, null));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var problema = await resposta.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(campo, problema!.Errors.Keys);
    }

    [Fact]
    public async Task Listar_RetornaSoAsMesasEmQueSouMembroComOMeuPapel()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesaDoMestre = await mestre.CriarMesaAsync("Do Mestre");
        var (jogador, _) = await factory.AdicionarJogadorAsync(mestre, mesaDoMestre.Id);
        var mesaDoJogador = await jogador.CriarMesaAsync("Do Jogador");
        var estranho = await factory.CriarSessaoAsync();
        await estranho.CriarMesaAsync("De outra pessoa");

        var mesas = await jogador.Client.GetFromJsonAsync<List<MesaResumoDto>>("/api/mesas", ApiTestHelpers.Json);

        Assert.NotNull(mesas);
        Assert.Equal(2, mesas.Count);
        Assert.Equal(PapelNaMesa.Jogador, mesas.Single(m => m.Id == mesaDoMestre.Id).MeuPapel);
        Assert.Equal(2, mesas.Single(m => m.Id == mesaDoMestre.Id).QuantidadeMembros);
        Assert.Equal(PapelNaMesa.Mestre, mesas.Single(m => m.Id == mesaDoJogador.Id).MeuPapel);
    }

    [Fact]
    public async Task NaoMembro_NaoAcessaNadaDaMesa()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var estranho = await factory.CriarSessaoAsync();
        var url = $"/api/mesas/{mesa.Id}";

        Assert.Equal(HttpStatusCode.NotFound, (await estranho.Client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await estranho.Client.PutAsJsonAsync(url, new MesaRequest("Invadida", null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await estranho.Client.DeleteAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await estranho.Client.GetAsync($"{url}/convites")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await estranho.Client.PostAsJsonAsync($"{url}/convites", new ConviteRequest(null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await estranho.Client.PutAsJsonAsync($"{url}/sessao", new SessaoRequest(true))).StatusCode);

        Assert.Equal("A Mina Perdida", (await mestre.ObterMesaAsync(mesa.Id))!.Nome);
    }

    [Fact]
    public async Task SemToken_Retorna401()
    {
        var resposta = await factory.CreateClient().GetAsync("/api/mesas");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Jogador_VeAMesaMasNaoEditaNemExcluiNemControlaASessao()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (jogador, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id);
        var url = $"/api/mesas/{mesa.Id}";

        var detalhe = await jogador.ObterMesaAsync(mesa.Id);
        Assert.Equal(PapelNaMesa.Jogador, detalhe!.MeuPapel);
        Assert.Equal(2, detalhe.Membros.Count);

        var edicao = await jogador.Client.PutAsJsonAsync(url, new MesaRequest("Renomeada", null));
        Assert.Equal(HttpStatusCode.Forbidden, edicao.StatusCode);
        Assert.Equal("application/problem+json", edicao.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.Forbidden, (await jogador.Client.DeleteAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await jogador.Client.PutAsJsonAsync($"{url}/sessao", new SessaoRequest(true))).StatusCode);

        var depois = await mestre.ObterMesaAsync(mesa.Id);
        Assert.Equal("A Mina Perdida", depois!.Nome);
        Assert.False(depois.SessaoAtiva);
    }

    [Fact]
    public async Task Mestre_EditaInciaSessaoEExcluiEmCascata()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (jogador, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id);
        var convite = await mestre.GerarConviteAsync(mesa.Id);
        var url = $"/api/mesas/{mesa.Id}";

        var edicao = await mestre.Client.PutAsJsonAsync(url, new MesaRequest("Novo nome", "Nova descrição"));
        var sessao = await mestre.Client.PutAsJsonAsync($"{url}/sessao", new SessaoRequest(true));

        Assert.Equal(HttpStatusCode.OK, edicao.StatusCode);
        Assert.Equal(HttpStatusCode.OK, sessao.StatusCode);
        var atualizada = await jogador.ObterMesaAsync(mesa.Id);
        Assert.Equal("Novo nome", atualizada!.Nome);
        Assert.Equal("Nova descrição", atualizada.Descricao);
        Assert.True(atualizada.SessaoAtiva);

        var exclusao = await mestre.Client.DeleteAsync(url);

        Assert.Equal(HttpStatusCode.NoContent, exclusao.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await mestre.Client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await jogador.Client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await jogador.Client.GetAsync($"/api/convites/{convite.Codigo}")).StatusCode);
        // Os personagens dos jogadores não são da mesa e continuam existindo.
        Assert.Single((await jogador.Client.GetFromJsonAsync<List<object>>("/api/personagens"))!);
    }

    [Fact]
    public async Task Jogador_TrocaOProprioPersonagemSoComASessaoEncerrada()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (jogador, original) = await factory.AdicionarJogadorAsync(mestre, mesa.Id);
        var outro = await jogador.CriarPersonagemAsync("Reserva");
        var url = $"/api/mesas/{mesa.Id}/meu-personagem";

        var comSessaoEncerrada = await jogador.Client.PutAsJsonAsync(url, new DefinirPersonagemRequest(outro.Id));
        Assert.Equal(HttpStatusCode.OK, comSessaoEncerrada.StatusCode);

        (await mestre.Client.PutAsJsonAsync($"/api/mesas/{mesa.Id}/sessao", new SessaoRequest(true))).EnsureSuccessStatusCode();
        var comSessaoAtiva = await jogador.Client.PutAsJsonAsync(url, new DefinirPersonagemRequest(original.Id));

        Assert.Equal(HttpStatusCode.Conflict, comSessaoAtiva.StatusCode);
        var problema = await comSessaoAtiva.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("sessão está ativa", problema!.Detail);
        var membro = (await mestre.ObterMesaAsync(mesa.Id))!.Membros.Single(m => m.UsuarioId == jogador.UsuarioId);
        Assert.Equal(outro.Id, membro.PersonagemAtivo!.Id);
    }

    [Fact]
    public async Task Jogador_NaoUsaPersonagemDeOutroUsuario()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (jogador, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id);
        var alheio = await (await factory.CriarSessaoAsync()).CriarPersonagemAsync("Alheio");

        var resposta = await jogador.Client.PutAsJsonAsync(
            $"/api/mesas/{mesa.Id}/meu-personagem", new DefinirPersonagemRequest(alheio.Id));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var problema = await resposta.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("personagemId", problema!.Errors.Keys);
    }

    [Fact]
    public async Task Mestre_TrocaOPersonagemDoJogadorMesmoComASessaoAtiva()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (jogador, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id);
        var outro = await jogador.CriarPersonagemAsync("Reserva");
        var doMestre = await mestre.CriarPersonagemAsync("NPC do Mestre");
        (await mestre.Client.PutAsJsonAsync($"/api/mesas/{mesa.Id}/sessao", new SessaoRequest(true))).EnsureSuccessStatusCode();
        var url = $"/api/mesas/{mesa.Id}/membros/{jogador.UsuarioId}";

        var disponiveis = await mestre.Client.GetFromJsonAsync<List<PersonagemResumoDto>>($"{url}/personagens", ApiTestHelpers.Json);
        var troca = await mestre.Client.PutAsJsonAsync($"{url}/personagem", new DefinirPersonagemRequest(outro.Id));
        var comPersonagemDeOutro = await mestre.Client.PutAsJsonAsync($"{url}/personagem", new DefinirPersonagemRequest(doMestre.Id));
        var peloJogador = await jogador.Client.PutAsJsonAsync($"{url}/personagem", new DefinirPersonagemRequest(outro.Id));
        var listaPeloJogador = await jogador.Client.GetAsync($"{url}/personagens");

        Assert.Equal(2, disponiveis!.Count);
        Assert.Equal(HttpStatusCode.OK, troca.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, comPersonagemDeOutro.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, peloJogador.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, listaPeloJogador.StatusCode);
        var membro = (await mestre.ObterMesaAsync(mesa.Id))!.Membros.Single(m => m.UsuarioId == jogador.UsuarioId);
        Assert.Equal(outro.Id, membro.PersonagemAtivo!.Id);
    }

    [Fact]
    public async Task Mestre_RemoveJogador_EJogadorNaoRemoveOutro()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (alice, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Alice");
        var (bob, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Bob");
        var url = $"/api/mesas/{mesa.Id}/membros";

        var jogadorRemovendoOutro = await alice.Client.DeleteAsync($"{url}/{bob.UsuarioId}");
        var jogadorRemovendoMestre = await alice.Client.DeleteAsync($"{url}/{mestre.UsuarioId}");
        var mestreRemovendo = await mestre.Client.DeleteAsync($"{url}/{bob.UsuarioId}");
        var mestreRemovendoDeNovo = await mestre.Client.DeleteAsync($"{url}/{bob.UsuarioId}");

        Assert.Equal(HttpStatusCode.Forbidden, jogadorRemovendoOutro.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, jogadorRemovendoMestre.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, mestreRemovendo.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, mestreRemovendoDeNovo.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.Client.GetAsync($"/api/mesas/{mesa.Id}")).StatusCode);
        Assert.Equal(2, (await mestre.ObterMesaAsync(mesa.Id))!.Membros.Count);
    }

    [Fact]
    public async Task Jogador_SaiDaMesa_MasOMestreNaoPodeSair()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (jogador, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id);
        var url = $"/api/mesas/{mesa.Id}/membros";

        var mestreSaindo = await mestre.Client.DeleteAsync($"{url}/{mestre.UsuarioId}");
        var jogadorSaindo = await jogador.Client.DeleteAsync($"{url}/{jogador.UsuarioId}");

        Assert.Equal(HttpStatusCode.Conflict, mestreSaindo.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, jogadorSaindo.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await jogador.Client.GetAsync($"/api/mesas/{mesa.Id}")).StatusCode);
        Assert.Single((await mestre.ObterMesaAsync(mesa.Id))!.Membros);
    }

    [Fact]
    public async Task ExcluirPersonagemAtivo_DeixaOMembroSemPersonagem()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (jogador, personagem) = await factory.AdicionarJogadorAsync(mestre, mesa.Id);

        (await jogador.Client.DeleteAsync($"/api/personagens/{personagem.Id}")).EnsureSuccessStatusCode();

        var membro = (await mestre.ObterMesaAsync(mesa.Id))!.Membros.Single(m => m.UsuarioId == jogador.UsuarioId);
        Assert.Null(membro.PersonagemAtivo);
    }
}
