using Vtt.Domain.Mapas;

namespace Vtt.Application.Mapas;

/// <summary>
/// Busca de espaços livres em um mapa de <c>largura</c> x <c>altura</c>. Um espaço é o quadrado de
/// <c>lado</c> células a partir do canto superior esquerdo (x, y): para o token Médio, uma célula.
/// </summary>
internal static class Grade
{
    /// <summary>true se o quadrado de <paramref name="lado"/> a partir de (x, y) cabe no mapa sem tocar nenhuma célula indisponível.</summary>
    public static bool Cabe(int largura, int altura, IReadOnlySet<(int X, int Y)> ocupadas, int x, int y, int lado = 1)
    {
        if (x < 0 || y < 0 || x + lado > largura || y + lado > altura)
        {
            return false;
        }

        for (var cy = y; cy < y + lado; cy++)
        {
            for (var cx = x; cx < x + lado; cx++)
            {
                if (ocupadas.Contains((cx, cy)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Marca como ocupadas as células do quadrado de <paramref name="lado"/> a partir de (x, y).</summary>
    public static void Ocupar(HashSet<(int X, int Y)> ocupadas, int x, int y, int lado = 1)
    {
        for (var cy = y; cy < y + lado; cy++)
        {
            for (var cx = x; cx < x + lado; cx++)
            {
                ocupadas.Add((cx, cy));
            }
        }
    }

    /// <summary>
    /// Onde nasce o token de um jogador: o primeiro espaço livre dentro das entradas, na ordem
    /// recebida; com elas cheias (ou pequenas demais), o livre mais próximo da primeira entrada. Sem entradas, <see cref="PrimeiraLivre"/>.
    /// </summary>
    public static (int X, int Y)? PontoDeSpawn(
        int largura, int altura, IReadOnlyList<Estrutura> entradas, IReadOnlySet<(int X, int Y)> ocupadas, int lado = 1)
    {
        if (entradas.Count == 0)
        {
            return PrimeiraLivre(largura, altura, ocupadas, lado);
        }

        foreach (var entrada in entradas)
        {
            for (var y = Math.Max(0, entrada.Y); y + lado <= Math.Min(altura, entrada.Y + entrada.Profundidade); y++)
            {
                for (var x = Math.Max(0, entrada.X); x + lado <= Math.Min(largura, entrada.X + entrada.Largura); x++)
                {
                    if (Cabe(largura, altura, ocupadas, x, y, lado))
                    {
                        return (x, y);
                    }
                }
            }
        }

        var primeira = entradas[0];
        var centroX = Math.Clamp(primeira.X + primeira.Largura / 2, 0, largura - 1);
        var centroY = Math.Clamp(primeira.Y + primeira.Profundidade / 2, 0, altura - 1);
        return LivreMaisProxima(largura, altura, ocupadas, centroX, centroY, lado);
    }

    /// <summary>O primeiro espaço livre varrendo linha a linha a partir de (0,0).</summary>
    public static (int X, int Y)? PrimeiraLivre(int largura, int altura, IReadOnlySet<(int X, int Y)> ocupadas, int lado = 1)
    {
        for (var y = 0; y + lado <= altura; y++)
        {
            for (var x = 0; x + lado <= largura; x++)
            {
                if (Cabe(largura, altura, ocupadas, x, y, lado))
                {
                    return (x, y);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// O espaço livre mais próximo de (x, y), procurando em anéis de distância crescente
    /// (diagonal conta como 1, como no grid de D&amp;D). Empates: de cima para baixo, da esquerda para a direita.
    /// </summary>
    /// <param name="incluirOrigem">true para aceitar o próprio (x, y), quando o token só precisa caber onde já está.</param>
    public static (int X, int Y)? LivreMaisProxima(
        int largura, int altura, IReadOnlySet<(int X, int Y)> ocupadas, int x, int y, int lado = 1, bool incluirOrigem = false)
    {
        if (incluirOrigem && Cabe(largura, altura, ocupadas, x, y, lado))
        {
            return (x, y);
        }

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
                    if (Cabe(largura, altura, ocupadas, cx, cy, lado))
                    {
                        return (cx, cy);
                    }
                }
            }
        }

        return null;
    }
}
