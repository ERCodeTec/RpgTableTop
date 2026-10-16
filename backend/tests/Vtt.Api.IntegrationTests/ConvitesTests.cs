using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vtt.Application.Mesas;
using Vtt.Domain.Mesas;
using Vtt.Infrastructure.Persistence;

namespace Vtt.Api.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class ConvitesTests(VttApiFactory factory)
{
    private async Task AlterarConviteNoBancoAsync(string codigo, Action<Convite> alterar)
    {
        await using var escopo = factory.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        var convite = await db.Convites.SingleAsync(c => c.Codigo == codigo);
        alterar(convite);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Gerar_CriaCodigoDe8CaracteresSemAmbiguosEComAUrlPublica()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();

        var resposta = await mestre.Client.PostAsJsonAsync($"/api/mesas/{mesa.Id}/convites", new ConviteRequest(24, 5));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var convite = await resposta.Content.ReadFromJsonAsync<ConviteDto>(ApiTestHelpers.Json);
        Assert.NotNull(convite);
        Assert.Equal(8, convite.Codigo.Length);
        Assert.All(convite.Codigo, caractere => Assert.Contains(caractere, Convite.Alfabeto));
        Assert.DoesNotContain(convite.Codigo, caractere => "0O1IL".Contains(caractere));
        Assert.Equal($"{VttApiFactory.UrlPublica}/convite/{convite.Codigo}", convite.Url);
        Assert.Equal(5, convite.UsosMaximos);
        Assert.Equal(0, convite.UsosAtuais);
        Assert.True(convite.Valido);
        Assert.InRange(convite.ExpiraEm!.Value, DateTimeOffset.UtcNow.AddHours(23), DateTimeOffset.UtcNow.AddHours(25));
    }

    [Fact]
    public async Task Gerar_SemCorpo_CriaConviteSemLimites()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();

