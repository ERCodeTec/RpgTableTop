using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vtt.Infrastructure.Identity;

namespace Vtt.Infrastructure.Persistence.Configurations;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuarios");
        builder.Property(u => u.NomeExibicao).HasMaxLength(40).IsRequired();
        builder.HasIndex(u => u.NormalizedEmail).HasDatabaseName("ix_usuarios_email_normalizado").IsUnique();
        builder.HasIndex(u => u.NormalizedUserName).HasDatabaseName("ix_usuarios_username_normalizado").IsUnique();

        builder.HasMany<IdentityUserClaim<Guid>>()
            .WithOne()
            .HasForeignKey(claim => claim.UserId)
            .HasConstraintName("fk_usuario_claims_usuarios_user_id");
    }
}

public class UsuarioClaimConfiguration : IEntityTypeConfiguration<IdentityUserClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder) => builder.ToTable("usuario_claims");
}

public class UsuarioLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder) => builder.ToTable("usuario_logins");
}

public class UsuarioTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder) => builder.ToTable("usuario_tokens");
}
