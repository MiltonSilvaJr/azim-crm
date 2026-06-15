using System.Security.Claims;
using GoalForecast.Application.Queries;
using GoalForecast.Contracts;
using GoalForecast.Domain.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoalForecast.Api.Controllers;

/// <summary>
/// Controller REST para o recurso Forecast: painel comparativo e agregação derivada.
///
/// Operação total: GET /api/v1/forecast nunca retorna 404.
/// Pipeline indisponível → pipelineUnavailable=true no body, nunca 5xx.
/// Sem meta cadastrada → valorMeta/gap/pctAtingimento nulos no body (DD-006).
/// pctAtingimento nunca serializado como NaN.
///
/// Mapeia: design §8.4, §8.5, Req 5, Req 6, Req 7, PBT-03, PBT-04, TASK-24.
/// </summary>
[ApiController]
[Route("api/v1/forecast")]
[Authorize]
public sealed class ForecastController : ControllerBase
{
    private readonly IMediator _mediator;

    public ForecastController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ── GET /api/v1/forecast — painel comparativo "Direção" ───────────────────
    // Operação total: sempre 200 para qualquer período/escopo válido (PBT-04).
    // Erros: 400 (parâmetros inválidos), 403 (RBAC).

    /// <summary>
    /// Retorna o painel comparativo meta vs realizado vs pipeline para uma BU/período.
    /// Nunca retorna 404; sem meta → campos nulos; pipeline indisponível → pipelineUnavailable=true.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ForecastPanelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPanel(
        [FromQuery] Guid? buId,
        [FromQuery] Guid? ownerId,
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken cancellationToken)
    {
        if (buId is null)
            return BadRequest(new { errorCode = "GF-ERR-VAL", detail = "buId é obrigatório." });
        if (year is null)
            return BadRequest(new { errorCode = "GF-ERR-VAL", detail = "year é obrigatório." });
        if (month is null)
            return BadRequest(new { errorCode = "GF-ERR-VAL", detail = "month é obrigatório." });

        var principal = ExtractPrincipal();
        var query = new GetForecastPanelQuery
        {
            Principal = principal,
            BuId = buId.Value,
            OwnerId = ownerId,
            Year = year.Value,
            Month = month.Value
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(MapToResponse(result));
    }

    // ── GET /api/v1/forecast/aggregate — agregação derivada por trimestre/ano ──
    // Mapeia: design §8.5, Req 7, PBT-02.

    /// <summary>
    /// Retorna a soma derivada de metas por trimestre ou ano.
    /// Meses ausentes contribuem com zero (RN-027).
    /// valorMetaAgregado em long (centavos inteiros).
    /// </summary>
    [HttpGet("aggregate")]
    [ProducesResponseType(typeof(ForecastAggregateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAggregate(
        [FromQuery] Guid? buId,
        [FromQuery] Guid? ownerId,
        [FromQuery] int? year,
        [FromQuery] string? granularity,
        [FromQuery] int? quarter,
        CancellationToken cancellationToken)
    {
        if (buId is null)
            return BadRequest(new { errorCode = "GF-ERR-VAL", detail = "buId é obrigatório." });
        if (year is null)
            return BadRequest(new { errorCode = "GF-ERR-VAL", detail = "year é obrigatório." });

        var parsedGranularity = ParseGranularity(granularity);
        if (parsedGranularity is null)
            return BadRequest(new { errorCode = "GF-ERR-VAL", detail = "granularity deve ser 'quarter' ou 'year'." });

        if (parsedGranularity == AggregateGranularity.Quarter && quarter is null)
            return BadRequest(new { errorCode = "GF-ERR-VAL", detail = "quarter é obrigatório quando granularity=quarter (1..4)." });

        var principal = ExtractPrincipal();
        var query = new GetGoalAggregateQuery
        {
            Principal = principal,
            BuId = buId.Value,
            OwnerId = ownerId,
            Year = year.Value,
            Granularity = parsedGranularity.Value,
            Quarter = quarter
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(new ForecastAggregateResponse(
            result.Granularity,
            result.Year,
            result.Quarter,
            result.ValorMetaAgregado,
            result.Currency));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Extrai o principal autenticado dos claims do token JWT.
    /// TenantId, UserId, Role e BuId são sempre do token — nunca do payload.
    /// </summary>
    private GoalPrincipal ExtractPrincipal()
    {
        var claims = User.Claims.ToList();

        var tenantId = ParseGuidClaim(claims, "tenantId") ?? Guid.Empty;
        var userId = ParseGuidClaim(claims, "userId")
            ?? ParseGuidClaim(claims, ClaimTypes.NameIdentifier)
            ?? Guid.Empty;
        var buId = ParseGuidClaim(claims, "buId");
        var roleStr = claims.FirstOrDefault(c => c.Type == "role")?.Value ?? string.Empty;
        var role = ParseRole(roleStr);

        return new GoalPrincipal(tenantId, userId, role, buId);
    }

    private static GoalRole ParseRole(string roleStr) => roleStr switch
    {
        "TenantAdmin" => GoalRole.TenantAdmin,
        "GestorDeBu" => GoalRole.GestorDeBu,
        "Executivo" => GoalRole.Executivo,
        "Vendedor" => GoalRole.Vendedor,
        _ => GoalRole.Vendedor
    };

    private static Guid? ParseGuidClaim(IEnumerable<Claim> claims, string type)
    {
        var value = claims.FirstOrDefault(c => c.Type == type)?.Value;
        return value is not null && Guid.TryParse(value, out var g) ? g : null;
    }

    /// <summary>
    /// Converte granularity string para enum.
    /// Retorna null para valores inválidos.
    /// </summary>
    private static AggregateGranularity? ParseGranularity(string? granularity) =>
        granularity?.ToLowerInvariant() switch
        {
            "quarter" => AggregateGranularity.Quarter,
            "year" => AggregateGranularity.Year,
            _ => null
        };

    /// <summary>
    /// Mapeia ForecastPanelResult para ForecastPanelResponse do Contracts.
    /// Garante que pctAtingimento nunca é NaN (design §8.4, Req 6).
    /// double NaN/Infinity → null na resposta (RNF-6).
    /// </summary>
    private static ForecastPanelResponse MapToResponse(ForecastPanelResult result)
    {
        // Garante que NaN/Infinity nunca chegam ao JSON (design §8.4)
        double? pct = result.PctAtingimento;
        if (pct.HasValue && (double.IsNaN(pct.Value) || double.IsInfinity(pct.Value)))
            pct = null;

        return new ForecastPanelResponse(
            result.Scope,
            result.BuId,
            result.OwnerId,
            result.Year,
            result.Month,
            result.ValorMeta,
            result.Realizado,
            result.PipelineDisponivel,
            result.Gap,
            pct,
            result.PipelineUnavailable);
    }
}
