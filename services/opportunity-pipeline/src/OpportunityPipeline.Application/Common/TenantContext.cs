namespace OpportunityPipeline.Application.Common;

/// <summary>
/// Contexto de tenant resolvido do JWT pelo TenantBehavior.
/// Propagado a todas as camadas durante o ciclo de vida da request.
/// Mapeia: ADR-0001 (isolamento multi-tenant), RNF 3, design §5.4.
/// </summary>
public sealed class TenantContext
{
    private Guid? _tenantId;
    private Guid? _buId;
    private Guid? _actorId;

    /// <summary>Tenant ID resolvido do JWT.</summary>
    public Guid TenantId => _tenantId
        ?? throw new InvalidOperationException("TenantContext não foi inicializado. TenantBehavior deve ser executado antes de acessar TenantId.");

    /// <summary>Business Unit ID resolvido do JWT.</summary>
    public Guid BuId => _buId
        ?? throw new InvalidOperationException("TenantContext não foi inicializado. TenantBehavior deve ser executado antes de acessar BuId.");

    /// <summary>Actor (usuário autenticado) ID resolvido do JWT.</summary>
    public Guid ActorId => _actorId
        ?? throw new InvalidOperationException("TenantContext não foi inicializado. TenantBehavior deve ser executado antes de acessar ActorId.");

    /// <summary>Indica se o contexto já foi inicializado.</summary>
    public bool IsInitialized => _tenantId.HasValue;

    /// <summary>
    /// Inicializa o contexto de tenant. Chamado exclusivamente pelo TenantBehavior.
    /// </summary>
    public void Initialize(Guid tenantId, Guid buId, Guid actorId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId não pode ser vazio.", nameof(tenantId));
        if (buId == Guid.Empty)
            throw new ArgumentException("buId não pode ser vazio.", nameof(buId));
        if (actorId == Guid.Empty)
            throw new ArgumentException("actorId não pode ser vazio.", nameof(actorId));

        _tenantId = tenantId;
        _buId = buId;
        _actorId = actorId;
    }
}
