using MediatR;

namespace AccountManagement.Application.Behaviors;

/// <summary>
/// Behavior MediatR que valida que o <see cref="BuScopeContext"/> foi inicializado
/// antes de qualquer operação de dados. Análogo a <see cref="TenantScopeBehavior{TRequest,TResponse}"/>.
///
/// Posição no pipeline: 3ª (após <see cref="TenantScopeBehavior{TRequest,TResponse}"/>
/// e antes de <see cref="ValidationBehavior{TRequest,TResponse}"/>) — design §5.4.
///
/// Garante que o filtro global de BU do EF Core terá o escopo resolvido para todas
/// as queries e commands subsequentes (ADR-0009).
///
/// Em produção, o escopo de BU é extraído dos claims do principal pelo controller
/// e injetado via <see cref="BuScopeContext.SetScope"/>. Em testes, o contexto é
/// configurado diretamente.
///
/// Mapeia: ADR-0009, design §5.4, VAL-ACC-03.
/// </summary>
internal sealed class BuScopeBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly BuScopeContext _buScopeContext;

    /// <summary>Inicializa o behavior com o contexto de escopo de BU injetado.</summary>
    public BuScopeBehavior(BuScopeContext buScopeContext)
    {
        _buScopeContext = buScopeContext;
    }

    /// <inheritdoc />
    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Valida que o contexto de BU foi inicializado antes de prosseguir.
        // Se o controller não populou o BuScopeContext, lança exceção.
        _buScopeContext.GetRequiredBuIds();
        _buScopeContext.GetRequiredIsTenantWide();

        return next(cancellationToken);
    }
}