        var resposta = await mestre.Client.PostAsync($"/api/mesas/{mesa.Id}/convites", null);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var convite = await resposta.Content.ReadFromJsonAsync<ConviteDto>(ApiTestHelpers.Json);
        Assert.Null(convite!.ExpiraEm);
        Assert.Null(convite.UsosMaximos);
    }

    [Fact]
    public async Task Gerar_NovoConvite_MantemOAnteriorValido()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var primeiro = await mestre.GerarConviteAsync(mesa.Id);
        var segundo = await mestre.GerarConviteAsync(mesa.Id);
        var jogador = await factory.CriarSessaoAsync();

        var lista = await mestre.Client.GetFromJsonAsync<List<ConviteDto>>($"/api/mesas/{mesa.Id}/convites", ApiTestHelpers.Json);

        Assert.NotEqual(primeiro.Codigo, segundo.Codigo);
        Assert.Equal(2, lista!.Count);
        Assert.All(lista, convite => Assert.True(convite.Valido));
        Assert.Equal(HttpStatusCode.OK, (await jogador.Client.GetAsync($"/api/convites/{primeiro.Codigo}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await jogador.Client.GetAsync($"/api/convites/{segundo.Codigo}")).StatusCode);
    }

    [Fact]
    public async Task Jogador_NaoGeraNaoListaNemRevogaConvites()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (jogador, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id);
        var convite = await mestre.GerarConviteAsync(mesa.Id);

        var geracao = await jogador.Client.PostAsJsonAsync($"/api/mesas/{mesa.Id}/convites", new ConviteRequest(null, null));
        var listagem = await jogador.Client.GetAsync($"/api/mesas/{mesa.Id}/convites");
        var revogacao = await jogador.Client.DeleteAsync($"/api/convites/{convite.Codigo}");
        var revogacaoPorEstranho = await (await factory.CriarSessaoAsync()).Client.DeleteAsync($"/api/convites/{convite.Codigo}");

        Assert.Equal(HttpStatusCode.Forbidden, geracao.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, listagem.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, revogacao.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, revogacaoPorEstranho.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await jogador.Client.GetAsync($"/api/convites/{convite.Codigo}")).StatusCode);
    }

    [Fact]
    public async Task PreVisualizar_MostraMesaMestreEJogadores_EExigeLogin()
    {
        var mestre = await factory.CriarSessaoAsync("Mestre Edu");
        var mesa = await mestre.CriarMesaAsync("Curse of Strahd");
        await factory.AdicionarJogadorAsync(mestre, mesa.Id);
        var convite = await mestre.GerarConviteAsync(mesa.Id);
        var visitante = await factory.CriarSessaoAsync();

        var semLogin = await factory.CreateClient().GetAsync($"/api/convites/{convite.Codigo}");
        var previa = await visitante.Client.GetFromJsonAsync<ConvitePreviaDto>(
            $"/api/convites/{convite.Codigo.ToLowerInvariant()}", ApiTestHelpers.Json);
        var previaDoMestre = await mestre.Client.GetFromJsonAsync<ConvitePreviaDto>($"/api/convites/{convite.Codigo}", ApiTestHelpers.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, semLogin.StatusCode);
        Assert.Equal(new ConvitePreviaDto(convite.Codigo, mesa.Id, "Curse of Strahd", null, "Mestre Edu", 1, false), previa);
        Assert.True(previaDoMestre!.JaSouMembro);
    }

    [Fact]
    public async Task Aceitar_EntraComoJogadorComOPersonagemEscolhido()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var convite = await mestre.GerarConviteAsync(mesa.Id);
        var jogador = await factory.CriarSessaoAsync("Alice");
        var personagem = await jogador.CriarPersonagemAsync("Lia");

        var resposta = await jogador.AceitarConviteAsync(convite.Codigo, personagem.Id);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(mesa.Id, (await resposta.Content.ReadFromJsonAsync<AceitarConviteResponse>())!.MesaId);
        var detalhe = await jogador.ObterMesaAsync(mesa.Id);
        Assert.Equal(PapelNaMesa.Jogador, detalhe!.MeuPapel);
        var membro = detalhe.Membros.Single(m => m.UsuarioId == jogador.UsuarioId);
        Assert.Equal("Alice", membro.NomeExibicao);
        Assert.Equal(PapelNaMesa.Jogador, membro.Papel);
        Assert.Equal(personagem.Id, membro.PersonagemAtivo!.Id);
        Assert.Equal("Lia", membro.PersonagemAtivo.Nome);
    }

    [Fact]
    public async Task Aceitar_ComPersonagemDeOutroUsuario_Retorna400ENaoEntra()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var convite = await mestre.GerarConviteAsync(mesa.Id);
        var jogador = await factory.CriarSessaoAsync();
        var alheio = await (await factory.CriarSessaoAsync()).CriarPersonagemAsync("Alheio");

        var resposta = await jogador.AceitarConviteAsync(convite.Codigo, alheio.Id);
        var semPersonagem = await jogador.AceitarConviteAsync(convite.Codigo, Guid.Empty);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var problema = await resposta.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("personagemId", problema!.Errors.Keys);
        Assert.Equal(HttpStatusCode.BadRequest, semPersonagem.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await jogador.Client.GetAsync($"/api/mesas/{mesa.Id}")).StatusCode);
        var lista = await mestre.Client.GetFromJsonAsync<List<ConviteDto>>($"/api/mesas/{mesa.Id}/convites", ApiTestHelpers.Json);
        Assert.Equal(0, lista!.Single().UsosAtuais);
    }

    [Fact]
    public async Task Aceitar_DeNovo_EIdempotente_SoTrocaOPersonagemESoContaUmUso()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var convite = await mestre.GerarConviteAsync(mesa.Id);
        var jogador = await factory.CriarSessaoAsync();
        var primeiro = await jogador.CriarPersonagemAsync("Primeiro");
        var segundo = await jogador.CriarPersonagemAsync("Segundo");

        (await jogador.AceitarConviteAsync(convite.Codigo, primeiro.Id)).EnsureSuccessStatusCode();
        var repeticao = await jogador.AceitarConviteAsync(convite.Codigo, primeiro.Id);
        var comOutroPersonagem = await jogador.AceitarConviteAsync(convite.Codigo, segundo.Id);

        Assert.Equal(HttpStatusCode.OK, repeticao.StatusCode);
        Assert.Equal(HttpStatusCode.OK, comOutroPersonagem.StatusCode);
        var detalhe = await mestre.ObterMesaAsync(mesa.Id);
        Assert.Equal(2, detalhe!.Membros.Count);
        Assert.Equal(segundo.Id, detalhe.Membros.Single(m => m.UsuarioId == jogador.UsuarioId).PersonagemAtivo!.Id);
        var lista = await mestre.Client.GetFromJsonAsync<List<ConviteDto>>($"/api/mesas/{mesa.Id}/convites", ApiTestHelpers.Json);
        Assert.Equal(1, lista!.Single().UsosAtuais);
    }

    [Fact]
    public async Task Aceitar_DeNovoComASessaoAtiva_NaoBurlaABloqueioDeTroca()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var convite = await mestre.GerarConviteAsync(mesa.Id);
        var jogador = await factory.CriarSessaoAsync();
        var primeiro = await jogador.CriarPersonagemAsync("Primeiro");
        var segundo = await jogador.CriarPersonagemAsync("Segundo");
        (await jogador.AceitarConviteAsync(convite.Codigo, primeiro.Id)).EnsureSuccessStatusCode();
        (await mestre.Client.PutAsJsonAsync($"/api/mesas/{mesa.Id}/sessao", new SessaoRequest(true))).EnsureSuccessStatusCode();

        var mesmoPersonagem = await jogador.AceitarConviteAsync(convite.Codigo, primeiro.Id);
        var outroPersonagem = await jogador.AceitarConviteAsync(convite.Codigo, segundo.Id);

        Assert.Equal(HttpStatusCode.OK, mesmoPersonagem.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, outroPersonagem.StatusCode);
    }

    [Fact]
    public async Task ConviteRevogado_Retorna404NaPreviaENoAceite_MasQuemJaEntrouContinua()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var convite = await mestre.GerarConviteAsync(mesa.Id);
        var (membro, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id);
        var jogador = await factory.CriarSessaoAsync();
        var personagem = await jogador.CriarPersonagemAsync();

        var revogacao = await mestre.Client.DeleteAsync($"/api/convites/{convite.Codigo}");
        var revogacaoRepetida = await mestre.Client.DeleteAsync($"/api/convites/{convite.Codigo}");

        Assert.Equal(HttpStatusCode.NoContent, revogacao.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, revogacaoRepetida.StatusCode);
        await AssertConviteIndisponivelAsync(jogador, convite.Codigo, personagem.Id);
        Assert.Equal(HttpStatusCode.OK, (await membro.Client.GetAsync($"/api/mesas/{mesa.Id}")).StatusCode);
        var lista = await mestre.Client.GetFromJsonAsync<List<ConviteDto>>($"/api/mesas/{mesa.Id}/convites", ApiTestHelpers.Json);
        var revogado = lista!.Single(c => c.Codigo == convite.Codigo);
        Assert.False(revogado.Valido);
        Assert.NotNull(revogado.RevogadoEm);
    }

    [Fact]
    public async Task ConviteExpirado_Retorna404NaPreviaENoAceite()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var convite = await mestre.GerarConviteAsync(mesa.Id, validadeHoras: 1);
        var jogador = await factory.CriarSessaoAsync();
        var personagem = await jogador.CriarPersonagemAsync();
        Assert.Equal(HttpStatusCode.OK, (await jogador.Client.GetAsync($"/api/convites/{convite.Codigo}")).StatusCode);

        await AlterarConviteNoBancoAsync(convite.Codigo, c => c.ExpiraEm = DateTimeOffset.UtcNow.AddMinutes(-1));

        await AssertConviteIndisponivelAsync(jogador, convite.Codigo, personagem.Id);
    }

    [Fact]
    public async Task ConviteEsgotado_Retorna404ParaOProximoJogador()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var convite = await mestre.GerarConviteAsync(mesa.Id, usosMaximos: 1);
        var primeiro = await factory.CriarSessaoAsync();
        var segundo = await factory.CriarSessaoAsync();
        var personagemDoSegundo = await segundo.CriarPersonagemAsync();

        (await primeiro.AceitarConviteAsync(convite.Codigo, (await primeiro.CriarPersonagemAsync()).Id)).EnsureSuccessStatusCode();

        await AssertConviteIndisponivelAsync(segundo, convite.Codigo, personagemDoSegundo.Id);
        Assert.Equal(2, (await mestre.ObterMesaAsync(mesa.Id))!.Membros.Count);
    }

    [Fact]
    public async Task ConviteComUmaVaga_DisputadoEmParalelo_SoUmJogadorEntra()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var convite = await mestre.GerarConviteAsync(mesa.Id, usosMaximos: 1);
        var jogadores = new List<(Sessao Sessao, Guid PersonagemId)>();
        for (var i = 0; i < 6; i++)
        {
            var sessao = await factory.CriarSessaoAsync();
            jogadores.Add((sessao, (await sessao.CriarPersonagemAsync()).Id));
        }

        var respostas = await Task.WhenAll(jogadores.Select(j => j.Sessao.AceitarConviteAsync(convite.Codigo, j.PersonagemId)));

        Assert.Single(respostas, resposta => resposta.StatusCode == HttpStatusCode.OK);
        Assert.Equal(5, respostas.Count(resposta => resposta.StatusCode == HttpStatusCode.NotFound));
        Assert.Equal(2, (await mestre.ObterMesaAsync(mesa.Id))!.Membros.Count);
    }

    [Fact]
    public async Task ConviteInexistente_Retorna404()
    {
        var jogador = await factory.CriarSessaoAsync();
        var personagem = await jogador.CriarPersonagemAsync();

        await AssertConviteIndisponivelAsync(jogador, "ZZZZZZZZ", personagem.Id);
    }

    /// <summary>Prévia e aceite respondem o mesmo 404, com a mesma mensagem, seja qual for o motivo.</summary>
    private static async Task AssertConviteIndisponivelAsync(Sessao jogador, string codigo, Guid personagemId)
    {
        var previa = await jogador.Client.GetAsync($"/api/convites/{codigo}");
        var aceite = await jogador.AceitarConviteAsync(codigo, personagemId);

        Assert.Equal(HttpStatusCode.NotFound, previa.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, aceite.StatusCode);
        Assert.Equal("Convite inválido ou não mais disponível.", (await previa.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);
        Assert.Equal("Convite inválido ou não mais disponível.", (await aceite.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);
    }
}
