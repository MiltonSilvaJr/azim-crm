-- Migration: M003_CreateViewRankingReport
-- TASK-15: View vw_ranking_report com security_invoker (DD-008, Req 2, RNF 4)
--
-- Mapeia: design §7.2, DD-005, DD-008, Req 2, RNF 4
--
-- display_name é PII (DD-008): exposto na view para que o handler decida inclusão
-- via PiiMinimizationPolicy. A view NÃO filtra PII — o handler o faz.
-- A view não aparece em logs de query (ADR-0001, DD-008).

CREATE OR REPLACE VIEW vw_ranking_report
WITH (security_invoker = true) AS
SELECT
    o.tenant_id,
    o.bu_id,
    o.owner_id,
    u.display_name,
    o.created_at,
    o.stage_category,
    o.valor_total          AS total_cents,
    o.forecast_ponderado   AS weighted_forecast_cents,
    o.currency
FROM opportunities o
LEFT JOIN users u ON u.id = o.owner_id;

-- display_name é PII — handler aplica PiiMinimizationPolicy antes de retornar ao cliente (DD-008).
-- Isolamento: security_invoker avalia RLS de opportunities e users com app.current_tenant.
-- Ordenação por won_value_cents desc aplicada pelo handler (Req 2.2).
