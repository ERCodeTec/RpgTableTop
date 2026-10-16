using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Vtt.Application.Biblioteca;
using Vtt.Domain.Mapas;

namespace Vtt.Api.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class BibliotecaCategoriasTests(VttApiFactory factory)
{
    private static Task<List<CategoriaDto>?> ListarAsync(Sessao sessao) =>
        sessao.Client.GetFromJsonAsync<List<CategoriaDto>>("/api/biblioteca/categorias", ApiTestHelpers.Json);

    [Fact]
    public async Task Criar_Renomear_Listar_EmOrdemAlfabetica_ComContagem()
    {
        var mestre = await factory.CriarSessaoAsync();
        var viloes = await mestre.CriarCategoriaAsync("  Vilões  ");
        var npcs = await mestre.CriarCategoriaAsync("NPCs");
        await mestre.CriarModeloAsync(BibliotecaTestHelpers.Modelo(categoriaId: npcs.Id));

        var renomear = await mestre.Client.PutAsJsonAsync($"/api/biblioteca/categorias/{viloes.Id}", new CategoriaRequest("Chefes"));
        var lista = (await ListarAsync(mestre))!;

        Assert.Equal("Vilões", viloes.Nome);
        Assert.Equal(HttpStatusCode.OK, renomear.StatusCode);
        Assert.Equal(["Chefes", "NPCs"], lista.Select(c => c.Nome));
        Assert.Equal(1, lista.Single(c => c.Id == npcs.Id).QuantidadeDeModelos);
    }

    [Fact]
    public async Task NomeRepetido_SemDiferenciarMaiusculas_DaConflito_AoCriarEAoRenomear()
    {
        var mestre = await factory.CriarSessaoAsync();
        await mestre.CriarCategoriaAsync("Inimigos");
        var outra = await mestre.CriarCategoriaAsync("Bestas");

        var criar = await mestre.PostarCategoriaAsync("INIMIGOS");
        var renomear = await mestre.Client.PutAsJsonAsync($"/api/biblioteca/categorias/{outra.Id}", new CategoriaRequest("inimigos"));
        var mesmaNoOutroUsuario = await (await factory.CriarSessaoAsync()).PostarCategoriaAsync("Inimigos");

        Assert.Equal(HttpStatusCode.Conflict, criar.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, renomear.StatusCode);
        Assert.Equal(HttpStatusCode.Created, mesmaNoOutroUsuario.StatusCode);
    }

    [Fact]
    public async Task NomeVazioOuLongo_EInvalido()
    {
        var mestre = await factory.CriarSessaoAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await mestre.PostarCategoriaAsync("   ")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await mestre.PostarCategoriaAsync(new string('a', 41))).StatusCode);
    }

    [Fact]
    public async Task Excluir_DeixaOsModelosSemCategoria()
    {
        var mestre = await factory.CriarSessaoAsync();
        var categoria = await mestre.CriarCategoriaAsync();
        var modelo = await mestre.CriarModeloAsync(BibliotecaTestHelpers.Modelo(categoriaId: categoria.Id));

        var excluir = await mestre.Client.DeleteAsync($"/api/biblioteca/categorias/{categoria.Id}");
        var depois = await mestre.Client.GetFromJsonAsync<ModeloDeTokenDto>($"/api/biblioteca/modelos/{modelo.Id}", ApiTestHelpers.Json);

        Assert.Equal(HttpStatusCode.NoContent, excluir.StatusCode);
        Assert.Null(depois!.CategoriaId);
        Assert.Empty((await ListarAsync(mestre))!);
    }

    [Fact]
    public async Task Sugeridas_CriaSoAsQueFaltam()
    {
        var mestre = await factory.CriarSessaoAsync();
        await mestre.CriarCategoriaAsync("npcs");

        var primeira = await mestre.Client.PostAsync("/api/biblioteca/categorias/sugeridas", null);
        var segunda = await mestre.Client.PostAsync("/api/biblioteca/categorias/sugeridas", null);
        var lista = (await segunda.Content.ReadFromJsonAsync<List<CategoriaDto>>(ApiTestHelpers.Json))!;

        Assert.Equal(HttpStatusCode.OK, primeira.StatusCode);
        Assert.Equal(["Inimigos", "npcs", "Vilões"], lista.Select(c => c.Nome));
    }

    [Fact]
    public async Task CategoriaDeOutroUsuario_NaoExiste()
    {
        var dono = await factory.CriarSessaoAsync();
        var outro = await factory.CriarSessaoAsync();
        var categoria = await dono.CriarCategoriaAsync();

        var renomear = await outro.Client.PutAsJsonAsync($"/api/biblioteca/categorias/{categoria.Id}", new CategoriaRequest("Minha"));
        var excluir = await outro.Client.DeleteAsync($"/api/biblioteca/categorias/{categoria.Id}");

        Assert.Equal(HttpStatusCode.NotFound, renomear.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, excluir.StatusCode);
        Assert.Empty((await ListarAsync(outro))!);
        Assert.Single((await ListarAsync(dono))!);
    }
}

