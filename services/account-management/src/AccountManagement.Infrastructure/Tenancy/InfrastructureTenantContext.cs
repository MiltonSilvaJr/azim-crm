using AccountManagement.Application.Behaviors;

namespace AccountManagement.Infrastructure.Tenancy;

/// <summary>
/// Adaptador de Infrastructure que expõe o <see cref="TenantContext"/> da Application
/// para o <see cref="AccountManagementDbContext"/> e o filtro global EF Core.
///
/// O filtro global usa este contexto para filtrar queries por <c>tenant_id</c> (DD-002, ADR-0001).
/// Resolvido como serviço scoped — um por request HTTP.
///
/// Mapeia: design §6.1, design §14, DD-002, ADR-0001, RNF 5.
/// </summary>
public sealed class InfrastructureTenantContext
{
    private readonly TenantContext _tenantContext;

    /// <summary>Inicializa com o <see cref="TenantContext"/> da Application.</summary>
    public InfrastructureTenantContext(TenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Retorna o <c>tenant_id</c> do contexto autenticado.
    /// Retorna <c>Guid.Empty</c> quando o contexto ainda não foi inicializado
    /// (usado apenas em migrações ou contextos de tooling — nunca em runtime).
    /// </summary>
    public Guid TenantId => _tenantContext.TenantId ?? Guid.Empty;

    /// <summary>
    /// Indica se o contexto de tenant está devidamente inicializado.
    /// Em runtime, o <see cref="TenantScopeBehavior{TRequest,TResponse}"/> garante
    /// que este valor seja <c>true</c> antes de qualquer operação de dados.
    /// </summary>
    public bool IsInitialized => _tenantContext.TenantId.HasValue;
}
