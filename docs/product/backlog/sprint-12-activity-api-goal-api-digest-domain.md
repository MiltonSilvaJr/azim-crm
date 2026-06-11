# Sprint 12 — activity-api-goal-api-digest-domain

- **Objetivo:** Ao final desta sprint, links de ação de 1 clique do digest (concluir/reagendar atividade sem abrir o CRM) estão operacionais, a detecção de oportunidades estagnadas publica OpportunityStale, o painel de forecast individual do vendedor está disponível, e o digest worker tem seu domínio completo com seleção de destinatários por fuso e composição de conteúdo, viabilizando o primeiro e-mail digest de teste enviado para a equipe.
- **Período:** 2026-11-16 a 2026-11-29
- **Story Points totais:** 38
- **Status:** Planejada
- **Dependências:** Sprint 11 (activity e goal-forecast application), Sprint 9 (notification-delivery IEmailSender disponível)

---

## 1. Backlog da Sprint

### US-041 — Links de ação de 1 clique no digest

- **Épico:** activity-management (EP-008)
- **RF rastreado:** RF act-digest-action
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `POST /api/v1/activities/digest-actions` processa token de ação (complete/reschedule) em tempo constante (anti-enumeração, PBT-03 verde)
- [ ] Token de uso único; link consumido ou expirado retorna 410 com mensagem genérica
- [ ] `ActivitiesController` e `DigestActionsController` com middleware, OpenAPI e catálogo de erros ACT-ERR completo
- [ ] Contratos de evento `activity.completed.v1`, `activity.rescheduled.v1` e `activity.overdue.v1` com Pact provider tests

#### Tasks técnicas
- activity-management/TASK-12: Queries de sistema e ScanOverdueActivities — TO DO — pendente Jira
- activity-management/TASK-13: EF Core DbContext + ActivityRepository + migrations base — TO DO — pendente Jira
- activity-management/TASK-14: Migration DD-001 (status/priority, backfill, constraints, token_hash) — TO DO — pendente Jira
- activity-management/TASK-15: Índices, RLS falha-fechada e TenantConnectionInterceptor — TO DO — pendente Jira
- activity-management/TASK-16: Outbox transacional + PiiMasker + AuditPublisher — TO DO — pendente Jira
- activity-management/TASK-17: DigestActionTokenAdapter e adapters de leitura (Opportunity, Account) — TO DO — pendente Jira
- activity-management/TASK-18: ActivitiesController, middleware e OpenAPI — TO DO — pendente Jira
- activity-management/TASK-19: DigestActionsController, InternalController e anti-enumeração — TO DO — pendente Jira
- activity-management/TASK-20: Contratos de evento v1 e Pact provider tests — TO DO — pendente Jira
- activity-management/TASK-21: Testes de API — RBAC, erros, cross-tenant CI gate — TO DO — pendente Jira

#### Definition of Done
- [ ] Infrastructure.Tests ≥ 70%; Api.Tests ≥ 80%; PBT-03 verde; contract tests verdes
- [ ] CI verde; Code review; PR mergeado

---

### US-042 — Detecção de oportunidades estagnadas (ActivityStale scan)

- **Épico:** activity-management (EP-008)
- **RF rastreado:** RF act-stagnation
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `ScanOverdueActivities` varre oportunidades sem atividade em X dias (configurável por BU) e publica `OpportunityStale` via Outbox
- [ ] `ActivityOverdue` publicado para atividades vencidas; digest consome `OpportunityStale` e `ActivityOverdue`

#### Definition of Done
- [ ] Incluso em TASK-12 e TASK-17 acima; CI verde; PR mergeado

---

### US-043 — Varredura periódica de atividades vencidas

- **Épico:** activity-management (EP-008)
- **RF rastreado:** RF act-overdue
- **Story Points:** 3
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `InternalController` com endpoint `/internal/activities/scan-overdue` (mTLS); scan idempotente por data
- [ ] Hardening de segurança: tempo constante, anti-PII, privilégio mínimo; DoD final e coverage gates

#### Tasks técnicas
- activity-management/TASK-22: Observabilidade — métricas, logs, traces, alertas — TO DO — pendente Jira
- activity-management/TASK-23: Hardening de segurança — tempo constante, anti-PII, privilégio mínimo — TO DO — pendente Jira
- activity-management/TASK-24: DoD final — coverage gates, PBT-01..05 completos — TO DO — pendente Jira

#### Definition of Done
- [ ] PBTs 01-05 verdes; DoD completo; CI verde; PR mergeado

---

### US-046 — Meta individual do vendedor e US-047 — Bloco digest de meta

- **Épico:** goal-forecast (EP-009)
- **RF rastreado:** RF gf-individual, RF gf-digest-block
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `GoalForecastDbContext` com mapeamentos + RLS + Global Query Filter; migration com UNIQUE, índice parcial BU e RLS policies
- [ ] `GoalRepository` com testes de integração Testcontainers; `PipelineForecastReader` com circuit breaker
- [ ] `GET /api/v1/goals` com filtro RBAC por escopo; `PUT /api/v1/goals` com idempotência
- [ ] `GET /api/v1/forecast` e `GET /api/v1/forecast/aggregate` com RBAC
- [ ] `GET /internal/goals/digest-block` (mTLS) com sinal de ausência quando meta não configurada
- [ ] Testes de segurança: RBAC, anti-enumeração, cross-tenant e cross-BU

