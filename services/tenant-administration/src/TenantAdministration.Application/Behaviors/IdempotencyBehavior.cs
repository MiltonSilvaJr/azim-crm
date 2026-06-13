using System.Text.Json;
using MediatR;
using TenantAdministration.Application.Ports;

namespace TenantAdministration.Application.Behaviors;

/// <summary>
/// Marca um command como elegível para idempotência via <c>Idempotency-Key</c>.
/// O <see cref="IdempotencyBehavior{TRequest,TResponse}"/> só aplica a chave quando
/// o request implementa esta interface.
/// </summary>
public interface IIdempotentCommand
{
    /// <summary>Chave de idempotência fornecida pelo chamador (ex.: UUID do header HTTP).</summary>
    string? IdempotencyKey { get; }
}

/// <summary>
/// Behavior MediatR #5 na cadeia do pipeline (design.md §5.4, §6.5).
/// Para commands que implementam <see cref="IIdempotentCommand"/>: verifica se a chave já foi
/// processada. Em caso afirmativo, retorna o resultado armazenado sem chamar o handler.
/// Evita duplicação de provisionamento de tenant por retry (Req 1, §6.5).
/// </summary>
public sealed class IdempotencyBehavior<TRequest, TResponse>(
    IIdempotencyStore store)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Aplica idempotência somente para commands que declaram a chave
        if (request is not IIdempotentCommand idempotent ||
            string.IsNullOrWhiteSpace(idempotent.IdempotencyKey))
        {
            return await next();
        }

        var key = idempotent.IdempotencyKey!;

        // Verifica se a chave já foi processada
        var cached = await store.GetAsync(key, cancellationToken);
        if (cached is not null)
        {
            // Retorna o resultado serializado anteriormente
            return JsonSerializer.Deserialize<TResponse>(cached)
                ?? throw new InvalidOperationException(
                    $"Não foi possível desserializar o resultado de idempotência para a chave '{key}'.");
        }

        // Executa o handler e armazena o resultado
        var response = await next(cancellationToken);

        var resultJson = JsonSerializer.Serialize(response);
        await store.SetAsync(key, resultJson, cancellationToken);

        return response;
    }
}
