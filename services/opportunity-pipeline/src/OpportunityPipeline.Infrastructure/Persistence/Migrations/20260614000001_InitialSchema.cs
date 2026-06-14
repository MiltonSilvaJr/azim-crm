using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpportunityPipeline.Infrastructure.Persistence.Migrations;

/// <summary>
/// Migration inicial: cria as 7 tabelas do módulo opportunity-pipeline + RLS + REVOKE.
/// Aplicada manualmente — inclui SQL que o scaffolding do EF não gera:
/// RLS, policies, REVOKE, índices parciais de segurança, check constraints.
/// Mapeia: design §7, ADR-0001, RNF 5, RNF 7, TASK-13.
/// </summary>
public partial class InitialSchema : Migration
{
    /// <inheritdoc/>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // =====================================================================
        // 1. opportunities
        // =====================================================================
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS opportunities (
    id                          UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id                   UUID         NOT NULL,
    bu_id                       UUID         NOT NULL,
    account_id                  UUID         NOT NULL,
    partner_id                  UUID,
    stage_id                    UUID         NOT NULL,
    stage_name                  VARCHAR(100) NOT NULL DEFAULT '',
    stage_category_ref          VARCHAR(20)  NOT NULL DEFAULT 'open',
    stage_default_probability   SMALLINT     NOT NULL DEFAULT 0,
    stage_order                 INTEGER      NOT NULL DEFAULT 0,
    owner_id                    UUID         NOT NULL,
    origin_channel_id           UUID         NOT NULL,
    origin_channel_name         VARCHAR(100) NOT NULL DEFAULT '',
    origin_is_partner_channel   BOOLEAN      NOT NULL DEFAULT FALSE,
    opportunity_number          VARCHAR(10)  NOT NULL,
    title                       TEXT         NOT NULL,
    valor_setup                 BIGINT       NOT NULL DEFAULT 0,
    valor_mensal                BIGINT       NOT NULL DEFAULT 0,
    duracao_meses               INTEGER      NOT NULL DEFAULT 0,
    probabilidade               SMALLINT     NOT NULL DEFAULT 0,
    data_fechamento_esperada    DATE,
    loss_reason_id              UUID,
    loss_reason_name            VARCHAR(200),
    closed_at                   TIMESTAMPTZ,
    stage_category              VARCHAR(20)  NOT NULL DEFAULT 'open',
    is_stale                    BOOLEAN      NOT NULL DEFAULT FALSE,
    notes                       TEXT,
    created_at                  TIMESTAMPTZ  NOT NULL DEFAULT now(),
    updated_at                  TIMESTAMPTZ  NOT NULL DEFAULT now(),
    created_by                  UUID         NOT NULL,
    CONSTRAINT uq_opportunities_tenant_number UNIQUE (tenant_id, opportunity_number),
    CONSTRAINT chk_opportunities_category CHECK (stage_category IN ('open','won','lost')),
    CONSTRAINT chk_opportunities_prob CHECK (probabilidade BETWEEN 0 AND 100),
    CONSTRAINT chk_opportunities_values_nonneg CHECK (valor_setup >= 0 AND valor_mensal >= 0 AND duracao_meses >= 0)
);

-- Índices de performance (design §7.1)
CREATE INDEX IF NOT EXISTS idx_opportunities_tenant_bu_stage
    ON opportunities (tenant_id, bu_id, stage_id);
CREATE INDEX IF NOT EXISTS idx_opportunities_tenant_owner
    ON opportunities (tenant_id, owner_id);
CREATE INDEX IF NOT EXISTS idx_opportunities_tenant_partner
    ON opportunities (tenant_id, partner_id) WHERE partner_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_opportunities_tenant_close
    ON opportunities (tenant_id, data_fechamento_esperada);
CREATE INDEX IF NOT EXISTS idx_opportunities_tenant_lastact
    ON opportunities (tenant_id, updated_at) WHERE stage_category = 'open';

-- RLS: enable + policy falha-fechada (ADR-0001 camada 3)
ALTER TABLE opportunities ENABLE ROW LEVEL SECURITY;
ALTER TABLE opportunities FORCE ROW LEVEL SECURITY;

CREATE POLICY opp_tenant_isolation ON opportunities
    FOR ALL
    USING (tenant_id = NULLIF(current_setting('app.current_tenant', TRUE), '')::UUID)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', TRUE), '')::UUID);
");

