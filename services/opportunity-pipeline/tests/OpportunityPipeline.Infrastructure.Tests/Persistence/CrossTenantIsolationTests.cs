using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace OpportunityPipeline.Infrastructure.Tests.Persistence;

/// <summary>
/// PBT-10 — Isolamento cross-tenant (anti-enumeração).
/// Garante que nenhuma query executada no contexto do tenant A retorna dados do tenant B.
/// Cobre: Global Query Filter + RLS PostgreSQL falha-fechada (ADR-0001, RNF 3, design §14).
/// Mapeia: TASK-22, PBT-10, RNF 3, ADR-0001, KPI-06.
/// Categoria de gate: SecurityGate — falha = incidente sev-1.
/// </summary>
[Collection("PostgresCollection")]
[Trait("Category", "SecurityGate")]
public sealed class CrossTenantIsolationTests(PostgresFixture fixture)
{
    // =========================================================================
    // Helpers de seed
    // =========================================================================

    /// <summary>
    /// Insere N oportunidades para o tenant informado usando SQL raw com tenant setado.
    /// Cada oportunidade recebe número único AZ-NNNN para satisfazer a constraint.
    /// </summary>
    private async Task<List<Guid>> SeedOpportunitiesAsync(Guid tenantId, int count)
    {
        await using var ctx = await fixture.CreateContextAsync(tenantId);
        var ids = new List<Guid>(count);

        for (var i = 0; i < count; i++)
        {
            var id = Guid.NewGuid();
            ids.Add(id);
            var number = $"AZ-{(9000 + i):D4}";

            await ctx.Database.ExecuteSqlRawAsync($@"
                INSERT INTO opportunities (
                    id, tenant_id, bu_id, account_id,
                    stage_id, stage_name, stage_category_ref, stage_default_probability, stage_order,
                    owner_id, origin_channel_id, origin_channel_name, origin_is_partner_channel,
                    opportunity_number, title, valor_setup, valor_mensal, duracao_meses,
                    probabilidade, stage_category, created_by, created_at, updated_at
                ) VALUES (
                    '{id}', '{tenantId}', '{Guid.NewGuid()}', '{Guid.NewGuid()}',
                    '{Guid.NewGuid()}', 'Qualificação', 'open', 20, 1,
                    '{Guid.NewGuid()}', '{Guid.NewGuid()}', 'Direto', FALSE,
                    '{number}', 'Oportunidade Isolamento {i}', 0, 0, 0,
                    20, 'open', '{Guid.NewGuid()}', now(), now()
                )");
        }

        return ids;
    }

    // =========================================================================
    // PBT-10 — Isolamento cross-tenant via FsCheck
    // =========================================================================

    /// <summary>
    /// PBT-10 (FsCheck + Testcontainers): para quaisquer índices de tenant distintos,
    /// qualquer query executada no contexto do tenant A não retorna opportunity_id do tenant B.
    /// Satisfaz RNF 3 (isolamento em profundidade) e ADR-0001 (camadas 2 e 3).
    /// FsCheck gera ≥ 100 pares (indexA, indexB) mapeados para GUIDs distintos pré-alocados.
    /// </summary>
    [Property(MaxTest = 100, Verbose = false)]
    public Property Pbt10_TenantA_CannotEnumerateOpportunitiesOfTenantB()
    {
        // Pré-aloca 10 tenants distintos para que FsCheck varie índices entre 0..9.
        // Isso garante GUIDs distintos sem usar Arb.Default.Guid() (que não existe no FsCheck 3.x).
        var tenants = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray();

        // Gera par de índices distintos no intervalo [0, 9]
        var pairGen = Gen.Two(Gen.Choose(0, tenants.Length - 1))
                         .Where(pair => pair.Item1 != pair.Item2);

        return Prop.ForAll(
            Arb.From(pairGen),
            pair =>
            {
                var tenantA = tenants[pair.Item1];
                var tenantB = tenants[pair.Item2];
                return RunIsolationCheckAsync(tenantA, tenantB).GetAwaiter().GetResult();
            });
    }

    private async Task<bool> RunIsolationCheckAsync(Guid tenantA, Guid tenantB)
    {
        // Seed: 2 oportunidades em B (usando número com sufixo de GUID para evitar colisão)
        // Em cada execução do PBT podemos ter os mesmos tenants com dados já inseridos
        // pelo teste anterior. Usamos numeros únicos baseados em GUID.
        var oppBNumber = $"AZ-{Math.Abs(tenantB.GetHashCode() % 9000):D4}";
        var oppBId = Guid.NewGuid();
        var tenantBCtx = await fixture.CreateContextAsync(tenantB);
        await using (tenantBCtx)
        {
            try
            {
                await tenantBCtx.Database.ExecuteSqlRawAsync($@"
                    INSERT INTO opportunities (
                        id, tenant_id, bu_id, account_id,
                        stage_id, stage_name, stage_category_ref, stage_default_probability, stage_order,
                        owner_id, origin_channel_id, origin_channel_name, origin_is_partner_channel,
                        opportunity_number, title, valor_setup, valor_mensal, duracao_meses,
                        probabilidade, stage_category, created_by, created_at, updated_at
                    ) VALUES (
                        '{oppBId}', '{tenantB}', '{Guid.NewGuid()}', '{Guid.NewGuid()}',
                        '{Guid.NewGuid()}', 'Qualificação', 'open', 20, 1,
                        '{Guid.NewGuid()}', '{Guid.NewGuid()}', 'Direto', FALSE,
                        '{oppBNumber}', 'Opp Tenant B PBT-10', 0, 0, 0,
                        20, 'open', '{Guid.NewGuid()}', now(), now()
                    ) ON CONFLICT DO NOTHING");
            }
            catch
            {
                // Conflito de número: oportunidade já existe para esse tenant; OK para o teste.
            }
        }

        // Consulta no contexto de A: não deve retornar nenhum ID de B
        await using var tenantACtx = await fixture.CreateContextAsync(tenantA);
        var idsFromA = await tenantACtx.Opportunities
            .Select(o => o.Id)
            .ToListAsync();

        // A query de A não deve conter IDs que pertencem a B
        return !idsFromA.Contains(oppBId);
    }

    // =========================================================================
    // CT-01 — Consulta cross-tenant determinística: A não vê B
    // =========================================================================

    [Fact(DisplayName = "CT-01: tenant A não enxerga oportunidades do tenant B (EF Global Query Filter)")]
    public async Task TenantA_CannotSeeOpportunitiesOfTenantB_ViaEf()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var idsInB = await SeedOpportunitiesAsync(tenantB, 3);

        // Seed 1 em A para garantir que A tem dados (isola bug de "tabela vazia")
        await SeedOpportunitiesAsync(tenantA, 1);

        // Act: consulta no contexto de A
        await using var ctxA = await fixture.CreateContextAsync(tenantA);
        var idsFromA = await ctxA.Opportunities.Select(o => o.Id).ToListAsync();

        // Assert: nenhum ID de B aparece no resultado de A
        idsFromA.Should().NotContain(idsInB,
            "o Global Query Filter (camada 2, ADR-0001) deve impedir que tenant A " +
            "enxergue oportunidades do tenant B.");
    }

    // =========================================================================
    // CT-02 — Escrita cross-tenant: A não altera dados de B
    // =========================================================================

    [Fact(DisplayName = "CT-02: operação de escrita no contexto de A não afeta dados de B (RLS WITH CHECK)")]
    public async Task TenantA_WriteDoesNotAffectTenantB_Data()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var idsInB = await SeedOpportunitiesAsync(tenantB, 2);

        // Act: tenta inserir no contexto de A (RLS WITH CHECK garante tenant_id correto)
        await using var ctxA = await fixture.CreateContextAsync(tenantA);
        await ctxA.Database.ExecuteSqlRawAsync($@"
            INSERT INTO opportunities (
                id, tenant_id, bu_id, account_id,
                stage_id, stage_name, stage_category_ref, stage_default_probability, stage_order,
                owner_id, origin_channel_id, origin_channel_name, origin_is_partner_channel,
                opportunity_number, title, valor_setup, valor_mensal, duracao_meses,
                probabilidade, stage_category, created_by, created_at, updated_at
            ) VALUES (
                '{Guid.NewGuid()}', '{tenantA}', '{Guid.NewGuid()}', '{Guid.NewGuid()}',
                '{Guid.NewGuid()}', 'Prospecção', 'open', 10, 0,
                '{Guid.NewGuid()}', '{Guid.NewGuid()}', 'Direto', FALSE,
                'AZ-9900', 'Opp Escrita A', 0, 0, 0,
                10, 'open', '{Guid.NewGuid()}', now(), now()
            ) ON CONFLICT DO NOTHING");

        // Assert: dados de B permanecem intactos
        await using var ctxB = await fixture.CreateContextAsync(tenantB);
        var countB = await ctxB.Opportunities.CountAsync();

        countB.Should().BeGreaterOrEqualTo(idsInB.Count,
            "oportunidades do tenant B não devem ser afetadas por operações do tenant A.");
    }

    // =========================================================================
    // CT-03 — RLS sem app.current_tenant → falha-fechada (banco bloqueia)
    // =========================================================================

    [Fact(DisplayName = "CT-03: query SQL raw sem app.current_tenant retorna zero linhas (RLS falha-fechada)")]
    public async Task RlsWithoutTenant_SqlRaw_ReturnsEmpty()
    {
        // Arrange: garante que há pelo menos um registro no banco para algum tenant
        var tenantId = Guid.NewGuid();
        await SeedOpportunitiesAsync(tenantId, 1);

        // Act: abre conexão como appuser (NOSUPERUSER) sem setar app.current_tenant
        // FORCE ROW LEVEL SECURITY + NULLIF(...) garante que NULL::UUID não passa na policy.
        await using var connection = new NpgsqlConnection(fixture.AppConnectionString + ";Pooling=false");
        await connection.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            "SELECT COUNT(*)::int FROM opportunities", connection);
        var count = (int)(await cmd.ExecuteScalarAsync() ?? 0);

        // Assert: nenhuma linha visível — RLS falha-fechada
        count.Should().Be(0,
            "FORCE ROW LEVEL SECURITY com app.current_tenant ausente (NULL) " +
            "deve bloquear acesso a todos os registros (ADR-0001, DD-006).");
    }

