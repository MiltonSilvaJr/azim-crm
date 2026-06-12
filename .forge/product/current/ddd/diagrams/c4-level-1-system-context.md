# C4 Level 1 — System Context: Azim CRM

## Diagrama

```mermaid
flowchart TB
    Vendedor["Vendedor / Executivo de Contas\n(P-01)\nOpera o Kanban diário;\nrecebe e age pelo digest"]
    Gestor["Gestor de BU\n(P-02)\nAcompanha pipeline e metas;\nrecebe azimute da semana"]
    Executivo["Executivo do Tenant\n(P-03)\nVisão macro consolidada;\nrecebe azimute da semana"]
    TAdmin["Administrador do Tenant\n(P-04)\nConfigura branding, BUs,\nusuários e parceiros"]
    PlatOp["Operador da Plataforma\n(P-05)\nProvisiona tenants;\nmonitora a plataforma"]

    AzimCRM["Azim CRM\nPlataforma SaaS multi-tenant\nde gestão comercial B2B\n\nFornece: Pipeline com owner obrigatório,\ncomissão nativa, digest diário,\nmetas e relatórios"]

    GCPIdP["GCP Identity Platform\nAutenticação multi-tenant\nGoogle / e-mail e senha"]
    EmailProvider["Provider de E-mail\nPostmark (candidato)\nou SendGrid\nEntrega de e-mails transacionais"]
    CloudScheduler["Cloud Scheduler\nTrigger horário UTC\ndo job de digest"]
    GCPInfra["GCP southamerica-east1\nCloud SQL, Cloud Run,\nPub/Sub, Secret Manager,\nCloud Logging"]

    Vendedor -->|Acessa via app.azim.com.br/slug\nOpera Kanban, registra atividades| AzimCRM
    Gestor -->|Acessa pipeline e metas da BU| AzimCRM
    Executivo -->|Acessa visão macro e relatórios| AzimCRM
    TAdmin -->|Configura o tenant| AzimCRM
    PlatOp -->|Provisiona tenants via backoffice| AzimCRM

    AzimCRM -->|Autentica usuários via OIDC| GCPIdP
    AzimCRM -->|Envia e-mails transacionais e digest| EmailProvider
    CloudScheduler -->|Dispara job horário UTC| AzimCRM
    AzimCRM -->|Hospedado em| GCPInfra

    AzimCRM -->|Envia digest diário 07:00 BRT| Vendedor
    AzimCRM -->|Envia azimute da semana\nnas segundas-feiras| Gestor
    AzimCRM -->|Envia azimute da semana\nnas segundas-feiras| Executivo
```

## Descrição

O **Azim CRM** é a plataforma central que substitui a operação em planilha Excel da Vellus. Cinco tipos de atores humanos interagem com o sistema, três sistemas externos são consumidos e dois sistemas de infraestrutura GCP são utilizados.

### Atores

| Ator | Interação Principal |
|---|---|
| Vendedor (P-01) | Kanban diário, registro de atividades, ação pelo digest |
| Gestor de BU (P-02) | Acompanhamento de pipeline, metas, redistribuição de oportunidades |
| Executivo do Tenant (P-03) | Visão macro, relatórios, azimute da semana |
| Administrador do Tenant (P-04) | Configuração de branding, BUs, usuários, parceiros e metas |
| Operador da Plataforma (P-05) | Provisionamento de tenants, monitoramento da plataforma |

### Sistemas Externos

| Sistema | Finalidade |
|---|---|
| GCP Identity Platform | Autenticação multi-tenant por e-mail/senha e Google OAuth |
| Provider de E-mail (Postmark/SendGrid) | Entrega de digest diário e e-mails transacionais (convites, recuperação de senha) |
| Cloud Scheduler | Disparo do job horário UTC do serviço de digest |
| GCP (Cloud SQL, Cloud Run, Pub/Sub, etc.) | Infraestrutura de hospedagem e execução |
