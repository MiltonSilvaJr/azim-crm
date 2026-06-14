using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Organization.Application.Commands.PipelineConfig;
using Organization.Application.Queries;

namespace Organization.Api.Controllers;

/// <summary>
/// Controller REST para estágios de pipeline de uma Business Unit.
/// Endpoints: GET/POST /api/v1/business-units/{buId}/stages,
///            PUT /api/v1/business-units/{buId}/stages/{id} (reordenar),
///            DELETE /api/v1/business-units/{buId}/stages/{id}.
/// RBAC: TAdmin e GestorBU (com escopo na BU) para escrita; leitura pública.
/// </summary>
[ApiController]
[Route("api/v1/business-units/{buId:guid}/stages")]
[Produces("application/json")]
public sealed class StagesController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>Inicializa o controller.</summary>
    public StagesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lista os estágios de uma BU, ordenados por posição.
    /// </summary>
    /// <param name="buId">Identificador da BU.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Lista de estágios ordenada por posição.</response>
    /// <response code="404">BU não encontrada. ORG-ERR-016</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<StageResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListAsync(
        [FromRoute] Guid buId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ListStagesQuery(buId),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Adiciona um estágio à BU.
    /// </summary>
    /// <param name="buId">Identificador da BU.</param>
    /// <param name="request">Dados do estágio.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="201">Estágio criado.</response>
    /// <response code="400">Dados inválidos.</response>
    /// <response code="401">Não autenticado.</response>
    /// <response code="403">Sem permissão. ORG-ERR-010</response>
    /// <response code="409">Nome duplicado. ORG-ERR-013</response>
    /// <response code="400">Posição duplicada. ORG-ERR-014</response>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddAsync(
        [FromRoute] Guid buId,
        [FromBody] AddStageRequest request,
        CancellationToken cancellationToken)
    {
        var stageId = Guid.NewGuid();

        await _mediator.Send(
            new AddStageCommand(buId, stageId, request.Name, request.ProbabilityPercent, request.Category, request.Position),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created);
    }

    /// <summary>
    /// Reordena estágios da BU.
    /// </summary>
    /// <param name="buId">Identificador da BU.</param>
    /// <param name="id">Identificador do estágio (ignorado — reordenação completa).</param>
    /// <param name="request">Mapa de stageId → nova posição.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Estágios reordenados.</response>
    /// <response code="400">Posições inválidas. ORG-ERR-014</response>
    /// <response code="401">Não autenticado.</response>
    /// <response code="403">Sem permissão. ORG-ERR-010</response>
    [HttpPut("{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ReorderAsync(
        [FromRoute] Guid buId,
        [FromRoute] Guid id,
        [FromBody] ReorderStagesRequest request,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(
            new ReorderStagesCommand(buId, request.NewPositions),
            cancellationToken);

        return Ok();
    }

    /// <summary>
    /// Remove um estágio da BU.
    /// </summary>
    /// <param name="buId">Identificador da BU.</param>
    /// <param name="id">Identificador do estágio.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="204">Estágio removido.</response>
    /// <response code="401">Não autenticado.</response>
    /// <response code="403">Sem permissão. ORG-ERR-010</response>
    /// <response code="409">Último estágio terminal. ORG-ERR-015</response>
    [HttpDelete("{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveAsync(
        [FromRoute] Guid buId,
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(
            new RemoveStageCommand(buId, id),
            cancellationToken);

        return NoContent();
    }
}

/// <summary>Request de adição de estágio.</summary>
public sealed record AddStageRequest(
    string Name,
    int ProbabilityPercent,
    string Category,
    int Position);

/// <summary>Request de reordenação de estágios.</summary>
/// <param name="NewPositions">Mapa stageId → nova posição.</param>
public sealed record ReorderStagesRequest(IReadOnlyDictionary<Guid, int> NewPositions);