[Collection(ApiCollection.Nome)]
public class BibliotecaModelosTests(VttApiFactory factory)
{
    [Fact]
    public async Task Crud_DoModelo()
    {
        var mestre = await factory.CriarSessaoAsync();
        var categoria = await mestre.CriarCategoriaAsync("Vilões");
        var imagem = await mestre.CriarImagemPessoalAsync();

        var criado = await mestre.CriarModeloAsync(BibliotecaTestHelpers.Modelo(
            "Lich", TipoToken.Inimigo, "morto-vivo", 120, categoria.Id, imagem.Id, deslocamento: 4, visao: 24));
        var editar = await mestre.Client.PutAsJsonAsync(
            $"/api/biblioteca/modelos/{criado.Id}",
            BibliotecaTestHelpers.Modelo("Taverneiro", TipoToken.Npc, "npc", 9, exibicaoHp: ExibicaoHp.Oculto),
            ApiTestHelpers.Json);
        var editado = (await editar.Content.ReadFromJsonAsync<ModeloDeTokenDto>(ApiTestHelpers.Json))!;
        var lista = await mestre.Client.GetFromJsonAsync<List<ModeloDeTokenDto>>("/api/biblioteca/modelos", ApiTestHelpers.Json);
        var excluir = await mestre.Client.DeleteAsync($"/api/biblioteca/modelos/{criado.Id}");

        Assert.Equal(("Lich", 120, "morto-vivo", categoria.Id, imagem.Id, 4, 24, "#AA3322"),
            (criado.Nome, criado.HpMaximo, criado.Icone, criado.CategoriaId, criado.ImagemId, criado.Deslocamento, criado.Visao, criado.CorBorda));
        Assert.Equal(ExibicaoHp.Barra, criado.ExibicaoHp);
        Assert.Equal(HttpStatusCode.OK, editar.StatusCode);
        Assert.Equal(("Taverneiro", TipoToken.Npc, (Guid?)null, (Guid?)null, ExibicaoHp.Oculto),
            (editado.Nome, editado.Tipo, editado.CategoriaId, editado.ImagemId, editado.ExibicaoHp));
        Assert.Single(lista!);
        Assert.Equal(HttpStatusCode.NoContent, excluir.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await mestre.Client.GetAsync($"/api/biblioteca/modelos/{criado.Id}")).StatusCode);
    }

    [Fact]
    public async Task TipoPersonagem_EIconeDeOutroTipo_SaoRejeitados()
    {
        var mestre = await factory.CriarSessaoAsync();

        var personagem = await mestre.PostarModeloAsync(BibliotecaTestHelpers.Modelo(tipo: TipoToken.Personagem, icone: "Mago"));
        var iconeDeClasse = await mestre.PostarModeloAsync(BibliotecaTestHelpers.Modelo(icone: "Mago"));
        var hpZero = await mestre.PostarModeloAsync(BibliotecaTestHelpers.Modelo(hp: 0));

        Assert.Equal(HttpStatusCode.BadRequest, personagem.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, iconeDeClasse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, hpZero.StatusCode);
    }

    [Fact]
    public async Task ImagemDeMesa_ImagemECategoriaDeOutroUsuario_SaoRejeitadas()
    {
        var mestre = await factory.CriarSessaoAsync();
        var outro = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var imagemDaMesa = await mestre.CriarImagemAsync(mesa.Id);
        var imagemDoOutro = await outro.CriarImagemPessoalAsync();
        var categoriaDoOutro = await outro.CriarCategoriaAsync();

        var comImagemDaMesa = await mestre.PostarModeloAsync(BibliotecaTestHelpers.Modelo(imagemId: imagemDaMesa.Id));
        var comImagemDoOutro = await mestre.PostarModeloAsync(BibliotecaTestHelpers.Modelo(imagemId: imagemDoOutro.Id));
        var comCategoriaDoOutro = await mestre.PostarModeloAsync(BibliotecaTestHelpers.Modelo(categoriaId: categoriaDoOutro.Id));

        Assert.Equal(HttpStatusCode.BadRequest, comImagemDaMesa.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, comImagemDoOutro.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, comCategoriaDoOutro.StatusCode);
    }

    [Fact]
    public async Task ModeloDeOutroUsuario_NaoExiste()
    {
        var dono = await factory.CriarSessaoAsync();
        var outro = await factory.CriarSessaoAsync();
        var modelo = await dono.CriarModeloAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await outro.Client.GetAsync($"/api/biblioteca/modelos/{modelo.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await outro.Client.PutAsJsonAsync($"/api/biblioteca/modelos/{modelo.Id}", BibliotecaTestHelpers.Modelo(), ApiTestHelpers.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outro.Client.DeleteAsync($"/api/biblioteca/modelos/{modelo.Id}")).StatusCode);
        Assert.Empty((await outro.Client.GetFromJsonAsync<List<ModeloDeTokenDto>>("/api/biblioteca/modelos", ApiTestHelpers.Json))!);
    }

    [Fact]
    public async Task ImagemPessoalEmUsoPorModelo_NaoPodeSerExcluida()
    {
        var mestre = await factory.CriarSessaoAsync();
        var imagem = await mestre.CriarImagemPessoalAsync();
        await mestre.CriarModeloAsync(BibliotecaTestHelpers.Modelo("Ogro", imagemId: imagem.Id));

        var excluir = await mestre.Client.DeleteAsync($"/api/imagens/{imagem.Id}");

        Assert.Equal(HttpStatusCode.Conflict, excluir.StatusCode);
        Assert.Contains("modelo \"Ogro\"", (await excluir.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);
    }
}
