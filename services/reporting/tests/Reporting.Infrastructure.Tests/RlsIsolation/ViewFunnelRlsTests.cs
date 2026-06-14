using Dapper;
using FluentAssertions;
using Xunit;

namespace Reporting.Infrastructure.Tests.RlsIsolation;

/// <summary>
/// Testes de isolamento RLS para <c>vw_funnel_report</c>.
///
/// TASK-13 — Critério de aceite:
/// Com <c>app.current_tenant = A</c>, a view retorna zero linhas de tenant B.
/// Mapeia: TASK-13, design §7.2, ADR-0001, DD-005, Req 8.
/// </summary>
[Collection("PostgresContainer")]
public sealed class ViewFunnelRlsTests(PostgresContainerFixture fixture)
{
    [Fact]
    public async Task FunnelView_ComTenantA_RetornaZeroLinhasDoTenantB()
    {
        // Arrange
        await fixture.CleanDataAsync();

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var buId = Guid.NewGuid();

        // Inserir dados para ambos os tenants (sem RLS — usando conexão administrativa)
        await using var adminConn = await fixture.OpenAdminConnectionAsync();
        await adminConn.ExecuteAsync("SET row_security = off");

        var stageA = await fixture.InsertStageAsync(adminConn, tenantA, buId, "Estágio A");
        var stageB = await fixture.InsertStageAsync(adminConn, tenantB, buId, "Estágio B");

        await fixture.InsertOpportunityAsync(adminConn, tenantA, buId, Guid.NewGuid(), stageA, 100_00);
        await fixture.InsertOpportunityAsync(adminConn, tenantB, buId, Guid.NewGuid(), stageB, 200_00);

        await adminConn.ExecuteAsync("SET row_security = on");

        // Act — query com tenant A ativo
        await using var connA = await fixture.OpenConnectionWithTenantAsync(tenantA);
        var rows = await connA.QueryAsync(
            "SELECT tenant_id FROM vw_funnel_report");

        // Assert — zero linhas de tenant B
        var tenantIds = rows.Select(r => (Guid)r.tenant_id).ToList();
        tenantIds.Should().NotBeEmpty("deve haver linhas do tenant A");
        tenantIds.Should().AllSatisfy(id => id.Should().Be(tenantA,
            "a view não deve vazar dados do tenant B com security_invoker=true"));
        tenantIds.Should().NotContain(tenantB,
            "isolamento RLS: tenant B não deve ser visível para tenant A");
    }

    [Fact]
    public async Task FunnelView_SemTenantConfigurado_RetornaZeroLinhas_FalhaFechada()
    {
        // Arrange
        await fixture.CleanDataAsync();

        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();

        await using var adminConn = await fixture.OpenAdminConnectionAsync();
        await adminConn.ExecuteAsync("SET row_security = off");
        var stage = await fixture.InsertStageAsync(adminConn, tenantId, buId);
        await fixture.InsertOpportunityAsync(adminConn, tenantId, buId, Guid.NewGuid(), stage);
        await adminConn.ExecuteAsync("SET row_security = on");

        // Act — conexão SEM app.current_tenant (falha-fechada, ADR-0001)
        await using var connSemTenant = await fixture.OpenConnectionWithoutTenantAsync();
        var rows = await connSemTenant.QueryAsync("SELECT tenant_id FROM vw_funnel_report");

        // Assert — zero linhas (RLS com setting ausente → empty string → cast falha ou retorna false)
        rows.Should().BeEmpty(
            "sem app.current_tenant configurado, a RLS deve barrar todo acesso (falha-fechada)");
    }
}
