using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vtt.Domain.Imagens;
using Vtt.Domain.Mesas;
using Vtt.Infrastructure.Identity;

namespace Vtt.Infrastructure.Persistence.Configurations;

public class ImagemConfiguration : IEntityTypeConfiguration<ImagemUpload>
{
    public void Configure(EntityTypeBuilder<ImagemUpload> builder)
    {
        builder.ToTable("imagens", tabela =>
            tabela.HasCheckConstraint("ck_imagens_escopo", "(mesa_id IS NULL) <> (usuario_id IS NULL)"));

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.NomeOriginal).HasMaxLength(ImagemUpload.NomeOriginalTamanhoMaximo).IsRequired();
        builder.Property(i => i.ContentType).HasMaxLength(20).IsRequired();
        builder.Property(i => i.CaminhoArmazenado).HasMaxLength(100).IsRequired();
        builder.Ignore(i => i.CaminhoDaMiniatura);

        builder.HasOne<Mesa>()
            .WithMany()
            .HasForeignKey(i => i.MesaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(i => i.EnviadaPorUsuarioId)
            .HasConstraintName("fk_imagens_usuarios_enviada_por_usuario_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(i => i.UsuarioId)
            .HasConstraintName("fk_imagens_usuarios_usuario_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => i.MesaId);
        builder.HasIndex(i => i.UsuarioId);

        // Uma cópia por mesa de cada imagem pessoal.
        builder.HasIndex(i => new { i.MesaId, i.OrigemImagemId })
            .IsUnique()
            .HasFilter("origem_imagem_id IS NOT NULL");
    }
}
