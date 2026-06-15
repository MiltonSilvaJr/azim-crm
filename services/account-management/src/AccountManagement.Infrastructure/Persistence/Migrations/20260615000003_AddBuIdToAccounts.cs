using Microsoft.EntityFrameworkCore.Migrations;

namespace AccountManagement.Infrastructure.Persistence.Migrations;

#pragma warning disable CA1707 // Identifiers should not contain underscores (EF Core naming convention)

/// <summary>
/// Migration ADR-0009 — adiciona <c>bu_id</c> à tabela <c>accounts</c>.
///
/// Alterações:
/// - Coluna <c>bu_id uuid NOT NULL</c> em <c>accounts</c>.
///   Greenfield (sem dados): NOT NULL aplicável sem backfill.
/// - Índice <c>idx_accounts_tenant_bu</c> em <c>(tenant_id, bu_id)</c> para queries de escopo de BU.
/// - Atualiza a RLS policy de <c>accounts</c> para incluir restrição de BU:
///   <c>tenant_id = current_tenant AND (app.bu_tenant_wide = true OR bu_id = ANY(app.current_bu_scope))</c>.
/// - Cria função auxiliar <c>current_bu_scope_array()</c> que converte a variável de sessão
///   <c>app.current_bu_scope</c> (text — lista separada por vírgula de uuids) para uuid[].
///
/// Fail-closed: escopo vazio + não-tenant-wide ⇒ nenhuma linha retornada.
///
/// Mapeia: ADR-0009, VAL-ACC-03, design §7, TASK-08 (revisado).
/// </summary>
[Migration("20260615000003_AddBuIdToAccounts")]
public partial class AddBuIdToAccounts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // =====================================================================
        // 1. Adicionar coluna bu_id em accounts (NOT NULL — greenfield)
        // =====================================================================
        migrationBuilder.Sql(@"
            ALTER TABLE accounts
                ADD COLUMN IF NOT EXISTS bu_id UUID NOT NULL DEFAULT gen_random_uuid();");

        // Remover o DEFAULT após a adição (NOT NULL sem DEFAULT em runtime — ADR-0009)
        migrationBuilder.Sql(@"
            ALTER TABLE accounts
                ALTER COLUMN bu_id DROP DEFAULT;");

        // =====================================================================
        // 2. Índice (tenant_id, bu_id) para queries de escopo de BU
        // =====================================================================
        migrationBuilder.Sql(@"
            CREATE INDEX IF NOT EXISTS idx_accounts_tenant_bu
                ON accounts (tenant_id, bu_id);");

        // =====================================================================
        // 3. Função auxiliar para converter app.current_bu_scope → uuid[]
        //
        // A variável de sessão é armazenada como text (lista de uuids separada por
        // vírgula) porque PostgreSQL não permite SET com arrays nativos.
        // Esta função converte para uuid[] para usar com o operador = ANY().
        // =====================================================================
        migrationBuilder.Sql(@"
            CREATE OR REPLACE FUNCTION current_bu_scope_array()
            RETURNS uuid[] AS $$
            DECLARE
                raw_scope TEXT;
            BEGIN
                raw_scope := current_setting('app.current_bu_scope', TRUE);
                IF raw_scope IS NULL OR raw_scope = '' THEN
                    RETURN ARRAY[]::uuid[];
                END IF;
                RETURN string_to_array(raw_scope, ',')::uuid[];
            END;
            $$ LANGUAGE plpgsql STABLE SECURITY DEFINER;");

        // =====================================================================
        // 4. Habilitar RLS e (re)criar policy em accounts com restrição de BU
        //
        // Policy: tenant_id = current_tenant AND (bu_tenant_wide OR bu_id = ANY(bu_scope))
        // Fail-closed: se app.current_bu_scope = '' e app.bu_tenant_wide != 'true' → zero linhas.
        // =====================================================================
        migrationBuilder.Sql(@"
            ALTER TABLE accounts ENABLE ROW LEVEL SECURITY;
            ALTER TABLE accounts FORCE ROW LEVEL SECURITY;");

        migrationBuilder.Sql(@"
            DROP POLICY IF EXISTS tenant_isolation ON accounts;
            DROP POLICY IF EXISTS bu_scope_isolation ON accounts;

            CREATE POLICY bu_scope_isolation ON accounts
                AS PERMISSIVE
                FOR ALL
                USING (
                    tenant_id = NULLIF(current_setting('app.current_tenant', TRUE), '')::uuid
                    AND (
                        current_setting('app.bu_tenant_wide', TRUE) = 'true'
                        OR bu_id = ANY(current_bu_scope_array())
                    )
                );");

        // =====================================================================
        // 5. Habilitar RLS em contacts com restrição de tenant
        //    (contacts são acessados via conta-mãe; o filtro de BU é herdado
        //     pela FK — não duplicar a lógica de BU em contacts: ADR-0009)
        // =====================================================================
        migrationBuilder.Sql(@"
            ALTER TABLE contacts ENABLE ROW LEVEL SECURITY;
            ALTER TABLE contacts FORCE ROW LEVEL SECURITY;");

        migrationBuilder.Sql(@"
            DROP POLICY IF EXISTS tenant_isolation ON contacts;

            CREATE POLICY tenant_isolation ON contacts
                AS PERMISSIVE
                FOR ALL
                USING (
                    tenant_id = NULLIF(current_setting('app.current_tenant', TRUE), '')::uuid
                );");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Remover policies e RLS
        migrationBuilder.Sql(@"
            DROP POLICY IF EXISTS bu_scope_isolation ON accounts;
            ALTER TABLE accounts DISABLE ROW LEVEL SECURITY;");

        migrationBuilder.Sql(@"
            DROP POLICY IF EXISTS tenant_isolation ON contacts;
            ALTER TABLE contacts DISABLE ROW LEVEL SECURITY;");

        // Remover função auxiliar
        migrationBuilder.Sql(@"
            DROP FUNCTION IF EXISTS current_bu_scope_array();");

        // Remover índice e coluna
        migrationBuilder.Sql(@"
            DROP INDEX IF EXISTS idx_accounts_tenant_bu;");

        migrationBuilder.Sql(@"
            ALTER TABLE accounts DROP COLUMN IF EXISTS bu_id;");
    }
}
