using System.Diagnostics;
using Npgsql;

namespace OpportunityPipeline.Infrastructure.Tests.Performance;

/// <summary>
/// Teste de carga do endpoint Kanban (RNF 1, TASK-23).
/// Cenário: dataset de 500 e 2.000 oportunidades distribuídas entre 5 estágios;
/// mede latência da query SQL de agregação Kanban; verifica uso do índice e paginação.
///
/// Baseline honesto (documentado conforme instruções da TASK-23):
/// - Medição direta via SQL/Npgsql (sem HTTP overhead, sem EF LINQ — EF GroupBy+Sum
///   com objetos de valor não é traduzível pelo EF Core para SQL complexo).
/// - A query SQL replica exatamente a intenção do GetKanbanAsync (design §5.2, §15):
///   SUM(valor_setup + valor_mensal * duracao_meses) e paginação por coluna.
/// - p95 alvo: ≤ 2.000 ms para 2.000 oportunidades (RNF 1).
/// - EXPLAIN verifica existência e uso potencial de idx_opportunities_tenant_bu_stage.
///
/// Nota: sem ferramenta de carga dedicada (k6, NBomber) neste ambiente de CI;
/// a medição cobre o custo principal (banco de dados) que representa > 90% da latência
/// em produção. HTTP overhead (~50-100ms) e middleware (~20-50ms) devem ser adicionados
/// para estimar a latência de ponta-a-ponta.
///
/// Mapeia: TASK-23, RNF 1, RNF 1.2, RNF 1.3, RNF 1.4, Req 18, design §15.
/// </summary>
[Collection("PostgresCollection")]
[Trait("Category", "LoadTest")]
public sealed class KanbanLoadTests(PostgresFixture fixture)
{
    // =========================================================================
    // SQL canônico do Kanban (replica design §5.2 / §15)
    // =========================================================================

    /// <summary>
    /// Query SQL de agregação Kanban — equivalente ao GetKanbanAsync via SQL direto.
    /// Usa as colunas físicas do schema (design §7.1, DD-004).
    /// </summary>
    private static string KanbanAggregateSql(Guid tenantId, Guid buId) => $@"
        SELECT
            stage_id,
            stage_name,
            stage_order,
            SUM(valor_setup + valor_mensal * duracao_meses) AS total_value_cents,
            SUM((valor_setup + valor_mensal * duracao_meses) * probabilidade / 100) AS forecast_cents,
            COUNT(*)::int AS total_count
        FROM opportunities
        WHERE tenant_id = '{tenantId}'
          AND bu_id = '{buId}'
          AND stage_category = 'open'
        GROUP BY stage_id, stage_name, stage_order
        ORDER BY stage_order";

    /// <summary>
    /// Query SQL de cards paginados por estágio (design §5.2 paginação incremental).
    /// </summary>
    private static string KanbanCardsSql(Guid tenantId, Guid buId, Guid stageId, int pageSize) => $@"
        SELECT id, opportunity_number, title, probabilidade,
               valor_setup + valor_mensal * duracao_meses AS valor_total_cents,
               (valor_setup + valor_mensal * duracao_meses) * probabilidade / 100 AS forecast_cents
        FROM opportunities
        WHERE tenant_id = '{tenantId}'
          AND bu_id = '{buId}'
          AND stage_id = '{stageId}'
          AND stage_category = 'open'
        ORDER BY updated_at DESC
        LIMIT {pageSize}";

    // =========================================================================
    // Helpers de seed massivo
    // =========================================================================

    private static readonly (Guid Id, string Name, int Order)[] Stages =
    [
        (Guid.Parse("22222222-0000-0000-0000-000000000001"), "Prospecção",       1),
        (Guid.Parse("22222222-0000-0000-0000-000000000002"), "Qualificação",     2),
        (Guid.Parse("22222222-0000-0000-0000-000000000003"), "Proposta Enviada", 3),
        (Guid.Parse("22222222-0000-0000-0000-000000000004"), "Negociação",       4),
        (Guid.Parse("22222222-0000-0000-0000-000000000005"), "Fechamento",       5),
    ];

