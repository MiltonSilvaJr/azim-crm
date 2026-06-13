using MediatR;
using Microsoft.Extensions.Logging;
using Organization.Application.Ports;

namespace Organization.Application.Behaviors;

/// <summary>
/// Marcador de interface para commands/queries que requerem idempotência via Inbox.
/// Implementado por commands como <c>AcceptInvitationCommand</c> e <c>ProvisionInitialOrganizationCommand</c>.
/// </summary>
public interface IIdempotentRequest
{
    /// <summary>Chave de idempotência única para este request (ex.: tokenHash, messageId).</summary>
    string IdempotencyKey { get; }

    /// <summary>Tipo do evento/comando para rastreabilidade no Inbox.</summary>
    string EventType { get; }
}

/// <summary>
/// Pipeline behavior de idempotência baseado em Inbox (§6.5, Req 4.4, Req 12.3, PBT-03).
/// Para requests que implementam <see cref="IIdempotentRequest"/>:
/// - Verifica no <see cref="IInboxStore"/> se já foi processado.
/// - Segundo processamento com mesma chave retorna sem executar o handler.
/// </summary>
/// <typeparam name="TRequest">Tipo do request.</typeparam>
/// <typeparam name="TResponse">Tipo da resposta.</typeparam>
public sealed class IdempotencyBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IInboxStore _inboxStore;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<IdempotencyBehavior<TRequest, TResponse>> _logger;

    /// <summary>Inicializa o behavior com o Inbox store e contexto de tenant.</summary>
    public IdempotencyBehavior(
        IInboxStore inboxStore,
        ITenantContext tenantContext,
        ILogger<IdempotencyBehavior<TRequest, TResponse>> logger)
    {
        _inboxStore = inboxStore;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IIdempotentRequest idempotentRequest)
            return await next(cancellationToken);

        var tenantId = _tenantContext.TenantId;
        var key = idempotentRequest.IdempotencyKey;

        var alreadyProcessed = await _inboxStore.IsProcessedAsync(key, tenantId, cancellationToken);
        if (alreadyProcessed)
        {
            _logger.LogInformation(
                "Request idempotente já processado: chave={Key}, tenant={TenantId}, tipo={RequestType}",
                key,
                tenantId,
                typeof(TRequest).Name);

            // Retorna default para o tipo de resposta (sem duplicar efeitos)
            return default!;
        }

        var response = await next(cancellationToken);

        await _inboxStore.MarkProcessedAsync(
            key,
            tenantId,
            idempotentRequest.EventType,
            cancellationToken);

        return response;
    }
}
