using AccountManagement.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core para <see cref="OutboxMessage"/> (tabela <c>outbox_messages</c>).
///
/// Índice parcial <c>idx_outbox_unpublished</c> sobre registros com <c>published_at IS NULL</c>
/// para eficiência do relay (design §7, DD-007).
///
/// Mapeia: design §7, DD-007, TASK-09.
/// </summary>
internal sealed class OutboxMessageEntityTypeConfiguration : IEntityTypeConfiguration<OutboxMessage>
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

        builder.Property(m => m.PayloadJson)
            .HasColumnName("payload_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(m => m.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        builder.Property(m => m.PublishedAt)
            .HasColumnName("published_at");

        // Índice parcial para relay eficiente — apenas mensagens pendentes (design §7)
        builder.HasIndex(m => m.PublishedAt)
            .HasDatabaseName("idx_outbox_unpublished")
            .HasFilter("published_at IS NULL");
    }
}