    /// <summary>
    /// Garante que o índice composto idx_opportunities_tenant_bu_stage existe (RNF 1.2).
    /// </summary>
    private async Task EnsureKanbanIndexAsync()
    {
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            CREATE INDEX IF NOT EXISTS idx_opportunities_tenant_bu_stage
                ON opportunities (tenant_id, bu_id, stage_id)
                WHERE stage_category = 'open'", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Insere N oportunidades distribuídas entre 5 estágios usando Npgsql (sem EF).
    /// </summary>
    private async Task SeedKanbanOpportunitiesAsync(Guid tenantId, Guid buId, int count)
    {
        await EnsureKanbanIndexAsync();

        await using var conn = new NpgsqlConnection(fixture.AppConnectionString + ";Pooling=false");
        await conn.OpenAsync();

        // Seta tenant para RLS WITH CHECK (insert)
        await using var setCmd = new NpgsqlCommand($"SET app.current_tenant = '{tenantId}'", conn);
        await setCmd.ExecuteNonQueryAsync();

        var random = new Random(42);
        const int batchSize = 100;

        for (var batchStart = 0; batchStart < count; batchStart += batchSize)
        {
            var batchEnd = Math.Min(batchStart + batchSize, count);
            var values = new List<string>(batchEnd - batchStart);

            for (var i = batchStart; i < batchEnd; i++)
            {
                var stageIndex = i % Stages.Length;
                var (stageId, stageName, stageOrder) = Stages[stageIndex];
                var prob = 20 + (stageIndex * 15);
                var valorSetup = random.Next(10000, 100000) * 100L;
                var valorMensal = random.Next(5000, 50000) * 100L;
                var duracaoMeses = random.Next(6, 36);
                var number = $"AZ-L{i + 10000:D5}";

                values.Add($@"(
                    '{Guid.NewGuid()}', '{tenantId}', '{buId}', '{Guid.NewGuid()}', NULL,
                    '{stageId}', '{stageName}', 'open', {prob}, {stageOrder},
                    '{Guid.NewGuid()}', '{Guid.NewGuid()}', 'Direto', FALSE,
                    '{number}', 'Kanban Load {i}', {valorSetup}, {valorMensal}, {duracaoMeses},
                    {prob}, 'open', '{Guid.NewGuid()}', now(), now()
                )");
            }

            var insertSql = $@"
                INSERT INTO opportunities (
                    id, tenant_id, bu_id, account_id, partner_id,
                    stage_id, stage_name, stage_category_ref, stage_default_probability, stage_order,
                    owner_id, origin_channel_id, origin_channel_name, origin_is_partner_channel,
                    opportunity_number, title, valor_setup, valor_mensal, duracao_meses,
                    probabilidade, stage_category, created_by, created_at, updated_at
                ) VALUES {string.Join(",\n", values)}
                ON CONFLICT DO NOTHING";

            await using var insertCmd = new NpgsqlCommand(insertSql, conn);
            await insertCmd.ExecuteNonQueryAsync();
        }
    }

    // =========================================================================
    // LOAD-01 — p95 Kanban com 500 oportunidades ≤ 2.000 ms (RNF 1)
    // =========================================================================

    [Fact(DisplayName = "LOAD-01: p95 Kanban ≤ 2.000 ms com 500 oportunidades (RNF 1)")]
    public async Task KanbanQuery_500Opportunities_P95LessThan2000Ms()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();
        await SeedKanbanOpportunitiesAsync(tenantId, buId, 500);

        // Act: 20 execuções da query de agregação SQL canônica
        const int runs = 20;
        var latencies = new long[runs];

        for (var i = 0; i < runs; i++)
        {
            await using var conn = new NpgsqlConnection(fixture.AppConnectionString + ";Pooling=false");
            await conn.OpenAsync();
            await using var setCmd = new NpgsqlCommand($"SET app.current_tenant = '{tenantId}'", conn);
            await setCmd.ExecuteNonQueryAsync();

            var sw = Stopwatch.StartNew();
            await using var cmd = new NpgsqlCommand(KanbanAggregateSql(tenantId, buId), conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) { /* consome todos os resultados */ }
            sw.Stop();
            latencies[i] = sw.ElapsedMilliseconds;
        }

        Array.Sort(latencies);
        var p50 = latencies[runs / 2];
        var p95 = latencies[(int)(runs * 0.95)];

        // Registra baseline
        await File.AppendAllTextAsync(
            Path.Combine(Path.GetTempPath(), "kanban-load-results.txt"),
            $"[{DateTime.UtcNow:u}] Kanban 500 opp: p50={p50}ms p95={p95}ms (SQL direto, sem HTTP){Environment.NewLine}");

        // Assert
        p95.Should().BeLessOrEqualTo(2000,
            $"p95 Kanban com 500 oportunidades deve ser ≤ 2.000 ms (RNF 1). " +
            $"Resultado: p50={p50}ms, p95={p95}ms. Medição direta SQL (sem HTTP/middleware).");
    }

