using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantAdministration.Infrastructure.Persistence.Migrations;

// ──────────────────────────────────────────────────────────────────────────────
// REVISÃO OBRIGATÓRIA (RNF 1.3): esta migration toca a tabela tenant_brandings
// e configura Row-Level Security (RLS). Qualquer alteração neste arquivo ou em
// migrations subsequentes que afetem tenant_id, RLS policies ou o trigger
// prevent_slug_update deve passar por revisão de código antes de ser aplicada
// em staging ou produção.
// ──────────────────────────────────────────────────────────────────────────────

/// <inheritdoc/>
public partial class InitialSchema : Migration
{
    /// <inheritdoc/>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── tenants ──────────────────────────────────────────────────────────
        migrationBuilder.Sql("""
            CREATE TABLE tenants (
                id                  UUID         NOT NULL DEFAULT gen_random_uuid(),
                slug                TEXT         NOT NULL,
                display_name        TEXT         NOT NULL,
                iana_timezone       TEXT         NOT NULL DEFAULT 'America/Sao_Paulo',
                digest_time         TEXT         NOT NULL DEFAULT '07:00',
                status              VARCHAR(20)  NOT NULL DEFAULT 'provisioned'
                                    CHECK (status IN ('provisioned', 'suspended')),
                active              BOOLEAN      NOT NULL DEFAULT TRUE,
                identity_tenant_id  TEXT         NULL,
                provisioned_at      TIMESTAMPTZ  NOT NULL DEFAULT now(),
                created_at          TIMESTAMPTZ  NOT NULL DEFAULT now(),
                updated_at          TIMESTAMPTZ  NOT NULL DEFAULT now(),
                CONSTRAINT pk_tenants PRIMARY KEY (id)
            );
            """);

        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX uq_tenants_slug ON tenants (slug);
            """);

        // ── Trigger: impede UPDATE do slug (RN-019, design.md §7.1, DD-002) ─
        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION prevent_slug_update_fn()
            RETURNS TRIGGER LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.slug <> OLD.slug THEN
                    RAISE EXCEPTION 'TA-ERR-SLUG-IMMUTABLE: slug is immutable and cannot be changed';
                END IF;
                RETURN NEW;
            END;
            $$;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER prevent_slug_update
            BEFORE UPDATE ON tenants
            FOR EACH ROW
            WHEN (OLD.slug IS DISTINCT FROM NEW.slug)
            EXECUTE FUNCTION prevent_slug_update_fn();
            """);

        // ── Trigger: mantém active sincronizado com status (DD-002) ──────────
        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION sync_active_from_status_fn()
            RETURNS TRIGGER LANGUAGE plpgsql AS $$
            BEGIN
                NEW.active := (NEW.status = 'provisioned');
                RETURN NEW;
            END;
            $$;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER sync_active_from_status
            BEFORE INSERT OR UPDATE OF status ON tenants
            FOR EACH ROW
            EXECUTE FUNCTION sync_active_from_status_fn();
            """);

        // ── tenant_brandings ─────────────────────────────────────────────────
        // REVISÃO OBRIGATÓRIA (RNF 1.3): tabela com RLS — ver cabeçalho.
        migrationBuilder.Sql("""
            CREATE TABLE tenant_brandings (
                id                 UUID          NOT NULL DEFAULT gen_random_uuid(),
                tenant_id          UUID          NOT NULL,
                logo_url           TEXT          NULL,
                favicon_url        TEXT          NULL,
                primary_color      CHAR(7)       NULL CHECK (primary_color ~ '^#[0-9A-F]{6}$'),
                secondary_color    CHAR(7)       NULL CHECK (secondary_color ~ '^#[0-9A-F]{6}$'),
                wcag_contrast_ok   BOOLEAN       NOT NULL DEFAULT FALSE,
                last_contrast_ratio NUMERIC(4,2) NULL,
                updated_at         TIMESTAMPTZ   NOT NULL DEFAULT now(),
                CONSTRAINT pk_tenant_brandings PRIMARY KEY (id),
                CONSTRAINT fk_tenant_brandings_tenant_id
                    FOREIGN KEY (tenant_id) REFERENCES tenants (id)
            );
            """);

        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX uq_tenant_brandings_tenant ON tenant_brandings (tenant_id);
            """);

        // ── RLS em tenant_brandings (ADR-0001, design.md §7.4) ─────────────
        // REVISÃO OBRIGATÓRIA (RNF 1.3): habilita RLS e cria policy de isolamento.
        migrationBuilder.Sql("""
            ALTER TABLE tenant_brandings ENABLE ROW LEVEL SECURITY;
            ALTER TABLE tenant_brandings FORCE ROW LEVEL SECURITY;
            """);

        migrationBuilder.Sql("""
            CREATE POLICY tenant_isolation_policy ON tenant_brandings
            USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
            """);

        // ── RLS em tenants (plano de tenant — DD-001, design.md §7.4) ───────
        // No plano de tenant, cada tenant vê apenas a própria linha.
        // No plano de plataforma (PlatOp), a role bypass_rls tem acesso sem SET.
        migrationBuilder.Sql("""
            ALTER TABLE tenants ENABLE ROW LEVEL SECURITY;
            """);

        migrationBuilder.Sql("""
            CREATE POLICY tenant_self_policy ON tenants
            USING (id = current_setting('app.current_tenant', true)::uuid);
            """);

        // ── outbox_events (design.md §6.6, TRD §9.5) ────────────────────────
        migrationBuilder.Sql("""
            CREATE TABLE outbox_events (
                id              UUID        NOT NULL,
                event_type      VARCHAR(100) NOT NULL,
                aggregate_type  VARCHAR(100) NOT NULL,
                aggregate_id    UUID        NOT NULL,
                tenant_id       UUID        NULL,
                correlation_id  VARCHAR(200) NULL,
                payload         JSONB       NOT NULL,
                status          VARCHAR(20) NOT NULL DEFAULT 'pending'
                                CHECK (status IN ('pending', 'published', 'failed')),
                created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
                published_at    TIMESTAMPTZ NULL,
                retry_count     INT         NOT NULL DEFAULT 0,
                last_error      TEXT        NULL,
                CONSTRAINT pk_outbox_events PRIMARY KEY (id)
            );
            """);

        migrationBuilder.Sql("""
            CREATE INDEX ix_outbox_events_status ON outbox_events (status)
            WHERE status = 'pending';
            """);

        // ── tenant_provisioning_requests (design.md §7.3, §6.5) ─────────────
        migrationBuilder.Sql("""
            CREATE TABLE tenant_provisioning_requests (
                idempotency_key          TEXT        NOT NULL,
                slug                     TEXT        NOT NULL,
                result_tenant_id         UUID        NULL,
                result_identity_tenant_id TEXT       NULL,
                status                   VARCHAR(20) NOT NULL
                                         CHECK (status IN ('in_progress', 'succeeded', 'failed')),
                created_at               TIMESTAMPTZ NOT NULL DEFAULT now(),
                CONSTRAINT pk_tenant_provisioning_requests
                    PRIMARY KEY (idempotency_key)
            );
            """);
    }

    /// <inheritdoc/>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS tenant_provisioning_requests CASCADE;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS outbox_events CASCADE;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS tenant_brandings CASCADE;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS prevent_slug_update ON tenants;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS sync_active_from_status ON tenants;");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS prevent_slug_update_fn CASCADE;");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS sync_active_from_status_fn CASCADE;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS tenants CASCADE;");
    }
}
