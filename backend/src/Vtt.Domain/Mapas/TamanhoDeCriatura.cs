namespace Vtt.Domain.Mapas;

/// <summary>
/// A categoria de tamanho de D&amp;D. Define quantas células o token ocupa no grid (cada célula tem
/// 1,5 m = 5 ft): um quadrado de <see cref="Tamanhos.Celulas"/> células de lado, a partir de X,Y.
/// </summary>
public enum TamanhoDeCriatura
{
    /// <summary>0,75 m: ocupa a célula inteira sozinho, mas é desenhado menor.</summary>
    Minusculo,

    /// <summary>1,5 m: 1 célula.</summary>
    Pequeno,

    /// <summary>1,5 m: 1 célula. O tamanho de todo token que não informar outro.</summary>
    Medio,

    /// <summary>3 m: 2x2 células.</summary>
    Grande,

    /// <summary>4,5 m: 3x3 células.</summary>
    Enorme,

    /// <summary>6 m ou mais: 4x4 células, ou o lado escolhido (até <see cref="Tamanhos.LadoGargantuescoLimite"/>).</summary>
    Gargantuesco
}

public static class Tamanhos
{
    public const TamanhoDeCriatura Padrao = TamanhoDeCriatura.Medio;
    public const int LadoGargantuescoMinimo = 4;
    public const int LadoGargantuescoLimite = 10;

    /// <summary>O lado, em células, do quadrado que o tamanho ocupa.</summary>
    /// <param name="ladoGargantuesco">Só vale para o Gargantuesco; sem ele, 4.</param>
    public static int Celulas(TamanhoDeCriatura tamanho, int? ladoGargantuesco) => tamanho switch
    {
        TamanhoDeCriatura.Grande => 2,
        TamanhoDeCriatura.Enorme => 3,
        TamanhoDeCriatura.Gargantuesco => ladoGargantuesco ?? LadoGargantuescoMinimo,
        _ => 1
    };

    /// <summary>O lado do Gargantuesco só é guardado nele; nos demais tamanhos fica nulo.</summary>
    public static int? NormalizarLado(TamanhoDeCriatura tamanho, int? ladoGargantuesco) =>
        tamanho == TamanhoDeCriatura.Gargantuesco ? ladoGargantuesco ?? LadoGargantuescoMinimo : null;

    /// <summary>true se os quadrados (x1,y1,lado1) e (x2,y2,lado2) têm alguma célula em comum.</summary>
    public static bool Sobrepoem(int x1, int y1, int lado1, int x2, int y2, int lado2) =>
        x1 < x2 + lado2 && x2 < x1 + lado1 && y1 < y2 + lado2 && y2 < y1 + lado1;

    /// <summary>
    /// A distância entre dois quadrados contada como o movimento (diagonal = 1): 0 se se sobrepõem,
    /// 1 se encostam (inclusive pela quina).
    /// </summary>
    public static int Distancia(int x1, int y1, int lado1, int x2, int y2, int lado2)
    {
        var dx = Math.Max(0, Math.Max(x2 - (x1 + lado1 - 1), x1 - (x2 + lado2 - 1)));
        var dy = Math.Max(0, Math.Max(y2 - (y1 + lado1 - 1), y1 - (y2 + lado2 - 1)));
        return Math.Max(dx, dy);
    }
}
