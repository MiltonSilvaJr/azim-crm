# Sprint 14 — reporting-api-pipeline-domain

- **Objetivo:** Ao final desta sprint, um GestorBU consegue consultar os 5 relatórios (funil, forecast, ranking, canal, comissões) e exportar qualquer um como CSV, metas e forecasts representam currency em centavos multimoeda (BRL/USD/EUR), e o módulo opportunity-pipeline tem seu domínio e camada de application completos (create, move stage, cálculos financeiros PBT-verdes), viabilizando o primeiro pipeline consultável e a base financeira para Fase 1.
- **Período:** 2026-12-14 a 2026-12-27
- **Story Points totais:** 46
- **Status:** Planejada
- **Dependências:** Sprint 13 (reporting bootstrap + views PostgreSQL criadas; data-migration operacional), Sprint 12 (goal-forecast API operacional), Sprint 11 (activity, goal-forecast application)

---

## 1. Backlog da Sprint

### US-049 — Money multimoeda em metas e forecast (HITL#1 — ADR-0008)

- **Épico:** goal-forecast (EP-009)
- **RF rastreado:** RF gf-multicurrency
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] VO `Money` atualizado com campo `currency` (enum `BRL | USD | EUR`); centavos inteiros (`long`) preservados
- [ ] Aggregate `Goal` aceita `currency` e valida consistência entre `valorMeta.currency` e `currency` do escopo (INV-1 atualizado)
- [ ] `GoalAggregation` (serviço de domínio) rejeita soma de metas com currencies diferentes (PBT-02 verde com currencies heterogêneas)
- [ ] Endpoints `PUT /api/v1/goals` e `GET /api/v1/goals` expõem campo `currency` nos DTOs
- [ ] Testes de segurança (TASK-27), observabilidade (TASK-28), performance p95 (TASK-29) e DoD final (TASK-30) verdes

#### Tasks técnicas (goal-forecast — TASK-27 a TASK-30)
- goal-forecast/TASK-27: Testes de segurança — RBAC, anti-enumeração, cross-tenant e cross-BU — TO DO — pendente Jira
- goal-forecast/TASK-28: Observabilidade — logs estruturados, métricas, traces e alertas — TO DO — pendente Jira
- goal-forecast/TASK-29: Baseline de performance do painel (p95 ≤ 3.000ms) — TO DO — pendente Jira
- goal-forecast/TASK-30: DoD final — reconciliar DD-008, coverage gates, README sincronizado — TO DO — pendente Jira

#### Definition of Done
- [ ] PBT-02 verde com currencies heterogêneas; testes de segurança verdes; p95 medido; DoD completo; CI verde; PR mergeado

---

### US-060 a US-065 — Reporting API: 5 relatórios + exportação CSV

- **Épico:** reporting (EP-012)
- **RF rastreado:** Req 1..6 (reporting)
- **Story Points:** 26
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite (US-060 a US-065 consolidados)
- [ ] `GET /api/v1/reports/funnel?buId=&period=` retorna funil por estágio (entrada, avanço, fechamento, tempo médio); 401/403 para papéis não autorizados (US-060)
- [ ] `GET /api/v1/reports/forecast?period=` retorna forecast consolidado vs meta com flag `goalUnavailable` quando goal-forecast indisponível — degradação graciosa (US-061)
- [ ] `GET /api/v1/reports/ranking?buId=&period=` retorna ranking com `PiiMinimizationPolicy` (mínimo de registros para exibir nome real); RNF 4 (US-062)
- [ ] `GET /api/v1/reports/channel?buId=&period=` retorna distribuição percentual por canal; PBT-04 verde (soma = 100%) (US-063)
- [ ] `GET /api/v1/reports/commissions?period=` distingue snapshot imutável (ganhas) de projetado (em aberto); PBT-01 e PBT-02 verdes (US-064)
- [ ] `GET /api/v1/reports/export?type=&period=` gera CSV fiel ao relatório (PBT-05 round-trip: linhas e totais equivalentes); upload para GCS via `ICsvStorage` (US-065)
- [ ] Catálogo de erros REPORT-ERR completo; anti-enumeração em 403 (não revela existência de relatório de outro tenant)
- [ ] Testes de segurança: PII ausente em logs; Platform Operator bloqueado; cross-tenant verificado (RNF 4, 5)
- [ ] Observabilidade: duração de query em histograma; traces distribuídos; alertas p95 > 3s; DoD final (RNF 6)
- [ ] Performance: p95 ≤ 3s para relatórios; export ≤ 10s para 2.000 linhas (RNF 1, 3)

