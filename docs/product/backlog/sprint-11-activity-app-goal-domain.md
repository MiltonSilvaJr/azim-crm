# Sprint 11 — activity-app-goal-domain

- **Objetivo:** Ao final desta sprint, um Vendedor consegue criar, completar e reagendar atividades (call, e-mail, reunião, tarefa) pelo CRM com visão Meu Dia/Minha Semana, e um GestorBU consegue criar metas mensais por BU e visualizar o painel realizado vs forecast, viabilizando o núcleo de produtividade do vendedor e a gestão de desempenho.
- **Período:** 2026-11-02 a 2026-11-15
- **Story Points totais:** 26
- **Status:** Planejada
- **Dependências:** Sprint 9 (organization operacional), Sprint 10 (account operacional)

---

## 1. Backlog da Sprint

### US-039 — Criar e gerenciar atividades de acompanhamento

- **Épico:** activity-management (EP-008)
- **RF rastreado:** RF act-create
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `ActivityManagement.sln` com 5 projetos compilando; Architecture.Tests verdes
- [ ] Objetos de valor e state machine (PBT-01); domain events; Aggregate `Activity` com invariantes I1-I6 (PBT criado/completo/overdue)
- [ ] Specifications: Overdue, FunnelHealth, Scope (PBT-04, PBT-05 verdes)
- [ ] `CreateActivity`, `UpdateActivity`, `DeleteActivity` commands com handlers TDD; `CompleteActivity` idempotente e `SuggestNextActivity` (PBT-02 verde)
- [ ] `POST /api/v1/activities` cria atividade vinculada a oportunidade ou conta; `GET /api/v1/activities/my-day` retorna atividades do dia

#### Tasks técnicas
- activity-management/TASK-01: Bootstrap solution — TO DO — pendente Jira
- activity-management/TASK-02: Objetos de valor e state machine (PBT-01) — TO DO — pendente Jira
- activity-management/TASK-03: Domain events e exceptions — TO DO — pendente Jira
- activity-management/TASK-04: Aggregate Activity — factory e invariantes — TO DO — pendente Jira
- activity-management/TASK-05: Specifications Overdue, FunnelHealth, Scope (PBT-04, PBT-05) — TO DO — pendente Jira
- activity-management/TASK-06: Pipeline behaviors e ports interfaces — TO DO — pendente Jira
- activity-management/TASK-07: Commands/Handlers CRUD e validators — TO DO — pendente Jira
- activity-management/TASK-08: CompleteActivity idempotente e SuggestNextActivity (PBT-02) — TO DO — pendente Jira
- activity-management/TASK-11: Queries de visão do vendedor (GetMyDay, GetMyWeek, ListActivities) — TO DO — pendente Jira

#### Definition of Done
- [ ] Domain.Tests ≥ 95%; Application.Tests ≥ 85% (parcial); PBTs 01, 02, 04, 05 verdes
- [ ] CI verde; Code review; PR mergeado

---

### US-040 — Visão Meu Dia e Minha Semana

- **Épico:** activity-management (EP-008)
- **RF rastreado:** RF act-myview
- **Story Points:** 3
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `GET /api/v1/activities/my-week` filtra atividades da semana corrente por usuário autenticado
- [ ] `GET /api/v1/activities` com filtros por BU, owner, tipo, status, período
- [ ] `ProcessDigestAction` — validar e consumir token de ação do digest (PBT-02, PBT-03 verde)

#### Tasks técnicas
- activity-management/TASK-09: ProcessDigestAction — validar e consumir token — TO DO — pendente Jira
- activity-management/TASK-10: RescheduleActivity — reagendamento e ação via token — TO DO — pendente Jira

#### Definition of Done
- [ ] Application.Tests ≥ 85%; PBT-03 verde; CI verde; PR mergeado

---

### US-044 — Criar meta mensal por BU ou vendedor

