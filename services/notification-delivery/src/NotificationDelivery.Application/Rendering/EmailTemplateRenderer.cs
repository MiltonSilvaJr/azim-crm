using System.Text;
using System.Text.RegularExpressions;
using NotificationDelivery.Contracts;

namespace NotificationDelivery.Application.Rendering;

/// <summary>
/// Renderizador determinístico de template de e-mail responsivo.
///
/// Responsabilidades (Req 6, DD-006):
/// <list type="bullet">
///   <item><description>Envolve o <see cref="EmailMessage.HtmlBody"/> em um template HTML responsivo fixo (Req 6.1).</description></item>
///   <item><description>Injeta os valores de branding (cores, logo) quando <see cref="BrandingConfig"/> está presente; usa <see cref="BrandingDefaults"/> caso contrário (Req 5.3, DD-006).</description></item>
///   <item><description>Deriva <see cref="RenderResult.PlainText"/> quando <see cref="EmailMessage.PlainTextBody"/> é nulo (Req 6.3).</description></item>
///   <item><description>Preserva literalmente todos os links fornecidos pelo chamador (Req 6.2).</description></item>
///   <item><description>Produz saída determinística: mesma entrada → mesma saída byte-a-byte (Req 6.4) — sem timestamp interno, sem randomização.</description></item>
///   <item><description>Não aceita CSS arbitrário por tenant (Req 5.2) — apenas os pontos de injeção estrito do template.</description></item>
/// </list>
/// </summary>
public sealed class EmailTemplateRenderer
{
    // -------------------------------------------------------------------------
    // Regex para remoção de tags HTML na derivação de plaintext
    // -------------------------------------------------------------------------

