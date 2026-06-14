namespace Reporting.Application.Behaviors;

/// <summary>
/// Contrato marcador para queries de relatório que passam pelo pipeline de behaviors.
///
/// Carrega o contexto de tenant e usuário resolvidos pelo <c>CorrelationBehavior</c>
/// e verificados pelo <c>TenantContextBehavior</c>.
///
/// Mapeia: TASK-12, design §5.4, ADR-0001.
/// </summary>
public interface IReportingQuery
{
    /// <summary>Identificador do tenant resolvido do token JWT.</summary>
    Guid TenantId { get; }

    /// <summary>Identificador do usuário autenticado (sub do JWT).</summary>
    Guid UserId { get; }

    /// <summary>Identificador de correlação da requisição.</summary>
    string CorrelationId { get; }
}
