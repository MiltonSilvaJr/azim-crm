namespace ActivityManagement.Infrastructure.Persistence.Migrations;

using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>
/// Migration TASK-15: cria índices de performance e habilita RLS falha-fechada
/// em <c>activities</c> e <c>digest_action_tokens</c>.
///
/// Índices obrigatórios (design §7):
///   - <c>idx_activities_tenant_opportunity_completed</c>: (tenant_id, opportunity_id, completed_at)
///     suporta a consulta de última atividade concluída por oportunidade (RNF 4).
///   - <c>idx_activities_tenant_owner_due</c>: parcial em (tenant_id, owner_id, due_at)
///     para status não-terminal — suporta "Meu dia" e detecção de vencidas.
///
/// RLS (ADR-0001, DD-002):
///   - ENABLE ROW LEVEL SECURITY + FORCE ROW LEVEL SECURITY em ambas as tabelas.
///   - Políticas comparando tenant_id = current_setting('app.current_tenant')::uuid.
///   - Comportamento falha-fechada: sem tenant setado → zero linhas retornadas.
///   - O <c>TenantConnectionInterceptor</c> já faz SET app.current_tenant por conexão.
///
/// Nota sobre o role 'app': em produção existe o role 'app' com privilégios restritos.
///   Em desenvolvimento/testes usamos o superusuário que ignora RLS, exceto quando
///   FORCE ROW LEVEL SECURITY está ativo — o teste usa um usuário NOSUPERUSER para validar.
///
/// Mapeia: TASK-15, design §6.1, §7, §14, RNF 1, RNF 4, DD-002, ADR-0001.
/// </summary>
public partial class AddIndexesAndRls : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── Índices de performance ────────────────────────────────────────────

        // RNF 4: última atividade concluída por oportunidade
        migrationBuilder.Sql(@"
            CREATE INDEX IF NOT EXISTS idx_activities_tenant_opportunity_completed
                ON activities (tenant_id, opportunity_id, completed_at);
        ");

        // Req 5/11: visão 'meu dia' e detecção de vencidas por usuário (índice parcial)
        migrationBuilder.Sql(@"
            CREATE INDEX IF NOT EXISTS idx_activities_tenant_owner_due
                ON activities (tenant_id, owner_id, due_at)
                WHERE status IN ('pending','in_progress');
        ");

        // ── RLS em activities ─────────────────────────────────────────────────
        // NULLIF(current_setting(...), '') converte string vazia em NULL antes do cast ::uuid
        // garantindo comportamento falha-fechada quando app.current_tenant não está setado.
        migrationBuilder.Sql(@"
            ALTER TABLE activities ENABLE ROW LEVEL SECURITY;
            ALTER TABLE activities FORCE ROW LEVEL SECURITY;

            DROP POLICY IF EXISTS rls_activities_tenant ON activities;
            CREATE POLICY rls_activities_tenant ON activities
                USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);
        ");

        // ── RLS em digest_action_tokens ───────────────────────────────────────
        migrationBuilder.Sql(@"
            ALTER TABLE digest_action_tokens ENABLE ROW LEVEL SECURITY;
            ALTER TABLE digest_action_tokens FORCE ROW LEVEL SECURITY;

            DROP POLICY IF EXISTS rls_digest_action_tokens_tenant ON digest_action_tokens;
            CREATE POLICY rls_digest_action_tokens_tenant ON digest_action_tokens
                USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);
        ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Remove RLS
        migrationBuilder.Sql(@"
            DROP POLICY IF EXISTS rls_digest_action_tokens_tenant ON digest_action_tokens;
            ALTER TABLE digest_action_tokens DISABLE ROW LEVEL SECURITY;

            DROP POLICY IF EXISTS rls_activities_tenant ON activities;
            ALTER TABLE activities DISABLE ROW LEVEL SECURITY;
        ");

        // Remove índices
        migrationBuilder.Sql(@"
            DROP INDEX IF EXISTS idx_activities_tenant_owner_due;
            DROP INDEX IF EXISTS idx_activities_tenant_opportunity_completed;
        ");
    }
}
