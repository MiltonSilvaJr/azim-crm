using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpportunityPipeline.Domain.Opportunities.Entities;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core para OpportunityStageTransition (append-only, RNF 7).
/// REVOKE UPDATE, DELETE aplicado em migration manual.
/// Mapeia: design §7.2, TASK-13.
/// </summary>
internal sealed class OpportunityStageTransitionConfiguration : IEntityTypeConfiguration<OpportunityStageTransition>
{
    public void Configure(EntityTypeBuilder<OpportunityStageTransition> builder)
    {
        builder.ToTable("opportunity_stage_transitions");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(t => t.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(t => t.OpportunityId).HasColumnName("opportunity_id").IsRequired();
        builder.Property(t => t.FromStageId).HasColumnName("from_stage_id");
        builder.Property(t => t.ToStageId).HasColumnName("to_stage_id").IsRequired();

        builder.Property(t => t.FromCategory)
            .HasColumnName("from_category")
            .HasMaxLength(20)
            .HasConversion(
                v => v.HasValue ? v.Value.ToString().ToLowerInvariant() : null,
                v => v != null ? (StageCategory?)Enum.Parse(typeof(StageCategory), v, ignoreCase: true) : null);

        builder.Property(t => t.ToCategory)
            .HasColumnName("to_category")
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => (StageCategory)Enum.Parse(typeof(StageCategory), v, ignoreCase: true));

        builder.Property(t => t.ActorId).HasColumnName("actor_id").IsRequired();
        builder.Property(t => t.OccurredAt).HasColumnName("occurred_at").IsRequired();

        // Índice para timeline (design §7.2)
        builder.HasIndex(t => new { t.TenantId, t.OpportunityId, t.OccurredAt })
            .HasDatabaseName("idx_stage_transitions_tenant_opp");
    }
}
