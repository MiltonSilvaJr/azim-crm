using FluentValidation;
using MediatR;

namespace GoalForecast.Application.Behaviors;

/// <summary>
/// Behavior 3/5: validação sintática de commands/queries via FluentValidation.
/// Lança <see cref="ValidationException"/> com erros detalhados quando há violação.
/// Controllers mapeiam <see cref="ValidationException"/> para HTTP 400 com códigos GF-ERR-*.
///
/// Atua em todos os requests para os quais existe um <see cref="IValidator{T}"/> registrado.
/// Mapeia: design §5.4, §5.5, TASK-10.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    /// <summary>Inicializa com validators registrados no container DI.</summary>
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next();
    }
}
