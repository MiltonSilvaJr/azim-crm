using System.Text.Json;
using AccountManagement.Domain.Accounts;
using AccountManagement.Domain.Accounts.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core para a entidade <see cref="Contact"/> (tabela <c>contacts</c>).
///
/// Convenções de nomenclatura física: snake_case (rule database-naming.md).
///
/// <see cref="Contact.Info"/> (<see cref="ContactInfo"/>) é um value object que encapsula
/// nome, e-mail e telefone (PII). O mapeamento persiste cada campo em sua coluna dedicada
/// usando shadow properties para email e phone (EF Core não acessa diretamente as propriedades
/// internas do value object).
///
/// Ao reconstituter o Contact via repositório, as propriedades de PII são injetadas
/// através de <see cref="Contact.Reconstitute"/>.
///
/// Mapeia: design §7, Req 5.2, RNF 1, RNF 2, DD-001, TASK-08.
/// </summary>
internal sealed class ContactEntityTypeConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("contacts",
            t => t.HasCheckConstraint(
                "chk_contacts_privacy_state",
                "privacy_state IN ('active','anonymized')"));

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(c => c.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(c => c.AccountId)
            .HasColumnName("account_id")
            .IsRequired();

        // ContactInfo.Name — PII: coluna "name" (Req 5.2, RN-025)
        // Persiste apenas o nome (campo obrigatório do ContactInfo).
        // Email e phone são persistidos em shadow properties abaixo.
        builder.Property(c => c.Info)
            .HasColumnName("name")
            .IsRequired()
            .HasConversion(
                info => info.Name,
                name => ContactInfo.Create(name));

        // Shadow property: email (PII — coluna separada)
        builder.Property<string?>("Email")
            .HasColumnName("email");

        // Shadow property: phone (PII — coluna separada)
        builder.Property<string?>("Phone")
            .HasColumnName("phone");

        builder.Property(c => c.Role)
            .HasColumnName("role");

        // ContactPrivacyState — convertido via PersistenceValue (active/anonymized)
        builder.Property(c => c.PrivacyState)
            .HasColumnName("privacy_state")
            .HasMaxLength(16)
            .IsRequired()
            .HasConversion(
                state => state.PersistenceValue,
                value => ContactPrivacyState.FromPersistenceValue(value));

        builder.Property(c => c.ForgottenAt)
            .HasColumnName("forgotten_at");

        builder.Property(c => c.ForgottenBy)
            .HasColumnName("forgotten_by");

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Índice de busca por tenant e conta
        builder.HasIndex(c => new { c.TenantId, c.AccountId })
            .HasDatabaseName("idx_contacts_tenant_account");
    }
}
