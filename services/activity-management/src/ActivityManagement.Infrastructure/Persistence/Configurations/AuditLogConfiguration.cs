namespace ActivityManagement.Infrastructure.Persistence.Configurations;

using ActivityManagement.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Configuração EF Core para <see cref="AuditLog"/>.
/// Tabela append-only: UPDATE e DELETE são bloqueados por trigger e REVOKE no banco.
/// Mapeia: design §7, RNF 2, TASK-13.
/// </summary>
internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(l => l.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(l => l.UserId)
            .HasColumnName("user_id")
            .IsRequired(false);

        builder.Property(l => l.EntityType)
            .HasColumnName("entity_type")
            .IsRequired();

        builder.Property(l => l.EntityId)
            .HasColumnName("entity_id")
            .IsRequired();

        builder.Property(l => l.Action)
            .HasColumnName("action")
            .IsRequired();

        builder.Property(l => l.DeltaJson)
            .HasColumnName("delta_json")
            .IsRequired();

        builder.Property(l => l.CorrelationId)
            .HasColumnName("correlation_id")
            .IsRequired(false);

        builder.Property(l => l.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Índice para consultas de auditoria por entidade dentro do tenant
        builder.HasIndex(l => new { l.TenantId, l.EntityType, l.EntityId })
            .HasDatabaseName("idx_audit_logs_tenant_entity");
    }
}
