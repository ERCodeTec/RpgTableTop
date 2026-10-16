using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vtt.Domain.Mapas;

/// <summary>Um ambiente que agrupa texturas prontas (masmorra, caverna, natureza...).</summary>
public sealed record TemaDeTextura(string Id, string Nome);

/// <summary>
/// Uma textura pronta de estrutura. As categorias: <see cref="Tema"/> (o ambiente),
/// <see cref="Estruturas"/> (os tipos em que ela faz sentido; o seletor do Mestre mostra só as do
/// tipo da estrutura) e <see cref="Papeis"/> (onde o gerador de mapas pode usá-la).
/// </summary>
/// <param name="Largura">Em pixels. Um ladrilho mais alto que largo cobre mais de uma célula na vertical.</param>
public sealed record TexturaPronta(
    string Chave,
    string Nome,
    string Tema,
    IReadOnlyList<TipoEstrutura> Estruturas,
    IReadOnlyList<string> Papeis,
    int Largura,
    int Altura,
    string Pacote);

/// <summary>
/// O catálogo das texturas prontas, disponíveis em toda mesa sem ocupar a galeria. Vem de
/// texturas-prontas.json (embutido), gerado por scripts/texturas/montar_texturas.py junto com os
/// PNGs em frontend/public/texturas.
/// </summary>
public static class TexturasPadrao
{
    public const int ChaveTamanhoMaximo = 40;

    // As que o código usa pelo nome: padrões das estruturas novas e testes.
    public const string ParedeDePedra = "parede-pedra";
    public const string PisoDeLajotas = "piso-lajotas";
    public const string PisoDeAreia = "piso-areia";
    public const string PisoDeTerra = "piso-terra";
    public const string PortaDeMadeira = "porta-madeira";

    /// <summary>Papéis do gerador de mapas.</summary>
    public static class Papel
    {
        public const string Piso = "piso";
        public const string Corredor = "corredor";
        public const string Parede = "parede";
        public const string Rocha = "rocha";
        public const string Porta = "porta";
        public const string Pilar = "pilar";
        public const string Obstaculo = "obstaculo";
        public const string Perigo = "perigo";
    }

    private sealed record Arquivo(IReadOnlyList<TemaDeTextura> Temas, IReadOnlyList<TexturaPronta> Texturas);

    private static readonly Arquivo Catalogo = Carregar();

    public static IReadOnlyList<TemaDeTextura> Temas => Catalogo.Temas;

    public static IReadOnlyList<TexturaPronta> Todas => Catalogo.Texturas;

    public static readonly IReadOnlySet<string> Chaves = Catalogo.Texturas.Select(t => t.Chave).ToHashSet(StringComparer.Ordinal);

    /// <summary>As texturas do tema que o gerador pode usar no papel, na ordem do catálogo.</summary>
    public static IReadOnlyList<TexturaPronta> DoPapel(string tema, string papel) =>
        Catalogo.Texturas.Where(t => t.Tema == tema && t.Papeis.Contains(papel)).ToList();

    private static Arquivo Carregar()
    {
        using var conteudo = typeof(TexturasPadrao).Assembly.GetManifestResourceStream("texturas-prontas.json")
            ?? throw new InvalidOperationException("texturas-prontas.json não está embutido no Vtt.Domain.");
        var opcoes = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
        return JsonSerializer.Deserialize<Arquivo>(conteudo, opcoes)
            ?? throw new InvalidOperationException("texturas-prontas.json está vazio.");
    }
}
