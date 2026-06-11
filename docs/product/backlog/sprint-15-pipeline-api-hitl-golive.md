# Sprint 15 — pipeline-api-hitl-golive

- **Objetivo:** Ao final desta sprint, o Azim CRM está operacional para go-live de Fase 1: Vendedores gerenciam o pipeline de oportunidades com Kanban em tempo real, encerramento Ganho/Perdido com snapshot imutável de comissão multimoeda (BRL/USD/EUR), parceiros vinculados com comissão por componente, detecção de estagnação ativa, e-mail com entregabilidade SPF/DKIM/DMARC configurada, e DPO tem base legal e retenção LGPD formalizadas.
- **Período:** 2026-12-28 a 2027-01-10
- **Story Points totais:** 64
- **Status:** Planejada
- **Dependências:** Sprint 14 (opportunity-pipeline domain + application; reporting API operacional), Sprint 12 (activity-management API, digest domain), Sprint 11 (organization + account + partner operacionais), Sprint 9 (IPartnerCommissionReadPort estável), Sprint 5 (Resend provider, SPF/DKIM/DMARC infra disponível)

---

## 1. Backlog da Sprint

### US-068 — Kanban de oportunidades por BU em tempo real

- **Épico:** opportunity-pipeline (EP-013)
- **RF rastreado:** Req 18
- **Story Points:** 8
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `GET /api/v1/opportunities/kanban?buId=` retorna oportunidades agrupadas por estágio com somas de `valor_total` e `forecast_ponderado` por estágio
- [ ] p95 ≤ 2.000ms para 2.000 cards (RNF 1); índice em `(tenant_id, bu_id, stage_id, is_closed)` aplicado
- [ ] Paginação por estágio (cursor-based) para >500 oportunidades por coluna
- [ ] Testes de isolamento cross-tenant via `IOpportunityRepository` (PBT-10, gate CI); nenhum card de tenant B visível para tenant A

#### Tasks técnicas (opportunity-pipeline)
- opportunity-pipeline/TASK-13: DbContext + EF mapeamentos + Global Query Filter + migrations (7 tabelas) + RLS + REVOKE — TO DO — pendente Jira
- opportunity-pipeline/TASK-14: Trigger trg_block_snapshot_mutation + constraint uq_active_snapshot + imutabilidade no banco (PBT-07) — TO DO — pendente Jira
- opportunity-pipeline/TASK-15: OpportunityNumberGenerator + lock atômico por tenant + teste de concorrência (PBT-01) — TO DO — pendente Jira
- opportunity-pipeline/TASK-16: RlsConnectionInterceptor + OutboxPublisher + AuditPublisher + PiiMasker — TO DO — pendente Jira
- opportunity-pipeline/TASK-17: Adaptadores de portas (Organization, Account, Partner, Activity) + Polly + cache de configuração — TO DO — pendente Jira
- opportunity-pipeline/TASK-18: StaleScanEndpointHandler + stale_detection_runs + idempotência de estagnação (PBT-09 infra) — TO DO — pendente Jira

#### Definition of Done
- [ ] Infrastructure.Tests ≥ 70%; PBT-01 e PBT-07 verdes; PBT-10 gate CI verde; p95 Kanban ≤ 2.000ms medido; CI verde; PR mergeado

---

### US-069 — Encerrar oportunidade como Ganha com snapshot imutável de comissão

