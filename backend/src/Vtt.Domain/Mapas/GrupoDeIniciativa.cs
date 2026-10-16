using System.Text.RegularExpressions;

namespace Vtt.Domain.Mapas;

/// <summary>
/// Tokens iguais dividem a iniciativa: "Goblin 1", "Goblin 2" e "Goblin 3" são o grupo "goblin",
/// e jogam na ordem do número. O cliente usa a mesma regra para montar a trilha.
/// </summary>
public static partial class GrupoDeIniciativa
{
    public const int TamanhoMaximo = Token.NomeTamanhoMaximo;

    /// <summary>A chave do grupo (o nome sem o número final, em minúsculas) e o número, ou 0 sem número.</summary>
    public static (string Chave, int Sequencia) De(string nome)
    {
        var limpo = nome.Trim();
        var numerado = Numerado().Match(limpo);
        if (numerado.Success && int.TryParse(numerado.Groups[2].Value, out var sequencia))
        {
            return (Chave(numerado.Groups[1].Value), sequencia);
        }

        return (Chave(limpo), 0);
    }

    public static string Chave(string nome) => nome.Trim().ToLowerInvariant();

    [GeneratedRegex(@"^(.*?\S)\s+(\d{1,6})$")]
    private static partial Regex Numerado();
}
