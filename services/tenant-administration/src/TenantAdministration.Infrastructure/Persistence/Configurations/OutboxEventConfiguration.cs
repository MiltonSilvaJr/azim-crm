using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TenantAdministration.Infrastructure.Outbox;

namespace TenantAdministration.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento Fluent API para <see cref="OutboxEvent"/> na tabela <c>outbox_events</c>.
/// Conforme design.md §6.6, §9 (envelope TRD).
/// </summary>
internal sealed class OutboxEventConfiguration : IEntityTypeConfiguration<OutboxEvent>
{
    public void Configure(EntityTypeBuilder<OutboxEvent> builder)
    {
        builder.ToTable("outbox_events");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.EventType)
            .HasColumnName("event_type")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.AggregateType)
            .HasColumnName("aggregate_type")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.AggregateId)
            .HasColumnName("aggregate_id")
            .IsRequired();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired(false);

        builder.Property(e => e.CorrelationId)
            .HasColumnName("correlation_id")
            .IsRequired(false)
            .HasMaxLength(200);

        builder.Property(e => e.Payload)
            .HasColumnName("payload")
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue(OutboxEventStatus.Pending)
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<OutboxEventStatus>(v, ignoreCase: true));

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.PublishedAt)
            .HasColumnName("published_at")
            .IsRequired(false);

        builder.Property(e => e.RetryCount)
            .HasColumnName("retry_count")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.LastError)
            .HasColumnName("last_error")
            .IsRequired(false);

        builder.HasIndex(e => e.Status)
            .HasDatabaseName("ix_outbox_events_status");
    }
}
