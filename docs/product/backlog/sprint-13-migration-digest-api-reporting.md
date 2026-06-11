# Sprint 13 — migration-digest-api-reporting

- **Objetivo:** Ao final desta sprint, um TAdmin consegue importar a planilha legada com dry-run, triagem assistida e rollback garantido (tudo-ou-nada), o digest às 07:00 está operacional via Cloud Scheduler com links de ação e purge de 90 dias, e o módulo reporting tem as views PostgreSQL criadas e a API REST com 5 relatórios disponíveis, viabilizando os dados iniciais do CRM migrados e os primeiros relatórios consultáveis.
- **Período:** 2026-11-30 a 2026-12-13
- **Story Points totais:** 44
- **Status:** Planejada
- **Dependências:** Sprint 12 (digest domain, activity API), Sprint 10 (account, partner operacionais)

---

## 1. Backlog da Sprint

### US-050..054 — Data Migration completa (upload, dry-run, triagem, import, lifecycle)

- **Épico:** data-migration (EP-010)
- **Story Points:** 16
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite (US-050 a US-054 consolidados)
- [ ] `DataMigration.sln` compilando; Architecture.Tests verdes
- [ ] Aggregate `MigrationJob` com state machine + PBT-07 verde
- [ ] VOs: `ExcelSerialDate` (PBT-03), `ForecastDivergence` (PBT-06), `OpportunityNumber` (PBT-05)
- [ ] `UploadSpreadsheetCommand` + handler; `RunDryRunCommand` + `TriageReport` + PBT-04 verde
- [ ] Commands de triagem assistida (owner, estágio, parceiro, dedupe, salvar, ready)
- [ ] `ExecuteImportCommand` tudo-ou-nada com rollback garantido (PBT-01 e PBT-02 verdes)
- [ ] `SpreadsheetParser` (ClosedXML, DD-002) + `CanonicalRowMapper`
- [ ] EF Core + migrations + RLS (DD-008) + idempotência por `import_key` (DD-003)
- [ ] Portas: `IAccountImportPort`, `IPartnerImportPort`, `IOpportunityImportPort`, `IActivityImportPort`, `IOrganizationReadPort`; UoW compartilhado (DD-001)
- [ ] Outbox `ImportCompleted` + eventos `DryRunCompleted`, `ImportRolledBack`
- [ ] `MigrationController`: upload + dry-run + status + triagem + execute com confirmação explícita
- [ ] `PiiSafeLogger` + `PiiSafeLoggingBehavior` (LGPD); observabilidade completa
- [ ] Feature flag `migration.import_enabled` + plano de remoção documentado (DD-009)
- [ ] Teste de staging: dry-run e rollback com 108 e 500 linhas (RNF 1.3, RNF 5.2)

#### Tasks técnicas (data-migration — TASK-01 a TASK-28, todas)
- data-migration/TASK-01..02: Bootstrap + Architecture.Tests — TO DO — pendente Jira
- data-migration/TASK-03..07: Domain (aggregado, VOs, policies) — TO DO — pendente Jira
- data-migration/TASK-08..13: Application (commands, handlers, queries) — TO DO — pendente Jira
- data-migration/TASK-14..20: Infrastructure (parser, EF, RLS, ports, outbox) — TO DO — pendente Jira
- data-migration/TASK-21..23: API (upload, triagem, execute) — TO DO — pendente Jira
- data-migration/TASK-24..28: Hardening (PII, observabilidade, staging tests, feature flag, DoD) — TO DO — pendente Jira

#### Definition of Done
- [ ] PBTs 01-07 verdes; teste de staging verde (108 + 500 linhas); DoD completo; CI verde; PR mergeado

---

### US-056..059 — Digest API operacional com Cloud Scheduler e purge

- **Épico:** digest (EP-011)
- **Story Points:** 12
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite (US-055 parcial + US-056 a US-059)
- [ ] `DbContext`, Global Query Filter e migrations SQL (email_digest_logs, digest_action_tokens)
- [ ] RLS falha-fechada e `TenantConnectionInterceptor` (cross-tenant gate CI verde)
- [ ] `ActionTokenFactory` (PBT-05 verde); repositórios; `OutboxPublisher` (DigestEmailSent → `digest.email_sent.v1`)
- [ ] Adaptadores das 6 portas de leitura com resiliência Polly; `EmailTemplateRenderer` com branding do tenant
- [ ] `DigestTriggerEndpoint` OIDC/WIF via Cloud Scheduler; health checks operacionais
- [ ] `PerTenantConsumer` Pub/Sub com fan-out por tenant; Contracts DTOs e evento `digest.email_sent.v1`
- [ ] Purge de `email_digest_logs` (90 dias) e `digest_action_tokens`; gate de isolamento CI cross-tenant
- [ ] Observabilidade: métricas, traces, alertas e logs sem PII; DoD final

