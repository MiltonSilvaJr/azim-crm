-- Migration: M001_CreateViewFunnelReport
-- TASK-13: View vw_funnel_report com security_invoker e migration idempotente
--
-- Mapeia: design §7.2, DD-005, ADR-0001, Req 1, Req 8, RISK-REPORT-03, RISK-REPORT-06
--
-- Requer PostgreSQL 15+ para suporte a security_invoker em views (RISK-REPORT-06).
-- Alternativa para PG < 15: WHERE tenant_id = current_setting('app.current_tenant')::uuid
-- aplicada explicitamente na view — documentada como fallback em design §7.2.
--
-- A RLS das tabelas base (opportunities, stages) é avaliada com o app.current_tenant
-- do chamador graças ao security_invoker=true. A view NUNCA escapa do isolamento.
-- Isolamento confirmado por PBT-03 (TASK-18).

CREATE OR REPLACE VIEW vw_funnel_report
WITH (security_invoker = true) AS
SELECT
    o.tenant_id,
    o.bu_id,
    o.owner_id,
    s.id            AS stage_id,
    s.name          AS stage_name,
    s.category      AS stage_category,
    o.created_at,
    o.valor_total          AS total_cents,
    o.forecast_ponderado   AS weighted_forecast_cents
FROM opportunities o
JOIN stages s ON s.id = o.stage_id;

-- Agregação (count, SUM) e predicado de período/escopo RBAC são feitos
-- na query do handler com GROUP BY stage_id (design §7.2, DD-006).
-- EXPLAIN ANALYZE confirma uso de ix_opp_tenant_bu_created (TASK-17).
