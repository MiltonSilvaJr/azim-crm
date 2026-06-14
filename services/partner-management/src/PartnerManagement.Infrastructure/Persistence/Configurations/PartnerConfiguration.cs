using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Domain.Partners.ValueObjects;

namespace PartnerManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core para o agregado <see cref="Partner"/>.
/// Mapeia owned types (<c>CommissionDefaults</c>, <c>PartnerRole</c>, <c>PartnerContact</c>) como colunas inline
/// com nomes em <c>snake_case</c> (rule database-naming.md).
/// CHECKs de integridade são aplicados via migration (design §7).
/// Mapeia: design §6.1, design §7, RNF 1, DD-001, TASK-15.
/// </summary>
internal sealed class PartnerConfiguration : IEntityTypeConfiguration<Partner>
{
    // =========================================================================
    // Nomes físicos de coluna (snake_case — rule database-naming.md)
    // =========================================================================
    internal const string TableName = "partners";
    internal const string ColId = "id";
    internal const string ColTenantId = "tenant_id";
    internal const string ColName = "name";
    internal const string ColPartnerType = "partner_type";
    internal const string ColPctSetup = "pct_setup";
    internal const string ColPctRecorrente = "pct_recorrente";
    internal const string ColContactEmail = "contact_email";
    internal const string ColContactPhone = "contact_phone";
    internal const string ColNotes = "notes";
    internal const string ColActive = "active";
    internal const string ColCreatedAt = "created_at";
    internal const string ColUpdatedAt = "updated_at";
    internal const string ColCreatedBy = "created_by";
    internal const string ColUpdatedBy = "updated_by";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Partner> builder)
    {
        builder.ToTable(TableName);

        // Chave primária
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasColumnName(ColId)
            .ValueGeneratedNever(); // UUID gerado pelo domínio

        // Tenant (RNF 1, DD-001)
        builder.Property(p => p.TenantId)
            .HasColumnName(ColTenantId)
            .IsRequired();

        // PartnerName (possível PII — DD-008)
        builder.Property(p => p.Name)
            .HasColumnName(ColName)
            .IsRequired()
            .HasMaxLength(255)
            .HasConversion(
                name => name.Value,
                value => PartnerName.Create(value));

        // PartnerRole — owned type inline como texto (Req 5, DD-005)
        builder.Property(p => p.Role)
            .HasColumnName(ColPartnerType)
            .IsRequired()
            .HasConversion(
                role => role.Value,
                value => PartnerRole.Create(value));

        // CommissionDefaults — owned type inline (pct_setup, pct_recorrente) (Req 6, DD-004)
        builder.OwnsOne(p => p.CommissionDefaults, cd =>
        {
            cd.Property(c => c.PctSetup)
                .HasColumnName(ColPctSetup)
                .IsRequired()
                .HasPrecision(5, 2)
                .HasConversion(
                    pct => pct.Value,
                    value => Percentage.Create(value));

            cd.Property(c => c.PctRecorrente)
                .HasColumnName(ColPctRecorrente)
                .IsRequired()
                .HasPrecision(5, 2)
                .HasConversion(
                    pct => pct.Value,
                    value => Percentage.Create(value));
        });

        // PartnerContact — armazenado como colunas diretas em partners (contact_email, contact_phone)
        // Usa shadow properties no Partner porque PartnerContact é imutável (owned type com VOs complexos).
        // Rehidratação: o repositório usa Partner.Reconstitute passando PartnerContact.Create.
        builder.Ignore(p => p.Contact);

        builder.Property<string?>("ContactEmail")
            .HasColumnName(ColContactEmail);

        builder.Property<string?>("ContactPhone")
            .HasColumnName(ColContactPhone);

        // Notes
        builder.Property(p => p.Notes)
            .HasColumnName(ColNotes);

        // PartnerStatus como coluna booleana 'active' (design §7, Req 3)
        builder.Property(p => p.Status)
            .HasColumnName(ColActive)
            .IsRequired()
            .HasConversion(
                status => status == PartnerStatus.Active,
                active => active ? PartnerStatus.Active : PartnerStatus.Inactive);

        // Auditoria temporal
        builder.Property(p => p.CreatedAt)
            .HasColumnName(ColCreatedAt)
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .HasColumnName(ColUpdatedAt);

        builder.Property(p => p.CreatedBy)
            .HasColumnName(ColCreatedBy)
            .IsRequired();

        builder.Property(p => p.UpdatedBy)
            .HasColumnName(ColUpdatedBy);
    }
}
