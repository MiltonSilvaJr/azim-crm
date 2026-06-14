using Digest.Domain.Entities;
using Digest.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Digest.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core da entidade <see cref="DigestActionToken"/>.
/// Mapeia para a tabela <c>digest_action_tokens</c> com UNIQUE em <c>token_hash</c>,
/// índice de expiração e check constraint de <c>action</c> (design §7).
/// </summary>
internal sealed class DigestActionTokenConfiguration : IEntityTypeConfiguration<DigestActionToken>
{
    public void Configure(EntityTypeBuilder<DigestActionToken> builder)
    {
        builder.ToTable("digest_action_tokens");

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

        builder.Property(e => e.ActivityId)
            .HasColumnName("activity_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.Action)
            .HasColumnName("action")
            .HasColumnType("varchar(20)")
            .IsRequired()
            .HasConversion<string>();

        // token_hash BYTEA — SHA-256 do token opaco; nunca o token em claro (DD-007)
        builder.Property(e => e.TokenHash)
            .HasColumnName("token_hash")
            .HasColumnType("bytea")
            .IsRequired();

        builder.Property(e => e.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(e => e.UsedAt)
            .HasColumnName("used_at")
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        // UNIQUE token_hash — anti-enumeração (DD-007, RNF 7.4)
        builder.HasIndex(e => e.TokenHash)
            .IsUnique()
            .HasDatabaseName("uq_digest_action_tokens_hash");

        // índice de expiração para purge (RNF 9.2)
        builder.HasIndex(e => e.ExpiresAt)
            .HasDatabaseName("ix_digest_action_tokens_expires");
    }
}
