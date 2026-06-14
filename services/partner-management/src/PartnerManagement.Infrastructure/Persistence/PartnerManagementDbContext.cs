using Microsoft.EntityFrameworkCore;
using PartnerManagement.Application.Ports;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Infrastructure.Idempotency;
using PartnerManagement.Infrastructure.Outbox;
using PartnerManagement.Infrastructure.Persistence.Configurations;

namespace PartnerManagement.Infrastructure.Persistence;

/// <summary>
/// DbContext principal do módulo partner-management.
/// Aplica filtro global de tenant (<c>HasQueryFilter</c>) em todas as entidades multi-tenant,
/// garantindo isolamento por <c>tenant_id</c> em toda query de leitura (DD-001, ADR-0001).
/// Mapeamentos via <see cref="IEntityTypeConfiguration{TEntity}"/> — sem lógica de domínio aqui.
/// Mapeia: design §6.1, design §7, RNF 1, DD-001, TASK-15.
/// </summary>
public sealed class PartnerManagementDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    // =========================================================================
    // DbSets (visíveis apenas à Infrastructure — sem vazamento para camadas superiores)
    // =========================================================================

    /// <summary>Parceiros do tenant corrente (filtro global aplicado).</summary>
    internal DbSet<Partner> Partners { get; set; } = null!;

    /// <summary>Mensagens de Outbox pendentes de publicação (filtro global aplicado).</summary>
    internal DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;

    /// <summary>Chaves de idempotência para operações do data-migration (filtro global aplicado).</summary>
    internal DbSet<IdempotencyKey> IdempotencyKeys { get; set; } = null!;

    // =========================================================================
    // Construtor
    // =========================================================================

    /// <summary>
    /// Inicializa o DbContext com as opções e o contexto de tenant.
    /// </summary>
    /// <param name="options">Opções do DbContext (connection string, provedor Npgsql).</param>
    /// <param name="tenantContext">Contexto do tenant corrente (filtro global).</param>
    public PartnerManagementDbContext(
        DbContextOptions<PartnerManagementDbContext> options,
        ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    // =========================================================================
    // Configuração do modelo
    // =========================================================================

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplica as configurações explícitas de cada entidade (IEntityTypeConfiguration<T>)
        modelBuilder.ApplyConfiguration(new PartnerConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new IdempotencyKeyConfiguration());

        // =====================================================================
        // Filtro global de tenant (HasQueryFilter) — DD-001, ADR-0001
        // A expressão captura _tenantContext (interface) — avaliada em runtime (por scoped DI).
        // Isso garante que o filtro usa o tenant da requisição corrente, não um valor fixo.
        // A segunda camada de defesa é a RLS no PostgreSQL (TASK-16, TASK-17).
        // =====================================================================
        modelBuilder.Entity<Partner>()
            .HasQueryFilter(p => p.TenantId == _tenantContext.CurrentTenantId);

        modelBuilder.Entity<OutboxMessage>()
            .HasQueryFilter(o => o.TenantId == _tenantContext.CurrentTenantId);

        modelBuilder.Entity<IdempotencyKey>()
            .HasQueryFilter(k => k.TenantId == _tenantContext.CurrentTenantId);
    }
}
