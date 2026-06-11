# Integration Flows — Azim CRM

**Status:** Rascunho para revisão
**Versão:** v0.1

---

## 1. Objetivo

Documentar os principais fluxos de integração entre módulos e sistemas externos do Azim CRM.

---

## 2. Fluxo 1 — Digest Diário (07:00 BRT)

```mermaid
sequenceDiagram
    participant CS as Cloud Scheduler
    participant DW as azim-digest-worker\n(digest module)
    participant API as azim-api
    participant ND as notification-delivery\n(IEmailSender)
    participant PM as Postmark / SendGrid
    participant DB as Cloud SQL

    CS->>DW: POST /internal/digest/trigger {tenant_id, date}
    DW->>API: GET /v1/users?role=Vendedor,GestorBU
    API-->>DW: Lista de destinatários
    loop Por usuário
        DW->>DB: SELECT email_digest_logs (idempotência RN-010)
        alt Já enviado hoje
            DW->>DW: Pular usuário
        else Não enviado
            DW->>API: GET /internal/pipeline/stale?user_id=X
            API-->>DW: Opps estagnadas
            DW->>API: GET /v1/activities/overdue?user_id=X
            API-->>DW: Atividades vencidas
            DW->>API: GET /v1/goals/forecast?bu_id=X (se segunda-feira)
            API-->>DW: Bloco de metas (ou null)
            DW->>DW: DigestComposer — gera HTML personalizado
            DW->>ND: IEmailSender.Send(EmailMessage)
            ND->>PM: HTTP POST (API do provedor)
            PM-->>ND: message_id, status
            ND-->>DW: SendResult
            DW->>DB: INSERT email_digest_logs (sent)
        end
    end
    DW-->>CS: 200 OK
```

**Regras do Fluxo:**

| Regra | Descrição |
|---|---|
| Idempotência RN-010 | UNIQUE(tenant_id, user_id, digest_date) em email_digest_logs — digest nunca enviado duas vezes no mesmo dia |
| Azimute apenas segunda | Bloco de metas (azimute) incluído somente às segundas-feiras (RN-029) |
| Graceful degradation RN-018 | Bloco de metas omitido quando não há meta cadastrada — sem erro |
| Seleção por fuso IANA | Horário UTC do trigger calculado pelo fuso IANA do tenant (padrão 07:00 local) |

**Pontos de Falha:**

| Ponto | Tratamento Esperado |
|---|---|
| Provedor de e-mail fora do ar | Retry com backoff exponencial; alerta imediato |
| azim-api indisponível | Retry do job; alerta; não deixar usuário sem digest silenciosamente |
| Token de ação expirado no e-mail | Link de fallback para o CRM web no e-mail |

---

## 3. Fluxo 2 — Criar e Ganhar Oportunidade com Comissão

```mermaid
sequenceDiagram
    participant V as Vendedor (azim-web)
    participant API as azim-api
    participant PM as partner-management
    participant AM as account-management
    participant DB as Cloud SQL
    participant AL as audit-log

    V->>API: POST /v1/opportunities\n{account_id, partner_id, stage_id, owner_id, ...}
    API->>AM: Validar account_id
    AM-->>API: OK
    API->>PM: Obter pct_setup, pct_recorrente do partner_id
    PM-->>API: CommissionDefaults
    API->>DB: INSERT opportunities (opportunity_number=AZ-NNNN)
    API->>DB: INSERT opportunity_partner_commissions (is_snapshot=false)
    API->>AL: AuditEvent(OpportunityCreated)
    API-->>V: 201 Created {id, opportunity_number}

    V->>API: POST /v1/opportunities/{id}/win
    API->>DB: SELECT opportunity + commission
    API->>DB: UPDATE opp_partner_commissions SET is_snapshot=TRUE, snapshot_at=now()
    API->>DB: INSERT opportunity_stage_transitions (won)
    API->>DB: UPDATE opportunities SET stage_category=won, closed_at=now()
    API->>AL: AuditEvent(OpportunityWon)
    API->>AL: AuditEvent(CommissionSnapshotCreated)
    API-->>V: 200 OK {opportunity, commission_snapshot}
```

**Regras do Fluxo:**

| Regra | Descrição |
|---|---|
| RN-001 | opportunity_number gerado sequencialmente no formato AZ-NNNN; imutável |
| RN-002 | owner_id obrigatório na criação |
| RN-007 | Snapshot de comissão imutável após is_snapshot=TRUE |
| RN-022 | Comissão calculada e congelada no momento do ganho |
| RN-026 | Fórmula: pct_setup × valor_setup + pct_recorrente × valor_mensal × meses_comissionados |

