-- Migration: M002_CreateViewForecastReport
-- TASK-14: View vw_forecast_report com security_invoker e tratamento de meta ausente
--
-- Mapeia: design §7.2, DD-005, Req 6, Req 6.3
--
-- LEFT JOIN goals garante que BUs sem meta cadastrada retornam goal_cents = NULL
-- (degradação graciosa, Req 6.3, P8) — nunca exclui a linha.
-- A ausência de meta NÃO gera erro — o handler preserva null sem substituir por zero.

CREATE OR REPLACE VIEW vw_forecast_report
WITH (security_invoker = true) AS
SELECT
    o.tenant_id,
    o.bu_id,
    o.owner_id,
    EXTRACT(YEAR  FROM o.closed_at)::int AS year,
    EXTRACT(MONTH FROM o.closed_at)::int AS month,
    o.forecast_ponderado                  AS weighted_forecast_cents,
    CASE WHEN o.stage_category = 'won'
         THEN o.valor_total
         ELSE 0
    END                                   AS realized_cents,
    g.goal_cents,
    o.currency
FROM opportunities o
LEFT JOIN goals g
    ON  g.tenant_id = o.tenant_id
    AND g.bu_id     = o.bu_id
    AND EXTRACT(YEAR  FROM o.closed_at) = g.goal_year
    AND EXTRACT(MONTH FROM o.closed_at) = g.goal_month;

-- goal_cents é NULL quando não há meta cadastrada para a BU/mês (Req 6.3).
-- O handler agrega weighted_forecast_cents e realized_cents por bu_id/year/month.
-- Isolamento por security_invoker: RLS de opportunities e goals aplicada com app.current_tenant.
