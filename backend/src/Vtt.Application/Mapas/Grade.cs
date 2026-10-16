namespace Vtt.Application.Mapas;

/// <summary>Busca de células livres em um mapa de <c>largura</c> x <c>altura</c>.</summary>
internal static class Grade
{
    /// <summary>Ponto de spawn: a primeira célula livre varrendo linha a linha a partir de (0,0).</summary>
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
