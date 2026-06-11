# Solution Modules — Azim CRM

**Status:** Rascunho para revisão
**Versão:** v0.1
**Data:** 2026-06-11
**Derivado de:** DDD Segmentation v0.1, Data Model v1.0, Context Map, NFRD v0.1

---

## 1. Objetivo

Este diretório documenta os módulos candidatos da solução Azim CRM derivados da segmentação DDD (§9 — Módulos Funcionais × Bounded Contexts × Deployables), do context map, dos deployables candidatos, dos requisitos não funcionais, das decisões arquiteturais e das obrigações de compliance aplicáveis (LGPD).

---

## 2. Fontes

| Documento | Caminho | Finalidade |
|---|---|---|
| DDD Segmentation | docs/product/ddd/ddd-segmentation.md | Fonte principal: bounded contexts, subdomínios, módulos e deployables |
| Context Map — Relações | docs/product/ddd/context-map/relations.md | Relações entre bounded contexts |
| Data Model | docs/product/data-model/data-model.md | Ownership de dados, tabelas, RLS |
| NFRD | docs/product/frd-nfrd/nfrd.md | Requisitos não funcionais e compliance LGPD |
| FRD | docs/product/frd-nfrd/frd.md | Capacidades funcionais por módulo |

---

## 3. Visão Geral dos Módulos

| Módulo | Tipo | Bounded Context | Subdomínio | Deployable | Fase | Status |
|---|---|---|---|---|---|---|
| [audit-log](./audit-log/README.md) | Application Module | Audit Log (BC-15) | Generic | azim-api | Fase 1 | Rascunho para revisão |
| [notification-delivery](./notification-delivery/README.md) | Adapter (ACL) | Notification Delivery (BC-14) | Generic | azim-digest-worker / azim-api | Fase 1 | Rascunho para revisão |
| [authentication](./authentication/README.md) | Application Module | Identity & Access (BC-12) | Generic | azim-api | Fase 1 | Rascunho para revisão |
| [tenant-administration](./tenant-administration/README.md) | Application Module | Tenancy & Branding (BC-13) | Generic | azim-api | Fase 1 | Rascunho para revisão |
| [organization](./organization/README.md) | Application Module | Organization Management (BC-08) | Supporting | azim-api | Fase 1 | Rascunho para revisão |
| [account-management](./account-management/README.md) | Application Module | Account Management (BC-02) | Supporting | azim-api | Fase 1 | Rascunho para revisão |
| [partner-management](./partner-management/README.md) | Application Module | Partner Management (BC-03) | Supporting | azim-api | Fase 1 | Rascunho para revisão |
| [activity-management](./activity-management/README.md) | Application Module | Activity Management (BC-04) | Supporting | azim-api | Fase 1 | Rascunho para revisão |
| [goal-forecast](./goal-forecast/README.md) | Application Module | Goal & Forecast (BC-05) | Supporting | azim-api | Fase 1 | Rascunho para revisão |
| [opportunity-pipeline](./opportunity-pipeline/README.md) | Application Module | Opportunity Pipeline (BC-01) | Core Domain | azim-api | Fase 1 | Rascunho para revisão |
| [data-migration](./data-migration/README.md) | Application Module | Data Migration (BC-09) | Supporting | azim-api | Fase 1 (temporário) | Rascunho para revisão |
| [digest](./digest/README.md) | Worker | Digest (BC-06) | Supporting | azim-digest-worker | Fase 1 | Rascunho para revisão |
| [reporting](./reporting/README.md) | Worker | Reporting (BC-07) | Supporting | azim-reporting-worker | Fase 1/2 | Rascunho para revisão |
| [azim-web](./azim-web/README.md) | Frontend SPA | — (consome azim-api) | Cross-cutting | azim-web | Fase 1 | Rascunho para revisão |
| [workflow-automation](./workflow-automation/README.md) | Worker | Workflow Automation (BC-10) | Supporting | azim-workflow-worker | Fase 2 | Rascunho para revisão |
| [ai-intelligence](./ai-intelligence/README.md) | Microservice | AI Intelligence (BC-11) | Supporting | azim-ai-service | Fase 3 | Rascunho para revisão |

---

## 4. Módulos por Tipo

### Application Modules (dentro de azim-api — Monólito Modular)