- **Épico:** opportunity-pipeline (EP-013)
- **RF rastreado:** Req 14
- **Story Points:** 8
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `POST /api/v1/opportunities/{id}/win` executa `WinOpportunityCommand`; cria snapshot imutável de comissão por componente (MRR, setup, hardware) no banco com `trg_block_snapshot_mutation` ativo
- [ ] Quando parceiro vinculado e percentuais em branco: alerta com confirmação explícita (não bloqueia — VAL-07 HITL#1); snapshot registrado com comissão zero após confirmação
- [ ] Evento `OpportunityWon` publicado via Outbox com snapshot embutido
- [ ] Snapshot sobrevive a mudança posterior de percentuais do parceiro (PBT-07 verde)

#### Definition of Done
- [ ] PBT-07 verde; teste de imutabilidade de snapshot verde; CI verde; PR mergeado

---

### US-070 — Encerrar oportunidade como Perdida com motivo obrigatório

- **Épico:** opportunity-pipeline (EP-013)
- **RF rastreado:** Req 10
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `POST /api/v1/opportunities/{id}/lose` exige `lossReasonId` da lista configurada em organization; retorna 422 se motivo ausente ou inválido
- [ ] Evento `OpportunityLost` publicado via Outbox com `lossReasonId` e timestamp
- [ ] Oportunidade fechada como Perdida não aparece no Kanban ativo; aparece em relatório de perdas com motivo

#### Definition of Done
- [ ] Api.Tests ≥ 80%; CI verde; PR mergeado

---

### US-071 — Reabrir oportunidade fechada com re-cálculo de snapshot

- **Épico:** opportunity-pipeline (EP-013)
- **RF rastreado:** Req 15
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `POST /api/v1/opportunities/{id}/reopen` executa `ReopenOpportunityCommand`; oportunidade retorna ao último estágio aberto
- [ ] Snapshot anterior marcado como `superseded`; novo snapshot calculado ao próximo encerramento como Ganha
- [ ] Evento `OpportunityReopened` publicado; registro de auditoria documenta quem reabriu e quando
- [ ] Apenas papéis GestorBU e TAdmin podem reabrir

#### Definition of Done
- [ ] Api.Tests ≥ 80%; auditoria verificada; CI verde; PR mergeado

---

### US-072 — Vincular parceiro e definir comissão por componente

- **Épico:** opportunity-pipeline (EP-013)
- **RF rastreado:** Req 11, Req 12, Req 13
- **Story Points:** 8
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `PUT /api/v1/opportunities/{id}/commission` define percentuais por componente (MRR, setup, hardware) com `partnerId`; valida parceiro ativo via `IPartnerEligibilityPort`
- [ ] `forecast_líquido = forecast_ponderado × (1 - comissão_total_pct)` calculado em centavos (PBT-06 verde)
- [ ] Contratos de evento: `CommissionCalculated.v1` publicado via Outbox
- [ ] `GET /api/v1/opportunities/{id}` inclui `commissionSummary` (percentuais + valores calculados em centavos + currency)

#### Tasks técnicas (opportunity-pipeline)
- opportunity-pipeline/TASK-19: Contratos públicos — DTOs, requests/responses, envelopes .v1, catálogo OP-ERR-001..017 — TO DO — pendente Jira
- opportunity-pipeline/TASK-20: Controllers (OpportunitiesController, CommissionsController, KanbanController) + RBAC + ProblemDetails + OpenAPI — TO DO — pendente Jira
- opportunity-pipeline/TASK-21: InternalPipelineController + mTLS + testes RBAC 200/403 por papel × capacidade — TO DO — pendente Jira

#### Definition of Done
- [ ] PBT-06 verde; Api.Tests ≥ 80%; CI verde; PR mergeado

---

### US-074 — Vincular contatos à oportunidade com principal marcado

- **Épico:** opportunity-pipeline (EP-013)
- **RF rastreado:** Req 16
- **Story Points:** 3
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `POST /api/v1/opportunities/{id}/contacts` vincula contato de account à oportunidade; `isPrimary` pode ser definido
- [ ] `LinkContact` invariante: exatamente um contato `isPrimary=true` por oportunidade (INV-11); exceção lançada se violado
- [ ] `UnlinkContact` remove vínculo; se contato removido era principal, retorna 409 solicitando novo principal antes de desvincular
- [ ] PBT-11 verde (invariante de contato principal)

#### Definition of Done
- [ ] PBT-11 verde; Api.Tests ≥ 80%; CI verde; PR mergeado

---

### US-075 — Marcação de oportunidades estagnadas para digest

- **Épico:** opportunity-pipeline (EP-013)
- **RF rastreado:** Req 17
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `POST /internal/pipeline/scan-stale` (mTLS) dispara `StagnationDetectionService`; varre oportunidades sem atividade em N dias (N configurável por BU em organization)
- [ ] `MarkStaleCommand` publicado para cada oportunidade estagnada; `OpportunityStale.v1` publicado via Outbox para consumo pelo digest
- [ ] Scan idempotente: segunda execução no mesmo dia não duplica eventos (PBT-09 gate CI infra verde)
- [ ] `stale_detection_runs` registra execuções por tenant por data para auditoria

#### Definition of Done
- [ ] PBT-09 gate CI verde; scan idempotente verificado; CI verde; PR mergeado

---

### US-076 — Filtros customizados salvos na lista de oportunidades

- **Épico:** opportunity-pipeline (EP-013)
- **RF rastreado:** Req 19
- **Story Points:** 3
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `POST /api/v1/opportunities/filters` salva filtro customizado (nome, critérios JSON) por usuário autenticado
- [ ] `GET /api/v1/opportunities/filters` lista filtros salvos do usuário; `DELETE /api/v1/opportunities/filters/{id}` remove
- [ ] Filtros não são compartilhados entre usuários dentro do mesmo tenant

#### Definition of Done
- [ ] Api.Tests ≥ 80%; CI verde; PR mergeado

---

### US-077 — Eventos de domínio via Outbox + Pub/Sub

- **Épico:** opportunity-pipeline (EP-013)
- **RF rastreado:** Req 20
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] Todos os eventos de domínio publicados via Outbox + Pub/Sub `azim-opportunities`: `OpportunityCreated.v1`, `StageChanged.v1`, `OpportunityWon.v1`, `OpportunityLost.v1`, `OpportunityStale.v1`, `OpportunityReopened.v1`, `CommissionCalculated.v1`
- [ ] Schemas de evento validados por contract tests (Pact ou schema JSON) em `Opportunity.Contracts`
- [ ] Idempotência de consumers verificada (PBT-idempotency para cada consumer que consome `OpportunityStale.v1` no digest)

