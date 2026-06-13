using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TenantAdministration.Domain.Entities;
using TenantAdministration.Domain.ValueObjects;

namespace TenantAdministration.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento Fluent API da entidade interna <see cref="TenantBranding"/> para a tabela
/// <c>tenant_brandings</c>. RLS habilitado por migration (design.md §7.2, §7.4, ADR-0001).
/// Global query filter aplicado via <see cref="TenantAdministrationDbContext"/>.
/// </summary>
internal sealed class TenantBrandingConfiguration : IEntityTypeConfiguration<TenantBranding>
{
    public void Configure(EntityTypeBuilder<TenantBranding> builder)
    {
        builder.ToTable("tenant_brandings");

        // PK próprio; TenantId é FK com unicidade 1:1
        // Usando Guid? para que o EF Core possa detectar ausência do registro no LEFT JOIN
        // (quando Branding é null, a coluna 'id' vem como NULL — Guid não-nullable lança exceção).
        builder.Property<Guid?>("Id")
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();
        builder.HasKey("Id");

        // FK para tenants (shadow property — compartilhada com Tenant)
        // Guid? para permitir que o EF Core detecte ausência no LEFT JOIN.
        builder.Property<Guid?>("TenantId")
            .HasColumnName("tenant_id")
            .IsRequired(false);

        builder.HasIndex("TenantId")
            .IsUnique()
            .HasDatabaseName("uq_tenant_brandings_tenant");

        builder.Property(b => b.WcagContrastOk)
            .HasColumnName("wcag_contrast_ok")
            .IsRequired()
            .HasColumnType("boolean")
            .HasDefaultValueSql("false");

        builder.Property(b => b.LastContrastRatio)
            .HasColumnName("last_contrast_ratio")
            .HasPrecision(4, 2)
            .IsRequired(false);

        builder.Property(b => b.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired()
            .HasDefaultValueSql("now()");

        // BrandingTheme: mapeado como shadow properties escalares para evitar problemas
        // de materialização de records com owned types aninhados (ColorPair).
        // Os valores são reconstituídos em TenantBranding via propriedades de navegação
        // para o owned type quando necessário.
        builder.Property<string?>("LogoUrl")
            .HasColumnName("logo_url")
            .IsRequired(false);

        builder.Property<string?>("FaviconUrl")
            .HasColumnName("favicon_url")
            .IsRequired(false);

        builder.Property<string?>("PrimaryColor")
            .HasColumnName("primary_color")
            .HasMaxLength(7)
            .IsRequired(false);

        builder.Property<string?>("SecondaryColor")
            .HasColumnName("secondary_color")
            .HasMaxLength(7)
            .IsRequired(false);

        // Theme: ignorado pelo EF — reconstituído via propriedades sombra acima
        builder.Ignore(b => b.Theme);
    }
}
