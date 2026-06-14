using System.Data.Common;
using Digest.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Digest.Infrastructure.Security;

/// <summary>
/// EF Core <see cref="DbConnectionInterceptor"/> que executa
/// <c>SET app.current_tenant = @tenant_id</c> ao alugar a conexão do pool,
/// antes de qualquer comando (ADR-0001, DD-002, design §6.1).
///
/// Garante que a política RLS
/// <c>tenant_id = current_setting('app.current_tenant')::uuid</c>
/// seja satisfeita em cada conexão, fornecendo a segunda camada de defesa em profundidade
/// (a primeira é o Global Query Filter do <see cref="DigestDbContext"/>).
///
/// Se <see cref="ITenantContext.CurrentTenantId"/> for <see cref="Guid.Empty"/>,
/// o interceptor NÃO executa o SET — a conexão fica sem tenant, e o RLS
/// <c>FORCE ROW LEVEL SECURITY</c> retornará zero linhas (falha-fechada — RNF 1.4).
/// </summary>
public sealed class TenantConnectionInterceptor : DbConnectionInterceptor
{
    private readonly ITenantContext _tenantContext;

    /// <summary>
    /// Constrói o interceptor com o contexto de tenant injetado (Scoped).
    /// </summary>
    public TenantConnectionInterceptor(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    /// <inheritdoc/>
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await SetCurrentTenantAsync(connection, cancellationToken);
    }

    /// <inheritdoc/>
    public override void ConnectionOpened(
        DbConnection connection,
        ConnectionEndEventData eventData)
    {
        SetCurrentTenantSync(connection);
    }

    // ---------------------------------------------------------------
    // Implementações privadas
    // ---------------------------------------------------------------

    private async Task SetCurrentTenantAsync(DbConnection connection, CancellationToken ct)
    {
        var tenantId = _tenantContext.CurrentTenantId;
        if (tenantId == Guid.Empty)
            return; // sem tenant — falha-fechada pelo RLS

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SET app.current_tenant = '{tenantId}'";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private void SetCurrentTenantSync(DbConnection connection)
    {
        var tenantId = _tenantContext.CurrentTenantId;
        if (tenantId == Guid.Empty)
            return;

        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SET app.current_tenant = '{tenantId}'";
        cmd.ExecuteNonQuery();
    }
}
