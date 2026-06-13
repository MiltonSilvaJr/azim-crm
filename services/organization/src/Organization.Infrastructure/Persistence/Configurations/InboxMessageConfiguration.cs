using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Organization.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core da entidade <see cref="InboxMessage"/>.
/// Tabela: <c>inbox_messages</c>.
/// </summary>
internal sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages");

        // Chave composta: (message_id, tenant_id) garante deduplicação por tenant
        builder.HasKey(m => new { m.MessageId, m.TenantId });

        builder.Property(m => m.MessageId)
            .HasColumnName("message_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(m => m.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(m => m.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(m => m.ProcessedAt)
            .HasColumnName("processed_at")
            .IsRequired();
    }
}
