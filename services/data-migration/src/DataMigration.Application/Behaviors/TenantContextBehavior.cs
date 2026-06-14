using DataMigration.Application.Exceptions;
using DataMigration.Application.Ports;
using MediatR;

namespace DataMigration.Application.Behaviors;

/// <summary>
/// Pipeline behavior que garante que <c>app.current_tenant</c> está definido
/// antes de qualquer command ou query ser processado.
///
/// Executa antes de todos os outros behaviors (deve ser registrado primeiro).
/// Falha-fechada: bloqueia quando tenant ausente (ADR-0001).
///
/// Rastreia: design §5.4, ADR-0001, TASK-12.
/// </summary>
public sealed class TenantContextBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentTenantContext _tenantContext;

    /// <summary>Cria o behavior com o contexto de tenant injetado.</summary>
    public TenantContextBehavior(ICurrentTenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (_tenantContext.TenantId is null)
        {
            throw new MigrationDomainException(
                "MIG-ERR-005",
                "Tenant não identificado no contexto. Operação bloqueada (ADR-0001).");
        }

        return await next();
    }
}
