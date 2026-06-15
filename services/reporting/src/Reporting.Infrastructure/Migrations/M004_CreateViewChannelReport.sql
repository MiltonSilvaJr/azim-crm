-- Migration: M004_CreateViewChannelReport
-- TASK-15: View vw_channel_report com security_invoker (Req 3, sem PII)
--
-- Mapeia: design §7.2, DD-005, Req 3
--
-- Sem colunas de PII (DD-008): a view de canal não expõe display_name nem dados pessoais.
-- basis points calculados pelo handler via ChannelShare (DD-010, PBT-04).

CREATE OR REPLACE VIEW vw_channel_report
WITH (security_invoker = true) AS
SELECT
    o.tenant_id,
    o.bu_id,
    o.owner_id,
    o.origin_channel_id    AS channel_id,
    oc.name                AS channel_name,
    o.created_at,
    o.valor_total          AS total_cents,
    o.currency
FROM opportunities o
LEFT JOIN origin_channels oc ON oc.id = o.origin_channel_id;

-- Sem PII (DD-008): channel_name é nome de canal de marketing, não dados pessoais.
-- percentBasisPoints calculados pelo handler com aritmética inteira (DD-010, PBT-04).
-- Isolamento: security_invoker avalia RLS de opportunities com app.current_tenant.
