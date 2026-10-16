using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vtt.Domain.Imagens;
using Vtt.Domain.Mapas;
using Vtt.Domain.Mesas;
using Vtt.Domain.Personagens;
using Vtt.Infrastructure.Identity;

namespace Vtt.Infrastructure.Persistence.Configurations;

public class MapaConfiguration : IEntityTypeConfiguration<Mapa>
{
    public void Configure(EntityTypeBuilder<Mapa> builder)
    {
        builder.ToTable("mapas", tabela =>
        {
            tabela.HasCheckConstraint("ck_mapas_largura", "largura BETWEEN 5 AND 100");
            tabela.HasCheckConstraint("ck_mapas_altura", "altura BETWEEN 5 AND 100");
        });

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Nome).HasMaxLength(Mapa.NomeTamanhoMaximo).IsRequired();
        builder.Property(m => m.CorFundo).HasMaxLength(7).IsRequired();
        builder.Property(m => m.Ativo).HasDefaultValue(false);

        builder.HasOne<Mesa>()
            .WithMany()
            .HasForeignKey(m => m.MesaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(m => m.FundoEscala).HasDefaultValue(1d);
        builder.Property(m => m.Nevoa).HasConversion<string>().HasMaxLength(20).HasDefaultValue(ModoDeNevoa.Desligada).IsRequired();
        builder.Ignore(m => m.Celulas);

        // NoAction: o banco recusa apagar uma imagem em uso, mas deixa a exclusão da mesa levar tudo junto.
        builder.HasOne<ImagemUpload>()
            .WithMany()
            .HasForeignKey(m => m.ImagemFundoId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(m => m.MesaId);

        // No máximo um mapa ativo por mesa, garantido pelo banco.
        builder.HasIndex(m => m.MesaId, "ix_mapas_mesa_id_ativo")
            .HasDatabaseName("ix_mapas_mesa_id_ativo")
            .IsUnique()
            .HasFilter("ativo");
    }
}

public class TokenConfiguration : IEntityTypeConfiguration<Token>
{
    public void Configure(EntityTypeBuilder<Token> builder)
    {
        builder.ToTable("tokens", tabela =>
        {
            tabela.HasCheckConstraint("ck_tokens_hp", "hp_maximo BETWEEN 1 AND 999 AND hp_atual BETWEEN 0 AND hp_maximo");
            tabela.HasCheckConstraint("ck_tokens_posicao", "x >= 0 AND y >= 0");
            tabela.HasCheckConstraint("ck_tokens_movimento", "deslocamento BETWEEN 1 AND 30 AND movimento_restante BETWEEN 0 AND CASE WHEN disparada THEN deslocamento * 2 ELSE deslocamento END");
            tabela.HasCheckConstraint("ck_tokens_visao", "visao BETWEEN 0 AND 60");
        });

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Tipo).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(t => t.Nome).HasMaxLength(Token.NomeTamanhoMaximo).IsRequired();
        builder.Property(t => t.CorBorda).HasMaxLength(7).IsRequired();
        builder.Property(t => t.Icone).HasMaxLength(Token.IconeTamanhoMaximo).IsRequired();
        builder.Property(t => t.Visivel).HasDefaultValue(true);
        builder.Property(t => t.Versao).IsConcurrencyToken();
        builder.Property(t => t.Deslocamento).HasDefaultValue(Personagem.DeslocamentoPadrao);
        builder.Property(t => t.Visao).HasDefaultValue(0);
        builder.Property(t => t.JogouNaRodada).HasDefaultValue(false);
        builder.Ignore(t => t.MovimentoPendente);
        builder.Ignore(t => t.MovimentoMaximo);
        // Sentinel fora do enum: sem ele, Oculto (o valor 0) contaria como "não informado" e o banco gravaria o padrão.
        builder.Property(t => t.ExibicaoHp).HasConversion<string>().HasMaxLength(20).HasDefaultValue(ExibicaoHp.Barra)
            .HasSentinel((ExibicaoHp)(-1)).IsRequired();
        builder.Ignore(t => t.Caido);
        builder.Ignore(t => t.HpPercentual);

        builder.HasOne<Mapa>()
            .WithMany()
            .HasForeignKey(t => t.MapaId)
            .OnDelete(DeleteBehavior.Cascade);

        // A exclusão do personagem remove o token pelo serviço; o SetNull é só a rede de segurança.
        builder.HasOne<Personagem>()
            .WithMany()
            .HasForeignKey(t => t.PersonagemId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<ImagemUpload>()
            .WithMany()
            .HasForeignKey(t => t.ImagemId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(t => t.DonoUsuarioId)
            .HasConstraintName("fk_tokens_usuarios_dono_usuario_id")
            .OnDelete(DeleteBehavior.Cascade);

        // Sem sobreposição entre tokens visíveis. Um token oculto não ocupa a célula: se ocupasse,
        // um jogador barrado em uma célula "vazia" descobriria que há algo escondido ali.
        builder.HasIndex(t => new { t.MapaId, t.X, t.Y }).IsUnique().HasFilter("visivel");

        // Um token por jogador em cada mapa.
        builder.HasIndex(t => new { t.MapaId, t.DonoUsuarioId })
            .IsUnique()
            .HasFilter("dono_usuario_id IS NOT NULL");

        builder.HasIndex(t => t.PersonagemId);
        builder.HasIndex(t => t.DonoUsuarioId);
    }
}

public class MemoriaDeNevoaConfiguration : IEntityTypeConfiguration<MemoriaDeNevoa>
{
    public void Configure(EntityTypeBuilder<MemoriaDeNevoa> builder)
    {
        builder.ToTable("memorias_de_nevoa");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Tipo).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(m => m.Celulas).IsRequired();
        builder.Property(m => m.Versao).IsConcurrencyToken();

        builder.HasOne<Mapa>()
            .WithMany()
            .HasForeignKey(m => m.MapaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(m => m.UsuarioId)
            .HasConstraintName("fk_memorias_de_nevoa_usuarios_usuario_id")
            .OnDelete(DeleteBehavior.Cascade);

        // Uma camada de cada tipo por mapa e por dono; a do grupo (sem dono) também é única.
        builder.HasIndex(m => new { m.MapaId, m.Tipo, m.UsuarioId }).IsUnique().AreNullsDistinct(false);
        builder.HasIndex(m => m.UsuarioId);
    }
}
