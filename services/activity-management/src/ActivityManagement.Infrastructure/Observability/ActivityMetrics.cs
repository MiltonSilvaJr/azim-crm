namespace ActivityManagement.Infrastructure.Observability;

using System.Diagnostics.Metrics;
using ActivityManagement.Application.Ports;

/// <summary>
/// Métricas Prometheus obrigatórias do módulo activity-management (RNF 6.2, design §11).
///
/// Contadores snake_case conforme convenção Prometheus:
/// - <c>activities_created_total</c>: atividades criadas com sucesso.
/// - <c>activities_completed_total</c>: atividades concluídas (primeira conclusão efetiva).
/// - <c>activities_overdue_total</c>: atividades vencidas detectadas pelo scan.
/// - <c>digest_action_tokens_used_total</c>: tokens de digest consumidos com sucesso.
/// - <c>digest_action_tokens_expired_total</c>: tokens de digest rejeitados por expiração.
///
/// Registrada como singleton no DI (<c>AddInfrastructure</c>).
/// Injetada nos handlers de command via DI — contadores incrementados nos handlers,
/// não nos controllers (design §11).
///
/// Mapeia: TASK-22, RNF 6.2, design §11.
/// </summary>
public sealed class ActivityMetrics : IActivityMetrics, IDisposable
{
    /// <summary>Nome do medidor (meter) Prometheus exportado pelo OpenTelemetry.</summary>
    public const string MeterName = "ActivityManagement";

    // ── Nomes das métricas (snake_case Prometheus) ────────────────────────────────

    /// <summary>Nome da métrica de atividades criadas.</summary>
    public const string ActivitiesCreatedTotalName   = "activities_created_total";

    /// <summary>Nome da métrica de atividades concluídas.</summary>
    public const string ActivitiesCompletedTotalName = "activities_completed_total";

    /// <summary>Nome da métrica de atividades vencidas detectadas pelo scan.</summary>
    public const string ActivitiesOverdueTotalName   = "activities_overdue_total";

    /// <summary>Nome da métrica de tokens de digest usados.</summary>
    public const string DigestTokensUsedTotalName    = "digest_action_tokens_used_total";

    /// <summary>Nome da métrica de tokens de digest expirados.</summary>
    public const string DigestTokensExpiredTotalName = "digest_action_tokens_expired_total";

    // ── Instrumentos ──────────────────────────────────────────────────────────────

    private readonly Meter   _meter;
    private readonly Counter<long> _activitiesCreated;
    private readonly Counter<long> _activitiesCompleted;
    private readonly Counter<long> _activitiesOverdue;
    private readonly Counter<long> _digestTokensUsed;
    private readonly Counter<long> _digestTokensExpired;

    /// <summary>
    /// Inicializa os contadores Prometheus do módulo.
    /// Cada instância cria seu próprio <see cref="Meter"/>; em produção o DI garante singleton.
    /// </summary>
    public ActivityMetrics()
    {
        _meter = new Meter(MeterName, "1.0.0");

        _activitiesCreated = _meter.CreateCounter<long>(
            ActivitiesCreatedTotalName,
            unit:        "activities",
            description: "Total de atividades criadas com sucesso.");

        _activitiesCompleted = _meter.CreateCounter<long>(
            ActivitiesCompletedTotalName,
            unit:        "activities",
            description: "Total de atividades concluídas (primeira conclusão efetiva).");

        _activitiesOverdue = _meter.CreateCounter<long>(
            ActivitiesOverdueTotalName,
            unit:        "activities",
            description: "Total de atividades vencidas detectadas pelo scan agendado.");

        _digestTokensUsed = _meter.CreateCounter<long>(
            DigestTokensUsedTotalName,
            unit:        "tokens",
            description: "Total de tokens de digest consumidos com sucesso.");

        _digestTokensExpired = _meter.CreateCounter<long>(
            DigestTokensExpiredTotalName,
            unit:        "tokens",
            description: "Total de tokens de digest rejeitados por expiração.");
    }

    // ── Métodos de incremento ─────────────────────────────────────────────────────

    /// <summary>Incrementa o contador de atividades criadas.</summary>
    public void IncrementCreated() => _activitiesCreated.Add(1);

    /// <summary>Incrementa o contador de atividades concluídas.</summary>
    public void IncrementCompleted() => _activitiesCompleted.Add(1);

    /// <summary>
    /// Incrementa o contador de atividades vencidas pelo número detectado no scan.
    /// </summary>
    /// <param name="count">Número de atividades vencidas detectadas nesta iteração.</param>
    public void IncrementOverdue(long count = 1) => _activitiesOverdue.Add(count);

    /// <summary>Incrementa o contador de tokens de digest usados.</summary>
    public void IncrementDigestTokenUsed() => _digestTokensUsed.Add(1);

    /// <summary>Incrementa o contador de tokens de digest expirados.</summary>
    public void IncrementDigestTokenExpired() => _digestTokensExpired.Add(1);

    /// <inheritdoc/>
    public void Dispose() => _meter.Dispose();
}
