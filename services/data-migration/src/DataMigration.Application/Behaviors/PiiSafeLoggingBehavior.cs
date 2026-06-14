using DataMigration.Application.Logging;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DataMigration.Application.Behaviors;

/// <summary>
/// Pipeline behavior que garante que logs estruturados do pipeline
/// não contenham PII (nome, e-mail, telefone de contatos — LGPD, RNF 3).
///
/// Responsabilidades:
/// - Registra início e conclusão de cada request no pipeline sem vazar PII.
/// - Qualquer campo marcado como PII (<see cref="PiiSafeLogger.ProhibitedFields"/>)
///   é bloqueado antes de ser emitido.
/// - Referências por índice de linha são permitidas; nomes/e-mails/telefones nunca.
///
/// Sequência de behaviors (design §5.4):
///   TenantContext → Authorization → FeatureFlag → Validation →
///   PiiSafeLogging ← este behavior → Idempotency → Handler
///
/// Rastreia: design §5.4, §10, §11, RNF 3, RISK-MIGR-03, TASK-24.
/// </summary>
public sealed class PiiSafeLoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<PiiSafeLoggingBehavior<TRequest, TResponse>> _logger;

    /// <summary>
    /// Cria o behavior com o logger estruturado injetado.
    /// </summary>
    public PiiSafeLoggingBehavior(
        ILogger<PiiSafeLoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        // Log de início — sem parâmetros do request (evita PII por reflexão)
        _logger.LogInformation(
            "[PII-SAFE] Iniciando pipeline para {request_type}",
            requestName);

        TResponse response;
        try
        {
            response = await next();
        }
        catch (Exception ex)
        {
            // Log de erro sem PII: apenas tipo de request e mensagem sanitizada
            var safeMessage = PiiSafeLogger.SanitizeMessage(ex.Message);
            _logger.LogError(
                "[PII-SAFE] Falha no pipeline para {request_type}: {safe_message}",
                requestName,
                safeMessage);
            throw;
        }

        _logger.LogInformation(
            "[PII-SAFE] Pipeline concluído para {request_type}",
            requestName);

        return response;
    }
}
