namespace Vtt.Domain.Mapas;

/// <summary>Chaves dos ícones de inimigos, NPCs e objetos de interação. Os desenhos ficam no frontend.</summary>
public static class IconesGenericos
{
    public const string Npc = "npc";
    public const string Interacao = "interacao";

    public static readonly IReadOnlySet<string> Chaves = new HashSet<string>(StringComparer.Ordinal)
    {
        "goblin",
        "esqueleto",
        "dragao",
        "lobo",
        "humanoide",
        "besta",
        "morto-vivo",
        Npc,
        Interacao,
        "bau",
        "pergaminho",
        "estatua",
        "alavanca",
        "livro"
    };
}
