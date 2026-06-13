using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Organization.Domain.Aggregates;
using Organization.Domain.ValueObjects;

namespace Organization.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core da entidade interna <see cref="UserMembership"/> (pertence a <see cref="User"/>).
/// </summary>
internal sealed class UserMembershipConfiguration : IEntityTypeConfiguration<UserMembership>
{
    public void Configure(EntityTypeBuilder<UserMembership> builder)
    {
        builder.ToTable("user_memberships");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");

        builder.Property<Guid>("TenantId").HasColumnName("tenant_id").IsRequired();
        builder.Property<Guid>("UserId").HasColumnName("user_id").IsRequired();

        builder.Property(m => m.BuId)
            .HasColumnName("bu_id")
            .IsRequired();

        builder.Property(m => m.Role)
            .HasColumnName("role")
            .HasMaxLength(30)
            .IsRequired()
            .HasConversion(
                r => r.Value,
                v => Role.Create(v));

        builder.Property<DateTimeOffset>("CreatedAt")
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Unicidade: um membership por (tenant_id, user_id, bu_id)
        builder.HasIndex("TenantId", "UserId", nameof(UserMembership.BuId))
            .IsUnique()
            .HasDatabaseName("uq_user_memberships_tenant_user_bu");

        // Índice para RBAC context queries
        builder.HasIndex("TenantId", "UserId")
            .HasDatabaseName("idx_user_memberships_tenant_user");

        // CHECK constraint do papel
        builder.ToTable(t => t.HasCheckConstraint(
            "chk_user_memberships_role",
            "role IN ('TAdmin','GestorBU','Vendedor','Viewer')"));
    }
}
