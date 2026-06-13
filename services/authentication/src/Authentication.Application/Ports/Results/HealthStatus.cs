namespace Authentication.Application.Ports.Results;

/// <summary>
/// Status de saúde de uma dependência externa, retornado por health checks.
///
/// Mapeia: RNF 3.2, design.md § 6.4.
/// </summary>
public enum HealthStatus
{
    /// <summary>Dependência disponível e respondendo dentro do prazo esperado.</summary>
    Healthy = 0,

    /// <summary>Dependência indisponível ou com falha.</summary>
    Unhealthy = 1,

    /// <summary>Dependência disponível mas com degradação de desempenho.</summary>
    Degraded = 2,
}
