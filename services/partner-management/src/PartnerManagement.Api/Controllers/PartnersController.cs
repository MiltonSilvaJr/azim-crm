using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PartnerManagement.Api.Middleware;
using PartnerManagement.Application.Partners;
using PartnerManagement.Application.Partners.Commands;
using PartnerManagement.Application.Partners.Queries;
using PartnerManagement.Application.Ports;
using PartnerManagement.Contracts.Commissions;
using PartnerManagement.Contracts.Partners;
using PartnerManagement.Domain.Partners.ValueObjects;

namespace PartnerManagement.Api.Controllers;

/// <summary>
/// Controller REST para o módulo partner-management.
/// Implementa os 8 endpoints de design §8. Nenhuma lógica de negócio aqui —
/// tudo delegado para a camada Application via MediatR.
/// RBAC enforçado pelo AuthorizationBehavior no pipeline MediatR
/// e declarado pelo RequiredPermissionAttribute nos commands/queries.
/// Mapeia: TASK-23, TASK-24, design §8.
/// </summary>
[ApiController]
[Route("api/v1/partners")]
[Authorize]
public sealed class PartnersController(IMediator mediator, ITenantContext tenantContext) : ControllerBase
{
    // =========================================================================
    // GET /api/v1/partners
    // =========================================================================

