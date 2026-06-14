using Digest.Domain.Entities;
using Digest.Domain.Enums;
using Digest.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;

namespace Digest.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core da entidade <see cref="EmailDigestLog"/>.
/// Mapeia para a tabela <c>email_digest_logs</c> com UNIQUE, índices e check constraint (design §7).
/// Global Query Filter por <c>tenant_id</c> aplicado no <see cref="DigestDbContext"/>.
/// </summary>
internal sealed class EmailDigestLogConfiguration : IEntityTypeConfiguration<EmailDigestLog>
{
    public void Configure(EntityTypeBuilder<EmailDigestLog> builder)
    {
        builder.ToTable("email_digest_logs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid")
            .IsRequired();

        // DigestDate.Value é LocalDate (NodaTime) — mapeado como DATE no PostgreSQL
        builder.Property(e => e.DigestDate)
            .HasColumnName("digest_date")
            .HasColumnType("date")
            .IsRequired()
            .HasConversion(
                d => d.Value,
                v => new DigestDate(v));

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(20)")
            .IsRequired()
            .HasConversion<string>();

        builder.Property(e => e.MessageId)
            .HasColumnName("message_id")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(e => e.CorrelationId)
            .HasColumnName("correlation_id")
            .HasColumnType("uuid")
            .IsRequired(false);

        builder.Property(e => e.ScheduledAt)
            .HasColumnName("scheduled_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(e => e.SentAt)
            .HasColumnName("sent_at")
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.Property(e => e.DeliveredAt)
            .HasColumnName("delivered_at")
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.Property(e => e.OpenedAt)
            .HasColumnName("opened_at")
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.Property(e => e.FailedAt)
            .HasColumnName("failed_at")
            .HasColumnType("timestamptz")
            .IsRequired(false);

        // UNIQUE (tenant_id, user_id, digest_date) — RN-010: idempotência
        builder.HasIndex(e => new { e.TenantId, e.UserId, e.DigestDate })
            .IsUnique()
            .HasDatabaseName("uq_email_digest_logs_tenant_user_date");

        // índice de acesso por tenant + data (ix_email_digest_logs_tenant_date)
        builder.HasIndex(e => new { e.TenantId, e.DigestDate })
            .HasDatabaseName("ix_email_digest_logs_tenant_date");

        // índice por tenant + status (ix_email_digest_logs_status)
        builder.HasIndex(e => new { e.TenantId, e.Status })
            .HasDatabaseName("ix_email_digest_logs_status");
    }
}
