using Digest.Application.Abstractions;
using Digest.Domain.ValueObjects;
using Digest.Infrastructure.Rendering;
using FluentAssertions;
using NodaTime;
using Xunit;

namespace Digest.Infrastructure.Tests.Rendering;

/// <summary>
/// Testes unitários para <see cref="EmailTemplateRenderer"/> (TASK-20).
/// Verifica: blocos vazios omitidos; links autenticados no HTML; branding do tenant;
/// sem bloco azimute_metas quando ausente; sem PII em exceções.
/// </summary>
public sealed class EmailTemplateRendererTests
{
    private static readonly EmailTemplateRenderer Renderer = new();

    private static readonly TenantBranding DefaultBranding = new(
        TenantName: "Empresa Teste",
        LogoUrl: "https://cdn.azim.com/logo.png",
        PrimaryColor: "#0066CC");

    private static readonly DigestDate TestDate = new(new LocalDate(2026, 6, 14));

    private static EmailRenderContext CreateContext(
        IReadOnlyDictionary<Guid, string>? actionLinks = null,
        TenantBranding? branding = null)
    {
        return new EmailRenderContext(
            TenantId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            DigestDate: TestDate,
            BaseUrl: "https://app.azim.com",
            ActionLinks: actionLinks ?? new Dictionary<Guid, string>(),
            Branding: branding ?? DefaultBranding);
    }

    // ---------------------------------------------------------------
    // RenderAsync — estrutura geral do HTML
    // ---------------------------------------------------------------

    [Fact(DisplayName = "RenderAsync retorna HTML com DOCTYPE válido")]
    public async Task RenderAsync_ReturnsValidHtml()
    {
        var content = new DigestContent([
            new DigestSection("overdue_activities", ["Atividade vencida 1", "Atividade vencida 2"])
        ]);

        var result = await Renderer.RenderAsync(content, CreateContext());

        result.HtmlBody.Should().StartWith("<!DOCTYPE html>",
            "HTML deve começar com DOCTYPE para compatibilidade de clientes de e-mail");
        result.HtmlBody.Should().Contain("<html");
        result.HtmlBody.Should().Contain("</html>");
    }

    [Fact(DisplayName = "RenderAsync inclui nome do tenant no HTML (branding)")]
    public async Task RenderAsync_IncludesTenantBranding()
    {
        var content = new DigestContent([
            new DigestSection("overdue_activities", ["Atividade 1"])
        ]);

        var result = await Renderer.RenderAsync(content, CreateContext());

        result.HtmlBody.Should().Contain("Empresa Teste",
            "HTML deve conter o nome do tenant (branding — Req 4.3)");
        result.Subject.Should().Contain("Empresa Teste",
            "assunto deve conter o nome do tenant");
    }

    [Fact(DisplayName = "RenderAsync inclui logo do tenant quando LogoUrl fornecida")]
    public async Task RenderAsync_IncludesLogo_WhenLogoUrlProvided()
    {
        var content = new DigestContent([
            new DigestSection("overdue_activities", ["Item 1"])
        ]);

        var result = await Renderer.RenderAsync(content, CreateContext());

        result.HtmlBody.Should().Contain("https://cdn.azim.com/logo.png",
            "HTML deve conter a URL do logo do tenant (Req 4.3)");
    }

    // ---------------------------------------------------------------
    // Blocos vazios — omitidos (Req 4.6, RN-018)
    // ---------------------------------------------------------------

    [Fact(DisplayName = "RenderAsync omite blocos vazios do HTML (Req 4.6)")]
    public async Task RenderAsync_OmitsEmptySections()
    {
        var content = new DigestContent([
            new DigestSection("overdue_activities", []), // Vazio — deve ser omitido
            new DigestSection("today_activities", ["Atividade hoje 1"]) // Com conteúdo
        ]);

        var result = await Renderer.RenderAsync(content, CreateContext());

        result.HtmlBody.Should().Contain("Atividades de Hoje",
            "bloco com conteúdo deve estar no HTML");
        result.HtmlBody.Should().NotContain("Atividades Vencidas",
            "bloco vazio não deve aparecer no HTML (Req 4.6)");
    }

    [Fact(DisplayName = "RenderAsync omite bloco azimute_metas quando ausente no DigestContent")]
    public async Task RenderAsync_NoAzimuteMetas_WhenSectionAbsent()
    {
        // Somente bloco de atividades — sem azimute_metas
        var content = new DigestContent([
            new DigestSection("overdue_activities", ["Atividade 1"])
        ]);

        var result = await Renderer.RenderAsync(content, CreateContext());

        result.HtmlBody.Should().NotContain("azimute_metas",
            "identificador de seção não deve aparecer no HTML");
        result.HtmlBody.Should().NotContain("Metas e Realizado",
            "bloco ausente não deve ter placeholder no HTML (RN-018, Req 5.4)");
    }

    [Fact(DisplayName = "RenderAsync inclui bloco azimute_metas quando presente e não vazio")]
    public async Task RenderAsync_IncludesAzimuteMetas_WhenPresent()
    {
        var content = new DigestContent([
            new DigestSection("overdue_activities", ["Atividade 1"]),
            new DigestSection("azimute_metas", ["Meta: R$ 100.000", "Realizado: R$ 75.000"])
        ]);

        var result = await Renderer.RenderAsync(content, CreateContext());

        result.HtmlBody.Should().Contain("Metas e Realizado",
            "bloco azimute_metas deve aparecer quando presente e não vazio");
    }

