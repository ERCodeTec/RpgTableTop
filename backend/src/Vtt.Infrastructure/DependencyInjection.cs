using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Vtt.Application.Auth;
using Vtt.Application.Common;
using Vtt.Application.Imagens;
using Vtt.Infrastructure.Auth;
using Vtt.Infrastructure.Identity;
using Vtt.Infrastructure.Imagens;
using Vtt.Infrastructure.Persistence;

namespace Vtt.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // A connection string é lida só quando o contexto é resolvido, para que a configuração
        // final (variáveis de ambiente, overrides de teste) já esteja montada.
        services.AddDbContext<AppDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString("Default")
                ?? throw new InvalidOperationException("Defina ConnectionStrings:Default.");
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
        });
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        services
            .AddIdentityCore<Usuario>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = RegistrarRequestValidator.SenhaTamanhoMinimo;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 10;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddErrorDescriber<IdentityErrorDescriberPtBr>()
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.Secao)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Value.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Value.Issuer,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = JwtTokenService.CriarChave(jwt.Value.Secret),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtRegisteredClaimNames.Name
                };
            });
        services.AddAuthorization();

        services.AddSingleton<JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUsuarioDirectory, UsuarioDirectory>();

        services.AddOptions<StorageOptions>().BindConfiguration(StorageOptions.Secao);
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IImagemProcessor, ImageSharpProcessor>();

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("postgres");

        return services;
    }
}
