using DataMigration.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DataMigration.Infrastructure.Persistence;

/// <summary>
/// Interceptor de SaveChanges que preenche a shadow property <c>TenantId</c>
/// nas entradas de <see cref="MigrationLogEntry"/> antes de persistir.
///
/// O <c>TenantId</c> não existe como propriedade em <see cref="MigrationLogEntry"/>
/// (pertence ao agregado, que o propaga via shadow property no EF Core).
///
/// Rastreia: design §6.1, §7, DD-008, ADR-0001, TASK-15.
/// </summary>
internal sealed class TenantSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly Guid _tenantId;

    public TenantSaveChangesInterceptor(Guid tenantId)
    {
        _tenantId = tenantId;
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        SetTenantOnLogEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        SetTenantOnLogEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void SetTenantOnLogEntries(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<MigrationLogEntry>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property("TenantId").CurrentValue = _tenantId;
            }
        }
    }
}
