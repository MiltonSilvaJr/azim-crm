using MediatR;
using Reporting.Application.Exceptions;
using Reporting.Application.Ports;
using Reporting.Application.Queries.Channel;
using Reporting.Application.Queries.Commission;
using Reporting.Application.Queries.Export;
using Reporting.Application.Queries.Forecast;
using Reporting.Application.Queries.Funnel;
using Reporting.Application.Queries.Ranking;
using Reporting.Contracts.Responses;
using Reporting.Domain.Enums;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Dispatching;

/// <summary>
/// Implementação do <see cref="IReportDispatcher"/>.
///
/// Responsabilidades:
/// <list type="number">
///   <item><description>Converte primitivos (DateOnly) em <c>Period</c> — lança <see cref="InvalidPeriodException"/> se inválido.</description></item>
///   <item><description>Resolve o <c>ReportScope</c> via <see cref="IScopeResolver"/> — delega exceções ao chamador.</description></item>
///   <item><description>Valida <c>buIds</c> contra o escopo RBAC do GestorBU — lança <see cref="BuIdOutOfScopeException"/> (anti-enumeração).</description></item>
///   <item><description>Converte string de tipo de relatório para <see cref="ReportType"/> — lança <see cref="InvalidReportTypeException"/> se desconhecido.</description></item>
///   <item><description>Despacha queries MediatR com os tipos de domínio já resolvidos.</description></item>
/// </list>
///
/// O controller usa apenas <see cref="IReportDispatcher"/> e nunca toca tipos do Domain (design §3).
///
/// Mapeia: TASK-21, design §3, §5, §8, §12, ADR-0001.
/// </summary>
public sealed class ReportDispatcher : IReportDispatcher
{
    private readonly IMediator _mediator;
    private readonly IScopeResolver _scopeResolver;