#### Tasks técnicas (opportunity-pipeline)
- opportunity-pipeline/TASK-22: Testes de isolamento cross-tenant + gate CI KPI-06 (PBT-10, RNF 3) — TO DO — pendente Jira
- opportunity-pipeline/TASK-23: Teste de carga Kanban 500–2.000 oportunidades p95 ≤ 2.000ms + índices + paginação (RNF 1) — TO DO — pendente Jira
- opportunity-pipeline/TASK-24: Observabilidade completa (logs, métricas, traces, health checks, alertas) + scan PII + DoD final — TO DO — pendente Jira

#### Definition of Done
- [ ] Contract tests verdes para todos os 7 eventos; PBT-10 gate CI verde; p95 Kanban ≤ 2.000ms confirmado; sem PII em logs; DoD assinado; CI verde; PR mergeado

---

### US-078 — Multimoeda em oportunidades e comissões (HITL#1 — ADR-0008)

- **Épico:** opportunity-pipeline (EP-013)
- **RF rastreado:** Req 8 (delta HITL#1 ADR-0008)
- **Story Points:** 8
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] VO `Money` em `OpportunityPipeline.Domain` inclui `currency` (BRL/USD/EUR); campo exposto em todos os DTOs de response e contratos de evento
- [ ] `valor_total`, `forecast_ponderado`, `forecast_líquido` calculados dentro da mesma currency; mistura de currencies rejeitada com erro estruturado OP-ERR-017
- [ ] Snapshot imutável de comissão preserva `currency` do momento do encerramento como Ganha (imutabilidade verificada via PBT-07)
- [ ] Migration idempotente adiciona coluna `currency varchar(3) not null default 'BRL'` sem breaking change nos dados existentes

#### Definition of Done
- [ ] PBT-07 e PBT-06 verdes com cenários multimoeda; migration de backfill verde; CI verde; PR mergeado

---

### US-080 — Configurar SPF, DKIM e DMARC (HITL#1 — go-live)

- **Épico:** notification-delivery (EP-002)
- **RF rastreado:** HITL#1 go-live
- **Story Points:** 3
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] Registro SPF publicado no DNS do domínio de envio incluindo os servidores Resend (ADR-0005); verificado via `dig TXT`
- [ ] DKIM ativo no painel Resend com chave de 2048 bits; verificação DKIM passando em ferramenta de e-mail (MXToolbox ou equivalente)
- [ ] Política DMARC publicada (`p=quarantine; pct=100; rua=...`) com endereço de relatório configurado
- [ ] Runbook documentado em `.forge/scripts/email-dns-setup.md` com comandos de verificação
- [ ] Teste de envio de e-mail digest com cabeçalho `Authentication-Results` mostrando SPF=pass, DKIM=pass, DMARC=pass

