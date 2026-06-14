namespace ActivityManagement.Infrastructure.Persistence.Configurations;

using ActivityManagement.Infrastructure.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Configuração EF Core para <see cref="DigestActionToken"/>.
/// Índice único em <c>token_hash</c> garante unicidade e previne replay (DD-003, RNF 5).
/// RLS aplicada na migration: políticas por <c>tenant_id</c>.
/// Mapeia: design §7, DD-003, TASK-13.
/// </summary>
internal sealed class DigestActionTokenConfiguration : IEntityTypeConfiguration<DigestActionToken>
{
    public void Configure(EntityTypeBuilder<DigestActionToken> builder)
    {
        builder.ToTable("digest_action_tokens");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(t => t.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(t => t.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(t => t.ActivityId)
            .HasColumnName("activity_id")
            .IsRequired(false);

        builder.Property(t => t.Action)
            .HasColumnName("action")
            .HasMaxLength(20)
            .IsRequired();

        // O token em claro nunca é persistido — apenas o hash (DD-003)
        builder.Property(t => t.TokenHash)
            .HasColumnName("token_hash")
            .IsRequired();

        builder.Property(t => t.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(t => t.UsedAt)
            .HasColumnName("used_at")
            .IsRequired(false);

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Índice único pelo hash do token (anti-enumeração, uso único)
        builder.HasIndex(t => t.TokenHash)
            .IsUnique()
            .HasDatabaseName("uq_digest_action_tokens_hash");
    }
}
