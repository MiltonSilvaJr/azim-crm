using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpportunityPipeline.Domain.Opportunities.Entities;

namespace OpportunityPipeline.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core para OpportunityContactLink.
/// Índice parcial único para garantir exatamente 1 contato principal (INV-11).
/// Mapeia: design §7.4, TASK-13.
/// </summary>
internal sealed class OpportunityContactLinkConfiguration : IEntityTypeConfiguration<OpportunityContactLink>
{
    public void Configure(EntityTypeBuilder<OpportunityContactLink> builder)
    {
        builder.ToTable("opportunity_contacts");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(c => c.OpportunityId).HasColumnName("opportunity_id").IsRequired();
        builder.Property(c => c.ContactId).HasColumnName("contact_id").IsRequired();
        builder.Property(c => c.IsPrimary).HasColumnName("is_primary").IsRequired().HasDefaultValue(false);
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();

        // Unique: um contato por oportunidade
        builder.HasIndex(c => new { c.OpportunityId, c.ContactId })
            .HasDatabaseName("uq_opportunity_contacts_link")
            .IsUnique();

        // Índice parcial único: somente 1 principal por oportunidade (INV-11)
        builder.HasIndex(c => c.OpportunityId)
            .HasDatabaseName("uq_opportunity_contacts_primary")
            .IsUnique()
            .HasFilter("is_primary = TRUE");
    }
}
