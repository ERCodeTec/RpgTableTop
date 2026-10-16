using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Vtt.Application.Iniciativas;
using Vtt.Application.Mapas;
using Vtt.Domain.Mapas;
using Vtt.Domain.Mesas;

namespace Vtt.Api.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class InteracaoTests(VttApiFactory factory)
{
    private const string Descricao = "Uma carta selada com o brasão da família Varn.";

    private sealed record Cena(
        Sessao Mestre, Sessao Alice, Guid MesaId, Guid MapaId, TokenDto Interacao, TokenDto DaAlice, HubConnection ConexaoDoMestre)
        : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ConexaoDoMestre.DisposeAsync();
    }

    /// <summary>
    /// Mestre e Alice num mapa ativo de 10x8, com uma Interação colocada da biblioteca em (5,5) e o
    /// token da Alice em <paramref name="xDaAlice"/>, <paramref name="yDaAlice"/>.
    /// </summary>
    private async Task<Cena> MontarAsync(int xDaAlice, int yDaAlice)
    {
        var mestre = await factory.CriarSessaoAsync("Mestre");
        var mesa = await mestre.CriarMesaAsync();
        var mapa = (await mestre.CriarMapaAtivoAsync(mesa.Id)).Mapa;
        var (alice, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Alice");

        var aparencia = await mestre.CriarImagemPessoalAsync(nome: "bau.png");
        var conteudo = await mestre.CriarImagemPessoalAsync(nome: "carta.png");
        var modelo = await mestre.CriarModeloAsync(ModeloDeInteracao(aparencia.Id, conteudo.Id));
        var interacao = Assert.Single(await mestre.ColocarAsync(mapa.Id, modelo.Id));

        var (conexao, _) = await factory.EntrarNaMesaAsync(mestre, mesa.Id);
        var movida = await conexao.MoverTokenAsync(interacao, 5, 5);
        Assert.True(movida.Aceito, movida.Motivo);

        var daAlice = (await mestre.ObterEstadoDoMapaAsync(mapa.Id)).TokenDe(alice)!;
        var movimento = await conexao.MoverTokenAsync(daAlice, xDaAlice, yDaAlice);
        Assert.True(movimento.Aceito, movimento.Motivo);

        return new Cena(mestre, alice, mesa.Id, mapa.Id, movida.Token!, movimento.Token!, conexao);
    }

    private static Application.Biblioteca.ModeloDeTokenRequest ModeloDeInteracao(Guid? imagemId = null, Guid? conteudoId = null) =>
        BibliotecaTestHelpers.Modelo("Carta", TipoToken.Interacao, "pergaminho", hp: 1, imagemId: imagemId) with
        {
            Descricao = Descricao,
            ConteudoImagemId = conteudoId
        };

    private static Task<HttpResponseMessage> ExaminarAsync(Sessao sessao, Guid tokenId) =>
        sessao.Client.GetAsync($"/api/tokens/{tokenId}/interacao");

    private static async Task<TokenDto> EditarAsync(Sessao mestre, Guid tokenId, InteracaoRequest request)
    {
        var resposta = await mestre.Client.PutAsJsonAsync($"/api/tokens/{tokenId}/interacao", request, ApiTestHelpers.Json);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<TokenDto>(ApiTestHelpers.Json))!;
    }

    [Fact]
    public async Task ColocarDaBiblioteca_CopiaDescricaoEImagensParaAMesa_SemHpNemMovimento()
    {
        await using var cena = await MontarAsync(0, 0);
        var interacao = cena.Interacao;

        Assert.Equal((TipoToken.Interacao, "Carta", "pergaminho", Descricao, ExibicaoHp.Oculto, false),
            (interacao.Tipo, interacao.Nome, interacao.Icone, interacao.Descricao, interacao.ExibicaoHp, interacao.Revelado));
        Assert.NotNull(interacao.ImagemId);
        Assert.NotNull(interacao.ConteudoImagemId);
        Assert.NotEqual(interacao.ImagemId, interacao.ConteudoImagemId);

        // As duas são cópias na galeria da mesa, que o Mestre baixa.
        Assert.Equal(HttpStatusCode.OK, (await cena.Mestre.Client.GetAsync($"/api/imagens/{interacao.ImagemId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cena.Mestre.Client.GetAsync($"/api/imagens/{interacao.ConteudoImagemId}")).StatusCode);
    }

    [Theory]
    [InlineData(4, 5)]
    [InlineData(6, 6)]
    [InlineData(4, 4)]
    [InlineData(5, 6)]
    public async Task JogadorAoLado_IncluindoDiagonal_VeOConteudo(int x, int y)
    {
        await using var cena = await MontarAsync(x, y);

        var resposta = await ExaminarAsync(cena.Alice, cena.Interacao.Id);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var conteudo = (await resposta.Content.ReadFromJsonAsync<InteracaoDto>(ApiTestHelpers.Json))!;
        Assert.Equal(("Carta", Descricao, cena.Interacao.ConteudoImagemId), (conteudo.Nome, conteudo.Descricao, conteudo.ConteudoImagemId));
        Assert.Equal(HttpStatusCode.OK, (await cena.Alice.Client.GetAsync($"/api/imagens/{conteudo.ConteudoImagemId}")).StatusCode);
    }

    [Fact]
    public async Task JogadorLonge_NaoVeOConteudo_NemBaixaAImagem()
    {
        await using var cena = await MontarAsync(3, 5);

        Assert.Equal(HttpStatusCode.Forbidden, (await ExaminarAsync(cena.Alice, cena.Interacao.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cena.Alice.Client.GetAsync($"/api/imagens/{cena.Interacao.ConteudoImagemId}")).StatusCode);

        // A aparência do token, por outro lado, está à vista de todos.
        Assert.Equal(HttpStatusCode.OK, (await cena.Alice.Client.GetAsync($"/api/imagens/{cena.Interacao.ImagemId}")).StatusCode);
    }

    [Fact]
    public async Task TokenDoJogador_NaoTrazDescricaoNemImagemDoConteudo()
    {
        await using var cena = await MontarAsync(4, 5);

        var paraAAlice = (await cena.Alice.ObterEstadoDoMapaAsync(cena.MapaId)).Tokens.Single(t => t.Id == cena.Interacao.Id);

        Assert.Equal(TipoToken.Interacao, paraAAlice.Tipo);
        Assert.Null(paraAAlice.Descricao);
        Assert.Null(paraAAlice.ConteudoImagemId);
    }

    [Fact]
    public async Task Revelada_QualquerJogadorVe_EAoEsconderVoltaAExigirProximidade()
    {
        await using var cena = await MontarAsync(0, 0);
        var (conexaoDaAlice, _) = await factory.EntrarNaMesaAsync(cena.Alice, cena.MesaId);
        await using var _1 = conexaoDaAlice;
        var atualizado = new TaskCompletionSource<TokenDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        conexaoDaAlice.On<Guid, TokenDto>("TokenAtualizado", (_, token) => atualizado.TrySetResult(token));

        var revelada = await EditarAsync(cena.Mestre, cena.Interacao.Id, new InteracaoRequest(Descricao, cena.Interacao.ConteudoImagemId, true));

        Assert.True(revelada.Revelado);
        var evento = await atualizado.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(evento.Revelado);
        Assert.Null(evento.Descricao);
        Assert.Equal(HttpStatusCode.OK, (await ExaminarAsync(cena.Alice, cena.Interacao.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cena.Alice.Client.GetAsync($"/api/imagens/{cena.Interacao.ConteudoImagemId}")).StatusCode);

        await EditarAsync(cena.Mestre, cena.Interacao.Id, new InteracaoRequest(Descricao, cena.Interacao.ConteudoImagemId, false));

        Assert.Equal(HttpStatusCode.Forbidden, (await ExaminarAsync(cena.Alice, cena.Interacao.Id)).StatusCode);
    }

    [Fact]
    public async Task Mestre_EditaOConteudo_EOJogadorAoLadoVeONovo()
    {
        await using var cena = await MontarAsync(4, 5);

        var editada = await EditarAsync(cena.Mestre, cena.Interacao.Id, new InteracaoRequest("  O selo foi quebrado.  ", null, null));

        Assert.Equal(("O selo foi quebrado.", (Guid?)null, false), (editada.Descricao, editada.ConteudoImagemId, editada.Revelado));
        var conteudo = await (await ExaminarAsync(cena.Alice, cena.Interacao.Id)).Content.ReadFromJsonAsync<InteracaoDto>(ApiTestHelpers.Json);
        Assert.Equal("O selo foi quebrado.", conteudo!.Descricao);
    }

    [Fact]
    public async Task SoOMestreEditaOConteudo()
    {
        await using var cena = await MontarAsync(4, 5);

        var resposta = await cena.Alice.Client.PutAsJsonAsync(
            $"/api/tokens/{cena.Interacao.Id}/interacao", new InteracaoRequest("Hackeado", null, true), ApiTestHelpers.Json);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task InteracaoOculta_NaoExisteParaOJogador_MasOMestreVe()
    {
        await using var cena = await MontarAsync(4, 5);
        (await cena.Mestre.Client.PutAsJsonAsync(
            $"/api/tokens/{cena.Interacao.Id}/exibicao", new ExibicaoRequest(false, null), ApiTestHelpers.Json)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NotFound, (await ExaminarAsync(cena.Alice, cena.Interacao.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ExaminarAsync(cena.Mestre, cena.Interacao.Id)).StatusCode);
    }

    [Fact]
    public async Task TokenQueNaoEInteracao_NaoTemConteudo()
    {
        await using var cena = await MontarAsync(4, 5);
        var goblin = await cena.Mestre.CriarTokenAsync(cena.MapaId, x: 3, y: 4);

        Assert.Equal(HttpStatusCode.NotFound, (await ExaminarAsync(cena.Alice, goblin.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ExaminarAsync(cena.Mestre, goblin.Id)).StatusCode);
    }

    [Fact]
    public async Task InteracaoNaoTemHp()
    {
        await using var cena = await MontarAsync(0, 0);

        var resposta = await cena.Mestre.Client.PutAsJsonAsync(
            $"/api/tokens/{cena.Interacao.Id}/hp", new HpRequest(-1, null, null), ApiTestHelpers.Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task NaoDaParaCriarInteracaoDiretoNoMapa()
    {
        var mestre = await factory.CriarSessaoAsync("Mestre");
        var mesa = await mestre.CriarMesaAsync();
        var mapa = (await mestre.CriarMapaAtivoAsync(mesa.Id)).Mapa;

        var resposta = await mestre.PostarTokenAsync(mapa.Id, "Baú", tipo: TipoToken.Interacao, icone: "bau", hp: 1);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public void Iniciativa_IgnoraAInteracao_MesmoComONomeDeUmGrupo()
    {
        var mapaId = Guid.NewGuid();
        var goblin = new Token { Id = Guid.NewGuid(), MapaId = mapaId, Tipo = TipoToken.Inimigo, Nome = "Carta" };
        var carta = new Token { Id = Guid.NewGuid(), MapaId = mapaId, Tipo = TipoToken.Interacao, Nome = "Carta", X = 1 };
        var valor = new ValorDeIniciativa { Grupo = GrupoDeIniciativa.Chave("Carta"), Valor = 10 };

        var ordem = OrdemDeIniciativa.Calcular([valor], [goblin, carta]);

        Assert.Equal([goblin.Id], ordem.Select(t => t.Id));
    }
}
