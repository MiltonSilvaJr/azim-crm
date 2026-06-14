using MediatR;

namespace AccountManagement.Application.Behaviors;

/// <summary>
/// Behavior MediatR que resolve o <see cref="TenantContext"/> antes de qualquer operação de dados.
///
/// Posição no pipeline: 2ª (após <see cref="CorrelationLoggingBehavior{TRequest,TResponse}"/>
/// e antes de <see cref="ValidationBehavior{TRequest,TResponse}"/>) — design §5.4.
///
/// Garante que o filtro global de tenant do EF Core terá o <c>TenantId</c> resolvido
/// para todas as queries e commands subsequentes (DD-002, ADR-0001).
///
/// Em ambiente de produção o <c>TenantId</c> é extraído do JWT pelo middleware de autenticação
/// e injetado via <see cref="TenantContext.SetTenant"/>. Em testes, o contexto é configurado
/// diretamente.
///
/// Mapeia: design §5.4, Req 10, RNF 5, DD-002.
/// </summary>
internal sealed class TenantScopeBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly TenantContext _tenantContext;

    /// <summary>Inicializa o behavior com o contexto de tenant injetado.</summary>
    public TenantScopeBehavior(TenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    /// <inheritdoc />
    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Valida que o contexto de tenant foi inicializado antes de prosseguir.
        // Se o middleware de autenticação não populou o TenantContext, lança exceção.
        _tenantContext.GetRequiredTenantId();

        return next(cancellationToken);
    }
}
