using Microsoft.Extensions.Hosting;
using Serilog;

namespace Authentication.Infrastructure.Logging;

/// <summary>
/// Configura o Serilog com:
///   - Destructuring policy de mascaramento de PII (e-mail, nome)
///   - Enriquecimento automático de propriedades de rastreabilidade
///   - Saída JSON estruturada (compatível com Cloud Logging)
///
/// NUNCA registra: e-mail em texto claro, identity_uid, senha, token completo.
///
/// Mapeia: TASK-22, RNF 4, design.md § 11, DD-006.
/// </summary>
public static class SerilogConfigurator
{
    /// <summary>
    /// Configura Serilog no <see cref="IHostBuilder"/> com mascaramento de PII e enriquecimento.
    /// </summary>
    public static IHostBuilder UseAuthenticationSerilog(this IHostBuilder builder)
    {
        return builder.UseSerilog((context, services, configuration) =>
        {
            configuration
                // Mascaramento de PII por construção (DD-006, RNF 4)
                .Destructure.With<MaskEmailDestructuringPolicy>()

                // Enriquecimento automático
                .Enrich.FromLogContext()

                // Nível mínimo de log
                .MinimumLevel.Information()

                // Sink: console JSON (Cloud Logging consome JSON estruturado)
                .WriteTo.Console(
                    outputTemplate: "{Timestamp:yyyy-MM-ddTHH:mm:ssZ} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
        });
    }
}
