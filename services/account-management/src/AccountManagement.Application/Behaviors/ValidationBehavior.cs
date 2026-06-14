using FluentValidation;
using MediatR;

namespace AccountManagement.Application.Behaviors;

/// <summary>
/// Behavior MediatR que executa os validators FluentValidation antes do handler.
///
/// Posição no pipeline: 3ª (após <see cref="TenantScopeBehavior{TRequest,TResponse}"/>
/// e antes de <see cref="PiiAccessBehavior{TRequest,TResponse}"/>) — design §5.4.
///
/// Requests inválidos lançam <see cref="FluentValidation.ValidationException"/> e
/// não produzem efeito colateral (nenhum handler é invocado — rule api-and-contracts.md).
///
/// Mapeia: design §5.4, design §5.5, rule api-and-contracts.md.
/// </summary>
internal sealed class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    /// <summary>Inicializa o behavior com os validators registrados no container DI.</summary>
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next(cancellationToken);

        var context = new ValidationContext<TRequest>(request);

        var failures = (await Task.WhenAll(
                _validators.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next(cancellationToken);
    }
}
