using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationDelivery.Application.Decorators;
using NotificationDelivery.Application.Rendering;
using NotificationDelivery.Contracts;
using Xunit;

namespace NotificationDelivery.Application.Tests;

/// <summary>
/// Testes para <see cref="BrandingEmailDecorator"/>.
///
/// Cobre aplicação de branding estrito DEC-004 (Req 5, Req 5.1..5.4, DD-006):
/// injeção de cores, degradação para tema padrão, imutabilidade de campos críticos.
/// </summary>
public sealed class BrandingEmailDecoratorTests
{
    // -------------------------------------------------------------------------
    // Fake sender para captura da mensagem decorada
    // -------------------------------------------------------------------------

    /// <summary>
    /// Fake de <see cref="IEmailSender"/> que captura a última <see cref="EmailMessage"/> recebida.
    /// Retorna sempre <see cref="SendStatus.Sent"/> para não interferir nos asserts de branding.
    /// </summary>
    private sealed class CapturingSender : IEmailSender
    {
        public EmailMessage? LastMessage { get; private set; }
        public string? LastHtml { get; private set; }

        // A BrandingEmailDecorator deve passar a mensagem decorada ao sender interno.
        // Para capturar o HTML renderizado, o decorator deve enviar uma EmailMessage
        // com o HtmlBody substituído.
        public Task<SendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            LastMessage = message;
            LastHtml = message.HtmlBody;
            return Task.FromResult(new SendResult(
                status: SendStatus.Sent,
                correlationId: message.CorrelationId,
                provider: "fake",
                attemptCount: 1,
                messageId: "fake-msg-id",
                reason: null));
        }

        public Task<HealthCheckResult> CheckAvailabilityAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(HealthCheckResult.Healthy());
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static EmailMessage MessageWithBranding(
        string primaryColor = "#1A2B3C",
        string secondaryColor = "#FFFFFF",
        string logoUrl = "https://logo.example.com/logo.png") =>
        new(
            recipientEmail: "user@example.com",
            subject: "Assunto com branding",
            htmlBody: "<p>Corpo do e-mail.</p>",
            tenantId: "tenant-branding",
            correlationId: "corr-branding",
            branding: new BrandingConfig(logoUrl, primaryColor, secondaryColor));

    private static EmailMessage MessageWithoutBranding() =>
        new(
            recipientEmail: "user@example.com",
            subject: "Assunto sem branding",
            htmlBody: "<p>Corpo sem branding.</p>",
            tenantId: "tenant-no-branding",
            correlationId: "corr-no-branding");

    private static BrandingEmailDecorator CreateDecorator(IEmailSender inner) =>
        new(inner, new EmailTemplateRenderer(), NullLogger<BrandingEmailDecorator>.Instance);

    // -------------------------------------------------------------------------
    // ST-01a — BrandingConfig presente → PrimaryColor injetada no HTML
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-09/ST-01a: BrandingConfig presente → PrimaryColor injetada no HTML (Req 5.1, DEC-004)")]
    public async Task SendAsync_WithBranding_InjectsPrimaryColorIntoHtml()
    {
        var sender = new CapturingSender();
        var decorator = CreateDecorator(sender);
        var message = MessageWithBranding(primaryColor: "#AABB11");

        await decorator.SendAsync(message);

        sender.LastHtml.Should().NotBeNull();
        sender.LastHtml.Should().Contain("#AABB11",
            because: "a cor primária do BrandingConfig deve ser injetada no HTML (Req 5.1, DEC-004)");
    }

    [Fact(DisplayName = "TASK-09/ST-01a: BrandingConfig presente → LogoUrl injetada no HTML")]
    public async Task SendAsync_WithBranding_InjectsLogoUrlIntoHtml()
    {
        var sender = new CapturingSender();
        var decorator = CreateDecorator(sender);
        const string logoUrl = "https://tenant-assets.example.com/my-logo.png";
        var message = MessageWithBranding(logoUrl: logoUrl);

        await decorator.SendAsync(message);

        sender.LastHtml.Should().Contain(logoUrl,
            because: "o logo do tenant deve aparecer no HTML renderizado");
    }

    // -------------------------------------------------------------------------
    // ST-01b — Sem BrandingConfig → tema padrão, sem lançar exceção
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-09/ST-01b: sem BrandingConfig → tema padrão aplicado sem lançar exceção (Req 5.3)")]
    public async Task SendAsync_WithoutBranding_UsesDefaultThemeWithoutThrowing()
    {
        var sender = new CapturingSender();
        var decorator = CreateDecorator(sender);
        var message = MessageWithoutBranding();

        var act = async () => await decorator.SendAsync(message);

        await act.Should().NotThrowAsync(
            because: "ausência de branding deve usar tema padrão sem falha (Req 5.3)");
    }

