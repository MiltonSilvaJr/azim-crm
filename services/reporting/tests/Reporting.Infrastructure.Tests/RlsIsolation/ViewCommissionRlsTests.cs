using Dapper;
using FluentAssertions;
using Xunit;

namespace Reporting.Infrastructure.Tests.RlsIsolation;

/// <summary>
/// Testes de isolamento RLS para <c>vw_commission_report</c>.
///
/// TASK-16 — Critérios de aceite:
/// <list type="bullet">
///   <item><description><c>is_snapshot</c> exposto como booleano confiável.</description></item>
///   <item><description>Isolamento cross-tenant verificado.</description></item>
///   <item><description>View não realiza agregação — colunas brutas para o handler.</description></item>
/// </list>
///
/// Mapeia: TASK-16, design §7.2, DD-005, Req 4, Req 4.2, PBT-01, RN-007.
/// </summary>
[Collection("PostgresContainer")]
public sealed class ViewCommissionRlsTests(PostgresContainerFixture fixture)
{
    [Fact]
    public async Task CommissionView_ComTenantA_RetornaZeroLinhasDoTenantB()
    {
        await fixture.CleanDataAsync();

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var buId = Guid.NewGuid();

        await using var adminConn = await fixture.OpenAdminConnectionAsync();
        await adminConn.ExecuteAsync("SET row_security = off");

        // Tenant A: parceiro, oportunidade e comissão
        var partnerA = await fixture.InsertPartnerAsync(adminConn, tenantA, "Parceiro A");
        var stageA = await fixture.InsertStageAsync(adminConn, tenantA, buId);
        var oppA = await fixture.InsertOpportunityAsync(adminConn, tenantA, buId, Guid.NewGuid(), stageA,
            partnerId: partnerA);
        await fixture.InsertCommissionAsync(adminConn, tenantA, oppA, partnerA, 5000, false);

        // Tenant B: parceiro, oportunidade e comissão
        var partnerB = await fixture.InsertPartnerAsync(adminConn, tenantB, "Parceiro B");
        var stageB = await fixture.InsertStageAsync(adminConn, tenantB, buId);
        var oppB = await fixture.InsertOpportunityAsync(adminConn, tenantB, buId, Guid.NewGuid(), stageB,
            partnerId: partnerB);
        await fixture.InsertCommissionAsync(adminConn, tenantB, oppB, partnerB, 9999, true);

        await adminConn.ExecuteAsync("SET row_security = on");

        await using var connA = await fixture.OpenConnectionWithTenantAsync(tenantA);
        var rows = await connA.QueryAsync("SELECT tenant_id FROM vw_commission_report");

        var tenantIds = rows.Select(r => (Guid)r.tenant_id).ToList();
        tenantIds.Should().NotContain(tenantB,
            "vw_commission_report com security_invoker=true não deve vazar dados de tenant B");
        tenantIds.Should().AllSatisfy(id => id.Should().Be(tenantA));
    }

    [Fact]
    public async Task CommissionView_IsSnapshot_EBooleanoConfiavel_RN007()
    {
        await fixture.CleanDataAsync();

        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();

        await using var adminConn = await fixture.OpenAdminConnectionAsync();
        await adminConn.ExecuteAsync("SET row_security = off");

        var partner = await fixture.InsertPartnerAsync(adminConn, tenantId);
        var stage = await fixture.InsertStageAsync(adminConn, tenantId, buId);
        var opp = await fixture.InsertOpportunityAsync(adminConn, tenantId, buId, Guid.NewGuid(), stage,
            partnerId: partner);

        // Uma linha com is_snapshot=true e outra com is_snapshot=false
        await fixture.InsertCommissionAsync(adminConn, tenantId, opp, partner, 10000, isSnapshot: true);
        await fixture.InsertCommissionAsync(adminConn, tenantId, opp, partner, 5000, isSnapshot: false);

        await adminConn.ExecuteAsync("SET row_security = on");

        await using var conn = await fixture.OpenConnectionWithTenantAsync(tenantId);
        var rows = await conn.QueryAsync(
            "SELECT is_snapshot, commission_cents FROM vw_commission_report ORDER BY is_snapshot");

        var list = rows.ToList();
        list.Should().HaveCount(2, "a view deve retornar linhas brutas sem agregar");

        // is_snapshot deve ser boolean confiável (não string, não int)
        var withSnapshot = list.First(r => (bool)r.is_snapshot);
        var withoutSnapshot = list.First(r => !(bool)r.is_snapshot);

        ((long)withSnapshot.commission_cents).Should().Be(10000,
            "linha com is_snapshot=true deve ter commission_cents=10000 (consolidado)");
        ((long)withoutSnapshot.commission_cents).Should().Be(5000,
            "linha com is_snapshot=false deve ter commission_cents=5000 (projetado)");
    }

    [Fact]
    public async Task CommissionView_SemTenant_RetornaZeroLinhas_FalhaFechada()
    {
        await fixture.CleanDataAsync();

        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();

        await using var adminConn = await fixture.OpenAdminConnectionAsync();
        await adminConn.ExecuteAsync("SET row_security = off");

        var partner = await fixture.InsertPartnerAsync(adminConn, tenantId);
        var stage = await fixture.InsertStageAsync(adminConn, tenantId, buId);
        var opp = await fixture.InsertOpportunityAsync(adminConn, tenantId, buId, Guid.NewGuid(), stage,
            partnerId: partner);
        await fixture.InsertCommissionAsync(adminConn, tenantId, opp, partner);

        await adminConn.ExecuteAsync("SET row_security = on");

        await using var connSemTenant = await fixture.OpenConnectionWithoutTenantAsync();
        var rows = await connSemTenant.QueryAsync("SELECT tenant_id FROM vw_commission_report");

        rows.Should().BeEmpty("falha-fechada: sem app.current_tenant, RLS barra todo acesso");
    }
}
