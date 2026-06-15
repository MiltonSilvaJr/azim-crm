namespace ActivityManagement.Infrastructure.Persistence.Migrations;

using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>
/// Migration inicial: cria as tabelas base do módulo activity-management.
/// - <c>activities</c>: agregado Activity (sem status/priority ainda — DD-001 as adiciona na próxima migration).
/// - <c>digest_action_tokens</c>: tokens do digest (DD-003).
/// - <c>outbox_messages</c>: outbox transacional (DD-007).
/// - <c>audit_logs</c>: auditoria imutável (RNF 2).
/// Mapeia: design §7, TASK-13.
/// </summary>
public partial class Initial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── activities ────────────────────────────────────────────────────────
        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS activities (
                id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                tenant_id       UUID NOT NULL,
                bu_id           UUID NOT NULL,
                owner_id        UUID NOT NULL,
                opportunity_id  UUID,
                account_id      UUID,
                activity_type   VARCHAR(20) NOT NULL,
                title           TEXT NOT NULL,
                description     TEXT,
                due_at          TIMESTAMPTZ NOT NULL,
                completed_at    TIMESTAMPTZ,
                created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
                updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
                CONSTRAINT chk_activities_title_not_blank CHECK (length(btrim(title)) > 0),
                CONSTRAINT chk_activities_type CHECK (activity_type IN ('meeting','follow_up','call','email','task'))
            );
        ");

        // ── digest_action_tokens ──────────────────────────────────────────────
        // ADR-0006: a tabela digest_action_tokens é owned exclusivamente pelo digest (BC-06).
        // O activity-management é somente consumidor — não cria nem altera esta tabela.
        // A DDL, RLS e índices são responsabilidade das migrations do Digest.Infrastructure.
        // A tabela deve existir no banco compartilhado ANTES que este serviço seja deployado.

        // ── outbox_messages ───────────────────────────────────────────────────
        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS outbox_messages (
                id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                tenant_id       UUID NOT NULL,
                event_type      TEXT NOT NULL,
                dedup_key       TEXT,
                payload_json    TEXT NOT NULL,
                occurred_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
                published_at    TIMESTAMPTZ
            );
        ");

        // ── audit_logs ────────────────────────────────────────────────────────
        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS audit_logs (
                id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                tenant_id       UUID NOT NULL,
                user_id         UUID,
                entity_type     TEXT NOT NULL,
                entity_id       UUID NOT NULL,
                action          TEXT NOT NULL,
                delta_json      TEXT NOT NULL,
                correlation_id  UUID,
                created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
            );
        ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS audit_logs CASCADE;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS outbox_messages CASCADE;");
        // digest_action_tokens NÃO é dropada aqui — owned pelo digest (ADR-0006).
        migrationBuilder.Sql("DROP TABLE IF EXISTS activities CASCADE;");
    }
}
