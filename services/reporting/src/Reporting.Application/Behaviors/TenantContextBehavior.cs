using MediatR;
using Reporting.Application.Exceptions;

namespace Reporting.Application.Behaviors;

/// <summary>
/// Behavior 2: garante que <c>tenant_id</c> está presente e válido antes de tocar o banco.
///
/// Falha-fechada: sem tenant válido → lança <see cref="TenantNotResolvedException"/> (<c>REPORT-ERR-409</c>).
/// Não executa o próximo behavior quando o tenant estiver ausente.
///
/// Mapeia: TASK-12, design §5.4, ADR-0001, Req 8, REPORT-ERR-409.
/// </summary>
public sealed class TenantContextBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Verifica apenas para queries que implementam IReportingQuery
        if (request is IReportingQuery rq)
        {
            if (rq.TenantId == Guid.Empty)
            {
                throw new TenantNotResolvedException(
                    $"Tenant não resolvido para a query {typeof(TRequest).Name}. " +
                    $"Verifique o token JWT. (REPORT-ERR-409, ADR-0001)");
            }
        }

        return await next();
    }
}
