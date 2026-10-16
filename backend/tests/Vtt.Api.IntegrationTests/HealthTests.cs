using System.Net;

namespace Vtt.Api.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class HealthTests(VttApiFactory factory)
{
    [Fact]
    public async Task Health_ComBancoDisponivel_Retorna200()
    {
        var resposta = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("Healthy", await resposta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RotaDesconhecidaDaApi_Retorna404EmVezDoIndexDoSpa()
    {
        var resposta = await factory.CreateClient().GetAsync("/api/nao-existe");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
    }
}
