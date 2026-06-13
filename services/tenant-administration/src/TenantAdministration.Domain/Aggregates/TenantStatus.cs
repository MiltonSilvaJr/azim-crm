namespace TenantAdministration.Domain.Aggregates;

/// <summary>
/// Estado do ciclo de vida do tenant.
/// Fonte de verdade para a state machine (design.md §4.5).
/// A coluna física <c>active</c> é projeção derivada deste estado (DD-002).
/// </summary>
public enum TenantStatus
{
    /// <summary>Tenant ativo e operacional.</summary>
    Provisioned = 0,

    /// <summary>Tenant suspenso; acesso bloqueado.</summary>
    Suspended = 1
}
