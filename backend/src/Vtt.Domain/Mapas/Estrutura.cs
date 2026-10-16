namespace Vtt.Domain.Mapas;

/// <summary>Um elemento fixo do cenário. Ocupa um retângulo de células inteiras, alinhado aos eixos do mapa.</summary>
public class Estrutura
{
    public const double AlturaMinima = 0.1;
    public const double AlturaMaxima = 3;
    public const string CorPadrao = "#6B6F76";

    public Guid Id { get; set; }
    public Guid MapaId { get; set; }
    public TipoEstrutura Tipo { get; set; }

    /// <summary>Célula do canto superior esquerdo.</summary>
    public int X { get; set; }

    public int Y { get; set; }

    /// <summary>Células no eixo X.</summary>
    public int Largura { get; set; } = 1;

    /// <summary>Células no eixo Y.</summary>
    public int Profundidade { get; set; } = 1;

    /// <summary>Em células.</summary>
    public double Altura { get; set; } = 2;

    /// <summary>0, 90, 180 ou 270. Orienta só o desenho; a área ocupada é sempre o retângulo acima.</summary>
    public int RotacaoGraus { get; set; }

    public string Cor { get; set; } = CorPadrao;
    public Guid? TexturaImagemId { get; set; }

    /// <summary>Uma das <see cref="TexturasPadrao"/>; nunca junto com <see cref="TexturaImagemId"/>.</summary>
    public string? TexturaPadrao { get; set; }
    public bool BloqueiaMovimento { get; set; } = true;

    /// <summary>Só tem efeito quando <see cref="Tipo"/> é Porta.</summary>
    public bool PortaAberta { get; set; }

    public int Versao { get; set; } = 1;

    /// <summary>Uma porta bloqueia enquanto está fechada; os demais tipos seguem <see cref="BloqueiaMovimento"/>.</summary>
    public bool Bloqueia => Tipo == TipoEstrutura.Porta ? !PortaAberta : BloqueiaMovimento;

    public bool Contem(int x, int y) => x >= X && y >= Y && x < X + Largura && y < Y + Profundidade;

    public static bool RotacaoValida(int graus) => graus is 0 or 90 or 180 or 270;
}
