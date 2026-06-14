namespace ActivityManagement.Infrastructure.Persistence.Migrations;

using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>
/// Migration TASK-16: cria trigger de imutabilidade em <c>audit_logs</c> e revoga
/// privilégios de UPDATE, DELETE e TRUNCATE no role de aplicação.
///
/// Garantias após esta migration:
/// - <c>trg_audit_logs_immutable</c>: BEFORE UPDATE OR DELETE — lança exceção P0001
///   impedindo qualquer modificação de registros existentes (RNF 2).
/// - O trigger cobre todos os rows; é executado por row (<c>FOR EACH ROW</c>).
/// - <c>REVOKE UPDATE, DELETE, TRUNCATE ON audit_logs FROM app</c>: defence-in-depth —
///   mesmo sem o trigger, o role de aplicação não teria privilégio para modificar a tabela.
///   Nota: em testes o trigger é suficiente (o role de teste é superusuário).
///
/// Mapeia: TASK-16, RNF 2, design §6.6, DD-009.
/// </summary>
public partial class AddAuditImmutability : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── Função do trigger ─────────────────────────────────────────────────
        migrationBuilder.Sql(@"
            CREATE OR REPLACE FUNCTION fn_audit_logs_immutable()
            RETURNS TRIGGER AS $$
            BEGIN
                RAISE EXCEPTION 'audit_logs é append-only: UPDATE e DELETE não são permitidos (RNF 2)';
            END;
            $$ LANGUAGE plpgsql;
        ");

        // ── Trigger BEFORE UPDATE OR DELETE ───────────────────────────────────
        migrationBuilder.Sql(@"
            DROP TRIGGER IF EXISTS trg_audit_logs_immutable ON audit_logs;
            CREATE TRIGGER trg_audit_logs_immutable
                BEFORE UPDATE OR DELETE ON audit_logs
                FOR EACH ROW EXECUTE FUNCTION fn_audit_logs_immutable();
        ");

        // ── REVOKE no role de aplicação ───────────────────────────────────────
        // Defence-in-depth: mesmo sem o trigger, o role 'app' não poderá modificar audit_logs.
        // Em ambientes de teste o role 'app' pode não existir — o DO..END trata este caso.
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'app') THEN
                    REVOKE UPDATE, DELETE, TRUNCATE ON audit_logs FROM app;
                END IF;
            END $$;
        ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Remove trigger e função (reversível para ambientes de desenvolvimento)
        migrationBuilder.Sql(@"
            DROP TRIGGER IF EXISTS trg_audit_logs_immutable ON audit_logs;
            DROP FUNCTION IF EXISTS fn_audit_logs_immutable();
        ");

        // Nota: não recriamos o GRANT — se necessário, executar manualmente no ambiente.
    }
}
