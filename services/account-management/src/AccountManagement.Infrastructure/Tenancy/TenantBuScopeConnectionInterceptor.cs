using System.Data.Common;
using AccountManagement.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AccountManagement.Infrastructure.Tenancy;

/// <summary>
/// Interceptor de conexão que seta as variáveis de sessão PostgreSQL de tenant e BU
/// no momento em que a conexão é aberta, antes de qualquer query.
///
/// Variáveis setadas por requisição:
/// - <c>app.current_tenant</c>  (uuid como text) — base do RLS de tenant (ADR-0001).
/// - <c>app.current_bu_scope</c> (lista de uuids separada por vírgula) — escopo de BU (ADR-0009).
/// - <c>app.bu_tenant_wide</c>  (bool como text) — bypass de BU para gestores (ADR-0009).
///
/// A combinação de RLS (PostgreSQL) + EF Global Query Filter constitui a defesa em
/// profundidade exigida pelo ADR-0001 e extendida pelo ADR-0009.
///
/// Fail-closed: se o contexto não estiver inicializado, as variáveis são setadas como
/// vazias/false, o que garante que nenhuma linha seja retornada pela policy RLS.
///
/// Mapeia: ADR-0001, ADR-0009, design §6.1, design §14.
/// </summary>
internal sealed class TenantBuScopeConnectionInterceptor : DbConnectionInterceptor
{
    private readonly InfrastructureTenantContext _tenantContext;
    private readonly InfrastructureBuScopeContext _buScopeContext;

    public TenantBuScopeConnectionInterceptor(
        InfrastructureTenantContext tenantContext,
        InfrastructureBuScopeContext buScopeContext)
    {
        _tenantContext = tenantContext;
        _buScopeContext = buScopeContext;
    }

    /// <inheritdoc />
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await SetSessionVariablesAsync(connection, cancellationToken);
    }

    /// <inheritdoc />
    public override void ConnectionOpened(
        DbConnection connection,
        ConnectionEndEventData eventData)
    {
        SetSessionVariablesAsync(connection, CancellationToken.None).GetAwaiter().GetResult();
    }

    private async Task SetSessionVariablesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        // Tenant (ADR-0001)
        var tenantId = _tenantContext.IsInitialized
            ? _tenantContext.TenantId.ToString()
            : string.Empty;

        // BU scope (ADR-0009)
        var buScope = _buScopeContext.IsInitialized && _buScopeContext.BuIds.Count > 0
            ? string.Join(",", _buScopeContext.BuIds)
            : string.Empty;

        var isTenantWide = _buScopeContext.IsInitialized && _buScopeContext.IsTenantWide
            ? "true"
            : "false";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            SET LOCAL "app.current_tenant" = '{tenantId}';
            SET LOCAL "app.current_bu_scope" = '{buScope}';
            SET LOCAL "app.bu_tenant_wide" = '{isTenantWide}';
            """;

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
