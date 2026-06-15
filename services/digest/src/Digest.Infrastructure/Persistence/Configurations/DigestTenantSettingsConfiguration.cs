using Digest.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Digest.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core de <see cref="DigestTenantSettings"/>.
/// Mapeia para <c>digest_tenant_settings</c> com <c>tenant_id</c> como PK.
/// RLS e Global Query Filter garantem isolamento por tenant (ADR-0001, VAL-ACT-02).
/// </summary>
internal sealed class DigestTenantSettingsConfiguration : IEntityTypeConfiguration<DigestTenantSettings>
{
    public void Configure(EntityTypeBuilder<DigestTenantSettings> builder)
    {
        builder.ToTable("digest_tenant_settings");

        builder.HasKey(e => e.TenantId);

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(e => e.ActionTokenTtlHours)
            .HasColumnName("action_token_ttl_hours")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired();
    }
}
