namespace ActivityManagement.Infrastructure.Persistence.Configurations;

using ActivityManagement.Infrastructure.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Configuração EF Core de consumo para <see cref="DigestActionToken"/>.
///
/// ADR-0006: o digest (BC-06) é owner exclusivo do schema da tabela
/// <c>digest_action_tokens</c>. O activity-management é somente consumidor:
/// lê por <c>token_hash</c> e grava <c>used_at</c>.
///
/// Por isso, a tabela é mapeada com <c>ExcludeFromMigrations()</c> — nenhuma migration
/// gerada pelo activity-management tocará nesta tabela. O schema é criado e versionado
/// exclusivamente pelo digest.
///
/// Contrato canônico (alinhado ao digest):
///   - <c>token_hash</c>  BYTEA NOT NULL (SHA-256, 32 bytes) — índice único.
///   - <c>activity_id</c> UUID NOT NULL (referência lógica, sem FK física — DD-001).
///   - <c>action</c>      VARCHAR(20) NOT NULL — casing 'Complete'/'Reschedule' (enum digest).
///
/// Mapeia: design §6.4, §7, DD-003, DD-007, ADR-0006, TASK-13.
/// </summary>
internal sealed class DigestActionTokenConfiguration : IEntityTypeConfiguration<DigestActionToken>
{
    public void Configure(EntityTypeBuilder<DigestActionToken> builder)
    {
        // ExcludeFromMigrations: activity-management nunca gera DDL para esta tabela.
        // O schema é propriedade exclusiva do digest (ADR-0006).
        builder.ToTable("digest_action_tokens", t => t.ExcludeFromMigrations());

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(t => t.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(t => t.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid")
            .IsRequired();

        // NOT NULL — referência lógica sem FK física (DD-001, ADR-0006)
        builder.Property(t => t.ActivityId)
            .HasColumnName("activity_id")
            .HasColumnType("uuid")
            .IsRequired();

        // Casing canônico do enum ActionType do digest: 'Complete' / 'Reschedule'
        builder.Property(t => t.Action)
            .HasColumnName("action")
            .HasColumnType("varchar(20)")
            .HasMaxLength(20)
            .IsRequired();

        // BYTEA NOT NULL — SHA-256 do token opaco; nunca o token em claro (DD-007)
        builder.Property(t => t.TokenHash)
            .HasColumnName("token_hash")
            .HasColumnType("bytea")
            .IsRequired();

        builder.Property(t => t.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(t => t.UsedAt)
            .HasColumnName("used_at")
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        // Índice único pelo hash — anti-enumeração, uso único (DD-007, RNF 5)
        // Definido aqui apenas como metadado; o índice físico é criado pela migration do digest.
        builder.HasIndex(t => t.TokenHash)
            .IsUnique()
            .HasDatabaseName("uq_digest_action_tokens_hash");
    }
}