| Módulo | Função | Fase |
|---|---|---|
| authentication | ACL para GCP Identity Platform; autenticação e sessão por tenant | Fase 1 |
| tenant-administration | Provisionamento de tenant, slug, fuso, white-label | Fase 1 |
| organization | BUs, usuários, papéis, RBAC, convites | Fase 1 |
| account-management | Contas e contatos (PII); dedupe; visão 360° | Fase 1 |
| partner-management | Parceiros e percentuais de comissão default | Fase 1 |
| activity-management | Atividades e follow-ups; detecção de estagnação | Fase 1 |
| goal-forecast | Metas mensais; painel realizado vs meta | Fase 1 |
| opportunity-pipeline | Ciclo de vida de oportunidades; comissão nativa; snapshot imutável | Fase 1 |
| audit-log | Registro imutável de toda escrita em entidade de negócio (cross-cutting) | Fase 1 |
| data-migration | Import transacional da planilha Excel (temporário, Fase 1) | Fase 1 |

### Workers / CronJobs

| Módulo | Função | Fase | Deployable |
|---|---|---|---|
| digest | Seleção de destinatários por fuso, composição e envio do e-mail diário às 07:00 BRT | Fase 1 | azim-digest-worker |
| reporting | Read models assíncronos: funil, forecast, ranking, comissões, CSV | Fase 1/2 | azim-reporting-worker |
| workflow-automation | Execução assíncrona de automações visuais por BU via Pub/Sub | Fase 2 | azim-workflow-worker |

### Adapters

| Módulo | Função | Fase | Deployable |
|---|---|---|---|
| notification-delivery | ACL para provedores de e-mail (Postmark/SendGrid) via IEmailSender | Fase 1 | azim-digest-worker / azim-api |

### Frontends

| Módulo | Função | Fase |
|---|---|---|
| azim-web | SPA React + TypeScript; Kanban, pipeline, relatórios, configurações | Fase 1 |

### Microservices (Fase 3)

| Módulo | Função | Fase | Deployable |
|---|---|---|---|
| ai-intelligence | Scoring, resumo 360°, próxima ação e copilot por tenant (Python/FastAPI/LangGraph) | Fase 3 | azim-ai-service |

---

## 5. Relação Bounded Context × Módulo

| Bounded Context | Módulo(s) Relacionado(s) |
|---|---|
| Opportunity Pipeline (BC-01) | opportunity-pipeline |
| Account Management (BC-02) | account-management |
| Partner Management (BC-03) | partner-management |
| Activity Management (BC-04) | activity-management |
| Goal & Forecast (BC-05) | goal-forecast |
| Digest (BC-06) | digest |
| Reporting (BC-07) | reporting |
| Organization Management (BC-08) | organization |
| Data Migration (BC-09) | data-migration |
| Workflow Automation (BC-10) | workflow-automation |
| AI Intelligence (BC-11) | ai-intelligence |
| Identity & Access (BC-12) | authentication |
| Tenancy & Branding (BC-13) | tenant-administration |
| Notification Delivery (BC-14) | notification-delivery |
| Audit Log (BC-15) | audit-log |

---

## 6. Relação Módulo × Dados

| Módulo | Dados Próprios | Dono da Escrita | Forma de Consumo por Outros |
|---|---|---|---|
| opportunity-pipeline | opportunities, opportunity_stage_transitions, opportunity_partner_commissions | Sim | API / Read Model |
| account-management | accounts, contacts (PII) | Sim | API (restrito) |
| partner-management | partners | Sim | API |
| activity-management | activities | Sim | API / Evento |
| goal-forecast | goals | Sim | API |
| organization | business_units, users, user_memberships, user_invitations, stages, origin_channels, loss_reasons | Sim | API / Cache |
| tenant-administration | tenants, tenant_brandings | Sim | Cross-cutting RLS / API |
| authentication | — (GCP IdP externo) | Não (GCP IdP) | OAuth/OIDC → session token |
| audit-log | audit_logs | Sim (append-only) | API somente leitura |
| digest | email_digest_logs, digest_action_tokens | Sim | API somente leitura |
| reporting | — (read models derivados) | Não (sem escrita própria) | Read Model / Query |
| data-migration | migration_jobs, migration_logs | Sim (temporário) | Interno |
| notification-delivery | — (stateless adapter) | Não | Package / Interface |
| azim-web | — (stateless SPA) | Não | — |
| workflow-automation | workflow_definitions, workflow_executions (Fase 2) | Sim | API / Evento |
| ai-intelligence | scoring_results, briefings (Fase 3) | Sim | API |

---

## 7. Relação Módulo × Eventos