---

## 4. Fluxo 3 — Migração de Planilha (Fase 1 — único uso)

```mermaid
sequenceDiagram
    participant P as PlatOp
    participant API as azim-api\n(data-migration)
    participant AM as account-management
    participant PM as partner-management
    participant OP as opportunity-pipeline
    participant DB as Cloud SQL (transaction)
    participant AL as audit-log

    P->>API: POST /v1/migrations/upload (.xlsx)
    API-->>P: {job_id}
    P->>API: POST /v1/migrations/{job_id}/dry-run
    API->>API: DryRunService — valida sem escrever
    API-->>P: Relatório de erros e avisos por linha
    P->>API: POST /v1/migrations/{job_id}/execute (confirmação explícita)
    API->>DB: BEGIN TRANSACTION
    API->>AM: CreateAccount(bulk)
    AM->>DB: INSERT accounts
    API->>PM: CreatePartner(bulk)
    PM->>DB: INSERT partners
    API->>OP: CreateOpportunity(bulk)
    OP->>DB: INSERT opportunities
    alt Sem erros
        API->>DB: COMMIT
        API->>AL: AuditEvent(ImportCompleted)
        API-->>P: 200 OK — {imported: 108, errors: 0}
    else Qualquer erro
        API->>DB: ROLLBACK
        API-->>P: 422 — ImportFailed (nenhum dado persistido — RN-023)
    end
```

**Regras do Fluxo:**

| Regra | Descrição |
|---|---|
| RN-023 | Rollback total em caso de qualquer erro — nenhum dado parcial |
| NFR-PERF-06 | Import deve concluir em ≤ 5 minutos para 108 registros |
| Dry-run obrigatório | PlatOp deve revisar o relatório do dry-run antes de executar o import real |
| Confirmação explícita | O endpoint de execução requer confirmação explícita para evitar execução acidental |

---

## 5. Fluxo 4 — Autenticação e Carregamento de Contexto

```mermaid
sequenceDiagram
    participant U as Usuário
    participant W as azim-web
    participant FB as Firebase SDK
    participant GCP as GCP Identity Platform
    participant API as azim-api\n(authentication)
    participant Cache as Redis
    participant OrgDB as organization (users)

    U->>W: Login com e-mail/senha ou Google
    W->>FB: signInWithEmailAndPassword()
    FB->>GCP: Autenticar usuário no tenant
    GCP-->>FB: ID token JWT
    FB-->>W: ID token
    W->>API: Requisição com Bearer {ID_token}
    API->>API: AuthMiddleware — validar JWT
    API->>Cache: Buscar memberships em cache
    alt Cache hit
        Cache-->>API: user_id, tenant_id, papéis
    else Cache miss
        API->>OrgDB: SELECT users WHERE identity_uid
        OrgDB-->>API: user_id, tenant_id
        API->>OrgDB: SELECT user_memberships WHERE user_id
        OrgDB-->>API: papéis por BU
        API->>Cache: Armazenar (TTL 5 min)
    end
    API-->>W: Resposta autenticada com AuthContext
```

---

## 6. Fluxo 5 — Provisionamento de Tenant

```mermaid
sequenceDiagram
    participant P as PlatOp
    participant API as azim-api\n(tenant-administration)
    participant GCP as GCP Identity Platform
    participant OrgModule as organization
    participant DB as Cloud SQL

    P->>API: POST /v1/tenants {slug, display_name, iana_timezone}
    API->>GCP: Criar tenant de identidade (slug)
    GCP-->>API: identity_tenant_id
    API->>DB: INSERT tenants {id, slug, iana_timezone, digest_time}
    API->>DB: INSERT tenant_brandings (padrão)
    API-->>OrgModule: Evento TenantProvisioned {tenant_id}
    OrgModule->>DB: INSERT business_units (BU inicial)
    OrgModule->>DB: INSERT users (TAdmin inicial)
    API-->>P: 201 Created {tenant_id, slug}
```

---

## 7. Pontos de Falha Globais

| Ponto | Módulos Afetados | Tratamento Esperado |
|---|---|---|
| Cloud SQL indisponível | Todos | Circuit breaker; resposta 503; alerta imediato |
| Redis indisponível | authentication, digest, ai-intelligence | Fallback: consulta ao banco sem cache; alerta |
| GCP Identity Platform indisponível | authentication | Nenhum usuário consegue autenticar; alerta crítico |
| Postmark/SendGrid indisponível | notification-delivery, digest, organization | Retry com backoff; alerta; digest não entregue |
| Cloud Scheduler não dispara | digest | Alerta se digest não processar no horário esperado |
