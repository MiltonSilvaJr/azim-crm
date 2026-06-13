using MediatR;
using Microsoft.Extensions.Logging;
using TenantAdministration.Application.Ports;

namespace TenantAdministration.Application.Behaviors;

/// <summary>
/// Behavior MediatR #6 na cadeia do pipeline (design.md §5.4, Req 11.4).
/// Abre transação de banco de dados antes do handler; aplica <c>SET app.current_tenant</c>
/// via <see cref="IUnitOfWork"/> quando há contexto de tenant (RLS — camada 1, design.md §14).
/// Em caso de sucesso, confirma (commit) e o Outbox é gravado na mesma transação.
/// Em caso de exceção, faz rollback.
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse>(
    IUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        await unitOfWork.BeginAsync(tenantContext.TenantId, cancellationToken);

        try
        {
            var response = await next(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Rollback de transação em {RequestName} por exceção: {Message}",
                typeof(TRequest).Name,
                ex.Message);

            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
