namespace Vtt.Infrastructure.Imagens;

public class StorageOptions
{
    public const string Secao = "Storage";

    /// <summary>Pasta dos uploads. No Docker é o volume montado em /app/uploads.</summary>
    public string UploadsPath { get; set; } = "uploads";
}