    [Fact(DisplayName = "TASK-09/ST-01b: sem BrandingConfig → cor padrão do BrandingDefaults no HTML")]
    public async Task SendAsync_WithoutBranding_InjectsDefaultPrimaryColor()
    {
        var sender = new CapturingSender();
        var decorator = CreateDecorator(sender);
        var message = MessageWithoutBranding();

        await decorator.SendAsync(message);

        sender.LastHtml.Should().Contain(BrandingDefaults.PrimaryColor,
            because: "o tema padrão deve ser aplicado quando BrandingConfig está ausente (Req 5.3)");
    }

    // -------------------------------------------------------------------------
    // ST-01c — RecipientEmail, Subject e CorrelationId inalterados (Req 5.4)
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-09/ST-01c: RecipientEmail inalterado após decoração (Req 5.4)")]
    public async Task SendAsync_Always_PreservesRecipientEmail()
    {
        var sender = new CapturingSender();
        var decorator = CreateDecorator(sender);
        var message = MessageWithBranding();

        await decorator.SendAsync(message);

        sender.LastMessage.Should().NotBeNull();
        sender.LastMessage!.RecipientEmail.Should().Be(message.RecipientEmail,
            because: "RecipientEmail não deve ser alterado pelo decorator (Req 5.4)");
    }

    [Fact(DisplayName = "TASK-09/ST-01c: Subject inalterado após decoração (Req 5.4)")]
    public async Task SendAsync_Always_PreservesSubject()
    {
        var sender = new CapturingSender();
        var decorator = CreateDecorator(sender);
        var message = MessageWithBranding();

        await decorator.SendAsync(message);

        sender.LastMessage!.Subject.Should().Be(message.Subject,
            because: "Subject não deve ser alterado pelo decorator (Req 5.4)");
    }

    [Fact(DisplayName = "TASK-09/ST-01c: CorrelationId inalterado após decoração (Req 5.4)")]
    public async Task SendAsync_Always_PreservesCorrelationId()
    {
        var sender = new CapturingSender();
        var decorator = CreateDecorator(sender);
        var message = MessageWithBranding();

        await decorator.SendAsync(message);

        sender.LastMessage!.CorrelationId.Should().Be(message.CorrelationId,
            because: "CorrelationId não deve ser alterado pelo decorator (Req 5.4)");
    }

    // -------------------------------------------------------------------------
    // ST-01d — CSS arbitrário não aceito (Req 5.2)
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-09/ST-01d: CSS arbitrário fora dos campos de branding não é aceito (Req 5.2)")]
    public async Task SendAsync_WithBranding_DoesNotInjectArbitraryCss()
    {
        var sender = new CapturingSender();
        var decorator = CreateDecorator(sender);
        // BrandingConfig só aceita hex para cores e URL para logo — não há campo para CSS arbitrário
        var message = MessageWithBranding(primaryColor: "#123456", secondaryColor: "#ABCDEF");

        await decorator.SendAsync(message);

        // Verificar que apenas os valores injetáveis (hex) aparecem — sem style arbitrário
        sender.LastHtml.Should().NotContain("javascript:",
            because: "CSS/JS arbitrário não deve ser injetado via branding (Req 5.2)");
        sender.LastHtml.Should().NotContain("expression(",
            because: "expressões CSS arbitrárias não devem ser injetadas (Req 5.2)");
    }

    // -------------------------------------------------------------------------
    // Resultado propagado corretamente
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-09: resultado do sender interno é propagado pelo decorator")]
    public async Task SendAsync_Always_PropagatesInnerSenderResult()
    {
        var sender = new CapturingSender();
        var decorator = CreateDecorator(sender);
        var message = MessageWithBranding();

        var result = await decorator.SendAsync(message);

        result.Should().NotBeNull();
        result.Status.Should().Be(SendStatus.Sent);
        result.CorrelationId.Should().Be(message.CorrelationId);
    }

    // -------------------------------------------------------------------------
    // CheckAvailabilityAsync delega ao sender interno
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-09: CheckAvailabilityAsync delega ao sender interno")]
    public async Task CheckAvailabilityAsync_DelegatesToInnerSender()
    {
        var sender = new CapturingSender();
        var decorator = CreateDecorator(sender);

        var result = await decorator.CheckAvailabilityAsync();

        result.Status.Should().Be(HealthStatus.Healthy);
    }
}
