using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Organization.Domain.Aggregates;
using Organization.Domain.ValueObjects;

namespace Organization.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core da entidade interna <see cref="Stage"/> (pertence a <see cref="BusinessUnit"/>).
/// </summary>
internal sealed class StageConfiguration : IEntityTypeConfiguration<Stage>
{
    public void Configure(EntityTypeBuilder<Stage> builder)
    {
        builder.ToTable("stages");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        // tenant_id e bu_id são shadow properties (não existem na entidade Stage)
        builder.Property<Guid>("TenantId").HasColumnName("tenant_id").IsRequired();
        builder.Property<Guid>("BuId").HasColumnName("bu_id").IsRequired();

        builder.Property(s => s.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.Probability)
            .HasColumnName("probability")
            .IsRequired()
            .HasConversion(
                p => p.Value,
                v => Probability.Create(v));

        builder.Property(s => s.Category)
            .HasColumnName("category")
            .HasMaxLength(10)
            .IsRequired()
            .HasConversion(
                c => c.Value,
                v => StageCategory.Create(v));

        builder.Property(s => s.Position)
            .HasColumnName("position")
            .IsRequired();

        builder.Property<DateTimeOffset>("CreatedAt")
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Unicidade de nome por (tenant_id, bu_id, name)
        builder.HasIndex("TenantId", "BuId", nameof(Stage.Name))
            .IsUnique()
            .HasDatabaseName("uq_stages_tenant_bu_name");

        // Unicidade de position por (tenant_id, bu_id, position)
        builder.HasIndex("TenantId", "BuId", nameof(Stage.Position))
            .IsUnique()
            .HasDatabaseName("uq_stages_tenant_bu_position");

        // CHECK constraint validado no banco
        builder.ToTable(t => t.HasCheckConstraint(
            "chk_stages_category",
            "category IN ('open','won','lost')"));
        builder.ToTable(t => t.HasCheckConstraint(
            "chk_stages_probability",
            "probability BETWEEN 0 AND 100"));
    }
}
