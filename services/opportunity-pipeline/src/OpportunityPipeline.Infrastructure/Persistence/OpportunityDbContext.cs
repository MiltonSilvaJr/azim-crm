using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OpportunityPipeline.Application.Behaviors;
using OpportunityPipeline.Application.Common;
using OpportunityPipeline.Application.Opportunities.Queries;
using OpportunityPipeline.Domain.Opportunities;
using OpportunityPipeline.Domain.Opportunities.Entities;
using OpportunityPipeline.Domain.Opportunities.Events;
using OpportunityPipeline.Domain.Opportunities.Ports;
using OpportunityPipeline.Infrastructure.Outbox;

namespace OpportunityPipeline.Infrastructure.Persistence;

/// <summary>
/// DbContext principal do módulo opportunity-pipeline.
/// Configura mapeamentos snake_case, Global Query Filter por tenant_id (ADR-0001 camada 2)
/// e implementa IUnitOfWork para coordenar transação, Outbox e auditoria.
/// Mapeia: design §6.1, TASK-13, ADR-0001.
/// </summary>
public sealed class OpportunityDbContext : DbContext, IUnitOfWork
{
    private readonly TenantContext _tenantContext;
    private IDbContextTransaction? _currentTransaction;

    // Dados de auditoria pendente (preenchido pelo handler, persistido no commit)
    private Guid _auditAggregateId;
    private string _auditAggregateType = string.Empty;
    private object? _auditDelta;
    private bool _hasPendingAudit;

    // Eventos de domínio acumulados pelo handler
    private readonly List<DomainEvent> _pendingDomainEvents = [];

    public OpportunityDbContext(
        DbContextOptions<OpportunityDbContext> options,
        TenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    // =========================================================================
    // DbSets
    // =========================================================================

    /// <summary>Oportunidades comerciais (aggregate root).</summary>
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();

    /// <summary>Transições de estágio (append-only, RNF 7).</summary>
    public DbSet<OpportunityStageTransition> StageTransitions => Set<OpportunityStageTransition>();

    /// <summary>Comissões de parceiro (projetada + snapshot imutável).</summary>
    public DbSet<OpportunityPartnerCommission> PartnerCommissions => Set<OpportunityPartnerCommission>();

    /// <summary>Contatos vinculados à oportunidade.</summary>
    public DbSet<OpportunityContactLink> ContactLinks => Set<OpportunityContactLink>();

    /// <summary>Sequências de numeração atômica por tenant (DD-001).</summary>
    public DbSet<OpportunityNumberSequence> NumberSequences => Set<OpportunityNumberSequence>();

    /// <summary>Registros de detecção de estagnação (idempotência RNF 9, PBT-09).</summary>
    public DbSet<StaleDetectionRun> StaleDetectionRuns => Set<StaleDetectionRun>();

    /// <summary>Filtros salvos pelo usuário.</summary>
    public DbSet<SavedFilterEntity> SavedFilters => Set<SavedFilterEntity>();

    /// <summary>Eventos do Outbox (ADR-0004).</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    // =========================================================================
    // Configuração de modelo
    // =========================================================================

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplica todas as configurações de entidade desta camada
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OpportunityDbContext).Assembly);

        // Global Query Filter por tenant_id — ADR-0001 camada 2
        // Garante que todas as queries filtram automaticamente pelo tenant corrente.
        // IMPORTANTE: captura _tenantContext (referência) e não TenantId (valor) para que
        // o filtro avalie o TenantId corrente de CADA instância de DbContext em tempo de execução.
        // Capturar o valor diretamente causaria congelamento na primeira instância criada
        // quando o modelo EF Core é cacheado entre contextos (bug de isolamento multi-tenant).
        modelBuilder.Entity<Opportunity>()
            .HasQueryFilter(o => o.TenantId == _tenantContext.TenantId);

        modelBuilder.Entity<OpportunityStageTransition>()
            .HasQueryFilter(t => t.TenantId == _tenantContext.TenantId);

        modelBuilder.Entity<OpportunityPartnerCommission>()
            .HasQueryFilter(c => c.TenantId == _tenantContext.TenantId);

        modelBuilder.Entity<OpportunityContactLink>()
            .HasQueryFilter(cl => cl.TenantId == _tenantContext.TenantId);

        modelBuilder.Entity<OpportunityNumberSequence>()
            .HasQueryFilter(s => s.TenantId == _tenantContext.TenantId);

        modelBuilder.Entity<StaleDetectionRun>()
            .HasQueryFilter(r => r.TenantId == _tenantContext.TenantId);

        modelBuilder.Entity<SavedFilterEntity>()
            .HasQueryFilter(f => f.TenantId == _tenantContext.TenantId);

        modelBuilder.Entity<OutboxMessage>()
            .HasQueryFilter(m => m.TenantId == _tenantContext.TenantId);
    }

    // =========================================================================
    // IUnitOfWork — coordenação de transação, eventos e auditoria
    // =========================================================================

    /// <inheritdoc/>
    public IReadOnlyList<DomainEvent> PendingDomainEvents => _pendingDomainEvents.AsReadOnly();

    /// <inheritdoc/>
    public void AddDomainEvent(DomainEvent domainEvent)
        => _pendingDomainEvents.Add(domainEvent);

    /// <inheritdoc/>
    public void AddDomainEvents(IEnumerable<DomainEvent> domainEvents)
        => _pendingDomainEvents.AddRange(domainEvents);

    /// <inheritdoc/>
    public bool HasPendingAudit => _hasPendingAudit;

    /// <inheritdoc/>
    public Guid AuditAggregateId => _auditAggregateId;

    /// <inheritdoc/>
    public string AuditAggregateType => _auditAggregateType;

    /// <inheritdoc/>
    public object? AuditDelta => _auditDelta;

    /// <inheritdoc/>
    public void RegisterAudit(Guid aggregateId, string aggregateType, object? delta)
    {
        _auditAggregateId = aggregateId;
        _auditAggregateType = aggregateType;
        _auditDelta = delta;
        _hasPendingAudit = true;
    }

    /// <inheritdoc/>
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is not null)
            return; // já em transação — idempotente

        _currentTransaction = await Database.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (_currentTransaction is not null)
        {
            await _currentTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            await _currentTransaction.DisposeAsync().ConfigureAwait(false);
            _currentTransaction = null;
        }

        // Limpa estado após commit
        _pendingDomainEvents.Clear();
        _hasPendingAudit = false;
    }

    /// <inheritdoc/>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is not null)
        {
            await _currentTransaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            await _currentTransaction.DisposeAsync().ConfigureAwait(false);
            _currentTransaction = null;
        }

        _pendingDomainEvents.Clear();
        _hasPendingAudit = false;
    }
}
