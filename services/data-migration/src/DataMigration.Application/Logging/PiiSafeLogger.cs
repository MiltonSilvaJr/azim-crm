using Microsoft.Extensions.Logging;

namespace DataMigration.Application.Logging;

/// <summary>
/// Logger estruturado com proteção de PII (LGPD, RNF 3).
///
/// Envolve um <see cref="ILogger"/> real e rejeita (substitui por marcador)
/// qualquer valor associado a campos classificados como PII:
/// <c>contactName</c>, <c>email</c>, <c>phone</c>.
///
/// Referências por índice de linha são sempre permitidas; nomes,
/// e-mails e telefones de contatos nunca aparecem em logs.
///
/// Rastreia: design §5.4, §10, §11, RNF 3, RISK-MIGR-03, TASK-24.
/// </summary>
public sealed class PiiSafeLogger
{
    /// <summary>Campos proibidos em logs (PII, RNF 3).</summary>
    public static readonly IReadOnlySet<string> ProhibitedFields =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "contactName",
            "contact_name",
            "email",
            "phone",
            "telefone",
            "nome_contato",
            "name",          // previne leak acidental de "name" como campo de contato
        };

    private const string PiiMask = "[PII-REMOVIDO]";

    private readonly ILogger _inner;

    /// <summary>
    /// Cria o <see cref="PiiSafeLogger"/> envolvendo o logger real.
    /// </summary>
    /// <param name="inner">Logger subjacente a ser envolvido.</param>
    public PiiSafeLogger(ILogger inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    // =========================================================================
    // API pública de log estruturado (sem PII)
    // =========================================================================

    /// <summary>
    /// Registra uma linha processada com sucesso (sem PII).
    /// Campos: migration_job_id, source_sheet, source_row_index, status, message,
    /// correlation_id.
    /// </summary>
    public void LogRowProcessed(
        Guid migrationJobId,
        string sourceSheet,
        int sourceRowIndex,
        string status,
        string message,
        Guid correlationId)
    {
        // Garante que a mensagem técnica não contém PII vazado
        var safeMessage = SanitizeMessage(message);

        _inner.LogInformation(
            "[MIGRATION] {migration_job_id} sheet={source_sheet} row={source_row_index} " +
            "status={status} msg={message} correlation_id={correlation_id}",
            migrationJobId,
            sourceSheet,
            sourceRowIndex,
            status,
            safeMessage,
            correlationId);
    }

    /// <summary>
    /// Registra um erro de linha (sem PII — referência por índice de linha).
    /// </summary>
    public void LogRowError(
        Guid migrationJobId,
        string sourceSheet,
        int sourceRowIndex,
        string errorCode,
        string technicalMessage,
        Guid correlationId)
    {
        var safeMessage = SanitizeMessage(technicalMessage);

        _inner.LogWarning(
            "[MIGRATION] {migration_job_id} sheet={source_sheet} row={source_row_index} " +
            "error={error_code} msg={message} correlation_id={correlation_id}",
            migrationJobId,
            sourceSheet,
            sourceRowIndex,
            errorCode,
            safeMessage,
            correlationId);
    }

    /// <summary>
    /// Registra início do import com metadados do job (sem PII).
    /// </summary>
    public void LogImportStarted(
        Guid migrationJobId,
        Guid tenantId,
        int detectedRowCount,
        Guid correlationId)
    {
        _inner.LogInformation(
            "[MIGRATION] import_started job={migration_job_id} tenant={tenant_id} " +
            "rows={detected_row_count} correlation_id={correlation_id}",
            migrationJobId,
            tenantId,
            detectedRowCount,
            correlationId);
    }

    /// <summary>
    /// Registra conclusão do import com contagens por entidade (sem PII).
    /// </summary>
    public void LogImportCompleted(
        Guid migrationJobId,
        Guid tenantId,
        int accounts,
        int partners,
        int opportunities,
        int activities,
        TimeSpan duration,
        Guid correlationId)
    {
        _inner.LogInformation(
            "[MIGRATION] import_completed job={migration_job_id} tenant={tenant_id} " +
            "accounts={accounts} partners={partners} opportunities={opportunities} " +
            "activities={activities} duration_ms={duration_ms} correlation_id={correlation_id}",
            migrationJobId,
            tenantId,
            accounts,
            partners,
            opportunities,
            activities,
            (long)duration.TotalMilliseconds,
            correlationId);
    }

    /// <summary>
    /// Registra rollback total do import (sem PII).
    /// </summary>
    public void LogImportRolledBack(
        Guid migrationJobId,
        Guid tenantId,
        string errorCode,
        int? failedRowIndex,
        Guid correlationId)
    {
        _inner.LogError(
            "[MIGRATION] import_rolled_back job={migration_job_id} tenant={tenant_id} " +
            "error={error_code} failed_row={failed_row_index} correlation_id={correlation_id}",
            migrationJobId,
            tenantId,
            errorCode,
            failedRowIndex.HasValue ? (object)failedRowIndex.Value : "unknown",
            correlationId);
    }

    // =========================================================================
    // Sanitização de mensagens
    // =========================================================================

    /// <summary>
    /// Verifica se o nome do campo é PII proibido.
    /// </summary>
    /// <param name="fieldName">Nome do campo.</param>
    /// <returns><c>true</c> quando o campo é PII proibido.</returns>
    public static bool IsProhibitedField(string fieldName)
        => ProhibitedFields.Contains(fieldName);

    /// <summary>
    /// Mascara um valor de campo PII proibido.
    /// </summary>
    /// <param name="fieldName">Nome do campo.</param>
    /// <param name="value">Valor original.</param>
    /// <returns>
    /// O marcador <c>[PII-REMOVIDO]</c> quando o campo é PII;
    /// o valor original quando é permitido.
    /// </returns>
    public static string MaskIfPii(string fieldName, string value)
        => IsProhibitedField(fieldName) ? PiiMask : value;

    /// <summary>
    /// Remove padrões típicos de PII de mensagens de texto livre
    /// (e-mail, telefone) usando regex conservador.
    /// </summary>
    public static string SanitizeMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return message;
        }

        // Mascara e-mails (padrão básico RFC-5321)
        var withoutEmail = System.Text.RegularExpressions.Regex.Replace(
            message,
            @"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}",
            PiiMask);

        // Mascara telefones brasileiros: (11) 9xxxx-xxxx, +55 11 9xxxx-xxxx, etc.
        var withoutPhone = System.Text.RegularExpressions.Regex.Replace(
            withoutEmail,
            @"(\+?55\s?)?(\(?\d{2}\)?\s?)?\d{4,5}[-\s]?\d{4}",
            PiiMask);

        return withoutPhone;
    }
}
