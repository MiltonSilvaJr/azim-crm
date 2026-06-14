using MediatR;
using Reporting.Application.Exceptions;
using Reporting.Application.Ports;
using Reporting.Domain.Enums;

namespace Reporting.Application.Behaviors;

/// <summary>
/// Behavior 4: valida o escopo RBAC e bloqueia papéis sem acesso a relatórios.
///
/// Estratégia de resolução de escopo (elimina chamada dupla — dívida Onda 5):
/// <list type="bullet">
///   <item><description>
///     Para queries que implementam <see cref="IScopedQuery"/>: valida o <see cref="Domain.ValueObjects.ReportScope"/>
///     já presente na query — resolvido uma única vez pelo <c>ReportDispatcher</c> via <c>IScopeResolver</c>.
///     <b>Não invoca o <c>IScopeResolver</c> novamente.</b>
///   </description></item>
///   <item><description>
///     Para queries que implementam <see cref="IReportingQuery"/> (sem scope pré-resolvido):
///     invoca o <c>IScopeResolver</c> para resolver o escopo. Usado em testes de behavior isolados.
///   </description></item>
/// </list>
///
/// Bloqueio duro: <c>PlatformOperator</c> é negado ANTES de qualquer acesso ao banco (RNF 5, DD-006).
/// Segunda camada de defesa em profundidade (ADR-0001): o <c>ReportScope.Create</c> já bloqueia
/// no Dispatcher; o behavior garante que scope inválido nunca chegue ao handler.
///
/// Mapeia: TASK-12, Onda 6 (refactor dívida IScopeResolver), design §5.4, Req 7, RNF 5, DD-006,
///         REPORT-ERR-005, ADR-0001.
/// </summary>
public sealed class AuthorizationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IScopeResolver _scopeResolver;

    /// <summary>Inicializa com o resolvedor de escopo (usado apenas para <see cref="IReportingQuery"/>).</summary>
    public AuthorizationBehavior(IScopeResolver scopeResolver)
    {
        ArgumentNullException.ThrowIfNull(scopeResolver);
        _scopeResolver = scopeResolver;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Caminho 1 — query com scope já resolvido (IScopedQuery):
        // Não invoca o IScopeResolver novamente. Valida a invariante de bloqueio de PlatformOperator
        // como segunda camada de defesa em profundidade (ADR-0001, DD-006, RNF 5).
        if (request is IScopedQuery scopedQuery)
        {
            // ReportScope.Create já lança UnauthorizedAccessException para PlatformOperator.
            // Esta verificação é gate de defesa adicional — garante que o scope não foi adulterado.
            if (scopedQuery.Scope.Role == ReportingRole.PlatformOperator)
            {
                throw new AccessDeniedException(
                    "PlatformOperator não pode acessar relatórios. (REPORT-ERR-005, RNF 5)");
            }

            return await next();
        }

        // Caminho 2 — query com contexto de tenant/usuário (IReportingQuery) mas sem scope pré-resolvido:
        // Invoca o IScopeResolver. Mantido para testes de behavior isolados e extensibilidade.
        if (request is IReportingQuery rq)
        {
            try
            {
                var scope = await _scopeResolver.ResolveAsync(rq.TenantId, rq.UserId, cancellationToken);

                // Verificação adicional de segurança (defense in depth — ADR-0001)
                if (scope.Role == ReportingRole.PlatformOperator)
                {
                    throw new AccessDeniedException(
                        "PlatformOperator não pode acessar relatórios. (REPORT-ERR-005, RNF 5)");
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new AccessDeniedException(ex.Message, ex);
            }
        }

        return await next();
    }
}
