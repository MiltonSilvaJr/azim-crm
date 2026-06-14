using Microsoft.EntityFrameworkCore.Migrations;

namespace AccountManagement.Infrastructure.Persistence.Migrations;

#pragma warning disable CA1707 // Identifiers should not contain underscores (EF Core naming convention)

/// <summary>
/// Migration inicial — cria tabelas <c>accounts</c> e <c>contacts</c>.
///
/// Schema conforme design §7:
/// - <c>accounts</c>: sem <c>bu_id</c> (Req 2.3); índice não-unique de dedupe (DD-006).
/// - <c>contacts</c>: com <c>privacy_state</c>; constraint de check ativo.
///
/// Mapeia: TASK-08 (ST-03), design §7.
/// </summary>
[Migration("20260613000001_InitialAccountsContacts")]
public partial class InitialAccountsContacts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // =====================================================================
        // Tabela accounts
        // =====================================================================
        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS accounts (
                id              UUID NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
                tenant_id       UUID NOT NULL,
                name            TEXT NOT NULL,
                normalized_name TEXT NOT NULL,
                website         TEXT,
                notes           TEXT,
                created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
                updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
                CONSTRAINT chk_accounts_name_not_blank CHECK (length(btrim(name)) > 0)
            );");

        // Índice de dedupe/busca por nome normalizado — NÃO unique (DD-006, RNF 7.1)
        migrationBuilder.Sql(@"
            CREATE INDEX IF NOT EXISTS idx_accounts_tenant_normalized_name
                ON accounts (tenant_id, normalized_name);");

        // =====================================================================
        // Tabela contacts
        // =====================================================================
        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS contacts (
                id              UUID NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
                tenant_id       UUID NOT NULL,
                account_id      UUID NOT NULL REFERENCES accounts(id),
                name            TEXT NOT NULL,
                email           TEXT,
                phone           TEXT,
                role            TEXT,
                privacy_state   VARCHAR(16) NOT NULL DEFAULT 'active',
                forgotten_at    TIMESTAMPTZ,
                forgotten_by    UUID,
                created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
                updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
                CONSTRAINT chk_contacts_privacy_state CHECK (privacy_state IN ('active','anonymized'))
            );");

        migrationBuilder.Sql(@"
            CREATE INDEX IF NOT EXISTS idx_contacts_tenant_account
                ON contacts (tenant_id, account_id);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS contacts;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS accounts;");
    }
}
