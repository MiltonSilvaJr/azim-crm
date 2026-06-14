using System.Text.Json;
using System.Text.Json.Nodes;
using AccountManagement.Domain.Accounts.ValueObjects;

namespace AccountManagement.Infrastructure.Audit;

/// <summary>
/// Componente centralizado de mascaramento de PII para logs, auditoria e eventos.
///
/// Aplica mascaramento nos três pontos definidos em DD-003:
/// 1. Serialização de <c>delta_json</c> de auditoria.
/// 2. Construção de <c>ContactLinked.maskedDelta</c>.
/// 3. Enriquecimento de logs/traces (via substituição de campos conhecidos).
///
/// Campos de PII mascarados: <c>name</c>, <c>email</c>, <c>phone</c>.
/// Campos de não-PII preservados: <c>role</c>, <c>account_id</c>, <c>action</c>, <c>event_id</c>, etc.
///
/// Mapeia: design §6.3 (Audit), DD-003, RNF 1, Req 8.4, TASK-11.
/// </summary>
public sealed class PiiMasker
{
    private static readonly string[] PiiFieldNames =
        ["name", "email", "phone", "contact_name", "contact_email", "contact_phone"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    /// <summary>
    /// Mascara campos de PII em um JSON delta.
    ///
    /// Substitui os valores dos campos <c>name</c>, <c>email</c> e <c>phone</c>
    /// pelo marcador <see cref="ContactInfo.AnonymizationMarker"/>.
    /// Campos ausentes são ignorados; campos de não-PII são preservados.
    ///
    /// Mapeia: DD-003, Req 8.4, TASK-11 (ST-01).
    /// </summary>
    /// <param name="deltaJson">JSON a mascarar.</param>
    /// <returns>JSON com campos de PII substituídos por marcadores.</returns>
    public string MaskContactDelta(string deltaJson)
    {
        if (string.IsNullOrWhiteSpace(deltaJson))
            return deltaJson;

        try
        {
            var node = JsonNode.Parse(deltaJson);
            if (node is not JsonObject obj)
                return deltaJson;

            MaskObject(obj);
            return obj.ToJsonString(JsonOptions);
        }
        catch (JsonException)
        {
            // JSON inválido: retorna marcador genérico sem vazar conteúdo original
            return $"{{\"error\":\"{ContactInfo.AnonymizationMarker}\"}}";
        }
    }

    /// <summary>
    /// Verifica se um JSON delta contém PII em texto claro.
    /// Usado em testes de scan anti-PII (RNF 1.4, TASK-17).
    /// </summary>
    /// <param name="deltaJson">JSON a verificar.</param>
    /// <param name="knownPiiValues">Valores de PII conhecidos para detectar.</param>
    /// <returns><c>true</c> quando PII detectada; <c>false</c> caso contrário.</returns>
    public bool ContainsPii(string deltaJson, IEnumerable<string> knownPiiValues)
    {
        if (string.IsNullOrWhiteSpace(deltaJson))
            return false;

        foreach (var pii in knownPiiValues)
        {
            if (!string.IsNullOrEmpty(pii) && deltaJson.Contains(pii, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    // =========================================================================
    // Helpers privados
    // =========================================================================

    private static void MaskObject(JsonObject obj)
    {
        foreach (var field in PiiFieldNames)
        {
            if (obj.ContainsKey(field))
                obj[field] = JsonValue.Create(ContactInfo.AnonymizationMarker);
        }

        // Recursão para objetos aninhados
        foreach (var prop in obj)
        {
            if (prop.Value is JsonObject nested)
                MaskObject(nested);
            else if (prop.Value is JsonArray arr)
                MaskArray(arr);
        }
    }

    private static void MaskArray(JsonArray arr)
    {
        foreach (var item in arr)
        {
            if (item is JsonObject obj)
                MaskObject(obj);
        }
    }
}
