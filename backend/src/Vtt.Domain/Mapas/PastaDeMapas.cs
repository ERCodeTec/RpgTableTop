namespace Vtt.Domain.Mapas;

/// <summary>Uma pasta da aba Mapas de uma mesa. Só organiza a lista do Mestre: não muda nada no mapa.</summary>
public class PastaDeMapas
{
    public const int NomeTamanhoMaximo = 40;
    public const int LimitePorMesa = 100;

    /// <summary>O latão do tema.</summary>
    public const string CorPadrao = "#E2B23C";

    public Guid Id { get; set; }
    public Guid MesaId { get; set; }
    public string Nome { get; set; } = string.Empty;

    /// <summary>#RRGGBB, escolhida pelo Mestre; só muda a borda da pasta na lista.</summary>
    public string Cor { get; set; } = CorPadrao;
    public DateTimeOffset CriadaEm { get; set; }
}
