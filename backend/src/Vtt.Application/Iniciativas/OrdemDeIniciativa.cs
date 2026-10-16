using Vtt.Domain.Mapas;
using Vtt.Domain.Mesas;

namespace Vtt.Application.Iniciativas;

/// <summary>
/// A ordem da iniciativa. O cliente monta a trilha com a mesma regra (ordemDeIniciativa.ts); mudar
/// uma exige mudar a outra.
/// </summary>
public static class OrdemDeIniciativa
{
    /// <summary>
    /// Os tokens visíveis que têm iniciativa, do primeiro ao último a jogar: maior valor primeiro; no
    /// empate, jogadores antes dos grupos e depois o nome; dentro do grupo, o número do nome. Os
    /// ocultos ficam de fora, para a vez não denunciar que eles existem.
    /// </summary>
    public static IReadOnlyList<Token> Calcular(IEnumerable<ValorDeIniciativa> valores, IEnumerable<Token> tokens)
    {
        var dosJogadores = new Dictionary<Guid, int>();
        var dosGrupos = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var valor in valores)
        {
            if (valor.UsuarioId is { } usuarioId)
            {
                dosJogadores[usuarioId] = valor.Valor;
            }
            else if (valor.Grupo is { } grupo)
            {
                dosGrupos[grupo] = valor.Valor;
            }
        }

        var itens = new List<(Token Token, int Valor, int Tipo, string Nome, int Sequencia)>();
        // Objetos de interação não jogam: ficam fora da ordem mesmo com o nome de um grupo.
        foreach (var token in tokens.Where(t => t.Visivel && t.Tipo != TipoToken.Interacao))
        {
            if (token.DonoUsuarioId is { } dono)
            {
                if (dosJogadores.TryGetValue(dono, out var valor))
                {
                    itens.Add((token, valor, 0, GrupoDeIniciativa.Chave(token.Nome), 0));
                }

                continue;
            }

            var (chave, sequencia) = GrupoDeIniciativa.De(token.Nome);
            if (dosGrupos.TryGetValue(chave, out var valorDoGrupo))
            {
                itens.Add((token, valorDoGrupo, 1, chave, sequencia));
            }
        }

        return itens
            .OrderByDescending(item => item.Valor)
            .ThenBy(item => item.Tipo)
            .ThenBy(item => item.Nome, StringComparer.Ordinal)
            .ThenBy(item => item.Sequencia)
            .ThenBy(item => item.Token.Id.ToString(), StringComparer.Ordinal)
            .Select(item => item.Token)
            .ToList();
    }

    /// <summary>O próximo a jogar nesta rodada: o primeiro que ainda não jogou e não está caído.</summary>
    public static Token? Proxima(IReadOnlyList<Token> ordem, Guid? atual) =>
        ordem.FirstOrDefault(token => !token.JogouNaRodada && !token.Caido && token.Id != atual);
}
