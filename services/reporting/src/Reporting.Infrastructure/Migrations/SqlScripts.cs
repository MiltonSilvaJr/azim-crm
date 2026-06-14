namespace Reporting.Infrastructure.Migrations;

/// <summary>
/// Scripts SQL inline sincronizados com os arquivos .sql de migrations.
/// Usados pelo <see cref="MigrationRunner"/> em testes de integração (Testcontainers)
/// quando o diretório de migrations não está disponível no path do assembly de teste.
///
/// Mapeia: TASK-13..TASK-17, design §7.2, §7.4.
/// </summary>
public static class SqlScripts
{
    /// <summary>
    /// Schema base das tabelas autoritativas (somente para testes — não cria em produção).
    /// As tabelas reais são criadas pelo schema do azim-api; aqui usamos um subset mínimo.
    /// </summary>
    public const string BaseSchema = """
        -- Tabelas autoritativas (subset mínimo para testes de integração)
        -- Em produção, estas tabelas são criadas pelo schema do azim-api.

        CREATE TABLE IF NOT EXISTS stages (
            id          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
            tenant_id   UUID        NOT NULL,
            bu_id       UUID        NOT NULL,
            name        TEXT        NOT NULL,
            probability INT         NOT NULL DEFAULT 0,
            category    TEXT        NOT NULL DEFAULT 'open',
            position    INT         NOT NULL DEFAULT 0
        );

        ALTER TABLE stages ENABLE ROW LEVEL SECURITY;
        ALTER TABLE stages FORCE ROW LEVEL SECURITY;
        DROP POLICY IF EXISTS rls_stages ON stages;
        CREATE POLICY rls_stages ON stages
            USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

        CREATE TABLE IF NOT EXISTS opportunities (
            id                  UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
            tenant_id           UUID        NOT NULL,
            bu_id               UUID        NOT NULL,
            owner_id            UUID        NOT NULL,
            stage_id            UUID        NOT NULL REFERENCES stages(id),
            origin_channel_id   UUID,
            partner_id          UUID,
            stage_category      TEXT        NOT NULL DEFAULT 'open',
            valor_total         BIGINT      NOT NULL DEFAULT 0,
            forecast_ponderado  BIGINT      NOT NULL DEFAULT 0,
            created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            closed_at           TIMESTAMPTZ
        );

        ALTER TABLE opportunities ENABLE ROW LEVEL SECURITY;
        ALTER TABLE opportunities FORCE ROW LEVEL SECURITY;
        DROP POLICY IF EXISTS rls_opportunities ON opportunities;
        CREATE POLICY rls_opportunities ON opportunities
            USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

        CREATE TABLE IF NOT EXISTS partners (
            id          UUID    PRIMARY KEY DEFAULT gen_random_uuid(),
            tenant_id   UUID    NOT NULL,
            name        TEXT    NOT NULL
        );

        ALTER TABLE partners ENABLE ROW LEVEL SECURITY;
        ALTER TABLE partners FORCE ROW LEVEL SECURITY;
        DROP POLICY IF EXISTS rls_partners ON partners;
        CREATE POLICY rls_partners ON partners
            USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

        CREATE TABLE IF NOT EXISTS opportunity_partner_commissions (
            id                  UUID    PRIMARY KEY DEFAULT gen_random_uuid(),
            tenant_id           UUID    NOT NULL,
            opportunity_id      UUID    NOT NULL REFERENCES opportunities(id),
            partner_id          UUID    NOT NULL REFERENCES partners(id),
            comissao_calculada  BIGINT  NOT NULL DEFAULT 0,
            is_snapshot         BOOLEAN NOT NULL DEFAULT false,
            snapshot_at         TIMESTAMPTZ
        );

        ALTER TABLE opportunity_partner_commissions ENABLE ROW LEVEL SECURITY;
        ALTER TABLE opportunity_partner_commissions FORCE ROW LEVEL SECURITY;
        DROP POLICY IF EXISTS rls_opc ON opportunity_partner_commissions;
        CREATE POLICY rls_opc ON opportunity_partner_commissions
            USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

        CREATE TABLE IF NOT EXISTS goals (
            id          UUID    PRIMARY KEY DEFAULT gen_random_uuid(),
            tenant_id   UUID    NOT NULL,
            bu_id       UUID    NOT NULL,
            goal_year   INT     NOT NULL,
            goal_month  INT     NOT NULL,
            goal_cents  BIGINT  NOT NULL DEFAULT 0
        );

        ALTER TABLE goals ENABLE ROW LEVEL SECURITY;
        ALTER TABLE goals FORCE ROW LEVEL SECURITY;
        DROP POLICY IF EXISTS rls_goals ON goals;
        CREATE POLICY rls_goals ON goals
            USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

        CREATE TABLE IF NOT EXISTS origin_channels (
            id          UUID    PRIMARY KEY DEFAULT gen_random_uuid(),
            tenant_id   UUID    NOT NULL,
            bu_id       UUID,
            name        TEXT    NOT NULL
        );

        ALTER TABLE origin_channels ENABLE ROW LEVEL SECURITY;
        ALTER TABLE origin_channels FORCE ROW LEVEL SECURITY;
        DROP POLICY IF EXISTS rls_origin_channels ON origin_channels;
        CREATE POLICY rls_origin_channels ON origin_channels
            USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

        CREATE TABLE IF NOT EXISTS users (
            id              UUID    PRIMARY KEY DEFAULT gen_random_uuid(),
            tenant_id       UUID    NOT NULL,
            display_name    TEXT    NOT NULL
        );

        ALTER TABLE users ENABLE ROW LEVEL SECURITY;
        ALTER TABLE users FORCE ROW LEVEL SECURITY;
        DROP POLICY IF EXISTS rls_users ON users;
        CREATE POLICY rls_users ON users
            USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
        """;

