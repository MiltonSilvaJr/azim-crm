using Dapper;
using FluentAssertions;
using Xunit;

namespace Reporting.Infrastructure.Tests.RlsIsolation;

/// <summary>
/// Testes de isolamento RLS para <c>vw_forecast_report</c>.
///
/// TASK-14 — Critérios de aceite:
/// <list type="bullet">
///   <item><description>BU sem meta retorna linha com <c>goal_cents = NULL</c> (degradação graciosa, Req 6.3).</description></item>
///   <item><description>Com <c>app.current_tenant = A</c>, zero linhas de tenant B.</description></item>
/// </list>
///
/// Mapeia: TASK-14, design §7.2, DD-005, Req 6, Req 6.3.
/// </summary>
[Collection("PostgresContainer")]
public sealed class ViewForecastRlsTests(PostgresContainerFixture fixture)
{
    [Fact]
    public async Task ForecastView_ComTenantA_RetornaZeroLinhasDoTenantB()
    {
        await fixture.CleanDataAsync();

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var buId = Guid.NewGuid();

        await using var adminConn = await fixture.OpenAdminConnectionAsync();
        await adminConn.ExecuteAsync("SET row_security = off");

        var stageA = await fixture.InsertStageAsync(adminConn, tenantA, buId);
        var stageB = await fixture.InsertStageAsync(adminConn, tenantB, buId);

        await fixture.InsertOpportunityAsync(adminConn, tenantA, buId, Guid.NewGuid(), stageA,
            closedAt: DateTimeOffset.UtcNow);
        await fixture.InsertOpportunityAsync(adminConn, tenantB, buId, Guid.NewGuid(), stageB,
            closedAt: DateTimeOffset.UtcNow);

        await adminConn.ExecuteAsync("SET row_security = on");

        await using var connA = await fixture.OpenConnectionWithTenantAsync(tenantA);
        var rows = await connA.QueryAsync("SELECT tenant_id FROM vw_forecast_report");

        var tenantIds = rows.Select(r => (Guid)r.tenant_id).ToList();
        tenantIds.Should().NotContain(tenantB,
            "vw_forecast_report com security_invoker=true não deve vazar dados de tenant B");
    }

    [Fact]
    public async Task ForecastView_BuSemMeta_RetornaGoalCentsNulo_DegradacaoGraciosa()
    {
        await fixture.CleanDataAsync();

        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();

        await using var adminConn = await fixture.OpenAdminConnectionAsync();
        await adminConn.ExecuteAsync("SET row_security = off");

        var stage = await fixture.InsertStageAsync(adminConn, tenantId, buId);
        await fixture.InsertOpportunityAsync(adminConn, tenantId, buId, Guid.NewGuid(), stage,
            closedAt: DateTimeOffset.UtcNow);
        // Sem inserção de goal — deve retornar null (LEFT JOIN, Req 6.3)

        await adminConn.ExecuteAsync("SET row_security = on");

        await using var conn = await fixture.OpenConnectionWithTenantAsync(tenantId);
        var rows = await conn.QueryAsync("SELECT goal_cents FROM vw_forecast_report");

        rows.Should().NotBeEmpty("deve haver linhas mesmo sem meta");
        foreach (var row in rows)
        {
            ((object?)row.goal_cents).Should().BeNull(
                "LEFT JOIN goals deve retornar NULL quando não há meta cadastrada (Req 6.3)");
        }
    }

    [Fact]
    public async Task ForecastView_SemTenant_RetornaZeroLinhas_FalhaFechada()
    {
        await fixture.CleanDataAsync();

        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();

        await using var adminConn = await fixture.OpenAdminConnectionAsync();
        await adminConn.ExecuteAsync("SET row_security = off");
        var stage = await fixture.InsertStageAsync(adminConn, tenantId, buId);
        await fixture.InsertOpportunityAsync(adminConn, tenantId, buId, Guid.NewGuid(), stage,
            closedAt: DateTimeOffset.UtcNow);
        await adminConn.ExecuteAsync("SET row_security = on");

        await using var connSemTenant = await fixture.OpenConnectionWithoutTenantAsync();
        var rows = await connSemTenant.QueryAsync("SELECT tenant_id FROM vw_forecast_report");

        rows.Should().BeEmpty("falha-fechada: sem tenant, a RLS barra todo acesso");
    }
}
