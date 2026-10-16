using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Vtt.Application.Auth;

namespace Vtt.Api.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class AuthTests(VttApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Registrar_ComDadosValidos_RetornaTokenEUsuario()
    {
        var email = ApiTestHelpers.EmailUnico();

        var resposta = await _client.PostAsJsonAsync(
            "/api/auth/register", new RegistrarRequest(email, ApiTestHelpers.SenhaValida, "  Elminster  "));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var auth = await resposta.Content.ReadFromJsonAsync<AuthResponse>(ApiTestHelpers.Json);
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth.Token));
        Assert.True(auth.ExpiraEm > DateTimeOffset.UtcNow);
        Assert.Equal(email, auth.Usuario.Email);
        Assert.Equal("Elminster", auth.Usuario.NomeExibicao);
        Assert.NotEqual(Guid.Empty, auth.Usuario.Id);
    }

    [Fact]
    public async Task Registrar_ComEmailJaCadastrado_Retorna400NoCampoEmail()
    {
        var email = ApiTestHelpers.EmailUnico();
        await _client.RegistrarAsync(email);

        var resposta = await _client.PostAsJsonAsync(
            "/api/auth/register", new RegistrarRequest(email.ToUpperInvariant(), ApiTestHelpers.SenhaValida, "Outro"));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var problema = await resposta.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("email", problema!.Errors.Keys);
    }

    [Theory]
    [InlineData("curta1")]
    [InlineData("semnumeros")]
    public async Task Registrar_ComSenhaFraca_Retorna400NoCampoSenha(string senha)
    {
        var resposta = await _client.PostAsJsonAsync(
            "/api/auth/register", new RegistrarRequest(ApiTestHelpers.EmailUnico(), senha, "Jogador"));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var problema = await resposta.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("senha", problema!.Errors.Keys);
    }

    [Fact]
    public async Task Registrar_ComEmailInvalidoESemNome_Retorna400ComOsDoisCampos()
    {
        var resposta = await _client.PostAsJsonAsync(
            "/api/auth/register", new RegistrarRequest("nao-e-email", ApiTestHelpers.SenhaValida, "   "));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var problema = await resposta.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("email", problema!.Errors.Keys);
        Assert.Contains("nomeExibicao", problema.Errors.Keys);
    }

    [Fact]
    public async Task Login_ComCredenciaisCorretas_RetornaTokenQueAutenticaNoMe()
    {
        var email = ApiTestHelpers.EmailUnico();
        var cadastro = await _client.RegistrarAsync(email, "Drizzt");

        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, ApiTestHelpers.SenhaValida));

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>(ApiTestHelpers.Json);

        var requisicao = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        var me = await _client.SendAsync(requisicao);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var usuario = await me.Content.ReadFromJsonAsync<UsuarioDto>(ApiTestHelpers.Json);
        Assert.Equal(new UsuarioDto(cadastro.Usuario.Id, email, "Drizzt"), usuario);
    }

    [Fact]
    public async Task Login_ComSenhaErrada_Retorna401()
    {
        var email = ApiTestHelpers.EmailUnico();
        await _client.RegistrarAsync(email);

        var resposta = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "senha-errada-123"));

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Login_ComEmailDesconhecido_Retorna401()
    {
        var resposta = await _client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(ApiTestHelpers.EmailUnico(), ApiTestHelpers.SenhaValida));

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Me_SemToken_Retorna401()
    {
        var resposta = await _client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Me_ComTokenAdulterado_Retorna401()
    {
        var auth = await _client.RegistrarAsync();
        var requisicao = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token[..^4] + "AAAA");

        var resposta = await _client.SendAsync(requisicao);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }
}
