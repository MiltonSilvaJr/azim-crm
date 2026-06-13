using System.Text.RegularExpressions;
using NotificationDelivery.Contracts;

namespace NotificationDelivery.Application.Validation;

/// <summary>
/// Validador de borda de <see cref="EmailMessage"/> na camada Application.
///
/// Centraliza a validação antes de qualquer chamada ao provedor, mapeando
/// entradas inválidas para <see cref="SendResult"/> de <see cref="SendStatus.PermanentFailure"/>
/// com os códigos canônicos do catálogo (Req 2.5, design §5.5):
/// <list type="bullet">
///   <item><description><see cref="FailureCode.InvalidRecipient"/> — destinatário ausente ou malformado.</description></item>
///   <item><description><see cref="FailureCode.MissingSubjectOrBody"/> — assunto ou corpo ausente, ou campos obrigatórios de rastreabilidade ausentes.</description></item>
/// </list>
///
/// Regra de segurança: o <see cref="RecipientEmail"/> nunca aparece em
/// nenhuma mensagem de erro ou log (RNF 4, design §5.5).
/// </summary>
public sealed class EmailMessageValidator
{
    // -------------------------------------------------------------------------
    // Regex de validação sintática de e-mail (RFC 5321 simplificado)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Regex de validação de e-mail — mesma lógica do <c>EmailMessage</c> em Contracts,
    /// duplicada aqui para validação de strings brutas antes da construção do objeto.
    /// </summary>
    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~\-]+@[a-zA-Z0-9\-]+(?:\.[a-zA-Z0-9\-]+)*\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromMilliseconds(100));

    // -------------------------------------------------------------------------
    // Validação de EmailMessage tipada
    // -------------------------------------------------------------------------

    /// <summary>
    /// Valida um <see cref="EmailMessage"/> já construído.
    /// Retorna <c>null</c> quando válido; retorna <see cref="SendResult"/> de falha quando inválido.
    ///
    /// Como <see cref="EmailMessage"/> já valida seus campos na construção,
    /// este método verifica invariantes adicionais de Application (ex.: consistência contextual futura).
    /// </summary>
    /// <param name="message">Mensagem a validar.</param>
    /// <param name="provider">Identificador do provedor ativo (para preencher o <see cref="SendResult"/>).</param>
    /// <param name="attemptCount">Contagem de tentativas (para preencher o <see cref="SendResult"/>).</param>
    /// <returns><c>null</c> se válida; <see cref="SendResult"/> de falha permanente caso contrário.</returns>
    public SendResult? Validate(EmailMessage message, string provider, int attemptCount) =>
        // EmailMessage já garante invariantes na construção.
        // Validação adicional de Application pode ser adicionada aqui conforme evolução.
        null;

    // -------------------------------------------------------------------------
    // Validação de strings brutas (antes de construir EmailMessage)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Valida os campos de uma mensagem representados como strings brutas,
    /// antes da construção do objeto <see cref="EmailMessage"/>.
    ///
    /// Usado pelo <c>ResilientEmailSender</c> para interceptar entradas inválidas
    /// sem consumir tentativa nem cota de envio (Req 2.5).
    ///
    /// Ordem de verificação:
    /// <list type="number">
    ///   <item><description>Destinatário (<see cref="FailureCode.InvalidRecipient"/>).</description></item>
    ///   <item><description>Assunto, corpo, TenantId e CorrelationId (<see cref="FailureCode.MissingSubjectOrBody"/>).</description></item>
    /// </list>
    /// </summary>
    /// <param name="recipientEmail">E-mail do destinatário (PII — nunca exposto em mensagens de erro).</param>
    /// <param name="subject">Assunto da mensagem.</param>
    /// <param name="htmlBody">Corpo HTML.</param>
    /// <param name="tenantId">Identificador do tenant.</param>
    /// <param name="correlationId">Identificador de correlação.</param>
    /// <param name="provider">Identificador do provedor ativo.</param>
    /// <param name="attemptCount">Contagem de tentativas.</param>
    /// <returns><c>null</c> se todos os campos são válidos; <see cref="SendResult"/> de falha caso contrário.</returns>
    public SendResult? ValidateRaw(
        string recipientEmail,
        string subject,
        string htmlBody,
        string tenantId,
        string correlationId,
        string provider,
        int attemptCount)
    {
        // Fallback de correlationId para preencher o SendResult mesmo em caso de ausência
        var safeCorrelationId = string.IsNullOrWhiteSpace(correlationId)
            ? "unknown"
            : correlationId;

        // 1. Validar destinatário → NOTIF-ERR-001
        if (string.IsNullOrWhiteSpace(recipientEmail) || !EmailRegex.IsMatch(recipientEmail))
            return PermanentFailure(
                code: FailureCode.InvalidRecipient,
                // Mensagem sem PII — não incluir o e-mail (RNF 4)
                message: "Mensagem inválida: destinatário ausente ou malformado.",
                correlationId: safeCorrelationId,
                provider: provider,
                attemptCount: attemptCount);

        // 2. Validar assunto, corpo e campos de rastreabilidade → NOTIF-ERR-002
        if (string.IsNullOrWhiteSpace(subject)
            || string.IsNullOrWhiteSpace(htmlBody)
            || string.IsNullOrWhiteSpace(tenantId)
            || string.IsNullOrWhiteSpace(correlationId))
            return PermanentFailure(
                code: FailureCode.MissingSubjectOrBody,
                message: "Assunto, corpo HTML, TenantId ou CorrelationId obrigatório ausente.",
                correlationId: safeCorrelationId,
                provider: provider,
                attemptCount: attemptCount);

        return null;
    }

    // -------------------------------------------------------------------------
    // Helper privado
    // -------------------------------------------------------------------------

    private static SendResult PermanentFailure(
        string code,
        string message,
        string correlationId,
        string provider,
        int attemptCount) =>
        new(
            status: SendStatus.PermanentFailure,
            correlationId: correlationId,
            provider: provider,
            attemptCount: attemptCount,
            messageId: null,
            reason: new FailureReason(code, message, IsRetriable: false));
}
