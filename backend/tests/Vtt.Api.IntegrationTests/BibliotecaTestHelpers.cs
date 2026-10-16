using System.Net.Http.Headers;
using System.Net.Http.Json;
using Vtt.Application.Biblioteca;
using Vtt.Application.Imagens;
using Vtt.Domain.Mapas;

namespace Vtt.Api.IntegrationTests;

public static class BibliotecaTestHelpers
{
    public static async Task<HttpResponseMessage> EnviarImagemPessoalAsync(this Sessao sessao, byte[]? conteudo = null, string nome = "goblin.png")
    {
        using var formulario = new MultipartFormDataContent();
        var arquivo = new ByteArrayContent(conteudo ?? ImagensDeTeste.Png());
        arquivo.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        formulario.Add(arquivo, "arquivo", nome);
        return await sessao.Client.PostAsync("/api/biblioteca/imagens", formulario);
    }

    public static async Task<ImagemDto> CriarImagemPessoalAsync(this Sessao sessao, byte[]? conteudo = null, string nome = "goblin.png")
    {
        var resposta = await sessao.EnviarImagemPessoalAsync(conteudo, nome);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<ImagemDto>(ApiTestHelpers.Json))!;
    }

    public static Task<HttpResponseMessage> PostarCategoriaAsync(this Sessao sessao, string nome) =>
        sessao.Client.PostAsJsonAsync("/api/biblioteca/categorias", new CategoriaRequest(nome), ApiTestHelpers.Json);

    public static async Task<CategoriaDto> CriarCategoriaAsync(this Sessao sessao, string nome = "Inimigos")
    {
        var resposta = await sessao.PostarCategoriaAsync(nome);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<CategoriaDto>(ApiTestHelpers.Json))!;
    }

    public static ModeloDeTokenRequest Modelo(
        string nome = "Goblin", TipoToken tipo = TipoToken.Inimigo, string icone = "goblin", int hp = 7,
        Guid? categoriaId = null, Guid? imagemId = null, int deslocamento = 6, int visao = 0, ExibicaoHp? exibicaoHp = null) =>
        new(categoriaId, tipo, nome, hp, icone, imagemId, "#AA3322", deslocamento, visao, exibicaoHp);

    public static Task<HttpResponseMessage> PostarModeloAsync(this Sessao sessao, ModeloDeTokenRequest request) =>
        sessao.Client.PostAsJsonAsync("/api/biblioteca/modelos", request, ApiTestHelpers.Json);

    public static async Task<ModeloDeTokenDto> CriarModeloAsync(this Sessao sessao, ModeloDeTokenRequest? request = null)
    {
        var resposta = await sessao.PostarModeloAsync(request ?? Modelo());
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<ModeloDeTokenDto>(ApiTestHelpers.Json))!;
    }

    public static Task<HttpResponseMessage> ColocarDaBibliotecaAsync(this Sessao sessao, Guid mapaId, Guid modeloId, int quantidade = 1, bool? visivel = null) =>
        sessao.Client.PostAsJsonAsync(
            $"/api/mapas/{mapaId}/tokens/da-biblioteca", new Application.Mapas.CriarDaBibliotecaRequest(modeloId, quantidade, visivel), ApiTestHelpers.Json);

    public static async Task<List<Application.Mapas.TokenDto>> ColocarAsync(this Sessao sessao, Guid mapaId, Guid modeloId, int quantidade = 1, bool? visivel = null)
    {
        var resposta = await sessao.ColocarDaBibliotecaAsync(mapaId, modeloId, quantidade, visivel);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<List<Application.Mapas.TokenDto>>(ApiTestHelpers.Json))!;
    }
}
