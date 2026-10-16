namespace Vtt.Domain.Personagens;

public class Personagem
{
    public const int NomeTamanhoMaximo = 60;
    public const int NivelMinimo = 1;
    public const int NivelMaximo = 20;
    public const int HpMaximoMinimo = 1;
    public const int HpMaximoLimite = 999;
    public const string CorBordaPadrao = "#E2B23C";

    /// <summary>Em células: 6 células = 30 ft, o deslocamento comum de D&amp;D.</summary>
    public const int DeslocamentoPadrao = 6;

    public const int DeslocamentoMinimo = 1;
    public const int DeslocamentoLimite = 30;

    /// <summary>Em células, contadas como o movimento (diagonal = 1): 12 células = 60 ft.</summary>
    public const int VisaoPadrao = 12;

    /// <summary>0 = não enxerga sem luz: com a névoa ligada, só vê o que o grupo ou o Mestre revelar.</summary>
    public const int VisaoMinima = 0;

    public const int VisaoLimite = 60;

    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public ClasseDnd Classe { get; set; }
    public int Nivel { get; set; }
    public int HpMaximo { get; set; }
    public string CorBorda { get; set; } = CorBordaPadrao;

    /// <summary>Quantas células o personagem anda por rodada; o token dele usa este valor.</summary>
    public int Deslocamento { get; set; } = DeslocamentoPadrao;

    /// <summary>Até onde o personagem enxerga, em células, quando o mapa tem névoa; o token dele usa este valor.</summary>
    public int Visao { get; set; } = VisaoPadrao;

    /// <summary>A foto do personagem: uma imagem pessoal do dono. O token do jogador a espelha.</summary>
    public Guid? ImagemId { get; set; }

    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset AtualizadoEm { get; set; }
}
