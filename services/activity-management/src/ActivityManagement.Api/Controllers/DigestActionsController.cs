namespace ActivityManagement.Api.Controllers;

using ActivityManagement.Application.Activities.Commands;
using ActivityManagement.Contracts.Activities;
using MediatR;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Controller para ações de 1 clique via links do digest (Req 7, design §8, design §10).
/// Sem autenticação JWT — a autoridade é o token opaco no path (não requer <c>[Authorize]</c>).
/// Anti-enumeração PBT-03: respostas de token inexistente/malformado são indistinguíveis
/// em forma de atividade inacessível (ACT-ERR-008).
/// Mapeia: design §8, design §10, Req 7, ACT-ERR-008, ACT-ERR-009, TASK-19.
/// </summary>
[ApiController]
[Route("api/v1/digest-actions")]
[Produces("application/json")]
public sealed class DigestActionsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>Inicializa o controller com o mediador da Application.</summary>
    public DigestActionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Processa ação de 1 clique via token opaco do link do digest.
    /// Os 4 ramos de resposta cobrem: válido, já processado, expirado e inválido.
    /// </summary>
    /// <param name="token">Token opaco presente no link do digest enviado por e-mail.</param>
    /// <param name="newDueAt">Nova data de vencimento (obrigatório para ação <c>reschedule</c>).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Resultado da ação ou erro padronizado do catálogo ACT-ERR.</returns>
    /// <response code="200">Ação executada ou token já processado (idempotente).</response>
    /// <response code="404">Token inválido, malformado ou atividade inacessível (ACT-ERR-008).</response>
    /// <response code="410">Token expirado — acesse o portal para visualizar a atividade (ACT-ERR-009).</response>
    [HttpPost("{token}")]
    [ProducesResponseType(typeof(DigestActionResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 410)]
    public async Task<IActionResult> ProcessDigestAction(
        string token,
        [FromQuery] DateTimeOffset? newDueAt   = null,
        CancellationToken cancellationToken = default)
    {
        var command = new ProcessDigestActionCommand(token, newDueAt);
        var result  = await _mediator.Send(command, cancellationToken);

        return Ok(new DigestActionResponse(
            ActivityId:          result.ActivityId,
            Action:              result.Action,
            WasAlreadyProcessed: result.WasAlreadyProcessed));
    }
}
