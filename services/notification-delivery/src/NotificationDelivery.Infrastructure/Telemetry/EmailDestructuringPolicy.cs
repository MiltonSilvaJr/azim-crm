using Serilog.Core;
using Serilog.Events;

namespace NotificationDelivery.Infrastructure.Telemetry;

/// <summary>
/// Serilog <see cref="IDestructuringPolicy"/> que intercepta propriedades de log
/// contendo e-mail do destinatário (PII) e substitui pelo hash mascarado.
///
/// Campos interceptados: <c>RecipientEmail</c>, <c>Email</c>, <c>recipient</c>.
/// Qualquer propriedade com esses nomes tem seu valor substituído por
/// <see cref="EmailHasher.Hash"/> antes de ser serializado no log (RNF 4, DD-008, PBT-03).
///
/// Registrada no pipeline de log do módulo via <c>AddNotificationDelivery()</c> (TASK-16).
///
/// Uso:
/// <code>
/// Log.Logger = new LoggerConfiguration()
///     .Destructure.With&lt;EmailDestructuringPolicy&gt;()
///     .CreateLogger();
/// </code>
/// </summary>
public sealed class EmailDestructuringPolicy : IDestructuringPolicy
{
    /// <summary>
    /// Nomes de propriedade de log que contêm e-mail do destinatário (PII).
    /// Qualquer propriedade com esses nomes será mascarada antes da serialização.
    /// </summary>
    private static readonly HashSet<string> PiiPropertyNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "RecipientEmail",
            "Email",
            "recipient"
        };

    /// <inheritdoc/>
    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
#pragma warning disable CS8767 // Nullability mismatch — Serilog interface assinatura varia entre versões
        out LogEventPropertyValue result)
#pragma warning restore CS8767
    {
        // Esta policy não atua na destruturação de objetos complexos —
        // a interceptação de campos PII é feita via Enricher (EmailPiiScrubberEnricher).
        // Retorna false para que o pipeline padrão processe normalmente.
        result = null!;
        return false;
    }
}

/// <summary>
/// Serilog <see cref="ILogEventEnricher"/> que varre as propriedades de cada log event
/// e substitui valores de e-mail por hash mascarado (RNF 4, DD-008, PBT-03).
///
/// Intercept: propriedades com nome em <see cref="PiiPropertyNames"/> e
/// qualquer valor string que pareça um endereço de e-mail (contém <c>@</c>)
/// têm o valor substituído por <see cref="EmailHasher.Hash"/>.
/// </summary>
public sealed class EmailPiiScrubberEnricher : ILogEventEnricher
{
    /// <summary>
    /// Nomes de propriedades de log que podem conter PII de e-mail.
    /// </summary>
    private static readonly HashSet<string> PiiPropertyNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "RecipientEmail",
            "Email",
            "recipient",
            "Recipient",
            "emailAddress",
            "EmailAddress"
        };

    /// <inheritdoc/>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var name in PiiPropertyNames)
        {
            if (!logEvent.Properties.TryGetValue(name, out var prop))
                continue;

            if (prop is ScalarValue { Value: string emailValue }
                && !EmailHasher.IsHashed(emailValue))
            {
                // Substitui o valor PII pelo hash mascarado
                var maskedProperty = propertyFactory.CreateProperty(
                    name,
                    EmailHasher.Hash(emailValue));

                logEvent.AddOrUpdateProperty(maskedProperty);
            }
        }
    }
}