        // =====================================================================
        // 2. opportunity_stage_transitions (append-only, RNF 7)
        // =====================================================================
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS opportunity_stage_transitions (
    id              UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID        NOT NULL,
    opportunity_id  UUID        NOT NULL REFERENCES opportunities(id),
    from_stage_id   UUID,
    to_stage_id     UUID        NOT NULL,
    from_category   VARCHAR(20),
    to_category     VARCHAR(20) NOT NULL,
    actor_id        UUID        NOT NULL,
    occurred_at     TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_stage_transitions_tenant_opp
    ON opportunity_stage_transitions (tenant_id, opportunity_id, occurred_at);

-- RLS (ADR-0001)
ALTER TABLE opportunity_stage_transitions ENABLE ROW LEVEL SECURITY;
ALTER TABLE opportunity_stage_transitions FORCE ROW LEVEL SECURITY;

CREATE POLICY transitions_tenant_isolation ON opportunity_stage_transitions
    FOR ALL
    USING (tenant_id = NULLIF(current_setting('app.current_tenant', TRUE), '')::UUID)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', TRUE), '')::UUID);

-- REVOKE UPDATE/DELETE do role app (RNF 7, RNF 8.2)
-- Nota: se o role 'app' não existir no ambiente, este comando deve ser executado
-- após criar o role. Em ambiente de teste a constraint é garantida pelo trigger.
DO $$ BEGIN
    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'app') THEN
        REVOKE UPDATE, DELETE ON opportunity_stage_transitions FROM app;
    END IF;
END $$;
");

        // =====================================================================
        // 3. opportunity_partner_commissions (snapshot imutável, RNF 5)
        // =====================================================================
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS opportunity_partner_commissions (
    id                      UUID           PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id               UUID           NOT NULL,
    opportunity_id          UUID           NOT NULL REFERENCES opportunities(id),
    partner_id              UUID           NOT NULL,
    role                    VARCHAR(20)    NOT NULL,
    pct_setup               NUMERIC(5,2)   NOT NULL DEFAULT 0,
    pct_recorrente          NUMERIC(5,2)   NOT NULL DEFAULT 0,
    valor_fixo              BIGINT         NOT NULL DEFAULT 0,
    meses_comissionados     INTEGER        NOT NULL DEFAULT 0,
    comissao_setup_cents    BIGINT         NOT NULL DEFAULT 0,
    comissao_recorrente_cents BIGINT       NOT NULL DEFAULT 0,
    comissao_calculada      BIGINT         NOT NULL DEFAULT 0,
    is_snapshot             BOOLEAN        NOT NULL DEFAULT FALSE,
    snapshot_at             TIMESTAMPTZ,
    created_at              TIMESTAMPTZ    NOT NULL DEFAULT now(),
    CONSTRAINT chk_commission_role
        CHECK (role IN ('Indicador','Revendedor','Distribuidor','Integrador')),
    CONSTRAINT chk_commission_nonneg
        CHECK (valor_fixo >= 0 AND comissao_calculada >= 0 AND meses_comissionados >= 0)
);

CREATE INDEX IF NOT EXISTS idx_commission_tenant_opp
    ON opportunity_partner_commissions (tenant_id, opportunity_id);
CREATE INDEX IF NOT EXISTS idx_commission_tenant_partner
    ON opportunity_partner_commissions (tenant_id, partner_id);

-- Índice parcial único: no máximo 1 snapshot por oportunidade (INV-12, RNF 5)
CREATE UNIQUE INDEX IF NOT EXISTS uq_partner_commission_active_snapshot
    ON opportunity_partner_commissions (opportunity_id)
    WHERE is_snapshot = TRUE;

-- RLS
ALTER TABLE opportunity_partner_commissions ENABLE ROW LEVEL SECURITY;
ALTER TABLE opportunity_partner_commissions FORCE ROW LEVEL SECURITY;

CREATE POLICY commissions_tenant_isolation ON opportunity_partner_commissions
    FOR ALL
    USING (tenant_id = NULLIF(current_setting('app.current_tenant', TRUE), '')::UUID)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', TRUE), '')::UUID);
");

