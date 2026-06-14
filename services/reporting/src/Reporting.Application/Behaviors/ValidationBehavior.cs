using FluentValidation;
using MediatR;

namespace Reporting.Application.Behaviors;

/// <summary>
/// Behavior 3: valida a query usando os validadores FluentValidation registrados.
///
/// Agrega todos os erros de validação e lança <see cref="ValidationException"/> quando há falhas.
/// Validação sintática (período, UUIDs, enum) — não valida regras de negócio (P2, design §5.5).
///
/// Mapeia: TASK-12, design §5.4, §5.5, RNF, REPORT-ERR-001/002/003/004.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    /// <summary>Inicializa com a coleção de validadores registrados para o tipo de request.</summary>
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        ArgumentNullException.ThrowIfNull(validators);
        _validators = validators;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var failures = _validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}
