using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vtt.Domain.Mesas;
using Vtt.Infrastructure.Identity;

namespace Vtt.Infrastructure.Persistence.Configurations;

public class MesaConfiguration : IEntityTypeConfiguration<Mesa>
{
    public void Configure(EntityTypeBuilder<Mesa> builder)
    {
        builder.ToTable("mesas");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Nome).HasMaxLength(Mesa.NomeTamanhoMaximo).IsRequired();
        builder.Property(m => m.Descricao).HasMaxLength(Mesa.DescricaoTamanhoMaximo);
        builder.Property(m => m.SessaoAtiva).HasDefaultValue(false);
        builder.Property(m => m.Movimentacao)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(ModoDeMovimentacao.PorBlocos);

        // O criador é só um registro histórico; quem manda na mesa é o MesaMembro com papel Mestre.
        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(m => m.CriadaPorUsuarioId)
            .HasConstraintName("fk_mesas_usuarios_criada_por_usuario_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(m => m.Membros)
            .WithOne()
            .HasForeignKey(membro => membro.MesaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(m => m.Convites)
            .WithOne()
            .HasForeignKey(convite => convite.MesaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class MesaMembroConfiguration : IEntityTypeConfiguration<MesaMembro>
{
    public void Configure(EntityTypeBuilder<MesaMembro> builder)
    {
        builder.ToTable("mesa_membros");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Papel).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(m => m.UsuarioId)
            .HasConstraintName("fk_mesa_membros_usuarios_usuario_id")
            .OnDelete(DeleteBehavior.Cascade);

        // Excluir o personagem não tira o jogador da mesa: ele só fica sem personagem ativo.
        builder.HasOne(m => m.PersonagemAtivo)
            .WithMany()
            .HasForeignKey(m => m.PersonagemAtivoId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(m => new { m.MesaId, m.UsuarioId }).IsUnique();
        builder.HasIndex(m => m.UsuarioId);
    }
}

public class ConviteConfiguration : IEntityTypeConfiguration<Convite>
{
    public void Configure(EntityTypeBuilder<Convite> builder)
    {
        builder.ToTable("convites", tabela =>
            tabela.HasCheckConstraint("ck_convites_usos", "usos_atuais >= 0 AND (usos_maximos IS NULL OR usos_atuais <= usos_maximos)"));

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Codigo).HasMaxLength(Convite.CodigoTamanho).IsFixedLength().IsRequired();

        builder.HasIndex(c => c.Codigo).IsUnique();
        builder.HasIndex(c => c.MesaId);
    }
}
