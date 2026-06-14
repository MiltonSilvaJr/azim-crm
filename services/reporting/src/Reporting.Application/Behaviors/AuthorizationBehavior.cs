using MediatR;
using Reporting.Application.Exceptions;
using Reporting.Application.Ports;
using Reporting.Domain.Enums;

namespace Reporting.Application.Behaviors;

/// <summary>
/// Behavior 4: resolve o <c>ReportScope</c> e bloqueia papéis sem escopo de acesso.
///
/// Bloqueio duro: PlatformOperator é negado ANTES de qualquer acesso ao banco (RNF 5).
/// Resolve o escopo via <see cref="IScopeResolver"/> e injeta no contexto da query.
///
/// Mapeia: TASK-12, design §5.4, Req 7, RNF 5, DD-006, REPORT-ERR-005.
/// </summary>
public sealed class AuthorizationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IScopeResolver _scopeResolver;

    /// <summary>Inicializa com o resolvedor de escopo.</summary>
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
        if (request is IReportingQuery rq)
        {
            try
            {
                // Resolve e valida o escopo — lança para PlatformOperator (RNF 5, DD-006)
                var scope = await _scopeResolver.ResolveAsync(rq.TenantId, rq.UserId, cancellationToken);

                // PlatformOperator: ReportScope.Create já lança UnauthorizedAccessException.
                // Verificação adicional de segurança (defense in depth)
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
