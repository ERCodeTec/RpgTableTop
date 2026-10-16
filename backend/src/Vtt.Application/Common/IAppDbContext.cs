using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Vtt.Domain.Biblioteca;
using Vtt.Domain.Imagens;
using Vtt.Domain.Mapas;
using Vtt.Domain.Mesas;
using Vtt.Domain.Personagens;

namespace Vtt.Application.Common;

public interface IAppDbContext
{
    DbSet<Personagem> Personagens { get; }
    DbSet<Mesa> Mesas { get; }
    DbSet<MesaMembro> MesaMembros { get; }
    DbSet<Convite> Convites { get; }
    DbSet<Mapa> Mapas { get; }
    DbSet<Token> Tokens { get; }
    DbSet<Estrutura> Estruturas { get; }
    DbSet<ImagemUpload> Imagens { get; }
    DbSet<MemoriaDeNevoa> MemoriasDeNevoa { get; }
    DbSet<CategoriaDeModelo> CategoriasDeModelo { get; }
    DbSet<ModeloDeToken> ModelosDeToken { get; }

    /// <summary>Para transações explícitas quando uma operação tem mais de um passo.</summary>
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