    /// <summary>Inicializa o dispatcher com mediator e scope resolver.</summary>
    public ReportDispatcher(IMediator mediator, IScopeResolver scopeResolver)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(scopeResolver);
        _mediator = mediator;
        _scopeResolver = scopeResolver;
    }

    /// <inheritdoc/>
    public async Task<FunnelReportResponse> GetFunnelAsync(
        Guid tenantId,
        Guid userId,
        string correlationId,
        DateOnly from,
        DateOnly to,
        IReadOnlyList<Guid>? buIds,
        CancellationToken cancellationToken = default)
    {
        var period = ResolvePeriod(from, to);
        var scope = await ResolveScope(tenantId, userId, cancellationToken);
        ValidateBuIds(buIds, scope);

        var query = new GetFunnelReportQuery(period, buIds, scope);
        return await _mediator.Send(query, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ForecastReportResponse> GetForecastAsync(
        Guid tenantId,
        Guid userId,
        string correlationId,
        DateOnly from,
        DateOnly to,
        IReadOnlyList<Guid>? buIds,
        CancellationToken cancellationToken = default)
    {
        var period = ResolvePeriod(from, to);
        var scope = await ResolveScope(tenantId, userId, cancellationToken);
        ValidateBuIds(buIds, scope);

        var query = new GetForecastReportQuery(period, buIds, scope);
        return await _mediator.Send(query, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<RankingReportResponse> GetRankingAsync(
        Guid tenantId,
        Guid userId,
        string correlationId,
        DateOnly from,
        DateOnly to,
        IReadOnlyList<Guid>? buIds,
        CancellationToken cancellationToken = default)
    {
        var period = ResolvePeriod(from, to);
        var scope = await ResolveScope(tenantId, userId, cancellationToken);
        ValidateBuIds(buIds, scope);

        var query = new GetRankingReportQuery(period, buIds, scope);
        return await _mediator.Send(query, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ChannelReportResponse> GetChannelAsync(
        Guid tenantId,
        Guid userId,
        string correlationId,
        DateOnly from,
        DateOnly to,
        IReadOnlyList<Guid>? buIds,
        CancellationToken cancellationToken = default)
    {
        var period = ResolvePeriod(from, to);
        var scope = await ResolveScope(tenantId, userId, cancellationToken);
        ValidateBuIds(buIds, scope);

        var query = new GetChannelReportQuery(period, buIds, scope);
        return await _mediator.Send(query, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CommissionReportResponse> GetCommissionsAsync(
        Guid tenantId,
        Guid userId,
        string correlationId,
        DateOnly from,
        DateOnly to,
        IReadOnlyList<Guid>? buIds,
        CancellationToken cancellationToken = default)
    {
        var period = ResolvePeriod(from, to);
        var scope = await ResolveScope(tenantId, userId, cancellationToken);
        ValidateBuIds(buIds, scope);

        var query = new GetCommissionReportQuery(period, buIds, scope);
        return await _mediator.Send(query, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CsvExportResponse> GetExportAsync(
        string reportType,
        Guid tenantId,
        Guid userId,
        string correlationId,
        DateOnly from,
        DateOnly to,
        IReadOnlyList<Guid>? buIds,
        CancellationToken cancellationToken = default)
    {
        var parsedType = ParseReportType(reportType);
        var period = ResolvePeriod(from, to);
        var scope = await ResolveScope(tenantId, userId, cancellationToken);
        ValidateBuIds(buIds, scope);

        var query = new GetExportReportCsvQuery(parsedType, period, buIds, scope);
        return await _mediator.Send(query, cancellationToken);
    }

    // ─── Helpers privados (encapsulam tipos do Domain) ─────────────────────────

    /// <summary>
    /// Converte DateOnly em <c>Period</c> do Domain.
    /// Lança <see cref="InvalidPeriodException"/> se <paramref name="from"/> &gt; <paramref name="to"/>.
    /// </summary>
    private static Period ResolvePeriod(DateOnly from, DateOnly to)
    {
        try
        {
            return Period.Create(from, to);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidPeriodException(
                "Período inválido. A data 'from' deve ser anterior ou igual a 'to'.", ex);
        }
    }

    /// <summary>
    /// Resolve <c>ReportScope</c> via <see cref="IScopeResolver"/>.
    /// Lança <see cref="AccessDeniedException"/> para PlatformOperator (via <see cref="UnauthorizedAccessException"/>).
    /// </summary>
    private async Task<ReportScope> ResolveScope(Guid tenantId, Guid userId, CancellationToken ct)
    {
        try
        {
            return await _scopeResolver.ResolveAsync(tenantId, userId, ct);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new AccessDeniedException(ex.Message, ex);
        }
    }

    /// <summary>
    /// Valida que os <paramref name="buIds"/> estão dentro do escopo RBAC do <paramref name="scope"/>.
    /// Lança <see cref="BuIdOutOfScopeException"/> para GestorBU com BU fora do escopo (anti-enumeração, design §10).
    /// </summary>
    private static void ValidateBuIds(IReadOnlyList<Guid>? buIds, ReportScope scope)
    {
        if (buIds is null || buIds.Count == 0)
        {
            return;
        }

        if (scope.Role != ReportingRole.GestorBU)
        {
            return;
        }

        foreach (var buId in buIds)
        {
            if (!scope.AllowedBuIds.Contains(buId))
            {
                // Anti-enumeração: 404 genérico sem revelar existência de BU em outro tenant
                throw new BuIdOutOfScopeException();
            }
        }
    }

    /// <summary>
    /// Converte string de tipo de relatório (case-insensitive) para <see cref="ReportType"/>.
    /// Lança <see cref="InvalidReportTypeException"/> se não reconhecido.
    /// </summary>
    private static ReportType ParseReportType(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "funnel"      => ReportType.Funnel,
            "forecast"    => ReportType.Forecast,
            "ranking"     => ReportType.Ranking,
            "channel"     => ReportType.Channel,
            "channels"    => ReportType.Channel,
            "commissions" => ReportType.Commissions,
            _             => throw new InvalidReportTypeException(type)
        };
    }
}
