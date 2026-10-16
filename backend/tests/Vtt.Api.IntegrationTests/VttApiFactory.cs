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

    /// <summary>Limite de uploads por usuário por minuto; alto por padrão pelo mesmo motivo.</summary>
    protected virtual int UploadsPorMinuto => 10_000;

    /// <summary>Cotas de imagens por mesa; as de produção por padrão.</summary>
    protected virtual int CotaImagensPorMesa => 100;

    protected virtual long CotaBytesPorMesa => 200L * 1024 * 1024;

    protected virtual long PixelsMaximosDaAnimacao => 64L * 1024 * 1024;

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
        builder.UseSetting("RateLimiting:UploadsPorMinuto", UploadsPorMinuto.ToString());
        builder.UseSetting("Uploads:CotaImagensPorMesa", CotaImagensPorMesa.ToString());
        builder.UseSetting("Uploads:CotaBytesPorMesa", CotaBytesPorMesa.ToString());
        builder.UseSetting("Uploads:PixelsMaximosDaAnimacao", PixelsMaximosDaAnimacao.ToString());
        builder.UseSetting("Storage:UploadsPath", PastaDeUploads);
        builder.UseSetting("App:PublicBaseUrl", UrlPublica + "/");
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
