using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoalForecast.Infrastructure.Persistence.Migrations;

/// <summary>
/// Migration inicial: cria tabela goals com constraints, índices e RLS (ADR-0001).
///
/// Mapeia: Req 2, RNF 1, ADR-0001, DD-002, DD-008, design §7, TASK-16.
///
/// Decisão DD-008: bu_id é NOT NULL aqui, divergindo da data-model que o marca nullable.
/// Justificativa: requirements §4 determina que não existe meta global de tenant — bu_id
/// é sempre obrigatória. Registrado para reconciliação na TASK-30.
/// </summary>
public partial class InitialGoals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── 1. Tabela goals ──────────────────────────────────────────────────
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS goals (
                id          UUID        NOT NULL DEFAULT gen_random_uuid(),
                tenant_id   UUID        NOT NULL,
                bu_id       UUID        NOT NULL,
                owner_id    UUID,
                year        SMALLINT    NOT NULL,
                month       SMALLINT    NOT NULL CHECK (month BETWEEN 1 AND 12),
                valor_meta  BIGINT      NOT NULL CHECK (valor_meta >= 0),
                created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
                updated_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
                CONSTRAINT pk_goals PRIMARY KEY (id)
            );
            """);

        // ── 2. UNIQUE composta para escopo RESPONSAVEL (owner_id NOT NULL) ──
        // Garante unicidade por (tenant, bu, owner, year, month) quando há owner.
        // NULL != NULL no Postgres, então não impede duplicatas de escopo BU.
        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX IF NOT EXISTS ux_goals_responsavel_scope
                ON goals (tenant_id, bu_id, owner_id, year, month)
                WHERE owner_id IS NOT NULL;
            """);

        // ── 3. Índice único parcial para escopo BU (owner_id IS NULL) (DD-002) ──
        // Complementa a UNIQUE acima: impede duplicata de meta BU com owner nulo.
        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX IF NOT EXISTS ux_goals_bu_scope
                ON goals (tenant_id, bu_id, year, month)
                WHERE owner_id IS NULL;
            """);

        // ── 4. Índices de consulta (design §7) ──────────────────────────────
        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS ix_goals_tenant_bu_period
                ON goals (tenant_id, bu_id, year, month);
            """);

        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS ix_goals_tenant_owner_period
                ON goals (tenant_id, owner_id, year, month);
            """);

        // ── 5. Tabela outbox_events ──────────────────────────────────────────
        // Usada pelo OutboxDispatcher (TASK-20) para garantir atomicidade entre
        // escrita de goals e despacho de GoalUpdated (RNF 5, design §6.6).
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS outbox_events (
                id              UUID        NOT NULL DEFAULT gen_random_uuid(),
                event_id        UUID        NOT NULL,
                tenant_id       UUID        NOT NULL,
                event_type      TEXT        NOT NULL,
                payload         JSONB       NOT NULL,
                created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
                processed_at    TIMESTAMPTZ,
                CONSTRAINT pk_outbox_events PRIMARY KEY (id),
                CONSTRAINT uq_outbox_event_id UNIQUE (event_id)
            );
            """);

        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS ix_outbox_events_unprocessed
                ON outbox_events (created_at)
                WHERE processed_at IS NULL;
            """);

        // ── 6. RLS — Row-Level Security obrigatória (ADR-0001) ──────────────
        // Habilita RLS na tabela goals. Segunda camada de defesa além do Global Query Filter.
        // FORCE ROW LEVEL SECURITY: garante isolamento mesmo para o owner da tabela.
        migrationBuilder.Sql("""
            ALTER TABLE goals ENABLE ROW LEVEL SECURITY;
            ALTER TABLE goals FORCE ROW LEVEL SECURITY;
            """);

        // Policy de isolamento por tenant: filtra por current_setting('app.tenant_id').
        // A sessão de conexão deve definir SET app.tenant_id = '<uuid>' a cada request.
        // Sem SET app.tenant_id: current_setting retorna '' → cast para UUID falha →
        // exceção interceptada por pg_exception → RLS retorna false → zero linhas.
        migrationBuilder.Sql("""
            CREATE POLICY goals_tenant_isolation ON goals
                USING (
                    tenant_id = CASE
                        WHEN current_setting('app.tenant_id', true) = '' OR
                             current_setting('app.tenant_id', true) IS NULL
                        THEN '00000000-0000-0000-0000-000000000000'::uuid
                        ELSE current_setting('app.tenant_id', true)::uuid
                    END
                );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP POLICY IF EXISTS goals_tenant_isolation ON goals;");
        migrationBuilder.Sql("ALTER TABLE goals DISABLE ROW LEVEL SECURITY;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS outbox_events;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS goals;");
    }
}
