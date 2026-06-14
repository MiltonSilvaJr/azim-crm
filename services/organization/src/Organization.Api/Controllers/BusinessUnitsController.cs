using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Organization.Application.Commands.BusinessUnit;
using Organization.Application.Queries;

namespace Organization.Api.Controllers;

/// <summary>
/// Controller REST para Business Units.
/// Endpoints: POST /api/v1/business-units, GET /api/v1/business-units,
///            PUT /api/v1/business-units/{id}, DELETE /api/v1/business-units/{id}.
/// RBAC delegado ao <c>RbacAuthorizationBehavior</c> via MediatR pipeline.
/// Erros mapeados pelo <c>OrganizationExceptionMiddleware</c> para Problem Details.
/// </summary>
[ApiController]
[Route("api/v1/business-units")]
[Authorize]
[Produces("application/json")]
public sealed class BusinessUnitsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>Inicializa o controller com o mediator.</summary>
    public BusinessUnitsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Cria uma nova Business Unit com seeds de pipeline.
    /// </summary>
    /// <param name="request">Nome da BU.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Identificador da BU criada.</returns>
    /// <response code="201">BU criada com sucesso.</response>
    /// <response code="400">Dados inválidos.</response>
    /// <response code="401">Não autenticado.</response>
    /// <response code="403">Sem permissão (apenas TAdmin). ORG-ERR-010</response>
    /// <response code="409">Nome duplicado. ORG-ERR-001</response>
    [HttpPost]
    [ProducesResponseType(typeof(CreateBusinessUnitResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateBusinessUnitRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(
            new CreateBusinessUnitCommand(request.Name),
            cancellationToken);

        return Created(
            $"api/v1/business-units/{id}",
            new CreateBusinessUnitResponse(id, request.Name, true));
    }

    /// <summary>
    /// Lista as Business Units ativas do tenant (paginada).
    /// </summary>
    /// <param name="page">Número da página (1-based, padrão 1).</param>
    /// <param name="pageSize">Tamanho da página (padrão 20).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Lista de BUs ativas.</response>
    /// <response code="401">Não autenticado.</response>
    /// <response code="403">Sem permissão. ORG-ERR-010</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BusinessUnitResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetListAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new ListBusinessUnitsQuery(page, pageSize),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Renomeia uma Business Unit existente.
    /// </summary>
    /// <param name="id">Identificador da BU.</param>
    /// <param name="request">Novo nome.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">BU renomeada.</response>
    /// <response code="400">Dados inválidos.</response>
    /// <response code="401">Não autenticado.</response>
    /// <response code="403">Sem permissão. ORG-ERR-010 / ORG-ERR-008</response>
    /// <response code="409">Nome duplicado. ORG-ERR-001</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RenameAsync(
        [FromRoute] Guid id,
        [FromBody] RenameBusinessUnitRequest request,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(
            new RenameBusinessUnitCommand(id, request.Name),
            cancellationToken);

        return Ok();
    }

    /// <summary>
    /// Desativa (soft-delete) uma Business Unit.
    /// </summary>
    /// <param name="id">Identificador da BU.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="204">BU desativada.</response>
    /// <response code="401">Não autenticado.</response>
    /// <response code="403">Sem permissão. ORG-ERR-010</response>
    /// <response code="409">BU possui oportunidades ativas. ORG-ERR-002</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeactivateAsync(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(
            new DeactivateBusinessUnitCommand(id),
            cancellationToken);

        return NoContent();
    }
}

/// <summary>Request de criação de Business Unit.</summary>
/// <param name="Name">Nome da BU (1..120 caracteres).</param>
public sealed record CreateBusinessUnitRequest(string Name);

/// <summary>Request de renomeação de Business Unit.</summary>
/// <param name="Name">Novo nome da BU.</param>
public sealed record RenameBusinessUnitRequest(string Name);

/// <summary>Resposta de criação de Business Unit.</summary>
/// <param name="Id">Identificador gerado.</param>
/// <param name="Name">Nome da BU.</param>
/// <param name="Active">Estado inicial.</param>
public sealed record CreateBusinessUnitResponse(Guid Id, string Name, bool Active);
