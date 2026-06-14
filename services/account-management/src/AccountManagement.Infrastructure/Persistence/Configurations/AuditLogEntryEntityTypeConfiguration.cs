using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core para <see cref="AuditLogEntry"/> (tabela <c>audit_logs</c>).
///
/// A tabela é append-only: sem coluna <c>updated_at</c>.
/// Imutabilidade garantida por trigger PL/pgSQL + REVOKE na migration (RNF 8, design §7).
///
/// Mapeia: design §7 (audit_logs), RNF 8, TASK-09.
/// </summary>
internal sealed class AuditLogEntryEntityTypeConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(e => e.EntityType)
            .HasColumnName("entity_type")
            .IsRequired();

        builder.Property(e => e.EntityId)
            .HasColumnName("entity_id")
            .IsRequired();

        builder.Property(e => e.Action)
            .HasColumnName("action")
            .IsRequired();

        builder.Property(e => e.DeltaJson)
            .HasColumnName("delta_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Índice de busca por tenant, tipo e entidade
        builder.HasIndex(e => new { e.TenantId, e.EntityType, e.EntityId })
            .HasDatabaseName("idx_audit_logs_tenant_entity");
    }
}
