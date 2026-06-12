using AuditLog.Application.Abstractions;
using AuditLog.Application.Results;
using AuditLog.Domain.Aggregates;
using MediatR;

namespace AuditLog.Application.Queries;

/// <summary>
/// Query de histórico de auditoria de uma entidade específica (REQ-007.3).
/// Retorna registros filtrados por <c>entity_type</c> + <c>entity_id</c>,
/// paginados e ordenados por <c>created_at</c> desc.
/// </summary>
public sealed record GetEntityAuditHistoryQuery : IRequest<PagedResult<AuditLogAggregate>>, IAuditQuery, IEntityContextRequest
{
    /// <summary>Tipo da entidade (obrigatório).</summary>
    public required string EntityType { get; init; }

    /// <summary>Identificador da entidade (obrigatório).</summary>
    public required Guid EntityId { get; init; }

    /// <summary>Início do intervalo de <c>created_at</c> (opcional).</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Fim do intervalo de <c>created_at</c> (opcional).</summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>Número da página; base 1. Default: 1.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Tamanho da página. Default: 50; máx: 200 (REQ-007.4).</summary>
    public int PageSize { get; init; } = 50;

    // IEntityContextRequest
    string? IEntityContextRequest.EntityType => EntityType;
    Guid? IEntityContextRequest.EntityId => EntityId;
}
