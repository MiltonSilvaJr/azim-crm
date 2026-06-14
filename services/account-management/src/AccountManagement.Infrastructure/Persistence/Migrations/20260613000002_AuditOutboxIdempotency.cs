using Microsoft.EntityFrameworkCore.Migrations;

namespace AccountManagement.Infrastructure.Persistence.Migrations;

#pragma warning disable CA1707 // Identifiers should not contain underscores (EF Core naming convention)

/// <summary>
/// Migration de auditoria imutável, Outbox e idempotência.
///
/// Cria:
/// - <c>audit_logs</c>: append-only com trigger PL/pgSQL + REVOKE (RNF 8, design §7).
/// - <c>outbox_messages</c>: com índice parcial para relay eficiente (DD-007).
/// - <c>idempotency_keys</c>: PK composta tenant_id + idempotency_key (design §6.5).
///
/// O trigger <c>trg_audit_logs_immutable</c> bloqueia UPDATE/DELETE/TRUNCATE.
/// O <c>REVOKE UPDATE, DELETE, TRUNCATE ON audit_logs FROM app</c> remove permissões
/// do role de aplicação (menor privilégio — RNF 8.3).
///
/// Mapeia: TASK-09, design §7, RNF 8, DD-007.
/// </summary>
[Migration("20260613000002_AuditOutboxIdempotency")]
public partial class AuditOutboxIdempotency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // =====================================================================
        // Função PL/pgSQL de imutabilidade
        // =====================================================================
        migrationBuilder.Sql(@"
            CREATE OR REPLACE FUNCTION prevent_immutable_table_modification()
            RETURNS trigger AS $$
            BEGIN
                RAISE EXCEPTION 'Modificação proibida: audit_logs é append-only (RNF 8).';
                RETURN NULL;
            END;
            $$ LANGUAGE plpgsql;");

        // =====================================================================
        // Tabela audit_logs (append-only — sem updated_at — RNF 8.1)
        // =====================================================================
        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS audit_logs (
                id          UUID NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
                tenant_id   UUID NOT NULL,
                user_id     UUID NOT NULL,
                entity_type TEXT NOT NULL,
                entity_id   UUID NOT NULL,
                action      TEXT NOT NULL,
                delta_json  JSONB NOT NULL,
                created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
            );");

        migrationBuilder.Sql(@"
            CREATE INDEX IF NOT EXISTS idx_audit_logs_tenant_entity
                ON audit_logs (tenant_id, entity_type, entity_id);");

        // Trigger de imutabilidade — BEFORE UPDATE OR DELETE OR TRUNCATE (design §7)
        migrationBuilder.Sql(@"
            CREATE TRIGGER trg_audit_logs_immutable
                BEFORE UPDATE OR DELETE OR TRUNCATE ON audit_logs
                FOR EACH STATEMENT EXECUTE FUNCTION prevent_immutable_table_modification();");

        // REVOKE — role 'app' não pode UPDATE, DELETE nem TRUNCATE (RNF 8.3)
        // Nota: em PostgreSQL, REVOKE requer que o role exista.
        // Em ambiente de teste, o role é o owner — aplicamos o REVOKE condicionalmente.
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'app') THEN
                    REVOKE UPDATE, DELETE, TRUNCATE ON audit_logs FROM app;
                END IF;
            END
            $$;");

        // =====================================================================
        // Tabela outbox_messages (Outbox transacional — DD-007)
        // =====================================================================
        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS outbox_messages (
                id           UUID NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
                tenant_id    UUID NOT NULL,
                event_type   TEXT NOT NULL,
                payload_json JSONB NOT NULL,
                occurred_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
                published_at TIMESTAMPTZ
            );");

        // Índice parcial — apenas registros pendentes (published_at IS NULL)
        migrationBuilder.Sql(@"
            CREATE INDEX IF NOT EXISTS idx_outbox_unpublished
                ON outbox_messages (published_at)
                WHERE published_at IS NULL;");

        // =====================================================================
        // Tabela idempotency_keys (design §6.5)
        // =====================================================================
        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS idempotency_keys (
                tenant_id       UUID NOT NULL,
                idempotency_key TEXT NOT NULL,
                request_hash    TEXT NOT NULL,
                response_ref    UUID,
                created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
                PRIMARY KEY (tenant_id, idempotency_key)
            );");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS idempotency_keys;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS outbox_messages;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_audit_logs_immutable ON audit_logs;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS audit_logs;");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS prevent_immutable_table_modification();");
    }
}
