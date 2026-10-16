using System.Globalization;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Vtt.Api.Hubs;
using Vtt.Api.Infrastructure;
using Vtt.Application;
using Vtt.Application.Common;
using Vtt.Application.Imagens;
using Vtt.Application.Mapas;
using Vtt.Application.Mesas;
using Vtt.Infrastructure;
using Vtt.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure();

builder.Services.AddOptions<AppOptions>().BindConfiguration(AppOptions.Secao);
builder.Services.AddOptions<RateLimitingOptions>().BindConfiguration(RateLimitingOptions.Secao);
builder.Services.AddOptions<UploadOptions>().BindConfiguration(UploadOptions.Secao);

builder.Services
    .AddControllers(options => options.Filters.Add<ValidationFilter>())
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));

builder.Services.AddProblemDetails();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ILinkPublico, LinkPublico>();

builder.Services
    .AddSignalR()
    .AddJsonProtocol(options =>
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddOptions<HubOptions>()
    .Configure<IOptions<RateLimitingOptions>>((hub, limites) =>
    {
        hub.MaximumReceiveMessageSize = limites.Value.TamanhoMaximoDaMensagemDoHub;

        // Sinal de vida a cada 5 s (o cliente usa o mesmo intervalo): uma conexão que morreu sem
        // aviso, como a de um celular que dormiu, é percebida em segundos e a presença se corrige.
        hub.KeepAliveInterval = TimeSpan.FromSeconds(5);
        hub.ClientTimeoutInterval = TimeSpan.FromSeconds(20);
    });
builder.Services.AddSingleton<IUserIdProvider, SubUserIdProvider>();
builder.Services.AddSingleton<PresencaTracker>();
builder.Services.AddScoped<IMesaNotifier, MesaNotifier>();
builder.Services.AddScoped<IMapaNotifier, MapaNotifier>();
builder.Services.AddSingleton<LimitadorDoHub>();

// O navegador não envia cabeçalhos em WebSockets, então o cliente do SignalR manda o JWT em
// ?access_token=. Só aceitamos o token pela URL nas rotas dos hubs.
builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var token = context.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
            {
                context.Token = token;
            }

            return Task.CompletedTask;
        }
    });

// Atrás do ngrok (e do proxy do Docker) o esquema, o host e o IP reais chegam nos cabeçalhos
// X-Forwarded-*. O proxy não tem IP fixo, então aceitamos de qualquer origem; por isso a API
// não deve ser exposta diretamente à internet sem um proxy na frente.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddRateLimiter(_ => { });
builder.Services.AddOptions<RateLimiterOptions>()
    .Configure<IOptions<RateLimitingOptions>>((limiter, configuracao) =>
    {
        var limite = configuracao.Value;

        limiter.AddPolicy(RateLimitingOptions.PoliticaAuth, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limite.AuthLimite,
                    Window = TimeSpan.FromSeconds(limite.AuthJanelaSegundos),
                    QueueLimit = 0
                }));

        // Por usuário: o limitador roda depois da autenticação, então o "sub" do token já está disponível.
        limiter.AddPolicy(RateLimitingOptions.PoliticaUpload, httpContext => limite.UploadsPorMinuto is not { } uploadsPorMinuto
            ? RateLimitPartition.GetNoLimiter("sem-limite")
            : RateLimitPartition.GetFixedWindowLimiter(
                httpContext.User.FindFirst("sub")?.Value ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = uploadsPorMinuto,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

        limiter.OnRejected = async (context, cancellationToken) =>
        {
            var resposta = context.HttpContext.Response;
            resposta.StatusCode = StatusCodes.Status429TooManyRequests;
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
            {
                resposta.Headers.RetryAfter = ((int)Math.Ceiling(espera.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            }

            await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>()
                .WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails =
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Detail = "Muitas tentativas. Aguarde um minuto e tente de novo."
                    }
                });
        };
    });

var swaggerHabilitado = builder.Environment.IsDevelopment();
if (swaggerHabilitado)
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo { Title = "VTT API", Version = "v1" });
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Cole o token retornado por /api/auth/login."
        });
        options.AddSecurityRequirement(documento => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", documento)] = []
        });
    });
}

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (swaggerHabilitado)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// O build do frontend (Vite) é copiado para wwwroot na imagem Docker.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        // Os arquivos em /assets têm hash no nome, então podem ficar em cache para sempre.
        if (context.Context.Request.Path.StartsWithSegments("/assets"))
        {
            context.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        }
    }
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHub<MesaHub>(MesaHub.Caminho);
app.MapHealthChecks("/health");

// Rotas desconhecidas da API e dos hubs respondem 404; qualquer outra cai no index.html (rotas do SPA).
app.MapFallback("/api/{**resto}", () => Results.Problem(statusCode: StatusCodes.Status404NotFound));
app.MapFallback("/hubs/{**resto}", () => Results.Problem(statusCode: StatusCodes.Status404NotFound));
app.MapFallbackToFile("index.html", new StaticFileOptions
{
    OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache"
});

if (app.Configuration.GetValue("Database:AutoMigrate", app.Environment.IsDevelopment()))
{
    await using var escopo = app.Services.CreateAsyncScope();
    await escopo.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    app.Logger.LogInformation("Migrations aplicadas");
}

var urlPublica = app.Services.GetRequiredService<IOptions<AppOptions>>().Value.PublicBaseUrl;
app.Logger.LogInformation("URL pública configurada: {UrlPublica}", string.IsNullOrEmpty(urlPublica) ? "(não definida)" : urlPublica);

await app.RunAsync();

public partial class Program;
