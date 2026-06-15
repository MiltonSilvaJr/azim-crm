using System.Text.RegularExpressions;

namespace PartnerManagement.Infrastructure.Audit;

/// <summary>
/// Serviço de mascaramento de PII do módulo partner-management.
/// Garante que dados de contato (<c>contact_email</c>, <c>contact_phone</c>) nunca apareçam
/// em claro em logs, traces, payloads de evento ou <c>delta_json</c> de auditoria (RNF 4).
/// <c>partner.name</c> <strong>não é PII</strong> por decisão VAL-PARTNER-01 (2026-06-15):
/// não é mascarado e sua presença em claro não constitui violação.
/// Deve ser aplicado antes de qualquer escrita em log, Outbox ou <c>audit_logs</c>.
/// Mapeia: RNF 4, design §6.6, design §11, TASK-20.
/// </summary>
public sealed class PartnerPiiMasker
{
    // Padrão simples de e-mail para detecção em texto livre
    private static readonly Regex EmailPattern =
        new(@"[^@\s]+@[^@\s]+\.[^@\s]+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase,
            TimeSpan.FromMilliseconds(250));

    /// <summary>Representação mascarada de e-mail de contato.</summary>
    public const string MaskedEmail = "[e-mail mascarado]";

    /// <summary>Representação mascarada de telefone de contato.</summary>
    public const string MaskedPhone = "[telefone mascarado]";

    /// <summary>
    /// Mascara o endereço de e-mail.
    /// </summary>
    /// <param name="email">E-mail original.</param>
    /// <returns>Representação mascarada.</returns>
    public string MaskEmail(string? email) => MaskedEmail;

    /// <summary>
    /// Mascara o número de telefone.
    /// </summary>
    /// <param name="phone">Telefone original.</param>
    /// <returns>Representação mascarada.</returns>
    public string MaskPhone(string? phone) => MaskedPhone;

    /// <summary>
    /// Aplica mascaramento em um JSON que possa conter campos PII de parceiro.
    /// Substitui valores dos campos <c>contact_email</c>, <c>contact_phone</c>
    /// e padrões de e-mail detectados no texto.
    /// O campo <c>name</c> <strong>não</strong> é mascarado (VAL-PARTNER-01).
    /// Usado para <c>delta_json</c> de auditoria e payloads de Outbox.
    /// </summary>
    /// <param name="json">JSON original (pode conter PII de contato).</param>
    /// <returns>JSON com PII de contato mascarada.</returns>
    public string MaskJson(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return json;
        }

        // Mascara campo "contact_email"
        string result = Regex.Replace(
            json,
            @"""contact_email""\s*:\s*""[^""]*""",
            $@"""contact_email"":""{MaskedEmail}""",
            RegexOptions.None,
            TimeSpan.FromMilliseconds(250));

        // Mascara campo "contact_phone"
        result = Regex.Replace(
            result,
            @"""contact_phone""\s*:\s*""[^""]*""",
            $@"""contact_phone"":""{MaskedPhone}""",
            RegexOptions.None,
            TimeSpan.FromMilliseconds(250));

        // Mascara padrões de e-mail residuais no texto
        result = EmailPattern.Replace(result, MaskedEmail);

        return result;
    }

    /// <summary>
    /// Verifica se um texto contém PII de parceiro em claro.
    /// Usado pelo teste anti-PII (gate CI, TASK-27).
    /// Retorna <c>true</c> se PII for detectada — o teste deve falhar nesse caso.
    /// <c>partner.name</c> em claro <strong>não</strong> é violação (VAL-PARTNER-01).
    /// </summary>
    /// <param name="text">Texto a verificar.</param>
    /// <param name="knownEmail">E-mail do parceiro a verificar.</param>
    /// <param name="knownPhone">Telefone do parceiro a verificar.</param>
    public bool ContainsPii(string text, string? knownEmail = null, string? knownPhone = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        // Verifica presença do e-mail em claro
        if (!string.IsNullOrEmpty(knownEmail) &&
            text.Contains(knownEmail, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Verifica presença do telefone em claro
        if (!string.IsNullOrEmpty(knownPhone) &&
            text.Contains(knownPhone, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Verifica padrão genérico de e-mail (detecção heurística)
        if (EmailPattern.IsMatch(text))
        {
            return true;
        }

        return false;
    }
}
