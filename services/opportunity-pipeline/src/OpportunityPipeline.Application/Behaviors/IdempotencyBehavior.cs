using System.Text.Json;
using MediatR;
using OpportunityPipeline.Application.Common;

namespace OpportunityPipeline.Application.Behaviors;

/// <summary>
/// Pipeline behavior: deduplica escritas por Idempotency-Key.
/// Chave composta: tenant_id + tipo do command + idempotency_key.
/// Segunda requisição com mesma chave retorna resposta cacheada sem invocar o handler.
/// Executa QUARTO no pipeline.
/// Mapeia: NFR-RES-02 (idempotência), design §5.4 posição 4, design §6.5.
/// </summary>
public sealed class IdempotencyBehavior<TRequest, TResponse>(
    IIdempotencyStore store,
    TenantContext tenantContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Apenas commands idempotentes participam
        if (request is not IIdempotentCommand idempotentCmd)
            return await next(cancellationToken).ConfigureAwait(false);

        var key = idempotentCmd.IdempotencyKey;
        if (string.IsNullOrWhiteSpace(key))
            return await next(cancellationToken).ConfigureAwait(false);

        var tenantId = tenantContext.TenantId;
        var compositeKey = $"{tenantId}:{typeof(TRequest).Name}:{key}";

        // Verifica se já existe resposta cacheada
        var cached = await store.TryGetAsync(compositeKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            var deserialized = JsonSerializer.Deserialize<TResponse>(cached);
            if (deserialized is not null)
                return deserialized;
        }

        var response = await next(cancellationToken).ConfigureAwait(false);

        // Persiste resposta serializada para deduplicação futura
        var serialized = JsonSerializer.Serialize(response);
        await store.SetAsync(compositeKey, serialized, cancellationToken).ConfigureAwait(false);

        return response;
    }
}

/// <summary>
/// Store de idempotência (port) — implementação na Infrastructure (Redis ou banco).
/// Usa serialização JSON para persistir respostas de qualquer tipo.
/// Mapeia: design §6.5, NFR-RES-02.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>Tenta recuperar resposta cacheada serializada. Retorna null se não existir.</summary>
    Task<string?> TryGetAsync(string compositeKey, CancellationToken cancellationToken = default);

    /// <summary>Persiste resposta serializada para deduplicação futura.</summary>
    Task SetAsync(string compositeKey, string serializedResponse, CancellationToken cancellationToken = default);
}
