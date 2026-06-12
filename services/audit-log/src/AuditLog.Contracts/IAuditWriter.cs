namespace AuditLog.Contracts;

/// <summary>
/// Porta de escrita in-process para o módulo de auditoria.
/// Permite que outros módulos registrem entradas sem depender da implementação concreta.
/// <para>
/// O implementador é responsável por:
/// <list type="bullet">
/// <item><description>Resolver <c>TenantId</c> a partir do contexto autenticado.</description></item>
/// <item><description>Resolver <c>CorrelationId</c> / <c>TraceId</c> do contexto de execução.</description></item>
/// <item><description>Aplicar mascaramento de PII antes de persistir os campos <c>RawBefore</c> / <c>RawAfter</c>.</description></item>
/// <item><description>Garantir idempotência quando o chamador reenviar a mesma operação.</description></item>
/// </list>
/// </para>
/// </summary>
public interface IAuditWriter
{
    /// <summary>
    /// Registra assincronamente uma entrada de auditoria.
    /// </summary>
    /// <param name="request">Dados da operação a ser auditada.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    /// <returns>
    /// <see cref="Task"/> que representa a operação assíncrona.
    /// Falhas de auditoria NÃO devem propagar exceções para o chamador
    /// — a implementação deve decidir a política de tolerância a falhas.
    /// </returns>
    Task RecordAsync(AuditEntryRequest request, CancellationToken cancellationToken);
}
