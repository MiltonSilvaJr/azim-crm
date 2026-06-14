using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PartnerManagement.Infrastructure.Idempotency;

namespace PartnerManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core para <see cref="IdempotencyKey"/>.
/// Chave primária composta: (<c>tenant_id</c>, <c>idempotency_key</c>).
/// Nomes físicos em <c>snake_case</c> (rule database-naming.md).
/// Mapeia: design §6.5, design §7, TASK-15.
/// </summary>
internal sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    internal const string TableName = "idempotency_keys";
    internal const string ColTenantId = "tenant_id";
    internal const string ColKey = "idempotency_key";
    internal const string ColRequestHash = "request_hash";
    internal const string ColResponseRef = "response_ref";
    internal const string ColCreatedAt = "created_at";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<IdempotencyKey> builder)
    {
        builder.ToTable(TableName);

        // Chave primária composta (tenant_id, idempotency_key)
        builder.HasKey(k => new { k.TenantId, k.Key });

        builder.Property(k => k.TenantId)
            .HasColumnName(ColTenantId)
            .IsRequired();

        builder.Property(k => k.Key)
            .HasColumnName(ColKey)
            .IsRequired();

        builder.Property(k => k.RequestHash)
            .HasColumnName(ColRequestHash)
            .IsRequired();

        builder.Property(k => k.ResponseRef)
            .HasColumnName(ColResponseRef);

        builder.Property(k => k.CreatedAt)
            .HasColumnName(ColCreatedAt)
            .IsRequired();
    }
}
