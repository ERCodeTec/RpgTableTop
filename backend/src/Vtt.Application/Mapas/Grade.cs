using Vtt.Domain.Mapas;

namespace Vtt.Application.Mapas;

/// <summary>Busca de células livres em um mapa de <c>largura</c> x <c>altura</c>.</summary>
internal static class Grade
{
    /// <summary>
    /// Onde nasce o token de um jogador: a primeira célula livre dentro das entradas, na ordem
    /// recebida; com elas cheias, a livre mais próxima da primeira entrada. Sem entradas, <see cref="PrimeiraLivre"/>.
    /// </summary>
    public static (int X, int Y)? PontoDeSpawn(
        int largura, int altura, IReadOnlyList<Estrutura> entradas, IReadOnlySet<(int X, int Y)> ocupadas)
    {
        if (entradas.Count == 0)
        {
            return PrimeiraLivre(largura, altura, ocupadas);
        }

        foreach (var entrada in entradas)
        {
            for (var y = Math.Max(0, entrada.Y); y < Math.Min(altura, entrada.Y + entrada.Profundidade); y++)
            {
                for (var x = Math.Max(0, entrada.X); x < Math.Min(largura, entrada.X + entrada.Largura); x++)
                {
                    if (!ocupadas.Contains((x, y)))
                    {
                        return (x, y);
                    }
                }
            }
        }

        var primeira = entradas[0];
        var centroX = Math.Clamp(primeira.X + primeira.Largura / 2, 0, largura - 1);
        var centroY = Math.Clamp(primeira.Y + primeira.Profundidade / 2, 0, altura - 1);
        return LivreMaisProxima(largura, altura, ocupadas, centroX, centroY);
    }

    /// <summary>A primeira célula livre varrendo linha a linha a partir de (0,0).</summary>
    public static (int X, int Y)? PrimeiraLivre(int largura, int altura, IReadOnlySet<(int X, int Y)> ocupadas)
    {
        for (var y = 0; y < altura; y++)
        {
            for (var x = 0; x < largura; x++)
            {
                if (!ocupadas.Contains((x, y)))
                {
                    return (x, y);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// A célula livre mais próxima de (x, y), procurando em anéis de distância crescente
    /// (diagonal conta como 1, como no grid de D&amp;D). Empates: de cima para baixo, da esquerda para a direita.
    /// </summary>
    public static (int X, int Y)? LivreMaisProxima(int largura, int altura, IReadOnlySet<(int X, int Y)> ocupadas, int x, int y)
    {
        var maiorDistancia = Math.Max(largura, altura);
        for (var distancia = 1; distancia <= maiorDistancia; distancia++)
        {
            for (var dy = -distancia; dy <= distancia; dy++)
            {
                for (var dx = -distancia; dx <= distancia; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != distancia)
                    {
                        continue;
                    }

                    var (cx, cy) = (x + dx, y + dy);
                    if (cx >= 0 && cy >= 0 && cx < largura && cy < altura && !ocupadas.Contains((cx, cy)))
                    {
                        return (cx, cy);
                    }
                }
            }
        }

        return null;
    }
}
