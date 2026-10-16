using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vtt.Domain.Imagens;
using Vtt.Domain.Personagens;
using Vtt.Infrastructure.Identity;

namespace Vtt.Infrastructure.Persistence.Configurations;

public class PersonagemConfiguration : IEntityTypeConfiguration<Personagem>
{
    public void Configure(EntityTypeBuilder<Personagem> builder)
    {
        builder.ToTable("personagens", tabela =>
        {
            tabela.HasCheckConstraint("ck_personagens_nivel", "nivel BETWEEN 1 AND 20");
            tabela.HasCheckConstraint("ck_personagens_hp_maximo", "hp_maximo BETWEEN 1 AND 999");
            tabela.HasCheckConstraint("ck_personagens_deslocamento", "deslocamento BETWEEN 1 AND 30");
            tabela.HasCheckConstraint("ck_personagens_visao", "visao BETWEEN 0 AND 60");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Nome).HasMaxLength(Personagem.NomeTamanhoMaximo).IsRequired();
        builder.Property(p => p.Classe).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.CorBorda).HasMaxLength(7).IsRequired();
        builder.Property(p => p.Deslocamento).HasDefaultValue(Personagem.DeslocamentoPadrao);
        builder.Property(p => p.Visao).HasDefaultValue(Personagem.VisaoPadrao);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(p => p.UsuarioId)
            .HasConstraintName("fk_personagens_usuarios_usuario_id")
            .OnDelete(DeleteBehavior.Cascade);

        // A exclusão do personagem apaga a foto pelo serviço; o SetNull é só a rede de segurança.
        builder.HasOne<ImagemUpload>()
            .WithMany()
            .HasForeignKey(p => p.ImagemId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(p => p.UsuarioId);
        builder.HasIndex(p => p.ImagemId);
    }
}
