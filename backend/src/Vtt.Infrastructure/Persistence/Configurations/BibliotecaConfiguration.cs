using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vtt.Domain.Biblioteca;
using Vtt.Domain.Imagens;
using Vtt.Domain.Mapas;
using Vtt.Infrastructure.Identity;

namespace Vtt.Infrastructure.Persistence.Configurations;

public class CategoriaDeModeloConfiguration : IEntityTypeConfiguration<CategoriaDeModelo>
{
    public void Configure(EntityTypeBuilder<CategoriaDeModelo> builder)
    {
        builder.ToTable("categorias_de_modelo");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Nome).HasMaxLength(CategoriaDeModelo.NomeTamanhoMaximo).IsRequired();

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(c => c.UsuarioId)
            .HasConstraintName("fk_categorias_de_modelo_usuarios_usuario_id")
            .OnDelete(DeleteBehavior.Cascade);

        // A unicidade do nome sem diferenciar maiúsculas é um índice sobre lower(nome), criado na migration.
        builder.HasIndex(c => c.UsuarioId);
    }
}

public class ModeloDeTokenConfiguration : IEntityTypeConfiguration<ModeloDeToken>
{
    public void Configure(EntityTypeBuilder<ModeloDeToken> builder)
    {
        builder.ToTable("modelos_de_token", tabela =>
        {
            tabela.HasCheckConstraint("ck_modelos_de_token_tipo", "tipo IN ('Inimigo', 'Npc', 'Interacao')");
            tabela.HasCheckConstraint("ck_modelos_de_token_hp_maximo", "hp_maximo BETWEEN 1 AND 999");
            tabela.HasCheckConstraint("ck_modelos_de_token_deslocamento", "deslocamento BETWEEN 1 AND 30");
            tabela.HasCheckConstraint("ck_modelos_de_token_visao", "visao BETWEEN 0 AND 60");
        });

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Tipo).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(m => m.Nome).HasMaxLength(Token.NomeTamanhoMaximo).IsRequired();
        builder.Property(m => m.Icone).HasMaxLength(Token.IconeTamanhoMaximo).IsRequired();
        builder.Property(m => m.CorBorda).HasMaxLength(7).IsRequired();
        builder.Property(m => m.ExibicaoHp).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(m => m.Descricao).HasMaxLength(Token.DescricaoTamanhoMaximo);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(m => m.UsuarioId)
            .HasConstraintName("fk_modelos_de_token_usuarios_usuario_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<CategoriaDeModelo>()
            .WithMany()
            .HasForeignKey(m => m.CategoriaId)
            .OnDelete(DeleteBehavior.SetNull);

        // A imagem pessoal não sai enquanto um modelo a usar. NO ACTION (e não RESTRICT) para que a
        // exclusão do usuário, que apaga modelos e imagens em cascata, não dependa da ordem.
        builder.HasOne<ImagemUpload>()
            .WithMany()
            .HasForeignKey(m => m.ImagemId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<ImagemUpload>()
            .WithMany()
            .HasForeignKey(m => m.ConteudoImagemId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(m => m.UsuarioId);
        builder.HasIndex(m => m.CategoriaId);
        builder.HasIndex(m => m.ImagemId);
        builder.HasIndex(m => m.ConteudoImagemId);
    }
}
