using Digest.Domain.Events;

namespace Digest.Application.Abstractions;

/// <summary>
/// Porta de publicação de eventos via Outbox transacional (ADR-0004, DD-009).
/// Implementação concreta em <c>Digest.Infrastructure</c> (TASK-18).
/// </summary>
/// <remarks>
/// O evento é gravado na mesma transação do <c>UPDATE</c> para <c>sent</c>,
/// garantindo atomicidade entre auditoria e envio (RNF 10).
/// </remarks>
public interface IOutboxPublisher
{
    /// <summary>
    /// Enfileira um <see cref="DigestEmailSent"/> no Outbox para publicação em <c>digest.email_sent.v1</c>.
    /// Deve ser chamado dentro da mesma transação do <see cref="IEmailDigestLogRepository.MarkSentAsync"/> (DD-009).
    /// </summary>
    /// <param name="domainEvent">Evento de domínio a publicar. Sem PII (RNF 10.2).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task PublishAsync(DigestEmailSent domainEvent, CancellationToken cancellationToken = default);
}
