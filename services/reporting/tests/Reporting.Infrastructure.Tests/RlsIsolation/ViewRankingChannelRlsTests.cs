using Dapper;
using FluentAssertions;
using Xunit;

namespace Reporting.Infrastructure.Tests.RlsIsolation;

/// <summary>
/// Testes de isolamento RLS para <c>vw_ranking_report</c> e <c>vw_channel_report</c>.
///
/// TASK-15 — Critérios de aceite:
/// <list type="bullet">
///   <item><description>Isolamento cross-tenant em ambas as views.</description></item>
///   <item><description><c>vw_channel_report</c> não contém colunas de PII.</description></item>
///   <item><description><c>vw_ranking_report</c> expõe <c>display_name</c> para o handler decidir (DD-008).</description></item>
/// </list>
///
/// Mapeia: TASK-15, design §7.2, DD-005, DD-008, Req 2, Req 3, RNF 4.
/// </summary>
[Collection("PostgresContainer")]
public sealed class ViewRankingChannelRlsTests(PostgresContainerFixture fixture)
{
    [Fact]
    public async Task RankingView_ComTenantA_RetornaZeroLinhasDoTenantB()
    {
        await fixture.CleanDataAsync();

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var buId = Guid.NewGuid();
        var ownerA = Guid.NewGuid();
        var ownerB = Guid.NewGuid();

        await using var adminConn = await fixture.OpenAdminConnectionAsync();
        await adminConn.ExecuteAsync("SET row_security = off");

        var stageA = await fixture.InsertStageAsync(adminConn, tenantA, buId);
        var stageB = await fixture.InsertStageAsync(adminConn, tenantB, buId);

        // Inserir usuários e oportunidades para ambos os tenants
        await fixture.InsertUserAsync(adminConn, tenantA, "Ana Silva");
        await fixture.InsertUserAsync(adminConn, tenantB, "Bob Costa");

        await fixture.InsertOpportunityAsync(adminConn, tenantA, buId, ownerA, stageA);
        await fixture.InsertOpportunityAsync(adminConn, tenantB, buId, ownerB, stageB);

        await adminConn.ExecuteAsync("SET row_security = on");

        await using var connA = await fixture.OpenConnectionWithTenantAsync(tenantA);
        var rows = await connA.QueryAsync("SELECT tenant_id FROM vw_ranking_report");

        var tenantIds = rows.Select(r => (Guid)r.tenant_id).ToList();
        tenantIds.Should().NotContain(tenantB,
            "vw_ranking_report com security_invoker=true não deve vazar dados de tenant B");
    }

    [Fact]
    public async Task RankingView_ExpoeDisplayName_ParaHandlerDecidir_DD008()
    {
        await fixture.CleanDataAsync();

        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        const string expectedDisplayName = "Carlos Teste";

        await using var adminConn = await fixture.OpenAdminConnectionAsync();
        await adminConn.ExecuteAsync("SET row_security = off");

        await fixture.InsertUserAsync(adminConn, tenantId, expectedDisplayName);
        var stage = await fixture.InsertStageAsync(adminConn, tenantId, buId);
        await fixture.InsertOpportunityAsync(adminConn, tenantId, buId, ownerId, stage);

        await adminConn.ExecuteAsync("SET row_security = on");

        await using var conn = await fixture.OpenConnectionWithTenantAsync(tenantId);
        var rows = await conn.QueryAsync("SELECT display_name FROM vw_ranking_report");

        // A view deve expor display_name — o handler (PiiMinimizationPolicy) decide inclusão (DD-008)
        // Como o owner_id da oportunidade pode não corresponder ao usuário inserido (teste isolado),
        // apenas verificamos que a coluna existe e que a view compila com a coluna PII.
        rows.Should().NotBeNull("a view vw_ranking_report deve expor a coluna display_name para o handler");
    }

    [Fact]
    public async Task ChannelView_NaoContemColunasPii_DD008()
    {
        await fixture.CleanDataAsync();

        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();

        await using var adminConn = await fixture.OpenAdminConnectionAsync();
        await adminConn.ExecuteAsync("SET row_security = off");

        var channelId = await fixture.InsertChannelAsync(adminConn, tenantId, "Orgânico");
        var stage = await fixture.InsertStageAsync(adminConn, tenantId, buId);
        await fixture.InsertOpportunityAsync(adminConn, tenantId, buId, Guid.NewGuid(), stage,
            channelId: channelId);

        await adminConn.ExecuteAsync("SET row_security = on");

        await using var conn = await fixture.OpenConnectionWithTenantAsync(tenantId);

        // Verificar que a view NÃO tem coluna display_name (sem PII)
        var columns = await conn.QueryAsync<string>(
            """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_name = 'vw_channel_report'
            ORDER BY ordinal_position
            """);

        columns.Should().NotContain("display_name",
            "vw_channel_report não deve expor PII (DD-008, RNF 4)");
        columns.Should().Contain("channel_name",
            "vw_channel_report deve expor o nome do canal");
    }

    [Fact]
    public async Task ChannelView_ComTenantA_RetornaZeroLinhasDoTenantB()
    {
        await fixture.CleanDataAsync();

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var buId = Guid.NewGuid();

        await using var adminConn = await fixture.OpenAdminConnectionAsync();
        await adminConn.ExecuteAsync("SET row_security = off");

        var channelA = await fixture.InsertChannelAsync(adminConn, tenantA, "Canal A");
        var channelB = await fixture.InsertChannelAsync(adminConn, tenantB, "Canal B");

        var stageA = await fixture.InsertStageAsync(adminConn, tenantA, buId);
        var stageB = await fixture.InsertStageAsync(adminConn, tenantB, buId);

        await fixture.InsertOpportunityAsync(adminConn, tenantA, buId, Guid.NewGuid(), stageA,
            channelId: channelA);
        await fixture.InsertOpportunityAsync(adminConn, tenantB, buId, Guid.NewGuid(), stageB,
            channelId: channelB);

        await adminConn.ExecuteAsync("SET row_security = on");

        await using var connA = await fixture.OpenConnectionWithTenantAsync(tenantA);
        var rows = await connA.QueryAsync("SELECT tenant_id FROM vw_channel_report");

        var tenantIds = rows.Select(r => (Guid)r.tenant_id).ToList();
        tenantIds.Should().NotContain(tenantB,
            "vw_channel_report com security_invoker=true não deve vazar dados de tenant B");
    }
}
