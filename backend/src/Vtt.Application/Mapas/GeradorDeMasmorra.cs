using Vtt.Domain.Mapas;

namespace Vtt.Application.Mapas;

/// <summary>
/// Gera uma masmorra a partir de uma semente: salas retangulares ligadas por corredores, com
/// paredes em volta do que é piso, rocha no restante, portas nas entradas das salas e pilares e
/// obstáculos dentro delas, e a entrada dos jogadores no centro da primeira sala. A mesma semente, as mesmas dimensões e o mesmo tema dão sempre o mesmo mapa.
/// </summary>
/// <remarks>
/// O tema escolhe as texturas pelas categorias do catálogo (<see cref="TexturasPadrao.DoPapel"/>):
/// cada parte do mapa (piso das salas, corredor, parede...) usa uma textura do tema com aquele
/// papel; sem nenhuma, usa a da masmorra. Com texturas de perigo (água, lava), parte dos obstáculos
/// vira uma poça que bloqueia a passagem.
/// </remarks>
/// <remarks>
/// Tudo o que é piso fica conectado: os corredores formam uma árvore entre as salas, e pilares e
/// obstáculos ficam só no miolo das salas, sem dois vizinhos, então nunca fecham uma passagem.
/// A borda do mapa é sempre rocha ou parede.
/// </remarks>
public static class GeradorDeMasmorra
{
    public const int DimensaoMinima = 12;
    public const int LarguraPadrao = 32;
    public const int AlturaPadrao = 24;

    private const int LadoMinimoDaSala = 4;
    private const int LadoMaximoDaSala = 10;
    private const double ChanceDePorta = 0.6;

    /// <summary>O tema sem escolha do Mestre, e o de reserva para o papel que um tema não tem.</summary>
    public const string TemaPadrao = "masmorra";

    /// <summary>Chance de um obstáculo virar uma poça de perigo, no tema que tem textura de perigo.</summary>
    private const double ChanceDePerigo = 0.4;

    private const string CorDaParede = "#6B6F76";
    private const string CorDaRocha = "#2B2E33";
    private const string CorDoPisoDaSala = "#5A5048";
    private const string CorDoCorredor = "#4A4238";
    private const string CorDoPilar = "#8C8C84";
    private const string CorDaPorta = "#8A5A2B";
    private const string CorDaEntrada = "#3CB371";
    private const int LadoDaEntrada = 2;
    private static readonly string[] CoresDeObstaculo = ["#7A5A3A", "#6E6A60", "#5E7A4A"];

    /// <summary>Os temas que o gerador sabe montar: os que têm ao menos um piso e uma parede.</summary>
    public static readonly IReadOnlyList<TemaDeTextura> Temas = TexturasPadrao.Temas
        .Where(tema => TexturasPadrao.DoPapel(tema.Id, TexturasPadrao.Papel.Piso).Count > 0
                       && TexturasPadrao.DoPapel(tema.Id, TexturasPadrao.Papel.Parede).Count > 0)
        .ToList();

    // Substantivos femininos, por causa dos adjetivos.
    private static readonly Dictionary<string, string[]> LugaresDoTema = new()
    {
        ["masmorra"] = ["Cripta", "Masmorra", "Catacumba"],
        ["castelo"] = ["Fortaleza", "Cidadela", "Muralha"],
        ["caverna"] = ["Caverna", "Mina", "Gruta"],
        ["vila"] = ["Vila", "Aldeia", "Taverna"],
        ["natureza"] = ["Floresta", "Clareira", "Mata"],
        ["neve"] = ["Geleira", "Tundra", "Montanha"],
        ["deserto"] = ["Ruína", "Tumba", "Pirâmide"],
        ["vulcao"] = ["Forja", "Cratera", "Caldeira"],
    };
    private static readonly string[] Adjetivos = ["Esquecida", "Sombria", "Perdida", "Profunda", "Amaldiçoada", "Silenciosa", "Antiga"];

