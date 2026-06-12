using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditLog.Infrastructure.Migrations
{
    /// <summary>
    /// Migration que cria a tabela <c>audit_logs</c> com garantias de imutabilidade
    /// (trigger + REVOKE + RLS) conforme design §7.2 e
    /// <c>.forge/rules/domain/audit-immutability.md</c>.
    ///
    /// Partes obrigatórias:
    /// 1. Tabela + CHECK constraints (action, entity_type).
    /// 2. Função <c>prevent_immutable_table_modification</c> + trigger <c>trg_audit_logs_immutable</c>.
    /// 3. REVOKE UPDATE/DELETE/TRUNCATE do role <c>app</c>.
    /// 4. RLS ENABLE + FORCE + policy <c>rls_audit_logs_tenant</c>.
    /// </summary>
    public partial class CreateImmutableAuditLogs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ------------------------------------------------------------------ 1. Tabela + CHECKs

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    delta_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);

                    // CHECK: action deve ser um dos valores canônicos (REQ-002.3)
                    table.CheckConstraint(
                        "chk_audit_logs_action",
                        "action IN ('create','update','delete')");

                    // CHECK: entity_type não pode ser vazio (REQ-002.1)
                    table.CheckConstraint(
                        "chk_audit_logs_entity_type_not_empty",
                        "char_length(entity_type) > 0");
                });

            // ------------------------------------------------------------------ Índices (design §7.1)

            // (tenant_id, entity_type, entity_id) — REQ-007.3
            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_tenant_entity",
                table: "audit_logs",
                columns: new[] { "tenant_id", "entity_type", "entity_id" });

            // (tenant_id, created_at DESC) — REQ-007.4
            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_tenant_created",
                table: "audit_logs",
                columns: new[] { "tenant_id", "created_at" },
                descending: new[] { false, true });

            // (tenant_id, user_id) — REQ-007.2
            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_tenant_user",
                table: "audit_logs",
                columns: new[] { "tenant_id", "user_id" });

            // ------------------------------------------------------------------ 2. Trigger de imutabilidade (DD-002, RNF-001)

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION prevent_immutable_table_modification()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE EXCEPTION 'Tabela imutavel: operacao % proibida em %.%',
                        TG_OP, TG_TABLE_SCHEMA, TG_TABLE_NAME;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_audit_logs_immutable
                BEFORE UPDATE OR DELETE OR TRUNCATE ON audit_logs
                FOR EACH STATEMENT EXECUTE FUNCTION prevent_immutable_table_modification();
                """);

            // ------------------------------------------------------------------ 3. REVOKE no role app (RNF-001, RNF-005)
            // Concede INSERT e SELECT primeiro, revoga operações de mutação.
            // IF EXISTS previne falha quando o role ainda não existe (ex.: ambiente de CI sem role app).

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'app') THEN
                        GRANT INSERT, SELECT ON audit_logs TO app;
                        REVOKE UPDATE, DELETE, TRUNCATE ON audit_logs FROM app;
                    END IF;
                END
                $$;
                """);

            // ------------------------------------------------------------------ 4. RLS por tenant_id (REQ-005, DD-003)

            migrationBuilder.Sql("ALTER TABLE audit_logs ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE audit_logs FORCE ROW LEVEL SECURITY;");

            migrationBuilder.Sql("""
                CREATE POLICY rls_audit_logs_tenant ON audit_logs
                    USING (tenant_id = current_setting('app.tenant_id', true)::uuid);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove policy RLS antes de dropar a tabela
            migrationBuilder.Sql(
                "DROP POLICY IF EXISTS rls_audit_logs_tenant ON audit_logs;");

            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS trg_audit_logs_immutable ON audit_logs;");

            migrationBuilder.DropTable(name: "audit_logs");

            // Não remove a função prevent_immutable_table_modification
            // pois pode ser compartilhada por outras tabelas imutáveis do banco
        }
    }
}
