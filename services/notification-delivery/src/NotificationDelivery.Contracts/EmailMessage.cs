using System.Text.RegularExpressions;

namespace NotificationDelivery.Contracts;

/// <summary>
/// Mensagem de e-mail a ser enviada via <see cref="IEmailSender"/>.
///
/// Value object imutável com igualdade por valor (Req 2.3).
/// Validação na construção rejeita entradas inválidas antes de qualquer chamada ao provedor (Req 2.5).
///
/// Invariantes (design §4.3, §8.2, Req 2.1..2.5, RNF 4):
/// <list type="bullet">
///   <item><description><see cref="RecipientEmail"/> deve ser sintaticamente válido; nunca exposto em <see cref="ToString()"/> nem em mensagens de exceção (RNF 4).</description></item>
///   <item><description><see cref="Subject"/> deve ser não vazio e respeitar <see cref="MaxSubjectLength"/>.</description></item>
///   <item><description><see cref="HtmlBody"/> deve ser não vazio.</description></item>
///   <item><description><see cref="TenantId"/> e <see cref="CorrelationId"/> devem estar presentes (Req 2.2).</description></item>
///   <item><description><see cref="PlainTextBody"/>, <see cref="Branding"/> e <see cref="IdempotencyKey"/> são opcionais.</description></item>
/// </list>
/// </summary>
public sealed class EmailMessage : IEquatable<EmailMessage>
{
    // -------------------------------------------------------------------------
    // Limites e regex de validação
    // -------------------------------------------------------------------------

    /// <summary>
    /// Comprimento máximo do assunto (RFC 2822 limita header de e-mail a 998 chars por linha).
    /// Constante extraída para referência em testes e validação (TASK-04/ST-03).
    /// </summary>
    public const int MaxSubjectLength = 998;

