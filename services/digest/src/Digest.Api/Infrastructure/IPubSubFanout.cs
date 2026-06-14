namespace Digest.Api.Infrastructure;

/// <summary>
/// Abstração para publicação de mensagens de fan-out no tópico Pub/Sub <c>azim-digest-fanout</c>.
/// Uma mensagem por tenant elegível (DD-005, design §6.3).
/// </summary>
public interface IPubSubFanout
{
    /// <summary>
    /// Publica uma mensagem Pub/Sub por tenant elegível no tópico <c>azim-digest-fanout</c>.
    /// O <see cref="PerTenantConsumer"/> consumirá cada mensagem e despachará
    /// um <c>RunDigestForTenantCommand</c> por tenant (design §6.3, DD-005).
    /// </summary>
    /// <param name="eligibleTenants">Lista de tenant IDs elegíveis para o ciclo do digest.</param>
    /// <param name="referenceUtc">Instante UTC de referência do disparo.</param>
    /// <param name="correlationId">Identificador de correlação (sem PII).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task PublishFanoutMessagesAsync(
        IReadOnlyList<Guid> eligibleTenants,
        DateTimeOffset referenceUtc,
        Guid correlationId,
        CancellationToken cancellationToken = default);
}
