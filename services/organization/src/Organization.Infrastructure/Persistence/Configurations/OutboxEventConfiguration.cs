using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Organization.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core da entidade <see cref="OutboxEvent"/>.
/// Tabela: <c>outbox_events</c>.
/// </summary>
internal sealed class OutboxEventConfiguration : IEntityTypeConfiguration<OutboxEvent>
{
    public void Configure(EntityTypeBuilder<OutboxEvent> builder)
    {
        builder.ToTable("outbox_events");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(e => e.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.CorrelationId)
            .HasColumnName("correlation_id")
            .IsRequired();

        builder.Property(e => e.CausationId)
            .HasColumnName("causation_id")
            .IsRequired(false);

        builder.Property(e => e.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        builder.Property(e => e.PublishedAt)
            .HasColumnName("published_at")
            .IsRequired(false);

        // Índice para o OutboxWorker: busca de não publicados por tenant, ordenados por ocorrência
        builder.HasIndex(e => new { e.TenantId, e.PublishedAt, e.OccurredAt })
            .HasDatabaseName("idx_outbox_events_unpublished");
    }
}
