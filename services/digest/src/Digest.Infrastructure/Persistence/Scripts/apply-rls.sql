-- Script idempotente de RLS para o módulo digest
-- Executado após a migration inicial (design §6.1, ADR-0001, DD-002)
-- Falha-fechada: FORCE ROW LEVEL SECURITY bloqueia até o proprietário da tabela sem SET

-- ---------------------------------------------------------------
-- email_digest_logs
-- ---------------------------------------------------------------
ALTER TABLE email_digest_logs ENABLE ROW LEVEL SECURITY;
ALTER TABLE email_digest_logs FORCE ROW LEVEL SECURITY;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_policies
        WHERE tablename = 'email_digest_logs'
          AND policyname = 'p_email_digest_logs_tenant'
    ) THEN
        CREATE POLICY p_email_digest_logs_tenant ON email_digest_logs
            USING (tenant_id = current_setting('app.current_tenant')::uuid)
            WITH CHECK (tenant_id = current_setting('app.current_tenant')::uuid);
    END IF;
END;
$$;

-- ---------------------------------------------------------------
-- digest_action_tokens
-- ---------------------------------------------------------------
ALTER TABLE digest_action_tokens ENABLE ROW LEVEL SECURITY;
ALTER TABLE digest_action_tokens FORCE ROW LEVEL SECURITY;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_policies
        WHERE tablename = 'digest_action_tokens'
          AND policyname = 'p_digest_action_tokens_tenant'
    ) THEN
        CREATE POLICY p_digest_action_tokens_tenant ON digest_action_tokens
            USING (tenant_id = current_setting('app.current_tenant')::uuid)
            WITH CHECK (tenant_id = current_setting('app.current_tenant')::uuid);
    END IF;
END;
$$;
