namespace Organization.Infrastructure.Persistence;

/// <summary>
/// Acessor de tenant_id para uso nos global query filters do EF Core.
/// É instanciado com escopo por request (scoped) via DI, garantindo que o
/// global filter seja avaliado com o tenant_id correto para cada operação.
///
/// No contexto de testes, é instanciado diretamente e atualizado antes de cada operação.
/// </summary>
public sealed class TenantContextAccessor
{
    /// <summary>Tenant_id corrente para uso no global query filter.</summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Quando <c>false</c>, desativa o global query filter (usado em migrations e seeds).
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