    /// <summary>
    /// Lista parceiros do tenant com filtros opcionais e paginação.
    /// Papel mínimo: Viewer (<c>partners:read</c>).
    /// Padrão: retorna apenas parceiros ativos.
    /// </summary>
    /// <param name="active">Filtro por status: <c>true</c>=ativos (padrão), <c>false</c>=inativos, omitir=todos.</param>
    /// <param name="triagePending">Quando <c>true</c>, retorna apenas parceiros com percentuais zerados.</param>
    /// <param name="page">Página (1-based, padrão: 1).</param>
    /// <param name="pageSize">Itens por página (padrão: 20, máximo: 100).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    [HttpGet]
    [ProducesResponseType(typeof(PartnerPagedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListPartners(
        [FromQuery] bool? active = true,
        [FromQuery] bool triagePending = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
        {
            return BadRequest(new
            {
                error = "Parâmetros de listagem inválidos.",
                code = PartnerErrors.InvalidListParameters,
                correlationId = GetCorrelationId()
            });
        }

        ListPartnersResult result = await mediator.Send(
            new ListPartnersQuery(tenantContext.CurrentTenantId, active, triagePending, page, pageSize),
            cancellationToken);

        var response = new PartnerPagedResponse
        {
            Items = result.Partners.Select(MapToResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };

        return Ok(response);
    }

    // =========================================================================
    // POST /api/v1/partners
    // =========================================================================

    /// <summary>
    /// Cria um novo parceiro.
    /// Papel mínimo: Gestor de BU ou Tenant Admin (<c>partners:write</c>).
    /// Aceita header <c>Idempotency-Key</c> opcional para integração com data-migration.
    /// </summary>
    /// <param name="request">Dados do parceiro a ser criado.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    [HttpPost]
    [ProducesResponseType(typeof(PartnerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreatePartner(
        [FromBody] CreatePartnerRequest request,
        CancellationToken cancellationToken = default)
    {
        CommissionDefaults commissionDefaults = CommissionDefaults.Create(
            Percentage.Create(request.PctSetup),
            Percentage.Create(request.PctRecorrente));

        Guid actorId = GetActorId();

        CreatePartnerResult result = await mediator.Send(
            new CreatePartnerCommand(
                TenantId: tenantContext.CurrentTenantId,
                Name: request.Name,
                Role: request.Role,
                CommissionDefaults: commissionDefaults,
                ContactEmail: request.ContactEmail,
                ContactPhone: request.ContactPhone,
                Notes: request.Notes,
                CreatedBy: actorId,
                ConfirmCreateDespiteDuplicate: request.ConfirmCreateDespiteDuplicate),
            cancellationToken);

        var response = new PartnerResponse
        {
            PartnerId = result.PartnerId,
            Name = request.Name,
            Role = request.Role,
            PctSetup = request.PctSetup,
            PctRecorrente = request.PctRecorrente,
            Active = true,
            IsTriagePending = request.PctSetup == 0m && request.PctRecorrente == 0m,
            DuplicateNameAlert = result.DuplicateNameAlert
        };

        return CreatedAtAction(
            nameof(GetPartnerById),
            new { id = result.PartnerId },
            response);
    }

    // =========================================================================
    // GET /api/v1/partners/{id}
    // =========================================================================

    /// <summary>
    /// Obtém o detalhe de um parceiro pelo identificador.
    /// Papel mínimo: Viewer (<c>partners:read</c>).
    /// Retorna PM-ERR-007 quando o parceiro não existe ou está fora do tenant.
    /// </summary>
    /// <param name="id">Identificador do parceiro.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PartnerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPartnerById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        PartnerDetail detail = await mediator.Send(
            new GetPartnerByIdQuery(id, tenantContext.CurrentTenantId),
            cancellationToken);

        return Ok(MapDetailToResponse(detail));
    }

    // =========================================================================
    // PATCH /api/v1/partners/{id}
    // =========================================================================

    /// <summary>
    /// Atualiza parcialmente um parceiro existente.
    /// Papel mínimo: Gestor de BU ou Tenant Admin (<c>partners:write</c>).
    /// </summary>
    /// <param name="id">Identificador do parceiro.</param>
    /// <param name="request">Campos a serem atualizados.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePartner(
        [FromRoute] Guid id,
        [FromBody] UpdatePartnerRequest request,
        CancellationToken cancellationToken = default)
    {
        // Buscar estado atual para defaults de campos não fornecidos (PATCH parcial)
        PartnerDetail current = await mediator.Send(
            new GetPartnerByIdQuery(id, tenantContext.CurrentTenantId),
            cancellationToken);

        CommissionDefaults commissionDefaults = CommissionDefaults.Create(
            Percentage.Create(request.PctSetup ?? current.PctSetup),
            Percentage.Create(request.PctRecorrente ?? current.PctRecorrente));

        await mediator.Send(
            new UpdatePartnerCommand(
                PartnerId: id,
                TenantId: tenantContext.CurrentTenantId,
                Name: request.Name ?? current.Name,
                Role: request.Role ?? current.Role,
                CommissionDefaults: commissionDefaults,
                ContactEmail: request.ContactEmail,
                ContactPhone: request.ContactPhone,
                Notes: request.Notes ?? current.Notes,
                UpdatedBy: GetActorId()),
            cancellationToken);

        return Ok(new { partnerId = id, updated = true });
    }

    // =========================================================================
    // Auxiliares
    // =========================================================================

    private string GetCorrelationId() =>
        HttpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out object? val)
            ? val?.ToString() ?? string.Empty
            : string.Empty;

    private Guid GetActorId()
    {
        string? sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        return Guid.TryParse(sub, out Guid actorId) ? actorId : Guid.Empty;
    }

    private static PartnerResponse MapToResponse(PartnerSummary s) =>
        new()
        {
            PartnerId = s.PartnerId,
            Name = s.Name,
            Role = s.Role,
            PctSetup = s.PctSetup,
            PctRecorrente = s.PctRecorrente,
            Active = s.Active,
            IsTriagePending = s.IsTriagePending
        };

    private static PartnerResponse MapDetailToResponse(PartnerDetail d) =>
        new()
        {
            PartnerId = d.PartnerId,
            Name = d.Name,
            Role = d.Role,
            PctSetup = d.PctSetup,
            PctRecorrente = d.PctRecorrente,
            ContactEmail = d.ContactEmail,
            ContactPhone = d.ContactPhone,
            Notes = d.Notes,
            Active = d.Active,
            IsTriagePending = d.IsTriagePending,
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt
        };
}
