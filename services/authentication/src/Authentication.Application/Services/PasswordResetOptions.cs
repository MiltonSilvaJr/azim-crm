namespace Authentication.Application.Services;

/// <summary>
/// Opções de configuração do <see cref="PasswordResetService"/>.
///
/// Mapeia: Req 10.2, RISK-AUTH-05, design.md § 5.1.
/// </summary>
public sealed class PasswordResetOptions
{
    /// <summary>
    /// Seção de configuração no appsettings.json.
    /// </summary>
    public const string SectionName = "Authentication:PasswordReset";

    /// <summary>
    /// Delay constante em milissegundos aplicado ao final do processamento,
    /// para equalizar o tempo de resposta e impedir oráculo de timing (Req 10.2, RISK-AUTH-05).
    ///
    /// Valor padrão: 200ms. Configure conforme medição do p95 do caso mais lento.
    /// </summary>
    public int ConstantDelayMs { get; set; } = 200;
}
