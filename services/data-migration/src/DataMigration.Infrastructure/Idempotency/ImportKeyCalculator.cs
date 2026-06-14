using System.Security.Cryptography;
using System.Text;

namespace DataMigration.Infrastructure.Idempotency;

/// <summary>
/// Calcula a <c>import_key</c> determinística por linha de origem (DD-003, design §6.5).
///
/// Fórmula: <c>SHA-256(tenant_id + ":" + source_sheet + ":" + source_row_index + ":" + normalized_payload)</c>
/// codificado como hex lowercase.
///
/// Propriedades garantidas (PBT-02):
/// - Determinística: mesmos inputs → mesma chave.
/// - Sem colisão por design: tenant + sheet + row + payload formam chave natural única.
/// - Reexecução segura: mesma linha triada → mesma chave → upsert idempotente.
/// - Sem PII no payload: o normalization removerá dados identificadores antes do hash.
///
/// Rastreia: design §6.5, DD-003, Req 12, PBT-02, TASK-19.
/// </summary>
public static class ImportKeyCalculator
{
    /// <summary>
    /// Calcula a import_key determinística para uma linha.
    /// </summary>
    /// <param name="tenantId">ID do tenant (ADR-0001).</param>
    /// <param name="sourceSheet">Nome da aba de origem (ex: "Pipeline", "Ações Comerciais").</param>
    /// <param name="sourceRowIndex">Índice da linha na aba (base 0 ou 1 — deve ser consistente).</param>
    /// <param name="normalizedPayload">Representação normalizada dos dados da linha (sem PII).</param>
    /// <returns>Hash SHA-256 em hex lowercase (64 caracteres).</returns>
    public static string Calculate(
        Guid tenantId,
        string sourceSheet,
        int sourceRowIndex,
        string normalizedPayload)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceSheet);
        ArgumentException.ThrowIfNullOrEmpty(normalizedPayload);

        // Composição da chave natural: tenant + sheet + row + payload.
        var raw = $"{tenantId}:{sourceSheet}:{sourceRowIndex}:{normalizedPayload}";
        var bytes = Encoding.UTF8.GetBytes(raw);
        var hash = SHA256.HashData(bytes);

        return Convert.ToHexStringLower(hash);
    }
}
