using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpportunityPipeline.Domain.Opportunities.Entities;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core para OpportunityPartnerCommission.
/// Constraint uq_partner_commission_active_snapshot (INV-12, RNF 5).
/// Trigger trg_block_snapshot_mutation aplicado em migration manual (TASK-14).
/// Mapeia: design §7.3, TASK-13.
/// </summary>
internal sealed class OpportunityPartnerCommissionConfiguration : IEntityTypeConfiguration<OpportunityPartnerCommission>
{
    public void Configure(EntityTypeBuilder<OpportunityPartnerCommission> builder)
    {
        builder.ToTable("opportunity_partner_commissions");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(c => c.OpportunityId).HasColumnName("opportunity_id").IsRequired();
        builder.Property(c => c.PartnerId).HasColumnName("partner_id").IsRequired();
        builder.Property(c => c.IsSnapshot).HasColumnName("is_snapshot").IsRequired().HasDefaultValue(false);
        builder.Property(c => c.SnapshotAt).HasColumnName("snapshot_at");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();

        // Moeda da comissão — herdada da oportunidade (ADR-0008)
        builder.Property(c => c.Currency)
            .HasColumnName("currency")
            .IsRequired()
            .HasMaxLength(3)
            .HasDefaultValue("BRL");

        // CommissionTerms — owned type
        builder.OwnsOne(c => c.Terms, terms =>
        {
            terms.Property(t => t.Role)
                .HasColumnName("role")
                .IsRequired()
                .HasMaxLength(20)
                .HasConversion(
                    v => v.ToString(),
                    v => (CommissionRole)Enum.Parse(typeof(CommissionRole), v));

            terms.Property(t => t.PctSetup)
                .HasColumnName("pct_setup")
                .IsRequired()
                .HasColumnType("NUMERIC(5,2)");

            terms.Property(t => t.PctRecorrente)
                .HasColumnName("pct_recorrente")
                .IsRequired()
                .HasColumnType("NUMERIC(5,2)");

            terms.Property(t => t.ValorFixo)
                .HasColumnName("valor_fixo")
                .HasConversion(
                    v => v != null ? v.AmountInCents : (long?)null,
                    v => v.HasValue ? new Money(v.Value, "BRL") : null);

            terms.Property(t => t.MesesComissionados)
                .HasColumnName("meses_comissionados")
                .IsRequired();
        });

        // CommissionCalculation — owned type
        // Os conversores usam "BRL" como placeholder; CommissionCurrencyMaterializationInterceptor
        // recria os Money com a currency real de OpportunityPartnerCommission.Currency (ADR-0008).
        builder.OwnsOne(c => c.Calculation, calc =>
        {
            calc.Property(r => r.ComissaoSetup)
                .HasColumnName("comissao_setup_cents")
                .IsRequired()
                .HasConversion(v => v.AmountInCents, v => new Money(v, "BRL"));

            calc.Property(r => r.ComissaoRecorrente)
                .HasColumnName("comissao_recorrente_cents")
                .IsRequired()
                .HasConversion(v => v.AmountInCents, v => new Money(v, "BRL"));

            calc.Property(r => r.ComissaoTotal)
                .HasColumnName("comissao_calculada")
                .IsRequired()
                .HasConversion(v => v.AmountInCents, v => new Money(v, "BRL"));
        });

        // Índices
        builder.HasIndex(c => new { c.TenantId, c.OpportunityId })
            .HasDatabaseName("idx_commission_tenant_opp");

        builder.HasIndex(c => new { c.TenantId, c.PartnerId })
            .HasDatabaseName("idx_commission_tenant_partner");

        // Unique constraint: no máximo 1 snapshot por oportunidade (INV-12, RNF 5)
        // Aplicado como índice parcial único WHERE is_snapshot = TRUE
        // (migration manual — não suportado diretamente pelo EF sem SQL raw)
        builder.HasIndex(c => c.OpportunityId)
            .HasDatabaseName("uq_partner_commission_active_snapshot")
            .IsUnique()
            .HasFilter("is_snapshot = TRUE");

        // Check constraints
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("chk_commission_role",
                "role IN ('Indicador','Revendedor','Distribuidor','Integrador')");
            t.HasCheckConstraint("chk_commission_nonneg",
                "valor_fixo >= 0 AND comissao_calculada >= 0 AND meses_comissionados >= 0");
        });
    }
}
