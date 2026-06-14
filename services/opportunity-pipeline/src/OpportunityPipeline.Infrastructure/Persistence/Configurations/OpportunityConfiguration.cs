using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpportunityPipeline.Domain.Opportunities;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core para o aggregate root Opportunity.
/// Mapeamento snake_case, colunas geradas STORED, constraints e índices.
/// Mapeia: design §7.1, TASK-13.
/// </summary>
internal sealed class OpportunityConfiguration : IEntityTypeConfiguration<Opportunity>
{
    public void Configure(EntityTypeBuilder<Opportunity> builder)
    {
        builder.ToTable("opportunities");

        // Chave primária
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        // Tenant (RLS)
        builder.Property(o => o.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        // Campos escalares
        builder.Property(o => o.BuId).HasColumnName("bu_id").IsRequired();
        builder.Property(o => o.AccountId).HasColumnName("account_id").IsRequired();
        builder.Property(o => o.OwnerId).HasColumnName("owner_id").IsRequired();
        builder.Property(o => o.PartnerId).HasColumnName("partner_id");
        builder.Property(o => o.Title).HasColumnName("title").IsRequired().HasMaxLength(500);
        builder.Property(o => o.ExpectedCloseDate).HasColumnName("data_fechamento_esperada");
        builder.Property(o => o.ClosedAt).HasColumnName("closed_at");
        builder.Property(o => o.Notes).HasColumnName("notes");
        builder.Property(o => o.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(o => o.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(o => o.IsStale).HasColumnName("is_stale").IsRequired().HasDefaultValue(false);

        // StageCategory como string "open"|"won"|"lost"
        builder.Property(o => o.StageCategory)
            .HasColumnName("stage_category")
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => (StageCategory)Enum.Parse(typeof(StageCategory), v, ignoreCase: true));

        // OpportunityNumber — objeto de valor mapeado como coluna varchar
        builder.Property(o => o.Number)
            .HasColumnName("opportunity_number")
            .IsRequired()
            .HasMaxLength(10)
            .HasConversion(
                v => v.Value,
                v => new OpportunityNumber(v));

        // StageRef — owned type mapeado para colunas da tabela
        builder.OwnsOne(o => o.Stage, stage =>
        {
            stage.Property(s => s.StageId).HasColumnName("stage_id").IsRequired();
            stage.Property(s => s.Name).HasColumnName("stage_name").IsRequired().HasMaxLength(100);
            stage.Property(s => s.Category)
                .HasColumnName("stage_category_ref")
                .HasMaxLength(20)
                .HasConversion(
                    v => v.ToString().ToLowerInvariant(),
                    v => (StageCategory)Enum.Parse(typeof(StageCategory), v, ignoreCase: true));
            stage.Property(s => s.DefaultProbability).HasColumnName("stage_default_probability");
            stage.Property(s => s.Order).HasColumnName("stage_order");
        });

        // OriginChannelRef — owned type
        builder.OwnsOne(o => o.OriginChannel, oc =>
        {
            oc.Property(c => c.OriginChannelId).HasColumnName("origin_channel_id").IsRequired();
            oc.Property(c => c.Name).HasColumnName("origin_channel_name").IsRequired().HasMaxLength(100);
            oc.Property(c => c.IsPartnerChannel).HasColumnName("origin_is_partner_channel");
        });

        // LossReasonRef — owned type (nullable)
        builder.OwnsOne(o => o.LossReason, lr =>
        {
            lr.Property(r => r.LossReasonId).HasColumnName("loss_reason_id");
            lr.Property(r => r.Name).HasColumnName("loss_reason_name").HasMaxLength(200);
        });

        // ContractValue — owned type (valor_setup, valor_mensal, duracao_meses em BigInt)
        builder.OwnsOne(o => o.ContractValue, cv =>
        {
            cv.Property(c => c.Setup)
                .HasColumnName("valor_setup")
                .IsRequired()
                .HasConversion(v => v.AmountInCents, v => new Money(v));
            cv.Property(c => c.Mensal)
                .HasColumnName("valor_mensal")
                .IsRequired()
                .HasConversion(v => v.AmountInCents, v => new Money(v));
            cv.Property(c => c.DuracaoMeses)
                .HasColumnName("duracao_meses")
                .IsRequired();
        });

        // Probability — owned type (smallint)
        builder.OwnsOne(o => o.Probability, p =>
        {
            p.Property(pr => pr.Value)
                .HasColumnName("probabilidade")
                .IsRequired();
        });

        // Unique constraint: tenant_id + opportunity_number (ADR-0003)
        builder.HasIndex(o => new { o.TenantId, o.Number })
            .HasDatabaseName("uq_opportunities_tenant_number")
            .IsUnique();

        // Índices de performance (design §7.1)
        builder.HasIndex(o => new { o.TenantId, o.BuId })
            .HasDatabaseName("idx_opportunities_tenant_bu_stage");

        builder.HasIndex(o => new { o.TenantId, o.OwnerId })
            .HasDatabaseName("idx_opportunities_tenant_owner");

        builder.HasIndex(o => new { o.TenantId, o.PartnerId })
            .HasDatabaseName("idx_opportunities_tenant_partner")
            .HasFilter("partner_id IS NOT NULL");

        builder.HasIndex(o => new { o.TenantId, o.ExpectedCloseDate })
            .HasDatabaseName("idx_opportunities_tenant_close");

        // DomainEvents: coleção em memória de eventos de domínio — não mapeada para o banco.
        // Ignorar explicitamente para que o EF Core não tente mapear DomainEvent como entidade.
        builder.Ignore(o => o.DomainEvents);

        // Navegações — coleções de entidades internas (owned via shadow FK)
        builder.HasMany(o => o.Transitions)
            .WithOne()
            .HasForeignKey("opportunity_id")
            .HasPrincipalKey(o => o.Id);

        builder.HasMany(o => o.Commissions)
            .WithOne()
            .HasForeignKey("opportunity_id")
            .HasPrincipalKey(o => o.Id);

        builder.HasMany(o => o.Contacts)
            .WithOne()
            .HasForeignKey("opportunity_id")
            .HasPrincipalKey(o => o.Id);

        // Check constraints
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("chk_opportunities_category",
                "stage_category IN ('open','won','lost')");
            t.HasCheckConstraint("chk_opportunities_prob",
                "probabilidade BETWEEN 0 AND 100");
            t.HasCheckConstraint("chk_opportunities_values_nonneg",
                "valor_setup >= 0 AND valor_mensal >= 0 AND duracao_meses >= 0");
        });
    }
}
