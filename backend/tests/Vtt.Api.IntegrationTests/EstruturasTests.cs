using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;
using Vtt.Api.Hubs;
using Vtt.Application.Mapas;
using Vtt.Domain.Mapas;
using static Vtt.Api.IntegrationTests.ImagemTestHelpers;

namespace Vtt.Api.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class EstruturasTests(VttApiFactory factory)
{
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Silencio = TimeSpan.FromMilliseconds(400);

    private static ChannelReader<string> Escutar(HubConnection conexao)
    {
        var eventos = Channel.CreateUnbounded<string>();
        conexao.On<Guid, EstruturaDto>("EstruturaAdicionada", (_, e) => eventos.Writer.TryWrite($"EstruturaAdicionada:{e.Tipo}:{e.X},{e.Y}"));
        conexao.On<Guid, EstruturaDto>("EstruturaAtualizada", (_, e) => eventos.Writer.TryWrite($"EstruturaAtualizada:{e.Tipo}:{e.X},{e.Y}:bloqueia={e.Bloqueia}"));
        conexao.On<Guid, Guid, Guid>("EstruturaRemovida", (_, _, id) => eventos.Writer.TryWrite($"EstruturaRemovida:{id}"));
        return eventos.Reader;
    }

    private static async Task<string> ProximoAsync(ChannelReader<string> eventos)
    {
        using var limite = new CancellationTokenSource(Espera);
        return await eventos.ReadAsync(limite.Token);
    }

    private static async Task AssertNadaChegaAsync(ChannelReader<string> eventos)
    {
        await Task.Delay(Silencio);
        Assert.False(eventos.TryRead(out var inesperado), $"Evento inesperado: {inesperado}");
    }

    private async Task<(HubConnection Conexao, ChannelReader<string> Eventos, LobbyDto Lobby)> ConectarAsync(Sessao sessao, Guid mesaId)
    {
        var conexao = factory.CriarConexaoDoHub(sessao);
        var eventos = Escutar(conexao);
        await conexao.StartAsync();
        var lobby = await conexao.InvokeAsync<LobbyDto>("EntrarNaMesa", mesaId);
        return (conexao, eventos, lobby);
    }

