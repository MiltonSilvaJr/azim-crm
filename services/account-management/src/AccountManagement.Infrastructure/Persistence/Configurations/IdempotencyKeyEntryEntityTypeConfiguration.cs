using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core para <see cref="IdempotencyKeyEntry"/> (tabela <c>idempotency_keys</c>).
///
/// PK composta: (<c>tenant_id</c>, <c>idempotency_key</c>) conforme design §7.
///
/// Mapeia: design §7 (idempotency_keys), design §6.5, TASK-09.
/// </summary>
internal sealed class IdempotencyKeyEntryEntityTypeConfiguration
    : IEntityTypeConfiguration<IdempotencyKeyEntry>
{
    public void Configure(EntityTypeBuilder<IdempotencyKeyEntry> builder)
    {
        builder.ToTable("idempotency_keys");

        // PK composta (tenant_id, idempotency_key) — design §7
        builder.HasKey(k => new { k.TenantId, k.IdempotencyKey });

        builder.Property(k => k.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(k => k.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .IsRequired();

        builder.Property(k => k.RequestHash)
            .HasColumnName("request_hash")
            .IsRequired();

        builder.Property(k => k.ResponseRef)
            .HasColumnName("response_ref");

        builder.Property(k => k.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
    }
}