    private readonly record struct Sala(int X, int Y, int Largura, int Altura)
    {
        public int CentroX => X + Largura / 2;
        public int CentroY => Y + Altura / 2;

        public bool Contem(int x, int y) => x >= X && y >= Y && x < X + Largura && y < Y + Altura;

        /// <summary>Se as duas salas se tocam ou ficam sem ao menos uma célula de parede entre elas.</summary>
        public bool EncostaEm(Sala outra) =>
            X - 1 < outra.X + outra.Largura && outra.X - 1 < X + Largura
            && Y - 1 < outra.Y + outra.Altura && outra.Y - 1 < Y + Altura;
    }

    public static bool TemaValido(string tema) => Temas.Any(t => t.Id == tema);

    public static string NomeDe(int semente, string tema = TemaPadrao)
    {
        var sorteio = new Random(semente);
        var lugares = LugaresDoTema.GetValueOrDefault(tema, LugaresDoTema[TemaPadrao]);
        return $"{lugares[sorteio.Next(lugares.Length)]} {Adjetivos[sorteio.Next(Adjetivos.Length)]} #{semente}";
    }

    /// <summary>As estruturas da masmorra, sem Id nem MapaId: quem chama as atribui.</summary>
    public static List<Estrutura> Gerar(int largura, int altura, int semente, string tema = TemaPadrao)
    {
        if (!TemaValido(tema))
        {
            throw new ArgumentOutOfRangeException(nameof(tema), $"Tema desconhecido: {tema}.");
        }

        if (largura < DimensaoMinima || altura < DimensaoMinima)
        {
            throw new ArgumentOutOfRangeException(nameof(largura), $"A masmorra precisa de ao menos {DimensaoMinima} x {DimensaoMinima} células.");
        }

        var sorteio = new Random(semente);
        var salas = SortearSalas(largura, altura, sorteio);

        var naSala = new bool[largura, altura];
        foreach (var sala in salas)
        {
            Marcar(naSala, sala.X, sala.Y, sala.Largura, sala.Altura);
        }

        var corredor = CavarCorredores(salas, naSala, largura, altura, sorteio);
        var piso = new bool[largura, altura];
        var parede = new bool[largura, altura];
        var rocha = new bool[largura, altura];
        for (var y = 0; y < altura; y++)
        {
            for (var x = 0; x < largura; x++)
            {
                piso[x, y] = naSala[x, y] || corredor[x, y];
            }
        }

        for (var y = 0; y < altura; y++)
        {
            for (var x = 0; x < largura; x++)
            {
                if (piso[x, y])
                {
                    continue;
                }

                if (VizinhoDePiso(piso, x, y))
                {
                    parede[x, y] = true;
                }
                else
                {
                    rocha[x, y] = true;
                }
            }
        }

        // As texturas saem de outro sorteio: o desenho da masmorra é o mesmo em qualquer tema.
        var texturas = new Texturas(tema, new Random(semente ^ 0x7E3A11));
        var estruturas = new List<Estrutura>();
        estruturas.AddRange(Retangulos(naSala).Select(r => Nova(TipoEstrutura.Chao, r, 0.1, CorDoPisoDaSala, bloqueia: false, texturas.PisoDaSala)));
        estruturas.AddRange(Retangulos(corredor).Select(r => Nova(TipoEstrutura.Chao, r, 0.1, CorDoCorredor, bloqueia: false, texturas.Corredor)));
        estruturas.AddRange(Retangulos(parede).Select(r => Nova(TipoEstrutura.Parede, r, 2, CorDaParede, bloqueia: true, texturas.Parede)));
        estruturas.AddRange(Retangulos(rocha).Select(r => Nova(TipoEstrutura.Parede, r, 0.4, CorDaRocha, bloqueia: true, texturas.Rocha)));
        estruturas.AddRange(Portas(corredor, naSala, piso, largura, altura, sorteio, texturas));

        var entrada = salas.Count > 0 ? AreaDaEntrada(salas[0]) : ((int X, int Y, int Largura, int Altura)?)null;
        estruturas.AddRange(Mobilia(salas, entrada, sorteio, texturas));
        if (entrada is { } area)
        {
            estruturas.Add(Nova(TipoEstrutura.Entrada, area, 0.1, CorDaEntrada, bloqueia: false));
        }

        return estruturas;
    }

