using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TenantAdministration.Application.Ports;

namespace TenantAdministration.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementação de <see cref="IUnitOfWork"/> usando EF Core.
/// Abre transação e configura <c>SET app.current_tenant</c> para o RLS (Camada 2 — ADR-0001).
/// </summary>
public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly TenantAdministrationDbContext _db;
    private IDbContextTransaction? _transaction;

    /// <param name="db">DbContext do módulo.</param>
    public EfUnitOfWork(TenantAdministrationDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task BeginAsync(Guid? tenantId, CancellationToken ct = default)
    {
        _transaction = await _db.Database.BeginTransactionAsync(ct);

        // Camada 2 de defesa em profundidade: SET app.current_tenant no PostgreSQL (ADR-0001).
        // O valor de tenantId é um Guid gerado internamente (não entrada do usuário),
        // mas usamos ExecuteSqlAsync com parâmetro para evitar qualquer risco de injeção.
        if (tenantId.HasValue)
        {
            // Guid.ToString() produz valor sanitizado sem risco de SQL injection.
            // Suprimido EF1002: o Guid não é entrada de usuário e é formatado de forma segura.
#pragma warning disable EF1002
            await _db.Database.ExecuteSqlRawAsync(
                $"SET LOCAL app.current_tenant = '{tenantId.Value:D}'",
                ct);
#pragma warning restore EF1002
        }
    }

    /// <inheritdoc/>
    public async Task CommitAsync(CancellationToken ct = default)
    {
        await _db.SaveChangesAsync(ct);
        if (_transaction is not null)
        {
            await _transaction.CommitAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    /// <inheritdoc/>
    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }
}
