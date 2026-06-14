using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Dispatching;

namespace Reporting.Api.Controllers;

/// <summary>
/// Controller REST do módulo reporting.
///
/// Base path: <c>/api/v1/reports</c> (design §8.2).
/// Autenticação JWT obrigatória em todos os endpoints (<see cref="AuthorizeAttribute"/>).
/// Sem lógica de negócio — delega para Application via <see cref="IReportDispatcher"/> (design §3, Clean Architecture).
/// Nunca importa tipos do Domain diretamente (design §3, ADR-0001).
///
/// Endpoints:
/// <list type="bullet">
///   <item><description>GET /funnel — funil por estágio (Req 1)</description></item>
///   <item><description>GET /forecast — forecast por BU/mês (Req 6)</description></item>
///   <item><description>GET /ranking — ranking por responsável (Req 2)</description></item>
///   <item><description>GET /channels — oportunidades por canal (Req 3)</description></item>
///   <item><description>GET /commissions — comissões por parceiro (Req 4)</description></item>
///   <item><description>GET /{type}/export — export CSV (Req 5)</description></item>
/// </list>
///
/// Catálogo de erros (design §12): REPORT-ERR-001..009.
/// Anti-enumeração: buId fora do escopo → 404 genérico (nunca revela existência em outro tenant).
/// X-Correlation-Id: propagado em todo response (RNF 6, ADR-0001).
///
/// Mapeia: TASK-21, design §3, §8.1, §8.2, §8.3, §12, Req 1–6, Req 7, Req 8, RNF 5.
/// </summary>
[ApiController]
[Route("api/v1/reports")]
[Authorize]
public sealed class ReportingController : ControllerBase
{
    private readonly IReportDispatcher _dispatcher;

    /// <summary>Inicializa o controller com o dispatcher de relatórios.</summary>
    public ReportingController(IReportDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
    }

    // ─── Funil por estágio (Req 1) ────────────────────────────────────────────

