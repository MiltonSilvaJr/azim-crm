using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using Reporting.Application.Ports;
using Reporting.Contracts.ReadModels;
using Reporting.Domain.ValueObjects;

namespace Reporting.Infrastructure.Persistence;

/// <summary>
/// Implementação de <see cref="IReportingReadRepository"/> usando Dapper sobre PostgreSQL.
///
/// Todas as queries são read-only (sem <c>SaveChanges</c>) e usam SQL parametrizado (anti-SQLi).
/// O interceptor de tenant (<see cref="TenantConnectionInterceptor"/>) aplica
/// <c>SET app.current_tenant</c> antes de cada query, ativando a RLS nas views
/// com <c>security_invoker = true</c> (ADR-0001, DD-005).
///
/// Sem concatenação de strings de filtro — apenas parâmetros Dapper com prefixo <c>@</c>.
///
/// Mapeia: TASK-19, design §6.1, §6.4, DD-003, DD-005, ADR-0001, Req 1–6.
/// </summary>
public sealed class ReportingReadRepository : IReportingReadRepository
{
    private readonly string _connectionString;
    private readonly TenantConnectionInterceptor _interceptor;
    private readonly ILogger<ReportingReadRepository> _logger;

    /// <summary>Inicializa o repositório de leitura com a connection string e o interceptor de tenant.</summary>
    public ReportingReadRepository(
        string connectionString,
        TenantConnectionInterceptor interceptor,
        ILogger<ReportingReadRepository> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(interceptor);
        ArgumentNullException.ThrowIfNull(logger);
        _connectionString = connectionString;
        _interceptor = interceptor;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<FunnelRow>> GetFunnelAsync(
        ReportScope scope,
        Period period,
        IEnumerable<Guid>? buIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(period);

        await using var connection = await OpenConnectionAsync(scope.TenantId, cancellationToken);

        var (whereClause, parameters) = BuildScopeAndPeriodFilter(scope, period, buIds, "created_at");

        var sql = $"""
            SELECT
                stage_id        AS {nameof(FunnelRow.StageId)},
                stage_name      AS {nameof(FunnelRow.StageName)},
                stage_category  AS {nameof(FunnelRow.Category)},
                COUNT(*)::int   AS {nameof(FunnelRow.Count)},
                COALESCE(SUM(total_cents), 0)::bigint             AS {nameof(FunnelRow.TotalCents)},
                COALESCE(SUM(weighted_forecast_cents), 0)::bigint AS {nameof(FunnelRow.WeightedForecastCents)}
            FROM vw_funnel_report
            WHERE tenant_id = @tenantId
            {whereClause}
            GROUP BY stage_id, stage_name, stage_category
            ORDER BY stage_category, stage_name
            """;

        var rows = await connection.QueryAsync<FunnelRow>(sql, parameters);
        return rows.ToList().AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ForecastRow>> GetForecastAsync(
        ReportScope scope,
        Period period,
        IEnumerable<Guid>? buIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(period);

        await using var connection = await OpenConnectionAsync(scope.TenantId, cancellationToken);

        var (whereClause, parameters) = BuildScopeAndPeriodFilter(scope, period, buIds, "created_at");

        var sql = $"""
            SELECT
                bu_id                           AS {nameof(ForecastRow.BuId)},
                '' :: text                      AS {nameof(ForecastRow.BuName)},
                year                            AS {nameof(ForecastRow.Year)},
                month                           AS {nameof(ForecastRow.Month)},
                COALESCE(SUM(weighted_forecast_cents), 0)::bigint AS {nameof(ForecastRow.WeightedForecastCents)},
                COALESCE(SUM(realized_cents), 0)::bigint          AS {nameof(ForecastRow.RealizedCents)},
                MAX(goal_cents)::bigint                           AS {nameof(ForecastRow.GoalCents)}
            FROM vw_forecast_report
            WHERE tenant_id = @tenantId
              AND closed_at >= @from
              AND closed_at <= @to
            {whereClause}
            GROUP BY bu_id, year, month
            ORDER BY bu_id, year, month
            """;

        var rows = await connection.QueryAsync<ForecastRow>(sql, parameters);
        return rows.ToList().AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RankingRow>> GetRankingAsync(
        ReportScope scope,
        Period period,
        IEnumerable<Guid>? buIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(period);

        await using var connection = await OpenConnectionAsync(scope.TenantId, cancellationToken);

        var (whereClause, parameters) = BuildScopeAndPeriodFilter(scope, period, buIds, "created_at");

        // display_name incluído na query — PiiMinimizationPolicy decide inclusão no handler (DD-008).
        // Nunca loga o valor de display_name.
        var sql = $"""
            SELECT
                owner_id                                  AS {nameof(RankingRow.OwnerId)},
                MAX(display_name)                         AS {nameof(RankingRow.DisplayName)},
                COUNT(*) FILTER (WHERE stage_category = 'won')::int AS {nameof(RankingRow.WonCount)},
                COALESCE(SUM(total_cents) FILTER (WHERE stage_category = 'won'), 0)::bigint AS {nameof(RankingRow.WonValueCents)},
                COALESCE(SUM(weighted_forecast_cents) FILTER (WHERE stage_category = 'open'), 0)::bigint AS {nameof(RankingRow.PipelineForecastCents)}
            FROM vw_ranking_report
            WHERE tenant_id = @tenantId
            {whereClause}
            GROUP BY owner_id
            ORDER BY {nameof(RankingRow.WonValueCents)} DESC
            """;

        var rows = await connection.QueryAsync<RankingRow>(sql, parameters);
        return rows.ToList().AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ChannelRow>> GetChannelAsync(
        ReportScope scope,
        Period period,
        IEnumerable<Guid>? buIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(period);

        await using var connection = await OpenConnectionAsync(scope.TenantId, cancellationToken);

        var (whereClause, parameters) = BuildScopeAndPeriodFilter(scope, period, buIds, "created_at");

        // PercentBasisPoints calculado pelo handler via ChannelShare (DD-010, PBT-04).
        // A view retorna linhas brutas; o handler agrega e calcula percentuais.
        var sql = $"""
            SELECT
                channel_id                          AS {nameof(ChannelRow.ChannelId)},
                MAX(channel_name)                   AS {nameof(ChannelRow.ChannelName)},
                COUNT(*)::int                       AS {nameof(ChannelRow.Count)},
                COALESCE(SUM(total_cents), 0)::bigint AS {nameof(ChannelRow.TotalCents)},
                0::int                              AS {nameof(ChannelRow.PercentBasisPoints)}
            FROM vw_channel_report
            WHERE tenant_id = @tenantId
              AND channel_id IS NOT NULL
            {whereClause}
            GROUP BY channel_id
            ORDER BY {nameof(ChannelRow.TotalCents)} DESC
            """;

        var rows = await connection.QueryAsync<ChannelRow>(sql, parameters);
        return rows.ToList().AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CommissionRow>> GetCommissionsAsync(
        ReportScope scope,
        Period period,
        IEnumerable<Guid>? buIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(period);

        await using var connection = await OpenConnectionAsync(scope.TenantId, cancellationToken);

        var (whereClause, parameters) = BuildScopeAndPeriodFilter(scope, period, buIds, "created_at");

        // Linhas brutas — o handler separa is_snapshot=true (consolidado) de is_snapshot=false (projetado).
        // Não agrega aqui para que PBT-01 seja validado no handler (design §5.3, Req 4.2).
        var sql = $"""
            SELECT
                partner_id      AS {nameof(CommissionRow.PartnerId)},
                partner_name    AS {nameof(CommissionRow.PartnerName)},
                commission_cents AS {nameof(CommissionRow.CommissionCents)},
                is_snapshot     AS {nameof(CommissionRow.IsSnapshot)},
                stage_category  AS {nameof(CommissionRow.StageCategory)}
            FROM vw_commission_report
            WHERE tenant_id = @tenantId
            {whereClause}
            ORDER BY partner_id, is_snapshot
            """;

        var rows = await connection.QueryAsync<CommissionRow>(sql, parameters);
        return rows.ToList().AsReadOnly();
    }

    // ───────────────────────────── helpers privados ──────────────────────────────

    /// <summary>
    /// Abre uma conexão PostgreSQL e aplica <c>SET app.current_tenant</c> via interceptor.
    /// </summary>
    private async Task<NpgsqlConnection> OpenConnectionAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await _interceptor.ApplyAsync(connection, tenantId, cancellationToken);
        return connection;
    }

    /// <summary>
    /// Constrói a cláusula WHERE e os parâmetros Dapper para filtro de escopo RBAC e período.
    ///
    /// Sem concatenação de strings — apenas predicados parametrizados (anti-SQLi).
    /// O tenant_id já é filtrado na cláusula raiz da query chamadora (redundância defensiva).
    /// </summary>
    private static (string WhereClause, DynamicParameters Parameters) BuildScopeAndPeriodFilter(
        ReportScope scope,
        Period period,
        IEnumerable<Guid>? buIds,
        string dateColumn)
    {
        var parameters = new DynamicParameters();
        parameters.Add("tenantId", scope.TenantId);
        parameters.Add("from", period.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        parameters.Add("to", period.To.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));

        var clauses = new List<string>
        {
            $"AND {dateColumn} >= @from",
            $"AND {dateColumn} <= @to"
        };

        // Predicado RBAC de escopo (DD-006, design §5.3)
        switch (scope.Role)
        {
            case Reporting.Domain.Enums.ReportingRole.Vendedor:
                clauses.Add("AND owner_id = @ownerId");
                parameters.Add("ownerId", scope.OwnerRestrictedTo);
                break;

            case Reporting.Domain.Enums.ReportingRole.GestorBU:
                // buIds do scope sempre prioritários sobre o filtro explícito do cliente
                var effectiveBuIds = buIds?.ToList() ?? [];
                var allowedBuIds = scope.AllowedBuIds
                    .Where(id => effectiveBuIds.Count == 0 || effectiveBuIds.Contains(id))
                    .ToArray();
                clauses.Add("AND bu_id = ANY(@buIds)");
                parameters.Add("buIds", allowedBuIds);
                break;

            case Reporting.Domain.Enums.ReportingRole.TenantAdmin:
                // Sem predicado adicional além do tenant_id já filtrado
                if (buIds is not null)
                {
                    var buList = buIds.ToArray();
                    if (buList.Length > 0)
                    {
                        clauses.Add("AND bu_id = ANY(@buIds)");
                        parameters.Add("buIds", buList);
                    }
                }
                break;

            default:
                // PlatformOperator já bloqueado no AuthorizationBehavior (REPORT-ERR-005)
                throw new UnauthorizedAccessException(
                    $"Papel {scope.Role} não possui acesso ao repositório. (DD-006, RNF 5)");
        }

        return (string.Join(" ", clauses), parameters);
    }
}
