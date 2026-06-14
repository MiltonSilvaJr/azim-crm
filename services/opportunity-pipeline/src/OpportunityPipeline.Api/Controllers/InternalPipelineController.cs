using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpportunityPipeline.Api.Auth;
using OpportunityPipeline.Application.Common;
using OpportunityPipeline.Application.Opportunities.Queries;
using OpportunityPipeline.Contracts.Responses;
using OpportunityPipeline.Infrastructure.Scheduling;

namespace OpportunityPipeline.Api.Controllers;

/// <summary>
/// Controller de endpoints internos — acesso restrito à identidade de serviço (mTLS simulado).
/// Rotas: POST /internal/stale-scan, GET /internal/pipeline/stale, GET /internal/pipeline/forecast.
/// Autenticação: X-Service-Identity (ServiceIdentityAuthHandler) — em produção: mTLS + Cloud Identity Token.
/// Sem acesso JWT de usuário. Sem Idempotency-Key documentado pelo OpenAPI (excluído no filtro).
/// Mapeia: design §8, §10 (ServiceIdentity), TASK-21.
/// </summary>
[ApiController]
[Route("internal")]
[Authorize(Policy = "ServiceIdentity", AuthenticationSchemes = ServiceIdentityAuthHandler.SchemeName)]
[Produces("application/json")]
public sealed class InternalPipelineController(
    StaleScanEndpointHandler staleScanHandler,
    IOpportunityQueryRepository queryRepository)
    : ControllerBase
{
    // =========================================================================
    // POST /internal/stale-scan — Disparar detecção de estagnação (scheduler)
    // =========================================================================

    /// <summary>
    /// Dispara detecção de estagnação para o tenant/BU informados.
    /// Chamado pelo Cloud Scheduler (identidade: "cloud-scheduler" ou "stale-scanner").
    /// Idempotente: reexecuções não duplicam alertas (PBT-09, DD-005).
    /// Retorna 202 Accepted com contagem de marcados.
    /// </summary>
    [HttpPost("stale-scan")]
    [ProducesResponseType(typeof(StaleScanResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> StaleScanAsync(
        [FromQuery] Guid tenant_id,
        [FromQuery] Guid bu_id,
        CancellationToken cancellationToken)
    {
        var result = await staleScanHandler.HandleAsync(tenant_id, bu_id, cancellationToken).ConfigureAwait(false);

        var response = new StaleScanResponse
        {
            TenantId = result.TenantId,
            BuId = result.BuId,
            DetectionPeriod = result.DetectionPeriod,
            MarkedStaleCount = result.MarkedStaleCount,
            ExecutedAt = result.ExecutedAt
        };

        return Accepted(response);
    }

    // =========================================================================
    // GET /internal/pipeline/stale — Listar oportunidades estagnadas (digest)
    // =========================================================================

    /// <summary>
    /// Retorna oportunidades estagnadas para digest de serviço.
    /// Chamado pelo serviço de digest interno.
    /// Parâmetros: tenant_id, bu_id, page (1-based), page_size (máx 200).
    /// </summary>
    [HttpGet("pipeline/stale")]
    [ProducesResponseType(typeof(OpportunityListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetStaleAsync(
        [FromQuery] Guid tenant_id,
        [FromQuery] Guid bu_id,
        [FromQuery] int page = 1,
        [FromQuery] int page_size = 50,
        CancellationToken cancellationToken = default)
    {
        var pageRequest = new PageRequest(Math.Max(1, page), Math.Clamp(page_size, 1, 200));
        var result = await queryRepository.ListStaleAsync(tenant_id, bu_id, pageRequest, cancellationToken).ConfigureAwait(false);

        var response = new OpportunityListResponse
        {
            Items = result.Items.Select(s => new OpportunitySummaryResponse
            {
                Id = s.Id,
                OpportunityNumber = s.Number,
                Title = s.Title,
                OwnerId = s.OwnerId,
                AccountId = s.AccountId,
                PartnerId = s.PartnerId,
                StageName = s.StageName,
                StageCategory = s.StageCategory.ToString(),
                ValorTotal = s.TotalValueCents,
                ForecastPonderado = s.ForecastPonderadoCents,
                IsStale = s.IsStale,
                IsOverdue = s.IsOverdue,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt
            }).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };

        return Ok(response);
    }

    // =========================================================================
    // GET /internal/pipeline/forecast — Forecast para serviço de metas
    // =========================================================================

    /// <summary>
    /// Retorna forecast agregado para o serviço de metas (goal-forecast).
    /// Parâmetros: tenant_id, bu_id.
    /// </summary>
    [HttpGet("pipeline/forecast")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetForecastAsync(
        [FromQuery] Guid tenant_id,
        [FromQuery] Guid bu_id,
        CancellationToken cancellationToken = default)
    {
        var (totalValueCents, forecastPonderadoCents, forecastLiquidoCents) =
            await queryRepository.GetForecastAsync(tenant_id, bu_id, cancellationToken).ConfigureAwait(false);

        return Ok(new
        {
            tenant_id,
            bu_id,
            total_value_cents = totalValueCents,
            forecast_ponderado_cents = forecastPonderadoCents,
            forecast_liquido_cents = forecastLiquidoCents
        });
    }
}
