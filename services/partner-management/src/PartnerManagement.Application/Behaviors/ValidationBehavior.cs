using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace PartnerManagement.Application.Behaviors;

/// <summary>
/// Behavior MediatR (posição 3 no pipeline) que executa validadores FluentValidation.
/// Requests inválidos não chegam ao handler e não produzem efeito colateral.
/// Lança <see cref="ValidationException"/> com todos os erros consolidados.
/// Mapeia: design §5.5, design §5.4.
/// </summary>
/// <typeparam name="TRequest">Tipo do request MediatR.</typeparam>
/// <typeparam name="TResponse">Tipo da resposta.</typeparam>
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
        {
            return await next().ConfigureAwait(false);
        }

        ValidationContext<TRequest> context = new(request);

        IEnumerable<Task<ValidationResult>> validationTasks = validators
            .Select(v => v.ValidateAsync(context, cancellationToken));

        ValidationResult[] results = await Task.WhenAll(validationTasks).ConfigureAwait(false);

        IReadOnlyList<ValidationFailure> failures = results
            .SelectMany(r => r.Errors)
            .Where(e => e is not null)
            .ToList();

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next().ConfigureAwait(false);
    }
}
