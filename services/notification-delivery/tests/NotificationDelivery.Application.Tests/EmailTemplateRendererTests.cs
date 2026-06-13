using FluentAssertions;
using NotificationDelivery.Application.Rendering;
using NotificationDelivery.Contracts;
using Xunit;

namespace NotificationDelivery.Application.Tests;

/// <summary>
/// Testes para <see cref="EmailTemplateRenderer"/>.
///
/// Cobre renderização determinística (Req 6.4), preservação de links de 1 clique (Req 6.2),
/// derivação de plaintext (Req 6.3) e indicador de responsividade (Req 6.1, DD-006).
/// </summary>
public sealed class EmailTemplateRendererTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static EmailTemplateRenderer CreateRenderer() => new();

    private static EmailMessage ValidMessage(
        string htmlBody = "<p>Corpo do e-mail de teste.</p>",
        string? plainTextBody = null,
        BrandingConfig? branding = null) =>
        new(
            recipientEmail: "user@example.com",
            subject: "Assunto de teste",
            htmlBody: htmlBody,
            tenantId: "tenant-1",
            correlationId: "corr-1",
            plainTextBody: plainTextBody,
            branding: branding);

    // -------------------------------------------------------------------------
    // ST-01a — Determinismo: mesma entrada → mesma saída byte-a-byte
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-08/ST-01a: mesma mensagem renderizada duas vezes produz HTML idêntico (Req 6.4)")]
    public void Render_SameInput_ProducesIdenticalHtmlOutput()
    {
        var renderer = CreateRenderer();
        var message = ValidMessage();

        var result1 = renderer.Render(message);
        var result2 = renderer.Render(message);

        result1.Html.Should().Be(result2.Html,
            because: "renderização deve ser determinística — mesma entrada, mesma saída (Req 6.4)");
    }

    [Fact(DisplayName = "TASK-08/ST-01a: mesma mensagem com branding renderizada duas vezes produz HTML idêntico")]
    public void Render_SameInputWithBranding_ProducesIdenticalHtmlOutput()
    {
        var renderer = CreateRenderer();
        var branding = new BrandingConfig("https://logo.example.com/logo.png", "#0F4C81", "#FFFFFF");
        var message = ValidMessage(branding: branding);

        var result1 = renderer.Render(message);
        var result2 = renderer.Render(message);

        result1.Html.Should().Be(result2.Html,
            because: "renderização com branding deve ser determinística (Req 6.4)");
    }

    // -------------------------------------------------------------------------
    // ST-01b — Preservação de links de 1 clique (Req 6.2)
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-08/ST-01b: link de 1 clique preservado literalmente no output HTML (Req 6.2)")]
    public void Render_HtmlWithOneClickLink_PreservesLinkLiterally()
    {
        var renderer = CreateRenderer();
        const string oneClickLink = "https://app.azim.com.br/digest?token=abc123";
        var message = ValidMessage(htmlBody: $"<p>Clique aqui: <a href=\"{oneClickLink}\">acessar</a></p>");

        var result = renderer.Render(message);

        result.Html.Should().Contain(oneClickLink,
            because: "links de 1 clique fornecidos pelo chamador devem ser preservados literalmente (Req 6.2)");
    }

    [Fact(DisplayName = "TASK-08/ST-01b: link com query string complexa preservado sem alteração")]
    public void Render_HtmlWithComplexQueryStringLink_PreservesLinkUnchanged()
    {
        var renderer = CreateRenderer();
        const string complexLink = "https://app.azim.com.br/unsubscribe?token=xyz&userId=123&lang=pt-BR";
        var message = ValidMessage(htmlBody: $"<a href=\"{complexLink}\">Descadastrar</a>");

        var result = renderer.Render(message);

        result.Html.Should().Contain(complexLink,
            because: "links com query string complexa devem ser preservados sem codificação ou truncamento (Req 6.2)");
    }

    // -------------------------------------------------------------------------
    // ST-01c — Derivação de PlainText quando ausente (Req 6.3)
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-08/ST-01c: PlainTextBody derivado quando ausente na mensagem de entrada (Req 6.3)")]
    public void Render_MessageWithoutPlainText_DerivesPlainTextFromHtml()
    {
        var renderer = CreateRenderer();
        var message = ValidMessage(htmlBody: "<p>Texto do corpo aqui.</p>", plainTextBody: null);

        var result = renderer.Render(message);

        result.PlainText.Should().NotBeNullOrWhiteSpace(
            because: "PlainTextBody deve ser derivado do HTML quando ausente (Req 6.3)");
    }

    [Fact(DisplayName = "TASK-08/ST-01c: PlainTextBody fornecido é preservado no output")]
    public void Render_MessageWithPlainText_PreservesExistingPlainText()
    {
        var renderer = CreateRenderer();
        const string plainText = "Texto puro já fornecido pelo chamador.";
        var message = ValidMessage(plainTextBody: plainText);

        var result = renderer.Render(message);

        result.PlainText.Should().Contain(plainText,
            because: "PlainTextBody fornecido pelo chamador deve ser preservado");
    }

    // -------------------------------------------------------------------------
    // ST-01d — Indicador de responsividade (Req 6.1, DD-006)
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-08/ST-01d: HTML renderizado contém meta viewport para responsividade (Req 6.1)")]
    public void Render_Always_ProducesHtmlWithViewportMeta()
    {
        var renderer = CreateRenderer();
        var message = ValidMessage();

        var result = renderer.Render(message);

        result.Html.Should().Contain("<meta name=\"viewport\"",
            because: "template responsivo deve incluir meta viewport (Req 6.1, DD-006)");
    }

    // -------------------------------------------------------------------------
    // Verificações de conteúdo do HTML
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-08: HTML renderizado contém o HtmlBody original")]
    public void Render_Always_ContainsOriginalHtmlBody()
    {
        var renderer = CreateRenderer();
        const string body = "<p>Conteúdo do digest para o usuário.</p>";
        var message = ValidMessage(htmlBody: body);

        var result = renderer.Render(message);

        result.Html.Should().Contain(body,
            because: "o corpo HTML original deve ser incorporado no template renderizado");
    }

    [Fact(DisplayName = "TASK-08: PlainText derivado não contém tags HTML")]
    public void Render_DerivedPlainText_DoesNotContainHtmlTags()
    {
        var renderer = CreateRenderer();
        var message = ValidMessage(htmlBody: "<h1>Título</h1><p>Parágrafo de corpo.</p>");

        var result = renderer.Render(message);

        result.PlainText.Should().NotContain("<h1>")
            .And.NotContain("<p>")
            .And.NotContain("</p>");
    }

    // -------------------------------------------------------------------------
    // Branding aplicado no template
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-08: branding presente → cores injetadas no HTML")]
    public void Render_WithBranding_InjectsColorsIntoHtml()
    {
        var renderer = CreateRenderer();
        var branding = new BrandingConfig("https://logo.example.com/logo.png", "#1A2B3C", "#FFFFFF");
        var message = ValidMessage(branding: branding);

        var result = renderer.Render(message);

        result.Html.Should().Contain("#1A2B3C",
            because: "a cor primária do branding deve ser injetada no template HTML");
    }

    [Fact(DisplayName = "TASK-08: branding ausente → tema padrão usado sem lançar exceção")]
    public void Render_WithoutBranding_UsesDefaultThemeWithoutThrowing()
    {
        var renderer = CreateRenderer();
        var message = ValidMessage(branding: null);

        var act = () => renderer.Render(message);

        act.Should().NotThrow(
            because: "ausência de branding deve usar tema padrão sem falha (Req 5.3)");
    }

    // -------------------------------------------------------------------------
    // Resultado tipado
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-08: resultado de renderização possui Html e PlainText não nulos")]
    public void Render_Always_ReturnsBothHtmlAndPlainText()
    {
        var renderer = CreateRenderer();
        var message = ValidMessage();

        var result = renderer.Render(message);

        result.Should().NotBeNull();
        result.Html.Should().NotBeNullOrWhiteSpace();
        result.PlainText.Should().NotBeNullOrWhiteSpace();
    }
}
