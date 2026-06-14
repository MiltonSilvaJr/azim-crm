using Microsoft.EntityFrameworkCore.Migrations;

namespace PartnerManagement.Infrastructure.Persistence.Migrations;

/// <summary>
/// Migration inicial: cria as tabelas <c>partners</c>, <c>outbox_messages</c> e <c>idempotency_keys</c>
/// com CHECKs, índices e RLS habilitada (gate de conformidade ADR-0001, DD-001).
///
/// Conformidade ADR-0001 — isolamento multi-tenant em defesa em profundidade:
/// - Coluna <c>tenant_id</c> obrigatória em todas as três tabelas.
/// - RLS habilitada nas três tabelas com política <c>tenant_id = current_setting('app.current_tenant')::uuid</c>.
/// - Falha-fechada: ausência de <c>SET app.current_tenant</c> resulta em nenhuma linha retornada.
///
/// Mapeia: design §7, RNF 1, DD-001, ADR-0001, TASK-16.
/// </summary>
public partial class InitialSchema : Migration
{
    /// <inheritdoc/>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // =====================================================================
        // Tabela partners
        // =====================================================================
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS partners (
                id              UUID        NOT NULL DEFAULT gen_random_uuid(),
                tenant_id       UUID        NOT NULL,
                name            TEXT        NOT NULL,
                partner_type    TEXT        NOT NULL,
                pct_setup       NUMERIC(5,2) NOT NULL DEFAULT 0,
                pct_recorrente  NUMERIC(5,2) NOT NULL DEFAULT 0,
                contact_email   TEXT,
                contact_phone   TEXT,
                notes           TEXT,
                active          BOOLEAN     NOT NULL DEFAULT TRUE,
                created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
                updated_at      TIMESTAMPTZ,
                created_by      UUID        NOT NULL,
                updated_by      UUID,

                CONSTRAINT pk_partners PRIMARY KEY (id),

                -- I1: nome não vazio após trim (design §7, Req 1.1)
                CONSTRAINT chk_partners_name_not_blank
                    CHECK (length(btrim(name)) > 0),

                -- I3: percentual de setup em [0,00; 100,00] (design §7, Req 6.3)
                CONSTRAINT chk_partners_pct_setup_range
                    CHECK (pct_setup BETWEEN 0 AND 100),

                -- I3: percentual recorrente em [0,00; 100,00]
                CONSTRAINT chk_partners_pct_recorrente_range
                    CHECK (pct_recorrente BETWEEN 0 AND 100)
            );
            """);

        // Índice para listagem de parceiros ativos por tenant (RNF 7.1, Req 4)
        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS idx_partners_tenant_active
                ON partners (tenant_id, active);
            """);

        // Índice para alerta de nome duplicado — case-insensitive, NÃO unique (Req 1.7)
        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS idx_partners_tenant_name
                ON partners (tenant_id, lower(name));
            """);

        // =====================================================================
        // RLS em partners (ADR-0001 — gate de conformidade obrigatório)
        // Política falha-fechada: ausência de app.current_tenant → nenhuma linha.
        // =====================================================================
        migrationBuilder.Sql("""
            ALTER TABLE partners ENABLE ROW LEVEL SECURITY;
            ALTER TABLE partners FORCE ROW LEVEL SECURITY;

            DROP POLICY IF EXISTS partners_tenant_isolation ON partners;
            CREATE POLICY partners_tenant_isolation ON partners
                USING (
                    tenant_id = current_setting('app.current_tenant', true)::uuid
                )
                WITH CHECK (
                    tenant_id = current_setting('app.current_tenant', true)::uuid
                );
            """);

        // =====================================================================
        // Tabela outbox_messages
        // =====================================================================
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS outbox_messages (
                id           UUID        NOT NULL DEFAULT gen_random_uuid(),
                tenant_id    UUID        NOT NULL,
                event_type   TEXT        NOT NULL,
                payload_json JSONB       NOT NULL,
                occurred_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
                published_at TIMESTAMPTZ,

                CONSTRAINT pk_outbox_messages PRIMARY KEY (id)
            );
            """);

        // Índice para o relay do Outbox — busca apenas mensagens não publicadas
        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS idx_outbox_unpublished
                ON outbox_messages (published_at)
                WHERE published_at IS NULL;
            """);

        // RLS em outbox_messages (ADR-0001)
        migrationBuilder.Sql("""
            ALTER TABLE outbox_messages ENABLE ROW LEVEL SECURITY;
            ALTER TABLE outbox_messages FORCE ROW LEVEL SECURITY;

            DROP POLICY IF EXISTS outbox_messages_tenant_isolation ON outbox_messages;
            CREATE POLICY outbox_messages_tenant_isolation ON outbox_messages
                USING (
                    tenant_id = current_setting('app.current_tenant', true)::uuid
                )
                WITH CHECK (
                    tenant_id = current_setting('app.current_tenant', true)::uuid
                );
            """);

        // =====================================================================
        // Tabela idempotency_keys
        // =====================================================================
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS idempotency_keys (
                tenant_id       UUID        NOT NULL,
                idempotency_key TEXT        NOT NULL,
                request_hash    TEXT        NOT NULL,
                response_ref    UUID,
                created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),

                CONSTRAINT pk_idempotency_keys PRIMARY KEY (tenant_id, idempotency_key)
            );
            """);

        // RLS em idempotency_keys (ADR-0001)
        migrationBuilder.Sql("""
            ALTER TABLE idempotency_keys ENABLE ROW LEVEL SECURITY;
            ALTER TABLE idempotency_keys FORCE ROW LEVEL SECURITY;

            DROP POLICY IF EXISTS idempotency_keys_tenant_isolation ON idempotency_keys;
            CREATE POLICY idempotency_keys_tenant_isolation ON idempotency_keys
                USING (
                    tenant_id = current_setting('app.current_tenant', true)::uuid
                )
                WITH CHECK (
                    tenant_id = current_setting('app.current_tenant', true)::uuid
                );
            """);

        // =====================================================================
        // Tabela __EFMigrationsHistory (necessária para EF Core migrations)
        // Criada automaticamente pelo EF Core se não existir.
        // =====================================================================
    }

    /// <inheritdoc/>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Remove RLS e tabelas na ordem inversa de dependência

        migrationBuilder.Sql("""
            DROP POLICY IF EXISTS idempotency_keys_tenant_isolation ON idempotency_keys;
            DROP TABLE IF EXISTS idempotency_keys;
            """);

        migrationBuilder.Sql("""
            DROP POLICY IF EXISTS outbox_messages_tenant_isolation ON outbox_messages;
            DROP TABLE IF EXISTS outbox_messages;
            """);

        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS idx_partners_tenant_name;
            DROP INDEX IF EXISTS idx_partners_tenant_active;
            DROP POLICY IF EXISTS partners_tenant_isolation ON partners;
            DROP TABLE IF EXISTS partners;
            """);
    }
}
