using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpportunityPipeline.Infrastructure.Outbox;

namespace OpportunityPipeline.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configurações EF Core para entidades de suporte: numeração, detecção de estagnação,
/// filtros salvos e OutboxMessage.
/// Mapeia: design §7.4, TASK-13.
/// </summary>
internal sealed class OpportunityNumberSequenceConfiguration : IEntityTypeConfiguration<OpportunityNumberSequence>
{
    public void Configure(EntityTypeBuilder<OpportunityNumberSequence> builder)
    {
        builder.ToTable("opportunity_number_sequences");
        builder.HasKey(s => s.TenantId);
        builder.Property(s => s.TenantId).HasColumnName("tenant_id").ValueGeneratedNever();
        builder.Property(s => s.NextValue).HasColumnName("next_value").IsRequired();
    }
}

internal sealed class StaleDetectionRunConfiguration : IEntityTypeConfiguration<StaleDetectionRun>
{
    public void Configure(EntityTypeBuilder<StaleDetectionRun> builder)
    {
        builder.ToTable("stale_detection_runs");
        builder.HasKey(r => new { r.TenantId, r.OpportunityId, r.DetectionPeriod });
        builder.Property(r => r.TenantId).HasColumnName("tenant_id");
        builder.Property(r => r.OpportunityId).HasColumnName("opportunity_id");
        builder.Property(r => r.DetectionPeriod).HasColumnName("detection_period").HasMaxLength(10);
        builder.Property(r => r.DetectedAt).HasColumnName("detected_at").IsRequired();
    }
}

internal sealed class SavedFilterEntityConfiguration : IEntityTypeConfiguration<SavedFilterEntity>
{
    public void Configure(EntityTypeBuilder<SavedFilterEntity> builder)
    {
        builder.ToTable("saved_filters");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(f => f.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(f => f.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(f => f.Name).HasColumnName("name").IsRequired().HasMaxLength(200);
        builder.Property(f => f.CriteriaJson).HasColumnName("criteria").IsRequired().HasColumnType("JSONB");
        builder.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(f => new { f.TenantId, f.UserId, f.Name })
            .HasDatabaseName("uq_saved_filters_user_name")
            .IsUnique();
    }
}

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_events");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(m => m.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(m => m.EventType).HasColumnName("event_type").IsRequired().HasMaxLength(100);
        builder.Property(m => m.Payload).HasColumnName("payload").IsRequired().HasColumnType("JSONB");
        builder.Property(m => m.Status).HasColumnName("status").IsRequired().HasMaxLength(20).HasDefaultValue("pending");
        builder.Property(m => m.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(m => m.PublishedAt).HasColumnName("published_at");
        builder.Property(m => m.Attempts).HasColumnName("attempts").IsRequired().HasDefaultValue(0);
        builder.Property(m => m.LastError).HasColumnName("last_error");

        builder.HasIndex(m => new { m.TenantId, m.Status, m.CreatedAt })
            .HasDatabaseName("idx_outbox_pending")
            .HasFilter("status = 'pending'");
    }
}
