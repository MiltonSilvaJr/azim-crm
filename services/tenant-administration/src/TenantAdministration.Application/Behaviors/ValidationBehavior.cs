using FluentValidation;
using MediatR;
using ApplicationValidationException = TenantAdministration.Application.Exceptions.ValidationException;

namespace TenantAdministration.Application.Behaviors;

/// <summary>
/// Behavior MediatR #3 na cadeia do pipeline (design.md §5.4).
/// Executa todos os <see cref="IValidator{T}"/> registrados para o request antes de chamar o handler.
/// Falha de validação lança <see cref="Exceptions.ValidationException"/> sem chegar ao handler.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next(cancellationToken);

        var context = new ValidationContext<TRequest>(request);

        var failures = (await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
            return await next(cancellationToken);

        var validationErrors = failures.Select(f =>
            new Exceptions.ValidationError(f.PropertyName, f.ErrorMessage, f.ErrorCode));

        throw new ApplicationValidationException(validationErrors);
    }
}
