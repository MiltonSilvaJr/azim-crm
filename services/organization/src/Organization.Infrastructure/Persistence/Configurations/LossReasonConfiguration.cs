using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Organization.Domain.Aggregates;

namespace Organization.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core da entidade interna <see cref="LossReason"/> (pertence a <see cref="BusinessUnit"/>).
/// </summary>
internal sealed class LossReasonConfiguration : IEntityTypeConfiguration<LossReason>
{
    public void Configure(EntityTypeBuilder<LossReason> builder)
    {
        builder.ToTable("loss_reasons");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");

        builder.Property<Guid>("TenantId").HasColumnName("tenant_id").IsRequired();
        builder.Property<Guid>("BuId").HasColumnName("bu_id").IsRequired();

        builder.Property(r => r.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.Active)
            .HasColumnName("active")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property<DateTimeOffset>("CreatedAt")
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.HasIndex("TenantId", "BuId", nameof(LossReason.Name))
            .IsUnique()
            .HasDatabaseName("uq_loss_reasons_tenant_bu_name");
    }
}
