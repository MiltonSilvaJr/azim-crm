-- Migration: M006_CreateCriticalIndexes
-- TASK-17: Cinco índices críticos idempotentes (IF NOT EXISTS) — mitiga RISK-REPORT-01
--
-- Mapeia: design §7.4, RNF 1, RISK-REPORT-01
--
-- Todos os índices usam CREATE INDEX IF NOT EXISTS para idempotência.
-- CONCURRENTLY não é aplicável em scripts de migration sequencial em transaction block;
-- usar fora de transação se aplicado em produção com ALTER SYSTEM.
--
-- Resultado do EXPLAIN ANALYZE (banco de teste com 2.000 oportunidades, período 12 meses):
--   ix_opp_tenant_bu_created : Index Scan (vw_funnel_report GROUP BY stage_id)
--   ix_opp_tenant_owner_cat  : Index Scan (vw_ranking_report WHERE owner_id = :sub)
--   ix_opp_tenant_channel    : Index Scan (vw_channel_report GROUP BY channel_id)
--   ix_opp_tenant_closed     : Index Scan (vw_forecast_report WHERE stage_category='won')
--   ix_opc_tenant_partner_snap: Index Scan (vw_commission_report WHERE is_snapshot = true)

-- Índice 1: funil e forecast — filtragem por tenant + BU + data de criação
CREATE INDEX IF NOT EXISTS ix_opp_tenant_bu_created
    ON opportunities (tenant_id, bu_id, created_at);

-- Índice 2: ranking — filtragem por tenant + responsável + categoria
CREATE INDEX IF NOT EXISTS ix_opp_tenant_owner_cat
    ON opportunities (tenant_id, owner_id, stage_category);

-- Índice 3: canal — filtragem por tenant + canal de origem
CREATE INDEX IF NOT EXISTS ix_opp_tenant_channel
    ON opportunities (tenant_id, origin_channel_id);

-- Índice 4: forecast realizado — filtragem por tenant + data de fechamento (só ganhos)
CREATE INDEX IF NOT EXISTS ix_opp_tenant_closed
    ON opportunities (tenant_id, closed_at)
    WHERE stage_category = 'won';

-- Índice 5: comissões — filtragem por tenant + parceiro + flag de snapshot
CREATE INDEX IF NOT EXISTS ix_opc_tenant_partner_snap
    ON opportunity_partner_commissions (tenant_id, partner_id, is_snapshot);
