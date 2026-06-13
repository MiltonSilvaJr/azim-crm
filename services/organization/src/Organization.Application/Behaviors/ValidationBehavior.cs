using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Organization.Application.Behaviors;

/// <summary>
/// Pipeline behavior de validação sintática via FluentValidation.
/// Executa todos os validadores registrados para <typeparamref name="TRequest"/> antes do handler.
/// Command inválido lança <see cref="ValidationException"/> sem executar o handler (RNF 2.2).
/// </summary>
/// <typeparam name="TRequest">Tipo do request.</typeparam>
/// <typeparam name="TResponse">Tipo da resposta.</typeparam>
public sealed class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly ILogger<ValidationBehavior<TRequest, TResponse>> _logger;

    /// <summary>Inicializa o behavior com os validadores registrados para o request.</summary>
    public ValidationBehavior(
        IEnumerable<IValidator<TRequest>> validators,
        ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    {
        _validators = validators;
        _logger = logger;
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
        var results = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
        {
            _logger.LogDebug(
                "Validação falhou para {RequestType}: {ErrorCount} erro(s)",
                typeof(TRequest).Name,
                failures.Count);
            throw new ValidationException(failures);
        }

        return await next(cancellationToken);
    }
}
