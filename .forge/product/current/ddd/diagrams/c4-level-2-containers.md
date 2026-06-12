# C4 Level 2 — Container Diagram: Azim CRM

## Diagrama

```mermaid
flowchart TB
    Vendedor["Vendedor / Gestor /\nExecutivo / TAdmin"]
    PlatOp["Operador da Plataforma"]

    subgraph AzimCRM["Azim CRM — Sistema"]
        WebApp["azim-web\nReact SPA + TypeScript\nHosteado no Cloud Run\nTheming por tenant via CSS variables"]

        ApiGateway["API Gateway\nCloud Run ingress\nResolve slug para tenant_id\nRateLimit por tenant"]

        AzimApi["azim-api\nASP.NET Core / .NET 10\nMonólito modular Fase 1\nRLS + Global Query Filter EF Core\nHosteado no Cloud Run"]

        DigestWorker["azim-digest-worker\nASP.NET Core / .NET 10\nConsome Pub/Sub\nHosteado no Cloud Run"]

        ReportingWorker["azim-reporting-worker\nASP.NET Core / .NET 10\nRead models assíncronos\nHosteado no Cloud Run"]

        PubSub["Cloud Pub/Sub\nBarramento de eventos\nTópicos: digest, audit, domain-events"]

        CloudSQL["Cloud SQL / PostgreSQL\nMulti-tenant pooled com RLS\nEsquema único por tenant via tenant_id\nsouthamerica-east1"]

        Redis["Memorystore Redis\nCache de sessão\nCache de memberships e RBAC\nRate-limit por tenant"]

        SecretManager["GCP Secret Manager\nSegredos de conexão\nChaves de API de providers\nSem segredo em repositório"]

        BackofficeAPI["Backoffice API\nProvisioning de tenants\nApenas Platform Operator\nHosteado no Cloud Run"]
    end

    subgraph EXTERNAL["Sistemas Externos"]
        GCPIdP["GCP Identity Platform\nAutenticação multi-tenant"]
        Postmark["Postmark\nou SendGrid\nE-mail transacional"]
        CloudScheduler["Cloud Scheduler\nTrigger horário UTC"]
        CloudLogging["Cloud Logging\nCloud Monitoring\nCloud Trace\nObservabilidade"]
    end

    Vendedor -->|HTTPS\napp.azim.com.br/slug| WebApp
    PlatOp -->|HTTPS\nbackoffice.azim.com.br| BackofficeAPI

    WebApp -->|REST / JSON| ApiGateway
    ApiGateway -->|REST / JSON| AzimApi

    AzimApi -->|Leitura/Escrita SQL\nRLS ativo| CloudSQL
    AzimApi -->|Cache de sessão\ne RBAC| Redis
    AzimApi -->|Publica eventos| PubSub
    AzimApi -->|OAuth2 / OIDC| GCPIdP
    AzimApi -->|IEmailSender\n(convites, recuperação)| Postmark
    AzimApi -->|Logs e Traces| CloudLogging

    CloudScheduler -->|HTTP trigger| DigestWorker
    DigestWorker -->|Consome mensagens| PubSub
    DigestWorker -->|Leitura SQL| CloudSQL
    DigestWorker -->|IEmailSender\n(digest diário)| Postmark
    DigestWorker -->|Logs| CloudLogging

    PubSub -->|Eventos de domínio| ReportingWorker
    ReportingWorker -->|Read models SQL| CloudSQL
    ReportingWorker -->|Logs| CloudLogging

    BackofficeAPI -->|Provisiona tenants| CloudSQL
    BackofficeAPI -->|Logs| CloudLogging
    BackofficeAPI -->|Segredos| SecretManager

    AzimApi -->|Segredos| SecretManager
    DigestWorker -->|Segredos| SecretManager
```

## Descrição dos Containers

| Container | Tecnologia | Responsabilidade |
|---|---|---|
| azim-web | React + TypeScript, Cloud Run | SPA responsivo; theming por tenant via CSS variables; consome azim-api |
| azim-api | ASP.NET Core / .NET 10, Cloud Run | Monólito modular Fase 1; implementa BCs de negócio: Pipeline, Account, Partner, Activity, Goal, Organization, Migration, Tenancy, AuditLog; RLS via EF Core Global Query Filter |
| azim-digest-worker | ASP.NET Core / .NET 10, Cloud Run | Worker de digest: consome trigger do Cloud Scheduler; seleciona destinatários; compõe e envia o e-mail via Postmark |
| azim-reporting-worker | ASP.NET Core / .NET 10, Cloud Run | Worker de read models: atualiza projeções de relatório de forma assíncrona a partir de eventos de domínio |
| API Gateway | Cloud Run ingress / NGINX | Resolve slug → tenant_id; rate-limit por tenant; TLS termination |
| Backoffice API | ASP.NET Core, Cloud Run | Provisionamento de tenants (Platform Operator only); sem acesso a dados comerciais |
| Cloud SQL / PostgreSQL | Cloud SQL, southamerica-east1 | Banco principal multi-tenant pooled; RLS via SET app.current_tenant; índices compostos (tenant_id, bu_id, ...) |
| Memorystore Redis | GCP Memorystore | Cache de sessão autenticada; cache de memberships RBAC; rate-limit por tenant (Fase 3: IA) |
| Cloud Pub/Sub | GCP Pub/Sub | Barramento de eventos assíncronos: digest job, audit events, domain events para automação/IA |
| GCP Secret Manager | GCP Secret Manager | Todos os segredos de conexão e chaves de API; zero segredo em repositório |
| Cloud Logging / Monitoring | GCP Observabilidade | Logs estruturados com correlationId e tenant_id; métricas de latência; alertas; traces |

## Notas de Arquitetura

1. **RLS em duas camadas:** EF Core aplica Global Query Filter com `tenant_id`; PostgreSQL aplica RLS via `SET app.current_tenant` na conexão — garante isolamento mesmo em queries ad-hoc.

2. **Monólito modular na Fase 1:** O `azim-api` é um monólito modular onde cada bounded context é um módulo C# com namespace, projeto ou folder dedicado. A separação física em microsserviços é possível na Fase 2+ sem reescrita, apenas extração do módulo.

3. **Workers separados:** `azim-digest-worker` e `azim-reporting-worker` são Cloud Run separados para garantir que o processamento assíncrono não impacte a latência da API transacional.

4. **Fase 3 — AI Service:** Container adicional `azim-ai-service` em Python/FastAPI será adicionado na Fase 3. Consumirá dados do Cloud SQL via read model separado; rate-limit por tenant no Redis.
