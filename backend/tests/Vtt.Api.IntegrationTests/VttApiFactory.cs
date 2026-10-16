using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Vtt.Api.IntegrationTests;

/// <summary>
/// Sobe a API em memória contra um PostgreSQL real (Testcontainers), com as migrations aplicadas.
/// Requer o Docker em execução.
/// </summary>
public class VttApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    public const string UrlPublica = "https://mesa.teste.local";

    /// <summary>Limite de login/cadastro por janela; alto por padrão para não interferir nos outros testes.</summary>
    protected virtual int AuthLimite => 10_000;

    /// <summary>Limite de MoverToken por conexão; alto por padrão pelo mesmo motivo.</summary>
    protected virtual int MovimentosPorSegundo => 10_000;

    /// <summary>Limite dos demais métodos do Hub por conexão; alto por padrão pelo mesmo motivo.</summary>
    protected virtual int ChamadasDoHubPorSegundo => 10_000;

    /// <summary>Limites de upload; null mantém o de produção (sem limite, nos opcionais).</summary>
    protected virtual int? UploadsPorMinuto => null;

    protected virtual int? CotaImagensPorMesa => null;

    protected virtual long? CotaBytesPorMesa => null;

    protected virtual long? TamanhoMaximoBytes => null;

    protected virtual int? DimensaoMaxima => null;

    protected virtual long? PixelsMaximosDaAnimacao => null;

    /// <summary>Pasta temporária que faz o papel do volume de uploads; apagada ao fim dos testes.</summary>
    public string PastaDeUploads { get; } = Path.Combine(Path.GetTempPath(), $"vtt-uploads-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
        builder.UseSetting("Database:AutoMigrate", "true");
        builder.UseSetting("Jwt:Secret", "segredo-apenas-para-testes-com-mais-de-32-caracteres");
        builder.UseSetting("Jwt:Issuer", "vtt-testes");
        builder.UseSetting("RateLimiting:AuthLimite", AuthLimite.ToString());
        builder.UseSetting("RateLimiting:MovimentosPorSegundo", MovimentosPorSegundo.ToString());
        builder.UseSetting("RateLimiting:ChamadasDoHubPorSegundo", ChamadasDoHubPorSegundo.ToString());
        UsarSeDefinido(builder, "RateLimiting:UploadsPorMinuto", UploadsPorMinuto);
        UsarSeDefinido(builder, "Uploads:CotaImagensPorMesa", CotaImagensPorMesa);
        UsarSeDefinido(builder, "Uploads:CotaBytesPorMesa", CotaBytesPorMesa);
        UsarSeDefinido(builder, "Uploads:TamanhoMaximoBytes", TamanhoMaximoBytes);
        UsarSeDefinido(builder, "Uploads:DimensaoMaxima", DimensaoMaxima);
        UsarSeDefinido(builder, "Uploads:PixelsMaximosDaAnimacao", PixelsMaximosDaAnimacao);
        builder.UseSetting("Storage:UploadsPath", PastaDeUploads);
        builder.UseSetting("App:PublicBaseUrl", UrlPublica + "/");
    }

    private static void UsarSeDefinido<T>(IWebHostBuilder builder, string chave, T? valor) where T : struct
    {
        if (valor is { } definido)
        {
            builder.UseSetting(chave, definido.ToString());
        }
    }

    public Task InitializeAsync() => _postgres.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        if (Directory.Exists(PastaDeUploads))
        {
            Directory.Delete(PastaDeUploads, recursive: true);
        }
    }
}

[CollectionDefinition(Nome)]
public class ApiCollection : ICollectionFixture<VttApiFactory>
{
    public const string Nome = "api";
}
