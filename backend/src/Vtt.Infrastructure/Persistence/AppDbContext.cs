using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Vtt.Application.Common;
using Vtt.Domain.Biblioteca;
using Vtt.Domain.Imagens;
using Vtt.Domain.Mapas;
using Vtt.Domain.Mesas;
using Vtt.Domain.Personagens;
using Vtt.Infrastructure.Identity;

namespace Vtt.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityUserContext<Usuario, Guid>(options), IAppDbContext
{
    public DbSet<Personagem> Personagens => Set<Personagem>();
    public DbSet<Mesa> Mesas => Set<Mesa>();
    public DbSet<MesaMembro> MesaMembros => Set<MesaMembro>();
    public DbSet<Convite> Convites => Set<Convite>();
    public DbSet<Mapa> Mapas => Set<Mapa>();
    public DbSet<Token> Tokens => Set<Token>();
    public DbSet<Estrutura> Estruturas => Set<Estrutura>();
    public DbSet<ImagemUpload> Imagens => Set<ImagemUpload>();
    public DbSet<MemoriaDeNevoa> MemoriasDeNevoa => Set<MemoriaDeNevoa>();
    public DbSet<CategoriaDeModelo> CategoriasDeModelo => Set<CategoriaDeModelo>();
    public DbSet<ModeloDeToken> ModelosDeToken => Set<ModeloDeToken>();
    public DbSet<ValorDeIniciativa> ValoresDeIniciativa => Set<ValorDeIniciativa>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