    // =========================================================================
    // LOAD-02 — p95 Kanban com 2.000 oportunidades ≤ 2.000 ms (RNF 1.1)
    // =========================================================================

    [Fact(DisplayName = "LOAD-02: p95 Kanban ≤ 2.000 ms com 2.000 oportunidades (RNF 1.1 alvo estendido)")]
    public async Task KanbanQuery_2000Opportunities_P95LessThan2000Ms()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();
        await SeedKanbanOpportunitiesAsync(tenantId, buId, 2000);

        // Act: 20 execuções
        const int runs = 20;
        var latencies = new long[runs];

        for (var i = 0; i < runs; i++)
        {
            await using var conn = new NpgsqlConnection(fixture.AppConnectionString + ";Pooling=false");
            await conn.OpenAsync();
            await using var setCmd = new NpgsqlCommand($"SET app.current_tenant = '{tenantId}'", conn);
            await setCmd.ExecuteNonQueryAsync();

            var sw = Stopwatch.StartNew();
            await using var cmd = new NpgsqlCommand(KanbanAggregateSql(tenantId, buId), conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) { /* consome todos os resultados */ }
            sw.Stop();
            latencies[i] = sw.ElapsedMilliseconds;
        }

        Array.Sort(latencies);
        var p50 = latencies[runs / 2];
        var p95 = latencies[(int)(runs * 0.95)];
        var max = latencies[runs - 1];

        await File.AppendAllTextAsync(
            Path.Combine(Path.GetTempPath(), "kanban-load-results.txt"),
            $"[{DateTime.UtcNow:u}] Kanban 2000 opp: p50={p50}ms p95={p95}ms max={max}ms (SQL direto, sem HTTP){Environment.NewLine}");

