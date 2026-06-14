using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Organization.Domain.Aggregates;

namespace Organization.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core da entidade interna <see cref="OriginChannel"/> (pertence a <see cref="BusinessUnit"/>).
/// </summary>
internal sealed class OriginChannelConfiguration : IEntityTypeConfiguration<OriginChannel>
{
    public void Configure(EntityTypeBuilder<OriginChannel> builder)
    {
        builder.ToTable("origin_channels");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");

        builder.Property<Guid>("TenantId").HasColumnName("tenant_id").IsRequired();
        builder.Property<Guid>("BuId").HasColumnName("bu_id").IsRequired();

        builder.Property(c => c.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.Active)
            .HasColumnName("active")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property<DateTimeOffset>("CreatedAt")
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.HasIndex("TenantId", "BuId", nameof(OriginChannel.Name))
            .IsUnique()
            .HasDatabaseName("uq_origin_channels_tenant_bu_name");
    }
}
