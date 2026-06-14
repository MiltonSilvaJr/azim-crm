using System.Text;
using Digest.Application.Abstractions;
using Digest.Domain.ValueObjects;
using NodaTime;
using NodaTime.Text;

namespace Digest.Infrastructure.Rendering;

/// <summary>
/// Implementação de <see cref="IEmailTemplateRenderer"/> para o digest de e-mail (TASK-20).
/// Renderiza <see cref="DigestContent"/> como HTML responsivo + texto simples com branding do tenant.
/// MVP: template funcional sem Razor/Scriban — usa StringBuilder diretamente.
/// Nenhum conteúdo do digest é logado em nenhum nível (RNF 3.2, DD-011).
/// </summary>
public sealed class EmailTemplateRenderer : IEmailTemplateRenderer
{
    private const string DefaultPrimaryColor = "#0066CC";

    /// <inheritdoc/>
    public Task<RenderedEmail> RenderAsync(
        DigestContent content,
        EmailRenderContext ctx,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(ctx);

        var subject = BuildSubject(ctx);
        var html = BuildHtml(content, ctx);
        var plain = BuildPlainText(content, ctx);

        // Nenhuma variável de conteúdo é logada aqui (RNF 3.2, DD-011)
        return Task.FromResult(new RenderedEmail(
            HtmlBody: html,
            PlainTextBody: plain,
            Subject: subject));
    }

    // ---------------------------------------------------------------
    // Subject
    // ---------------------------------------------------------------

    private static readonly LocalDatePattern ShortDatePattern =
        LocalDatePattern.CreateWithCurrentCulture("dd/MM/yyyy");

    private static readonly LocalDatePattern LongDatePattern =
        LocalDatePattern.CreateWithCurrentCulture("dd MMMM yyyy");

    private static string BuildSubject(EmailRenderContext ctx)
    {
        var date = ctx.DigestDate.Value;
        return $"Seu digest da {ctx.Branding.TenantName} — {ShortDatePattern.Format(date)}";
    }

    // ---------------------------------------------------------------
    // HTML
    // ---------------------------------------------------------------

    private static string BuildHtml(DigestContent content, EmailRenderContext ctx)
    {
        var sb = new StringBuilder();
        var color = string.IsNullOrWhiteSpace(ctx.Branding.PrimaryColor)
            ? DefaultPrimaryColor
            : ctx.Branding.PrimaryColor;

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"pt-BR\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\" />");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />");
        sb.AppendLine($"  <title>{Encode(ctx.Branding.TenantName)} — Digest</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    body { font-family: Arial, sans-serif; background: #f4f4f4; margin: 0; padding: 0; }");
        sb.AppendLine("    .container { max-width: 600px; margin: 32px auto; background: #fff; border-radius: 8px; padding: 32px; }");
        sb.AppendLine($"   .header {{ background: {Encode(color)}; color: #fff; padding: 20px 32px; border-radius: 8px 8px 0 0; }}");
        sb.AppendLine("    .section { margin-bottom: 24px; }");
        sb.AppendLine("    .section h2 { color: #333; font-size: 16px; border-bottom: 1px solid #eee; padding-bottom: 8px; }");
        sb.AppendLine("    ul { padding-left: 16px; }");
        sb.AppendLine("    li { margin-bottom: 8px; }");
        sb.AppendLine($"   a {{ color: {Encode(color)}; text-decoration: none; }}");
        sb.AppendLine("    .footer { font-size: 12px; color: #999; text-align: center; margin-top: 32px; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"container\">");

        // Header com logo e branding
        sb.AppendLine("    <div class=\"header\">");
        if (!string.IsNullOrWhiteSpace(ctx.Branding.LogoUrl))
        {
            sb.AppendLine($"      <img src=\"{Encode(ctx.Branding.LogoUrl)}\" alt=\"{Encode(ctx.Branding.TenantName)}\" height=\"40\" style=\"margin-bottom:8px;\" /><br />");
        }
        sb.AppendLine($"      <strong>Digest da {Encode(ctx.Branding.TenantName)}</strong>");
        sb.AppendLine($"      <br /><small>{LongDatePattern.Format(ctx.DigestDate.Value)}</small>");
        sb.AppendLine("    </div>");

