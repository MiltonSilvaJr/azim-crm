using Authentication.Application.Services;

namespace Authentication.Infrastructure.Audit;

/// <summary>
/// Implementação no-op do <see cref="IAuditEventEmitter"/> para uso em desenvolvimento.
///
/// Não persiste eventos auditáveis. Deve ser substituído pelo adapter real do
/// módulo <c>audit-log</c> em produção (design.md § 9.1, RNF 10).
///
/// Mapeia: TASK-07, TASK-08, TASK-09, RNF 10.
/// </summary>
public sealed class NoOpAuditEventEmitter : IAuditEventEmitter
{
    /// <inheritdoc/>
    public Task EmitAsync(
        string eventType,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // No-op: não persiste evento em desenvolvimento
        return Task.CompletedTask;
    }
}
