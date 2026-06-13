using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using NotificationDelivery.Application.Rendering;
using NotificationDelivery.Contracts;

namespace NotificationDelivery.Application.Decorators;

/// <summary>
/// Decorator que aplica o branding do tenant à <see cref="EmailMessage"/> antes de delegar
/// ao sender interno (design §5.3, Req 5, Req 5.1..5.4, DD-006, DEC-004).
///
/// Responsabilidades:
/// <list type="bullet">
///   <item><description>Injeta <see cref="BrandingConfig.LogoUrl"/>, <see cref="BrandingConfig.PrimaryColor"/> e <see cref="BrandingConfig.SecondaryColor"/> do tenant no template via <see cref="EmailTemplateRenderer"/> (Req 5.1, DEC-004).</description></item>
///   <item><description>Quando <see cref="EmailMessage.Branding"/> é <c>null</c>, aplica o tema padrão (<see cref="BrandingDefaults"/>) com log de aviso, sem falhar o envio (Req 5.3).</description></item>
///   <item><description>Nunca injeta CSS arbitrário — apenas os valores controlados do contrato (Req 5.2).</description></item>
///   <item><description>Não altera <see cref="EmailMessage.RecipientEmail"/>, <see cref="EmailMessage.Subject"/> nem <see cref="EmailMessage.CorrelationId"/> (Req 5.4).</description></item>
/// </list>
///
/// Posição na cadeia de decorators (design §5.3):
/// <code>ResilientEmailSender → BrandingEmailDecorator → ProviderEmailSender</code>
/// </summary>
public sealed class BrandingEmailDecorator : IEmailSender
{
    private readonly IEmailSender _inner;
    private readonly EmailTemplateRenderer _renderer;
    private readonly ILogger<BrandingEmailDecorator> _logger;

    /// <summary>
    /// Constrói um <see cref="BrandingEmailDecorator"/>.
    /// </summary>
    /// <param name="inner">Sender interno ao qual a mensagem decorada é delegada.</param>
    /// <param name="renderer">Renderizador determinístico de template HTML+plaintext.</param>
    /// <param name="logger">Logger para aviso quando tema padrão for aplicado (sem PII).</param>
    public BrandingEmailDecorator(
        IEmailSender inner,
        EmailTemplateRenderer renderer,
        ILogger<BrandingEmailDecorator> logger)
    {
        _inner = inner;
        _renderer = renderer;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SendResult> SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        // Logar aviso quando tema padrão for aplicado — sem PII (RNF 4, Req 5.3)
        if (message.Branding is null)
            _logger.LogWarning(
                "BrandingConfig ausente para TenantId={TenantId}, CorrelationId={CorrelationId}. " +
                "Aplicando tema padrão (BrandingDefaults).",
                message.TenantId,
                message.CorrelationId);

        // Renderizar HTML+plaintext com branding (ou tema padrão se ausente)
        var rendered = _renderer.Render(message);

        // Construir nova EmailMessage com HTML decorado, preservando todos os demais campos (Req 5.4):
        // RecipientEmail, Subject, CorrelationId, TenantId, IdempotencyKey inalterados.
        var decoratedMessage = new EmailMessage(
            recipientEmail: message.RecipientEmail,
            subject: message.Subject,
            htmlBody: rendered.Html,
            tenantId: message.TenantId,
            correlationId: message.CorrelationId,
            plainTextBody: rendered.PlainText,
            branding: message.Branding,
            idempotencyKey: message.IdempotencyKey);

        return await _inner.SendAsync(decoratedMessage, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckAvailabilityAsync(
        CancellationToken cancellationToken = default) =>
        _inner.CheckAvailabilityAsync(cancellationToken);
}
