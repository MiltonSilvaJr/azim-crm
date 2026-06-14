namespace ActivityManagement.Application.Ports;

/// <summary>
/// Port de métricas Prometheus do módulo activity-management (RNF 6.2, design §11).
/// Implementação concreta em Infrastructure (<c>ActivityMetrics</c>).
///
/// Os contadores são incrementados nos handlers de command/query,
/// nunca nos controllers (design §11).
///
/// Mapeia: TASK-22, RNF 6.2.
/// </summary>
public interface IActivityMetrics
{
    /// <summary>Incrementa o contador de atividades criadas com sucesso.</summary>
    void IncrementCreated();

    /// <summary>Incrementa o contador de atividades concluídas (primeira conclusão efetiva).</summary>
    void IncrementCompleted();

    /// <summary>Incrementa o contador de atividades vencidas detectadas pelo scan.</summary>
    /// <param name="count">Número de atividades vencidas nesta iteração do scan.</param>
    void IncrementOverdue(long count = 1);

    /// <summary>Incrementa o contador de tokens de digest usados com sucesso.</summary>
    void IncrementDigestTokenUsed();

    /// <summary>Incrementa o contador de tokens de digest rejeitados por expiração.</summary>
    void IncrementDigestTokenExpired();
}