| Módulo | Publica | Consome |
|---|---|---|
| opportunity-pipeline | OpportunityCreated, OpportunityStageChanged, OpportunityWon, OpportunityLost, OpportunityStale, OpportunityReopened, CommissionCalculated, CommissionSnapshotCreated | — |
| account-management | AccountCreated, ContactLinked | — |
| partner-management | PartnerCreated | — |
| activity-management | ActivityCreated, ActivityCompleted, ActivityOverdue | — |
| goal-forecast | GoalUpdated | — |
| organization | BUCreated, UserInvited, UserDeactivated | — |
| tenant-administration | TenantProvisioned, BrandingChanged | — |
| audit-log | — | AuditEvent (de todos os módulos de escrita) |
| digest | DigestEmailSent | OpportunityStale, ActivityOverdue |
| reporting | ReportGenerated | — (lê read models) |
| workflow-automation | WorkflowExecuted (Fase 2) | opportunity.* events via Pub/Sub (Fase 2) |
| ai-intelligence | ScoringCompleted (Fase 3) | — |

---

## 8. Relação Módulo × Integrações

| Módulo | Integração | Direção | Tipo |
|---|---|---|---|
| authentication | GCP Identity Platform | Entrada | OAuth2/OIDC (ACL) |
| tenant-administration | GCP Identity Platform | Saída | API (criação de tenant de identidade) |
| notification-delivery | Postmark / SendGrid | Saída | HTTP API (IEmailSender) |
| digest | Cloud Scheduler | Entrada | HTTP trigger |
| digest | notification-delivery | Saída | Package / Interface |
| reporting | Cloud Storage (CSV export) | Saída | GCS API |
| ai-intelligence | Vertex AI / Langfuse | Saída | API HTTP (Fase 3) |
| workflow-automation | Cloud Pub/Sub | Bidirecional | Mensageria (Fase 2) |

---

## 9. Fases do Roadmap

| Fase | Módulos | Deployables |
|---|---|---|
| Fase 1 MVP | authentication, tenant-administration, organization, account-management, partner-management, activity-management, goal-forecast, opportunity-pipeline, audit-log, data-migration, digest, reporting (sync), notification-delivery, azim-web | azim-api, azim-digest-worker, azim-reporting-worker, azim-web |
| Fase 2 | workflow-automation (+ in-app notifications como ação de workflow) | azim-workflow-worker |
| Fase 3 | ai-intelligence | azim-ai-service |

---

## 10. Compliance

| Compliance | Aplicável? | Módulos Afetados |
|---|---|---|
| LGPD | Sim | account-management (contacts: nome, e-mail, telefone), organization (users: e-mail, display_name), audit-log (delta_json mascarado) |
| PCI DSS | Não aplicável | Azim CRM não processa, transmite nem armazena dados de cartão |

---

## 11. Diagramas

| Diagrama | Caminho | Finalidade |
|---|---|---|
| Arquitetura da Solução | docs/product/modules/diagrams/solution-architecture.md | Módulos, deployables, sistemas externos e relações |
| Dependências entre Módulos | docs/product/modules/diagrams/module-dependencies.md | Grafo de dependências diretas |
| Fluxos de Integração | docs/product/modules/diagrams/integration-flows.md | Fluxos principais entre módulos e sistemas externos |
| Fluxos de Compliance | docs/product/modules/diagrams/compliance-flows.md | Consolidação de compliance LGPD |
| Compliance LGPD | docs/product/modules/diagrams/compliance-lgpd.md | Fluxo de PII por módulo |

---

## 12. Pontos a Validar

| Código | Ponto | Impacto |
|---|---|---|
| VAL-MOD-01 | Consolidar MOD-10 (Notificações In-App) e MOD-12 (Automações Visuais) no mesmo módulo workflow-automation ou separar em submódulos | Define escopo e backlog do azim-workflow-worker |
| VAL-MOD-02 | Confirmar se notification-delivery deve residir no azim-digest-worker ou ser um package compartilhado também usado pelo azim-api (convites) | Define deployable e dependência |
| VAL-MOD-03 | Confirmar Reporting como worker assíncrono desde Fase 1 ou manter como módulo síncrono do azim-api na Fase 1 (DDD-VAL-02) | Define deployable azim-reporting-worker |
| VAL-MOD-04 | Confirmar remoção do módulo data-migration após Fase 1 e limpeza arquitetural pós-import (DDD-VAL-03) | Define ciclo de vida do módulo |
| VAL-MOD-05 | Confirmar política de retenção de contacts (PII) e audit_logs sob LGPD com equipe jurídica (VAL-08 do data model) | Define regras de descarte e retenção |