#### Tasks técnicas
- goal-forecast/TASK-15: GoalForecastDbContext + EF Core + Global Query Filter — TO DO — pendente Jira
- goal-forecast/TASK-16: Migration: tabela goals, UNIQUE, índice parcial BU, RLS policies — TO DO — pendente Jira
- goal-forecast/TASK-17: GoalRepository + testes de integração — TO DO — pendente Jira
- goal-forecast/TASK-18: Teste de isolamento RLS cross-tenant (KPI-06) — TO DO — pendente Jira
- goal-forecast/TASK-19: PipelineForecastReader + circuit breaker + flag pipelineUnavailable — TO DO — pendente Jira
- goal-forecast/TASK-20: BuMembershipReader + OutboxDispatcher + PBT-05 round-trip — TO DO — pendente Jira
- goal-forecast/TASK-21: Contracts DTOs + schema evento goal.updated.v1 — TO DO — pendente Jira
- goal-forecast/TASK-22: GoalsController: POST /goals + PUT /goals/{id} — TO DO — pendente Jira
- goal-forecast/TASK-23: GoalsController: GET /goals + RBAC + catálogo de erros — TO DO — pendente Jira
- goal-forecast/TASK-24: ForecastController: GET /forecast + GET /forecast/aggregate — TO DO — pendente Jira
- goal-forecast/TASK-25: InternalGoalsController: GET /internal/goals/digest-block (mTLS) — TO DO — pendente Jira
- goal-forecast/TASK-26: Testes de contrato goal.updated.v1 + porta ForecastView — TO DO — pendente Jira

#### Definition of Done
- [ ] Infrastructure.Tests ≥ 70%; Api.Tests ≥ 80%; RLS gate CI verde; CI verde; PR mergeado

---

### US-048 e US-055 (parcial) — Domínio do digest worker

- **Épico:** digest (EP-011)
- **RF rastreado:** RF dg-content (parcial)
- **Story Points:** 8
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `Digest.sln` compilando; Architecture.Tests verdes
- [ ] Objetos de valor: `TenantEligibility` (PBT-01), `DigestDate`, `MoneyCents`, `ActionToken`, `DigestSection`, `DigestContent`
- [ ] Entidades: `EmailDigestLog` com state machine; `DigestActionToken` com ciclo de vida
- [ ] `RecipientSelectionPolicy` com PBT-03 verde (seleção por fuso correto)
- [ ] Aggregate `DigestJob` + evento `DigestEmailSent`
- [ ] Portas de leitura (6 interfaces): `IOpportunityStaleReader`, `IActivityOverdueReader`, `IGoalDigestBlockReader`, `IAccountReader`, `ITenantBrandingReader`, `ITenantRecipientReader`
- [ ] `SelectEligibleTenantsQuery` e `SelectRecipientsQuery`
- [ ] `DigestContentComposer` e `AzimuteSectionBuilder` (PBT-04 e PBT-06 verdes)
- [ ] `SendUserDigestHandler` idempotente (PBT-02 verde)
- [ ] `RunDigestForTenantHandler`, `UpdateDeliveryStatusHandler` e pipeline behaviors

#### Tasks técnicas
- digest/TASK-01: Criar solution e projetos — TO DO — pendente Jira
- digest/TASK-02: Architecture.Tests e CI mínimo — TO DO — pendente Jira
- digest/TASK-03: TenantEligibility e DigestDate (PBT-01) — TO DO — pendente Jira
- digest/TASK-04: MoneyCents, ActionToken, DigestSection, DigestContent — TO DO — pendente Jira
- digest/TASK-05: EmailDigestLog state machine — TO DO — pendente Jira
- digest/TASK-06: DigestActionToken ciclo de vida — TO DO — pendente Jira
- digest/TASK-07: RecipientSelectionPolicy (PBT-03) — TO DO — pendente Jira
- digest/TASK-08: DigestJob aggregate + DigestEmailSent — TO DO — pendente Jira
- digest/TASK-09: Portas de leitura (6 interfaces) — TO DO — pendente Jira
- digest/TASK-10: SelectEligibleTenantsQuery e SelectRecipientsQuery — TO DO — pendente Jira
- digest/TASK-11: DigestContentComposer e AzimuteSectionBuilder (PBT-04, PBT-06) — TO DO — pendente Jira
- digest/TASK-12: SendUserDigestHandler e idempotência (PBT-02) — TO DO — pendente Jira
- digest/TASK-13: RunDigestForTenantHandler + UpdateDeliveryStatusHandler + pipeline behaviors — TO DO — pendente Jira

#### Definition of Done
- [ ] Domain.Tests ≥ 95%; Application.Tests ≥ 85% (parcial); PBTs 01-04, 06 verdes
- [ ] CI verde; Code review; PR mergeado

---

## 2. Bugs Acompanhados

| ID | Severidade | Descrição | Status | Issue |
|---|---|---|---|---|
| — | — | Nenhum | — | — |

---

## 3. Riscos da Sprint

| Risco | Mitigação |
|---|---|
| Sprint pesada (38 pts) com 3 módulos simultâneos | Priorizar activity-management API (bloqueante para digest); goal-forecast e digest podem ter tasks movidas para Sprint 13 se necessário |
| `DigestContentComposer` acessa 5 módulos via ports — latência de composição | Usar `Task.WhenAll` para leituras paralelas; timeout por porta (Polly); PBT-04 valida composição correta independente de disponibilidade parcial |

---

## 4. Encerramento

- [ ] Todas as stories em DONE; todos os PRs mergeados
- [ ] Sprint encerrada no Jira; `progress-tracking.md` atualizado
