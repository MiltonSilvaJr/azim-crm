using Serilog.Core;
using Serilog.Events;

namespace Authentication.Infrastructure.Logging;

/// <summary>
/// Política de destructuring do Serilog que mascara PII (e-mail, nome) em objetos estruturados.
///
/// Campos mascarados (insensível a maiúsculas/minúsculas):
///   - Email, E-mail, UserEmail
///   - Name, FullName, FirstName, LastName, DisplayName
///
/// Campos de rastreabilidade NÃO mascarados (não são PII):
///   - TenantId, CorrelationId, UserId, TraceId, SpanId
///
/// Aplica também mascaramento em <see cref="ScalarValue"/> que contenha formato de e-mail.
///
/// Mapeia: TASK-22, RNF 4, design.md § 11, DD-006.
/// </summary>
public sealed class MaskEmailDestructuringPolicy : IDestructuringPolicy
{
    private static readonly HashSet<string> PiiFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "email", "e-mail", "useremail", "emailaddress",
        "name", "fullname", "firstname", "lastname", "displayname", "username"
    };

    private const string Masked = "***";

    /// <inheritdoc/>
    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        out LogEventPropertyValue result)
    {
        if (value is null)
        {
            result = new ScalarValue(null);
            return false;
        }

        // Para strings que parecem e-mail, mascarar diretamente
        if (value is string str && LooksLikeEmail(str))
        {
            result = new ScalarValue(Masked);
            return true;
        }

        // Para objetos anônimos ou poços de propriedades, inspecionar campos
        var type = value.GetType();
        if (!type.IsAnonymousType() && type.IsPrimitive)
        {
            result = new ScalarValue(null);
            return false;
        }

        var properties = type.GetProperties(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        if (properties.Length == 0)
        {
            result = new ScalarValue(null);
            return false;
        }

        bool hasPiiField = properties.Any(p => IsPiiField(p.Name));
        if (!hasPiiField)
        {
            result = new ScalarValue(null);
            return false;
        }

        // Reescreve o objeto com os campos PII mascarados
        var logProperties = new List<LogEventProperty>();
        foreach (var prop in properties)
        {
            string propName = prop.Name;
            object? propValue = prop.GetValue(value);

            LogEventPropertyValue logValue;
            if (IsPiiField(propName))
            {
                logValue = new ScalarValue(Masked);
            }
            else
            {
                logValue = propertyValueFactory.CreatePropertyValue(propValue, destructureObjects: true);
            }

            logProperties.Add(new LogEventProperty(propName, logValue));
        }

        result = new StructureValue(logProperties);
        return true;
    }

    private static bool IsPiiField(string fieldName) =>
        PiiFieldNames.Contains(fieldName);

    private static bool LooksLikeEmail(string value) =>
        value.Contains('@') && value.Contains('.');
}

/// <summary>
/// Extensões para detecção de tipos anônimos.
/// </summary>
internal static class TypeExtensions
{
    internal static bool IsAnonymousType(this Type type)
    {
        return type.Name.StartsWith("<>f__AnonymousType", StringComparison.Ordinal)
            || type.Name.StartsWith("VB$AnonymousType", StringComparison.Ordinal);
    }
}
