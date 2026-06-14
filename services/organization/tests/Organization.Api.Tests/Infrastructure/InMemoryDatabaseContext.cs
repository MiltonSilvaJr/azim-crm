using Organization.Application.Ports;

namespace Organization.Api.Tests.Infrastructure;

/// <summary>
/// Implementação de <see cref="IDatabaseContext"/> para testes de integração.
/// Opera como no-op — não executa transações reais (in-memory provider não as suporta).
/// </summary>
internal sealed class InMemoryDatabaseContext : IDatabaseContext
{
    /// <inheritdoc/>
    public Task SetTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
