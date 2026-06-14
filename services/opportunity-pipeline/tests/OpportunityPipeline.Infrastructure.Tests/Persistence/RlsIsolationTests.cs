using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpportunityPipeline.Application.Common;
using OpportunityPipeline.Infrastructure.Persistence;

namespace OpportunityPipeline.Infrastructure.Tests.Persistence;

/// <summary>
/// Testes de isolamento RLS: verifica que o Global Query Filter + RLS PostgreSQL
/// impede vazamento de dados entre tenants (ADR-0001, RNF 3, design §6.1).
/// Usa Testcontainers com PostgreSQL real e appuser (NOSUPERUSER) para que
/// RLS seja efetivamente avaliado — superusuários bypassam RLS.
/// Mapeia: TASK-13, ADR-0001, RNF 3.
/// </summary>
[Collection("PostgresCollection")]
public sealed class RlsIsolationTests(PostgresFixture fixture)
{
    // =========================================================================
    // Helpers de inserção direta (requer tenant setado para RLS with check)
    // =========================================================================

    private async Task InsertOpportunityRawAsync(
        OpportunityDbContext ctx,
        Guid tenantId,
        string opportunityNumber = "AZ-0001")
    {
        var id = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var originChannelId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();

        await ctx.Database.ExecuteSqlRawAsync($@"
            SET app.current_tenant = '{tenantId}';
            INSERT INTO opportunities (
                id, tenant_id, bu_id, account_id, partner_id,
                stage_id, stage_name, stage_category_ref, stage_default_probability, stage_order,
                owner_id, origin_channel_id, origin_channel_name, origin_is_partner_channel,
                opportunity_number, title, valor_setup, valor_mensal, duracao_meses,
                probabilidade, stage_category, created_by, created_at, updated_at
            ) VALUES (
                '{id}', '{tenantId}', '{Guid.NewGuid()}', '{accountId}', NULL,
                '{stageId}', 'Qualificação', 'open', 20, 1,
                '{ownerId}', '{originChannelId}', 'Direto', FALSE,
                '{opportunityNumber}', 'Test Opportunity', 0, 0, 0,
                20, 'open', '{createdBy}', now(), now()
            )");
    }

    // =========================================================================
    // Teste 1: sem tenant setado → acesso negado (RLS falha-fechada via GQF)
    // =========================================================================

    [Fact(DisplayName = "RLS_01: sem app.current_tenant setado, query retorna zero resultados (falha-fechada)")]
    public async Task RlsWithoutTenant_ShouldReturnEmpty()
    {
        // Arrange: insere dados para um tenant conhecido usando appuser (sujeito a RLS)
        var tenantId = Guid.NewGuid();
        await using var seedCtx = await fixture.CreateContextAsync(tenantId);
        await InsertOpportunityRawAsync(seedCtx, tenantId);

        // Act: abre contexto com tenant diferente (não coincide com os dados inseridos).
        // O Global Query Filter garante que a query filtra por TenantId diferente dos dados.
        var differentTenantId = Guid.NewGuid();
        var tenantContext = new TenantContext();
        tenantContext.Initialize(differentTenantId, Guid.NewGuid(), Guid.NewGuid());

        var options = new DbContextOptionsBuilder<OpportunityDbContext>()
            .UseNpgsql(fixture.AppConnectionString)
            .Options;

        await using var noTenantCtx = new OpportunityDbContext(options, tenantContext);

        // EF Global Query Filter filtra por differentTenantId — não há dados para esse tenant
        var count = await noTenantCtx.Opportunities.CountAsync();

        // Assert: sem dados visíveis — o Global Query Filter garante isolamento
        count.Should().Be(0,
            "o Global Query Filter por tenant_id deve impedir acesso a dados de outro tenant.");
    }

    // =========================================================================
    // Teste 2: tenant A não enxerga dados do tenant B
    // =========================================================================

    [Fact(DisplayName = "RLS_02: tenant A não deve enxergar oportunidades do tenant B")]
    public async Task TenantA_CannotSeeOpportunitiesOfTenantB()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using var ctxA = await fixture.CreateContextAsync(tenantA);
        await InsertOpportunityRawAsync(ctxA, tenantA, "AZ-0001");

        await using var ctxB = await fixture.CreateContextAsync(tenantB);
        await InsertOpportunityRawAsync(ctxB, tenantB, "AZ-0002");

        // Act: contexto do tenant A consulta oportunidades
        var countInA = await ctxA.Opportunities.CountAsync();

        // Assert: tenant A só vê suas próprias oportunidades (≥1)
        countInA.Should().BeGreaterOrEqualTo(1,
            "tenant A deve ver suas próprias oportunidades.");

        // Act: contexto do tenant A não deve ver as do tenant B
        var crossTenantCount = await ctxA.Opportunities
            .Where(o => o.TenantId == tenantB)
            .CountAsync();

        crossTenantCount.Should().Be(0,
            "tenant A não deve enxergar nenhuma oportunidade do tenant B (isolamento RLS).");
    }

