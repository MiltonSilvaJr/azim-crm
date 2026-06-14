namespace ActivityManagement.Application.Behaviors;

using FluentValidation;
using MediatR;

/// <summary>
/// Exceção lançada pelo <c>ValidationBehavior</c> quando a validação FluentValidation falha.
/// Carrega os erros de validação para mapeamento em ACT-ERR-* pelo exception handler da API.
/// Mapeia: design §5.4, design §5.5, TASK-06.
/// </summary>
public sealed class ValidationException : Exception
{
    /// <summary>Erros de validação agrupados por campo.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    /// <summary>
    /// Inicializa a exceção a partir dos resultados de validação FluentValidation.
    /// </summary>
    public ValidationException(IEnumerable<FluentValidation.Results.ValidationFailure> failures)
        : base("Um ou mais erros de validação ocorreram.")
    {
        Errors = failures
            .GroupBy(f => f.PropertyName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => f.ErrorMessage).ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Pipeline behavior MediatR (3ª posição — design §5.4).
/// Executa todos os validators FluentValidation registrados para o tipo do request.
/// Requests inválidos lançam <see cref="ValidationException"/> antes de atingir o handler,
/// sem efeito colateral (design §5.5).
/// Mapeia: design §5.4/§5.5, TASK-06.
/// </summary>
/// <typeparam name="TRequest">Tipo do Command ou Query MediatR.</typeparam>
/// <typeparam name="TResponse">Tipo da resposta.</typeparam>
public sealed class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    /// <summary>
    /// Inicializa o behavior com os validators registrados no DI para o tipo do request.
    /// </summary>
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest                          request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken                 cancellationToken)
    {
        if (!_validators.Any())
            return await next(cancellationToken);

        var context = new ValidationContext<TRequest>(request);

        var results = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count != 0)
            throw new ValidationException(failures);

        return await next(cancellationToken);
    }
}
