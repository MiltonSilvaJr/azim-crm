using FluentValidation;
using MediatR;

namespace OpportunityPipeline.Application.Behaviors;

/// <summary>
/// Pipeline behavior: executa todos os validators FluentValidation registrados para o command.
/// Executa QUINTO — após idempotência, antes da transação.
/// Agrega todos os erros e lança ValidationException com todos de uma vez.
/// Mapeia: design §5.4 posição 5, design §5.5.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next(cancellationToken).ConfigureAwait(false);

        var context = new ValidationContext<TRequest>(request);

        var results = await Task
            .WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken)))
            .ConfigureAwait(false);

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
            return await next(cancellationToken).ConfigureAwait(false);

        // Agrega erros por campo
        var errors = failures
            .GroupBy(f => f.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => f.ErrorMessage).ToArray());

        throw new Common.ValidationException(errors);
    }
}
