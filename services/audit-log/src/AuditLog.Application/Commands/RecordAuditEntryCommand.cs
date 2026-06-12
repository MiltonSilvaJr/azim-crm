using AuditLog.Application.Abstractions;
using AuditLog.Domain.ValueObjects;
using MediatR;

namespace AuditLog.Application.Commands;

/// <summary>
/// Command interno que instrui o <see cref="AuditService"/> a registrar uma entrada de auditoria.
/// Disparado exclusivamente pelo <c>AuditServiceWriter</c> (porta concreta de <c>IAuditWriter</c>).
/// <para>
/// <b>Regras de preenchimento:</b>
/// <list type="bullet">
/// <item><description><c>TenantId</c> é derivado do contexto autenticado pelo handler via <see cref="ITenantContext"/>; não deve ser passado pelo chamador externo (REQ-005.3).</description></item>
/// <item><description><c>CreatedAt</c> nunca é campo deste command; definido exclusivamente pelo relógio do servidor no handler (REQ-002.4).</description></item>
/// </list>
/// </para>
/// </summary>
public sealed record RecordAuditEntryCommand : IRequest, IEntityContextRequest
{
    /// <summary>Identificador do autor da operação (usuário humano ou sistema). Não pode ser vazio (REQ-002.2).</summary>
    public required Guid ActorId { get; init; }

    /// <summary>Tipo da entidade auditada (ex.: <c>Opportunity</c>). Não vazio; máx. 50 chars.</summary>
    public required string EntityType { get; init; }

    /// <summary>Identificador da entidade auditada. Não pode ser vazio.</summary>
    public required Guid EntityId { get; init; }

    /// <summary>Tipo de operação: Create, Update ou Delete (REQ-002.3).</summary>
    public required AuditAction Action { get; init; }

    /// <summary>Estado da entidade antes da operação. Nulo para criações.</summary>
    public IReadOnlyDictionary<string, object?>? RawBefore { get; init; }

    /// <summary>Estado da entidade após a operação. Nulo para exclusões.</summary>
    public IReadOnlyDictionary<string, object?>? RawAfter { get; init; }

    // IEntityContextRequest
    string? IEntityContextRequest.EntityType => EntityType;
    Guid? IEntityContextRequest.EntityId => EntityId;
}
