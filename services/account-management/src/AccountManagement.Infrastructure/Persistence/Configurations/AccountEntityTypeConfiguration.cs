using AccountManagement.Domain.Accounts;
using AccountManagement.Domain.Accounts.Services;
using AccountManagement.Domain.Accounts.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core para o agregado <see cref="Account"/> (tabela <c>accounts</c>).
///
/// Convenções de nomenclatura física: snake_case (rule database-naming.md).
/// Sem <c>bu_id</c> — conta é compartilhada por todo o tenant (Req 2.3).
/// Índice <c>idx_accounts_tenant_normalized_name</c> não-unique (DD-006).
///
/// Mapeia: design §7, DD-002, DD-006, RNF 7.1, TASK-08.
/// </summary>
internal sealed class AccountEntityTypeConfiguration : IEntityTypeConfiguration<Account>
{
    private static readonly NameNormalizer _normalizer = new();

    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts",
            t => t.HasCheckConstraint(
                "chk_accounts_name_not_blank",
                "length(btrim(name)) > 0"));

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(a => a.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        // AccountName — value object: persistido como string
        builder.Property(a => a.Name)
            .HasColumnName("name")
            .HasMaxLength(AccountName.MaxLength)
            .IsRequired()
            .HasConversion(
                name => name.Value,
                value => AccountName.Create(value));

        // NormalizedName — value object: persistido como string
        builder.Property(a => a.NormalizedName)
            .HasColumnName("normalized_name")
            .IsRequired()
            .HasConversion(
                nn => nn.Value,
                value => _normalizer.NormalizeName(value));

        builder.Property(a => a.Website)
            .HasColumnName("website");

        builder.Property(a => a.Notes)
            .HasColumnName("notes");

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(a => a.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Navegação para contatos — sem cascade delete (anonimização in-place, DD-001)
        // Usa metadata para mapear backing field privado _contacts (IReadOnlyList não suportado diretamente)
        builder.Metadata.FindNavigation(nameof(Account.Contacts))!
            .SetField("_contacts");

        builder.HasMany(a => a.Contacts)
            .WithOne()
            .HasForeignKey("account_id")
            .OnDelete(DeleteBehavior.Restrict);

        // Índice de dedupe/busca por nome normalizado — NÃO unique (DD-006, RNF 7.1)
        builder.HasIndex(a => new { a.TenantId, a.NormalizedName })
            .HasDatabaseName("idx_accounts_tenant_normalized_name")
            .IsUnique(false);

        // Ignorar coleção de domain events — não persistida no EF Core
        builder.Ignore(a => a.DomainEvents);
    }
}
