using MediatR;

namespace DataMigration.Application.Behaviors;

/// <summary>
/// Pipeline behavior que garante que logs estruturados do pipeline
/// não contenham PII (nome, e-mail, telefone de contatos).
///
/// Implementação atual: pass-through com placeholder para interceptação
/// de logs estruturados (implementação completa na Onda 6 — TASK-24).
/// O teste valida que o behavior não introduz vazamento de PII.
///
/// PII nunca deve aparecer em:
///   - mensagens de log;
///   - correlation_id;
///   - trace_id;
///   - span_id;
///   - propriedades estruturadas.
///
/// Rastreia: design §5.4, §11, RNF 3, TASK-12, TASK-24.
/// </summary>
public sealed class PiiSafeLoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Pass-through: a sanitização de PII é aplicada pelo PiiSafeLogger
        // nas implementações de ILogger (Onda 6, TASK-24).
        // Este behavior serve como ponto de extensão e documentação da
        // invariante de segurança de PII no pipeline.
        return await next();
    }
}
