using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PartnerManagement.Infrastructure.Outbox;

namespace PartnerManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core para <see cref="OutboxMessage"/>.
/// Nomes físicos em <c>snake_case</c> (rule database-naming.md).
/// Mapeia: design §6.3, design §6.6, design §7, TASK-15.
/// </summary>
internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    internal const string TableName = "outbox_messages";
    internal const string ColId = "id";
    internal const string ColTenantId = "tenant_id";
    internal const string ColEventType = "event_type";
    internal const string ColPayloadJson = "payload_json";
    internal const string ColOccurredAt = "occurred_at";
    internal const string ColPublishedAt = "published_at";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id)
            .HasColumnName(ColId)
            .ValueGeneratedNever();

        builder.Property(o => o.TenantId)
            .HasColumnName(ColTenantId)
            .IsRequired();

        builder.Property(o => o.EventType)
            .HasColumnName(ColEventType)
            .IsRequired();

        builder.Property(o => o.PayloadJson)
            .HasColumnName(ColPayloadJson)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(o => o.OccurredAt)
            .HasColumnName(ColOccurredAt)
            .IsRequired();

        builder.Property(o => o.PublishedAt)
            .HasColumnName(ColPublishedAt);
    }
}
