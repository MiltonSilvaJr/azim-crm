using AuditLog.Domain.Abstractions;

namespace AuditLog.Infrastructure.Clock;

/// <summary>
/// Implementação de <see cref="IClock"/> que retorna <see cref="DateTimeOffset.UtcNow"/>.
/// Permite substituição por relógio determinístico em testes.
/// </summary>
internal sealed class SystemClock : IClock
{
    /// <inheritdoc/>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
