using Reporting.Contracts.ReadModels;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Ports;

/// <summary>
/// Porta de leitura principal do módulo reporting.
///
/// Implementada na camada Infrastructure (Dapper/EF Core keyless read-only sobre views com RLS).
/// A Application nunca conhece a implementação concreta — apenas esta interface (design §3, DD-003).
///
/// Todas as operações são read-only e idempotentes (sem efeito colateral de negócio).
/// O predicado RBAC de escopo é aplicado pelo handler via <c>RbacScopeSpecification</c>
/// antes de chamar este repositório — a porta recebe o scope já resolvido (design §5.3, DD-006).
///
/// Mapeia: TASK-05, design §5.2, §5.3, §6.1, DD-003, DD-005, Req 1–6.
/// </summary>
public interface IReportingReadRepository
{
    /// <summary>
    /// Retorna as linhas do funil por estágio filtradas por escopo e período (Req 1).
    /// </summary>
    Task<IReadOnlyList<FunnelRow>> GetFunnelAsync(
        ReportScope scope,
        Period period,
        IEnumerable<Guid>? buIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna as linhas de forecast por BU/mês no período (Req 6).
    /// </summary>
    Task<IReadOnlyList<ForecastRow>> GetForecastAsync(
        ReportScope scope,
        Period period,
        IEnumerable<Guid>? buIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna as linhas de ranking por responsável no período (Req 2).
    /// Inclui <see cref="RankingRow.DisplayName"/> — a decisão de omitir PII é do handler (<c>PiiMinimizationPolicy</c>).
    /// </summary>
    Task<IReadOnlyList<RankingRow>> GetRankingAsync(
        ReportScope scope,
        Period period,
        IEnumerable<Guid>? buIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna as linhas brutas de oportunidades por canal no período (Req 3).
    /// Os basis points são calculados pelo handler (TASK-09).
    /// </summary>
    Task<IReadOnlyList<ChannelRow>> GetChannelAsync(
        ReportScope scope,
        Period period,
        IEnumerable<Guid>? buIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna as linhas brutas de comissões por parceiro no período (Req 4).
    /// A agregação snapshot × projetado é responsabilidade do handler (TASK-10, PBT-01).
    /// </summary>
    Task<IReadOnlyList<CommissionRow>> GetCommissionsAsync(
        ReportScope scope,
        Period period,
        IEnumerable<Guid>? buIds,
        CancellationToken cancellationToken = default);
}
