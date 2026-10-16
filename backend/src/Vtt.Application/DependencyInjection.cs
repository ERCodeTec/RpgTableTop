using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Vtt.Application.Biblioteca;
using Vtt.Application.Iniciativas;
using Vtt.Application.Imagens;
using Vtt.Application.Mapas;
using Vtt.Application.Mesas;
using Vtt.Application.Personagens;

namespace Vtt.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<IPersonagemService, PersonagemService>();
        services.AddScoped<IMesaService, MesaService>();
        services.AddScoped<IConviteService, ConviteService>();
        services.AddScoped<IMapaService, MapaService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<TokensDeJogador>();
        services.AddScoped<MovimentoDaRodada>();
        services.AddScoped<TurnoDaMesa>();
        services.AddScoped<IIniciativaService, IniciativaService>();
        services.AddScoped<IEstruturaService, EstruturaService>();
        services.AddScoped<INevoaService, NevoaService>();
        services.AddScoped<IImagemService, ImagemService>();
        services.AddScoped<IBibliotecaService, BibliotecaService>();
        return services;
    }
}
