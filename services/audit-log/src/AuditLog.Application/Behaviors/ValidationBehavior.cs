using FluentValidation;
using MediatR;

namespace AuditLog.Application.Behaviors;

/// <summary>
/// Behavior de pipeline MediatR que executa validações FluentValidation na borda (design §5.4).
/// Posição na pipeline: 1ª (mais externa) — intercepta antes de qualquer outro behavior.
/// Lança <see cref="ValidationException"/> ao encontrar falhas de validação; o handler nunca é invocado.
/// </summary>
/// <typeparam name="TRequest">Tipo da requisição MediatR.</typeparam>
/// <typeparam name="TResponse">Tipo da resposta MediatR.</typeparam>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    /// <summary>Inicializa o behavior com os validadores registrados no DI.</summary>
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