    /// <summary>A textura de cada parte do mapa, escolhida uma vez por mapa entre as do tema.</summary>
    private sealed class Texturas
    {
        private readonly string tema;
        private readonly Random sorteio;

        public Texturas(string tema, Random sorteio)
        {
            this.tema = tema;
            this.sorteio = sorteio;
            PisoDaSala = Uma(TexturasPadrao.Papel.Piso);
            // O corredor prefere uma textura diferente da do piso das salas, para as salas se destacarem.
            Corredor = Uma(TexturasPadrao.Papel.Corredor, evitar: PisoDaSala) ?? PisoDaSala;
            Parede = Uma(TexturasPadrao.Papel.Parede);
            // Sem rocha própria no tema, a rocha fica só com a cor escura, como um vazio.
            Rocha = Do(tema, TexturasPadrao.Papel.Rocha) is { Count: > 0 } rochas ? rochas[sorteio.Next(rochas.Count)].Chave : null;
            Porta = Uma(TexturasPadrao.Papel.Porta);
            Pilar = Uma(TexturasPadrao.Papel.Pilar) ?? Parede;
            Obstaculos = Lista(TexturasPadrao.Papel.Obstaculo).Select(t => t.Chave).ToArray();
            Perigos = Do(tema, TexturasPadrao.Papel.Perigo).Select(t => t.Chave).ToArray();
        }

        public string? PisoDaSala { get; }
        public string? Corredor { get; }
        public string? Parede { get; }
        public string? Rocha { get; }
        public string? Porta { get; }
        public string? Pilar { get; }
        public string[] Obstaculos { get; }

        /// <summary>Água, lava...: só as do próprio tema; sem elas, nenhum obstáculo vira poça.</summary>
        public string[] Perigos { get; }

        public string? Sortear(string[] opcoes) => opcoes.Length == 0 ? null : opcoes[sorteio.Next(opcoes.Length)];

        public bool Chance(double chance) => sorteio.NextDouble() < chance;

        private static IReadOnlyList<TexturaPronta> Do(string tema, string papel) => TexturasPadrao.DoPapel(tema, papel);

        private IReadOnlyList<TexturaPronta> Lista(string papel) =>
            Do(tema, papel) is { Count: > 0 } doTema ? doTema : Do(TemaPadrao, papel);

        private string? Uma(string papel, string? evitar = null)
        {
            var opcoes = Lista(papel).Where(t => t.Chave != evitar).ToList();
            return opcoes.Count == 0 ? null : opcoes[sorteio.Next(opcoes.Count)].Chave;
        }
    }

    private static List<Sala> SortearSalas(int largura, int altura, Random sorteio)
    {
        var ladoMaximo = Math.Clamp(Math.Min(largura, altura) / 3, LadoMinimoDaSala, LadoMaximoDaSala);
        var alvo = Math.Clamp(largura * altura / 90, 2, 14);
        var salas = new List<Sala>();

        for (var tentativa = 0; tentativa < alvo * 15 && salas.Count < alvo; tentativa++)
        {
            var w = sorteio.Next(LadoMinimoDaSala, ladoMaximo + 1);
            var h = sorteio.Next(LadoMinimoDaSala, ladoMaximo + 1);
            // Uma célula de folga na borda para a parede.
            var sala = new Sala(sorteio.Next(1, largura - w), sorteio.Next(1, altura - h), w, h);
            if (!salas.Any(sala.EncostaEm))
            {
                salas.Add(sala);
            }
        }

        return salas;
    }

