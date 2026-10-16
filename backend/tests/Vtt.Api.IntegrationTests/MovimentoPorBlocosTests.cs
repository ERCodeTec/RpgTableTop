using System.Net.Http.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Vtt.Api.Hubs;
using Vtt.Application.Mapas;
using Vtt.Application.Personagens;
using Vtt.Domain.Mapas;
using Vtt.Domain.Personagens;
using static Vtt.Api.IntegrationTests.ImagemTestHelpers;

namespace Vtt.Api.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class MovimentoPorBlocosTests(VttApiFactory factory)
{
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Silencio = TimeSpan.FromMilliseconds(400);

    /// <summary>
    /// Registra em ordem os eventos de movimento: "Movido:x,y" e "Movimento:restante/deslocamento",
    /// com "*" quando há movimento pendente.
    /// </summary>
    private static ChannelReader<string> Escutar(HubConnection conexao)
    {
        var eventos = Channel.CreateUnbounded<string>();
        conexao.On<Guid, Guid, Guid, int, int, int>("TokenMovido", (_, _, _, x, y, _) => eventos.Writer.TryWrite($"Movido:{x},{y}"));
        conexao.On<Guid, Guid, MovimentoDto>("MovimentoAlterado", (_, _, movimento) => eventos.Writer.TryWrite(
            $"Movimento:{movimento.MovimentoRestante}/{movimento.Deslocamento}{(movimento.MovimentoPendente ? "*" : "")}"));
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

    private async Task<(HubConnection Conexao, ChannelReader<string> Eventos)> ConectarAsync(Sessao sessao, Guid mesaId)
    {
        var conexao = factory.CriarConexaoDoHub(sessao);
        var eventos = Escutar(conexao);
        await conexao.StartAsync();
        await conexao.InvokeAsync<LobbyDto>("EntrarNaMesa", mesaId);
        return (conexao, eventos);
    }

    /// <summary>
    /// Mesa com Mestre e Alice (e Bob, se pedido) e um mapa ativo de 10x8. Os tokens dos jogadores
    /// nascem em (0,0) e (1,0), com o deslocamento padrão de 6.
    /// </summary>
    private async Task<(Sessao Mestre, Sessao Alice, Sessao? Bob, Guid MesaId, MapaEstadoDto Estado)> MontarMesaAsync(bool comBob = false)
    {
        var mestre = await factory.CriarSessaoAsync("Mestre");
        var mesa = await mestre.CriarMesaAsync();
        var (alice, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Alice");
        var bob = comBob ? (await factory.AdicionarJogadorAsync(mestre, mesa.Id, "Bob")).Jogador : null;
        var estado = await mestre.CriarMapaAtivoAsync(mesa.Id);
        return (mestre, alice, bob, mesa.Id, estado);
    }

    private static async Task<TokenDto> OcultarAsync(Sessao mestre, TokenDto token)
    {
        var resposta = await mestre.Client.PutAsJsonAsync($"/api/tokens/{token.Id}/exibicao", new ExibicaoRequest(false, null), ApiTestHelpers.Json);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<TokenDto>(ApiTestHelpers.Json))!;
    }

    [Fact]
    public async Task MoverDentroDoAlcance_DescontaOCaminhoMinimo_EOMovimentoSoChegaAoMestreEAoDono()
    {
        var (mestre, alice, bob, mesaId, estado) = await MontarMesaAsync(comBob: true);
        var (conexaoDoMestre, eventosDoMestre) = await ConectarAsync(mestre, mesaId);
        await using var _1 = conexaoDoMestre;
        var (conexaoDaAlice, eventosDaAlice) = await ConectarAsync(alice, mesaId);
        await using var _2 = conexaoDaAlice;
        var (conexaoDoBob, eventosDoBob) = await ConectarAsync(bob!, mesaId);
        await using var _3 = conexaoDoBob;
        var token = estado.TokenDe(alice)!;
        Assert.Equal((6, 6, false), (token.Deslocamento, token.MovimentoRestante, token.MovimentoPendente));

        // Diagonal custa 1: (0,0) até (3,2) são 3 passos.
        var resultado = await conexaoDaAlice.MoverPorBlocosAsync(token, 3, 2);

        Assert.True(resultado.Aceito);
        Assert.Equal((3, 2, 2), (resultado.Token!.X, resultado.Token.Y, resultado.Token.Versao));
        Assert.Equal((3, true), (resultado.Token.MovimentoRestante, resultado.Token.MovimentoPendente));
        foreach (var eventos in new[] { eventosDoMestre, eventosDaAlice })
        {
            Assert.Equal("Movido:3,2", await ProximoAsync(eventos));
            Assert.Equal("Movimento:3/6*", await ProximoAsync(eventos));
        }

        // O Bob vê o token andar, mas não o movimento dele.
        Assert.Equal("Movido:3,2", await ProximoAsync(eventosDoBob));
        await AssertNadaChegaAsync(eventosDoBob);
        var paraOBob = (await bob!.ObterEstadoDoMapaAsync(estado.Mapa.Id)).TokenDe(alice)!;
        Assert.Equal((null, null, null), (paraOBob.Deslocamento, paraOBob.MovimentoRestante, paraOBob.MovimentoPendente));
    }

    [Fact]
    public async Task ForaDoAlcance_ERejeitado_EOLimiteExatoPassa()
    {
        var (_, alice, _, mesaId, estado) = await MontarMesaAsync();
        var (conexao, eventos) = await ConectarAsync(alice, mesaId);
        await using var _1 = conexao;
        var token = estado.TokenDe(alice)!;

        var longe = await conexao.MoverPorBlocosAsync(token, 7, 0);
        await AssertNadaChegaAsync(eventos);
        var noLimite = await conexao.MoverPorBlocosAsync(token, 6, 6);

        Assert.False(longe.Aceito);
        Assert.Contains("Fora do alcance", longe.Motivo);
        Assert.Equal((0, 0, 6), (longe.Token!.X, longe.Token.Y, longe.Token.MovimentoRestante));
        Assert.True(noLimite.Aceito);
        Assert.Equal(0, noLimite.Token!.MovimentoRestante);
    }

    [Fact]
    public async Task Diagonal_NaoCortaOCantoDeEstrutura_EODesvioEntraNoCusto()
    {
        var (mestre, alice, _, mesaId, estado) = await MontarMesaAsync();
        await mestre.CriarEstruturaAsync(estado.Mapa.Id, Estrutura(x: 1, y: 0));
        var (conexao, _) = await ConectarAsync(alice, mesaId);
        await using var _1 = conexao;
        var token = estado.TokenDe(alice)!;

        // A diagonal (0,0) → (1,1) rasparia a parede: contorna por (0,1) e custa 2.
        var comUmaParede = await conexao.MoverPorBlocosAsync(token, 1, 1);
        var desfeito = await conexao.DesfazerMovimentoAsync(comUmaParede.Token!);
        await mestre.CriarEstruturaAsync(estado.Mapa.Id, Estrutura(x: 0, y: 1));
        var naQuina = await conexao.MoverPorBlocosAsync(desfeito.Token!, 1, 1);
        var naParede = await conexao.MoverPorBlocosAsync(desfeito.Token!, 1, 0);

        Assert.True(comUmaParede.Aceito);
        Assert.Equal(4, comUmaParede.Token!.MovimentoRestante);
        Assert.True(desfeito.Aceito);
        Assert.False(naQuina.Aceito);
        Assert.Contains("Fora do alcance", naQuina.Motivo);
        Assert.False(naParede.Aceito);
        Assert.Contains("estrutura bloqueando", naParede.Motivo);
    }

    [Fact]
    public async Task Tokens_DeixamPassarMasNaoDividemACelula_EOcultosNaoContamParaOJogador()
    {
        var (mestre, alice, _, mesaId, estado) = await MontarMesaAsync();
        var mapaId = estado.Mapa.Id;

        // Um túnel de uma célula na linha y = 0, com um goblin no meio do caminho.
        await mestre.CriarEstruturaAsync(mapaId, Estrutura(x: 0, y: 1, largura: 10));
        await mestre.CriarTokenAsync(mapaId, "Goblin A", x: 1, y: 0);
        var oculto = await mestre.CriarTokenAsync(mapaId, "Goblin B", x: 4, y: 0);
        await OcultarAsync(mestre, oculto);
        var (conexao, _) = await ConectarAsync(alice, mesaId);
        await using var _1 = conexao;
        var token = estado.TokenDe(alice)!;

        var emCima = await conexao.MoverPorBlocosAsync(token, 1, 0);
        var atravessou = await conexao.MoverPorBlocosAsync(token, 3, 0);
        var desfeito = await conexao.DesfazerMovimentoAsync(atravessou.Token!);
        var sobreOOculto = await conexao.MoverPorBlocosAsync(desfeito.Token!, 4, 0);

        Assert.False(emCima.Aceito);
        Assert.Contains("Já existe um token", emCima.Motivo);
        Assert.True(atravessou.Aceito);
        Assert.Equal((3, 0, 3), (atravessou.Token!.X, atravessou.Token.Y, atravessou.Token.MovimentoRestante));
        Assert.True(sobreOOculto.Aceito);
        Assert.Equal(2, sobreOOculto.Token!.MovimentoRestante);
    }

    [Fact]
    public async Task TokenCaido_OJogadorNaoFazMaisNada_MasOMestreMoveLivremente()
    {
        var (mestre, alice, _, mesaId, estado) = await MontarMesaAsync();
        var (conexaoDaAlice, eventos) = await ConectarAsync(alice, mesaId);
        await using var _1 = conexaoDaAlice;
        var (conexaoDoMestre, _) = await ConectarAsync(mestre, mesaId);
        await using var _2 = conexaoDoMestre;

        // Disparou e andou um passo, com o movimento ainda pendente; então caiu.
        var disparou = (await conexaoDaAlice.DispararAsync(estado.TokenDe(alice)!)).Token!;
        var andou = (await conexaoDaAlice.MoverPorBlocosAsync(disparou, 1, 1)).Token!;
        Assert.Equal((11, true), (andou.MovimentoRestante, andou.MovimentoPendente));
        (await mestre.Client.PutAsJsonAsync($"/api/tokens/{andou.Id}/hp", new HpRequest(null, 0, null))).EnsureSuccessStatusCode();
        var caido = (await mestre.ObterEstadoDoMapaAsync(estado.Mapa.Id)).TokenDe(alice)!;
        Assert.True(caido.Caido);
        while (eventos.TryRead(out _))
        {
        }

        var tentativas = new[]
        {
            await conexaoDaAlice.MoverPorBlocosAsync(caido, 2, 2),
            await conexaoDaAlice.DesfazerMovimentoAsync(caido),
            await conexaoDaAlice.ConcluirMovimentoAsync(caido),
            await conexaoDaAlice.DesfazerDisparadaAsync(caido),
            await conexaoDaAlice.DispararAsync(caido),
        };
        await AssertNadaChegaAsync(eventos);

        foreach (var tentativa in tentativas)
        {
            Assert.False(tentativa.Aceito);
            Assert.Contains("caído", tentativa.Motivo);
            Assert.Equal((1, 1, 11, true), (tentativa.Token!.X, tentativa.Token.Y, tentativa.Token.MovimentoRestante, tentativa.Token.MovimentoPendente));
        }

        // O Mestre desfaz, move por blocos e arrasta o token caído.
        var desfeito = await conexaoDoMestre.DesfazerMovimentoAsync(caido);
        var porBlocos = await conexaoDoMestre.MoverPorBlocosAsync(desfeito.Token!, 2, 2);
        var arrastado = await conexaoDoMestre.MoverTokenAsync(porBlocos.Token!, 5, 5);

        Assert.True(desfeito.Aceito);
        Assert.Equal((0, 0, 12), (desfeito.Token!.X, desfeito.Token.Y, desfeito.Token.MovimentoRestante));
        Assert.True(porBlocos.Aceito);
        Assert.Equal((2, 2, 10), (porBlocos.Token!.X, porBlocos.Token.Y, porBlocos.Token.MovimentoRestante));
        Assert.True(arrastado.Aceito);
        Assert.Equal((5, 5), (arrastado.Token!.X, arrastado.Token.Y));
    }

    [Fact]
    public async Task Pendencia_BloqueiaNovoMovimento_DesfazerDevolve_EConcluirConfirma()
    {
        var (_, alice, _, mesaId, estado) = await MontarMesaAsync();
        var (conexao, eventos) = await ConectarAsync(alice, mesaId);
        await using var _1 = conexao;
        var token = estado.TokenDe(alice)!;

        var primeiro = (await conexao.MoverPorBlocosAsync(token, 2, 0)).Token!;
        var semConcluir = await conexao.MoverPorBlocosAsync(primeiro, 3, 0);
        var atrasado = await conexao.DesfazerMovimentoAsync(primeiro, versaoEsperada: 1);
        var desfeito = await conexao.DesfazerMovimentoAsync(primeiro);

        Assert.False(semConcluir.Aceito);
        Assert.Contains("Conclua ou desfaça", semConcluir.Motivo);
        Assert.False(atrasado.Aceito);
        Assert.Contains("alterado por outra pessoa", atrasado.Motivo);
        Assert.True(desfeito.Aceito);
        Assert.Equal((0, 0, 6, false), (desfeito.Token!.X, desfeito.Token.Y, desfeito.Token.MovimentoRestante, desfeito.Token.MovimentoPendente));
        Assert.Equal(["Movido:2,0", "Movimento:4/6*", "Movido:0,0", "Movimento:6/6"],
            [await ProximoAsync(eventos), await ProximoAsync(eventos), await ProximoAsync(eventos), await ProximoAsync(eventos)]);

        var segundo = (await conexao.MoverPorBlocosAsync(desfeito.Token, 4, 0)).Token!;
        var concluido = await conexao.ConcluirMovimentoAsync(segundo);
        var nadaADesfazer = await conexao.DesfazerMovimentoAsync(concluido.Token!);
        var terceiro = await conexao.MoverPorBlocosAsync(concluido.Token!, 6, 0);

        Assert.True(concluido.Aceito);
        Assert.Equal((4, 0, 2, false), (concluido.Token!.X, concluido.Token.Y, concluido.Token.MovimentoRestante, concluido.Token.MovimentoPendente));
        Assert.False(nadaADesfazer.Aceito);
        Assert.Contains("Não há movimento para desfazer", nadaADesfazer.Motivo);
        Assert.True(terceiro.Aceito);
        Assert.Equal(0, terceiro.Token!.MovimentoRestante);
    }

    [Fact]
    public async Task Desfazer_ComAOrigemOcupada_ERejeitado()
    {
        var (mestre, alice, _, mesaId, estado) = await MontarMesaAsync();
        var goblin = await mestre.CriarTokenAsync(estado.Mapa.Id, x: 5, y: 5);
        var (conexaoDaAlice, _) = await ConectarAsync(alice, mesaId);
        await using var _1 = conexaoDaAlice;
        var (conexaoDoMestre, _) = await ConectarAsync(mestre, mesaId);
        await using var _2 = conexaoDoMestre;

        var movido = (await conexaoDaAlice.MoverPorBlocosAsync(estado.TokenDe(alice)!, 2, 2)).Token!;
        Assert.True((await conexaoDoMestre.MoverTokenAsync(goblin, 0, 0)).Aceito);
        var desfeito = await conexaoDaAlice.DesfazerMovimentoAsync(movido);

        Assert.False(desfeito.Aceito);
        Assert.Contains("origem foi ocupada", desfeito.Motivo);
        Assert.Equal((2, 2, true), (desfeito.Token!.X, desfeito.Token.Y, desfeito.Token.MovimentoPendente));
    }

    [Fact]
    public async Task Jogador_NaoUsaOArrasteLivre_NemMovePorBlocosOTokenDeOutro()
    {
        var (mestre, alice, _, mesaId, estado) = await MontarMesaAsync();
        var goblin = await mestre.CriarTokenAsync(estado.Mapa.Id, x: 5, y: 5);
        var (conexao, eventos) = await ConectarAsync(alice, mesaId);
        await using var _1 = conexao;
        var token = estado.TokenDe(alice)!;

        var arrastando = await conexao.MoverTokenAsync(token, 1, 1);
        var oGoblin = await conexao.MoverPorBlocosAsync(goblin, 5, 6);

        Assert.False(arrastando.Aceito);
        Assert.Contains("células destacadas", arrastando.Motivo);
        Assert.Equal((0, 0), (arrastando.Token!.X, arrastando.Token.Y));
        Assert.False(oGoblin.Aceito);
        Assert.Contains("seu próprio token", oGoblin.Motivo);
        await AssertNadaChegaAsync(eventos);
    }

    [Fact]
    public async Task Mestre_MovePorBlocosQualquerToken_ComAsMesmasRegras_EOArrasteDescartaAPendencia()
    {
        var (mestre, alice, _, mesaId, estado) = await MontarMesaAsync();
        await mestre.CriarEstruturaAsync(estado.Mapa.Id, Estrutura(x: 5, y: 3));
        var goblin = await mestre.CriarTokenAsync(estado.Mapa.Id, x: 5, y: 5);
        var (conexao, _) = await ConectarAsync(mestre, mesaId);
        await using var _1 = conexao;

        var naParede = await conexao.MoverPorBlocosAsync(goblin, 5, 3);
        // Contornando sem raspar a parede: (4,4), (4,3), (4,2), (5,2).
        var andou = (await conexao.MoverPorBlocosAsync(goblin, 5, 2)).Token!;
        var arrastado = await conexao.MoverTokenAsync(andou, 9, 7);
        var daAlice = (await conexao.MoverPorBlocosAsync(estado.TokenDe(alice)!, 1, 1)).Token!;

        Assert.False(naParede.Aceito);
        Assert.Contains("estrutura bloqueando", naParede.Motivo);
        Assert.Equal((2, true), (andou.MovimentoRestante, andou.MovimentoPendente));
        Assert.True(arrastado.Aceito);
        Assert.Equal((9, 7, 2, false), (arrastado.Token!.X, arrastado.Token.Y, arrastado.Token.MovimentoRestante, arrastado.Token.MovimentoPendente));
        Assert.Equal((1, 1, 5), (daAlice.X, daAlice.Y, daAlice.MovimentoRestante));
    }

    [Fact]
    public async Task NovaRodada_EhDoMestre_ERestauraTodosOsTokensDoMapa_ERestaurarValeParaUm()
    {
        var (mestre, alice, _, mesaId, estado) = await MontarMesaAsync();
        var goblin = await mestre.CriarTokenAsync(estado.Mapa.Id, x: 5, y: 5);
        var (conexaoDaAlice, eventosDaAlice) = await ConectarAsync(alice, mesaId);
        await using var _1 = conexaoDaAlice;
        var (conexaoDoMestre, _) = await ConectarAsync(mestre, mesaId);
        await using var _2 = conexaoDoMestre;
        await conexaoDaAlice.AndarAsync(estado.TokenDe(alice)!, 2, 2);
        var goblinMovido = (await conexaoDoMestre.MoverPorBlocosAsync(goblin, 5, 7)).Token!;
        // Do goblin, a Alice vê só a posição.
        Assert.Equal(["Movido:2,2", "Movimento:4/6*", "Movimento:4/6", "Movido:5,7"],
            [await ProximoAsync(eventosDaAlice), await ProximoAsync(eventosDaAlice), await ProximoAsync(eventosDaAlice), await ProximoAsync(eventosDaAlice)]);

        var pelaAlice = await Assert.ThrowsAsync<HubException>(() => conexaoDaAlice.InvokeAsync("NovaRodada", estado.Mapa.Id));
        await Assert.ThrowsAsync<HubException>(() => conexaoDaAlice.InvokeAsync<TokenDto>("RestaurarMovimento", goblin.Id));
        await conexaoDoMestre.InvokeAsync("NovaRodada", estado.Mapa.Id);

        Assert.Contains("Mestre", pelaAlice.Message);
        // A Alice só recebe o movimento do próprio token.
        Assert.Equal("Movimento:6/6", await ProximoAsync(eventosDaAlice));
        await AssertNadaChegaAsync(eventosDaAlice);
        var depois = await mestre.ObterEstadoDoMapaAsync(estado.Mapa.Id);
        Assert.All(depois.Tokens, t => Assert.Equal((t.Deslocamento, false), (t.MovimentoRestante, t.MovimentoPendente)));
        Assert.True(depois.Tokens.Single(t => t.Id == goblin.Id).Versao > goblinMovido.Versao);

        var andou = (await conexaoDoMestre.AndarAsync(depois.Tokens.Single(t => t.Id == goblin.Id), 5, 5)).Token!;
        var restaurado = await conexaoDoMestre.InvokeAsync<TokenDto>("RestaurarMovimento", goblin.Id);
        Assert.Equal(4, andou.MovimentoRestante);
        Assert.Equal((6, false), (restaurado.MovimentoRestante, restaurado.MovimentoPendente));
    }

    [Fact]
    public async Task Disparada_DobraOMovimento_UmaVezPorRodada_EANovaRodadaDevolve()
    {
        var (mestre, alice, bob, mesaId, estado) = await MontarMesaAsync(comBob: true);
        var (conexaoDaAlice, eventosDaAlice) = await ConectarAsync(alice, mesaId);
        await using var _1 = conexaoDaAlice;
        var (conexaoDoBob, eventosDoBob) = await ConectarAsync(bob!, mesaId);
        await using var _2 = conexaoDoBob;
        var (conexaoDoMestre, _) = await ConectarAsync(mestre, mesaId);
        await using var _3 = conexaoDoMestre;
        var token = estado.TokenDe(alice)!;

        // Andou 2 (resta 4); a Disparada soma mais 6, e o caminho até (9,0) custa 7.
        var andou = (await conexaoDaAlice.AndarAsync(token, 2, 0)).Token!;
        var pelaOutra = await conexaoDoBob.DispararAsync(andou);
        var disparou = await conexaoDaAlice.DispararAsync(andou);
        var deNovo = await conexaoDaAlice.DispararAsync(disparou.Token!);
        var longe = await conexaoDaAlice.MoverPorBlocosAsync(disparou.Token!, 9, 0);

        Assert.False(pelaOutra.Aceito);
        Assert.True(disparou.Aceito);
        Assert.Equal((10, true), (disparou.Token!.MovimentoRestante, disparou.Token.Disparada));
        Assert.False(deNovo.Aceito);
        Assert.Contains("já foi usada", deNovo.Motivo);
        Assert.True(longe.Aceito);
        Assert.Equal(3, longe.Token!.MovimentoRestante);
        Assert.Equal(["Movido:2,0", "Movimento:4/6*", "Movimento:4/6", "Movimento:10/6", "Movido:9,0", "Movimento:3/6*"],
            [await ProximoAsync(eventosDaAlice), await ProximoAsync(eventosDaAlice), await ProximoAsync(eventosDaAlice),
             await ProximoAsync(eventosDaAlice), await ProximoAsync(eventosDaAlice), await ProximoAsync(eventosDaAlice)]);

        // Desfazer devolve o custo até o máximo dobrado.
        var desfeito = await conexaoDaAlice.DesfazerMovimentoAsync(longe.Token);
        Assert.Equal(10, desfeito.Token!.MovimentoRestante);

        await conexaoDoMestre.InvokeAsync("NovaRodada", estado.Mapa.Id);
        var depois = (await mestre.ObterEstadoDoMapaAsync(estado.Mapa.Id)).TokenDe(alice)!;
        Assert.Equal((6, false), (depois.MovimentoRestante, depois.Disparada));
        var paraOBob = (await bob!.ObterEstadoDoMapaAsync(estado.Mapa.Id)).TokenDe(alice)!;
        Assert.Null(paraOBob.Disparada);
    }

    [Fact]
    public async Task DesfazerDisparada_DevolveODeslocamento_SoEnquantoOExtraNaoFoiGasto()
    {
        var (_, alice, _, mesaId, estado) = await MontarMesaAsync();
        var (conexao, _) = await ConectarAsync(alice, mesaId);
        await using var _1 = conexao;
        var token = estado.TokenDe(alice)!;

        var semDisparada = await conexao.DesfazerDisparadaAsync(token);
        var disparou = (await conexao.DispararAsync(token)).Token!;
        // O caminho até (8,0) custa 8: usa 2 das 6 células que a Disparada somou.
        var longe = (await conexao.MoverPorBlocosAsync(disparou, 8, 0)).Token!;
        var comExtraGasto = await conexao.DesfazerDisparadaAsync(longe);
        var voltou = (await conexao.DesfazerMovimentoAsync(longe)).Token!;
        var desfeita = await conexao.DesfazerDisparadaAsync(voltou);
        var deNovo = await conexao.DispararAsync(desfeita.Token!);

        Assert.False(semDisparada.Aceito);
        Assert.Contains("não foi usada", semDisparada.Motivo);
        Assert.False(comExtraGasto.Aceito);
        Assert.Contains("já foi gasto", comExtraGasto.Motivo);
        Assert.Equal((4, true), (comExtraGasto.Token!.MovimentoRestante, comExtraGasto.Token.Disparada));
        Assert.True(desfeita.Aceito);
        Assert.Equal((6, false), (desfeita.Token!.MovimentoRestante, desfeita.Token.Disparada));
        // Desfeita, a Disparada da rodada fica disponível de novo.
        Assert.True(deNovo.Aceito);
        Assert.Equal((12, true), (deNovo.Token!.MovimentoRestante, deNovo.Token.Disparada));
    }

    [Fact]
    public async Task Deslocamento_VemDaFichaNoTokenDoJogador_EDoMestreNosDemais()
    {
        var (mestre, alice, _, mesaId, estado) = await MontarMesaAsync();
        var (conexao, _) = await ConectarAsync(alice, mesaId);
        await using var _1 = conexao;
        var token = estado.TokenDe(alice)!;
        var personagem = (await alice.Client.GetFromJsonAsync<List<PersonagemDto>>("/api/personagens", ApiTestHelpers.Json))!.Single();
        Assert.Equal(Personagem.DeslocamentoPadrao, personagem.Deslocamento);
        await conexao.AndarAsync(token, 1, 0);

        // Ficha com 4 de deslocamento: o gasto (1) continua valendo.
        (await alice.Client.PutAsJsonAsync($"/api/personagens/{personagem.Id}",
            new PersonagemRequest(personagem.Nome, personagem.Classe, personagem.Nivel, personagem.HpMaximo, null, 4), ApiTestHelpers.Json)).EnsureSuccessStatusCode();
        var invalido = await alice.Client.PutAsJsonAsync($"/api/personagens/{personagem.Id}",
            new PersonagemRequest(personagem.Nome, personagem.Classe, personagem.Nivel, personagem.HpMaximo, null, 0), ApiTestHelpers.Json);
        var noToken = (await mestre.ObterEstadoDoMapaAsync(estado.Mapa.Id)).TokenDe(alice)!;

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, invalido.StatusCode);
        Assert.Equal((4, 3), (noToken.Deslocamento, noToken.MovimentoRestante));

        var criado = await mestre.Client.PostAsJsonAsync($"/api/mapas/{estado.Mapa.Id}/tokens",
            new CriarTokenRequest(TipoToken.Inimigo, "Lobo", 9, 9, "lobo", null, 5, 5, Deslocamento: 8), ApiTestHelpers.Json);
        criado.EnsureSuccessStatusCode();
        var lobo = (await criado.Content.ReadFromJsonAsync<TokenDto>(ApiTestHelpers.Json))!;
        var editado = await mestre.Client.PutAsJsonAsync($"/api/tokens/{lobo.Id}",
            new EditarTokenRequest("Lobo", 9, 9, "lobo", null, null, Deslocamento: 10), ApiTestHelpers.Json);
        editado.EnsureSuccessStatusCode();

        Assert.Equal((8, 8), (lobo.Deslocamento, lobo.MovimentoRestante));
        var loboEditado = (await editado.Content.ReadFromJsonAsync<TokenDto>(ApiTestHelpers.Json))!;
        Assert.Equal((10, 10), (loboEditado.Deslocamento, loboEditado.MovimentoRestante));
    }
}