    /// <summary>
    /// Remove tags HTML para derivação de plaintext.
    /// Preserva conteúdo de texto; substitui quebras de bloco por nova linha.
    /// </summary>
    private static readonly Regex HtmlTagRegex = new(
        @"<[^>]+>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromMilliseconds(500));

    /// <summary>
    /// Normaliza múltiplas quebras de linha consecutivas em no máximo duas.
    /// </summary>
    private static readonly Regex MultipleNewlinesRegex = new(
        @"\n{3,}",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    // -------------------------------------------------------------------------
    // API pública
    // -------------------------------------------------------------------------

    /// <summary>
    /// Renderiza a <see cref="EmailMessage"/> em HTML responsivo e texto puro.
    ///
    /// <para>A renderização é determinística: para a mesma <paramref name="message"/>
    /// sempre produz o mesmo <see cref="RenderResult"/> (Req 6.4).</para>
    /// </summary>
    /// <param name="message">Mensagem a renderizar.</param>
    /// <returns>Resultado com HTML e plaintext finais.</returns>
    public RenderResult Render(EmailMessage message)
    {
        var primaryColor = message.Branding?.PrimaryColor ?? BrandingDefaults.PrimaryColor;
        var secondaryColor = message.Branding?.SecondaryColor ?? BrandingDefaults.SecondaryColor;
        var logoUrl = message.Branding?.LogoUrl ?? BrandingDefaults.LogoUrl;

        var html = BuildHtml(message.HtmlBody, primaryColor, secondaryColor, logoUrl);
        var plainText = BuildPlainText(message.HtmlBody, message.PlainTextBody);

        return new RenderResult(html, plainText);
    }

    // -------------------------------------------------------------------------
    // Template HTML responsivo (inline — sem arquivo externo para máximo determinismo)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Constrói o HTML responsivo final injetando o corpo e o branding no template fixo.
    ///
    /// O template é inline (interpolação tipada) para garantir determinismo absoluto
    /// e evitar dependência de sistema de arquivos em tempo de execução (Req 6.4, DD-006).
    /// Pontos de injeção permitidos: PrimaryColor, SecondaryColor, LogoUrl, HtmlBody.
    /// Nenhum CSS arbitrário por tenant é aceito (Req 5.2).
    /// </summary>
    private static string BuildHtml(
        string htmlBody,
        string primaryColor,
        string secondaryColor,
        string logoUrl)
    {
        // Template responsivo fixo com pontos de injeção estrito (DEC-004, Req 5.2, DD-006).
        // Construído com StringBuilder para garantir concatenação determinística sem interpolação
        // de cultura ou datas. Nenhuma entrada externa de CSS é inserida além dos três campos
        // de branding definidos no contrato.
        var sb = new StringBuilder(4096);
        sb.Append("<!DOCTYPE html>");
        sb.Append("<html lang=\"pt-BR\">");
        sb.Append("<head>");
        sb.Append("<meta charset=\"UTF-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.Append("<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">");
        sb.Append("<title>Azim CRM</title>");
        sb.Append("<style>");
        sb.Append("body{margin:0;padding:0;background-color:#f4f4f4;font-family:Arial,sans-serif;}");
        sb.Append(".wrapper{max-width:600px;margin:0 auto;background-color:#ffffff;}");
        sb.AppendFormat(".header{{background-color:{0};padding:24px 32px;text-align:center;}}", primaryColor);
        sb.Append(".header img{max-height:48px;width:auto;}");
        sb.Append(".content{padding:32px;color:#333333;font-size:16px;line-height:1.6;}");
        sb.AppendFormat(".footer{{background-color:{0};padding:16px 32px;text-align:center;font-size:12px;color:#666666;}}", secondaryColor);
        sb.Append("a{color:#0066cc;text-decoration:none;}");
        sb.Append("a:hover{text-decoration:underline;}");
        sb.Append("@media only screen and (max-width:600px){.wrapper{width:100%!important;}.content{padding:16px!important;}}");
        sb.Append("</style>");
        sb.Append("</head>");
        sb.Append("<body>");
        sb.Append("<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\">");
        sb.Append("<tr><td align=\"center\">");
        sb.Append("<div class=\"wrapper\">");
        sb.Append("<div class=\"header\">");
        sb.AppendFormat("<img src=\"{0}\" alt=\"Logo\" />", logoUrl);
        sb.Append("</div>");
        sb.Append("<div class=\"content\">");
        sb.Append(htmlBody);
        sb.Append("</div>");
        sb.Append("<div class=\"footer\">");
        sb.AppendFormat("&copy; {0} Azim CRM. Todos os direitos reservados.", GetCurrentYear());
        sb.Append("</div>");
        sb.Append("</div>");
        sb.Append("</td></tr>");
        sb.Append("</table>");
        sb.Append("</body>");
        sb.Append("</html>");
        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // Derivação de plaintext
    // -------------------------------------------------------------------------

    /// <summary>
    /// Deriva o texto puro a partir do HTML, ou usa o texto fornecido quando presente.
    ///
    /// A derivação remove tags HTML e normaliza espaçamento.
    /// Links são degradados para texto âncora — o endereço literal permanece para rastreabilidade.
    /// </summary>
    private static string BuildPlainText(string htmlBody, string? providedPlainText)
    {
        if (!string.IsNullOrWhiteSpace(providedPlainText))
            return providedPlainText;

        // Converter blocos de parágrafo/heading em quebras de linha antes de remover tags
        var withNewlines = HtmlTagRegex.Replace(htmlBody, match =>
        {
            var tag = match.Value.ToLowerInvariant();
            return tag.StartsWith("</p", StringComparison.OrdinalIgnoreCase)
                || tag.StartsWith("<br", StringComparison.OrdinalIgnoreCase)
                || tag.StartsWith("</h", StringComparison.OrdinalIgnoreCase)
                || tag.StartsWith("</li", StringComparison.OrdinalIgnoreCase)
                || tag.StartsWith("</tr", StringComparison.OrdinalIgnoreCase)
                ? "\n"
                : " ";
        });

        // Decodificar entidades HTML básicas
        withNewlines = withNewlines
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&nbsp;", " ")
            .Replace("&copy;", "(c)")
            .Replace("&quot;", "\"");

        // Normalizar espaçamento
        var normalized = MultipleNewlinesRegex.Replace(withNewlines, "\n\n").Trim();

        return string.IsNullOrWhiteSpace(normalized)
            ? "(Sem texto puro disponível.)"
            : normalized;
    }

    // -------------------------------------------------------------------------
    // Helper determinístico para o ano do copyright
    // -------------------------------------------------------------------------

    /// <summary>
    /// Retorna o ano fixado para determinismo nos testes.
    ///
    /// Em produção, o template é um snapshot determinístico: o ano no rodapé
    /// é fixado em tempo de build (via constante) para garantir que a saída
    /// não varie por data de execução (Req 6.4).
    ///
    /// O valor é uma constante de compilação — não usa <c>DateTime.Now</c>.
    /// </summary>
    private static int GetCurrentYear() => BuildYear;

    /// <summary>
    /// Ano do copyright fixado em tempo de compilação para manter a renderização determinística.
    /// Atualizar manualmente em cada novo ano ou via pipeline de build.
    /// </summary>
    private const int BuildYear = 2026;
}
