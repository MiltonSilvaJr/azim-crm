using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Organization.Application.Commands.Invitation;

namespace Organization.Api.Controllers;

/// <summary>
/// Controller REST para convites de usuário.
/// Endpoints:
///   POST /api/v1/users/invite           — TAdmin envia convite
///   POST /api/v1/invitations/{id}/revoke — TAdmin revoga convite
///   POST /api/v1/invitations/accept      — público (token), sem JWT
///
/// Anti-enumeração: erros de convite e aceite retornam mensagem genérica
/// sem revelar existência de conta (RNF 3.2, design §10).
/// </summary>
[ApiController]
[Produces("application/json")]
public sealed class InvitationsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>Inicializa o controller.</summary>
    public InvitationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Envia convite por e-mail para um usuário.
    /// Anti-enumeração: seja o e-mail ativo ou inexistente, a resposta é a mesma (ORG-ERR-003 genérico).
    /// </summary>
    /// <param name="request">E-mail e memberships a atribuir.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="202">Convite enviado (ou enfileirado).</response>
    /// <response code="400">Dados inválidos.</response>
    /// <response code="401">Não autenticado.</response>
    /// <response code="403">Sem permissão. ORG-ERR-010</response>
    /// <response code="409">Não foi possível concluir o convite. ORG-ERR-003 (genérico, anti-enumeração)</response>
    [HttpPost("api/v1/users/invite")]
    [Authorize]
    [ProducesResponseType(typeof(InviteUserResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> InviteAsync(
        [FromBody] InviteUserRequest request,
        CancellationToken cancellationToken)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(72);

        var memberships = request.Memberships
            .Select(m => (m.BuId, m.Role))
            .ToList()
            .AsReadOnly();

        var invitationId = await _mediator.Send(
            new InviteUserCommand(request.Email, memberships, expiresAt),
            cancellationToken);

        return Accepted(new InviteUserResponse(invitationId));
    }

    /// <summary>
    /// Revoga um convite pendente.
    /// </summary>
    /// <param name="id">Identificador do convite.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="204">Convite revogado.</response>
    /// <response code="401">Não autenticado.</response>
    /// <response code="403">Sem permissão. ORG-ERR-010</response>
    /// <response code="409">Convite não está pendente. ORG-ERR-006</response>
    [HttpPost("api/v1/invitations/{id:guid}/revoke")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RevokeAsync(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(
            new RevokeInvitationCommand(id),
            cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Aceita um convite via token.
    /// Endpoint público — NÃO requer JWT (autenticado pelo token de convite).
    /// Idempotente: reprocessar o mesmo token retorna o mesmo resultado (PBT-03).
    /// Anti-enumeração: token inválido e expirado retornam a mesma mensagem (ORG-ERR-004).
    /// </summary>
    /// <param name="request">Token e nome de exibição do usuário.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Usuário ativado. Retorna userId.</response>
    /// <response code="400">Token inválido, expirado ou revogado. ORG-ERR-004 (genérico, anti-enumeração)</response>
    /// <response code="409">Convite já utilizado. ORG-ERR-005</response>
    [HttpPost("api/v1/invitations/accept")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AcceptInvitationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AcceptAsync(
        [FromBody] AcceptInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var userId = await _mediator.Send(
            new AcceptInvitationCommand(request.Token, request.DisplayName),
            cancellationToken);

        return Ok(new AcceptInvitationResponse(userId));
    }
}

/// <summary>Request de convite de usuário.</summary>
public sealed record InviteUserRequest(
    string Email,
    IReadOnlyList<InviteMembershipItem> Memberships);

/// <summary>Item de membership no convite.</summary>
/// <param name="BuId">Identificador da BU.</param>
/// <param name="Role">Papel a atribuir.</param>
public sealed record InviteMembershipItem(Guid BuId, string Role);

/// <summary>Resposta de convite enviado.</summary>
/// <param name="InvitationId">Identificador do convite criado.</param>
public sealed record InviteUserResponse(Guid InvitationId);

/// <summary>Request de aceite de convite.</summary>
/// <param name="Token">Token de convite em claro.</param>
/// <param name="DisplayName">Nome de exibição do usuário.</param>
public sealed record AcceptInvitationRequest(string Token, string DisplayName);

/// <summary>Resposta de aceite de convite.</summary>
/// <param name="UserId">Identificador do usuário criado/ativado.</param>
public sealed record AcceptInvitationResponse(Guid UserId);
