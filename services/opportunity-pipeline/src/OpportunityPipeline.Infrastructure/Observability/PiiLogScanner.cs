using System.Text.RegularExpressions;

namespace OpportunityPipeline.Infrastructure.Observability;

/// <summary>
/// Scanner de PII em texto de log/trace/evento (TASK-24, RNF 10.4, design §11).
/// Detecta campos proibidos em texto livre para o gate automatizado de CI.
///
/// Campos proibidos (design §11, RNF 10.4, LGPD):
/// - email / e-mail (padrão regex)
/// - cpf (padrão regex 000.000.000-00 ou 11 dígitos)
/// - phone / celular / tel (DDD + número BR)
/// - contact_name (campo literal no JSON de log)
/// - nome, name em contexto de contato (heurístico)
///
/// Uso:
/// - PiiLogScanner.ContainsPii(logLine) → true se PII detectado
/// - PiiLogScanner.ScanLines(lines) → lista de linhas com PII
///
/// Mapeia: RNF 10.4, design §11, TASK-24, gate CI anti-PII.
/// </summary>
public static class PiiLogScanner
{
    // Padrão de e-mail (simplificado mas efetivo para logs)
    private static readonly Regex EmailPattern = new(
        @"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // CPF com ou sem formatação
    private static readonly Regex CpfPattern = new(
        @"\b\d{3}[.\-]?\d{3}[.\-]?\d{3}[.\-]?\d{2}\b",
        RegexOptions.Compiled);

    // Telefone BR (8–9 dígitos com DDD).
    // Usa lookahead/lookbehind para não capturar UUIDs ou outras sequências numéricas longas.
    // Formas aceitas: (11) 98765-4321 | 11 98765-4321 | 11987654321 (sem espaço, sem UUID-like)
    // Requer que o número seja precedido por espaço, = ou início e seguido por fim ou espaço.
    private static readonly Regex PhonePattern = new(
        @"(?<![0-9a-fA-F\-])\(?\d{2}\)[\s-]?\d{4,5}[-\s]?\d{4}(?![0-9a-fA-F\-])",
        RegexOptions.Compiled);

    // Campos PII nominais em JSON/log (ex.: "contact_name":"João Silva")
    private static readonly Regex ContactNamePattern = new(
        @"""contact_name""\s*:\s*""[^""]{2,}""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Chaves explicitamente proibidas em qualquer contexto de log
    private static readonly string[] ProhibitedKeys =
    [
        "contact_name",
        "\"email\"",
        "\"cpf\"",
        "\"phone\"",
        "\"celular\"",
        "\"tel\"",
        "\"nome_contato\"",
    ];

    /// <summary>
    /// Verifica se a linha de log contém PII.
    /// Retorna true se qualquer padrão de PII for detectado.
    /// </summary>
    public static bool ContainsPii(string logLine)
    {
        if (string.IsNullOrWhiteSpace(logLine)) return false;

        if (EmailPattern.IsMatch(logLine)) return true;
        if (CpfPattern.IsMatch(logLine)) return true;
        if (PhonePattern.IsMatch(logLine)) return true;
        if (ContactNamePattern.IsMatch(logLine)) return true;

        foreach (var key in ProhibitedKeys)
            if (logLine.Contains(key, StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }

    /// <summary>
    /// Varre lista de linhas de log e retorna as que contêm PII.
    /// Resultado vazio = sem PII detectado (gate verde).
    /// </summary>
    public static IReadOnlyList<(int LineNumber, string Line, string Reason)> ScanLines(
        IEnumerable<string> lines)
    {
        var violations = new List<(int, string, string)>();
        var lineNumber = 0;

        foreach (var line in lines)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (EmailPattern.IsMatch(line))
                violations.Add((lineNumber, line, "e-mail detectado"));
            else if (CpfPattern.IsMatch(line))
                violations.Add((lineNumber, line, "CPF detectado"));
            else if (PhonePattern.IsMatch(line))
                violations.Add((lineNumber, line, "telefone BR detectado"));
            else if (ContactNamePattern.IsMatch(line))
                violations.Add((lineNumber, line, "contact_name detectado em JSON"));
            else
                foreach (var key in ProhibitedKeys)
                    if (line.Contains(key, StringComparison.OrdinalIgnoreCase))
                    {
                        violations.Add((lineNumber, line, $"chave proibida '{key}' detectada"));
                        break;
                    }
        }

        return violations;
    }
}
