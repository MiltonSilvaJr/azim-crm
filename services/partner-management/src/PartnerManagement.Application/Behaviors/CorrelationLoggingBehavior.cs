using MediatR;
using Microsoft.Extensions.Logging;

namespace PartnerManagement.Application.Behaviors;

/// <summary>
/// Behavior MediatR (posição 1 no pipeline) que injeta campos de rastreabilidade no escopo de log:
/// <c>correlation_id</c>, <c>tenant_id</c> e <c>action</c>.
/// <b>Nunca</b> loga <c>name</c>, <c>contact_email</c> nem <c>contact_phone</c> em claro (RNF 4, RNF 5).
/// Mapeia: RNF 4, RNF 5, design §5.4, design §11.
/// </summary>
/// <typeparam name="TRequest">Tipo do request MediatR.</typeparam>
/// <typeparam name="TResponse">Tipo da resposta.</typeparam>
public sealed class CorrelationLoggingBehavior<TRequest, TResponse>(
    ILogger<CorrelationLoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        string action = typeof(TRequest).Name;

        // Extrai correlation_id e tenant_id via reflection segura em interfaces opcionais
        string? correlationId = (request as IHasCorrelationId)?.CorrelationId;
        Guid? tenantId = (request as IHasTenantId)?.TenantId;
        Guid? partnerId = (request as IHasPartnerId)?.PartnerId;

        using IDisposable? scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["action"] = action,
            ["correlation_id"] = correlationId ?? "n/a",
            ["tenant_id"] = tenantId?.ToString() ?? "n/a",
            ["partner_id"] = partnerId?.ToString() ?? "n/a"
        });

        logger.LogInformation("Iniciando {Action}", action);

        TResponse response = await next().ConfigureAwait(false);

        logger.LogInformation("Concluído {Action}", action);

        return response;
    }
}

/// <summary>Interface opcional para requests com correlation_id.</summary>
public interface IHasCorrelationId
{
    string? CorrelationId { get; }
}

/// <summary>Interface opcional para requests com tenant_id.</summary>
public interface IHasTenantId
{
    Guid TenantId { get; }
}

/// <summary>Interface opcional para requests com partner_id.</summary>
public interface IHasPartnerId
{
    Guid PartnerId { get; }
}