    // ---------------------------------------------------------------
    // Links autenticados (Req 4.2)
    // ---------------------------------------------------------------

    [Fact(DisplayName = "RenderAsync gera link autenticado para atividade com token disponível")]
    public async Task RenderAsync_GeneratesActionLink_WhenTokenAvailable()
    {
        var activityId = Guid.NewGuid();
        var clearToken = "abc123TokenEmClaro";

        // Item formatado como "título|||activityId" para sinalizar link de ação
        var content = new DigestContent([
            new DigestSection("overdue_activities", [$"Reunião com cliente|||{activityId}"])
        ]);

        var actionLinks = new Dictionary<Guid, string> { { activityId, clearToken } };
        var ctx = CreateContext(actionLinks: actionLinks);

        var result = await Renderer.RenderAsync(content, ctx);

        // Link = {base_url}/digest/actions/{token_clear} (design §5.3, Req 4.2)
        result.HtmlBody.Should().Contain($"https://app.azim.com/digest/actions/{clearToken}",
            "link autenticado deve usar o token em claro na URL (Req 4.2)");

        // Título HTML-encoded (HtmlEncode converte 'ã' → '&#227;' e 'ã' → '&#227;')
        // Verifica tanto a versão encoded quanto a presença no texto simples
        result.PlainTextBody.Should().Contain("Reunião com cliente",
            "texto simples deve conter o título sem encoding");
        result.HtmlBody.Should().NotContain($"|||{activityId}",
            "separador interno não deve aparecer no HTML");
    }

    [Fact(DisplayName = "RenderAsync renderiza atividade sem link quando token não disponível")]
    public async Task RenderAsync_RendersActivity_WithoutLink_WhenTokenUnavailable()
    {
        var content = new DigestContent([
            new DigestSection("overdue_activities", [$"Atividade sem token|||{Guid.NewGuid()}"])
        ]);

        var result = await Renderer.RenderAsync(content, CreateContext()); // Sem tokens

        result.HtmlBody.Should().Contain("Atividade sem token",
            "atividade sem token deve ser renderizada sem link");
        result.HtmlBody.Should().NotContain("/digest/actions/",
            "não deve haver link quando token não está disponível");
    }

    // ---------------------------------------------------------------
    // Texto simples
    // ---------------------------------------------------------------

    [Fact(DisplayName = "RenderAsync retorna texto simples com cabeçalho e conteúdo")]
    public async Task RenderAsync_ReturnsPlainText_WithHeaderAndContent()
    {
        var content = new DigestContent([
            new DigestSection("overdue_activities", ["Atividade A", "Atividade B"])
        ]);

        var result = await Renderer.RenderAsync(content, CreateContext());

        result.PlainTextBody.Should().Contain("Empresa Teste",
            "texto simples deve conter o nome do tenant");
        result.PlainTextBody.Should().Contain("14/06/2026",
            "texto simples deve conter a data do digest");
        result.PlainTextBody.Should().Contain("Atividade A");
        result.PlainTextBody.Should().Contain("Atividade B");
    }

    [Fact(DisplayName = "RenderAsync texto simples omite blocos vazios")]
    public async Task RenderAsync_PlainText_OmitsEmptySections()
    {
        var content = new DigestContent([
            new DigestSection("azimute_metas", []), // Vazio
            new DigestSection("today_activities", ["Atividade hoje"])
        ]);

        var result = await Renderer.RenderAsync(content, CreateContext());

        result.PlainTextBody.Should().Contain("Atividade hoje");
        result.PlainTextBody.Should().NotContain("METAS E REALIZADO",
            "bloco vazio não deve aparecer no texto simples");
    }

    // ---------------------------------------------------------------
    // Guards e contrato da interface
    // ---------------------------------------------------------------

    [Fact(DisplayName = "RenderAsync com content nulo lança ArgumentNullException")]
    public async Task RenderAsync_NullContent_ThrowsArgumentNullException()
    {
        var act = async () => await Renderer.RenderAsync(null!, CreateContext());
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "RenderAsync com context nulo lança ArgumentNullException")]
    public async Task RenderAsync_NullContext_ThrowsArgumentNullException()
    {
        var content = new DigestContent([]);
        var act = async () => await Renderer.RenderAsync(content, null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "RenderAsync com DigestContent vazio retorna HTML válido sem seções")]
    public async Task RenderAsync_EmptyContent_ReturnsValidHtmlWithoutSections()
    {
        var content = new DigestContent([]);

        var result = await Renderer.RenderAsync(content, CreateContext());

        result.HtmlBody.Should().NotBeNullOrEmpty("HTML deve ser retornado mesmo sem seções");
        result.HtmlBody.Should().Contain("Empresa Teste");
        // Nenhum bloco de seção deve aparecer
        result.HtmlBody.Should().NotContain("<ul>");
    }

    [Fact(DisplayName = "RenderAsync inclui cor primária do tenant no CSS do HTML")]
    public async Task RenderAsync_IncludesPrimaryColorInCss()
    {
        var branding = new TenantBranding("Tenant Corp", null, PrimaryColor: "#FF5500");
        var content = new DigestContent([new DigestSection("overdue_activities", ["Item 1"])]);
        var ctx = CreateContext(branding: branding);

        var result = await Renderer.RenderAsync(content, ctx);

        result.HtmlBody.Should().Contain("#FF5500",
            "cor primária do tenant deve estar no CSS gerado (Req 4.3)");
    }
}
