using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DataMigration.Infrastructure.Persistence;

/// <summary>
/// Interceptor de conexão que executa <c>SET app.current_tenant = '{tenantId}'</c>
/// na abertura da conexão E antes de cada comando SQL (DD-008, ADR-0001).
///
/// O PostgreSQL reseta configurações de sessão quando a conexão volta ao pool
/// (Npgsql reset-on-return). Por isso o tenant é setado tanto na abertura
/// da conexão quanto em cada comando individualmente, garantindo que RLS
/// funcione corretamente mesmo com connection pooling e múltiplos SaveChanges.
///
/// Rastreia: design §6.1, DD-008, ADR-0001, TASK-15.
/// </summary>
internal sealed class TenantConnectionInterceptor : DbConnectionInterceptor, IDbCommandInterceptor
{
    private readonly Guid _tenantId;
    private readonly string _setTenantSql;

    // Evita recursão: o interceptor não processa o próprio SET command.
    [ThreadStatic]
    private static bool _isSettingTenant;

    public TenantConnectionInterceptor(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("tenantId não pode ser Empty (ADR-0001).", nameof(tenantId));
        }

        _tenantId = tenantId;
        _setTenantSql = $"SET app.current_tenant = '{_tenantId}'";
    }

    // =========================================================================
    // DbConnectionInterceptor — garante o SET na abertura da conexão
    // =========================================================================

    /// <inheritdoc />
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await SetTenantOnConnectionAsync(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    /// <inheritdoc />
    public override void ConnectionOpened(
        DbConnection connection,
        ConnectionEndEventData eventData)
    {
        SetTenantOnConnectionSync(connection);
        base.ConnectionOpened(connection, eventData);
    }

    // =========================================================================
    // IDbCommandInterceptor — garante o SET antes de cada comando EF Core
    // =========================================================================

    /// <inheritdoc />
    public InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        EnsureTenantSetSync(command.Connection);
        return result;
    }

    /// <inheritdoc />
    public ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantSetSync(command.Connection);
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        EnsureTenantSetSync(command.Connection);
        return result;
    }

    /// <inheritdoc />
    public ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantSetSync(command.Connection);
        return ValueTask.FromResult(result);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private async Task SetTenantOnConnectionAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        _isSettingTenant = true;
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = _setTenantSql;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _isSettingTenant = false;
        }
    }

    private void SetTenantOnConnectionSync(DbConnection connection)
    {
        _isSettingTenant = true;
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = _setTenantSql;
            cmd.ExecuteNonQuery();
        }
        finally
        {
            _isSettingTenant = false;
        }
    }

    private void EnsureTenantSetSync(DbConnection? connection)
    {
        if (_isSettingTenant || connection is null)
        {
            return;
        }
        SetTenantOnConnectionSync(connection);
    }
}
