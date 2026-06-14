using System.Security.Claims;
using GoalForecast.Application.Queries;
using GoalForecast.Contracts;
using GoalForecast.Domain.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoalForecast.Api.Controllers;

/// <summary>
/// Controller interno para consumo exclusivo do Digest worker (BC-06).
///
/// Autenticação: ServiceScope (mTLS ou escopo de serviço — não exposta publicamente).
/// Rota: /api/v1/internal/goals/digest-block (prefixo /internal segregado do gateway).
/// Não exposta no Swagger público (ApiExplorerSettings.IgnoreApi = true).
///
/// Semântica da resposta (Req 9.2, RN-018):
/// - present=true → meta cadastrada; Digest inclui o bloco.
/// - present=false → meta ausente; Digest omite o bloco completamente.
///
/// Mapeia: design §8.6, §10, Req 9, RN-018, RN-029, TASK-25.
/// </summary>
[ApiController]
[Route("api/v1/internal/goals")]
[Authorize(Policy = "ServiceScope")]
[ApiExplorerSettings(IgnoreApi = true)]  // Não exposta no Swagger público (design §8.6)
public sealed class InternalGoalsController : ControllerBase
{
    private readonly IMediator _mediator;

    public InternalGoalsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ── GET /api/v1/internal/goals/digest-block ───────────────────────────────
    // Uso exclusivo: Digest worker (BC-06).
    // Retorna: 200 com present=true (meta encontrada) ou present=false (ausência).
    // Erros: 400 (parâmetros inválidos), 403 (sem ServiceScope).

    /// <summary>
    /// Retorna o bloco de metas para o Digest worker.
    /// present=false indica ausência de meta — o Digest deve omitir o bloco completamente.
    /// Todos os valores monetários em centavos inteiros (long).
    /// </summary>
    [HttpGet("digest-block")]
    [ProducesResponseType(typeof(DigestBlockResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDigestBlock(
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
        var query = new GetGoalDigestBlockQuery
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Extrai o principal de serviço dos claims do token.
    /// Para chamadores internos, o role é ServiceScope.
    /// </summary>
    private GoalPrincipal ExtractPrincipal()
    {
        var claims = User.Claims.ToList();

        var tenantId = ParseGuidClaim(claims, "tenantId") ?? Guid.Empty;
        var userId = ParseGuidClaim(claims, "userId")
            ?? ParseGuidClaim(claims, ClaimTypes.NameIdentifier)
            ?? Guid.Empty;
        var buId = ParseGuidClaim(claims, "buId");

        // ServiceScope: papéis internos mapeados para TenantAdmin (visibilidade total) —
        // autorização real é feita pela política ServiceScope no [Authorize].
        return new GoalPrincipal(tenantId, userId, GoalRole.TenantAdmin, buId);
    }

    private static Guid? ParseGuidClaim(IEnumerable<Claim> claims, string type)
    {
        var value = claims.FirstOrDefault(c => c.Type == type)?.Value;
        return value is not null && Guid.TryParse(value, out var g) ? g : null;
    }

    /// <summary>
    /// Mapeia DigestBlockResult (Application) para DigestBlockResponse (Contracts).
    /// </summary>
    private static DigestBlockResponse MapToResponse(DigestBlockResult result) =>
        result.Present
            ? DigestBlockResponse.WithMeta(
                result.ValorMeta!.Value,
                result.Realizado,
                result.Gap,
                result.PipelineDisponivel,
                result.PipelineUnavailable)
            : DigestBlockResponse.Absent();
}