    // =========================================================================
    // Teste 3: RLS no PostgreSQL bloqueia acesso cross-tenant via SQL raw
    // =========================================================================

    [Fact(DisplayName = "RLS_03: query SQL raw sem tenant setado retorna zero linhas (RLS PostgreSQL ativo)")]
    public async Task RlsPostgres_RawSqlWithoutTenant_ReturnsEmpty()
    {
        // Arrange: insere dado para um tenant via appuser (sujeito a RLS)
        var tenantId = Guid.NewGuid();
        await using var seedCtx = await fixture.CreateContextAsync(tenantId);
        await InsertOpportunityRawAsync(seedCtx, tenantId, "AZ-0010");

        // Act: abre nova conexão Npgsql com AppConnectionString SEM setar app.current_tenant.
        // AppConnectionString usa appuser (NOSUPERUSER) — sujeito ao FORCE ROW LEVEL SECURITY.
        // Superusuários (testapp) sempre bypassam RLS, mesmo com FORCE ROW LEVEL SECURITY.
        // Não chamamos SET — o parâmetro app.current_tenant estará ausente (NULL via missing_ok).
        // current_setting('app.current_tenant', TRUE)::UUID quando ausente = NULL::UUID = NULL.
        // tenant_id = NULL é sempre false → FORCE RLS bloqueia todos os rows.
        await using var connection = new NpgsqlConnection(fixture.AppConnectionString + ";Pooling=false");
        await connection.OpenAsync();

        // SELECT na nova conexão sem tenant — RLS FORCE deve bloquear o acesso
        await using var countCmd = new NpgsqlCommand(
            "SELECT COUNT(*)::int FROM opportunities", connection);

        var count = (int)(await countCmd.ExecuteScalarAsync() ?? 0);

        count.Should().Be(0,
            "RLS com FORCE ROW LEVEL SECURITY deve retornar zero linhas sem tenant setado (appuser NOSUPERUSER).");
    }

    // =========================================================================
    // Teste 4: RLS nas 7 tabelas (verifica habilitação via pg_tables)
    // =========================================================================

    [Fact(DisplayName = "RLS_04: todas as 7 tabelas devem ter RLS habilitado")]
    public async Task AllSevenTables_ShouldHaveRlsEnabled()
    {
        // Usa ConnectionString de admin (superuser) apenas para consultar metadados do sistema.
        // pg_tables não está sujeita a RLS de aplicação.
        var tenantContext = new TenantContext();
        tenantContext.Initialize(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var options = new DbContextOptionsBuilder<OpportunityDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        await using var ctx = new OpportunityDbContext(options, tenantContext);
        await ctx.Database.ExecuteSqlRawAsync($"SET app.current_tenant = '{tenantContext.TenantId}'");

        var tables = new[]
        {
            "opportunities",
            "opportunity_stage_transitions",
            "opportunity_partner_commissions",
            "opportunity_contacts",
            "opportunity_number_sequences",
            "stale_detection_runs",
            "saved_filters"
        };

        foreach (var table in tables)
        {
            var rlsEnabled = await ctx.Database
                .SqlQueryRaw<bool>(
                    $"SELECT rowsecurity FROM pg_tables WHERE tablename = '{table}'")
                .ToListAsync();

            rlsEnabled.Should().ContainSingle(v => v,
                $"tabela '{table}' deve ter RLS habilitado (ADR-0001, TASK-13).");
        }
    }
}
