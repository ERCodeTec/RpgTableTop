namespace Vtt.Domain.Mapas;

public class Mapa
{
    public const int NomeTamanhoMaximo = 80;
    public const int DimensaoMinima = 5;
    public const int DimensaoMaxima = 100;
    public const string CorFundoPadrao = "#1B2A2F";

    public Guid Id { get; set; }
    public Guid MesaId { get; set; }
    public string Nome { get; set; } = string.Empty;

    /// <summary>Em células.</summary>
    public int Largura { get; set; }

    /// <summary>Em células.</summary>
    public int Altura { get; set; }

    /// <summary>No máximo um mapa ativo por mesa; é o que os jogadores veem.</summary>
    public bool Ativo { get; set; }

    public string CorFundo { get; set; } = CorFundoPadrao;
    public DateTimeOffset CriadoEm { get; set; }

    public const double FundoEscalaMinima = 0.05;
    public const double FundoEscalaMaxima = 20;

    public Guid? ImagemFundoId { get; set; }

    /// <summary>1 = a imagem tem a largura do mapa, mantendo a proporção.</summary>
    public double FundoEscala { get; set; } = 1;

    /// <summary>Deslocamento do canto superior esquerdo da imagem, em células.</summary>
    public double FundoOffsetX { get; set; }

    public double FundoOffsetY { get; set; }

    /// <summary>Se os jogadores enxergam o mapa inteiro ou só o que os tokens deles veem.</summary>
    public ModoDeNevoa Nevoa { get; set; } = ModoDeNevoa.Desligada;

    public int Celulas => Largura * Altura;

    public bool Contem(int x, int y) => x >= 0 && y >= 0 && x < Largura && y < Altura;
}