    /// <summary>Liga cada sala à mais próxima entre as anteriores (uma árvore) e, havendo salas de sobra, abre um atalho.</summary>
    private static bool[,] CavarCorredores(List<Sala> salas, bool[,] naSala, int largura, int altura, Random sorteio)
    {
        var corredor = new bool[largura, altura];
        var ligacoes = new List<(Sala, Sala)>();
        for (var i = 1; i < salas.Count; i++)
        {
            var atual = salas[i];
            var maisProxima = salas.Take(i)
                .MinBy(outra => Math.Abs(outra.CentroX - atual.CentroX) + Math.Abs(outra.CentroY - atual.CentroY));
            ligacoes.Add((atual, maisProxima));
        }

        if (salas.Count >= 4)
        {
            ligacoes.Add((salas[sorteio.Next(salas.Count)], salas[sorteio.Next(salas.Count)]));
        }

        foreach (var (a, b) in ligacoes)
        {
            var horizontalPrimeiro = sorteio.Next(2) == 0;
            var (cantoX, cantoY) = horizontalPrimeiro ? (b.CentroX, a.CentroY) : (a.CentroX, b.CentroY);
            CavarReta(corredor, naSala, a.CentroX, a.CentroY, cantoX, cantoY);
            CavarReta(corredor, naSala, cantoX, cantoY, b.CentroX, b.CentroY);
        }

        return corredor;
    }

    private static void CavarReta(bool[,] corredor, bool[,] naSala, int x0, int y0, int x1, int y1)
    {
        for (var x = Math.Min(x0, x1); x <= Math.Max(x0, x1); x++)
        {
            for (var y = Math.Min(y0, y1); y <= Math.Max(y0, y1); y++)
            {
                if (!naSala[x, y])
                {
                    corredor[x, y] = true;
                }
            }
        }
    }

    /// <summary>
    /// Uma porta em cada célula de corredor encostada a uma sala que seja uma abertura de uma célula
    /// na parede (com parede dos dois lados), por sorteio.
    /// </summary>
    private static IEnumerable<Estrutura> Portas(bool[,] corredor, bool[,] naSala, bool[,] piso, int largura, int altura, Random sorteio, Texturas texturas)
    {
        for (var y = 1; y < altura - 1; y++)
        {
            for (var x = 1; x < largura - 1; x++)
            {
                if (!corredor[x, y])
                {
                    continue;
                }

                var entradaLateral = (naSala[x - 1, y] || naSala[x + 1, y]) && !piso[x, y - 1] && !piso[x, y + 1];
                var entradaFrontal = (naSala[x, y - 1] || naSala[x, y + 1]) && !piso[x - 1, y] && !piso[x + 1, y];
                if ((entradaLateral || entradaFrontal) && sorteio.NextDouble() < ChanceDePorta)
                {
                    var porta = Nova(TipoEstrutura.Porta, (x, y, 1, 1), 1.6, CorDaPorta, bloqueia: true, texturas.Porta);
                    porta.RotacaoGraus = entradaLateral ? 90 : 0;
                    yield return porta;
                }
            }
        }
    }

    /// <summary>
    /// Onde os jogadores nascem: um quadrado no centro da sala. Como a sala tem ao menos
    /// <see cref="LadoMinimoDaSala"/> de lado, ele fica longe da parede e dos pilares dos cantos.
    /// </summary>
    private static (int X, int Y, int Largura, int Altura) AreaDaEntrada(Sala sala) =>
        (sala.CentroX - LadoDaEntrada / 2, sala.CentroY - LadoDaEntrada / 2, LadoDaEntrada, LadoDaEntrada);