#### Tasks técnicas (digest — TASK-14 a TASK-27)
- digest/TASK-14: DbContext + Global Query Filter + migrations — TO DO — pendente Jira
- digest/TASK-15: RLS falha-fechada + TenantConnectionInterceptor — TO DO — pendente Jira
- digest/TASK-16: ActionTokenFactory (PBT-05) — TO DO — pendente Jira
- digest/TASK-17: EmailDigestLogRepository + DigestActionTokenRepository — TO DO — pendente Jira
- digest/TASK-18: OutboxPublisher — TO DO — pendente Jira
- digest/TASK-19: Adaptadores das portas de leitura com Polly — TO DO — pendente Jira
- digest/TASK-20: EmailTemplateRenderer com branding — TO DO — pendente Jira
- digest/TASK-21: DigestTriggerEndpoint OIDC/WIF + health checks — TO DO — pendente Jira
- digest/TASK-22: PerTenantConsumer Pub/Sub + fan-out — TO DO — pendente Jira
- digest/TASK-23: Contracts DTOs e evento digest.email_sent.v1 — TO DO — pendente Jira
- digest/TASK-24: Purge de email_digest_logs (90 dias) + digest_action_tokens — TO DO — pendente Jira
- digest/TASK-25: Gate de isolamento CI cross-tenant — TO DO — pendente Jira
- digest/TASK-26: Observabilidade completa — TO DO — pendente Jira
- digest/TASK-27: Documentação final e DoD — TO DO — pendente Jira

#### Definition of Done
- [ ] PBTs 01-06 verdes; cross-tenant gate CI verde; digest 07:00 testado manualmente
- [ ] Observabilidade ativa; DoD completo; CI verde; PR mergeado

---

### reporting (bootstrap e infrastructure) — Views PostgreSQL

- **Épico:** reporting (EP-012)
- **Story Points:** 8
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `Reporting.sln` compilando; Architecture.Tests verdes
- [ ] VOs: `Money`, `Period`, `ChannelShare`, `StageBucket`; `ReportScope`, `ReportType`, papéis RBAC
- [ ] Portas de aplicação: `IReportingReadRepository`, `IScopeResolver`, `ICsvReportWriter`, `ICsvStorage`
- [ ] Handlers dos 6 relatórios com degradação graciosa (funnel, forecast, ranking, channel, commission, csv export)
- [ ] Pipeline behaviors: Correlation, TenantContext, Validation, Authorization, LoggingMetrics, QueryTimeout
- [ ] 5 views PostgreSQL com `security_invoker`: `vw_funnel_report`, `vw_forecast_report`, `vw_ranking_report`, `vw_channel_report`, `vw_commission_report`; migration consolidada idempotente
- [ ] Índices críticos; teste de isolamento RLS cross-tenant (PBT-03 como gate CI)
- [ ] `IReportingReadRepository` com Dapper; `ICsvStorage` GCS client

#### Tasks técnicas (reporting — TASK-01 a TASK-19)
- reporting/TASK-01..04: Bootstrap + domain (VOs, report scope) — TO DO — pendente Jira
- reporting/TASK-05..12: Application (handlers 6 relatórios + behaviors) — TO DO — pendente Jira
- reporting/TASK-13..17: Infrastructure (5 views + índices + migration + Dapper + GCS) — TO DO — pendente Jira
- reporting/TASK-18: PBT-03 isolamento RLS cross-tenant (gate CI) — TO DO — pendente Jira
- reporting/TASK-19: IReportingReadRepository + ICsvStorage GCS — TO DO — pendente Jira

#### Definition of Done
- [ ] Application.Tests ≥ 85%; Infrastructure.Tests ≥ 70%; PBT-03 gate CI verde; CI verde; PR mergeado

---

## 2. Bugs Acompanhados

| ID | Severidade | Descrição | Status | Issue |
|---|---|---|---|---|
| — | — | Nenhum | — | — |

---

## 3. Riscos da Sprint

| Risco | Mitigação |
|---|---|
| Sprint muito pesada (44 pts) — data-migration é o módulo mais complexo desta lista | data-migration tem prioridade máxima; digest e reporting podem ter tasks de hardening movidas para Sprint 14 se necessário |
| Planilha legada com 108 oportunidades pode ter dados inconsistentes não cobertos pelo TriageReport | Dry-run primeiro; erros de triagem são coletados em `TriageReport` sem bloquear upload |
| Cloud Scheduler setup (OIDC + WIF) pode requerer configuração manual de infraestrutura GCP | Documentar em `.forge/scripts/setup-scheduler.sh`; usar `IHostedService` como alternativa local |

---

## 4. Encerramento

- [ ] Todas as stories em DONE; todos os PRs mergeados
- [ ] Sprint encerrada no Jira; retrospectiva realizada
- [ ] **Marco:** dados migrados do sistema legado disponíveis no Azim CRM; digest 07:00 enviado para equipe de teste
- [ ] `progress-tracking.md` atualizado
