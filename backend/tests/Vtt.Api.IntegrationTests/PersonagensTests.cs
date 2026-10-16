using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Vtt.Application.Personagens;
using Vtt.Domain.Personagens;

namespace Vtt.Api.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class PersonagensTests(VttApiFactory factory)
{
    private static PersonagemRequest RequestValido(string nome = "Lia") =>
        new(nome, ClasseDnd.Mago, 5, 32, "#3366cc");

    private static async Task<PersonagemDto> CriarAsync(HttpClient client, PersonagemRequest? request = null)
    {
        var resposta = await client.PostAsJsonAsync("/api/personagens", request ?? RequestValido(), ApiTestHelpers.Json);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<PersonagemDto>(ApiTestHelpers.Json))!;
    }

    [Fact]
    public async Task Listar_SemToken_Retorna401()
    {
        var resposta = await factory.CreateClient().GetAsync("/api/personagens");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Criar_ComDadosValidos_Retorna201EPersonagemNormalizado()
    {
        var client = await factory.CriarClienteAutenticadoAsync();

        var resposta = await client.PostAsJsonAsync(
            "/api/personagens", new PersonagemRequest("  Lia  ", ClasseDnd.Mago, 5, 32, "#3366cc"), ApiTestHelpers.Json);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var criado = await resposta.Content.ReadFromJsonAsync<PersonagemDto>(ApiTestHelpers.Json);
        Assert.NotNull(criado);
        Assert.Equal("Lia", criado.Nome);
        Assert.Equal(ClasseDnd.Mago, criado.Classe);
        Assert.Equal(5, criado.Nivel);
        Assert.Equal(32, criado.HpMaximo);
        Assert.Equal("#3366CC", criado.CorBorda);
        Assert.Equal($"/api/personagens/{criado.Id}", resposta.Headers.Location?.AbsolutePath);

        var obtido = await client.GetFromJsonAsync<PersonagemDto>($"/api/personagens/{criado.Id}", ApiTestHelpers.Json);
        Assert.Equal(criado.Id, obtido!.Id);
    }

    [Fact]
    public async Task Criar_SemCorDaBorda_UsaACorPadrao()
    {
        var client = await factory.CriarClienteAutenticadoAsync();

        var criado = await CriarAsync(client, new PersonagemRequest("Bruenor", ClasseDnd.Guerreiro, 1, 12, null));

        Assert.Equal(Personagem.CorBordaPadrao, criado.CorBorda);
    }

    [Fact]
    public async Task Criar_ComClasseEmTexto_SerializaAClassePeloNome()
    {
        var client = await factory.CriarClienteAutenticadoAsync();

        var resposta = await client.PostAsync("/api/personagens", JsonContent.Create(
            new { nome = "Kael", classe = "Patrulheiro", nivel = 3, hpMaximo = 24 }));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.Contains("\"classe\":\"Patrulheiro\"", await resposta.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("", 5, 30, "#112233", "nome")]
    [InlineData("Nome com mais de sessenta caracteres para estourar o limite definido", 5, 30, "#112233", "nome")]
    [InlineData("Lia", 0, 30, "#112233", "nivel")]
    [InlineData("Lia", 21, 30, "#112233", "nivel")]
    [InlineData("Lia", 5, 0, "#112233", "hpMaximo")]
    [InlineData("Lia", 5, 1000, "#112233", "hpMaximo")]
    [InlineData("Lia", 5, 30, "azul", "corBorda")]
    public async Task Criar_ComDadosInvalidos_Retorna400NoCampo(string nome, int nivel, int hpMaximo, string corBorda, string campo)
    {
        var client = await factory.CriarClienteAutenticadoAsync();

        var resposta = await client.PostAsJsonAsync(
            "/api/personagens", new PersonagemRequest(nome, ClasseDnd.Mago, nivel, hpMaximo, corBorda), ApiTestHelpers.Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var problema = await resposta.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(campo, problema!.Errors.Keys);
    }

    [Fact]
    public async Task Criar_ComClasseInexistente_Retorna400()
    {
        var client = await factory.CriarClienteAutenticadoAsync();

        var resposta = await client.PostAsync("/api/personagens", JsonContent.Create(
            new { nome = "Kael", classe = "Artifice", nivel = 3, hpMaximo = 24 }));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Listar_RetornaApenasOsPersonagensDoProprioUsuario()
    {
        var alice = await factory.CriarClienteAutenticadoAsync();
        var bob = await factory.CriarClienteAutenticadoAsync();
        var daAlice = await CriarAsync(alice, RequestValido("Da Alice"));
        var doBob = await CriarAsync(bob, RequestValido("Do Bob"));

        var listaDaAlice = await alice.GetFromJsonAsync<List<PersonagemDto>>("/api/personagens", ApiTestHelpers.Json);

        Assert.NotNull(listaDaAlice);
        Assert.Equal([daAlice.Id], listaDaAlice.Select(p => p.Id));
        Assert.DoesNotContain(listaDaAlice, p => p.Id == doBob.Id);
    }

    [Fact]
    public async Task Atualizar_OProprioPersonagem_AlteraOsCampos()
    {
        var client = await factory.CriarClienteAutenticadoAsync();
        var criado = await CriarAsync(client);

        var resposta = await client.PutAsJsonAsync(
            $"/api/personagens/{criado.Id}",
            new PersonagemRequest("Lia, a Sábia", ClasseDnd.Feiticeiro, 6, 40, "#AA0000"),
            ApiTestHelpers.Json);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var atualizado = await client.GetFromJsonAsync<PersonagemDto>($"/api/personagens/{criado.Id}", ApiTestHelpers.Json);
        Assert.Equal("Lia, a Sábia", atualizado!.Nome);
        Assert.Equal(ClasseDnd.Feiticeiro, atualizado.Classe);
        Assert.Equal(6, atualizado.Nivel);
        Assert.Equal(40, atualizado.HpMaximo);
        Assert.Equal("#AA0000", atualizado.CorBorda);
        Assert.Equal(criado.CriadoEm, atualizado.CriadoEm);
        Assert.True(atualizado.AtualizadoEm >= criado.AtualizadoEm);
    }

    [Fact]
    public async Task Excluir_OProprioPersonagem_Retorna204EDepois404()
    {
        var client = await factory.CriarClienteAutenticadoAsync();
        var criado = await CriarAsync(client);

        var exclusao = await client.DeleteAsync($"/api/personagens/{criado.Id}");
        var depois = await client.GetAsync($"/api/personagens/{criado.Id}");

        Assert.Equal(HttpStatusCode.NoContent, exclusao.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, depois.StatusCode);
    }

    [Fact]
    public async Task Obter_PersonagemInexistente_Retorna404ComProblemDetails()
    {
        var client = await factory.CriarClienteAutenticadoAsync();

        var resposta = await client.GetAsync($"/api/personagens/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task OutroUsuario_NaoConsegueLerAlterarNemExcluir()
    {
        var dona = await factory.CriarClienteAutenticadoAsync();
        var intruso = await factory.CriarClienteAutenticadoAsync();
        var original = await CriarAsync(dona, RequestValido("Intocável"));
        var url = $"/api/personagens/{original.Id}";

        var leitura = await intruso.GetAsync(url);
        var alteracao = await intruso.PutAsJsonAsync(
            url, new PersonagemRequest("Roubado", ClasseDnd.Ladino, 20, 999, "#000000"), ApiTestHelpers.Json);
        var exclusao = await intruso.DeleteAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, leitura.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, alteracao.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, exclusao.StatusCode);

        var aposTentativas = await dona.GetFromJsonAsync<PersonagemDto>(url, ApiTestHelpers.Json);
        Assert.Equal(original, aposTentativas);
    }
}
