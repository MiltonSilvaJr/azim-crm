using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantAdministration.Application.Commands;
using TenantAdministration.Contracts.Platform;

namespace TenantAdministration.Api.Controllers;

/// <summary>
/// Controller REST para endpoints do plano de plataforma (Platform Operator).
/// Rota base: <c>/api/v1/platform/tenants</c>.
/// Todos os endpoints exigem o papel <c>PlatformOperator</c> (design.md §8.1, RNF 7).
/// Não contém lógica de negócio — delega ao MediatR.
/// </summary>
[ApiController]
[Route("api/v1/platform/tenants")]
[Produces("application/json")]
public sealed class PlatformTenantController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Provisiona um novo tenant na plataforma Azim.
    /// Exige papel <c>PlatformOperator</c>.
    /// Aceita cabeçalho <c>Idempotency-Key</c> para evitar dupla execução (design.md §6.5).
    /// </summary>
    /// <param name="request">Dados do tenant a provisionar.</param>
    /// <param name="idempotencyKey">Chave de idempotência (header opcional).</param>
    /// <param name="ct">Token de cancelamento.</param>
    /// <returns>201 Created com tenantId, slug e status; ou erro 4xx/5xx.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ProvisionTenantResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Provision(
        [FromBody] ProvisionTenantRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        var command = new ProvisionTenantCommand(
            Slug: request.Slug,
            SlugConfirmation: request.SlugConfirmation,
            DisplayName: request.DisplayName,
            Timezone: request.Timezone,
            DigestTime: request.DigestTime,
            AdminEmail: request.AdminEmail,
            IdempotencyKey: idempotencyKey);

        var result = await mediator.Send(command, ct);

        // Nunca expõe adminEmail na resposta (design.md §8.1)
        return CreatedAtRoute(
            routeName: null,
            routeValues: null,
            value: new ProvisionTenantResponse(result.TenantId, result.Slug, result.Status));
    }

    /// <summary>
    /// Suspende um tenant ativo. Exige papel <c>PlatformOperator</c>.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant a suspender.</param>
    /// <param name="ct">Token de cancelamento.</param>
    /// <returns>200 OK com status updated; ou 404/409 em caso de erro.</returns>
    [HttpPost("{tenantId:guid}/suspend")]
    [ProducesResponseType(typeof(TenantStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Suspend(
        [FromRoute] Guid tenantId,
        CancellationToken ct)
    {
        var command = new SuspendTenantCommand(TenantId: tenantId);
        var result = await mediator.Send(command, ct);
        return Ok(new TenantStateResponse(result.TenantId, result.Status));
    }

    /// <summary>
    /// Reativa um tenant suspenso. Exige papel <c>PlatformOperator</c>.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant a reativar.</param>
    /// <param name="ct">Token de cancelamento.</param>
    /// <returns>200 OK com status atualizado; ou 404/409 em caso de erro.</returns>
    [HttpPost("{tenantId:guid}/reactivate")]
    [ProducesResponseType(typeof(TenantStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reactivate(
        [FromRoute] Guid tenantId,
        CancellationToken ct)
    {
        var command = new ReactivateTenantCommand(TenantId: tenantId);
        var result = await mediator.Send(command, ct);
        return Ok(new TenantStateResponse(result.TenantId, result.Status));
    }
}