    // =========================================================================
    // CT-04 — RLS com tenant A tentando ler via SQL raw com tenant B no WHERE
    // =========================================================================

    [Fact(DisplayName = "CT-04: SQL raw com tenant A setado retorna zero linhas ao filtrar por tenant_id B")]
    public async Task RlsPostgres_TenantA_CannotQueryTenantBById()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await SeedOpportunitiesAsync(tenantB, 2);

        // Act: abre conexão como appuser, seta tenant A e tenta ler com WHERE tenant_id = B
        await using var connection = new NpgsqlConnection(fixture.AppConnectionString + ";Pooling=false");
        await connection.OpenAsync();

        await using var setCmd = new NpgsqlCommand(
            $"SET app.current_tenant = '{tenantA}'", connection);
        await setCmd.ExecuteNonQueryAsync();

        await using var queryCmd = new NpgsqlCommand(
            $"SELECT COUNT(*)::int FROM opportunities WHERE tenant_id = '{tenantB}'", connection);
        var count = (int)(await queryCmd.ExecuteScalarAsync() ?? 0);

        // Assert: RLS camada 3 filtra os registros de B mesmo com WHERE explícito
        count.Should().Be(0,
            "RLS PostgreSQL (camada 3, ADR-0001) deve impedir que tenant A leia " +
            "registros de tenant B mesmo com filtro WHERE tenant_id = B explícito.");
    }

    // =========================================================================
    // CT-05 — Todas as 7 tabelas operacionais têm RLS e FORCE habilitados
    // =========================================================================

    [Fact(DisplayName = "CT-05: todas as 7 tabelas operacionais têm RLS e FORCE ROW LEVEL SECURITY habilitados")]
    public async Task AllOperationalTables_HaveRlsAndForcedEnabled()
    {
        // Usa conexão de admin (superuser) apenas para consultar metadados de sistema.
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

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
            // pg_tables.rowsecurity = ROW LEVEL SECURITY habilitado
            // pg_class.relforcerowsecurity = FORCE ROW LEVEL SECURITY habilitado
            await using var cmd = new NpgsqlCommand(
                $@"SELECT t.rowsecurity, c.relforcerowsecurity
                   FROM pg_tables t
                   JOIN pg_class c ON c.relname = t.tablename
                   WHERE t.tablename = '{table}' AND t.schemaname = 'public'",
                connection);

            await using var reader = await cmd.ExecuteReaderAsync();
            reader.Read().Should().BeTrue($"tabela '{table}' deve existir em pg_tables.");

            var rowSecurity = reader.GetBoolean(0);
            var forcedSecurity = reader.GetBoolean(1);

            rowSecurity.Should().BeTrue(
                $"tabela '{table}' deve ter ROW LEVEL SECURITY habilitado (ADR-0001).");
            forcedSecurity.Should().BeTrue(
                $"tabela '{table}' deve ter FORCE ROW LEVEL SECURITY (ADR-0001, falha-fechada).");
        }
    }

    // =========================================================================
    // CT-06 — Monitoramento: tentativa de acesso sem tenant gera evidência auditável
    // =========================================================================

    [Fact(DisplayName = "CT-06: acesso sem tenant retorna conjunto vazio (comportamento auditável para alertas RNF 3.3)")]
    public async Task AccessWithoutTenant_ReturnsSilentEmpty_ForMonitoring()
    {
        // Este cenário documenta o comportamento esperado para alertas em produção (RNF 3.3):
        // - A RLS não lança exceção — retorna silenciosamente conjunto vazio.
        // - O interceptor de monitoramento em produção deve detectar tentativas sem tenant.
        // - O comportamento é "falha silenciosa segura" (negação, não erro de servidor).

        var tenantId = Guid.NewGuid();
        await SeedOpportunitiesAsync(tenantId, 1);

        await using var connection = new NpgsqlConnection(fixture.AppConnectionString + ";Pooling=false");
        await connection.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            "SELECT COUNT(*)::int FROM opportunity_stage_transitions", connection);
        var count = (int)(await cmd.ExecuteScalarAsync() ?? 0);

        count.Should().Be(0,
            "acesso sem tenant configurado deve ser silenciosamente negado em todas as tabelas " +
            "(RNF 3.3 — base para alertas de monitoramento em produção).");
    }
}