        // Blocos de conteúdo — somente quando não vazio (Req 4.6, RN-018)
        foreach (var section in content.Sections)
        {
            if (section.IsEmpty())
                continue; // Bloco vazio não é renderizado (Req 4.6)

            sb.AppendLine("    <div class=\"section\">");
            sb.AppendLine($"      <h2>{Encode(SectionTitle(section.Key))}</h2>");
            sb.AppendLine("      <ul>");

            foreach (var item in section.Items)
            {
                // Item pode conter link autenticado (action token) se a seção for de atividades
                // O link é formatado como "título|||activityId" quando há token disponível
                var (text, href) = ParseActionItem(item, section.Key, ctx);
                if (href is not null)
                {
                    sb.AppendLine($"        <li>{Encode(text)} — <a href=\"{Encode(href)}\">Concluir</a></li>");
                }
                else
                {
                    sb.AppendLine($"        <li>{Encode(text)}</li>");
                }
            }

            sb.AppendLine("      </ul>");
            sb.AppendLine("    </div>");
        }

        sb.AppendLine("    <div class=\"footer\">");
        sb.AppendLine($"      <p>Este e-mail foi enviado por {Encode(ctx.Branding.TenantName)}.</p>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    // ---------------------------------------------------------------
    // Texto simples
    // ---------------------------------------------------------------

    private static string BuildPlainText(DigestContent content, EmailRenderContext ctx)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Digest da {ctx.Branding.TenantName}");
        sb.AppendLine($"Data: {ShortDatePattern.Format(ctx.DigestDate.Value)}");
        sb.AppendLine(new string('-', 40));

        foreach (var section in content.Sections)
        {
            if (section.IsEmpty())
                continue;

            sb.AppendLine();
            sb.AppendLine(SectionTitle(section.Key).ToUpperInvariant());

            foreach (var item in section.Items)
            {
                var (text, href) = ParseActionItem(item, section.Key, ctx);
                if (href is not null)
                    sb.AppendLine($"  - {text} [{href}]");
                else
                    sb.AppendLine($"  - {text}");
            }
        }

        sb.AppendLine();
        sb.AppendLine(new string('-', 40));
        sb.AppendLine($"Enviado por {ctx.Branding.TenantName}.");

        return sb.ToString();
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    /// <summary>
    /// Escapa HTML para prevenir XSS nos dados de branding e conteúdo do tenant.
    /// </summary>
    private static string Encode(string value)
        => System.Net.WebUtility.HtmlEncode(value);

    /// <summary>
    /// Título legível para cada seção canônica do digest (design §4.2).
    /// </summary>
    private static string SectionTitle(string key) => key switch
    {
        "overdue_activities" => "Atividades Vencidas",
        "today_activities" => "Atividades de Hoje",
        "stale_opportunities" => "Oportunidades Estagnadas",
        "overdue_closings" => "Fechamentos Vencidos",
        "weighted_pipeline" => "Pipeline Ponderado",
        "azimute_metas" => "Metas e Realizado",
        _ => key
    };

    /// <summary>
    /// Extrai texto e link de um item de atividade formatado como "título|||activityId".
    /// Retorna (texto, null) quando não há token disponível para o item.
    /// O separador "|||" não pode ocorrer em títulos normais de atividade.
    /// </summary>
    private static (string Text, string? Href) ParseActionItem(
        string item, string sectionKey, EmailRenderContext ctx)
    {
        // Somente seções de atividades possuem links de ação (Req 4.2)
        if (sectionKey is not ("overdue_activities" or "today_activities"))
            return (item, null);

        const string separator = "|||";
        var idx = item.IndexOf(separator, StringComparison.Ordinal);
        if (idx < 0)
            return (item, null);

        var title = item[..idx];
        var activityIdStr = item[(idx + separator.Length)..];

        if (!Guid.TryParse(activityIdStr, out var activityId))
            return (item, null);

        if (!ctx.ActionLinks.TryGetValue(activityId, out var clearToken))
            return (title, null);

        // Link autenticado: {base_url}/digest/actions/{token_clear} (design §5.3, Req 4.2)
        // Token em claro é usado APENAS para construção do link — nunca logado (DD-011)
        var href = $"{ctx.BaseUrl.TrimEnd('/')}/digest/actions/{clearToken}";
        return (title, href);
    }
}
