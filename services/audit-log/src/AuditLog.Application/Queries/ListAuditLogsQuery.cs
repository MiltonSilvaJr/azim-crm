using AuditLog.Application.Abstractions;
using AuditLog.Application.Results;
using AuditLog.Domain.Aggregates;
using MediatR;

namespace AuditLog.Application.Queries;

/// <summary>
/// Marcador de interface para queries de auditoria.
/// O <see cref="Behaviors.AuthorizationBehavior{TRequest,TResponse}"/> aplica verificação de papel
/// apenas a requisições que implementam esta interface (design §5.4).
/// </summary>
public interface IAuditQuery { }

/// <summary>
/// Query de listagem paginada da trilha de auditoria (REQ-007, REQ-008).
/// Suporta filtros opcionais por <c>entity_type</c>, <c>entity_id</c>, <c>user_id</c> e intervalo de <c>created_at</c>.
/// Ordenação: <c>created_at</c> desc.
/// </summary>
public sealed record ListAuditLogsQuery : IRequest<PagedResult<AuditLogAggregate>>, IAuditQuery, IEntityContextRequest
{
    /// <summary>Filtra por tipo de entidade (opcional).</summary>
    public string? EntityType { get; init; }

    /// <summary>Filtra por identificador de entidade (opcional).</summary>
    public Guid? EntityId { get; init; }

    /// <summary>Filtra por autor da operação (opcional).</summary>
    public Guid? UserId { get; init; }

    /// <summary>Início do intervalo de <c>created_at</c> (opcional).</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Fim do intervalo de <c>created_at</c> (opcional).</summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>Número da página; base 1. Default: 1.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Tamanho da página. Default: 50; máx: 200 (REQ-007.4, AUD-ERR-001).</summary>
    public int PageSize { get; init; } = 50;

    // IEntityContextRequest (para LoggingBehavior — não expõe PII, apenas metadados de entidade)
    string? IEntityContextRequest.EntityType => EntityType;
    Guid? IEntityContextRequest.EntityId => EntityId;
}
