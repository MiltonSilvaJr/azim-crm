using AuditLog.Application.Queries;
using AuditLog.Contracts;
using AuditLog.Domain.Aggregates;
using AuditLog.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ContractAction = AuditLog.Contracts.AuditAction;
using DomainAction = AuditLog.Domain.ValueObjects.AuditAction;

namespace AuditLog.Api.Controllers;

/// <summary>
/// Controller de consulta somente-leitura da trilha de auditoria (design §8.1, §8.2, REQ-007).
/// <para>
/// Expõe dois endpoints GET:
/// <list type="bullet">
/// <item><description><c>GET /api/v1/audit-logs</c> — listagem paginada com filtros.</description></item>
/// <item><description><c>GET /api/v1/audit-logs/{entityType}/{entityId}</c> — histórico de entidade específica.</description></item>
/// </list>
/// </para>
/// <para>
/// Nenhum endpoint de escrita é exposto (REQ-007.1). Tentativas de POST/PUT/PATCH/DELETE
/// retornam 405 Method Not Allowed (AUD-ERR-007).
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/audit-logs")]
[Authorize]
[Produces("application/json")]
public sealed class AuditLogQueryController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>Inicializa o controller com o dispatcher MediatR.</summary>
    public AuditLogQueryController(ISender sender)
    {
        ArgumentNullException.ThrowIfNull(sender);
        _sender = sender;
    }

    /// <summary>
    /// Lista a trilha de auditoria com filtros opcionais (REQ-007, design §8.1).
    /// </summary>
    /// <param name="entityType">Filtra por tipo de entidade (opcional).</param>
    /// <param name="entityId">Filtra por identificador de entidade (opcional).</param>
    /// <param name="userId">Filtra por autor da operação (opcional).</param>
    /// <param name="from">Início do intervalo de <c>created_at</c> em ISO-8601 (opcional).</param>
    /// <param name="to">Fim do intervalo de <c>created_at</c> em ISO-8601 (opcional).</param>
    /// <param name="page">Número da página (base 1, default 1).</param>
    /// <param name="pageSize">Tamanho da página (default 50, máx 200).</param>
    /// <param name="ct">Token de cancelamento.</param>
    /// <returns>Envelope paginado com registros de auditoria.</returns>
    /// <response code="200">Consulta executada com sucesso.</response>
    /// <response code="400">Filtro de consulta inválido (AUD-ERR-001) ou período inválido (AUD-ERR-006).</response>
    /// <response code="401">Requisição sem JWT válido (AUD-ERR-003).</response>
    /// <response code="403">Papel sem permissão (AUD-ERR-002) ou tenant ausente (AUD-ERR-008).</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedAuditLogResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAuditLogs(
        [FromQuery] string? entityType,
        [FromQuery] Guid? entityId,
        [FromQuery] Guid? userId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = new ListAuditLogsQuery
        {
            EntityType = entityType,
            EntityId = entityId,
            UserId = userId,
            From = from,
            To = to,
            Page = page,
            PageSize = pageSize
        };

        var result = await _sender.Send(query, ct);

        var response = MapToPagedResponse(result.Items, result.Page, result.PageSize, result.TotalCount);
        return Ok(response);
    }

    /// <summary>
    /// Retorna o histórico de auditoria de uma entidade específica (REQ-007.3, design §8.2).
    /// </summary>
    /// <param name="entityType">Tipo da entidade auditada.</param>
    /// <param name="entityId">Identificador da entidade auditada.</param>
    /// <param name="from">Início do intervalo de <c>created_at</c> (opcional).</param>
    /// <param name="to">Fim do intervalo de <c>created_at</c> (opcional).</param>
    /// <param name="page">Número da página (base 1, default 1).</param>
    /// <param name="pageSize">Tamanho da página (default 50, máx 200).</param>
    /// <param name="ct">Token de cancelamento.</param>
    /// <returns>Envelope paginado com histórico da entidade.</returns>
    /// <response code="200">Histórico retornado com sucesso.</response>
    /// <response code="400">Parâmetros inválidos (AUD-ERR-001) ou período inválido (AUD-ERR-006).</response>
    /// <response code="401">Requisição sem JWT válido (AUD-ERR-003).</response>
    /// <response code="403">Papel sem permissão (AUD-ERR-002) ou tenant ausente (AUD-ERR-008).</response>
    /// <response code="404">Nenhum registro encontrado para a entidade (AUD-ERR-004, opcional).</response>
    [HttpGet("{entityType}/{entityId:guid}")]
    [ProducesResponseType(typeof(PagedAuditLogResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEntityAuditHistory(
        string entityType,
        Guid entityId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = new GetEntityAuditHistoryQuery
        {
            EntityType = entityType,
            EntityId = entityId,
            From = from,
            To = to,
            Page = page,
            PageSize = pageSize
        };

        var result = await _sender.Send(query, ct);

        var response = MapToPagedResponse(result.Items, result.Page, result.PageSize, result.TotalCount);
        return Ok(response);
    }

    // ------------------------------------------------------------------ Mapeamento

    private static PagedAuditLogResponse MapToPagedResponse(
        IReadOnlyList<AuditLogAggregate> items,
        int page,
        int pageSize,
        int totalCount)
    {
        var responseItems = items.Select(MapToResponse).ToList().AsReadOnly();
        return new PagedAuditLogResponse(responseItems, page, pageSize, totalCount);
    }

    private static AuditLogResponse MapToResponse(AuditLogAggregate aggregate)
    {
        // Delta mapeado para objeto neutro para serialização JSON
        object? delta = aggregate.Delta.Kind switch
        {
            AuditDeltaKind.Create => aggregate.Delta.After,
            AuditDeltaKind.Delete => aggregate.Delta.Before,
            AuditDeltaKind.Update => aggregate.Delta.Changes?.ToDictionary(
                kvp => kvp.Key,
                kvp => new { before = kvp.Value.Before, after = kvp.Value.After }),
            _ => null
        };

        return new AuditLogResponse(
            Id: aggregate.Id.Value,
            UserId: aggregate.ActorId.Value,
            EntityType: aggregate.EntityTypePersisted,
            EntityId: aggregate.EntityIdPersisted,
            Action: MapAction(aggregate.Action),
            Delta: delta,
            CreatedAt: aggregate.CreatedAt);
    }

    private static ContractAction MapAction(DomainAction domainAction) =>
        domainAction switch
        {
            DomainAction.Create => ContractAction.Create,
            DomainAction.Update => ContractAction.Update,
            DomainAction.Delete => ContractAction.Delete,
            _ => throw new ArgumentOutOfRangeException(nameof(domainAction), domainAction, null)
        };
}
