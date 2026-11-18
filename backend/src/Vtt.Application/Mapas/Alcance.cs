namespace Vtt.Application.Mapas;

/// <summary>
/// O alcance do movimento por blocos, o mesmo cálculo do cliente (<c>celulasAlcancaveis</c> em
/// geometria.ts). Cada passo, nas 8 direções, custa 1; o token não entra em célula bloqueada nem
/// "corta o canto" de uma estrutura na diagonal (contorna, e o desvio custa). Pela célula com outro token ele passa, mas
/// não para nela: dois tokens nunca dividem a célula.
/// Um token maior que Médio é um quadrado de <c>lado</c> células, ancorado no canto superior esquerdo:
/// cada posição é válida só se o quadrado inteiro cabe no mapa sem tocar estrutura, então ele não
/// passa por um corredor mais estreito que ele.
/// </summary>
internal static class Alcance
{
    private static readonly (int Dx, int Dy)[] Direcoes =
        [(0, -1), (1, 0), (0, 1), (-1, 0), (1, -1), (1, 1), (-1, 1), (-1, -1)];

    /// <summary>
    /// As âncoras alcançáveis a partir da origem com até <paramref name="orcamento"/> passos, com o
    /// custo do caminho mínimo até cada uma. A origem entra com custo 0.
    /// </summary>
    /// <param name="ocupadas">Células com outro token: dá para atravessar, mas o espaço final não pode tocar nenhuma. Não deve conter o próprio token.</param>
    /// <param name="lado">O lado, em células, do espaço do token.</param>
    public static Dictionary<(int X, int Y), int> Calcular(
        int largura,
        int altura,
        (int X, int Y) origem,
        int orcamento,
        IReadOnlySet<(int X, int Y)> bloqueadas,
        IReadOnlySet<(int X, int Y)> ocupadas,
        int lado = 1)
    {
        var custos = new Dictionary<(int X, int Y), int> { [origem] = 0 };
        var fila = new Queue<(int X, int Y)>();
        fila.Enqueue(origem);

        bool Livre((int X, int Y) ancora) => Grade.Cabe(largura, altura, bloqueadas, ancora.X, ancora.Y, lado);

        while (fila.Count > 0)
        {
            var atual = fila.Dequeue();
            var custo = custos[atual];
            if (custo >= orcamento)
            {
                continue;
            }

            foreach (var (dx, dy) in Direcoes)
            {
                var vizinha = (X: atual.X + dx, Y: atual.Y + dy);
                if (custos.ContainsKey(vizinha) || !Livre(vizinha))
                {
                    continue;
                }

                // Na diagonal, uma estrutura em qualquer das duas ortogonais fecha o canto: o token contorna.
                if (dx != 0 && dy != 0 && (!Livre((atual.X + dx, atual.Y)) || !Livre((atual.X, atual.Y + dy))))
                {
                    continue;
                }

                custos[vizinha] = custo + 1;
                fila.Enqueue(vizinha);
            }
        }

        // Atravessar é permitido; parar com o espaço sobre outro token, não.
        foreach (var ancora in custos.Keys.ToList())
        {
            if (ancora != origem && !Grade.Cabe(largura, altura, ocupadas, ancora.X, ancora.Y, lado))
            {
                custos.Remove(ancora);
            }
        }

        return custos;
    }
}
