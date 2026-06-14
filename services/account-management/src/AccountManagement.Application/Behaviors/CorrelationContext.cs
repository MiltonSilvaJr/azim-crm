namespace AccountManagement.Application.Behaviors;

/// <summary>
/// Contexto de rastreabilidade contendo <c>correlation_id</c> e <c>tenant_id</c>.
///
/// Injetado como serviço scoped. Populado pelo middleware de CorrelationId e
/// pelo <see cref="CorrelationLoggingBehavior{TRequest,TResponse}"/> (design §5.4, RNF 9).
///
/// Mapeia: design §5.4, design §11, RNF 9, RNF 1.
/// </summary>
public sealed class CorrelationContext
{
    /// <summary><c>correlation_id</c> da requisição. Nulo antes de inicialização.</summary>
    public string? CorrelationId { get; private set; }

    /// <summary><c>tenant_id</c> associado à requisição. Nulo antes de inicialização.</summary>
    public Guid? TenantId { get; private set; }

    /// <summary>
    /// Define o contexto de correlação. Chamado pelo middleware de CorrelationId.
    /// </summary>
    public void SetCorrelation(string correlationId, Guid tenantId)
    {
        CorrelationId = correlationId;
        TenantId = tenantId;
    }
}
