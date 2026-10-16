using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Json;
using Vtt.Application.Mapas;
using Vtt.Domain.Mapas;

namespace Vtt.Api.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class TexturasTests(VttApiFactory factory)
{
    [Fact]
    public async Task Catalogo_TrazTemasETexturasComCategorias_SoParaQuemEstaLogado()
    {
        var sessao = await factory.CriarSessaoAsync();

        var catalogo = (await sessao.Client.GetFromJsonAsync<CatalogoDeTexturasDto>("/api/texturas-prontas", ApiTestHelpers.Json))!;
        var anonimo = await factory.CreateClient().GetAsync("/api/texturas-prontas");

        Assert.Equal(HttpStatusCode.Unauthorized, anonimo.StatusCode);
        Assert.Equal(TexturasPadrao.Todas.Count, catalogo.Texturas.Count);
        Assert.Contains(catalogo.Temas, t => t is { Id: "masmorra", Geravel: true });
        Assert.All(catalogo.Texturas, t =>
        {
            Assert.Contains(t.Tema, catalogo.Temas.Select(tema => tema.Id));
            Assert.NotEmpty(t.Estruturas);
        });
    }

    [Fact]
    public void Catalogo_TemChavesUnicas_EPapeisQueOGeradorConhece()
    {
        var papeis = typeof(TexturasPadrao.Papel).GetFields().Select(f => (string)f.GetValue(null)!).ToHashSet();

        Assert.Equal(TexturasPadrao.Todas.Count, TexturasPadrao.Chaves.Count);
        Assert.All(TexturasPadrao.Todas, t =>
        {
            Assert.True(t.Chave.Length <= TexturasPadrao.ChaveTamanhoMaximo, t.Chave);
            Assert.Subset(papeis, t.Papeis.ToHashSet());
        });
        // As chaves usadas pelo nome no código existem.
        Assert.Subset(TexturasPadrao.Chaves.ToHashSet(), new HashSet<string>
        {
            TexturasPadrao.ParedeDePedra, TexturasPadrao.PisoDeLajotas, TexturasPadrao.PisoDeAreia, TexturasPadrao.PisoDeTerra, TexturasPadrao.PortaDeMadeira
        });
    }

    /// <summary>Cada textura do catálogo tem o PNG no frontend, com o tamanho declarado, e não sobra PNG fora dele.</summary>
    [Fact]
    public void Catalogo_BateComOsArquivosDoFrontend()
    {
        var pasta = PastaDasTexturas();
        var arquivos = Directory.GetFiles(pasta, "*.png").Select(Path.GetFileNameWithoutExtension).ToHashSet();

        Assert.Equal(TexturasPadrao.Chaves.OrderBy(c => c), arquivos.OrderBy(c => c));
        Assert.All(TexturasPadrao.Todas, t =>
        {
            // Largura e altura ficam nos bytes 16 a 23 do PNG (bloco IHDR).
            var cabecalho = File.ReadAllBytes(Path.Combine(pasta, $"{t.Chave}.png")).AsSpan(16, 8);
            Assert.Equal((t.Largura, t.Altura), (BinaryPrimitives.ReadInt32BigEndian(cabecalho), BinaryPrimitives.ReadInt32BigEndian(cabecalho[4..])));
        });
    }

    private static string PastaDasTexturas()
    {
        for (var pasta = new DirectoryInfo(AppContext.BaseDirectory); pasta is not null; pasta = pasta.Parent)
        {
            var candidata = Path.Combine(pasta.FullName, "frontend", "public", "texturas");
            if (Directory.Exists(candidata))
            {
                return candidata;
            }
        }

        throw new DirectoryNotFoundException("frontend/public/texturas não foi encontrada acima da pasta dos testes.");
    }
}
