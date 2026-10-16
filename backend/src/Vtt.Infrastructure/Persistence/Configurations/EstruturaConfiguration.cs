using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vtt.Domain.Imagens;
using Vtt.Domain.Mapas;

namespace Vtt.Infrastructure.Persistence.Configurations;

public class EstruturaConfiguration : IEntityTypeConfiguration<Estrutura>
{
    public void Configure(EntityTypeBuilder<Estrutura> builder)
    {
        builder.ToTable("estruturas", tabela =>
        {
            tabela.HasCheckConstraint("ck_estruturas_area", "x >= 0 AND y >= 0 AND largura >= 1 AND profundidade >= 1");
            tabela.HasCheckConstraint("ck_estruturas_altura", "altura >= 0.1 AND altura <= 3");
            tabela.HasCheckConstraint("ck_estruturas_rotacao", "rotacao_graus IN (0, 90, 180, 270)");
        });

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Tipo).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.Cor).HasMaxLength(7).IsRequired();
        builder.Property(e => e.TexturaPadrao).HasMaxLength(TexturasPadrao.ChaveTamanhoMaximo);
        builder.Property(e => e.BloqueiaMovimento).HasDefaultValue(true);
        builder.Property(e => e.Versao).IsConcurrencyToken();
        builder.Ignore(e => e.Bloqueia);

        builder.HasOne<Mapa>()
            .WithMany()
            .HasForeignKey(e => e.MapaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ImagemUpload>()
            .WithMany()
            .HasForeignKey(e => e.TexturaImagemId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(e => e.MapaId);
    }
}
