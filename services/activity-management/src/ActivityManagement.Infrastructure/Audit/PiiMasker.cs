namespace ActivityManagement.Infrastructure.Audit;

using System.Text.Json;

/// <summary>
/// Mascara campos PII (<c>title</c> e <c>description</c>) substituindo-os por <c>[MASKED]</c>
/// antes de qualquer inclusão em logs estruturados ou no <c>delta_json</c> de auditoria.
/// Garante que dados pessoais sensíveis (texto livre) nunca apareçam em rastros de auditoria
/// ou em qualquer saída de log (RNF 7.2, DD-009, design §6.6).
/// Mapeia: TASK-16, RNF 7, DD-009.
/// </summary>
public static class PiiMasker
{
    /// <summary>Campos considerados PII que devem ser mascarados (insensível a maiúsculas).</summary>
    private static readonly HashSet<string> PiiFields =
        new(StringComparer.OrdinalIgnoreCase) { "title", "description" };

    /// <summary>Valor substituto para campos PII mascarados.</summary>
    public const string MaskedValue = "[MASKED]";

    /// <summary>
    /// Retorna uma cópia do dicionário de delta com os campos PII substituídos por <see cref="MaskedValue"/>.
    /// Campos não listados em <see cref="PiiFields"/> são preservados sem alteração.
    /// </summary>
    /// <param name="delta">Campos alterados a serem incluídos no registro de auditoria.</param>
    /// <returns>Novo dicionário com PII mascarada.</returns>
    public static Dictionary<string, object?> MaskDelta(Dictionary<string, object?> delta)
    {
        ArgumentNullException.ThrowIfNull(delta);

        var result = new Dictionary<string, object?>(delta.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in delta)
        {
            result[key] = PiiFields.Contains(key) ? MaskedValue : value;
        }
        return result;
    }

    /// <summary>
    /// Mascara os campos PII e serializa o delta em JSON pronto para persistência em <c>delta_json</c>.
    /// </summary>
    /// <param name="delta">Campos alterados.</param>
    /// <returns>String JSON com PII mascarada.</returns>
    public static string MaskDeltaToJson(Dictionary<string, object?> delta)
    {
        var masked = MaskDelta(delta);
        return JsonSerializer.Serialize(masked);
    }

    /// <summary>
    /// Mascara campos PII em um JSON string existente retornando um novo JSON string.
    /// Usado quando o delta já está serializado e precisa ser inspecionado/re-mascarado.
    /// </summary>
    /// <param name="deltaJson">JSON serializado do delta.</param>
    /// <returns>JSON com campos PII substituídos por <see cref="MaskedValue"/>.</returns>
    public static string MaskJson(string deltaJson)
    {
        if (string.IsNullOrWhiteSpace(deltaJson))
            return deltaJson;

        try
        {
            using var doc = JsonDocument.Parse(deltaJson);
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                dict[prop.Name] = PiiFields.Contains(prop.Name)
                    ? (object?)MaskedValue
                    : prop.Value.ValueKind switch
                    {
                        JsonValueKind.String  => prop.Value.GetString(),
                        JsonValueKind.Number  => prop.Value.GetDecimal(),
                        JsonValueKind.True    => true,
                        JsonValueKind.False   => false,
                        JsonValueKind.Null    => null,
                        _                     => prop.Value.GetRawText(),
                    };
            }

            return JsonSerializer.Serialize(dict);
        }
        catch (JsonException)
        {
            // JSON inválido: retorna mascarado como segurança defensiva
            return MaskedValue;
        }
    }
}
