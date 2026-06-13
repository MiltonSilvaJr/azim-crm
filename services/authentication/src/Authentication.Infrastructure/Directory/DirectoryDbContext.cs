using Authentication.Infrastructure.Directory.Entities;
using Microsoft.EntityFrameworkCore;

namespace Authentication.Infrastructure.Directory;

/// <summary>
/// DbContext de leitura para as tabelas de diretório consumidas pelo módulo authentication.
///
/// DÍVIDA TÉCNICA (TASK-14):
/// As tabelas <c>tenants</c>, <c>users</c> e <c>user_memberships</c> são de
/// propriedade dos módulos <c>tenant-administration</c> e <c>organization</c>
/// (design.md § 7). Este DbContext define apenas as entidades de leitura
/// necessárias para o authentication, sem migrations próprias.
///
/// Em produção, o schema é criado e mantido pelos módulos donos.
/// Para testes (TASK-14), o schema é criado via EnsureCreated() no Testcontainers.
/// Esta abordagem é uma dívida técnica: a integração real depende dos módulos
/// de organização/tenant-administration estarem disponíveis.
///
/// Mapeia: design.md § 6.1, § 7, DD-001, TASK-14.
/// </summary>
public sealed class DirectoryDbContext : DbContext
{
    public DirectoryDbContext(DbContextOptions<DirectoryDbContext> options)
        : base(options) { }

    /// <summary>Tabela de tenants (módulo tenant-administration).</summary>
    public DbSet<TenantEntity> Tenants => Set<TenantEntity>();

    /// <summary>Tabela de usuários (módulo organization).</summary>
    public DbSet<UserEntity> Users => Set<UserEntity>();

    /// <summary>Tabela de memberships (módulo organization).</summary>
    public DbSet<UserMembershipEntity> UserMemberships => Set<UserMembershipEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantEntity>(entity =>
        {
            entity.ToTable("tenants");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Id).HasColumnName("id");
            entity.Property(t => t.Slug).HasColumnName("slug").HasMaxLength(100).IsRequired();
            entity.Property(t => t.IdentityTenantId).HasColumnName("identity_tenant_id").HasMaxLength(200).IsRequired();
            entity.Property(t => t.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
            entity.HasIndex(t => t.Slug).IsUnique();
        });

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Id).HasColumnName("id");
            entity.Property(u => u.TenantId).HasColumnName("tenant_id");
            entity.Property(u => u.ProviderUserRef).HasColumnName("provider_user_ref").HasMaxLength(200).IsRequired();
            entity.Property(u => u.Email).HasColumnName("email").HasMaxLength(254).IsRequired();
            entity.Property(u => u.AuthMethod).HasColumnName("auth_method").HasMaxLength(50).IsRequired();
            entity.Property(u => u.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
            // RNF 1.1: Nenhuma coluna de senha em users
            entity.HasIndex(e => new { e.TenantId, e.ProviderUserRef }).IsUnique();
        });

        modelBuilder.Entity<UserMembershipEntity>(entity =>
        {
            entity.ToTable("user_memberships");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Id).HasColumnName("id");
            entity.Property(m => m.TenantId).HasColumnName("tenant_id");
            entity.Property(m => m.UserId).HasColumnName("user_id");
            entity.Property(m => m.BuId).HasColumnName("bu_id");
            entity.Property(m => m.Role).HasColumnName("role").HasMaxLength(100).IsRequired();
        });
    }
}
