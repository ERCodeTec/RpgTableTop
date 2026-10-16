namespace Vtt.Domain.Mesas;

public class Convite
{
    public const int CodigoTamanho = 8;

    /// <summary>Sem 0/O, 1/I/L: evita confusão ao ditar ou digitar o código.</summary>
    public const string Alfabeto = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public Guid Id { get; set; }
    public Guid MesaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset? ExpiraEm { get; set; }
    public DateTimeOffset? RevogadoEm { get; set; }
    public int? UsosMaximos { get; set; }
    public int UsosAtuais { get; set; }

    public bool EstaValido(DateTimeOffset agora) =>
        RevogadoEm is null
        && (ExpiraEm is null || ExpiraEm > agora)
        && (UsosMaximos is null || UsosAtuais < UsosMaximos);
}
