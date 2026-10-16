using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vtt.Application.Auth;

namespace Vtt.Api.IntegrationTests;

public static class ApiTestHelpers
{
    public const string SenhaValida = "senha-forte-123";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static string EmailUnico() => $"jogador-{Guid.NewGuid():N}@teste.local";

    public static async Task<AuthResponse> RegistrarAsync(this HttpClient client, string? email = null, string nomeExibicao = "Jogador")
    {
        var resposta = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegistrarRequest(email ?? EmailUnico(), SenhaValida, nomeExibicao));
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
    }

    /// <summary>Cadastra um usuário novo e devolve um cliente já autenticado como ele.</summary>
    public static async Task<HttpClient> CriarClienteAutenticadoAsync(this VttApiFactory factory)
    {
        var client = factory.CreateClient();
        var auth = await client.RegistrarAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }
}
