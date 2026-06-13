using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Organization.Domain.Aggregates;
using Organization.Infrastructure.Persistence.Converters;

namespace Organization.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core do agregado <see cref="BusinessUnit"/>.
/// Nomes físicos em snake_case conforme database-naming rule.
/// As entidades internas (Stage, OriginChannel, LossReason) são carregadas
/// via navigation properties mapeadas pelos seus campos privados.
/// </summary>
internal sealed class BusinessUnitConfiguration : IEntityTypeConfiguration<BusinessUnit>
{
    public void Configure(EntityTypeBuilder<BusinessUnit> builder)
    {
        builder.ToTable("business_units");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("id");

        builder.Property(b => b.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(b => b.Name)
            .HasColumnName("name")
            .HasMaxLength(120)
            .IsRequired()
            .HasConversion(new BusinessUnitNameConverter());

        builder.Property(b => b.Active)
            .HasColumnName("active")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(b => b.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(b => b.DeactivatedAt)
            .HasColumnName("deactivated_at")
            .IsRequired(false);

        // Constraint de unicidade: nome único por tenant
        builder.HasIndex(b => new { b.TenantId, b.Name })
            .IsUnique()
            .HasDatabaseName("uq_business_units_tenant_id_name");

        // Navegação via campo privado _stages para a propriedade pública Stages
        builder.HasMany(b => b.Stages)
            .WithOne()
            .HasForeignKey("BuId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(b => b.Stages)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_stages");

        // Navegação via campo privado _originChannels
        builder.HasMany(b => b.OriginChannels)
            .WithOne()
            .HasForeignKey("BuId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(b => b.OriginChannels)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_originChannels");

        // Navegação via campo privado _lossReasons
        builder.HasMany(b => b.LossReasons)
            .WithOne()
            .HasForeignKey("BuId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(b => b.LossReasons)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_lossReasons");
    }
}
