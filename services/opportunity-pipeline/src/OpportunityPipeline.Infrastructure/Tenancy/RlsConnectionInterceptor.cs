using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using OpportunityPipeline.Application.Common;

namespace OpportunityPipeline.Infrastructure.Tenancy;

/// <summary>
/// Interceptor de conexão EF Core que executa SET app.current_tenant antes
/// de qualquer comando de negócio — camada 3 de isolamento (ADR-0001, DD-006).
/// RLS falha-fechada: sem tenant setado, PostgreSQL nega o acesso.
/// Mapeia: design §6.1, ADR-0001 camada 3, DD-006, TASK-16.
/// </summary>
public sealed class RlsConnectionInterceptor(
    TenantContext tenantContext,
    ILogger<RlsConnectionInterceptor> logger)
    : DbConnectionInterceptor
{
    /// <inheritdoc/>
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await SetTenantAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override void ConnectionOpened(
        DbConnection connection,
        ConnectionEndEventData eventData)
    {
        SetTenantSync(connection);
    }

    private async Task SetTenantAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;

        // Tenant não definido — RLS bloqueará o acesso (falha-fechada).
        // Não logar o tenant_id em mensagens de erro (segurança).
        if (tenantId == Guid.Empty)
        {
            logger.LogWarning(
                "RlsConnectionInterceptor: tenant_id não definido — RLS irá negar acesso (ADR-0001 falha-fechada).");
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"SET app.current_tenant = '{tenantId}'";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        logger.LogDebug("RlsConnectionInterceptor: app.current_tenant definido para conexão.");
    }

    private void SetTenantSync(DbConnection connection)
    {
        var tenantId = tenantContext.TenantId;

        if (tenantId == Guid.Empty)
        {
            logger.LogWarning(
                "RlsConnectionInterceptor: tenant_id não definido — RLS irá negar acesso (ADR-0001 falha-fechada).");
            return;
        }

        using var command = connection.CreateCommand();
        command.CommandText = $"SET app.current_tenant = '{tenantId}'";
        command.ExecuteNonQuery();

        logger.LogDebug("RlsConnectionInterceptor: app.current_tenant definido (síncrono).");
    }
}
