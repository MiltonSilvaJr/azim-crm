using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpportunityPipeline.Application.Common;
using OpportunityPipeline.Application.Opportunities.Commands;
using OpportunityPipeline.Application.Opportunities.Queries;
using OpportunityPipeline.Application.SavedFilters;
using OpportunityPipeline.Contracts.Requests;
using OpportunityPipeline.Contracts.Responses;

namespace OpportunityPipeline.Api.Controllers;

/// <summary>
/// Controller principal de oportunidades (design §8).
/// Rotas: POST /opportunities, GET /opportunities, GET /opportunities/{id},
/// PATCH /opportunities/{id}, PATCH /opportunities/{id}/stage,
/// POST /opportunities/{id}/win, POST /opportunities/{id}/lose,
/// POST /opportunities/{id}/reopen,
/// POST /opportunities/{id}/contacts, DELETE /opportunities/{id}/contacts/{contactId},
/// GET /opportunities/{id}/timeline,
/// GET /opportunities/filters, POST /opportunities/filters.
/// Mapeia: design §8, §10, TASK-20.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class OpportunitiesController(
    IMediator mediator,
    TenantContext tenantContext,
    IOpportunityQueryRepository queryRepository,
    ISavedFilterRepository savedFilterRepository)
    : ControllerBase
{
    // =========================================================================
    // POST /api/v1/opportunities — Criar oportunidade (Vendedor, GestorBU, TenantAdmin)
    // =========================================================================

    /// <summary>
    /// Cria nova oportunidade comercial. Roles: Vendedor, GestorBU, TenantAdmin.
    /// Erros: OP-ERR-001..004, OP-ERR-010, OP-ERR-011. Idempotência via Idempotency-Key.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "WriteOpportunity")]
    [ProducesResponseType(typeof(CreateOpportunityResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Contracts.ErrorCodes.OpProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateOpportunityRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.TraceIdentifier;

        var command = new CreateOpportunityCommand
        {
            AccountId = request.AccountId,
            BuId = request.BuId,
            StageId = request.StageId,
            OriginChannelId = request.OriginChannelId,
            OwnerId = request.OwnerId,
            Title = request.Title,
            ValorSetupCents = request.ValorSetup,
            ValorMensalCents = request.ValorMensal,
            DuracaoMeses = request.DuracaoMeses,
            Probabilidade = request.Probabilidade,
            ExpectedCloseDate = request.ExpectedCloseDate,
            Notes = request.Notes,
            PartnerId = request.PartnerId,
            CorrelationId = correlationId,
            IdempotencyKey = idempotencyKey
        };

        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        var response = new CreateOpportunityResponse
        {
            Id = result.Id,
            OpportunityNumber = result.OpportunityNumber,
            TenantId = result.TenantId
        };

        return CreatedAtAction(
            nameof(GetByIdAsync),
            new { id = result.Id },
            response);
    }

    // =========================================================================
    // GET /api/v1/opportunities — Listar oportunidades (Viewer+)
    // =========================================================================

    /// <summary>
    /// Lista oportunidades com filtros e paginação. Roles: todos (Viewer+).
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "ReadOpportunity")]
    [ProducesResponseType(typeof(OpportunityListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync(
        [FromQuery] Guid? owner_id,
        [FromQuery] Guid? origin_channel_id,
        [FromQuery] Guid? partner_id,
        [FromQuery] Guid? stage_id,
        [FromQuery] string? stage_category,
        [FromQuery] DateOnly? created_from,
        [FromQuery] DateOnly? created_to,
        [FromQuery] bool? is_stale,
        [FromQuery] string? search_text,
        [FromQuery] int page = 1,
        [FromQuery] int page_size = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.TenantId;
        var buId = tenantContext.BuId;

        Domain.Opportunities.ValueObjects.StageCategory? categoryEnum = null;
        if (!string.IsNullOrWhiteSpace(stage_category) &&
            Enum.TryParse<Domain.Opportunities.ValueObjects.StageCategory>(stage_category, ignoreCase: true, out var parsedCategory))
        {
            categoryEnum = parsedCategory;
        }

        var filter = new OpportunityFilter(
            OwnerId: owner_id,
            OriginChannelId: origin_channel_id,
            PartnerId: partner_id,
            StageId: stage_id,
            StageCategory: categoryEnum,
            CreatedFrom: created_from,
            CreatedTo: created_to,
            IsStale: is_stale,
            SearchText: search_text);

        var pageRequest = new PageRequest(Math.Max(1, page), Math.Clamp(page_size, 1, 200));
        var result = await queryRepository.ListAsync(tenantId, buId, filter, pageRequest, cancellationToken).ConfigureAwait(false);

        var response = new OpportunityListResponse
        {
            Items = result.Items.Select(MapToSummaryResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };

        return Ok(response);
    }

    // =========================================================================
    // GET /api/v1/opportunities/{id} — Detalhe da oportunidade (Viewer+)
    // =========================================================================

    /// <summary>
    /// Retorna detalhe completo da oportunidade. Roles: todos (Viewer+).
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "ReadOpportunity")]
    [ProducesResponseType(typeof(OpportunityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var opportunity = await queryRepository.GetByIdWithDetailsAsync(id, tenantId, cancellationToken).ConfigureAwait(false);

        if (opportunity is null)
            return NotFound();

        var response = MapToResponse(opportunity);
        return Ok(response);
    }

    // =========================================================================
    // PATCH /api/v1/opportunities/{id} — Editar oportunidade (Vendedor+)
    // =========================================================================

    /// <summary>
    /// Edita campos permitidos da oportunidade. Roles: Vendedor, GestorBU, TenantAdmin.
    /// Erros: OP-ERR-002, OP-ERR-007, OP-ERR-012.
    /// </summary>
    [HttpPatch("{id:guid}")]
    [Authorize(Policy = "WriteOpportunity")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Contracts.ErrorCodes.OpProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateAsync(
        Guid id,
        [FromBody] UpdateOpportunityRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var command = new UpdateOpportunityCommand
        {
            OpportunityId = id,
            Title = request.Title,
            Notes = request.Notes,
            ValorSetupCents = request.ValorSetup,
            ValorMensalCents = request.ValorMensal,
            DuracaoMeses = request.DuracaoMeses,
            Probabilidade = request.Probabilidade,
            ExpectedCloseDate = request.ExpectedCloseDate,
            OwnerId = request.OwnerId,
            CorrelationId = HttpContext.TraceIdentifier
        };

        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(new { id = result.Id, updated_at = result.UpdatedAt });
    }

    // =========================================================================
    // PATCH /api/v1/opportunities/{id}/stage — Mover estágio (Vendedor+)
    // =========================================================================

    /// <summary>
    /// Move oportunidade para novo estágio (drag-and-drop). Roles: Vendedor, GestorBU, TenantAdmin.
    /// Erros: OP-ERR-005, OP-ERR-013.
    /// </summary>
    [HttpPatch("{id:guid}/stage")]
    [Authorize(Policy = "WriteOpportunity")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Contracts.ErrorCodes.OpProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MoveStageAsync(
        Guid id,
        [FromBody] MoveStageRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var command = new MoveStageCommand
        {
            OpportunityId = id,
            TargetStageId = request.StageId,
            CorrelationId = HttpContext.TraceIdentifier
        };

        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(new { opportunity_id = result.OpportunityId, to_stage_category = result.ToStageCategory });
    }

    // =========================================================================
    // POST /api/v1/opportunities/{id}/win — Encerrar como ganha (Vendedor+)
    // =========================================================================

    /// <summary>
    /// Encerra oportunidade como ganha e cria snapshot de comissão. Roles: Vendedor, GestorBU, TenantAdmin.
    /// Erros: OP-ERR-013, OP-ERR-017.
    /// </summary>
    [HttpPost("{id:guid}/win")]
    [Authorize(Policy = "WriteOpportunity")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Contracts.ErrorCodes.OpProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> WinAsync(
        Guid id,
        [FromBody] WinOpportunityRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var command = new WinOpportunityCommand
        {
            OpportunityId = id,
            CorrelationId = HttpContext.TraceIdentifier,
            IdempotencyKey = idempotencyKey
        };

        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(new
        {
            opportunity_id = result.OpportunityId,
            closed_at = result.ClosedAt,
            commission_snapshot_created = result.CommissionSnapshotCreated,
            commission_alert = result.CommissionAlert
        });
    }

    // =========================================================================
    // POST /api/v1/opportunities/{id}/lose — Encerrar como perdida (Vendedor+)
    // =========================================================================

    /// <summary>
    /// Encerra oportunidade como perdida. Roles: Vendedor, GestorBU, TenantAdmin.
    /// Erros: OP-ERR-006, OP-ERR-013.
    /// </summary>
    [HttpPost("{id:guid}/lose")]
    [Authorize(Policy = "WriteOpportunity")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Contracts.ErrorCodes.OpProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> LoseAsync(
        Guid id,
        [FromBody] LoseOpportunityRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var command = new LoseOpportunityCommand
        {
            OpportunityId = id,
            LossReasonId = request.LossReasonId,
            CorrelationId = HttpContext.TraceIdentifier
        };

        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(new { opportunity_id = result.OpportunityId, closed_at = result.ClosedAt });
    }

    // =========================================================================
    // POST /api/v1/opportunities/{id}/reopen — Reabrir (GestorBU, TenantAdmin)
    // =========================================================================

    /// <summary>
    /// Reabre oportunidade encerrada. Roles: GestorBU, TenantAdmin apenas.
    /// Erros: OP-ERR-008 (403), OP-ERR-013 (422).
    /// </summary>
    [HttpPost("{id:guid}/reopen")]
    [Authorize(Policy = "ReopenOpportunity")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Contracts.ErrorCodes.OpProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Contracts.ErrorCodes.OpProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReopenAsync(
        Guid id,
        [FromBody] ReopenOpportunityRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var command = new ReopenOpportunityCommand
        {
            OpportunityId = id,
            Reason = request.Reason,
            CorrelationId = HttpContext.TraceIdentifier
        };

        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(new { opportunity_id = result.OpportunityId, snapshot_preserved = result.SnapshotPreserved, previous_category = result.PreviousCategory });
    }

    // =========================================================================
    // GET /api/v1/opportunities/{id}/timeline — Linha do tempo (Viewer+)
    // =========================================================================

    /// <summary>
    /// Retorna a linha do tempo de transições de estágio. Roles: todos (Viewer+).
    /// </summary>
    [HttpGet("{id:guid}/timeline")]
    [Authorize(Policy = "ReadOpportunity")]
    [ProducesResponseType(typeof(TimelineResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTimelineAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var entries = await queryRepository.GetTimelineAsync(tenantId, id, cancellationToken).ConfigureAwait(false);

        var response = new TimelineResponse
        {
            OpportunityId = id,
            Entries = entries.Select(e => new TimelineEntryResponse
            {
                TransitionId = e.TransitionId,
                FromStageName = e.FromStageName,
                ToStageName = e.ToStageName,
                FromCategory = e.FromCategory,
                ToCategory = e.ToCategory,
                OccurredAt = e.OccurredAt,
                ActorId = e.ActorId
            }).ToList()
        };

        return Ok(response);
    }

    // =========================================================================
    // GET /api/v1/opportunities/{id}/commissions — Comissão (Viewer+)
    // =========================================================================

    /// <summary>
    /// Retorna detalhes de comissão projetada e snapshot. Roles: todos (Viewer+).
    /// </summary>
    [HttpGet("{id:guid}/commissions")]
    [Authorize(Policy = "ReadOpportunity")]
    [ProducesResponseType(typeof(CommissionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCommissionsAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var commissions = await queryRepository.GetCommissionsAsync(tenantId, id, cancellationToken).ConfigureAwait(false);
        var forecast = await queryRepository.GetForecastAsync(tenantId, tenantContext.BuId, cancellationToken).ConfigureAwait(false);

        var response = new CommissionResponse
        {
            OpportunityId = id,
            Commissions = commissions.Select(c => new CommissionDetailResponse
            {
                PartnerId = c.PartnerId,
                Role = c.Role,
                PctSetup = c.PctSetup,
                PctRecorrente = c.PctRecorrente,
                ValorFixo = c.ValorFixoCents,
                MesesComissionados = c.MesesComissionados,
                ComissaoTotal = c.ComissaoTotalCents,
                IsSnapshot = c.IsSnapshot,
                SnapshotAt = c.SnapshotAt
            }).ToList(),
            ForecastLiquido = forecast.ForecastLiquidoCents
        };

        return Ok(response);
    }

    // =========================================================================
    // PUT /api/v1/opportunities/{id}/partner-commission — Definir comissão (Vendedor+)
    // =========================================================================

    /// <summary>
    /// Define ou atualiza comissão de parceiro. Roles: Vendedor, GestorBU, TenantAdmin.
    /// Rejeita se snapshot já existe (409). Erros: OP-ERR-004, OP-ERR-014, OP-ERR-015.
    /// </summary>
    [HttpPut("{id:guid}/partner-commission")]
    [Authorize(Policy = "WriteOpportunity")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Contracts.ErrorCodes.OpProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(Contracts.ErrorCodes.OpProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SetPartnerCommissionAsync(
        Guid id,
        [FromBody] SetPartnerCommissionRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<Domain.Opportunities.ValueObjects.CommissionRole>(request.Role, ignoreCase: true, out var commissionRole))
            return UnprocessableEntity(new { error_code = "OP-ERR-014", detail = "Role de comissão inválido." });

        var command = new SetPartnerCommissionCommand
        {
            OpportunityId = id,
            PartnerId = request.PartnerId,
            Role = commissionRole,
            PctSetup = request.PctSetup,
            PctRecorrente = request.PctRecorrente,
            MesesComissionados = request.MesesComissionados,
            ValorFixoCents = request.ValorFixo,
            UsePartnerDefaults = request.UsePartnerDefaults,
            CorrelationId = HttpContext.TraceIdentifier
        };

        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(new
        {
            opportunity_id = result.OpportunityId,
            comissao_total_cents = result.ComissaoTotalCents,
            is_snapshot = result.IsSnapshot
        });
    }

    // =========================================================================
    // POST /api/v1/opportunities/{id}/contacts — Vincular contato (Vendedor+)
    // =========================================================================

    /// <summary>
    /// Vincula contato à oportunidade. Roles: Vendedor, GestorBU, TenantAdmin.
    /// Erros: OP-ERR-009, OP-ERR-016.
    /// </summary>
    [HttpPost("{id:guid}/contacts")]
    [Authorize(Policy = "WriteOpportunity")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Contracts.ErrorCodes.OpProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> LinkContactAsync(
        Guid id,
        [FromBody] LinkContactRequest request,
        CancellationToken cancellationToken)
    {
        var command = new LinkContactCommand
        {
            OpportunityId = id,
            ContactId = request.ContactId,
            IsPrimary = request.IsPrimary,
            CorrelationId = HttpContext.TraceIdentifier
        };

        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(new { opportunity_id = result.OpportunityId, total_contacts = result.TotalContacts });
    }

    // =========================================================================
    // DELETE /api/v1/opportunities/{id}/contacts/{contactId} — Remover contato (Vendedor+)
    // =========================================================================

    /// <summary>
    /// Remove vínculo de contato com a oportunidade. Roles: Vendedor, GestorBU, TenantAdmin.
    /// </summary>
    [HttpDelete("{id:guid}/contacts/{contactId:guid}")]
    [Authorize(Policy = "WriteOpportunity")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UnlinkContactAsync(
        Guid id,
        Guid contactId,
        CancellationToken cancellationToken)
    {
        var command = new UnlinkContactCommand
        {
            OpportunityId = id,
            ContactId = contactId,
            CorrelationId = HttpContext.TraceIdentifier
        };

        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(new { opportunity_id = result.OpportunityId, remaining_contacts = result.RemainingContacts });
    }

    // =========================================================================
    // GET /api/v1/opportunities/filters — Listar filtros salvos (Viewer+)
    // =========================================================================

    /// <summary>
    /// Lista filtros salvos do usuário. Roles: todos (Viewer+).
    /// </summary>
    [HttpGet("filters")]
    [Authorize(Policy = "ReadOpportunity")]
    [ProducesResponseType(typeof(IReadOnlyList<SavedFilterResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListFiltersAsync(CancellationToken cancellationToken)
    {
        var filters = await savedFilterRepository.ListByUserAsync(
            tenantContext.TenantId,
            tenantContext.ActorId,
            cancellationToken).ConfigureAwait(false);

        var response = filters.Select(f => new SavedFilterResponse
        {
            Id = f.Id,
            Name = f.Name,
            Criteria = JsonSerializer.Deserialize<object>(f.CriteriaJson) ?? new { },
            CreatedAt = f.CreatedAt
        }).ToList();

        return Ok(response);
    }

    // =========================================================================
    // POST /api/v1/opportunities/filters — Salvar filtro (Vendedor+)
    // =========================================================================

    /// <summary>
    /// Salva filtro de lista personalizado. Roles: Vendedor, GestorBU, TenantAdmin.
    /// Nome único por usuário/tenant.
    /// </summary>
    [HttpPost("filters")]
    [Authorize(Policy = "WriteOpportunity")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Contracts.ErrorCodes.OpProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SaveFilterAsync(
        [FromBody] SaveFilterRequest request,
        CancellationToken cancellationToken)
    {
        var criteriaJson = JsonSerializer.Serialize(request.Criteria);

        var command = new SaveFilterCommand
        {
            Name = request.Name,
            CriteriaJson = criteriaJson,
            CorrelationId = HttpContext.TraceIdentifier
        };

        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(ListFiltersAsync), null, new { id = result.FilterId, name = result.Name });
    }

    // =========================================================================
    // GET /api/v1/opportunities/kanban — Kanban (Viewer+)
    // =========================================================================

    /// <summary>
    /// Retorna visão kanban por BU. Roles: todos (Viewer+).
    /// Parâmetros: bu_id (query), page_size (por coluna, padrão 20).
    /// </summary>
    [HttpGet("kanban")]
    [Authorize(Policy = "ReadOpportunity")]
    [ProducesResponseType(typeof(KanbanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetKanbanAsync(
        [FromQuery] Guid? bu_id,
        [FromQuery] int page_size = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.TenantId;
        var buId = bu_id ?? tenantContext.BuId;
        var stagePageSize = Math.Clamp(page_size, 1, 100);

        var columns = await queryRepository.GetKanbanAsync(tenantId, buId, stagePageSize, cancellationToken).ConfigureAwait(false);

        var response = new KanbanResponse
        {
            BuId = buId,
            Columns = columns.Select(c => new KanbanColumnResponse
            {
                StageId = c.StageId,
                StageName = c.StageName,
                Order = c.Order,
                TotalValor = c.TotalValueCents,
                TotalForecast = c.ForecastPonderadoCents,
                TotalCount = c.TotalCount,
                Cards = c.Cards.Select(MapToSummaryResponse).ToList(),
                HasMore = c.Cards.Count < c.TotalCount
            }).ToList()
        };

        return Ok(response);
    }

    // =========================================================================
    // Mappers privados
    // =========================================================================

    private static OpportunitySummaryResponse MapToSummaryResponse(OpportunitySummary s) =>
        new()
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
        };

    private static OpportunityResponse MapToResponse(Domain.Opportunities.Opportunity opportunity)
    {
        var totalInCents = opportunity.ContractValue.TotalInCents;
        var forecastPonderadoCents = totalInCents * opportunity.Probability.Value / 100L;

        return new()
        {
            Id = opportunity.Id,
            OpportunityNumber = opportunity.Number.Value,
            Title = opportunity.Title,
            TenantId = opportunity.TenantId,
            BuId = opportunity.BuId,
            AccountId = opportunity.AccountId,
            OwnerId = opportunity.OwnerId,
            PartnerId = opportunity.PartnerId,
            StageId = opportunity.Stage.StageId,
            StageName = opportunity.Stage.Name,
            StageCategory = opportunity.Stage.Category.ToString(),
            OriginChannelId = opportunity.OriginChannel.OriginChannelId,
            OriginChannelName = opportunity.OriginChannel.Name,
            ValorSetup = opportunity.ContractValue.Setup.AmountInCents,
            ValorMensal = opportunity.ContractValue.Mensal.AmountInCents,
            DuracaoMeses = opportunity.ContractValue.DuracaoMeses,
            ValorTotal = totalInCents,
            Probabilidade = opportunity.Probability.Value,
            ForecastPonderado = forecastPonderadoCents,
            ForecastLiquido = null, // calculado por query separada (comissões)
            ExpectedCloseDate = opportunity.ExpectedCloseDate,
            LossReasonId = opportunity.LossReason?.LossReasonId,
            ClosedAt = opportunity.ClosedAt,
            Notes = opportunity.Notes,
            IsStale = opportunity.IsStale,
            IsOverdue = opportunity.ExpectedCloseDate.HasValue
                && opportunity.ExpectedCloseDate.Value < DateOnly.FromDateTime(DateTime.UtcNow)
                && opportunity.StageCategory == Domain.Opportunities.ValueObjects.StageCategory.Open,
            CreatedAt = opportunity.CreatedAt,
            UpdatedAt = opportunity.UpdatedAt
        };
    }
}
