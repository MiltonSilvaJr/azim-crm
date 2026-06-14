namespace PartnerManagement.Infrastructure.ReadPorts;

/// <summary>
/// Configuração de resiliência do <see cref="PartnerCommissionReadAdapter"/>.
/// Permite ajustar timeout, retry e circuit breaker via <c>appsettings.json</c>.
/// Mapeia: TASK-28, design §15, RISK-PM-06.
/// </summary>
public sealed class CommissionReadResilienceOptions
{
    /// <summary>Seção de configuração no appsettings.</summary>
    public const string SectionName = "CommissionReadResilience";

    /// <summary>Timeout total por tentativa (em segundos). Padrão: 10 s.</summary>
    public int TimeoutSeconds { get; init; } = 10;

    /// <summary>Número máximo de tentativas de retry (sem contar a tentativa inicial). Padrão: 2.</summary>
    public int MaxRetryAttempts { get; init; } = 2;

    /// <summary>Atraso base do backoff exponencial (em segundos). Padrão: 0,5 s.</summary>
    public double RetryBaseDelaySeconds { get; init; } = 0.5;

    /// <summary>
    /// Número de falhas consecutivas para abrir o circuit breaker. Padrão: 5.
    /// </summary>
    public int CircuitBreakerFailureThreshold { get; init; } = 5;

    /// <summary>Duração do break do circuit breaker (em segundos). Padrão: 30 s.</summary>
    public int CircuitBreakerBreakDurationSeconds { get; init; } = 30;
}