- **Épico:** goal-forecast (EP-009)
- **RF rastreado:** RF gf-upsert
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `GoalForecast.sln` compilando; Architecture.Tests verdes
- [ ] VO `Money` (centavos inteiros + campo currency BRL/USD/EUR — delta HITL#1 ADR-0008); `GoalPeriod` e `GoalScope`
- [ ] Aggregate `Goal` com `Create`, `ChangeValorMeta`; invariantes INV-1..4
- [ ] `GoalAggregation` serviço de domínio (PBT-02 verde); `GoalAuthorizationPolicy` + `BuMembershipSpecification`
- [ ] `CreateOrUpdateGoalCommandHandler` com upsert idempotente (PBT-01 verde)
- [ ] `POST /api/v1/goals` cria ou atualiza meta; retorna 201/200 com idempotência

#### Tasks técnicas
- goal-forecast/TASK-01: Bootstrap solution e 5 projetos — TO DO — pendente Jira
- goal-forecast/TASK-02: Architecture.Tests e dependências — TO DO — pendente Jira
- goal-forecast/TASK-03: VO Money (centavos + currency) (PBT-05) — TO DO — pendente Jira
- goal-forecast/TASK-04: VOs GoalPeriod e GoalScope — TO DO — pendente Jira
- goal-forecast/TASK-05: Aggregate Goal + Create + ChangeValorMeta (INV-1..4) — TO DO — pendente Jira
- goal-forecast/TASK-06: GoalAggregation + domain event GoalUpdated (PBT-02) — TO DO — pendente Jira
- goal-forecast/TASK-07: GoalAuthorizationPolicy + BuMembershipSpecification — TO DO — pendente Jira
- goal-forecast/TASK-08: Portas de aplicação (IGoalRepository, IPipelineForecastReader, IBuMembershipReader) — TO DO — pendente Jira
- goal-forecast/TASK-09: CreateOrUpdateGoalCommandHandler + upsert idempotente (PBT-01) — TO DO — pendente Jira
- goal-forecast/TASK-10: Pipeline behaviors — TO DO — pendente Jira
- goal-forecast/TASK-11: ListGoalsQueryHandler + filtro RBAC — TO DO — pendente Jira

#### Definition of Done
- [ ] Domain.Tests ≥ 95%; Application.Tests ≥ 85% (parcial); PBTs 01, 02, 05 verdes
- [ ] CI verde; Code review; PR mergeado

---

### US-045 — Painel realizado vs meta com forecast ponderado

- **Épico:** goal-forecast (EP-009)
- **RF rastreado:** RF gf-panel
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `GetForecastPanelQueryHandler` com degradação graciosa (PBT-03, PBT-04 verdes): quando pipeline indisponível, retorna meta e realizado com flag `pipelineUnavailable`
- [ ] Painel inclui: meta, realizado (won), forecast ponderado, forecast líquido (após comissões), % atingimento
- [ ] `GetGoalAggregateQueryHandler` com aggregation derivada (PBT-02)

#### Tasks técnicas
- goal-forecast/TASK-12: GetForecastPanelQueryHandler + degradação graciosa (PBT-03, PBT-04) — TO DO — pendente Jira
- goal-forecast/TASK-13: GetGoalAggregateQueryHandler + GoalAggregation derivada (PBT-02) — TO DO — pendente Jira
- goal-forecast/TASK-14: GetGoalDigestBlockQueryHandler com sinal de ausência — TO DO — pendente Jira

#### Definition of Done
- [ ] Application.Tests ≥ 85%; PBTs 03, 04 verdes; CI verde; PR mergeado

---

## 2. Bugs Acompanhados

| ID | Severidade | Descrição | Status | Issue |
|---|---|---|---|---|
| — | — | Nenhum | — | — |

---

## 3. Riscos da Sprint

| Risco | Mitigação |
|---|---|
| `ProcessDigestAction` (activity-management) depende de `DigestActionToken` (digest module) — módulo ainda não implementado | Definir contrato de token (IDigestActionTokenValidator) como porta em activity-management; implementar stub na Sprint 11; integrar com digest real na Sprint 12 |
| `IPipelineForecastReader` em goal-forecast depende de opportunity-pipeline ainda não implementado | Porta implementada com circuit breaker; fallback `pipelineUnavailable=true` no handler (PBT-03/04 cobrem isso) |

---

## 4. Encerramento

- [ ] Todas as stories em DONE; todos os PRs mergeados
- [ ] Sprint encerrada no Jira; `progress-tracking.md` atualizado