    /// <summary>Mesa com Mestre, uma jogadora e um mapa ativo de 10x8; o token dela nasce em (0,0).</summary>
    private async Task<(Sessao Mestre, Sessao Jogadora, Guid MesaId, MapaDto Mapa)> MontarMesaAsync()
    {
        var mestre = await factory.CriarSessaoAsync("Mestre");
        var mesa = await mestre.CriarMesaAsync();
        var (jogadora, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Alice");
        var estado = await mestre.CriarMapaAtivoAsync(mesa.Id);
        return (mestre, jogadora, mesa.Id, estado.Mapa);
    }

    [Fact]
    public async Task Mestre_CriaEditaERemoveEstrutura_EOEstadoDoMapaAsInclui()
    {
        var (mestre, jogadora, _, mapa) = await MontarMesaAsync();

        var resposta = await mestre.PostarEstruturaAsync(mapa.Id, Estrutura(TipoEstrutura.Parede, x: 2, y: 3, largura: 4, altura: 2.456));
        var criada = (await resposta.Content.ReadFromJsonAsync<EstruturaDto>(ApiTestHelpers.Json))!;
        var edicao = await mestre.Client.PutAsJsonAsync(
            $"/api/estruturas/{criada.Id}",
            Estrutura(TipoEstrutura.Obstaculo, x: 2, y: 3, largura: 1, profundidade: 4, altura: 0.5, rotacao: 90, bloqueia: false, versao: 1),
            ApiTestHelpers.Json);
        var editada = (await edicao.Content.ReadFromJsonAsync<EstruturaDto>(ApiTestHelpers.Json))!;
        var vistaPelaJogadora = Assert.Single((await jogadora.ObterEstadoDoMapaAsync(mapa.Id)).Estruturas);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.Equal((TipoEstrutura.Parede, 2, 3, 4, 1, 2.46, "#6B6F76", 1), (criada.Tipo, criada.X, criada.Y, criada.Largura, criada.Profundidade, criada.Altura, criada.Cor, criada.Versao));
        Assert.True(criada.Bloqueia);
        Assert.Equal(HttpStatusCode.OK, edicao.StatusCode);
        Assert.Equal((TipoEstrutura.Obstaculo, 1, 4, 0.5, 90, 2), (editada.Tipo, editada.Largura, editada.Profundidade, editada.Altura, editada.RotacaoGraus, editada.Versao));
        Assert.False(editada.Bloqueia);
        Assert.Equal(editada, vistaPelaJogadora);

        Assert.Equal(HttpStatusCode.NoContent, (await mestre.Client.DeleteAsync($"/api/estruturas/{criada.Id}")).StatusCode);
        Assert.Empty((await mestre.ObterEstadoDoMapaAsync(mapa.Id)).Estruturas);
        Assert.Equal(HttpStatusCode.NotFound, (await mestre.Client.DeleteAsync($"/api/estruturas/{criada.Id}")).StatusCode);
    }

    [Fact]
    public async Task TexturaPronta_EGravada_SoComChaveConhecida_ENuncaJuntoComImagemDaGaleria()
    {
        var (mestre, jogadora, _, mapa) = await MontarMesaAsync();

        var criada = await mestre.CriarEstruturaAsync(mapa.Id, Estrutura(TipoEstrutura.Parede) with { TexturaPadrao = TexturasPadrao.ParedeDePedra });
        var lava = await mestre.PostarEstruturaAsync(mapa.Id, Estrutura(TipoEstrutura.Chao, x: 5, altura: 0.1) with { TexturaPadrao = "lava" });
        var desconhecida = await mestre.PostarEstruturaAsync(mapa.Id, Estrutura(x: 2) with { TexturaPadrao = "parede-de-ouro" });
        var asDuas = await mestre.PostarEstruturaAsync(mapa.Id, Estrutura(x: 3) with { TexturaPadrao = TexturasPadrao.PisoDeAreia, TexturaImagemId = Guid.NewGuid() });
        var removida = await mestre.Client.PutAsJsonAsync($"/api/estruturas/{criada.Id}", Estrutura(TipoEstrutura.Parede, versao: 1), ApiTestHelpers.Json);
        var semTextura = (await removida.Content.ReadFromJsonAsync<EstruturaDto>(ApiTestHelpers.Json))!;

        Assert.Equal(TexturasPadrao.ParedeDePedra, criada.TexturaPadrao);
        Assert.Equal(HttpStatusCode.Created, lava.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, desconhecida.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, asDuas.StatusCode);
        Assert.Null(semTextura.TexturaPadrao);
        var vistas = (await jogadora.ObterEstadoDoMapaAsync(mapa.Id)).Estruturas;
        Assert.Equal([null, "lava"], vistas.OrderBy(e => e.X).Select(e => e.TexturaPadrao));
    }

    [Fact]
    public async Task Jogador_NaoCriaEditaAbrePortaNemRemove_ENaoMembroNaoEnxerga()
    {
        var (mestre, jogadora, _, mapa) = await MontarMesaAsync();
        var estranho = await factory.CriarSessaoAsync("Estranho");
        var porta = await mestre.CriarEstruturaAsync(mapa.Id, Estrutura(TipoEstrutura.Porta, x: 4, y: 4));

        Assert.Equal(HttpStatusCode.Forbidden, (await jogadora.PostarEstruturaAsync(mapa.Id, Estrutura(x: 6, y: 6))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await jogadora.Client.PutAsJsonAsync($"/api/estruturas/{porta.Id}", Estrutura(TipoEstrutura.Porta, x: 5, y: 4), ApiTestHelpers.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await jogadora.Client.PutAsJsonAsync($"/api/estruturas/{porta.Id}/porta", new PortaRequest(true))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await jogadora.Client.DeleteAsync($"/api/estruturas/{porta.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await estranho.PostarEstruturaAsync(mapa.Id, Estrutura())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await estranho.Client.PutAsJsonAsync($"/api/estruturas/{porta.Id}/porta", new PortaRequest(true))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await estranho.Client.DeleteAsync($"/api/estruturas/{porta.Id}")).StatusCode);

        var intacta = Assert.Single((await mestre.ObterEstadoDoMapaAsync(mapa.Id)).Estruturas);
        Assert.Equal((4, 4, false, 1), (intacta.X, intacta.Y, intacta.PortaAberta, intacta.Versao));
    }

    [Theory]
    [InlineData(9, 0, 2, 1, 2.0, 0)] // passa da borda direita de um mapa 10x8
    [InlineData(0, 7, 1, 2, 2.0, 0)] // passa da borda de baixo
    [InlineData(-1, 0, 1, 1, 2.0, 0)]
    [InlineData(0, 0, 0, 1, 2.0, 0)] // largura mínima é 1
    [InlineData(0, 0, 1, 1, 3.5, 0)] // altura máxima é 3
    [InlineData(0, 0, 1, 1, 0.05, 0)]
    [InlineData(0, 0, 1, 1, 2.0, 45)] // rotação só em passos de 90
    public async Task EstruturaInvalida_Retorna400(int x, int y, int largura, int profundidade, double altura, int rotacao)
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var mapa = await mestre.CriarMapaAsync(mesa.Id, largura: 10, altura: 8);

        var resposta = await mestre.PostarEstruturaAsync(mapa.Id, Estrutura(x: x, y: y, largura: largura, profundidade: profundidade, altura: altura, rotacao: rotacao));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task EditarComVersaoAntiga_Retorna409_EPortaSoAbreSeForPorta()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var mapa = await mestre.CriarMapaAsync(mesa.Id);
        var parede = await mestre.CriarEstruturaAsync(mapa.Id, Estrutura(x: 1, y: 1, portaAberta: true));
        (await mestre.Client.PutAsJsonAsync($"/api/estruturas/{parede.Id}", Estrutura(x: 2, y: 1, versao: 1), ApiTestHelpers.Json)).EnsureSuccessStatusCode();

        var atrasada = await mestre.Client.PutAsJsonAsync($"/api/estruturas/{parede.Id}", Estrutura(x: 5, y: 5, versao: 1), ApiTestHelpers.Json);
        var abrirParede = await mestre.Client.PutAsJsonAsync($"/api/estruturas/{parede.Id}/porta", new PortaRequest(true));

        Assert.False(parede.PortaAberta);
        Assert.Equal(HttpStatusCode.Conflict, atrasada.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, abrirParede.StatusCode);
        var atual = Assert.Single((await mestre.ObterEstadoDoMapaAsync(mapa.Id)).Estruturas);
        Assert.Equal((2, 1, 2), (atual.X, atual.Y, atual.Versao));
    }

    [Fact]
    public async Task ReduzirOMapa_ComEstruturaForaDosNovosLimites_Retorna409()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var mapa = await mestre.CriarMapaAsync(mesa.Id, largura: 10, altura: 8);
        await mestre.CriarEstruturaAsync(mapa.Id, Estrutura(x: 6, y: 0, largura: 4));

        var cortando = await mestre.Client.PutAsJsonAsync($"/api/mapas/{mapa.Id}", new MapaRequest("Caverna", 9, 8, null));
        var cabendo = await mestre.Client.PutAsJsonAsync($"/api/mapas/{mapa.Id}", new MapaRequest("Caverna", 10, 5, null));

        Assert.Equal(HttpStatusCode.Conflict, cortando.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cabendo.StatusCode);
    }

    [Fact]
    public async Task EstruturaBloqueante_ImpedeOMovimentoDoJogador_MasNaoODoMestre()
    {
        var (mestre, jogadora, mesaId, mapa) = await MontarMesaAsync();
        await mestre.CriarEstruturaAsync(mapa.Id, Estrutura(TipoEstrutura.Parede, x: 3, y: 0, largura: 1, profundidade: 3));
        var (conexaoDaJogadora, _, lobby) = await ConectarAsync(jogadora, mesaId);
        await using var _1 = conexaoDaJogadora;
        var (conexaoDoMestre, _, _) = await ConectarAsync(mestre, mesaId);
        await using var _2 = conexaoDoMestre;
        var token = lobby.MapaAtivo!.TokenDe(jogadora)!;

        var naParede = await conexaoDaJogadora.MoverPorBlocosAsync(token, 3, 2);
        // Contornando a parede por baixo, sem raspar a ponta dela: (1,1), (2,2), (2,3), (3,3), (4,3).
        var aoLado = await conexaoDaJogadora.MoverPorBlocosAsync(token, 4, 3);
        var peloMestre = await conexaoDoMestre.MoverTokenAsync(aoLado.Token!, 3, 1);
        // Dentro da parede, a jogadora ainda consegue sair.
        var saindo = await conexaoDaJogadora.MoverPorBlocosAsync(peloMestre.Token!, 2, 1);

        Assert.False(naParede.Aceito);
        Assert.Contains("estrutura bloqueando", naParede.Motivo);
        Assert.Equal((token.X, token.Y, token.Versao), (naParede.Token!.X, naParede.Token.Y, naParede.Token.Versao));
        Assert.True(aoLado.Aceito);
        Assert.Equal(1, aoLado.Token!.MovimentoRestante);
        Assert.True(peloMestre.Aceito);
        Assert.True(saindo.Aceito);
    }

    [Fact]
    public async Task PortaFechadaBloqueia_AbertaPermite_EObstaculoSemBloqueioPermite()
    {
        var (mestre, jogadora, mesaId, mapa) = await MontarMesaAsync();
        var porta = await mestre.CriarEstruturaAsync(mapa.Id, Estrutura(TipoEstrutura.Porta, x: 5, y: 5, bloqueia: false));
        await mestre.CriarEstruturaAsync(mapa.Id, Estrutura(TipoEstrutura.Obstaculo, x: 7, y: 7, altura: 0.2, bloqueia: false));
        var (conexao, eventos, lobby) = await ConectarAsync(jogadora, mesaId);
        await using var _1 = conexao;
        var (conexaoDoMestre, _, _) = await ConectarAsync(mestre, mesaId);
        await using var _2 = conexaoDoMestre;
        var token = lobby.MapaAtivo!.TokenDe(jogadora)!;

        var comAPortaFechada = await conexao.MoverPorBlocosAsync(token, 5, 5);
        var aberta = await mestre.DefinirPortaAsync(porta.Id, aberta: true);
        Assert.Equal("EstruturaAtualizada:Porta:5,5:bloqueia=False", await ProximoAsync(eventos));
        var comAPortaAberta = await conexao.AndarAsync(token, 5, 5);
        await conexaoDoMestre.InvokeAsync("NovaRodada", mapa.Id);
        var noTapete = await conexao.AndarAsync((await jogadora.ObterEstadoDoMapaAsync(mapa.Id)).TokenDe(jogadora)!, 7, 7);
        // Fechar a porta com o token em outro lugar volta a barrar.
        await mestre.DefinirPortaAsync(porta.Id, aberta: false);
        var deNovoFechada = await conexao.MoverPorBlocosAsync(noTapete.Token!, 5, 5);

        Assert.True(porta.Bloqueia);
        Assert.False(comAPortaFechada.Aceito);
        Assert.Contains("estrutura bloqueando", comAPortaFechada.Motivo);
        Assert.True(aberta.PortaAberta);
        Assert.False(aberta.Bloqueia);
        Assert.True(comAPortaAberta.Aceito);
        Assert.True(noTapete.Aceito);
        Assert.False(deNovoFechada.Aceito);
    }

    [Fact]
    public async Task ChaoSemBloqueio_DeixaOJogadorPisarEmQualquerCelulaDaArea()
    {
        var (mestre, jogadora, mesaId, mapa) = await MontarMesaAsync();
        var chao = await mestre.CriarEstruturaAsync(
            mapa.Id, Estrutura(TipoEstrutura.Chao, x: 4, y: 4, largura: 4, profundidade: 3, altura: 0.1, bloqueia: false));
        var (conexao, _, lobby) = await ConectarAsync(jogadora, mesaId);
        await using var _1 = conexao;
        var token = lobby.MapaAtivo!.TokenDe(jogadora)!;

        var noMeio = await conexao.MoverPorBlocosAsync(token, 6, 5);

        Assert.Equal((TipoEstrutura.Chao, 4, 3, false), (chao.Tipo, chao.Largura, chao.Profundidade, chao.Bloqueia));
        Assert.True(noMeio.Aceito);
    }

    [Fact]
    public async Task Spawn_EDuplicacao_PulamCelulasBloqueadas_MasOMestrePosicionaOndeQuiser()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var mapa = await mestre.CriarMapaAsync(mesa.Id);
        await mestre.CriarEstruturaAsync(mapa.Id, Estrutura(x: 0, y: 0, largura: 3));
        await mestre.CriarEstruturaAsync(mapa.Id, Estrutura(TipoEstrutura.Porta, x: 3, y: 0, portaAberta: true));
        var (jogadora, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id);

        var estado = await mestre.AtivarMapaAsync(mapa.Id);
        var semPosicao = await mestre.CriarTokenAsync(mapa.Id, "Lobo");
        var sobreAParede = await mestre.CriarTokenAsync(mapa.Id, "Fantasma", x: 1, y: 0);
        var copia = (await (await mestre.Client.PostAsync($"/api/tokens/{sobreAParede.Id}/duplicar", null)).Content.ReadFromJsonAsync<TokenDto>(ApiTestHelpers.Json))!;

        // (0..2, 0) é parede; a porta aberta em (3,0) não bloqueia e recebe o token da jogadora.
        Assert.Equal((3, 0), (estado.TokenDe(jogadora)!.X, estado.TokenDe(jogadora)!.Y));
        Assert.Equal((4, 0), (semPosicao.X, semPosicao.Y));
        Assert.Equal((1, 0), (sobreAParede.X, sobreAParede.Y));
        // Ao redor de (1,0), a primeira célula sem token e sem parede fica na linha de baixo.
        Assert.Equal((0, 1), (copia.X, copia.Y));
    }

    [Fact]
    public async Task EventosDeEstrutura_ChegamAoGrupo_EOEstadoAoEntrarNaMesaAsTraz()
    {
        var (mestre, jogadora, mesaId, mapa) = await MontarMesaAsync();
        var (conexaoDaJogadora, eventosDaJogadora, _) = await ConectarAsync(jogadora, mesaId);
        await using var _1 = conexaoDaJogadora;

        var parede = await mestre.CriarEstruturaAsync(mapa.Id, Estrutura(x: 4, y: 2, largura: 3));
        Assert.Equal("EstruturaAdicionada:Parede:4,2", await ProximoAsync(eventosDaJogadora));

        (await mestre.Client.PutAsJsonAsync($"/api/estruturas/{parede.Id}", Estrutura(x: 4, y: 3, largura: 3, versao: 1), ApiTestHelpers.Json)).EnsureSuccessStatusCode();
        Assert.Equal("EstruturaAtualizada:Parede:4,3:bloqueia=True", await ProximoAsync(eventosDaJogadora));

        var (reconectada, _, lobby) = await ConectarAsync(jogadora, mesaId);
        await reconectada.DisposeAsync();
        var noEstado = Assert.Single(lobby.MapaAtivo!.Estruturas);
        Assert.Equal((parede.Id, 4, 3, 3, 2), (noEstado.Id, noEstado.X, noEstado.Y, noEstado.Largura, noEstado.Versao));

        (await mestre.Client.DeleteAsync($"/api/estruturas/{parede.Id}")).EnsureSuccessStatusCode();
        Assert.Equal($"EstruturaRemovida:{parede.Id}", await ProximoAsync(eventosDaJogadora));
    }

    [Fact]
    public async Task EstruturasDeMapaEmPreparacao_SoOMestreRecebe()
    {
        var (mestre, jogadora, mesaId, _) = await MontarMesaAsync();
        var (conexaoDoMestre, eventosDoMestre, _) = await ConectarAsync(mestre, mesaId);
        await using var _1 = conexaoDoMestre;
        var (conexaoDaJogadora, eventosDaJogadora, _) = await ConectarAsync(jogadora, mesaId);
        await using var _2 = conexaoDaJogadora;
        var preparado = await mestre.CriarMapaAsync(mesaId, "Emboscada");

        var porta = await mestre.CriarEstruturaAsync(preparado.Id, Estrutura(TipoEstrutura.Porta, x: 1, y: 1));
        await mestre.DefinirPortaAsync(porta.Id, aberta: true);
        (await mestre.Client.DeleteAsync($"/api/estruturas/{porta.Id}")).EnsureSuccessStatusCode();

        Assert.Equal("EstruturaAdicionada:Porta:1,1", await ProximoAsync(eventosDoMestre));
        Assert.Equal("EstruturaAtualizada:Porta:1,1:bloqueia=False", await ProximoAsync(eventosDoMestre));
        Assert.Equal($"EstruturaRemovida:{porta.Id}", await ProximoAsync(eventosDoMestre));
        await AssertNadaChegaAsync(eventosDaJogadora);
    }
}
