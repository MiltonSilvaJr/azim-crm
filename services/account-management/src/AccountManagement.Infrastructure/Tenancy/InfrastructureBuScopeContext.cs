using AccountManagement.Application.Behaviors;

namespace AccountManagement.Infrastructure.Tenancy;

/// <summary>
/// Adaptador de Infrastructure que expõe o <see cref="BuScopeContext"/> da Application
/// para o <see cref="AccountManagementDbContext"/> e o filtro global EF Core de BU.
///
/// Análogo a <see cref="InfrastructureTenantContext"/>. Resolvido como serviço scoped —
/// um por request HTTP.
///
/// Mapeia: ADR-0009, design §6.1, design §14, VAL-ACC-03.
/// </summary>
public sealed class InfrastructureBuScopeContext
{
    private readonly BuScopeContext _buScopeContext;

    /// <summary>Inicializa com o <see cref="BuScopeContext"/> da Application.</summary>
    public InfrastructureBuScopeContext(BuScopeContext buScopeContext)
    {
        _buScopeContext = buScopeContext;
    }

    /// <summary>
    /// Conjunto de BU ids do usuário autenticado.
    /// Retorna coleção vazia quando o contexto não foi inicializado
    /// (tooling/migrations — nunca em runtime com requests reais).
    /// </summary>
    public IReadOnlyCollection<Guid> BuIds =>
        _buScopeContext.BuIds ?? Array.Empty<Guid>();

    /// <summary>
    /// Indica se o usuário tem visão tenant-wide (bypassa filtro de BU).
    /// Retorna <c>false</c> quando o contexto não foi inicializado (fail-closed).
    /// </summary>
    public bool IsTenantWide => _buScopeContext.IsTenantWide ?? false;

    /// <summary>
    /// Indica se o contexto de BU está devidamente inicializado.
    /// </summary>
    public bool IsInitialized => _buScopeContext.IsInitialized;
}
