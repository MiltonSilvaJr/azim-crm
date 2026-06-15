-- Migration: M005_CreateViewCommissionReport
-- TASK-16: View vw_commission_report com security_invoker e distinção snapshot/projetado
--
-- Mapeia: design §7.2, DD-005, Req 4, Req 4.2, PBT-01, RN-007
--
-- is_snapshot exposto como booleano confiável para o handler distinguir:
--   consolidado = SUM(commission_cents) WHERE is_snapshot = true  (oportunidades Ganhas)
--   projetado   = SUM(commission_cents) WHERE is_snapshot = false AND stage_category = 'open'
--
-- A view NÃO agrega — expõe linhas brutas para que o handler aplique PBT-01 (snapshot imutável).
-- comissao_consolidada vem EXCLUSIVAMENTE de is_snapshot=true (RN-007, Req 4.2).

CREATE OR REPLACE VIEW vw_commission_report
WITH (security_invoker = true) AS
SELECT
    c.tenant_id,
    c.partner_id,
    p.name          AS partner_name,
    o.bu_id,
    o.owner_id,
    o.created_at,
    o.stage_category,
    c.comissao_calculada AS commission_cents,
    c.is_snapshot,
    o.currency
FROM opportunity_partner_commissions c
JOIN opportunities o ON o.id = c.opportunity_id
JOIN partners p      ON p.id = c.partner_id;

-- is_snapshot: boolean confiável (sem cast frágil — coluna booleana nativa no schema).
-- Isolamento: security_invoker avalia RLS de opportunity_partner_commissions, opportunities
-- e partners com app.current_tenant do chamador.
-- O handler soma is_snapshot=true para consolidado e is_snapshot=false+stage_category='open'
-- para projetado (Req 4.2, Req 4.3, PBT-01).
