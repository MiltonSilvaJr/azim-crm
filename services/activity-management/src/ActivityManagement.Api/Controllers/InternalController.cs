namespace ActivityManagement.Api.Controllers;

using ActivityManagement.Application.Activities.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Controller para operações internas acionadas pelo Cloud Scheduler (design §8, design §10).
/// Autenticação via header <c>X-CloudScheduler-JobName</c> validado pelo middleware de scheduler.
/// Não usa JWT Bearer — a autoridade é o header de serviço interno (mTLS/OIDC é responsabilidade
/// do ingress; aqui validamos o header de job name para proteção em camada de aplicação).
/// Mapeia: design §8, design §10, Req 11, TASK-19.
/// </summary>
[ApiController]
[Route("internal")]
[Produces("application/json")]
public sealed class InternalController : ControllerBase
{
    /// <summary>Nome esperado do job do Cloud Scheduler (design §10).</summary>
    private const string ExpectedJobName = "activity-overdue-scan";

    private readonly IMediator _mediator;

    /// <summary>Inicializa o controller com o mediador da Application.</summary>
    public InternalController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Dispara a varredura de atividades vencidas (Req 11).
    /// Aceita apenas requisições com o header <c>X-CloudScheduler-JobName: activity-overdue-scan</c>.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="202">Varredura iniciada com sucesso.</response>
    /// <response code="401">Header de scheduler ausente ou inválido.</response>
    [HttpPost("overdue-scan")]
    [ProducesResponseType(202)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> OverdueScan(CancellationToken cancellationToken = default)
    {
        // Verificação do header de scheduler — proteção em camada de aplicação
        // mTLS/OIDC é responsabilidade do ingress (design §10)
        if (!Request.Headers.TryGetValue("X-CloudScheduler-JobName", out var jobName)
            || jobName.ToString() != ExpectedJobName)
        {
            return Unauthorized();
        }

        await _mediator.Send(new ScanOverdueActivitiesCommand(), cancellationToken);
        return Accepted();
    }
}
