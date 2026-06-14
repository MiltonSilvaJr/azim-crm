using Reporting.Contracts.Responses;

namespace Reporting.Application.Dispatching;

/// <summary>
/// Fachada de Application para os relatórios de leitura.
///
/// Recebe apenas primitivos do controller (<c>DateOnly</c>, <c>Guid</c>, <c>string</c>)
/// e isola o controller de qualquer tipo do Domain ou do pipeline MediatR.
///
/// Contrato:
/// <list type="bullet">
///   <item><description>Cria o objeto de valor <c>Period</c> internamente — lança <see cref="Exceptions.InvalidPeriodException"/> se inválido.</description></item>
///   <item><description>Resolve o <c>ReportScope</c> internamente — lança <see cref="Exceptions.AccessDeniedException"/> para PlatformOperator.</description></item>
///   <item><description>Valida os <paramref name="buIds"/> contra o escopo RBAC — lança <see cref="Exceptions.BuIdOutOfScopeException"/> para BU fora do escopo (anti-enumeração).</description></item>
///   <item><description>Converte a string <paramref name="reportType"/> para enum interno — lança <see cref="Exceptions.InvalidReportTypeException"/> se desconhecido.</description></item>
/// </list>
///
/// Nenhuma dependência do Domain é exposta ao chamador desta interface.
///
/// Mapeia: TASK-21, design §3, §8, §12, ADR-0001.
/// </summary>
public interface IReportDispatcher
{
    /// <summary>
    /// Despacha o relatório de funil por estágio.
    /// </summary>
    Task<FunnelReportResponse> GetFunnelAsync(
        Guid tenantId,
        Guid userId,
        string correlationId,
        DateOnly from,
        DateOnly to,
        IReadOnlyList<Guid>? buIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Despacha o relatório de forecast por BU/mês.
    /// </summary>
    Task<ForecastReportResponse> GetForecastAsync(
        Guid tenantId,
        Guid userId,
        string correlationId,
        DateOnly from,
        DateOnly to,
        IReadOnlyList<Guid>? buIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Despacha o relatório de ranking por responsável.
    /// </summary>
    Task<RankingReportResponse> GetRankingAsync(
        Guid tenantId,
        Guid userId,
        string correlationId,
        DateOnly from,
        DateOnly to,
        IReadOnlyList<Guid>? buIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Despacha o relatório de oportunidades por canal.
    /// </summary>
    Task<ChannelReportResponse> GetChannelAsync(
        Guid tenantId,
        Guid userId,
        string correlationId,
        DateOnly from,
        DateOnly to,
        IReadOnlyList<Guid>? buIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Despacha o relatório de comissões por parceiro.
    /// </summary>
    Task<CommissionReportResponse> GetCommissionsAsync(
        Guid tenantId,
        Guid userId,
        string correlationId,
        DateOnly from,
        DateOnly to,
        IReadOnlyList<Guid>? buIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Despacha a geração de CSV para o tipo de relatório informado e retorna URL assinada.
    /// </summary>
    /// <param name="reportType">
    ///   String do tipo de relatório (funnel, forecast, ranking, channel, commissions).
    ///   Case-insensitive.
    ///   Lança <see cref="Exceptions.InvalidReportTypeException"/> se não reconhecido.
    /// </param>
    Task<CsvExportResponse> GetExportAsync(
        string reportType,
        Guid tenantId,
        Guid userId,
        string correlationId,
        DateOnly from,
        DateOnly to,
        IReadOnlyList<Guid>? buIds,
        CancellationToken cancellationToken = default);
}
