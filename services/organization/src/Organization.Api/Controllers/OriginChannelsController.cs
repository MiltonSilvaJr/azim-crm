using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Organization.Application.Commands.PipelineConfig;
using Organization.Application.Queries;

namespace Organization.Api.Controllers;

/// <summary>
/// Controller REST para canais de origem de uma Business Unit.
/// Endpoints: GET/POST /api/v1/business-units/{buId}/origin-channels,
///            PUT (desativar) /api/v1/business-units/{buId}/origin-channels/{id}.
/// RBAC: TAdmin para escrita; leitura pública.
/// </summary>
[ApiController]
[Route("api/v1/business-units/{buId:guid}/origin-channels")]
[Produces("application/json")]
public sealed class OriginChannelsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>Inicializa o controller.</summary>
    public OriginChannelsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lista os canais de origem ativos de uma BU.
    /// </summary>
    /// <param name="buId">Identificador da BU.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Lista de canais ativos.</response>
    /// <response code="404">BU não encontrada. ORG-ERR-016</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<OriginChannelResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListAsync(
        [FromRoute] Guid buId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ListOriginChannelsQuery(buId),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Adiciona um canal de origem à BU.
    /// </summary>
    /// <param name="buId">Identificador da BU.</param>
    /// <param name="request">Nome do canal.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="201">Canal criado.</response>
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
        [FromBody] AddOriginChannelRequest request,
        CancellationToken cancellationToken)
    {
        var channelId = Guid.NewGuid();

        await _mediator.Send(
            new AddOriginChannelCommand(buId, channelId, request.Name),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created);
    }

    /// <summary>
    /// Desativa um canal de origem.
    /// </summary>
    /// <param name="buId">Identificador da BU.</param>
    /// <param name="id">Identificador do canal.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Canal desativado.</response>
    /// <response code="401">Não autenticado.</response>
    /// <response code="403">Sem permissão. ORG-ERR-010</response>
    /// <response code="404">Canal não encontrado. ORG-ERR-016</response>
    [HttpPut("{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateAsync(
        [FromRoute] Guid buId,
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(
            new DeactivateOriginChannelCommand(buId, id),
            cancellationToken);

        return Ok();
    }
}

/// <summary>Request de adição de canal de origem.</summary>
/// <param name="Name">Nome do canal.</param>
public sealed record AddOriginChannelRequest(string Name);
