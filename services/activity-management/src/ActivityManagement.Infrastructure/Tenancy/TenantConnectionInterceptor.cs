namespace ActivityManagement.Infrastructure.Tenancy;

using System.Data.Common;
using ActivityManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;

/// <summary>
/// Interceptor de conexão EF Core que executa <c>SET app.current_tenant = @tenantId</c>
/// ao alugar cada conexão do pool (design §6.1, DD-002, ADR-0001).
/// Primeira camada de defesa do isolamento multi-tenant: RLS no PostgreSQL compara
/// <c>tenant_id = current_setting('app.current_tenant')::uuid</c>.
/// Quando não há tenant setado (tenant nulo), a variável é esvaziada — RLS falha-fechada
/// retorna zero linhas (design §6.1: "falha-fechada").
/// Mapeia: design §6.1, §14, DD-002, ADR-0001, TASK-15.
/// </summary>
public sealed class TenantConnectionInterceptor : DbConnectionInterceptor
{
    private readonly ActivityManagementDbContext _dbContext;

    /// <summary>
    /// Inicializa o interceptor com o contexto que mantém o tenant corrente.
    /// </summary>
    /// <exception cref="ArgumentNullException">Quando <paramref name="dbContext"/> é nulo.</exception>
    public TenantConnectionInterceptor(ActivityManagementDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection              connection,
        ConnectionEventData       eventData,
        InterceptionResult        result,
        CancellationToken         cancellationToken = default)
    {
        // Deixa a conexão abrir normalmente; o SET é executado após abertura
        return result;
    }

    /// <inheritdoc />
    public override async Task ConnectionOpenedAsync(
        DbConnection          connection,
        ConnectionEndEventData eventData,
        CancellationToken     cancellationToken = default)
    {
        await SetCurrentTenantAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override void ConnectionOpened(
        DbConnection          connection,
        ConnectionEndEventData eventData)
    {
        SetCurrentTenantAsync(connection, CancellationToken.None).GetAwaiter().GetResult();
    }

    // ── Privado ──────────────────────────────────────────────────────────────

    private async Task SetCurrentTenantAsync(DbConnection connection, CancellationToken ct)
    {
        var tenantId = _dbContext.CurrentTenantId;

        // SET app.current_tenant ao valor do tenant corrente, ou '' para limpar
        // (RLS falha-fechada: sem tenant → zero linhas)
        var tenantValue = tenantId.HasValue
            ? tenantId.Value.ToString()
            : string.Empty;

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SET app.current_tenant = '{tenantValue}'";
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