#### Definition of Done
- [ ] SPF/DKIM/DMARC verificados em ferramenta independente; runbook commitado; CI verde; PR mergeado

---

### US-082 — Formalizar base legal e prazo de retenção LGPD (HITL#1 — go-live)

- **Épico:** tenant-administration (EP-004)
- **RF rastreado:** HITL#1 LGPD
- **Story Points:** 3
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] Documento `.forge/legal/lgpd-data-map.md` criado com tabela: dado pessoal → base legal (legítimo interesse, execução de contrato, obrigação legal) → prazo de retenção → processo de exclusão
- [ ] Cobrir: `account.contacts` (PII contato — base: execução de contrato, retenção: duração do contrato + 5 anos), `audit_logs.delta_json` (PII mascarado — base: obrigação legal, retenção: 5 anos), `email_digest_logs` (retenção: 90 dias — já implementado no digest), `notification_delivery.send_log` (retenção: 180 dias)
- [ ] Endpoint `GET /api/v1/accounts/{id}/contacts/{contactId}/pii` (já em account-management Sprint 10) referenciado no data map como mecanismo de direito de acesso
- [ ] DPO (persona) assina o documento (campo `approved_by` + data no frontmatter do .md)

#### Definition of Done
- [ ] Documento criado e aprovado pelo DPO; commitado no repositório; referenciado em README de compliance; CI verde; PR mergeado

---

## 2. Bugs Acompanhados

| ID | Severidade | Descrição | Status | Issue |
|---|---|---|---|---|
| — | — | Nenhum registrado no início da sprint | — | — |

---

## 3. Riscos da Sprint

| Risco | Mitigação |
|---|---|
| Sprint muito pesada (64 pts) — combina infrastructure, API, hardening e go-live do opportunity-pipeline com deltas HITL | Esta é a sprint de encerramento de Fase 1; prioridade: TASK-13..18 (infra) → US-069 (Win com snapshot) → US-068 (Kanban com carga) → demais stories; US-080 e US-082 (go-live) têm menor risco técnico e podem ser executadas em paralelo por outro membro do time |
| Snapshot imutável (PBT-07) depende de trigger SQL `trg_block_snapshot_mutation` criado em TASK-14 — falha no trigger impede US-069 e US-078 | TASK-14 é o primeiro item de TASK-13..18; não iniciar TASK-20 (controllers) antes de TASK-14 verde |
| `p95 Kanban ≤ 2.000ms` com 2.000 oportunidades pode falhar sem índice correto | TASK-23 (teste de carga) executa antes de TASK-24 (DoD); se p95 falhar, abrir bug Severidade Alta e corrigir índice antes de encerrar sprint |
| SPF/DKIM/DMARC (US-080) requer acesso ao painel DNS do domínio de envio — pode depender de aprovação externa | Iniciar processo de configuração DNS no dia 1 da sprint; não bloqueia stories técnicas; sprint pode encerrar com DNS "em propagação" se runbook estiver commitado e verificação agendada |
| Multimoeda (US-078) requer migration com backfill `currency='BRL'` em tabelas existentes que podem ter dados de staging | Usar migration idempotente com `WHERE currency IS NULL`; testar em staging com dados da data-migration Sprint 13 |

---

## 4. Encerramento

- [ ] Todas as stories em DONE; todos os PRs mergeados
- [ ] Sprint encerrada no Jira; retrospectiva realizada
- [ ] **Marco de Fase 1 — GO-LIVE:** Azim CRM operacional com pipeline de oportunidades, relatórios, digest, migracao de dados legados, multimoeda BRL/USD/EUR, LGPD formalizada e e-mail com entregabilidade certificada (SPF/DKIM/DMARC)
- [ ] `progress-tracking.md` atualizado com estado final de todos os sprints
- [ ] Jira sync retomado quando MCP Atlassian autenticado (ver `progress-tracking.md §2` para lista completa de operações pendentes)
