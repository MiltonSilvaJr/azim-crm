using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Behaviors;

/// <summary>
/// Contrato marcador para queries de relatório que carregam o <see cref="ReportScope"/>
/// já resolvido pelo servidor (via <c>ReportDispatcher</c> → <c>IScopeResolver</c>).
///
/// Permite que o <see cref="AuthorizationBehavior{TRequest,TResponse}"/> valide o escopo
/// sem invocar novamente o <c>IScopeResolver</c> — eliminando a chamada dupla da dívida técnica
/// da Onda 5 e ativando a segunda camada de defesa em profundidade (ADR-0001, DD-006).
///
/// Invariante: <c>Scope.Role != PlatformOperator</c> — garantida por <see cref="ReportScope.Create"/>.
/// O behavior verifica essa invariante como gate de segurança adicional.
///
/// Mapeia: Onda 6 (refactor dívida), design §5.4, ADR-0001, RNF 5, DD-006.
/// </summary>
public interface IScopedQuery
{
    /// <summary>Escopo de acesso RBAC resolvido no servidor.</summary>
    ReportScope Scope { get; }
}
