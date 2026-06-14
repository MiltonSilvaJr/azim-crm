using Digest.Domain.Entities;
using Digest.Domain.Enums;
using Digest.Domain.ValueObjects;

namespace Digest.Application.Repositories;

/// <summary>
/// Repositório de <see cref="EmailDigestLog"/> — expressão de intenção de domínio.
/// Implementação concreta em <c>Digest.Infrastructure</c> (TASK-17).
/// </summary>
/// <remarks>
/// O padrão de idempotência usa <c>INSERT ... ON CONFLICT DO NOTHING</c> (DD-008):
/// apenas quem vence a UNIQUE (<c>tenant_id</c>, <c>user_id</c>, <c>digest_date</c>) envia o e-mail.
/// </remarks>
public interface IEmailDigestLogRepository
{
    /// <summary>
    /// Tenta reservar o envio para o triplo (<paramref name="tenantId"/>, <paramref name="userId"/>, <paramref name="digestDate"/>).
    /// Insere status <see cref="DigestStatus.Scheduled"/> com <c>ON CONFLICT DO NOTHING</c>.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant.</param>
    /// <param name="userId">Identificador do usuário.</param>
    /// <param name="digestDate">Data local do digest.</param>
    /// <param name="correlationId">Identificador de correlação opcional.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>
    /// <see cref="ReservationResult.Reserved"/> se esta instância venceu a corrida;
    /// <see cref="ReservationResult.AlreadySent"/> se já existe registro sent/delivered/opened;
    /// <see cref="ReservationResult.AlreadyScheduled"/> se já existe scheduled por outro worker.
    /// </returns>
    Task<ReservationResult> ReserveAsync(
        Guid tenantId,
        Guid userId,
        DigestDate digestDate,
        Guid? correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza o status para <see cref="DigestStatus.Sent"/> e registra o <paramref name="messageId"/>.
    /// </summary>
    Task MarkSentAsync(
        Guid tenantId,
        Guid userId,
        DigestDate digestDate,
        string messageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza o status para <see cref="DigestStatus.Failed"/>.
    /// </summary>
    Task MarkFailedAsync(
        Guid tenantId,
        Guid userId,
        DigestDate digestDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza o status a partir de evento do provedor de e-mail.
    /// Idempotente por <paramref name="messageId"/> (design §5.3 — UpdateDeliveryStatusHandler).
    /// </summary>
    /// <param name="messageId">Identificador da mensagem no provedor (sem PII).</param>
    /// <param name="newStatus">Novo status: <see cref="DigestStatus.Delivered"/>, <see cref="DigestStatus.Opened"/> ou <see cref="DigestStatus.Bounced"/>.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task UpdateStatusByMessageIdAsync(
        string messageId,
        DigestStatus newStatus,
        CancellationToken cancellationToken = default);
}