#### Tasks técnicas (reporting — TASK-20 a TASK-25)
- reporting/TASK-20: Contracts DTOs — requests, responses e CsvExportResponse — TO DO — pendente Jira
- reporting/TASK-21: ReportingController — cinco endpoints GET de relatório e GET export — TO DO — pendente Jira
- reporting/TASK-22: Testes de API — contratos, RBAC, catálogo de erros e anti-enumeração — TO DO — pendente Jira
- reporting/TASK-23: Testes de segurança — PII em logs, Platform Operator bloqueado, cross-tenant (RNF 4, RNF 5) — TO DO — pendente Jira
- reporting/TASK-24: Observabilidade — logs estruturados, métricas, traces, alertas e health checks (RNF 6) — TO DO — pendente Jira
- reporting/TASK-25: Performance baseline e DoD final (RNF 1, RNF 3, PBT-01..05, PTV-01) — TO DO — pendente Jira

#### Definition of Done
- [ ] Application.Tests ≥ 85%; Infrastructure.Tests ≥ 70%; Api.Tests ≥ 80%; PBTs 01-05 verdes; PBT-03 gate CI verde; DoD completo; CI verde; PR mergeado

---

### US-066, US-067 e US-073 — opportunity-pipeline: bootstrap, domínio e application

- **Épico:** opportunity-pipeline (EP-013)
- **RF rastreado:** Req 1, Req 5, Req 6, Req 7, Req 8, Req 13
- **Story Points:** 15
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite (US-066 — criar oportunidade)
- [ ] `OpportunityPipeline.sln` com 5 projetos de produção + 5 de teste compilando; Architecture.Tests verde
- [ ] VOs financeiros: `Money` (centavos + currency BRL/USD/EUR, ADR-0008), `NbrRounding`, `ContractValue`, `Probability`
- [ ] VOs de domínio: `OpportunityNumber`, `StageCategory`, `CommissionTerms/Calculation/Role`, `ContactLink`
- [ ] Serviços de domínio puros: calculators, forecast, `NbrRounding` (PBT-03..06 verdes)
- [ ] Aggregate `Opportunity` com `Create` (validações obrigatórias: title, account, owner, stage inicial) e `MoveStage` com `OpportunityLifecycle` (PBT-02 e PBT-08 verdes)
- [ ] `CreateOpportunityCommand` e `UpdateOpportunityCommand` com handlers e validators; `POST /api/v1/opportunities` retorna 201 com `opportunityId` e `opportunityNumber` sequencial gerado pelo tenant

#### Critérios de aceite (US-067 — mover estágio)
- [ ] `MoveStageCommand` e handler com transição validada pelo `OpportunityLifecycle`; estágios configurados em organization (Sprint 7/8)
- [ ] Registro de transição na linha do tempo da oportunidade com `probability` herdada do estágio ou editável manualmente
- [ ] Evento `StageChanged` publicado via Outbox (schema `opportunity.stage_changed.v1`)

#### Critérios de aceite (US-073 — cálculos financeiros)
- [ ] `valor_total` (MRR × 12 + setup + hardware), `forecast_ponderado` e `forecast_líquido` calculados em centavos com `NbrRounding` (NBR 5891); PBT-03 e PBT-04 (conservação de centavos) verdes
- [ ] `StagnationDetectionService` (Application Service) detecta oportunidades sem atividade em N dias (configurável) e publica `MarkStaleCommand`; PBT-09 verde
- [ ] Pipeline behaviors: Logging, TenantContext, Rbac, Idempotency, Validation, Transaction ativos

