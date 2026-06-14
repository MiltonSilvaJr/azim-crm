using Microsoft.EntityFrameworkCore.Migrations;

namespace Digest.Infrastructure.Persistence.Migrations;

/// <summary>
/// Migration inicial do módulo digest.
/// Cria as tabelas <c>email_digest_logs</c>, <c>digest_action_tokens</c> e <c>outbox_messages</c>
/// conforme schema do design §7.
/// NOTA: RLS é aplicada por script idempotente separado (design §6.1, ADR-0001).
/// </summary>
public partial class InitialDigestSchema : Migration
{
    /// <inheritdoc/>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ---------------------------------------------------------------
        // email_digest_logs — idempotência (RN-010) e trilha de entregabilidade (KPI-03/04)
        // ---------------------------------------------------------------
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS email_digest_logs (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL,
    user_id         UUID NOT NULL,
    digest_date     DATE NOT NULL,
    status          VARCHAR(20) NOT NULL,
    message_id      TEXT,
    correlation_id  UUID,
    scheduled_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    sent_at         TIMESTAMPTZ,
    delivered_at    TIMESTAMPTZ,
    opened_at       TIMESTAMPTZ,
    failed_at       TIMESTAMPTZ,
    CONSTRAINT uq_email_digest_logs_tenant_user_date
        UNIQUE (tenant_id, user_id, digest_date),
    CONSTRAINT ck_email_digest_logs_status
        CHECK (status IN ('Scheduled','Sent','Delivered','Opened','Bounced','Failed'))
);
");

        migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_email_digest_logs_tenant_date
    ON email_digest_logs (tenant_id, digest_date);
");

        migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_email_digest_logs_status
    ON email_digest_logs (tenant_id, status);
");

        // ---------------------------------------------------------------
        // digest_action_tokens — token de 1 clique; persiste apenas o HASH (DD-007)
        // ---------------------------------------------------------------
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS digest_action_tokens (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL,
    user_id         UUID NOT NULL,
    activity_id     UUID NOT NULL,
    action          VARCHAR(20) NOT NULL,
    token_hash      BYTEA NOT NULL,
    expires_at      TIMESTAMPTZ NOT NULL,
    used_at         TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_digest_action_tokens_hash UNIQUE (token_hash),
    CONSTRAINT ck_digest_action_tokens_action CHECK (action IN ('Complete','Reschedule'))
);
");

        migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_digest_action_tokens_expires
    ON digest_action_tokens (expires_at);
");

        // ---------------------------------------------------------------
        // outbox_messages — Outbox transacional (ADR-0004, DD-009)
        // ---------------------------------------------------------------
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS outbox_messages (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    event_type      VARCHAR(200) NOT NULL,
    payload         JSONB NOT NULL,
    occurred_at     TIMESTAMPTZ NOT NULL,
    processed_at    TIMESTAMPTZ,
    tenant_id       UUID NOT NULL
);
");

        migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_outbox_messages_pending
    ON outbox_messages (processed_at, occurred_at);
");
    }

    /// <inheritdoc/>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS outbox_messages CASCADE;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS digest_action_tokens CASCADE;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS email_digest_logs CASCADE;");
    }
}
