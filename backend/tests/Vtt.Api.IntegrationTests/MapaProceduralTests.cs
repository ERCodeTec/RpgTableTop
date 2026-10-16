using System.Net;
using System.Net.Http.Json;
using Vtt.Application.Mapas;
using Vtt.Domain.Mapas;

namespace Vtt.Api.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class MapaProceduralTests(VttApiFactory factory)
{
    private static async Task<MapaEstadoDto> GerarAsync(Sessao mestre, Guid mesaId, MapaProceduralRequest request)
    {
        var resposta = await mestre.Client.PostAsJsonAsync($"/api/mesas/{mesaId}/mapas/procedural", request);
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<MapaEstadoDto>(ApiTestHelpers.Json))!;
    }

    [Fact]
    public async Task Mestre_GeraMasmorraInativa_ComPisoParedesEBordaFechada()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();

        var estado = await GerarAsync(mestre, mesa.Id, new MapaProceduralRequest());

        Assert.False(estado.Mapa.Ativo);
        Assert.Equal((GeradorDeMasmorra.LarguraPadrao, GeradorDeMasmorra.AlturaPadrao), (estado.Mapa.Largura, estado.Mapa.Altura));
        Assert.False(string.IsNullOrWhiteSpace(estado.Mapa.Nome));
        Assert.Contains(estado.Estruturas, e => e.Tipo == TipoEstrutura.Chao);
        Assert.Contains(estado.Estruturas, e => e.Tipo == TipoEstrutura.Parede);
        // Sem tema, as salas, corredores e paredes já nascem com texturas prontas da masmorra.
        var daMasmorra = TexturasPadrao.Todas.Where(t => t.Tema == GeradorDeMasmorra.TemaPadrao).Select(t => t.Chave).ToHashSet();
        Assert.Contains(estado.Estruturas, e => e.Tipo == TipoEstrutura.Chao && daMasmorra.Contains(e.TexturaPadrao!));
        Assert.Contains(estado.Estruturas, e => e.Tipo == TipoEstrutura.Parede && daMasmorra.Contains(e.TexturaPadrao!));
        Assert.All(estado.Estruturas, e => Assert.True(e.X + e.Largura <= estado.Mapa.Largura && e.Y + e.Profundidade <= estado.Mapa.Altura));

        // O que foi gravado é o que voltou na resposta.
        var gravado = await mestre.ObterEstadoDoMapaAsync(estado.Mapa.Id);
        Assert.Equal(estado.Estruturas.Count, gravado.Estruturas.Count);

        var (largura, altura) = (estado.Mapa.Largura, estado.Mapa.Altura);
        var bloqueadas = Celulas(estado.Estruturas.Where(e => e.Tipo != TipoEstrutura.Chao && e.Tipo != TipoEstrutura.Porta));
        for (var x = 0; x < largura; x++)
        {
            Assert.Contains((x, 0), bloqueadas);
            Assert.Contains((x, altura - 1), bloqueadas);
        }

        for (var y = 0; y < altura; y++)
        {
            Assert.Contains((0, y), bloqueadas);
            Assert.Contains((largura - 1, y), bloqueadas);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(777)]
    [InlineData(123456)]
    public async Task TodoOPisoLivre_FicaConectado_ContandoPortasComoPassagem(int semente)
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();

        var estado = await GerarAsync(mestre, mesa.Id, new MapaProceduralRequest(Largura: 40, Altura: 30, Semente: semente));

        var piso = Celulas(estado.Estruturas.Where(e => e.Tipo is TipoEstrutura.Chao or TipoEstrutura.Porta));
        var bloqueadas = Celulas(estado.Estruturas.Where(e => e.Tipo is not (TipoEstrutura.Chao or TipoEstrutura.Porta)));
        var livres = piso.Where(c => !bloqueadas.Contains(c)).ToHashSet();
        Assert.NotEmpty(livres);

        var alcancadas = new HashSet<(int X, int Y)> { livres.First() };
        var fila = new Queue<(int X, int Y)>(alcancadas);
        while (fila.TryDequeue(out var celula))
        {
            foreach (var vizinha in new[] { (celula.X + 1, celula.Y), (celula.X - 1, celula.Y), (celula.X, celula.Y + 1), (celula.X, celula.Y - 1) })
            {
                if (livres.Contains(vizinha) && alcancadas.Add(vizinha))
                {
                    fila.Enqueue(vizinha);
                }
            }
        }

        Assert.Equal(livres.Count, alcancadas.Count);
    }

    [Fact]
    public async Task MesmaSemente_GeraOMesmoMapa()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var pedido = new MapaProceduralRequest(Largura: 30, Altura: 20, Semente: 2024);

        var primeiro = await GerarAsync(mestre, mesa.Id, pedido);
        var segundo = await GerarAsync(mestre, mesa.Id, pedido);

        Assert.NotEqual(primeiro.Mapa.Id, segundo.Mapa.Id);
        Assert.Equal(primeiro.Mapa.Nome, segundo.Mapa.Nome);
        Assert.Equal(Desenho(primeiro), Desenho(segundo));
    }

    [Fact]
    public async Task AoAtivar_OTokenDoJogadorNasceNoPiso()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (jogador, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id);
        var gerado = await GerarAsync(mestre, mesa.Id, new MapaProceduralRequest(Semente: 99));

        var estado = await mestre.AtivarMapaAsync(gerado.Mapa.Id);

        var token = estado.TokenDe(jogador)!;
        var piso = Celulas(estado.Estruturas.Where(e => e.Tipo == TipoEstrutura.Chao));
        Assert.Contains((token.X, token.Y), piso);
        Assert.DoesNotContain(estado.Estruturas, e => e.Bloqueia && token.X >= e.X && token.X < e.X + e.Largura && token.Y >= e.Y && token.Y < e.Y + e.Profundidade);
    }

    [Fact]
    public async Task Jogador_NaoGera_ENaoMembroNaoEnxerga()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var (jogador, _) = await factory.AdicionarJogadorAsync(mestre, mesa.Id);
        var estranho = await factory.CriarSessaoAsync();
        var url = $"/api/mesas/{mesa.Id}/mapas/procedural";

        Assert.Equal(HttpStatusCode.Forbidden, (await jogador.Client.PostAsJsonAsync(url, new MapaProceduralRequest())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await estranho.Client.PostAsJsonAsync(url, new MapaProceduralRequest())).StatusCode);
    }

    public static TheoryData<string> TemasGeraveis() => new(GeradorDeMasmorra.Temas.Select(t => t.Id));

    [Theory]
    [MemberData(nameof(TemasGeraveis))]
    public async Task Gerar_ComTema_UsaAsTexturasDoTemaNoPisoENasParedes_EMantemODesenho(string tema)
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();
        var temaDe = TexturasPadrao.Todas.ToDictionary(t => t.Chave, t => t.Tema);

        var comTema = await GerarAsync(mestre, mesa.Id, new MapaProceduralRequest(Semente: 7, Tema: tema));
        var masmorra = await GerarAsync(mestre, mesa.Id, new MapaProceduralRequest(Semente: 7));

        var salas = comTema.Estruturas.Where(e => e.Tipo == TipoEstrutura.Chao && !e.Bloqueia).ToList();
        var paredes = comTema.Estruturas.Where(e => e.Tipo == TipoEstrutura.Parede && e.Altura == 2).ToList();
        Assert.All(salas.Concat(paredes), e => Assert.Equal(tema, temaDe[e.TexturaPadrao!]));
        // Toda textura usada existe no catálogo e serve para o tipo da estrutura.
        Assert.All(comTema.Estruturas.Where(e => e.TexturaPadrao is not null), e =>
            Assert.Contains(e.Tipo, TexturasPadrao.Todas.Single(t => t.Chave == e.TexturaPadrao).Estruturas));
        // O tema muda as texturas, não as paredes: o que bloqueia é o mesmo da masmorra com a mesma semente.
        Assert.Equal(Celulas(masmorra.Estruturas.Where(e => e.Tipo == TipoEstrutura.Parede)), Celulas(paredes.Concat(comTema.Estruturas.Where(e => e.Tipo == TipoEstrutura.Parede && e.Altura != 2))));
    }

    [Fact]
    public async Task Gerar_ComTemaDesconhecido_Retorna400()
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();

        var resposta = await mestre.Client.PostAsJsonAsync($"/api/mesas/{mesa.Id}/mapas/procedural", new MapaProceduralRequest(Tema: "lua"));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Theory]
    [InlineData(11, 20)]
    [InlineData(20, 101)]
    public async Task Gerar_ComDimensoesForaDaFaixa_Retorna400(int largura, int altura)
    {
        var mestre = await factory.CriarSessaoAsync();
        var mesa = await mestre.CriarMesaAsync();

        var resposta = await mestre.Client.PostAsJsonAsync(
            $"/api/mesas/{mesa.Id}/mapas/procedural", new MapaProceduralRequest(Largura: largura, Altura: altura));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    private static HashSet<(int X, int Y)> Celulas(IEnumerable<EstruturaDto> estruturas)
    {
        var celulas = new HashSet<(int X, int Y)>();
        foreach (var e in estruturas)
        {
            for (var y = e.Y; y < e.Y + e.Profundidade; y++)
            {
                for (var x = e.X; x < e.X + e.Largura; x++)
                {
                    celulas.Add((x, y));
                }
            }
        }

        return celulas;
    }

    private static List<string> Desenho(MapaEstadoDto estado) => estado.Estruturas
        .Select(e => $"{e.Tipo}:{e.X},{e.Y},{e.Largura}x{e.Profundidade}:{e.Altura}:{e.Cor}")
        .Order()
        .ToList();
}
