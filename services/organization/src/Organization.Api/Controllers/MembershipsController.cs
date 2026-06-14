using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Organization.Application.Commands.Membership;
using Organization.Application.Queries;

namespace Organization.Api.Controllers;

/// <summary>
/// Controller REST para memberships (vínculo usuário-BU-papel).
/// Endpoints: GET/POST /api/v1/users/{id}/memberships,
///            PUT /api/v1/users/{id}/memberships/{buId},
///            DELETE /api/v1/users/{id}/memberships/{buId}.
/// Todos os endpoints requerem papel TAdmin (RBAC delegado ao MediatR pipeline).
/// </summary>
[ApiController]
[Route("api/v1/users/{userId:guid}/memberships")]
[Authorize]
[Produces("application/json")]
public sealed class MembershipsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>Inicializa o controller.</summary>
    public MembershipsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lista os memberships (papéis por BU) de um usuário.
    /// </summary>
    /// <param name="userId">Identificador do usuário.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Lista de memberships.</response>
    /// <response code="401">Não autenticado.</response>
    /// <response code="403">Sem permissão. ORG-ERR-010</response>
    /// <response code="404">Usuário não encontrado.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MembershipResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListAsync(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ListMembershipsQuery(userId),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Atribui um novo membership (BU-papel) a um usuário.
    /// </summary>
    /// <param name="userId">Identificador do usuário.</param>
    /// <param name="request">BU e papel a atribuir.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="201">Membership criado.</response>
    /// <response code="400">Papel inválido. ORG-ERR-007</response>
    /// <response code="401">Não autenticado.</response>
    /// <response code="403">Sem permissão. ORG-ERR-010</response>
    /// <response code="409">Membership duplicado. ORG-ERR-012</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignAsync(
        [FromRoute] Guid userId,
        [FromBody] AssignMembershipRequest request,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(
            new AssignMembershipCommand(userId, request.BuId, request.Role),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created);
    }

    /// <summary>
    /// Altera o papel de um membership existente.
    /// </summary>
    /// <param name="userId">Identificador do usuário.</param>
    /// <param name="buId">Identificador da BU.</param>
    /// <param name="request">Novo papel.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Papel alterado.</response>
    /// <response code="400">Papel inválido. ORG-ERR-007</response>
    /// <response code="401">Não autenticado.</response>
    /// <response code="403">Sem permissão. ORG-ERR-010</response>
    /// <response code="409">Último TAdmin. ORG-ERR-009</response>
    [HttpPut("{buId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeRoleAsync(
        [FromRoute] Guid userId,
        [FromRoute] Guid buId,
        [FromBody] ChangeMembershipRoleRequest request,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(
            new ChangeMembershipRoleCommand(userId, buId, request.Role),
            cancellationToken);

        return Ok();
    }

    /// <summary>
    /// Remove um membership de um usuário.
    /// </summary>
    /// <param name="userId">Identificador do usuário.</param>
    /// <param name="buId">Identificador da BU.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="204">Membership removido.</response>
    /// <response code="401">Não autenticado.</response>
    /// <response code="403">Sem permissão. ORG-ERR-010</response>
    /// <response code="409">Último TAdmin. ORG-ERR-009</response>
    [HttpDelete("{buId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveAsync(
        [FromRoute] Guid userId,
        [FromRoute] Guid buId,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(
            new RemoveMembershipCommand(userId, buId),
            cancellationToken);

        return NoContent();
    }
}

/// <summary>Request de atribuição de membership.</summary>
/// <param name="BuId">Identificador da BU.</param>
/// <param name="Role">Papel (TAdmin, GestorBU, Vendedor, Viewer).</param>
public sealed record AssignMembershipRequest(Guid BuId, string Role);

/// <summary>Request de alteração de papel no membership.</summary>
/// <param name="Role">Novo papel.</param>
public sealed record ChangeMembershipRoleRequest(string Role);
