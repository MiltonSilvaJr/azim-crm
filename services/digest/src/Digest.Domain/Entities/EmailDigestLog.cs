using Digest.Domain.Enums;
using Digest.Domain.ValueObjects;

namespace Digest.Domain.Entities;

/// <summary>
/// Entidade de registro de envio do digest por (<c>tenant_id</c>, <c>user_id</c>, <c>digest_date</c>).
/// Base de idempotência (RN-010) e trilha de entregabilidade (KPI-03/04).
/// State machine: scheduled → sent → delivered → opened | scheduled → failed | sent → bounced.
/// Terminais: <see cref="DigestStatus.Bounced"/>, <see cref="DigestStatus.Failed"/>, <see cref="DigestStatus.Opened"/>.
/// </summary>
/// <remarks>
/// O status <see cref="DigestStatus.Scheduled"/> é a reserva inserida antes de <c>IEmailSender</c> (DD-008).
/// <c>digest_date</c> representa a data local do tenant, não UTC (Req 8.3).
/// Sem dependências de EF Core ou infraestrutura.
/// </remarks>
public sealed class EmailDigestLog
{
    // Estado encapsulado — sem setters públicos
    private DigestStatus _status;

    /// <summary>Identificador único do registro (UUID).</summary>
    public Guid Id { get; }

    /// <summary>Identificador do tenant (multi-tenancy).</summary>
    public Guid TenantId { get; }

    /// <summary>Identificador do usuário destinatário.</summary>
    public Guid UserId { get; }

    /// <summary>Data local do tenant — não UTC (Req 8.3).</summary>
    public DigestDate DigestDate { get; }

    /// <summary>Status atual na state machine.</summary>
    public DigestStatus Status => _status;

    /// <summary>Identificador da mensagem no provedor de e-mail (sem PII).</summary>
    public string? MessageId { get; private set; }

    /// <summary>Identificador de correlação para rastreabilidade (RNF 6.1).</summary>
    public Guid? CorrelationId { get; }

    /// <summary>Timestamp de reserva (inserção da linha).</summary>
    public DateTimeOffset ScheduledAt { get; }

    /// <summary>Timestamp do aceite pelo provedor.</summary>
    public DateTimeOffset? SentAt { get; private set; }

    /// <summary>Timestamp da confirmação de entrega pelo provedor.</summary>
    public DateTimeOffset? DeliveredAt { get; private set; }

    /// <summary>Timestamp de abertura pelo destinatário.</summary>
    public DateTimeOffset? OpenedAt { get; private set; }

    /// <summary>Timestamp de falha definitiva.</summary>
    public DateTimeOffset? FailedAt { get; private set; }

    private EmailDigestLog(
        Guid id,
        Guid tenantId,
        Guid userId,
        DigestDate digestDate,
        Guid? correlationId)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        DigestDate = digestDate;
        CorrelationId = correlationId;
        _status = DigestStatus.Scheduled;
        ScheduledAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Cria e retorna uma nova reserva de idempotência com status <see cref="DigestStatus.Scheduled"/>.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant. Não pode ser vazio.</param>
    /// <param name="userId">Identificador do usuário. Não pode ser vazio.</param>
    /// <param name="digestDate">Data local do tenant.</param>
    /// <param name="correlationId">Identificador de correlação opcional.</param>
    /// <exception cref="ArgumentException">Quando <paramref name="tenantId"/> ou <paramref name="userId"/> são vazios.</exception>
    public static EmailDigestLog Schedule(
        Guid tenantId,
        Guid userId,
        DigestDate digestDate,
        Guid? correlationId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenant_id não pode ser vazio.", nameof(tenantId));
        if (userId == Guid.Empty)
            throw new ArgumentException("user_id não pode ser vazio.", nameof(userId));
        ArgumentNullException.ThrowIfNull(digestDate);

        return new EmailDigestLog(Guid.NewGuid(), tenantId, userId, digestDate, correlationId);
    }

    // ---------------------------------------------------------------
    // Transições de estado
    // ---------------------------------------------------------------

    /// <summary>
    /// Transição: <see cref="DigestStatus.Scheduled"/> → <see cref="DigestStatus.Sent"/>.
    /// Registra o <paramref name="messageId"/> do provedor.
    /// </summary>
    /// <exception cref="InvalidOperationException">Quando não está em <see cref="DigestStatus.Scheduled"/>.</exception>
    public void MarkSent(string messageId)
    {
        EnsureStatus(DigestStatus.Scheduled, nameof(MarkSent));
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        MessageId = messageId;
        SentAt = DateTimeOffset.UtcNow;
        _status = DigestStatus.Sent;
    }

    /// <summary>
    /// Transição: <see cref="DigestStatus.Scheduled"/> → <see cref="DigestStatus.Failed"/> (terminal).
    /// </summary>
    /// <exception cref="InvalidOperationException">Quando não está em <see cref="DigestStatus.Scheduled"/>.</exception>
    public void MarkFailed()
    {
        EnsureStatus(DigestStatus.Scheduled, nameof(MarkFailed));
        FailedAt = DateTimeOffset.UtcNow;
        _status = DigestStatus.Failed;
    }

    /// <summary>
    /// Transição: <see cref="DigestStatus.Sent"/> → <see cref="DigestStatus.Delivered"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Quando não está em <see cref="DigestStatus.Sent"/>.</exception>
    public void MarkDelivered()
    {
        EnsureStatus(DigestStatus.Sent, nameof(MarkDelivered));
        DeliveredAt = DateTimeOffset.UtcNow;
        _status = DigestStatus.Delivered;
    }

    /// <summary>
    /// Transição: <see cref="DigestStatus.Delivered"/> → <see cref="DigestStatus.Opened"/> (terminal).
    /// </summary>
    /// <exception cref="InvalidOperationException">Quando não está em <see cref="DigestStatus.Delivered"/>.</exception>
    public void MarkOpened()
    {
        EnsureStatus(DigestStatus.Delivered, nameof(MarkOpened));
        OpenedAt = DateTimeOffset.UtcNow;
        _status = DigestStatus.Opened;
    }

    /// <summary>
    /// Transição: <see cref="DigestStatus.Sent"/> → <see cref="DigestStatus.Bounced"/> (terminal).
    /// </summary>
    /// <exception cref="InvalidOperationException">Quando não está em <see cref="DigestStatus.Sent"/>.</exception>
    public void MarkBounced()
    {
        EnsureStatus(DigestStatus.Sent, nameof(MarkBounced));
        _status = DigestStatus.Bounced;
    }

    // ---------------------------------------------------------------
    // Helper privado de guarda de transição
    // ---------------------------------------------------------------

    private void EnsureStatus(DigestStatus expected, string operation)
    {
        if (_status != expected)
            throw new InvalidOperationException(
                $"Operação '{operation}' inválida no status '{_status}'. Esperado: '{expected}'.");
    }
}
