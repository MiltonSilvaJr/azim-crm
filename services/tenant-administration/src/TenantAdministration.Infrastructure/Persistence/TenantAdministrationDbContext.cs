using Microsoft.EntityFrameworkCore;
using TenantAdministration.Domain.Aggregates;
using TenantAdministration.Domain.Entities;
using TenantAdministration.Infrastructure.Outbox;
using TenantAdministration.Infrastructure.Persistence.Configurations;

namespace TenantAdministration.Infrastructure.Persistence;

/// <summary>
/// DbContext do módulo Tenant Administration.
/// Implementa:
/// - Mapeamento completo de <see cref="Tenant"/> e <see cref="TenantBranding"/>
/// - Global query filter por <c>tenant_id</c> para <c>tenant_brandings</c> (Camada 1 de RLS — design.md §14, ADR-0001)
/// - Tabela Outbox (<c>outbox_events</c>) para padrão transacional (design.md §6.6)
/// - Tabela de idempotência (<c>tenant_provisioning_requests</c>) (design.md §7.3)
///
/// NOTA DE REVISÃO OBRIGATÓRIA (RNF 1.3): qualquer migration que toque
/// <c>tenant_brandings</c>, <c>tenant_id</c> ou a policy RLS deve passar por
/// revisão de código explícita antes de ser aplicada em staging/produção.
/// </summary>
public sealed class TenantAdministrationDbContext : DbContext
{
    private readonly Guid? _currentTenantId;

    /// <param name="options">Opções de configuração do EF Core.</param>
    /// <param name="currentTenantId">
    /// ID do tenant corrente para o global query filter. Nulo no plano de plataforma (DD-001).
    /// </param>
    public TenantAdministrationDbContext(
        DbContextOptions<TenantAdministrationDbContext> options,
        Guid? currentTenantId = null)
        : base(options)
    {
        _currentTenantId = currentTenantId;
    }

    /// <summary>Tenants gerenciados pela plataforma.</summary>
    public DbSet<Tenant> Tenants => Set<Tenant>();

    /// <summary>Brandings dos tenants. Protegido por RLS e global query filter.</summary>
    public DbSet<TenantBranding> TenantBrandings => Set<TenantBranding>();

    /// <summary>Eventos pendentes de publicação no Pub/Sub (Outbox).</summary>
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();

    /// <summary>Registro de idempotência do provisionamento.</summary>
    public DbSet<TenantProvisioningRequest> ProvisioningRequests => Set<TenantProvisioningRequest>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new TenantConfiguration());
        modelBuilder.ApplyConfiguration(new TenantBrandingConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxEventConfiguration());
        modelBuilder.ApplyConfiguration(new TenantProvisioningRequestConfiguration());

        // ──────────────────────────────────────────────────────────────────────
        // Global Query Filter — Camada 1 de defesa em profundidade (design.md §14, ADR-0001)
        // Filtra tenant_brandings pelo tenant_id corrente.
        // Quando _currentTenantId é nulo (plano de plataforma), o filtro não
        // retorna registros de branding — PlatOp não acessa dados comerciais (RNF 7).
        // ──────────────────────────────────────────────────────────────────────
        // Usar Guid? no EF.Property pois shadow property TenantId é Guid? (compatibilidade LEFT JOIN)
        modelBuilder.Entity<TenantBranding>()
            .HasQueryFilter(b => _currentTenantId != null
                && EF.Property<Guid?>(b, "TenantId") == _currentTenantId);
    }
}
