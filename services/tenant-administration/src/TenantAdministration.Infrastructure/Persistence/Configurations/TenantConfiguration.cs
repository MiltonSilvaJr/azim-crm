using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TenantAdministration.Domain.Aggregates;
using TenantAdministration.Domain.ValueObjects;

namespace TenantAdministration.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento Fluent API da entidade <see cref="Tenant"/> para a tabela <c>tenants</c>.
/// Conforme design.md §7.1, DD-002 (status + projeção active).
/// </summary>
internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        // Slug: imutável, mapeado como conversion (design.md §4.3, RN-019)
        builder.Property(t => t.Slug)
            .HasColumnName("slug")
            .IsRequired()
            .HasMaxLength(40)
            .HasConversion(
                v => v.Value,
                v => Slug.Create(v).Value);

        builder.HasIndex(t => t.Slug)
            .IsUnique()
            .HasDatabaseName("uq_tenants_slug");

        builder.Property(t => t.DisplayName)
            .HasColumnName("display_name")
            .IsRequired();

        builder.Property(t => t.Timezone)
            .HasColumnName("iana_timezone")
            .IsRequired()
            .HasDefaultValue(TimezoneIana.Create("America/Sao_Paulo").Value)
            .HasConversion(
                v => v.Value,
                v => TimezoneIana.Create(v).Value);

        builder.Property(t => t.DigestTime)
            .HasColumnName("digest_time")
            .IsRequired()
            .HasDefaultValue(Domain.ValueObjects.DigestTime.Create("07:00").Value)
            .HasConversion(
                v => v.Value,
                v => Domain.ValueObjects.DigestTime.Create(v).Value);

        // Status: enum armazenado como string; projeção active calculada (DD-002)
        builder.Property(t => t.Status)
            .HasColumnName("status")
            .IsRequired()
            .HasDefaultValue(TenantStatus.Provisioned)
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<TenantStatus>(v, ignoreCase: true));

        // active é projeção de Status; ignorado pelo EF (DD-002 — calculado via trigger/view)
        builder.Ignore(t => t.Active);

        builder.Property(t => t.IdentityTenantId)
            .HasColumnName("identity_tenant_id")
            .IsRequired(false);

        builder.Property(t => t.ProvisionedAt)
            .HasColumnName("provisioned_at")
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Branding: navegação 1:1 — entidade interna ao agregado
        builder.HasOne(t => t.Branding)
            .WithOne()
            .HasForeignKey<Domain.Entities.TenantBranding>("TenantId")
            .HasConstraintName("fk_tenant_brandings_tenant_id");

        // Domain events: nunca persistidos pelo EF; ignorados
        builder.Ignore(t => t.DomainEvents);
    }
}
