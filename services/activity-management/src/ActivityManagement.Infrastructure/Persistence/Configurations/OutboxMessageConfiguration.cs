namespace ActivityManagement.Infrastructure.Persistence.Configurations;

using ActivityManagement.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Configuração EF Core para <see cref="OutboxMessage"/>.
/// Índice único em <c>(event_type, dedup_key)</c> previne duplicatas do scan de overdue.
/// Mapeia: design §7, TASK-13.
/// </summary>
internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(m => m.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(m => m.EventType)
            .HasColumnName("event_type")
            .IsRequired();

        builder.Property(m => m.DedupKey)
            .HasColumnName("dedup_key")
            .IsRequired(false);

        builder.Property(m => m.PayloadJson)
            .HasColumnName("payload_json")
            .IsRequired();

        builder.Property(m => m.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        builder.Property(m => m.PublishedAt)
            .HasColumnName("published_at")
            .IsRequired(false);

        // Índice para o relay: mensagens pendentes (sem published_at)
        builder.HasIndex(m => m.PublishedAt)
            .HasFilter("published_at IS NULL")
            .HasDatabaseName("idx_outbox_unpublished");

        // Índice único de deduplicação (dedup_key não nulo)
        builder.HasIndex(m => new { m.EventType, m.DedupKey })
            .HasFilter("dedup_key IS NOT NULL")
            .IsUnique()
            .HasDatabaseName("uq_outbox_dedup");
    }
}
