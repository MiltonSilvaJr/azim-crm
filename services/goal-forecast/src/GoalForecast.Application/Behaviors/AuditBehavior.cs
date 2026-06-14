using GoalForecast.Application.Commands;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GoalForecast.Application.Behaviors;

/// <summary>
/// Behavior 5/5: após sucesso de commands de escrita, garante que domain events do aggregate
/// foram enfileirados no outbox para auditoria imutável (Req 10, RNF 5).
///
/// Atua apenas em requests marcados com <see cref="IHasPrincipal"/> cujo resultado
/// carrega domain events a auditar. Não enfileira em caso de falha do handler.
///
/// Na Onda 3, o comportamento de auditoria é registrado em log estruturado.
/// O enfileiramento real no outbox ocorre dentro do repositório (Onda 4).
///
/// Mapeia: Req 10, RNF 5, design §5.4, TASK-10.
/// </summary>
public sealed class AuditBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<AuditBehavior<TRequest, TResponse>> _logger;

    /// <summary>Inicializa o behavior com o logger.</summary>
    public AuditBehavior(ILogger<AuditBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Apenas commands (que carregam principal) geram trilha de auditoria.
        bool isCommand = request is IHasPrincipal;

        TResponse response;
        try
        {
            response = await next();
        }
        catch
        {
            // Falha no handler: NÃO enfileira evento de auditoria (design §5.4).
            throw;
        }

        // Sucesso: registrar ação auditável.
        if (isCommand && request is IHasPrincipal hasPrincipal)
        {
            _logger.LogInformation(
                "Auditoria: command {CommandType} executado com sucesso por tenant {TenantId}",
                typeof(TRequest).Name,
                hasPrincipal.Principal.TenantId);
        }

        return response;
    }
}
