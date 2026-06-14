using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PartnerManagement.Application.Ports;

namespace PartnerManagement.Infrastructure.Tenancy;

/// <summary>
/// Interceptor de conexão EF Core que executa <c>SET app.current_tenant = '&lt;tenantId&gt;'</c>
/// antes de qualquer comando de negócio, ativando a política de RLS no PostgreSQL.
/// Segunda camada de defesa do isolamento multi-tenant (DD-001, ADR-0001):
/// a primeira é o filtro global EF Core (<c>HasQueryFilter</c>).
/// Mapeia: RNF 1, DD-001, ADR-0001, design §6.1, TASK-17.
/// </summary>
public sealed class RlsSessionInterceptor : DbConnectionInterceptor
{
    private readonly ITenantContext _tenantContext;

    /// <summary>
    /// Inicializa o interceptor com o contexto de tenant corrente.
    /// </summary>
    /// <param name="tenantContext">Contexto de tenant (scoped — um por requisição).</param>
    public RlsSessionInterceptor(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    /// <inheritdoc/>
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await SetCurrentTenantAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override void ConnectionOpened(
        DbConnection connection,
        ConnectionEndEventData eventData)
    {
        SetCurrentTenantAsync(connection, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Executa <c>SET app.current_tenant = '&lt;tenantId&gt;'</c> na sessão PostgreSQL.
    /// Quando <c>tenant_id</c> não está resolvido (Guid.Empty), o SET é omitido
    /// e a RLS rejeita todas as linhas (falha-fechada).
    /// </summary>
    private async Task SetCurrentTenantAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsResolved)
        {
            // Sem tenant resolvido: não define a variável de sessão.
            // A RLS impede leitura de qualquer linha (falha-fechada — ADR-0001).
            return;
        }

        // Credenciais nunca em log; apenas o tenantId (não-PII) é gravado na sessão.
        using DbCommand cmd = connection.CreateCommand();
        cmd.CommandText = $"SET app.current_tenant = '{_tenantContext.CurrentTenantId}'";
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