    /// <summary>
    /// Retorna o relatório de funil por estágio para o período e BUs informados.
    /// </summary>
    /// <param name="from">Data de início do período (YYYY-MM-DD). Default: primeiro dia do mês corrente.</param>
    /// <param name="to">Data de fim do período (YYYY-MM-DD). Default: último dia do mês corrente.</param>
    /// <param name="buId">BUs para filtrar (repetível). Opcional.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>200 com <c>FunnelReportResponse</c> ou erro do catálogo.</returns>
    [HttpGet("funnel")]
    public async Task<IActionResult> GetFunnelAsync(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery(Name = "buId")] IReadOnlyList<Guid>? buId,
        CancellationToken cancellationToken)
    {
        var (effectiveFrom, effectiveTo) = ApplyPeriodDefault(from, to);
        var (tenantId, userId, correlationId) = GetRequestContext();

        var response = await _dispatcher.GetFunnelAsync(
            tenantId, userId, correlationId,
            effectiveFrom, effectiveTo, buId,
            cancellationToken);

        return Ok(response);
    }

    // ─── Forecast por BU/mês (Req 6) ─────────────────────────────────────────

    /// <summary>
    /// Retorna o relatório de forecast por BU/mês.
    /// Meta ausente retorna <c>goalCents = null</c> sem erro (degradação graciosa, Req 6.3).
    /// </summary>
    [HttpGet("forecast")]
    public async Task<IActionResult> GetForecastAsync(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery(Name = "buId")] IReadOnlyList<Guid>? buId,
        CancellationToken cancellationToken)
    {
        var (effectiveFrom, effectiveTo) = ApplyPeriodDefault(from, to);
        var (tenantId, userId, correlationId) = GetRequestContext();

        var response = await _dispatcher.GetForecastAsync(
            tenantId, userId, correlationId,
            effectiveFrom, effectiveTo, buId,
            cancellationToken);

        return Ok(response);
    }

    // ─── Ranking por responsável (Req 2) ─────────────────────────────────────

    /// <summary>
    /// Retorna o relatório de ranking por responsável.
    /// Vendedor recebe apenas a própria linha. <c>displayName</c> omitido fora do escopo RBAC (DD-008).
    /// </summary>
    [HttpGet("ranking")]
    public async Task<IActionResult> GetRankingAsync(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery(Name = "buId")] IReadOnlyList<Guid>? buId,
        CancellationToken cancellationToken)
    {
        var (effectiveFrom, effectiveTo) = ApplyPeriodDefault(from, to);
        var (tenantId, userId, correlationId) = GetRequestContext();

        var response = await _dispatcher.GetRankingAsync(
            tenantId, userId, correlationId,
            effectiveFrom, effectiveTo, buId,
            cancellationToken);

        return Ok(response);
    }

    // ─── Oportunidades por canal (Req 3) ─────────────────────────────────────

    /// <summary>
    /// Retorna o relatório de oportunidades por canal de origem.
    /// Percentuais em basis points inteiros (PBT-04, DD-010).
    /// </summary>
    [HttpGet("channels")]
    public async Task<IActionResult> GetChannelsAsync(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery(Name = "buId")] IReadOnlyList<Guid>? buId,
        CancellationToken cancellationToken)
    {
        var (effectiveFrom, effectiveTo) = ApplyPeriodDefault(from, to);
        var (tenantId, userId, correlationId) = GetRequestContext();

        var response = await _dispatcher.GetChannelAsync(
            tenantId, userId, correlationId,
            effectiveFrom, effectiveTo, buId,
            cancellationToken);

        return Ok(response);
    }

    // ─── Comissões por parceiro (Req 4) ──────────────────────────────────────

    /// <summary>
    /// Retorna o relatório de comissões por parceiro — projetado × consolidado.
    /// <c>consolidatedCents</c> derivado exclusivamente de snapshots imutáveis (PBT-01, RN-007).
    /// </summary>
    [HttpGet("commissions")]
    public async Task<IActionResult> GetCommissionsAsync(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery(Name = "buId")] IReadOnlyList<Guid>? buId,
        CancellationToken cancellationToken)
    {
        var (effectiveFrom, effectiveTo) = ApplyPeriodDefault(from, to);
        var (tenantId, userId, correlationId) = GetRequestContext();

        var response = await _dispatcher.GetCommissionsAsync(
            tenantId, userId, correlationId,
            effectiveFrom, effectiveTo, buId,
            cancellationToken);

        return Ok(response);
    }

    // ─── Export CSV (Req 5) ───────────────────────────────────────────────────

    /// <summary>
    /// Gera o CSV do relatório informado e retorna URL assinada de curta validade (~15 min).
    /// Reutiliza exatamente a query do relatório correspondente (PBT-05, design §5.2).
    /// </summary>
    /// <param name="type">Tipo de relatório (funnel, forecast, ranking, channel, commissions).</param>
    /// <param name="from">Data de início do período.</param>
    /// <param name="to">Data de fim do período.</param>
    /// <param name="buId">BUs para filtrar (repetível). Opcional.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    [HttpGet("{type}/export")]
    public async Task<IActionResult> GetExportAsync(
        [FromRoute] string type,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery(Name = "buId")] IReadOnlyList<Guid>? buId,
        CancellationToken cancellationToken)
    {
        var (effectiveFrom, effectiveTo) = ApplyPeriodDefault(from, to);
        var (tenantId, userId, correlationId) = GetRequestContext();

        var response = await _dispatcher.GetExportAsync(
            type, tenantId, userId, correlationId,
            effectiveFrom, effectiveTo, buId,
            cancellationToken);

        return Ok(response);
    }

    // ─── Helpers privados ─────────────────────────────────────────────────────

    /// <summary>
    /// Aplica o default de período = mês corrente quando datas forem omitidas.
    /// </summary>
    private static (DateOnly from, DateOnly to) ApplyPeriodDefault(DateOnly? from, DateOnly? to)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var firstDayOfMonth = new DateOnly(today.Year, today.Month, 1);
        var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
        return (from ?? firstDayOfMonth, to ?? lastDayOfMonth);
    }

    /// <summary>
    /// Extrai <c>tenant_id</c>, <c>sub</c> e <c>X-Correlation-Id</c> do contexto da requisição.
    /// Lança <see cref="InvalidOperationException"/> se claims obrigatórias estiverem ausentes.
    /// </summary>
    private (Guid tenantId, Guid userId, string correlationId) GetRequestContext()
    {
        var tenantClaim = User.FindFirst("tenant_id")?.Value
            ?? throw new InvalidOperationException("Claim 'tenant_id' ausente no token. (ADR-0001)");

        var userClaim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? throw new InvalidOperationException("Claim 'sub' ausente no token. (ADR-0001)");

        var correlationId = HttpContext.Items["CorrelationId"]?.ToString() ?? string.Empty;

        return (Guid.Parse(tenantClaim), Guid.Parse(userClaim), correlationId);
    }
}
