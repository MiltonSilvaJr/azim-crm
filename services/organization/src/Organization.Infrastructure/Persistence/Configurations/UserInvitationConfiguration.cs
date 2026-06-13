using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Organization.Domain.Aggregates;
using Organization.Domain.ValueObjects;

namespace Organization.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core do agregado <see cref="UserInvitation"/>.
/// A propriedade <c>TargetMemberships</c> (IReadOnlyList de tuplas) não é mapeável diretamente.
/// É armazenada via shadow property <c>TargetMembershipsJson</c> (JSONB) e gerenciada pelo
/// <see cref="OrganizationDbContext.SaveChangesAsync"/> (serialização) e
/// <see cref="OrganizationDbContext.RestoreTargetMemberships"/> (desserialização).
/// </summary>
internal sealed class UserInvitationConfiguration : IEntityTypeConfiguration<UserInvitation>
{
    public void Configure(EntityTypeBuilder<UserInvitation> builder)
    {
        builder.ToTable("user_invitations");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");

        builder.Property(i => i.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(i => i.Email)
            .HasColumnName("email")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(i => i.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(512)
            .IsRequired()
            .HasConversion(
                t => t.TokenHash,
                v => InvitationToken.FromHash(v));

        builder.Property(i => i.State)
            .HasColumnName("state")
            .HasMaxLength(10)
            .IsRequired()
            .HasConversion(
                s => s.ToString().ToLowerInvariant(),
                v => Enum.Parse<InvitationState>(v, ignoreCase: true));

        // Shadow property para serializar TargetMemberships como JSONB.
        // O valor é gerenciado manualmente pelo OrganizationDbContext.
        builder.Property<string>("TargetMembershipsJson")
            .HasColumnName("target_memberships")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(i => i.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(i => i.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(i => i.AcceptedAt)
            .HasColumnName("accepted_at")
            .IsRequired(false);

        // Ignora a propriedade de domínio (navegável mas não mapeada diretamente)
        builder.Ignore(i => i.TargetMemberships);

        // Índice para busca rápida por token_hash no tenant
        builder.HasIndex("TenantId", "TokenHash")
            .HasDatabaseName("idx_user_invitations_tenant_token_hash");

        // CHECK constraint de estado
        builder.ToTable(t => t.HasCheckConstraint(
            "chk_user_invitations_state",
            "state IN ('pending','accepted','revoked','expired')"));
    }
}
