namespace ActivityManagement.Infrastructure.Persistence.Configurations;

using ActivityManagement.Domain.Activities;
using ActivityManagement.Domain.Activities.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Configuração EF Core para o agregado <see cref="Activity"/>.
/// Aplica mapeamento <c>snake_case</c> conforme <c>database-naming.md</c>.
/// Objetos de valor mapeados via conversores de valor (owned types causariam
/// colunas desnecessárias aqui; conversores são mais diretos para VOs escalares).
/// Mapeia: design §6.1, §7, TASK-13.
/// </summary>
internal sealed class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.ToTable("activities");

        // PK
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasColumnName("id")
            .ValueGeneratedNever(); // UUID gerado no domínio

        // Colunas obrigatórias — tenant e BU
        builder.Property(a => a.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(a => a.BuId)
            .HasColumnName("bu_id")
            .IsRequired();

        builder.Property(a => a.OwnerId)
            .HasColumnName("owner_id")
            .IsRequired();

        // Vínculos opcionais (Req 3.1)
        builder.Property(a => a.OpportunityLink)
            .HasColumnName("opportunity_id")
            .HasConversion(
                link => link != null ? link.OpportunityId : (Guid?)null,
                id   => id.HasValue ? OpportunityLink.Create(id.Value) : null)
            .IsRequired(false);

        builder.Property(a => a.AccountLink)
            .HasColumnName("account_id")
            .HasConversion(
                link => link != null ? link.AccountId : (Guid?)null,
                id   => id.HasValue ? AccountLink.Create(id.Value) : null)
            .IsRequired(false);

        // Tipo da atividade (objeto de valor → string)
        builder.Property(a => a.Type)
            .HasColumnName("activity_type")
            .HasMaxLength(20)
            .HasConversion(
                t => t.Value,
                s => ActivityType.Create(s))
            .IsRequired();

        // Título (texto livre — PII potencial, nunca logado)
        builder.Property(a => a.Title)
            .HasColumnName("title")
            .IsRequired();

        // Descrição (opcional, PII potencial)
        builder.Property(a => a.Description)
            .HasColumnName("description")
            .IsRequired(false);

        // Data de vencimento (objeto de valor → DateTimeOffset)
        builder.Property(a => a.DueAt)
            .HasColumnName("due_at")
            .HasConversion(
                d => d.Value,
                v => DueDate.Create(v))
            .IsRequired();

        // Status (objeto de valor → string)
        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion(
                s => s.Value,
                v => ActivityStatus.Create(v))
            .IsRequired()
            .HasDefaultValueSql("'pending'");

        // Prioridade (objeto de valor → string)
        builder.Property(a => a.Priority)
            .HasColumnName("priority")
            .HasMaxLength(10)
            .HasConversion(
                p => p.Value,
                v => ActivityManagement.Domain.Activities.ValueObjects.Priority.Create(v))
            .IsRequired()
            .HasDefaultValueSql("'medium'");

        // Conclusão (nullable)
        builder.Property(a => a.CompletedAt)
            .HasColumnName("completed_at")
            .IsRequired(false);

        // Auditoria temporal
        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(a => a.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Domain events não são persistidos — apenas acumulados em memória
        builder.Ignore(a => a.DomainEvents);
    }
}
