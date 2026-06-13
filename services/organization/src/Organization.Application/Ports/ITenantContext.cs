namespace Organization.Application.Ports;

/// <summary>
/// Port que fornece o contexto de tenant e usuário autenticado ao pipeline de commands/queries.
/// Preenchido pelo middleware de autenticação a partir do JWT validado.
/// </summary>
public interface ITenantContext
{
    /// <summary>Identificador do tenant corrente, extraído do JWT.</summary>
    Guid TenantId { get; }

    /// <summary>Identificador do usuário autenticado, extraído do JWT.</summary>
    Guid UserId { get; }

    /// <summary>Papéis do usuário autenticado indexados por BU.</summary>
    IReadOnlyDictionary<Guid, string> RolesByBu { get; }

    /// <summary>Identificador de correlação propagado para rastreabilidade (ADR-0009).</summary>
    Guid CorrelationId { get; }
}
