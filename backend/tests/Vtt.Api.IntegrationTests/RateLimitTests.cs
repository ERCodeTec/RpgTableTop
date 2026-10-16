using System.Net;
using System.Net.Http.Json;
using Vtt.Application.Auth;

namespace Vtt.Api.IntegrationTests;

public class RateLimitFactory : VttApiFactory
{
    public const int Limite = 3;

    protected override int AuthLimite => Limite;
}

public class RateLimitTests(RateLimitFactory factory) : IClassFixture<RateLimitFactory>
{
    [Fact]
    public async Task Login_AcimaDoLimitePorIp_Retorna429ComRetryAfter()
    {
        var client = factory.CreateClient();
        var request = new LoginRequest(ApiTestHelpers.EmailUnico(), ApiTestHelpers.SenhaValida);

        for (var tentativa = 0; tentativa < RateLimitFactory.Limite; tentativa++)
        {
            var dentroDoLimite = await client.PostAsJsonAsync("/api/auth/login", request);
            Assert.Equal(HttpStatusCode.Unauthorized, dentroDoLimite.StatusCode);
        }

        var bloqueada = await client.PostAsJsonAsync("/api/auth/login", request);

        Assert.Equal(HttpStatusCode.TooManyRequests, bloqueada.StatusCode);
        Assert.True(bloqueada.Headers.Contains("Retry-After"));
        Assert.Equal("application/problem+json", bloqueada.Content.Headers.ContentType?.MediaType);

        // O limite vale só para login/cadastro: o restante da API continua respondendo.
        var saude = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, saude.StatusCode);
    }
}
