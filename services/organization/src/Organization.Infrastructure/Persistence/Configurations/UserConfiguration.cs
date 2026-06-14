using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Organization.Domain.Aggregates;

namespace Organization.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core do agregado <see cref="User"/>.
/// PII: email e display_name armazenados mas nunca expostos em logs ou eventos (RNF 3).
/// </summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id");

        builder.Property(u => u.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(u => u.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(u => u.IdentityUid)
            .HasColumnName("identity_uid")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(u => u.Active)
            .HasColumnName("active")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(u => u.DeactivatedAt)
            .HasColumnName("deactivated_at")
            .IsRequired(false);

        // E-mail único por tenant
        builder.HasIndex(u => new { u.TenantId, u.Email })
            .IsUnique()
            .HasDatabaseName("uq_users_tenant_id_email");

        // Memberships via campo privado _memberships
        builder.HasMany(u => u.Memberships)
            .WithOne()
            .HasForeignKey("UserId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(u => u.Memberships)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_memberships");
    }
}