    /// <summary>TASK-13: View vw_funnel_report com security_invoker.</summary>
    public const string M001_CreateViewFunnelReport = """
        CREATE OR REPLACE VIEW vw_funnel_report
        WITH (security_invoker = true) AS
        SELECT
            o.tenant_id,
            o.bu_id,
            o.owner_id,
            s.id            AS stage_id,
            s.name          AS stage_name,
            s.category      AS stage_category,
            o.created_at,
            o.valor_total          AS total_cents,
            o.forecast_ponderado   AS weighted_forecast_cents
        FROM opportunities o
        JOIN stages s ON s.id = o.stage_id;
        """;

    /// <summary>TASK-14: View vw_forecast_report com LEFT JOIN goals (meta ausente = NULL).</summary>
    public const string M002_CreateViewForecastReport = """
        CREATE OR REPLACE VIEW vw_forecast_report
        WITH (security_invoker = true) AS
        SELECT
            o.tenant_id,
            o.bu_id,
            o.owner_id,
            EXTRACT(YEAR  FROM o.closed_at)::int AS year,
            EXTRACT(MONTH FROM o.closed_at)::int AS month,
            o.forecast_ponderado                  AS weighted_forecast_cents,
            CASE WHEN o.stage_category = 'won'
                 THEN o.valor_total
                 ELSE 0
            END                                   AS realized_cents,
            g.goal_cents
        FROM opportunities o
        LEFT JOIN goals g
            ON  g.tenant_id = o.tenant_id
            AND g.bu_id     = o.bu_id
            AND EXTRACT(YEAR  FROM o.closed_at) = g.goal_year
            AND EXTRACT(MONTH FROM o.closed_at) = g.goal_month;
        """;

    /// <summary>TASK-15: View vw_ranking_report com display_name (PII — DD-008).</summary>
    public const string M003_CreateViewRankingReport = """
        CREATE OR REPLACE VIEW vw_ranking_report
        WITH (security_invoker = true) AS
        SELECT
            o.tenant_id,
            o.bu_id,
            o.owner_id,
            u.display_name,
            o.created_at,
            o.stage_category,
            o.valor_total          AS total_cents,
            o.forecast_ponderado   AS weighted_forecast_cents
        FROM opportunities o
        LEFT JOIN users u ON u.id = o.owner_id;
        """;

    /// <summary>TASK-15: View vw_channel_report sem PII (Req 3).</summary>
    public const string M004_CreateViewChannelReport = """
        CREATE OR REPLACE VIEW vw_channel_report
        WITH (security_invoker = true) AS
        SELECT
            o.tenant_id,
            o.bu_id,
            o.owner_id,
            o.origin_channel_id    AS channel_id,
            oc.name                AS channel_name,
            o.created_at,
            o.valor_total          AS total_cents
        FROM opportunities o
        LEFT JOIN origin_channels oc ON oc.id = o.origin_channel_id;
        """;

    /// <summary>TASK-16: View vw_commission_report com is_snapshot (RN-007, PBT-01).</summary>
    public const string M005_CreateViewCommissionReport = """
        CREATE OR REPLACE VIEW vw_commission_report
        WITH (security_invoker = true) AS
        SELECT
            c.tenant_id,
            c.partner_id,
            p.name          AS partner_name,
            o.bu_id,
            o.owner_id,
            o.created_at,
            o.stage_category,
            c.comissao_calculada AS commission_cents,
            c.is_snapshot
        FROM opportunity_partner_commissions c
        JOIN opportunities o ON o.id = c.opportunity_id
        JOIN partners p      ON p.id = c.partner_id;
        """;

    /// <summary>TASK-17: Cinco índices críticos idempotentes (IF NOT EXISTS).</summary>
    public const string M006_CreateCriticalIndexes = """
        CREATE INDEX IF NOT EXISTS ix_opp_tenant_bu_created
            ON opportunities (tenant_id, bu_id, created_at);

        CREATE INDEX IF NOT EXISTS ix_opp_tenant_owner_cat
            ON opportunities (tenant_id, owner_id, stage_category);

        CREATE INDEX IF NOT EXISTS ix_opp_tenant_channel
            ON opportunities (tenant_id, origin_channel_id);

        CREATE INDEX IF NOT EXISTS ix_opp_tenant_closed
            ON opportunities (tenant_id, closed_at)
            WHERE stage_category = 'won';

        CREATE INDEX IF NOT EXISTS ix_opc_tenant_partner_snap
            ON opportunity_partner_commissions (tenant_id, partner_id, is_snapshot);
        """;
}
