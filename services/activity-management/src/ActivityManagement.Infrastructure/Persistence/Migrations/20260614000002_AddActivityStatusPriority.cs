namespace ActivityManagement.Infrastructure.Persistence.Migrations;

using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>
/// Migration DD-001: adiciona <c>status</c> e <c>priority</c> à tabela <c>activities</c>
/// com backfill seguro, constraints de domínio e índices de performance (design §7).
/// Também finaliza a estrutura de <c>digest_action_tokens</c> garantindo o índice único
/// em <c>token_hash</c> (DD-003, RNF 5).
///
/// Backfill:
///   - Linhas com <c>completed_at IS NOT NULL</c> → <c>status = 'completed'</c>.
///   - Demais linhas → <c>status = 'pending'</c>.
///
/// Constraint I5/PBT-01:
///   <c>chk_activities_completed_consistency</c>: (status = 'completed') = (completed_at IS NOT NULL).
///
/// Mapeia: DD-001, design §7, Req 4, TASK-14.
/// </summary>
public partial class AddActivityStatusPriority : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── 1. Adicionar coluna status nullable temporariamente para backfill ──
        migrationBuilder.Sql(@"
            ALTER TABLE activities
                ADD COLUMN IF NOT EXISTS status   VARCHAR(20),
                ADD COLUMN IF NOT EXISTS priority VARCHAR(10);
        ");

        // ── 2. Backfill: status baseado em completed_at ───────────────────────
        // Executado em lotes para suportar tabelas grandes (RE-01)
        migrationBuilder.Sql(@"
            -- Backfill: linhas com completed_at preenchido → 'completed'
            UPDATE activities
               SET status = 'completed'
             WHERE completed_at IS NOT NULL
               AND status IS NULL;

            -- Backfill: demais linhas → 'pending'
            UPDATE activities
               SET status = 'pending'
             WHERE status IS NULL;

            -- Backfill de priority para linhas sem valor
            UPDATE activities
               SET priority = 'medium'
             WHERE priority IS NULL;
        ");

        // ── 3. Tornar NOT NULL e adicionar defaults ───────────────────────────
        migrationBuilder.Sql(@"
            ALTER TABLE activities
                ALTER COLUMN status   SET NOT NULL,
                ALTER COLUMN status   SET DEFAULT 'pending',
                ALTER COLUMN priority SET NOT NULL,
                ALTER COLUMN priority SET DEFAULT 'medium';
        ");

        // ── 4. Constraints de domínio ─────────────────────────────────────────
        migrationBuilder.Sql(@"
            ALTER TABLE activities
                ADD CONSTRAINT chk_activities_status
                    CHECK (status IN ('pending','in_progress','completed','cancelled')),
                ADD CONSTRAINT chk_activities_priority
                    CHECK (priority IN ('low','medium','high')),
                -- I5/PBT-01: completed_at preenchido sse status='completed'
                ADD CONSTRAINT chk_activities_completed_consistency
                    CHECK ((status = 'completed') = (completed_at IS NOT NULL));
        ");

        // ── 5. Índice único em token_hash (já criado no Initial, garante existência) ──
        migrationBuilder.Sql(@"
            CREATE UNIQUE INDEX IF NOT EXISTS uq_digest_action_tokens_hash
                ON digest_action_tokens (token_hash);
        ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            ALTER TABLE activities
                DROP CONSTRAINT IF EXISTS chk_activities_completed_consistency,
                DROP CONSTRAINT IF EXISTS chk_activities_priority,
                DROP CONSTRAINT IF EXISTS chk_activities_status,
                DROP COLUMN IF EXISTS priority,
                DROP COLUMN IF EXISTS status;

            DROP INDEX IF EXISTS uq_digest_action_tokens_hash;
        ");
    }
}
