using System.Text.Json;

namespace OpportunityPipeline.Infrastructure.Audit;

/// <summary>
/// Mascara campos PII de deltas de auditoria antes de persistir em audit_logs.
/// Remove ou substitui campos sensíveis (nome, email, cpf, etc.) do payload.
/// Nenhum campo PII deve aparecer em logs ou auditoria (RNF 6.3, RNF 10.4).
/// Mapeia: RNF 6.3, design §6.6, TASK-16.
/// </summary>
public static class PiiMasker
{
    // Campos que devem ser mascarados (valores substituídos por "[REDACTED]")
    private static readonly HashSet<string> PiiFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "email", "cpf", "cnpj", "phone", "telefone", "nome", "name",
        "contact_name", "address", "endereco", "cep", "rg",
        "full_name", "document", "documento", "birth_date", "data_nascimento"
    };

    /// <summary>
    /// Mascara campos PII de um objeto delta, retornando o JSON mascarado.
    /// Seguro para auditoria: campos sensíveis são substituídos por "[REDACTED]".
    /// </summary>
    /// <param name="delta">Objeto com dados de alteração (pode ser anonymous object ou dict).</param>
    /// <returns>JSON string com campos PII mascarados.</returns>
    public static string MaskAndSerialize(object? delta)
    {
        if (delta is null)
            return "{}";

        var json = JsonSerializer.Serialize(delta);

        // Deserializa, mascara e re-serializa
        using var doc = JsonDocument.Parse(json);
        var maskedDict = MaskDocument(doc.RootElement);
        return JsonSerializer.Serialize(maskedDict);
    }

    private static Dictionary<string, object?> MaskDocument(JsonElement element)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (element.ValueKind != JsonValueKind.Object)
            return result;

        foreach (var prop in element.EnumerateObject())
        {
            if (PiiFields.Contains(prop.Name))
            {
                result[prop.Name] = "[REDACTED]";
            }
            else if (prop.Value.ValueKind == JsonValueKind.Object)
            {
                result[prop.Name] = MaskDocument(prop.Value);
            }
            else
            {
                result[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.TryGetInt64(out var l) ? l : prop.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => prop.Value.GetRawText()
                };
            }
        }

        return result;
    }
}
