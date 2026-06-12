namespace AuditLog.Application.Abstractions;

/// <summary>
/// Interface opcional implementada por commands e queries que carregam contexto de entidade.
/// Usada pelo <c>LoggingBehavior</c> para emitir logs estruturados sem PII (RNF-002.1).
/// </summary>
public interface IEntityContextRequest
{
    /// <summary>Tipo da entidade auditada (ex.: <c>Opportunity</c>). Pode ser nulo em queries de listagem sem filtro de entidade.</summary>
    string? EntityType { get; }

    /// <summary>Identificador da entidade auditada. Pode ser nulo em queries de listagem sem filtro de entidade.</summary>
    Guid? EntityId { get; }
}
