namespace TenantAdministration.Application.Ports;

/// <summary>
/// Porta de entrada que provê o contexto do tenant corrente resolvido por slug ou claim JWT.
/// Implementada na camada de Infrastructure/Api; consumida pelo <c>TransactionBehavior</c>
/// para aplicar <c>SET app.current_tenant</c> no contexto de banco de dados.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Identificador do tenant corrente. Nulo quando a operação está no plano de plataforma
    /// (Platform Operator) ou não há contexto de tenant.
    /// </summary>
    Guid? TenantId { get; }

    /// <summary>Slug do tenant corrente. Nulo no plano de plataforma.</summary>
    string? Slug { get; }

    /// <summary>ID de correlação propagado do request HTTP.</summary>
    string? CorrelationId { get; }
}
