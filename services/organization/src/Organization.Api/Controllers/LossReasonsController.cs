using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Organization.Application.Commands.PipelineConfig;
using Organization.Application.Queries;

namespace Organization.Api.Controllers;

/// <summary>
/// Controller REST para motivos de perda de uma Business Unit.
/// Endpoints: GET/POST /api/v1/business-units/{buId}/loss-reasons,
///            PUT (desativar) /api/v1/business-units/{buId}/loss-reasons/{id}.
/// RBAC: TAdmin para escrita; leitura pública.
/// Invariante: ao menos um motivo de perda ativo por BU (ORG-ERR-017).
/// </summary>
[ApiController]
[Route("api/v1/business-units/{buId:guid}/loss-reasons")]
[Produces("application/json")]
public sealed class LossReasonsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>Inicializa o controller.</summary>
    public LossReasonsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lista os motivos de perda ativos de uma BU.
    /// </summary>
    /// <param name="buId">Identificador da BU.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Lista de motivos ativos.</response>
    /// <response code="404">BU não encontrada. ORG-ERR-016</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<LossReasonResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListAsync(
        [FromRoute] Guid buId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ListLossReasonsQuery(buId),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Adiciona um motivo de perda à BU.
    /// </summary>
    /// <param name="buId">Identificador da BU.</param>
    /// <param name="request">Nome do motivo.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="201">Motivo criado.</response>
    /// <response code="400">Dados inválidos.</response>
    /// <response code="401">Não autenticado.</response>
    /// <response code="403">Sem permissão. ORG-ERR-010</response>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddAsync(
        [FromRoute] Guid buId,
        [FromBody] AddLossReasonRequest request,
        CancellationToken cancellationToken)
    {
        var reasonId = Guid.NewGuid();

        await _mediator.Send(
            new AddLossReasonCommand(buId, reasonId, request.Name),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created);
    }

    /// <summary>
    /// Desativa um motivo de perda.
    /// </summary>
    /// <param name="buId">Identificador da BU.</param>
    /// <param name="id">Identificador do motivo.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Motivo desativado.</response>
    /// <response code="401">Não autenticado.</response>
    /// <response code="403">Sem permissão. ORG-ERR-010</response>
    /// <response code="404">Motivo não encontrado. ORG-ERR-016</response>
    /// <response code="409">Último motivo ativo. ORG-ERR-017</response>
    [HttpPut("{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeactivateAsync(
        [FromRoute] Guid buId,
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(
            new DeactivateLossReasonCommand(buId, id),
            cancellationToken);

        return Ok();
    }
}

/// <summary>Request de adição de motivo de perda.</summary>
/// <param name="Name">Nome do motivo.</param>
public sealed record AddLossReasonRequest(string Name);