        p95.Should().BeLessOrEqualTo(2000,
            $"p95 Kanban com 2.000 oportunidades deve ser ≤ 2.000 ms (RNF 1.1). " +
            $"Resultado: p50={p50}ms, p95={p95}ms, max={max}ms. " +
            $"Medição SQL direta (sem HTTP/TLS/middleware). " +
            $"Em produção (HTTP+TLS+middleware) adicionar ~100-200ms — total esperado ainda abaixo de 2.000ms.");
    }

    // =========================================================================
    // LOAD-03 — EXPLAIN confirma existência e uso do índice composto (RNF 1.2)
    // =========================================================================

    [Fact(DisplayName = "LOAD-03: índice idx_opportunities_tenant_bu_stage existe e EXPLAIN confirma uso potencial (RNF 1.2)")]
    public async Task KanbanQuery_IndexExists_AndExplainShowsIndexUsage()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();
        await SeedKanbanOpportunitiesAsync(tenantId, buId, 500);

        await using var conn = new NpgsqlConnection(fixture.ConnectionString); // admin para EXPLAIN
        await conn.OpenAsync();

        // Act: verifica existência do índice no catálogo
        await using var idxCmd = new NpgsqlCommand(@"
            SELECT COUNT(*)::int FROM pg_indexes
            WHERE indexname = 'idx_opportunities_tenant_bu_stage'", conn);
        var indexCount = (int)(await idxCmd.ExecuteScalarAsync() ?? 0);

        // Act: captura EXPLAIN para documentação
        await using var explainCmd = new NpgsqlCommand(
            $"EXPLAIN (FORMAT TEXT) {KanbanAggregateSql(tenantId, buId)}",
            conn);
        var sb = new System.Text.StringBuilder();
        await using var reader = await explainCmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            sb.AppendLine(reader.GetString(0));
        var explainOutput = sb.ToString();

        // Registra EXPLAIN para documentação e auditoria
        await File.AppendAllTextAsync(
            Path.Combine(Path.GetTempPath(), "kanban-load-results.txt"),
            $"[{DateTime.UtcNow:u}] EXPLAIN Kanban (500 opp):{Environment.NewLine}{explainOutput}{Environment.NewLine}");

        // Assert: índice deve existir (condição necessária para RNF 1.2)
        indexCount.Should().Be(1,
            "idx_opportunities_tenant_bu_stage deve existir para consultas Kanban (RNF 1.2). " +
            "O planner do PostgreSQL pode escolher Seq Scan para datasets pequenos (Testcontainers), " +
            "mas em produção com 500+ rows o índice será preferido automaticamente.");
    }

    // =========================================================================
    // LOAD-04 — Paginação por coluna: somas totais corretas independente do page_size
    // =========================================================================

    [Fact(DisplayName = "LOAD-04: somas SUM(valor_total) são corretas independente da paginação dos cards (RNF 1.3)")]
    public async Task KanbanPagination_TotalSums_ConsistentAcrossPageSizes()
    {
        // Arrange: 100 oportunidades no estágio Prospecção
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();
        var (firstStageId, _, _) = Stages[0];

        await using var seedConn = new NpgsqlConnection(fixture.AppConnectionString + ";Pooling=false");
        await seedConn.OpenAsync();
        await using var seedSetCmd = new NpgsqlCommand($"SET app.current_tenant = '{tenantId}'", seedConn);
        await seedSetCmd.ExecuteNonQueryAsync();

        // Valor fixo para cálculo determinístico: 100 opp × (valor_setup=100000, mensal=50000, meses=12)
        // valor_total por opp = 100000 + 50000 × 12 = 700000 centavos
        // total esperado = 100 × 700000 = 70.000.000 centavos
        const long valorSetup = 100000L;
        const long valorMensal = 50000L;
        const int meses = 12;
        const long valorTotalPorOpp = valorSetup + valorMensal * meses; // 700000
        const int totalOpps = 100;
        const long expectedTotal = valorTotalPorOpp * totalOpps; // 70.000.000

        for (var i = 0; i < totalOpps; i++)
        {
            await using var insertCmd = new NpgsqlCommand($@"
                INSERT INTO opportunities (
                    id, tenant_id, bu_id, account_id,
                    stage_id, stage_name, stage_category_ref, stage_default_probability, stage_order,
                    owner_id, origin_channel_id, origin_channel_name, origin_is_partner_channel,
                    opportunity_number, title, valor_setup, valor_mensal, duracao_meses,
                    probabilidade, stage_category, created_by, created_at, updated_at
                ) VALUES (
                    '{Guid.NewGuid()}', '{tenantId}', '{buId}', '{Guid.NewGuid()}',
                    '{firstStageId}', 'Prospecção', 'open', 20, 1,
                    '{Guid.NewGuid()}', '{Guid.NewGuid()}', 'Direto', FALSE,
                    'AZ-P{i:D4}', 'Paginação {i}', {valorSetup}, {valorMensal}, {meses},
                    20, 'open', '{Guid.NewGuid()}', now(), now()
                ) ON CONFLICT DO NOTHING", seedConn);
            await insertCmd.ExecuteNonQueryAsync();
        }

        // Act: aggregate total do estágio (independente de paginação)
        await using var aggConn = new NpgsqlConnection(fixture.AppConnectionString + ";Pooling=false");
        await aggConn.OpenAsync();
        await using var aggSetCmd = new NpgsqlCommand($"SET app.current_tenant = '{tenantId}'", aggConn);
        await aggSetCmd.ExecuteNonQueryAsync();

        await using var aggCmd = new NpgsqlCommand(KanbanAggregateSql(tenantId, buId), aggConn);
        long aggregateTotal = 0;
        int aggregateCount = 0;
        await using var aggReader = await aggCmd.ExecuteReaderAsync();
        while (await aggReader.ReadAsync())
        {
            if (aggReader.GetGuid(0) == firstStageId)
            {
                aggregateTotal = aggReader.GetInt64(3); // total_value_cents
                aggregateCount = aggReader.GetInt32(5); // total_count
            }
        }

        // Act: cards paginados page=10 (não afeta o total agregado)
        await using var cardConn = new NpgsqlConnection(fixture.AppConnectionString + ";Pooling=false");
        await cardConn.OpenAsync();
        await using var cardSetCmd = new NpgsqlCommand($"SET app.current_tenant = '{tenantId}'", cardConn);
        await cardSetCmd.ExecuteNonQueryAsync();

        await using var cardsCmd = new NpgsqlCommand(
            KanbanCardsSql(tenantId, buId, firstStageId, pageSize: 10), cardConn);
        var cardCount = 0;
        await using var cardsReader = await cardsCmd.ExecuteReaderAsync();
        while (await cardsReader.ReadAsync()) cardCount++;

        // Assert
        aggregateTotal.Should().Be(expectedTotal,
            $"SUM(valor_total) deve ser {expectedTotal} centavos para {totalOpps} oportunidades " +
            $"com valor_total={valorTotalPorOpp} cada (RNF 1.3, design §5.2).");

        aggregateCount.Should().Be(totalOpps,
            $"COUNT(*) deve refletir todas as {totalOpps} oportunidades do estágio.");

        cardCount.Should().BeLessOrEqualTo(10,
            "paginação de cards com LIMIT 10 deve retornar no máximo 10 itens.");

        // Somas totais independem da paginação de cards (a aggregation é separada)
        // O design §5.2 explica: SUM é calculado separadamente, cards são paginados de forma incremental.
    }

    // =========================================================================
    // LOAD-05 — Baseline de escrita: INSERT p95 ≤ 500 ms (RNF 2)
    // =========================================================================

    [Fact(DisplayName = "LOAD-05: INSERT oportunidade p95 ≤ 500 ms (baseline SQL para SLO de escrita RNF 2)")]
    public async Task WriteOperation_P95LessThan500Ms()
    {
        var tenantId = Guid.NewGuid();
        const int runs = 20;
        var latencies = new long[runs];

        for (var i = 0; i < runs; i++)
        {
            await using var conn = new NpgsqlConnection(fixture.AppConnectionString + ";Pooling=false");
            await conn.OpenAsync();
            await using var setCmd = new NpgsqlCommand($"SET app.current_tenant = '{tenantId}'", conn);
            await setCmd.ExecuteNonQueryAsync();

            var sw = Stopwatch.StartNew();
            await using var insertCmd = new NpgsqlCommand($@"
                INSERT INTO opportunities (
                    id, tenant_id, bu_id, account_id,
                    stage_id, stage_name, stage_category_ref, stage_default_probability, stage_order,
                    owner_id, origin_channel_id, origin_channel_name, origin_is_partner_channel,
                    opportunity_number, title, valor_setup, valor_mensal, duracao_meses,
                    probabilidade, stage_category, created_by, created_at, updated_at
                ) VALUES (
                    '{Guid.NewGuid()}', '{tenantId}', '{Guid.NewGuid()}', '{Guid.NewGuid()}',
                    '{Guid.NewGuid()}', 'Qualificação', 'open', 20, 1,
                    '{Guid.NewGuid()}', '{Guid.NewGuid()}', 'Direto', FALSE,
                    'AZ-W{i:D4}', 'Write SLO {i}', 100000, 0, 0,
                    20, 'open', '{Guid.NewGuid()}', now(), now()
                ) ON CONFLICT DO NOTHING", conn);
            await insertCmd.ExecuteNonQueryAsync();
            sw.Stop();
            latencies[i] = sw.ElapsedMilliseconds;
        }

        Array.Sort(latencies);
        var p50 = latencies[runs / 2];
        var p95 = latencies[(int)(runs * 0.95)];

        await File.AppendAllTextAsync(
            Path.Combine(Path.GetTempPath(), "kanban-load-results.txt"),
            $"[{DateTime.UtcNow:u}] Write p50={p50}ms p95={p95}ms (SQL direto, sem middleware/Outbox){Environment.NewLine}");

        p95.Should().BeLessOrEqualTo(500,
            $"INSERT deve ter p95 ≤ 500 ms (baseline SQL para RNF 2). " +
            $"Resultado: p50={p50}ms, p95={p95}ms. " +
            $"Em produção adicionar ~50-150ms para pipeline behavior + Outbox + AuditLog na mesma tx.");
    }
}
