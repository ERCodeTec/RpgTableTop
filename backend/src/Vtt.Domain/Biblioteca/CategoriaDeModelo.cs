namespace Vtt.Domain.Biblioteca;

/// <summary>Uma pasta da biblioteca de tokens. Só organiza: não muda nada no mapa.</summary>
public class CategoriaDeModelo
{
    public const int NomeTamanhoMaximo = 40;
    public const int LimitePorUsuario = 50;

    /// <summary>Oferecidas a quem começa a biblioteca vazia.</summary>
    public static readonly IReadOnlyList<string> Sugeridas = ["NPCs", "Vilões", "Inimigos"];

    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public DateTimeOffset CriadaEm { get; set; }
}