        // =====================================================================
        // 4. opportunity_contacts
        // =====================================================================
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS opportunity_contacts (
    id              UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID        NOT NULL,
    opportunity_id  UUID        NOT NULL REFERENCES opportunities(id),
    contact_id      UUID        NOT NULL,
    is_primary      BOOLEAN     NOT NULL DEFAULT FALSE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_opportunity_contacts_link UNIQUE (opportunity_id, contact_id)
);

-- Índice parcial único: exatamente 1 principal por oportunidade (INV-11)
CREATE UNIQUE INDEX IF NOT EXISTS uq_opportunity_contacts_primary
    ON opportunity_contacts (opportunity_id)
    WHERE is_primary = TRUE;

-- RLS
ALTER TABLE opportunity_contacts ENABLE ROW LEVEL SECURITY;
ALTER TABLE opportunity_contacts FORCE ROW LEVEL SECURITY;

CREATE POLICY contacts_tenant_isolation ON opportunity_contacts
    FOR ALL
    USING (tenant_id = NULLIF(current_setting('app.current_tenant', TRUE), '')::UUID)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', TRUE), '')::UUID);
");

        // =====================================================================
        // 5. opportunity_number_sequences (contador atômico — DD-001)
        // =====================================================================
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS opportunity_number_sequences (
    tenant_id   UUID   PRIMARY KEY,
    next_value  BIGINT NOT NULL DEFAULT 1
);

-- RLS
ALTER TABLE opportunity_number_sequences ENABLE ROW LEVEL SECURITY;
ALTER TABLE opportunity_number_sequences FORCE ROW LEVEL SECURITY;

CREATE POLICY sequences_tenant_isolation ON opportunity_number_sequences
    FOR ALL
    USING (tenant_id = NULLIF(current_setting('app.current_tenant', TRUE), '')::UUID)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', TRUE), '')::UUID);
");

        // =====================================================================
        // 6. stale_detection_runs (idempotência — RNF 9, PBT-09)
        // =====================================================================
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS stale_detection_runs (
    tenant_id           UUID        NOT NULL,
    opportunity_id      UUID        NOT NULL,
    detection_period    VARCHAR(10) NOT NULL,
    detected_at         TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (tenant_id, opportunity_id, detection_period)
);

-- RLS
ALTER TABLE stale_detection_runs ENABLE ROW LEVEL SECURITY;
ALTER TABLE stale_detection_runs FORCE ROW LEVEL SECURITY;

CREATE POLICY stale_runs_tenant_isolation ON stale_detection_runs
    FOR ALL
    USING (tenant_id = NULLIF(current_setting('app.current_tenant', TRUE), '')::UUID)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', TRUE), '')::UUID);
");

        // =====================================================================
        // 7. saved_filters
        // =====================================================================
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS saved_filters (
    id          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id   UUID        NOT NULL,
    user_id     UUID        NOT NULL,
    name        TEXT        NOT NULL,
    criteria    JSONB       NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_saved_filters_user_name UNIQUE (tenant_id, user_id, name)
);

-- RLS
ALTER TABLE saved_filters ENABLE ROW LEVEL SECURITY;
ALTER TABLE saved_filters FORCE ROW LEVEL SECURITY;

CREATE POLICY saved_filters_tenant_isolation ON saved_filters
    FOR ALL
    USING (tenant_id = NULLIF(current_setting('app.current_tenant', TRUE), '')::UUID)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', TRUE), '')::UUID);
");

        // =====================================================================
        // 8. outbox_events (ADR-0004, design §6.6)
        // =====================================================================
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS outbox_events (
    id          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id   UUID        NOT NULL,
    event_type  VARCHAR(100) NOT NULL,
    payload     JSONB       NOT NULL,
    status      VARCHAR(20) NOT NULL DEFAULT 'pending',
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    published_at TIMESTAMPTZ,
    attempts    INTEGER     NOT NULL DEFAULT 0,
    last_error  TEXT
);

CREATE INDEX IF NOT EXISTS idx_outbox_pending
    ON outbox_events (tenant_id, status, created_at)
    WHERE status = 'pending';

-- RLS
ALTER TABLE outbox_events ENABLE ROW LEVEL SECURITY;
ALTER TABLE outbox_events FORCE ROW LEVEL SECURITY;

CREATE POLICY outbox_tenant_isolation ON outbox_events
    FOR ALL
    USING (tenant_id = NULLIF(current_setting('app.current_tenant', TRUE), '')::UUID)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', TRUE), '')::UUID);
");
    }

    /// <inheritdoc/>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS outbox_events CASCADE;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS saved_filters CASCADE;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS stale_detection_runs CASCADE;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS opportunity_number_sequences CASCADE;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS opportunity_contacts CASCADE;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS opportunity_partner_commissions CASCADE;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS opportunity_stage_transitions CASCADE;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS opportunities CASCADE;");
    }
}