    /// <summary>
    /// Pilares nos cantos das salas grandes e obstáculos soltos, sempre no miolo e sem vizinhos entre si.
    /// Um obstáculo pode virar poça de perigo (chão que bloqueia), se o tema tiver textura de perigo.
    /// Nada é posto na área da entrada, para os jogadores nascerem juntos.
    /// </summary>
    private static IEnumerable<Estrutura> Mobilia(List<Sala> salas, (int X, int Y, int Largura, int Altura)? entrada, Random sorteio, Texturas texturas)
    {
        foreach (var sala in salas)
        {
            var ocupadas = new HashSet<(int X, int Y)>();
            if (entrada is { } area && sala.Contem(area.X, area.Y))
            {
                for (var y = area.Y; y < area.Y + area.Altura; y++)
                {
                    for (var x = area.X; x < area.X + area.Largura; x++)
                    {
                        ocupadas.Add((x, y));
                    }
                }
            }

            if (sala.Largura >= 7 && sala.Altura >= 7)
            {
                var direita = sala.X + sala.Largura - 2;
                var baixo = sala.Y + sala.Altura - 2;
                foreach (var (x, y) in new[] { (sala.X + 1, sala.Y + 1), (direita, sala.Y + 1), (sala.X + 1, baixo), (direita, baixo) })
                {
                    ocupadas.Add((x, y));
                    yield return Nova(TipoEstrutura.Pilar, (x, y, 1, 1), 2.5, CorDoPilar, bloqueia: true, texturas.Pilar);
                }
            }

            var quantidade = sorteio.Next(0, sala.Largura * sala.Altura / 14 + 1);
            for (var tentativa = 0; tentativa < quantidade * 6 && quantidade > 0; tentativa++)
            {
                var x = sorteio.Next(sala.X + 1, sala.X + sala.Largura - 1);
                var y = sorteio.Next(sala.Y + 1, sala.Y + sala.Altura - 1);
                if (ocupadas.Any(o => Math.Abs(o.X - x) <= 1 && Math.Abs(o.Y - y) <= 1))
                {
                    continue;
                }

                ocupadas.Add((x, y));
                var alturaDoObstaculo = Math.Round(0.4 + sorteio.NextDouble() * 0.6, 2);
                var cor = CoresDeObstaculo[sorteio.Next(CoresDeObstaculo.Length)];
                if (texturas.Perigos.Length > 0 && texturas.Chance(ChanceDePerigo))
                {
                    yield return Nova(TipoEstrutura.Chao, (x, y, 1, 1), 0.1, cor, bloqueia: true, texturas.Sortear(texturas.Perigos));
                }
                else
                {
                    yield return Nova(TipoEstrutura.Obstaculo, (x, y, 1, 1), alturaDoObstaculo, cor, bloqueia: true, texturas.Sortear(texturas.Obstaculos));
                }

                quantidade--;
            }
        }
    }

    private static bool VizinhoDePiso(bool[,] piso, int x, int y)
    {
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                var (vx, vy) = (x + dx, y + dy);
                if (vx >= 0 && vy >= 0 && vx < piso.GetLength(0) && vy < piso.GetLength(1) && piso[vx, vy])
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void Marcar(bool[,] grade, int x, int y, int largura, int altura)
    {
        for (var cy = y; cy < y + altura; cy++)
        {
            for (var cx = x; cx < x + largura; cx++)
            {
                grade[cx, cy] = true;
            }
        }
    }

    /// <summary>Cobre as células marcadas com poucos retângulos: estende cada um para a direita e depois para baixo.</summary>
    private static List<(int X, int Y, int Largura, int Altura)> Retangulos(bool[,] marcadas)
    {
        var (largura, altura) = (marcadas.GetLength(0), marcadas.GetLength(1));
        var usadas = new bool[largura, altura];
        var retangulos = new List<(int, int, int, int)>();

        for (var y = 0; y < altura; y++)
        {
            for (var x = 0; x < largura; x++)
            {
                if (!marcadas[x, y] || usadas[x, y])
                {
                    continue;
                }

                var w = 1;
                while (x + w < largura && marcadas[x + w, y] && !usadas[x + w, y])
                {
                    w++;
                }

                var h = 1;
                while (y + h < altura && Enumerable.Range(x, w).All(cx => marcadas[cx, y + h] && !usadas[cx, y + h]))
                {
                    h++;
                }

                Marcar(usadas, x, y, w, h);
                retangulos.Add((x, y, w, h));
            }
        }

        return retangulos;
    }

    /// <summary>A cor fica mesmo com textura: é o que aparece se a textura for removida.</summary>
    private static Estrutura Nova(TipoEstrutura tipo, (int X, int Y, int Largura, int Altura) area, double altura, string cor, bool bloqueia, string? textura = null) => new()
    {
        Tipo = tipo,
        X = area.X,
        Y = area.Y,
        Largura = area.Largura,
        Profundidade = area.Altura,
        Altura = altura,
        Cor = cor,
        TexturaPadrao = textura,
        BloqueiaMovimento = bloqueia
    };
}
