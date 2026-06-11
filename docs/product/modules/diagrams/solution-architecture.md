# Solution Architecture Diagram — Azim CRM

**Status:** Rascunho para revisão
**Versão:** v0.1

---

## 1. Objetivo

Mostrar como os módulos da solução Azim CRM se relacionam em alto nível, incluindo deployables, sistemas externos, zonas de dados e compliance.

---

## 2. Diagrama — Fase 1 MVP

```mermaid
flowchart TB
    subgraph External["Sistemas Externos e Atores"]
        User[Usuário\nVendedor / Gestor / TAdmin]
        PlatOp[PlatOp\nOperador da Plataforma]
        GcpIdP[GCP Identity Platform\nFirebase Auth Multi-Tenant]
        Postmark[Postmark / SendGrid\nProvedor de E-mail]
        CloudScheduler[Cloud Scheduler\nTrigger 07:00 BRT]
    end

    subgraph Frontend["azim-web — Frontend SPA"]
        WebApp[azim-web\nReact + TypeScript\nWhite-label por tenant]
    end

    subgraph AzimAPI["azim-api — Monólito Modular (.NET 10)"]
        direction TB
        AuthMod[authentication\nACL GCP Identity Platform]
        TenantMod[tenant-administration\nTenancy e Branding]
        OrgMod[organization\nBUs, Usuários, RBAC]
        AccountMod[account-management\nContas e Contatos PII]
        PartnerMod[partner-management\nParceiros e Comissão]
        ActivityMod[activity-management\nAtividades e Follow-ups]
        GoalMod[goal-forecast\nMetas e Forecast]
        PipelineMod[opportunity-pipeline\nCore Domain\nKanban, Comissão, Snapshot]
        AuditMod[audit-log\nImutável Cross-cutting]
        MigrationMod[data-migration\nImport Excel Fase 1]
        ReportingMod[reporting\nFase 1: síncrono]
    end

    subgraph DigestWorker["azim-digest-worker (.NET 10 — Cloud Run)"]
        DigestMod[digest\nSeleção + Composição + Envio]
        NotifMod[notification-delivery\nACL IEmailSender]
    end

    subgraph DataLayer["Data Layer — GCP southamerica-east1"]
        SQLDB[(Cloud SQL / Postgres\nMulti-tenant + RLS)]
        Redis[(Memorystore / Redis\nCache + Rate-limit)]
        GCS[(Cloud Storage\nLogo / Favicon / CSV)]
        PubSub[Cloud Pub/Sub\nEventos de Domínio]
    end

    User --> WebApp
    PlatOp --> WebApp
    WebApp --> AuthMod
    WebApp --> AzimAPI
    AuthMod --> GcpIdP
    TenantMod --> GcpIdP
    DigestMod --> NotifMod
    NotifMod --> Postmark
    CloudScheduler -->|HTTP trigger| DigestMod
    OrgMod --> NotifMod
    AzimAPI --> SQLDB
    AzimAPI --> Redis
    AzimAPI --> GCS
    DigestWorker --> SQLDB
    DigestWorker --> Redis
    PipelineMod -->|Fase 2 DDD-VAL-05| PubSub
```

---

## 3. Diagrama — Fase 2 e Fase 3 (Expansão)

```mermaid
flowchart TB
    subgraph Phase1["Fase 1 (MVP)"]
        AzimAPI2[azim-api]
        DigestWorker2[azim-digest-worker]
        ReportingWorker[azim-reporting-worker\nFase 2: assíncrono]
        WebApp2[azim-web]
    end

    subgraph Phase2["Fase 2"]
        WorkflowWorker[azim-workflow-worker\nAutomações por BU\nPub/Sub Consumer]
    end

    subgraph Phase3["Fase 3"]
        AiService[azim-ai-service\nPython / FastAPI / LangGraph\nScoring, Briefing, Copilot]
    end

    subgraph ExternalPhase23["Sistemas Externos Fase 2/3"]
        PubSub2[Cloud Pub/Sub\nopportunity-events]
        Langfuse[Langfuse\nObservabilidade LLM]
        VertexAI[Vertex AI / LLM API]
    end

    AzimAPI2 -->|opportunity.* events| PubSub2
    PubSub2 --> WorkflowWorker
    PubSub2 --> ReportingWorker
    AiService --> VertexAI
    AiService --> Langfuse
    WebApp2 --> AiService
```

---

## 4. Mapa de Deployables

| Deployable | Tipo | Runtime | Região | Fase |
|---|---|---|---|---|
| azim-api | Monólito Modular | .NET 10 / ASP.NET Core / Cloud Run | southamerica-east1 | Fase 1 |
| azim-digest-worker | Worker | .NET 10 / Cloud Run | southamerica-east1 | Fase 1 |
| azim-reporting-worker | Worker | .NET 10 / Cloud Run | southamerica-east1 | Fase 1 (sync) / Fase 2 (async) |
| azim-web | Frontend SPA | React + TypeScript / Firebase Hosting | CDN global | Fase 1 |
| azim-workflow-worker | Worker | .NET 10 / Cloud Run | southamerica-east1 | Fase 2 |
| azim-ai-service | Microservice | Python / FastAPI / Cloud Run | southamerica-east1 | Fase 3 |

---

## 5. Zonas de Segurança

| Zona | Componentes | Regras |
|---|---|---|
| Edge / Público | azim-web, GCP Load Balancer | HTTPS obrigatório; sem lógica de domínio |
| API Interna | azim-api | Autenticação obrigatória (Bearer token validado por AuthMiddleware); RLS por tenant_id |
| Workers | azim-digest-worker, azim-reporting-worker, azim-workflow-worker | Autenticação M2M via IAM Service Account; sem acesso público |
| Data | Cloud SQL, Redis, Cloud Storage | VPC privada; acesso apenas pelos deployables autorizados |
| IA (Fase 3) | azim-ai-service | Isolamento por tenant; rate-limit via Redis; dados nunca cruzam tenants |

---

## 6. Observações

- **Fase 1:** monólito modular no azim-api com fronteiras de módulo preservadas — separação física possível na Fase 2+.
- **Tenant RLS:** todo acesso ao banco é filtrado por `tenant_id` via Row Level Security do Postgres.
- **Digest separado:** azim-digest-worker deployado independentemente para garantir que falha no digest não afeta a API transacional e vice-versa.
- **DDD-VAL-05:** publicação de `opportunity.*` via Pub/Sub para o Workflow Automation precisa ser confirmada antes da Fase 2.
