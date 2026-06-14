using GoalForecast.Domain.Aggregates;
using GoalForecast.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GoalForecast.Infrastructure.Persistence;

/// <summary>
/// DbContext EF Core do módulo goal-forecast. Mapeia o aggregate <see cref="Goal"/>
/// para a tabela <c>goals</c> com:
/// <list type="bullet">
///   <item><term>ValueConverter Money → BIGINT</term><description>Preserva centavos inteiros (RNF 4, DEC-011).</description></item>
///   <item><term>ValueConverter GoalPeriod</term><description>year/month como SMALLINT.</description></item>
///   <item><term>Owned type GoalScope</term><description>bu_id NOT NULL, owner_id nullable (DD-008).</description></item>
///   <item><term>Global Query Filter</term><description>tenant_id == _currentTenantId em todas as queries (ADR-0001).</description></item>
/// </list>
///
/// Mapeia: RNF 1, RNF 4, ADR-0001, DD-008, design §6.1, TASK-15.
/// </summary>
public sealed class GoalForecastDbContext : DbContext
{
    /// <summary>
    /// Tenant do principal autenticado. Injetado na construção; usado pelo Global Query Filter.
    /// Nunca vem de configuração estática — vem do contexto de autenticação (ADR-0001).
    /// </summary>
    private readonly Guid _currentTenantId;

    /// <summary>DbSet do aggregate Goal.</summary>
    public DbSet<Goal> Goals => Set<Goal>();

    public GoalForecastDbContext(DbContextOptions<GoalForecastDbContext> options, Guid currentTenantId)
        : base(options)
    {
        _currentTenantId = currentTenantId;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Conversor: Money ↔ long (BIGINT) ─────────────────────────────────
        // Nunca passa por double/float. Centavos inteiros preservados exatamente (PBT-05).
        var moneyConverter = new ValueConverter<Money, long>(
            money => money.Cents,
            cents => Money.Of(cents));

        // ── Conversor: GoalPeriod.Year ↔ short (SMALLINT) ────────────────────
        var yearConverter = new ValueConverter<int, short>(
            year => (short)year,
            s => (int)s);

        // ── Conversor: GoalPeriod.Month ↔ short (SMALLINT) ───────────────────
        var monthConverter = new ValueConverter<int, short>(
            month => (short)month,
            s => (int)s);

        modelBuilder.Entity<Goal>(entity =>
        {
            entity.ToTable("goals");

            // Chave primária
            entity.HasKey(g => g.Id);
            entity.Property(g => g.Id)
                .HasColumnName("id")
                .ValueGeneratedNever(); // UUID gerado no domínio

            // tenant_id NOT NULL (ADR-0001)
            entity.Property(g => g.TenantId)
                .HasColumnName("tenant_id")
                .IsRequired();

            // valor_meta BIGINT NOT NULL CHECK (valor_meta >= 0) — RNF 4, DEC-011
            entity.Property(g => g.ValorMeta)
                .HasColumnName("valor_meta")
                .HasConversion(moneyConverter)
                .IsRequired();

            // Timestamps de auditoria técnica
            entity.Property(g => g.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            entity.Property(g => g.UpdatedAt)
                .HasColumnName("updated_at")
                .IsRequired();

            // ── GoalScope como owned type ─────────────────────────────────────
            // bu_id NOT NULL (DD-008: divergência da data-model; bu sempre obrigatória)
            // owner_id nullable (escopo BU → null; escopo RESPONSAVEL → UUID)
            entity.OwnsOne(g => g.Scope, scope =>
            {
                scope.Property(s => s.BuId)
                    .HasColumnName("bu_id")
                    .IsRequired(); // DD-008: NOT NULL

                scope.Property(s => s.OwnerId)
                    .HasColumnName("owner_id");

                // Kind não é coluna — é derivado de OwnerId (Design §4.3)
                scope.Ignore(s => s.Kind);
            });

            // ── GoalPeriod como owned type ────────────────────────────────────
            // year SMALLINT NOT NULL, month SMALLINT NOT NULL CHECK (month BETWEEN 1 AND 12)
            entity.OwnsOne(g => g.Period, period =>
            {
                period.Property(p => p.Year)
                    .HasColumnName("year")
                    .HasConversion(yearConverter)
                    .IsRequired();

                period.Property(p => p.Month)
                    .HasColumnName("month")
                    .HasConversion(monthConverter)
                    .IsRequired();
            });

            // DomainEvents é coleção transiente — não mapeada para o banco
            entity.Ignore(g => g.DomainEvents);

            // ── Índices de consulta (design §7) ──────────────────────────────
            entity.HasIndex(g => new { g.TenantId })
                .HasDatabaseName("ix_goals_tenant");

            // ── Global Query Filter — primeira camada de isolamento (ADR-0001) ──
            // Filtro aplicado em TODAS as queries pelo EF Core.
            // Segunda camada: RLS no Postgres (TASK-16).
            entity.HasQueryFilter(g => g.TenantId == _currentTenantId);
        });
    }
}
