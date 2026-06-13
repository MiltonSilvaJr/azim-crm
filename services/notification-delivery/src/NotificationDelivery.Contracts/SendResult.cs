namespace NotificationDelivery.Contracts;

/// <summary>
/// Resultado de uma tentativa de envio de e-mail via <see cref="IEmailSender"/>.
///
/// Value object imutável com igualdade por valor.
/// Toda chamada <c>SendAsync</c> resolve em exatamente um <see cref="SendStatus"/> canônico —
/// nunca lança exceção de provedor ao chamador (Req 3.5, PBT-04).
///
/// Invariantes (design §4.3, §8.3, Req 3.1..3.5, RNF 4):
/// <list type="bullet">
///   <item><description><see cref="MessageId"/> presente se e somente se <see cref="Status"/> = <see cref="SendStatus.Sent"/>.</description></item>
///   <item><description><see cref="Reason"/> presente se e somente se status for de falha (não <see cref="SendStatus.Sent"/>).</description></item>
///   <item><description><see cref="CorrelationId"/> sempre propagado, nunca nulo.</description></item>
///   <item><description>Nenhum campo expõe PII do destinatário nem credencial de provedor (RNF 4, Req 3.3).</description></item>
/// </list>
/// </summary>
public sealed class SendResult : IEquatable<SendResult>
{
    // -------------------------------------------------------------------------
    // Propriedades imutáveis
    // -------------------------------------------------------------------------

    /// <summary>Estado canônico do resultado (design §4.5).</summary>
    public SendStatus Status { get; }

    /// <summary>
    /// Identificador de mensagem atribuído pelo provedor.
    /// Presente se e somente se <see cref="Status"/> = <see cref="SendStatus.Sent"/> (design §8.3).
    /// </summary>
    public string? MessageId { get; }

    /// <summary>
    /// Motivo da falha com código do catálogo e mensagem sem PII.
    /// Presente se e somente se <see cref="Status"/> não é <see cref="SendStatus.Sent"/> (design §8.3).
    /// </summary>
    public FailureReason? Reason { get; }

    /// <summary>
    /// Identificador de correlação ecoado da <see cref="EmailMessage"/> de origem.
    /// Sempre propagado — presente em todos os casos (design §8.3, Req 3.2).
    /// </summary>
    public string CorrelationId { get; }

    /// <summary>
    /// Identificador do provedor que processou o envio (ex.: <c>"postmark"</c>, <c>"sendgrid"</c>, <c>"resend"</c>).
    /// </summary>
    public string Provider { get; }

    /// <summary>Número de tentativas realizadas antes de resolver este resultado.</summary>
    public int AttemptCount { get; }

    // -------------------------------------------------------------------------
    // Construtor com validação de invariantes
    // -------------------------------------------------------------------------

    /// <summary>
    /// Constrói um <see cref="SendResult"/> validando os invariantes do design §4.3 e §8.3.
    /// </summary>
    /// <param name="status">Estado canônico do resultado.</param>
    /// <param name="correlationId">Identificador de correlação da mensagem de origem. Obrigatório.</param>
    /// <param name="provider">Identificador do provedor (sem dados de credencial).</param>
    /// <param name="attemptCount">Número de tentativas realizadas.</param>
    /// <param name="messageId">ID do provedor — obrigatório quando <paramref name="status"/> = <see cref="SendStatus.Sent"/>; deve ser <c>null</c> caso contrário.</param>
    /// <param name="reason">Motivo da falha — obrigatório quando <paramref name="status"/> não é <see cref="SendStatus.Sent"/>; deve ser <c>null</c> para Sent.</param>
    /// <exception cref="ArgumentException">Quando os invariantes não forem satisfeitos.</exception>
    public SendResult(
        SendStatus status,
        string correlationId,
        string provider,
        int attemptCount,
        string? messageId,
        FailureReason? reason)
    {
        Status = status;
        CorrelationId = ValidateCorrelationId(correlationId);
        Provider = provider ?? string.Empty;
        AttemptCount = attemptCount;
        MessageId = ValidateMessageId(status, messageId);
        Reason = ValidateReason(status, reason);
    }

    // -------------------------------------------------------------------------
    // ToString() sem PII (TASK-06/ST-03, RNF 4)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Representação textual segura — nunca expõe PII nem credencial (RNF 4).
    /// </summary>
    public override string ToString() =>
        $"SendResult {{ Status={Status}, Provider={Provider}, AttemptCount={AttemptCount}, CorrelationId={CorrelationId} }}";

    // -------------------------------------------------------------------------
    // Igualdade por valor
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public bool Equals(SendResult? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Status == other.Status
            && MessageId == other.MessageId
            && Equals(Reason, other.Reason)
            && CorrelationId == other.CorrelationId
            && Provider == other.Provider
            && AttemptCount == other.AttemptCount;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as SendResult);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(Status, MessageId, Reason, CorrelationId, Provider, AttemptCount);

    /// <summary>Operador de igualdade por valor.</summary>
    public static bool operator ==(SendResult? left, SendResult? right) => Equals(left, right);

    /// <summary>Operador de desigualdade por valor.</summary>
    public static bool operator !=(SendResult? left, SendResult? right) => !Equals(left, right);

    // -------------------------------------------------------------------------
    // Validações de invariante
    // -------------------------------------------------------------------------

    private static string ValidateCorrelationId(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException(
                "CorrelationId é obrigatório e deve ser propagado em todos os casos (design §8.3, Req 3.2).",
                nameof(correlationId));

        return correlationId;
    }

    private static string? ValidateMessageId(SendStatus status, string? messageId)
    {
        if (status == SendStatus.Sent && string.IsNullOrWhiteSpace(messageId))
            throw new ArgumentException(
                "MessageId é obrigatório quando Status=Sent. " +
                "O provedor deve retornar um identificador de mensagem ao confirmar a entrega (design §8.3).",
                nameof(messageId));

        if (status != SendStatus.Sent && messageId is not null)
            throw new ArgumentException(
                "MessageId deve ser null quando Status não é Sent (design §8.3 — 'MessageId presente sse Sent').",
                nameof(messageId));

        return messageId;
    }

    private static FailureReason? ValidateReason(SendStatus status, FailureReason? reason)
    {
        if (status != SendStatus.Sent && reason is null)
            throw new ArgumentException(
                $"Reason é obrigatório quando Status={status}. " +
                "Use os códigos do catálogo FailureCode (design §8.3, §12).",
                nameof(reason));

        if (status == SendStatus.Sent && reason is not null)
            throw new ArgumentException(
                "Reason deve ser null quando Status=Sent (design §8.3 — 'Reason presente sse falha').",
                nameof(reason));

        return reason;
    }
}
