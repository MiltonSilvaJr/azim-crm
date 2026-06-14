using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Organization.Application.Commands.User;
using Organization.Application.Queries;

namespace Organization.Api.Controllers;

/// <summary>
/// Controller REST para usuários do tenant.
/// Endpoints: GET /api/v1/users, DELETE /api/v1/users/{id}.
/// Todos os endpoints requerem papel TAdmin (RBAC delegado ao MediatR pipeline).
/// PII (email, display_name) restrita a TAdmin (RNF 3.3).
/// </summary>
[ApiController]
[Route("api/v1/users")]
[Authorize]
[Produces("application/json")]
public sealed class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>Inicializa o controller.</summary>
    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lista os usuários ativos do tenant (paginada).
    /// PII (email) exposta somente para TAdmin.
    /// </summary>
    /// <param name="page">Número da página (1-based, padrão 1).</param>
    /// <param name="pageSize">Tamanho da página (padrão 20).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Lista de usuários.</response>
    /// <response code="401">Não autenticado.</response>
    /// <response code="403">Sem permissão. ORG-ERR-010</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new ListUsersQuery(page, pageSize),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Desativa (soft-delete) um usuário do tenant.
    /// Anti-enumeração: respostas de erro não revelam existência de conta.
    /// </summary>
    /// <param name="id">Identificador do usuário.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="204">Usuário desativado.</response>
    /// <response code="401">Não autenticado.</response>
    /// <response code="403">Sem permissão. ORG-ERR-010</response>
    /// <response code="409">Último TAdmin ativo. ORG-ERR-009</response>
    /// <response code="409">Usuário com atividades futuras. ORG-ERR-011</response>
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
            new DeactivateUserCommand(id),
            cancellationToken);

        return NoContent();
    }
}
