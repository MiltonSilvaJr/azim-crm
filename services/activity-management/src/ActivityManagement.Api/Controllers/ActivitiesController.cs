namespace ActivityManagement.Api.Controllers;

using System.Security.Claims;
using ActivityManagement.Application.Activities.Commands;
using ActivityManagement.Application.Activities.Queries;
using ActivityManagement.Application.Common;
using ActivityManagement.Contracts.Activities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using ContractSuggestion = ActivityManagement.Contracts.Activities.NextActivitySuggestion;

/// <summary>
/// Controller REST para gestão de atividades comerciais (design §8, Req 1–6, Req 8, Req 13).
/// Todos os endpoints exigem autenticação Bearer JWT.
/// Nenhuma lógica de negócio nos métodos — tudo delegado para <see cref="IMediator"/>.
/// Mapeia: design §8, TASK-18.
/// </summary>
[ApiController]
[Route("api/v1/activities")]
[Authorize]
[Produces("application/json")]
public sealed class ActivitiesController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>Inicializa o controller com o mediador da Application.</summary>
    public ActivitiesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ── GET /api/v1/activities ────────────────────────────────────────────────

    /// <summary>Lista atividades do escopo autenticado com filtros opcionais e paginação.</summary>
    /// <param name="owner">Filtrar por dono (opcional).</param>
    /// <param name="type">Filtrar por tipo (opcional).</param>
    /// <param name="status">Filtrar por status (opcional).</param>
    /// <param name="overdue">Retornar apenas vencidas (opcional).</param>
    /// <param name="opportunityId">Filtrar por oportunidade vinculada (opcional).</param>
    /// <param name="page">Número da página (base 1, padrão: 1).</param>
    /// <param name="pageSize">Tamanho da página (padrão: 20).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Página de atividades do escopo autenticado.</returns>
    /// <response code="200">Lista retornada com sucesso.</response>
    /// <response code="403">Papel insuficiente (ACT-ERR-007).</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedActivitiesResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 403)]
    public async Task<IActionResult> GetActivities(
        [FromQuery] Guid?   owner         = null,
        [FromQuery] string? type          = null,
        [FromQuery] string? status        = null,
        [FromQuery] bool    overdue       = false,
        [FromQuery] Guid?   opportunityId = null,
        [FromQuery] int     page          = 1,
        [FromQuery] int     pageSize      = 20,
        CancellationToken cancellationToken = default)
    {
        var ctx     = BuildTenantContext();
        var query   = new ListActivitiesQuery(owner, type, status, overdue, opportunityId, page, pageSize)
            { TenantContext = ctx };
        var result  = await _mediator.Send(query, cancellationToken);

        var response = new PagedActivitiesResponse(
            Items:    result.Items.Select(ActivityMapper.ToResponse).ToList(),
            Total:    result.Total,
            Page:     result.Page,
            PageSize: result.PageSize);

        return Ok(response);
    }

    // ── POST /api/v1/activities ───────────────────────────────────────────────

    /// <summary>Cria uma nova atividade comercial.</summary>
    /// <param name="request">Dados da nova atividade.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Atividade criada com Location header.</returns>
    /// <response code="201">Atividade criada.</response>
    /// <response code="400">Dados inválidos (ACT-ERR-001/002/010).</response>
    /// <response code="403">Papel insuficiente (ACT-ERR-007).</response>
    /// <response code="422">Vínculo de oportunidade/conta inválido (ACT-ERR-005/006).</response>
    [HttpPost]
    [ProducesResponseType(typeof(object), 201)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 403)]
    [ProducesResponseType(typeof(ErrorResponse), 422)]
    public async Task<IActionResult> CreateActivity(
        [FromBody] CreateActivityRequest request,
        CancellationToken cancellationToken = default)
    {
        var ctx     = BuildTenantContext();
        var command = new CreateActivityCommand(
            Type:          request.Type,
            Title:         request.Title,
            DueAt:         request.DueAt,
            OwnerId:       request.OwnerId,
            Priority:      request.Priority,
            Description:   request.Description,
            OpportunityId: request.OpportunityId,
            AccountId:     request.AccountId)
            { TenantContext = ctx };

        var id = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetActivity), new { id }, new { id });
    }

    // ── GET /api/v1/activities/{id} ───────────────────────────────────────────

    /// <summary>Retorna o detalhe de uma atividade pelo ID.</summary>
    /// <param name="id">Identificador da atividade.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Detalhe da atividade.</returns>
    /// <response code="200">Atividade encontrada.</response>
    /// <response code="404">Atividade não encontrada ou inacessível (ACT-ERR-003).</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ActivityResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    public async Task<IActionResult> GetActivity(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var ctx    = BuildTenantContext();
        var query  = new GetActivityByIdQuery(id) { TenantContext = ctx };
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    // ── PUT /api/v1/activities/{id} ───────────────────────────────────────────

    /// <summary>Atualiza os atributos de uma atividade existente.</summary>
    /// <param name="id">Identificador da atividade.</param>
    /// <param name="request">Novos dados da atividade.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="204">Atividade atualizada.</response>
    /// <response code="400">Dados inválidos (ACT-ERR-001/002).</response>
    /// <response code="403">Papel insuficiente (ACT-ERR-007).</response>
    /// <response code="404">Atividade não encontrada (ACT-ERR-003).</response>
    /// <response code="409">Atividade encerrada (ACT-ERR-011).</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 403)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 409)]
    public async Task<IActionResult> UpdateActivity(
        Guid id,
        [FromBody] UpdateActivityRequest request,
        CancellationToken cancellationToken = default)
    {
        var ctx     = BuildTenantContext();
        var command = new UpdateActivityCommand(
            ActivityId:    id,
            Title:         request.Title,
            DueAt:         request.DueAt,
            Priority:      request.Priority,
            Description:   request.Description,
            OpportunityId: request.OpportunityId,
            AccountId:     request.AccountId)
            { TenantContext = ctx };

        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    // ── DELETE /api/v1/activities/{id} ────────────────────────────────────────

    /// <summary>Exclui uma atividade (auditada).</summary>
    /// <param name="id">Identificador da atividade.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="204">Atividade excluída.</response>
    /// <response code="403">Papel insuficiente (ACT-ERR-007).</response>
    /// <response code="404">Atividade não encontrada (ACT-ERR-003).</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ErrorResponse), 403)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    public async Task<IActionResult> DeleteActivity(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var ctx     = BuildTenantContext();
        var command = new DeleteActivityCommand(id) { TenantContext = ctx };
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    // ── PATCH /api/v1/activities/{id}/complete ────────────────────────────────

    /// <summary>
    /// Conclui uma atividade de forma idempotente (Req 6, DD-004).
    /// Reconcluir retorna 200 sem alterar completedAt.
    /// </summary>
    /// <param name="id">Identificador da atividade.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Resultado da conclusão com sugestão de próxima atividade quando aplicável.</returns>
    /// <response code="200">Atividade concluída ou já estava concluída (idempotente).</response>
    /// <response code="404">Atividade não encontrada (ACT-ERR-003).</response>
    /// <response code="409">Transição inválida (ACT-ERR-004).</response>
    [HttpPatch("{id:guid}/complete")]
    [ProducesResponseType(typeof(CompleteActivityResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 409)]
    public async Task<IActionResult> CompleteActivity(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var ctx     = BuildTenantContext();
        var command = new CompleteActivityCommand(id) { TenantContext = ctx };
        var result  = await _mediator.Send(command, cancellationToken);

        // Sugestão de próxima atividade (Req 9, não-bloqueante — DD-006)
        ContractSuggestion? suggestion = null;
        if (!result.WasAlreadyCompleted)
        {
            var suggestionQuery = new SuggestNextActivityQuery(id) { TenantContext = ctx };
            var appSuggestion   = await _mediator.Send(suggestionQuery, cancellationToken);
            if (appSuggestion is not null)
            {
                suggestion = new ContractSuggestion(
                    appSuggestion.OpportunityId,
                    appSuggestion.AccountId);
            }
        }

        return Ok(new CompleteActivityResponse(
            ActivityId:          result.ActivityId,
            CompletedAt:         result.CompletedAt,
            WasAlreadyCompleted: result.WasAlreadyCompleted,
            Suggestion:          suggestion));
    }

    // ── PATCH /api/v1/activities/{id}/reschedule ──────────────────────────────

    /// <summary>Reagenda uma atividade com nova data de vencimento (Req 8).</summary>
    /// <param name="id">Identificador da atividade.</param>
    /// <param name="request">Nova data de vencimento.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Atividade reagendada.</response>
    /// <response code="404">Atividade não encontrada (ACT-ERR-003).</response>
    /// <response code="409">Atividade encerrada (ACT-ERR-011).</response>
    [HttpPatch("{id:guid}/reschedule")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 409)]
    public async Task<IActionResult> RescheduleActivity(
        Guid id,
        [FromBody] RescheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        var ctx     = BuildTenantContext();
        var command = new RescheduleActivityCommand(id, request.DueAt) { TenantContext = ctx };
        var result  = await _mediator.Send(command, cancellationToken);
        return Ok(new { result.ActivityId, result.NewDueAt });
    }

    // ── GET /api/v1/activities/me/day ─────────────────────────────────────────

    /// <summary>Retorna a visão "Meu dia" do vendedor com atividades em três faixas (Req 5).</summary>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Atividades agrupadas em vencidas, hoje e próximas no fuso do tenant.</returns>
    /// <response code="200">Visão retornada com sucesso.</response>
    [HttpGet("me/day")]
    [ProducesResponseType(typeof(MyDayResponse), 200)]
    public async Task<IActionResult> GetMyDay(CancellationToken cancellationToken = default)
    {
        var ctx    = BuildTenantContext();
        var query  = new GetMyDayQuery() { TenantContext = ctx };
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(new MyDayResponse(
            Overdue:  result.Overdue.Select(ActivityMapper.ToResponse).ToList(),
            Today:    result.Today.Select(ActivityMapper.ToResponse).ToList(),
            Upcoming: result.Upcoming.Select(ActivityMapper.ToResponse).ToList()));
    }

    // ── GET /api/v1/activities/me/week ────────────────────────────────────────

    /// <summary>Retorna a visão "Minha semana" do vendedor (Req 5).</summary>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Visão retornada com sucesso.</response>
    [HttpGet("me/week")]
    [ProducesResponseType(typeof(IReadOnlyList<ActivityResponse>), 200)]
    public async Task<IActionResult> GetMyWeek(CancellationToken cancellationToken = default)
    {
        var ctx    = BuildTenantContext();
        var query  = new GetMyWeekQuery() { TenantContext = ctx };
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result.Select(ActivityMapper.ToResponse).ToList());
    }

    // ── GET /api/v1/activities/overdue?owner={id} ────────────────────────────

    /// <summary>Retorna atividades vencidas do usuário (para digest — Req 11.3).</summary>
    /// <param name="owner">ID do usuário (obrigatório).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Atividades vencidas retornadas.</response>
    [HttpGet("overdue")]
    [ProducesResponseType(typeof(IReadOnlyList<ActivityResponse>), 200)]
    public async Task<IActionResult> GetOverdue(
        [FromQuery] Guid owner,
        CancellationToken cancellationToken = default)
    {
        var ctx    = BuildTenantContext();
        var query  = new GetOverdueByUserQuery(owner) { TenantContext = ctx };
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result.Select(ActivityMapper.ToResponse).ToList());
    }

    // ── GET /api/v1/activities/today?owner={id} ───────────────────────────────

    /// <summary>Retorna atividades do dia do usuário (para digest — Req 11.4).</summary>
    /// <param name="owner">ID do usuário (obrigatório).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Atividades do dia retornadas.</response>
    [HttpGet("today")]
    [ProducesResponseType(typeof(IReadOnlyList<ActivityResponse>), 200)]
    public async Task<IActionResult> GetToday(
        [FromQuery] Guid owner,
        CancellationToken cancellationToken = default)
    {
        var ctx    = BuildTenantContext();
        var query  = new GetTodayByUserQuery(owner) { TenantContext = ctx };
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result.Select(ActivityMapper.ToResponse).ToList());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Constrói o <see cref="TenantContext"/> a partir dos claims do usuário autenticado.
    /// </summary>
    private TenantContext BuildTenantContext()
    {
        var userId   = Guid.Parse(User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("Claim 'sub' ausente."));
        var tenantId = Guid.Parse(User.FindFirstValue("tenant_id")
            ?? throw new UnauthorizedAccessException("Claim 'tenant_id' ausente."));
        var buId     = Guid.Parse(User.FindFirstValue("bu_id")
            ?? throw new UnauthorizedAccessException("Claim 'bu_id' ausente."));
        var role     = User.FindFirstValue("role") ?? "viewer";

        var correlationId = HttpContext.Items.TryGetValue("CorrelationId", out var obj)
            && obj is Guid corrId
            ? corrId
            : Guid.NewGuid();

        return new TenantContext(tenantId, buId, userId, role, correlationId);
    }
}