#### Tasks técnicas (opportunity-pipeline — TASK-01 a TASK-12)
- opportunity-pipeline/TASK-01: Solution .NET + 5 projetos Clean Architecture + Architecture.Tests — TO DO — pendente Jira
- opportunity-pipeline/TASK-02: Objetos de valor financeiros — Money, NbrRounding, ContractValue, Probability — TO DO — pendente Jira
- opportunity-pipeline/TASK-03: Objetos de valor de domínio — OpportunityNumber, StageCategory, *Ref, CommissionTerms/Calculation/Role, ContactLink — TO DO — pendente Jira
- opportunity-pipeline/TASK-04: Serviços de domínio puros + Policies — calculators, forecast, NbrRounding (PBT-03..06) — TO DO — pendente Jira
- opportunity-pipeline/TASK-05: Entidades internas + eventos de domínio + exceções + interfaces de repositório e portas — TO DO — pendente Jira
- opportunity-pipeline/TASK-06: Aggregate Opportunity — Create, MoveStage, OpportunityLifecycle (PBT-02, PBT-08) — TO DO — pendente Jira
- opportunity-pipeline/TASK-07: Aggregate — Win, Lose, Reopen, MarkStale, SetPartnerCommission, LinkContact (PBT-07, PBT-11) — TO DO — pendente Jira
- opportunity-pipeline/TASK-08: Pipeline behaviors — Logging, Tenant, Rbac, Idempotency, Validation, Transaction — TO DO — pendente Jira
- opportunity-pipeline/TASK-09: Commands de criação/edição/movimentação — CreateOpportunity, UpdateOpportunity, MoveStage — TO DO — pendente Jira
- opportunity-pipeline/TASK-10: Commands de fechamento — WinOpportunity, LoseOpportunity, ReopenOpportunity + handlers transacionais — TO DO — pendente Jira
- opportunity-pipeline/TASK-11: Commands de comissão e contatos — SetPartnerCommission, LinkContact, UnlinkContact, SaveFilter — TO DO — pendente Jira
- opportunity-pipeline/TASK-12: Queries (8) + StagnationDetectionService (Application Service) — PBT-09 — TO DO — pendente Jira

#### Definition of Done
- [ ] Domain.Tests ≥ 95%; Application.Tests ≥ 85%; PBT-02..09 verdes; CI verde; Code review; PR mergeado

---

## 2. Bugs Acompanhados

| ID | Severidade | Descrição | Status | Issue |
|---|---|---|---|---|
| — | — | Nenhum | — | — |

---

## 3. Riscos da Sprint

| Risco | Mitigação |
|---|---|
| Sprint pesada (46 pts) com 3 módulos distintos | Prioridade: reporting API (6 stories — dependência de go-live); opportunity-pipeline domain/application; goal-forecast hardening pode deslizar para Sprint 15 se necessário |
| `opportunity-pipeline` começa do zero nesta sprint — TASK-01 a TASK-12 é o backbone inteiro (domain + application) | Domain-first: TASK-01..07 são pré-condição para TASK-08..12; paralelismo possível entre reporting e pipeline domain |
| `ReportingController` depende de todas as 5 views criadas em Sprint 13 | Verificar health das views (Sprint 13 DoD) antes de iniciar TASK-21; se alguma view falhou, abrir bug e priorizar correção antes de fechar TASK-21 |
| `PipelineForecastReader` (goal-forecast TASK-19) pode expor inconsistência entre `forecast_líquido` em goal-forecast e `forecast_líquido` calculado pelo opportunity-pipeline | ADR-0008 define que a fonte de verdade de forecast líquido é o opportunity-pipeline; goal-forecast lê via porta — confirmar contrato de porta antes de fechar TASK-19 e TASK-12 (op-pipeline) |

---

## 4. Encerramento

- [ ] Todas as stories em DONE; todos os PRs mergeados
- [ ] Sprint encerrada no Jira; retrospectiva realizada
- [ ] **Marco:** Azim CRM tem 5 relatórios consultáveis via API + módulo de oportunidades com domínio e application completos e PBTs verdes
- [ ] `progress-tracking.md` atualizado