    /// <summary>
    /// Regex de validação sintática de e-mail — RFC 5321 simplificado.
    /// Valida formato <c>local@domain.tld</c> sem fazer lookup DNS.
    /// Não expõe o valor inválido em mensagens de erro (RNF 4).
    /// </summary>
    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~\-]+@[a-zA-Z0-9\-]+(?:\.[a-zA-Z0-9\-]+)*\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromMilliseconds(100));

    // -------------------------------------------------------------------------
    // Propriedades imutáveis
    // -------------------------------------------------------------------------

    /// <summary>
    /// Endereço de e-mail do destinatário.
    /// PII — nunca exposto em <see cref="ToString()"/>, logs nem mensagens de erro (RNF 4).
    /// </summary>
    public string RecipientEmail { get; }

    /// <summary>Assunto do e-mail. Obrigatório e não vazio.</summary>
    public string Subject { get; }

    /// <summary>Corpo HTML fornecido pelo chamador. Obrigatório e não vazio.</summary>
    public string HtmlBody { get; }

    /// <summary>
    /// Corpo em texto puro.
    /// Opcional — quando ausente, derivado do HTML pelo <c>EmailTemplateRenderer</c> (Req 6.3).
    /// </summary>
    public string? PlainTextBody { get; }

    /// <summary>
    /// Configuração de branding do tenant.
    /// Opcional — quando ausente, o <c>BrandingEmailDecorator</c> aplica o tema padrão (Req 5.3).
    /// </summary>
    public BrandingConfig? Branding { get; }

    /// <summary>
    /// Identificador do tenant para rastreabilidade multi-tenant. Obrigatório (Req 2.2, TOBJ-09).
    /// </summary>
    public string TenantId { get; }

    /// <summary>
    /// Identificador de correlação propagado ao <see cref="SendResult"/>. Obrigatório (Req 2.2).
    /// </summary>
    public string CorrelationId { get; }

    /// <summary>
    /// Chave de idempotência propagada ao provedor quando suportado (Req 9, DD-005).
    /// Opcional — quando ausente, a deduplicação é responsabilidade do chamador.
    /// </summary>
    public string? IdempotencyKey { get; }

    // -------------------------------------------------------------------------
    // Construtor com validação
    // -------------------------------------------------------------------------

    /// <summary>
    /// Constrói um <see cref="EmailMessage"/> validando todos os campos obrigatórios.
    /// </summary>
    /// <param name="recipientEmail">E-mail do destinatário (PII; validado sintaticamente; não exposto em erro).</param>
    /// <param name="subject">Assunto. Não vazio e dentro de <see cref="MaxSubjectLength"/>.</param>
    /// <param name="htmlBody">Corpo HTML. Não vazio.</param>
    /// <param name="tenantId">Identificador do tenant. Não vazio.</param>
    /// <param name="correlationId">Identificador de correlação. Não vazio.</param>
    /// <param name="plainTextBody">Corpo texto puro (opcional).</param>
    /// <param name="branding">Configuração de branding do tenant (opcional).</param>
    /// <param name="idempotencyKey">Chave de idempotência (opcional).</param>
    /// <exception cref="ArgumentException">Quando qualquer campo obrigatório for inválido.</exception>
    public EmailMessage(
        string recipientEmail,
        string subject,
        string htmlBody,
        string tenantId,
        string correlationId,
        string? plainTextBody = null,
        BrandingConfig? branding = null,
        string? idempotencyKey = null)
    {
        RecipientEmail = ValidateRecipientEmail(recipientEmail);
        Subject = ValidateSubject(subject);
        HtmlBody = ValidateHtmlBody(htmlBody);
        TenantId = ValidateRequired(tenantId, nameof(tenantId));
        CorrelationId = ValidateRequired(correlationId, nameof(correlationId));
        PlainTextBody = plainTextBody;
        Branding = branding;
        IdempotencyKey = idempotencyKey;
    }

    // -------------------------------------------------------------------------
    // ToString() sem PII (TASK-04/ST-03, RNF 4)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Representação textual segura — nunca expõe <see cref="RecipientEmail"/> (RNF 4).
    /// </summary>
    public override string ToString() =>
        $"EmailMessage {{ Subject={Subject}, TenantId={TenantId}, CorrelationId={CorrelationId} }}";

    // -------------------------------------------------------------------------
    // Igualdade por valor
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public bool Equals(EmailMessage? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return RecipientEmail == other.RecipientEmail
            && Subject == other.Subject
            && HtmlBody == other.HtmlBody
            && PlainTextBody == other.PlainTextBody
            && Equals(Branding, other.Branding)
            && TenantId == other.TenantId
            && CorrelationId == other.CorrelationId
            && IdempotencyKey == other.IdempotencyKey;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as EmailMessage);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(
        RecipientEmail, Subject, HtmlBody, PlainTextBody, Branding, TenantId, CorrelationId, IdempotencyKey);

    /// <summary>Operador de igualdade por valor.</summary>
    public static bool operator ==(EmailMessage? left, EmailMessage? right) => Equals(left, right);

    /// <summary>Operador de desigualdade por valor.</summary>
    public static bool operator !=(EmailMessage? left, EmailMessage? right) => !Equals(left, right);

    // -------------------------------------------------------------------------
    // Validações de invariante (sem PII em mensagens de erro — RNF 4)
    // -------------------------------------------------------------------------

    private static string ValidateRecipientEmail(string recipientEmail)
    {
        // Não expõe o valor inválido na mensagem de exceção (RNF 4)
        if (string.IsNullOrWhiteSpace(recipientEmail) || !EmailRegex.IsMatch(recipientEmail))
            throw new ArgumentException(
                "O e-mail do destinatário é inválido ou está ausente. " +
                "Verifique o formato (local@domínio.tld) sem incluir o valor em mensagem de erro.",
                nameof(recipientEmail));

        return recipientEmail;
    }

    private static string ValidateSubject(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException(
                "O assunto não pode ser nulo, vazio ou apenas espaços em branco.",
                nameof(subject));

        if (subject.Length > MaxSubjectLength)
            throw new ArgumentException(
                $"O assunto excede o comprimento máximo permitido de {MaxSubjectLength} caracteres.",
                nameof(subject));

        return subject;
    }

    private static string ValidateHtmlBody(string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(htmlBody))
            throw new ArgumentException(
                "O corpo HTML não pode ser nulo, vazio ou apenas espaços em branco.",
                nameof(htmlBody));

        return htmlBody;
    }

    private static string ValidateRequired(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(
                $"O campo '{paramName}' é obrigatório e não pode ser nulo, vazio ou apenas espaços em branco.",
                paramName);

        return value;
    }
}
