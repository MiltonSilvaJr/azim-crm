using System.Diagnostics.Metrics;
using PartnerManagement.Application.Ports;

namespace PartnerManagement.Infrastructure.Observability;

/// <summary>
/// Métricas Prometheus do módulo partner-management.
/// Expõe contadores obrigatórios conforme design §11 (RNF 5.2):
/// <list type="bullet">
///   <item><c>partners_created_total</c> — parceiros criados com sucesso.</item>
///   <item><c>partners_deactivated_total</c> — transições Active → Inactive efetivas.</item>
///   <item><c>partners_reactivated_total</c> — transições Inactive → Active efetivas.</item>
///   <item><c>partner_commission_view_duration_seconds</c> — histograma de latência da visão de comissão.</item>
/// </list>
/// Todos os nomes em snake_case conforme convenção (design §11).
/// Implementa <see cref="IPartnerMetrics"/> (porta declarada no Application).
/// Mapeia: RNF 5, design §11, TASK-26.
/// </summary>
public sealed class PartnerMetrics : IPartnerMetrics, IDisposable
{
    /// <summary>Nome do meter do módulo (OpenTelemetry/Prometheus).</summary>
    public const string MeterName = "partner_management";

    private readonly Meter _meter;
    private readonly Counter<long> _createdTotal;
    private readonly Counter<long> _deactivatedTotal;
    private readonly Counter<long> _reactivatedTotal;
    private readonly Histogram<double> _commissionViewDurationSeconds;

    /// <summary>
    /// Inicializa as métricas do módulo partner-management.
    /// </summary>
    public PartnerMetrics()
    {
        _meter = new Meter(MeterName);

        _createdTotal = _meter.CreateCounter<long>(
            name: "partners_created_total",
            description: "Número total de parceiros criados com sucesso no tenant.");

        _deactivatedTotal = _meter.CreateCounter<long>(
            name: "partners_deactivated_total",
            description: "Número total de parceiros inativados (transição efetiva Active → Inactive).");

        _reactivatedTotal = _meter.CreateCounter<long>(
            name: "partners_reactivated_total",
            description: "Número total de parceiros reativados (transição efetiva Inactive → Active).");

        _commissionViewDurationSeconds = _meter.CreateHistogram<double>(
            name: "partner_commission_view_duration_seconds",
            unit: "s",
            description: "Duração da consulta de visão de comissão do parceiro (p50/p95/p99).");
    }

    /// <summary>
    /// Incrementa o contador <c>partners_created_total</c>.
    /// Deve ser chamado após criação bem-sucedida de um parceiro (RNF 5.2).
    /// </summary>
    public void RecordPartnerCreated() => _createdTotal.Add(1);

    /// <summary>
    /// Incrementa o contador <c>partners_deactivated_total</c>.
    /// Deve ser chamado apenas em transição efetiva (DD-006).
    /// </summary>
    public void RecordPartnerDeactivated() => _deactivatedTotal.Add(1);

    /// <summary>
    /// Incrementa o contador <c>partners_reactivated_total</c>.
    /// Deve ser chamado apenas em transição efetiva (DD-006).
    /// </summary>
    public void RecordPartnerReactivated() => _reactivatedTotal.Add(1);

    /// <summary>
    /// Registra a duração de uma consulta de visão de comissão.
    /// </summary>
    /// <param name="duration">Duração da operação.</param>
    public void RecordCommissionViewDuration(TimeSpan duration) =>
        _commissionViewDurationSeconds.Record(duration.TotalSeconds);

    /// <inheritdoc/>
    public void Dispose() => _meter.Dispose();
}
