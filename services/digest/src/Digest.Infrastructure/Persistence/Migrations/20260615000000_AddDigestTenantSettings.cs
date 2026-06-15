using Microsoft.EntityFrameworkCore.Migrations;

namespace Digest.Infrastructure.Persistence.Migrations;

/// <summary>
/// Migration VAL-ACT-02 — Cria a tabela <c>digest_tenant_settings</c>.
/// Permite configurar o TTL do action token por tenant;
/// quando ausente, o default global de 48h é aplicado pela camada Application.
/// RLS aplicada inline, idempotente (ADR-0001).
/// </summary>
public partial class AddDigestTenantSettings : Migration
{
    /// <inheritdoc/>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ---------------------------------------------------------------
        // digest_tenant_settings — configuração de digest por tenant (VAL-ACT-02)
        // PK: tenant_id (um registro por tenant, opcional)
        // ---------------------------------------------------------------
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS digest_tenant_settings (
    tenant_id               UUID PRIMARY KEY,
    action_token_ttl_hours  INTEGER NOT NULL,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_digest_tenant_settings_ttl CHECK (action_token_ttl_hours > 0)
);
");

        // ---------------------------------------------------------------
        // RLS — isolamento por tenant (ADR-0001)
        // Idempotente: CREATE POLICY apenas se não existir.
        // Política fail-closed: NULLIF trata current_setting vazio como NULL,
        // retornando 0 linhas quando o SET app.current_tenant não foi executado.
        // ---------------------------------------------------------------
        migrationBuilder.Sql(@"
ALTER TABLE digest_tenant_settings ENABLE ROW LEVEL SECURITY;
ALTER TABLE digest_tenant_settings FORCE ROW LEVEL SECURITY;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_policies
        WHERE tablename = 'digest_tenant_settings'
          AND policyname = 'p_digest_tenant_settings_tenant'
    ) THEN
        CREATE POLICY p_digest_tenant_settings_tenant ON digest_tenant_settings
            USING (tenant_id = NULLIF(current_setting('app.current_tenant', TRUE), '')::uuid)
            WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', TRUE), '')::uuid);
    END IF;
END;
$$;
");
    }

    /// <inheritdoc/>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS digest_tenant_settings CASCADE;");
    }
}
