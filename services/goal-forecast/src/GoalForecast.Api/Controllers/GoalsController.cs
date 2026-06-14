using System.Security.Claims;
using GoalForecast.Application.Commands;
using GoalForecast.Application.Common;
using GoalForecast.Application.Ports;
using GoalForecast.Application.Queries;
using GoalForecast.Domain.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GoalForecast.Contracts;
using ContractsGoalDto = GoalForecast.Contracts.GoalDto;
using ContractsGoalListResponse = GoalForecast.Contracts.GoalListResponse;

namespace GoalForecast.Api.Controllers;

/// <summary>
/// Controller REST para o recurso Goals: POST, PUT e GET de metas.
///
/// Responsabilidades da camada API:
/// - Extrair principal (tenant, user, role, bu) do token JWT.
/// - Mapear request para command/query.
/// - Mapear resultado para response HTTP com código correto.
/// - Referenciar catálogo de erros GF-ERR-* (design §12).
///
/// Sem lógica de negócio neste controller — toda regra está na Application.
///
/// Mapeia: design §8.1, §8.2, §8.3, TASK-22, TASK-23.
/// </summary>
[ApiController]
[Route("api/v1/goals")]
[Authorize]
public sealed class GoalsController : ControllerBase
{
    private readonly IMediator _mediator;

    public GoalsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ── POST /api/v1/goals — criar ou atualizar meta (upsert) ────────────────
    // Mapeia: design §8.1, Req 1, Req 2, Req 4.
    // Retorna: 201 (criação) ou 200 (atualização), GoalDto em ambos os casos.
    // Erros: 400 (GF-ERR-001/002/003), 403 (GF-ERR-006), 409 (GF-ERR-005), 422 (GF-ERR-004).

    /// <summary>
    /// Cria ou atualiza uma meta (upsert idempotente por chave natural).
    /// TenantId extraído do token JWT — nunca do body (Req 12.1).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ContractsGoalDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ContractsGoalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Post(
        [FromBody] CreateOrUpdateGoalRequest request,
        CancellationToken cancellationToken)
    {
        var principal = ExtractPrincipal();
        var command = MapToUpsertCommand(principal, request);
        var result = await _mediator.Send(command, cancellationToken);

        var responseDto = MapToContractDto(result.Goal);
        return result.Created
            ? StatusCode(StatusCodes.Status201Created, responseDto)
            : Ok(responseDto);
    }

    // ── PUT /api/v1/goals/{id} — atualizar valor_meta por id ─────────────────
    // Mapeia: design §8.2, Req 4.
    // Retorna: 200, GoalDto. Erros: 400, 403, 404 (GF-ERR-007), 409.

    /// <summary>
    /// Atualiza o valor_meta de uma meta existente por id.
    /// TenantId extraído do token — proteção de cross-tenant (INV-4).
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ContractsGoalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Put(
        [FromRoute] Guid id,
        [FromBody] UpdateGoalValueRequest body,
        CancellationToken cancellationToken)
    {
        var principal = ExtractPrincipal();
        var command = new UpdateGoalByIdCommand
        {
            Principal = principal,
            GoalId = id,
            ValorMeta = body.ValorMeta
        };

        var appDto = await _mediator.Send(command, cancellationToken);
        return Ok(MapToContractDto(appDto));
    }

    // ── GET /api/v1/goals — listar metas com RBAC e paginação ────────────────
    // Mapeia: design §8.3, Req 3, Req 12.

    /// <summary>
    /// Lista metas do tenant com filtro RBAC por escopo de visibilidade.
    /// Vendedor: só owner_id próprio; Gestor: sua BU; Admin/Executivo: tenant.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ContractsGoalListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(
        [FromQuery] Guid? buId,
        [FromQuery] Guid? ownerId,
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (pageSize > 200)
            return BadRequest(new { errorCode = "GF-ERR-VAL", detail = "pageSize não pode exceder 200." });

        var principal = ExtractPrincipal();
        var query = new ListGoalsQuery
        {
            Principal = principal,
            BuId = buId,
            OwnerId = ownerId,
            Year = year,
            Month = month,
            Page = page < 1 ? 1 : page,
            PageSize = Math.Clamp(pageSize, 1, 200)
        };

        var result = await _mediator.Send(query, cancellationToken);
        var items = result.Items.Select(MapToContractDto).ToList();
        return Ok(new ContractsGoalListResponse(items, result.Page, result.PageSize, result.Total));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Extrai o principal autenticado dos claims do token JWT.
    /// TenantId, UserId, Role e BuId são sempre do token — nunca do payload (Req 12.1).
    /// </summary>
    private GoalPrincipal ExtractPrincipal()
    {
        var claims = User.Claims.ToList();

        var tenantId = ParseGuid(claims, "tenantId") ?? Guid.Empty;
        var userId = ParseGuid(claims, "userId")
            ?? ParseGuid(claims, ClaimTypes.NameIdentifier)
            ?? Guid.Empty;
        var buId = ParseGuid(claims, "buId");
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
        _ => GoalRole.Vendedor  // papel menos privilegiado como fallback defensivo
    };

    private static Guid? ParseGuid(IEnumerable<Claim> claims, string type)
    {
        var value = claims.FirstOrDefault(c => c.Type == type)?.Value;
        return value is not null && Guid.TryParse(value, out var g) ? g : null;
    }

    private static CreateOrUpdateGoalCommand MapToUpsertCommand(
        GoalPrincipal principal,
        CreateOrUpdateGoalRequest request) =>
        new()
        {
            Principal = principal,
            Scope = request.Scope,
            BuId = request.BuId,
            OwnerId = request.OwnerId,
            Year = request.Year,
            Month = request.Month,
            ValorMeta = request.ValorMeta
        };

    private static ContractsGoalDto MapToContractDto(Application.Common.GoalDto dto) =>
        new(
            Id: dto.Id,
            Scope: dto.Scope,
            BuId: dto.BuId,
            OwnerId: dto.OwnerId,
            Year: dto.Year,
            Month: dto.Month,
            ValorMeta: dto.ValorMeta,
            CreatedAt: dto.CreatedAt,
            UpdatedAt: dto.UpdatedAt);
}

/// <summary>
/// Body do PUT /api/v1/goals/{id}: apenas o novo valor da meta.
/// Protege over-posting — nenhum outro campo aceito (design §10).
/// </summary>
public sealed record UpdateGoalValueRequest(long ValorMeta);
