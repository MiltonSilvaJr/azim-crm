# Data Model — Azim CRM

## Controle de Versão

| Versão | Data | Descrição |
|---|---|---|
| v1.0 | 2026-06-11 | Criação inicial do data model orientado por ownership de bounded contexts |

---

## 1. Princípios de Ownership

- Cada bounded context possui ownership claro sobre seus dados.
- Apenas o contexto dono pode escrever diretamente em suas tabelas.
- Outros contextos consomem dados via API, eventos, read models ou views controladas.
- Não há escrita cruzada entre contextos.
- Joins diretos entre tabelas de contextos diferentes devem ser evitados — usar read models ou views projetadas.
- Todos os valores monetários são armazenados como centavos inteiros (integer cents) acompanhados de moeda ISO-4217 (DEC-011; multimoeda aprovada — ADR-0008). Objeto de valor Money = `amount_cents` (BIGINT) + `currency` (CHAR(3)). Moedas pré-cadastradas: BRL, USD, EUR.
- Toda tabela de entidade de negócio carrega `tenant_id UUID NOT NULL` como coluna obrigatória para RLS.
- `created_at` e `updated_at` são obrigatórios em toda tabela; `deleted_at` para soft-delete quando aplicável.

---

## 2. Data Ownership Matrix

| Bounded Context | Entidade/Tabela | Tipo de Dado | Dono da Escrita | Consumidores | Forma de Consumo |
|---|---|---|---|---|---|
| Opportunity Pipeline | opportunities | Transacional | Opportunity Pipeline | Digest, Reporting, Goal&Forecast | API/Read Model |
| Opportunity Pipeline | opportunity_stage_transitions | Histórico | Opportunity Pipeline | Reporting, Audit Log | Read Model/Evento |
| Opportunity Pipeline | opportunity_partner_commissions | Imutável (snapshot) | Opportunity Pipeline | Reporting, Partner Management | API/Read Model |
| Opportunity Pipeline | stages | Configuração | Organization Management (escrita); Opportunity Pipeline (consumo) | Opportunity Pipeline | API |
| Opportunity Pipeline | origin_channels | Configuração | Organization Management (escrita) | Opportunity Pipeline | API |
| Opportunity Pipeline | loss_reasons | Configuração | Organization Management (escrita) | Opportunity Pipeline | API |
| Account Management | accounts | Transacional | Account Management | Opportunity Pipeline, Activity Management, Reporting | API |
| Account Management | contacts | Transacional (PII) | Account Management | Reporting | API (restrito) |
| Partner Management | partners | Transacional | Partner Management | Opportunity Pipeline, Reporting | API |
| Activity Management | activities | Transacional | Activity Management | Digest, Reporting | API/Evento |
| Goal & Forecast | goals | Transacional | Goal & Forecast | Digest, Reporting | API |
| Digest | email_digest_logs | Log | Digest | KPI dashboard | API (somente leitura) |
| Digest | digest_action_tokens | Temporário | Digest | Digest (validação de 1 clique) | Interno |
| Organization Management | business_units | Configuração | Organization Management | Todos os BCs | API |
| Organization Management | users | Transacional | Organization Management | Todos os BCs | API/Cache |
| Organization Management | user_memberships | Configuração | Organization Management | Todos os BCs | API/Cache |
| Organization Management | user_invitations | Temporário | Organization Management | Identity & Access | API |
| Tenancy & Branding | tenants | Configuração | Tenancy & Branding | Todos os BCs (tenant_id) | Cross-cutting RLS |
| Tenancy & Branding | tenant_brandings | Configuração | Tenancy & Branding | azim-web (CSS variables) | API |
| Data Migration | migration_jobs | Log (temporário) | Data Migration | — | Interno |
| Data Migration | migration_logs | Log (temporário) | Data Migration | — | Interno |
| Audit Log | audit_logs | Imutável (append-only) | Audit Log | Tenant Admin, Gestor de BU | API (somente leitura) |

---

## 3. Entidades por Contexto

### Opportunity Pipeline (BC-01 — Core Domain)

| Entidade | Tipo | Descrição | Persistência |
|---|---|---|---|
| Opportunity | Aggregate | Oportunidade comercial: owner, conta, parceiro, estágio, valor, comissão | opportunities |
| OpportunityStageTransition | Entity | Registro de cada transição de estágio com ator e timestamp | opportunity_stage_transitions |
| OpportunityPartnerCommission | Entity | Comissão por componente; snapshot imutável ao ganhar | opportunity_partner_commissions |
| OpportunityNumber | Value Object | Número sequencial AZ-NNNN | Campo em opportunities |
| Money | Value Object | Valor em centavos inteiros + moeda ISO-4217 (BRL/USD/EUR) | Campos de valor em opportunities, commissions e goals (ADR-0008) |
| CommissionCalculation | Value Object | Resultado calculado de comissão | Campo calculado; persiste o resultado em commissions |

**Schema principal — tabela `opportunities`:**

```sql
opportunities (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id       UUID NOT NULL,                    -- RLS
  bu_id           UUID NOT NULL REFERENCES business_units(id),
  account_id      UUID NOT NULL REFERENCES accounts(id),
  partner_id      UUID REFERENCES partners(id),     -- nullable (sem parceiro)
  stage_id        UUID NOT NULL REFERENCES stages(id),
  owner_id        UUID NOT NULL REFERENCES users(id), -- RN-002: obrigatório
  origin_channel_id UUID NOT NULL REFERENCES origin_channels(id),
  opportunity_number VARCHAR(10) NOT NULL,          -- RN-001: AZ-NNNN imutável; unicidade por tenant (ver UNIQUE composta abaixo)
  title           TEXT NOT NULL,
  currency        CHAR(3) NOT NULL DEFAULT 'BRL',   -- ISO-4217 (ADR-0008): BRL/USD/EUR
  valor_setup     BIGINT NOT NULL DEFAULT 0,        -- DEC-011: centavos na moeda da oportunidade
  valor_mensal    BIGINT NOT NULL DEFAULT 0,        -- centavos
  duracao_meses   INTEGER NOT NULL DEFAULT 0,
  valor_total     BIGINT GENERATED ALWAYS AS        -- RN-005: calculado
                    (valor_setup + valor_mensal * duracao_meses) STORED,
  probabilidade   SMALLINT NOT NULL DEFAULT 0,      -- de stages
  forecast_ponderado BIGINT GENERATED ALWAYS AS     -- RN-006: calculado
                    (valor_total * probabilidade / 100) STORED,
  data_fechamento_esperada DATE,                    -- RN-003: obrigatória a partir de Proposta Enviada
  loss_reason_id  UUID REFERENCES loss_reasons(id), -- RN-004: obrigatório ao perder
  closed_at       TIMESTAMPTZ,
  stage_category  VARCHAR(20) NOT NULL DEFAULT 'open', -- open/won/lost
  notes           TEXT,
  created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
  created_by      UUID NOT NULL REFERENCES users(id),
  CONSTRAINT uq_opportunity_number_per_tenant UNIQUE (tenant_id, opportunity_number) -- FIND-TRD-001 / ADR-0003: unicidade por tenant, não global
)
```

**Tabela `opportunity_partner_commissions` (snapshot imutável):**

```sql
opportunity_partner_commissions (
  id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id           UUID NOT NULL,
  opportunity_id      UUID NOT NULL REFERENCES opportunities(id),
  partner_id          UUID NOT NULL REFERENCES partners(id),
  currency            CHAR(3) NOT NULL DEFAULT 'BRL', -- ISO-4217 (ADR-0008): herda da oportunidade
  pct_setup           NUMERIC(5,2) NOT NULL DEFAULT 0,
  pct_recorrente      NUMERIC(5,2) NOT NULL DEFAULT 0,
  valor_fixo          BIGINT NOT NULL DEFAULT 0,
  meses_comissionados INTEGER NOT NULL DEFAULT 0,
  comissao_calculada  BIGINT NOT NULL,              -- RN-026: resultado em centavos
  is_snapshot         BOOLEAN NOT NULL DEFAULT FALSE, -- TRUE = imutável (RN-007)
  snapshot_at         TIMESTAMPTZ,
  created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
  -- Constraint: sem UPDATE quando is_snapshot = TRUE (aplicado por policy/trigger)
)
```

---

### Account Management (BC-02)

| Entidade | Tipo | Descrição | Persistência |
|---|---|---|---|
| Account | Aggregate | Empresa cliente; compartilhada entre BUs no tenant | accounts |
| Contact | Entity | Pessoa física vinculada à conta (PII) | contacts |
| NormalizedName | Value Object | Nome normalizado para dedupe | Campo em accounts |

```sql
accounts (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id       UUID NOT NULL,
  name            TEXT NOT NULL,                    -- nome de exibição da conta
  normalized_name TEXT NOT NULL,                    -- RN-014: dedupe
  cnpj            VARCHAR(14),                       -- PII mantida (decisão HITL #1)
  razao_social    TEXT,                              -- PII mantida (decisão HITL #1)
  nome_fantasia   TEXT,                              -- PII mantida (decisão HITL #1)
  website         TEXT,
  notes           TEXT,
  created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
)

contacts (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id       UUID NOT NULL,
  account_id      UUID NOT NULL REFERENCES accounts(id),
  name            TEXT NOT NULL,                    -- PII: mascarar em logs (RN-025)
  email           TEXT,                             -- PII
  phone           TEXT,                             -- PII
  role            TEXT,
  created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
)
```

---

### Partner Management (BC-03)

| Entidade | Tipo | Descrição | Persistência |
|---|---|---|---|
| Partner | Aggregate | Parceiro comissionado com percentuais default por componente | partners |
| CommissionDefaults | Value Object | pct_setup e pct_recorrente default do parceiro | Campos em partners |

```sql
partners (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id       UUID NOT NULL,
  name            TEXT NOT NULL,
  partner_type    TEXT NOT NULL,
  pct_setup       NUMERIC(5,2) NOT NULL DEFAULT 0,
  pct_recorrente  NUMERIC(5,2) NOT NULL DEFAULT 0,
  notes           TEXT,
  active          BOOLEAN NOT NULL DEFAULT TRUE,
  created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
)
```

---

### Activity Management (BC-04)

| Entidade | Tipo | Descrição | Persistência |
|---|---|---|---|
| Activity | Aggregate | Ação comercial com tipo, data, responsável e status | activities |

```sql
activities (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id       UUID NOT NULL,
  bu_id           UUID NOT NULL REFERENCES business_units(id),
  owner_id        UUID NOT NULL REFERENCES users(id),
  opportunity_id  UUID REFERENCES opportunities(id), -- nullable (pode ser apenas conta)
  account_id      UUID REFERENCES accounts(id),
  activity_type   VARCHAR(20) NOT NULL,             -- call, meeting, email, task, follow_up
  title           TEXT NOT NULL,
  description     TEXT,
  status          VARCHAR(20) NOT NULL DEFAULT 'pending', -- pending, in_progress, completed, cancelled (§BC-04, DD-001)
  priority        VARCHAR(10) NOT NULL DEFAULT 'medium',  -- low, medium, high (§BC-04, Req 1)
  due_at          TIMESTAMPTZ NOT NULL,
  completed_at    TIMESTAMPTZ,
  created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
  CONSTRAINT chk_activities_status   CHECK (status IN ('pending', 'in_progress', 'completed', 'cancelled')),
  CONSTRAINT chk_activities_priority CHECK (priority IN ('low', 'medium', 'high'))
)
```

> **Nota §BC-04** (atualizado TASK-24): colunas `status` e `priority` adicionadas conforme implementação do agregado `Activity` (design §4.3, DD-001). Índice recomendado: `(tenant_id, status, due_at)` para queries de overdue scan.

---

### Goal & Forecast (BC-05)

| Entidade | Tipo | Descrição | Persistência |
|---|---|---|---|
| Goal | Aggregate | Meta mensal por BU e/ou responsável em centavos | goals |
| GoalPeriod | Value Object | Período (ano, mês) | Campos em goals |

```sql
goals (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id       UUID NOT NULL,
  bu_id           UUID REFERENCES business_units(id), -- nullable (pode ser por responsável)
  owner_id        UUID REFERENCES users(id),           -- nullable (pode ser por BU)
  year            SMALLINT NOT NULL,
  month           SMALLINT NOT NULL CHECK (month BETWEEN 1 AND 12),
  currency        CHAR(3) NOT NULL DEFAULT 'BRL',    -- ISO-4217 (ADR-0008)
  valor_meta      BIGINT NOT NULL,                   -- centavos inteiros (DEC-011)
  created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
  UNIQUE (tenant_id, bu_id, owner_id, year, month)
)
```

---

### Digest (BC-06)

| Entidade | Tipo | Descrição | Persistência |
|---|---|---|---|
| DigestJob | Aggregate | Execução do digest para um tenant em uma data | (in-memory; log no EmailDigestLog) |
| EmailDigestLog | Entity | Registro de envio por (user_id, date) — idempotência | email_digest_logs |
| DigestActionToken | Entity | Token de 1 clique para conclusão/reagendamento | digest_action_tokens |

```sql
email_digest_logs (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id       UUID NOT NULL,
  user_id         UUID NOT NULL REFERENCES users(id),
  digest_date     DATE NOT NULL,                    -- data local no fuso do tenant
  status          VARCHAR(20) NOT NULL,             -- sent, delivered, opened, bounced, failed
  sent_at         TIMESTAMPTZ,
  delivered_at    TIMESTAMPTZ,
  opened_at       TIMESTAMPTZ,
  UNIQUE (tenant_id, user_id, digest_date)          -- RN-010: idempotência
)

digest_action_tokens (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id       UUID NOT NULL,
  user_id         UUID NOT NULL,
  activity_id     UUID REFERENCES activities(id),
  action          VARCHAR(20) NOT NULL,             -- complete, reschedule
  token_hash      VARCHAR(64) NOT NULL,             -- SHA-256 hex do token opaco (§BC-06, DD-003, RNF 5)
  expires_at      TIMESTAMPTZ NOT NULL,
  used_at         TIMESTAMPTZ,
  created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
  UNIQUE (token_hash)                               -- busca O(1) e unicidade garantida por índice
)
```

> **Nota §BC-06** (atualizado TASK-24): coluna `token_hash` adicionada conforme implementação do `DigestActionToken` (design §10, DD-003). O token opaco é gerado aleatoriamente e apenas seu hash SHA-256 é persistido. Busca por hash em tempo constante via `ConstantTimeComparison` (TASK-23, PBT-03). Índice único em `token_hash` garante anti-enumeração e idempotência de uso.

---

### Organization Management (BC-08)

| Entidade | Tipo | Descrição | Persistência |
|---|---|---|---|
| BusinessUnit | Aggregate | BU com estágios, canais e motivos de perda | business_units |
| User | Aggregate | Usuário do tenant com papel e memberships | users |
| UserMembership | Entity | Vínculo usuário-BU com papel | user_memberships |
| UserInvitation | Entity | Convite temporário com token | user_invitations |
| Stage | Entity | Estágio configurável por BU | stages |
| OriginChannel | Entity | Canal de origem configurável | origin_channels |
| LossReason | Entity | Motivo de perda configurável | loss_reasons |

```sql
business_units (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id       UUID NOT NULL,
  name            TEXT NOT NULL,
  active          BOOLEAN NOT NULL DEFAULT TRUE,
  created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
  UNIQUE (tenant_id, name)                          -- MSG-015
)

users (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id       UUID NOT NULL,
  email           TEXT NOT NULL,
  display_name    TEXT NOT NULL,
  identity_uid    TEXT NOT NULL,                    -- UID do GCP Identity Platform
  active          BOOLEAN NOT NULL DEFAULT TRUE,    -- RN-013: sem exclusão física
  created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
  deactivated_at  TIMESTAMPTZ,
  UNIQUE (tenant_id, email)
)

user_memberships (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id       UUID NOT NULL,
  user_id         UUID NOT NULL REFERENCES users(id),
  bu_id           UUID NOT NULL REFERENCES business_units(id),
  papel           VARCHAR(30) NOT NULL,             -- TAdmin, GestorBU, Vendedor, Viewer
  created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
  UNIQUE (tenant_id, user_id, bu_id)
)

stages (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id       UUID NOT NULL,
  bu_id           UUID NOT NULL REFERENCES business_units(id),
  name            TEXT NOT NULL,
  probability     SMALLINT NOT NULL DEFAULT 0 CHECK (probability BETWEEN 0 AND 100),
  category        VARCHAR(10) NOT NULL,             -- open, won, lost
  position        SMALLINT NOT NULL,
  created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
  UNIQUE (tenant_id, bu_id, name)
)
```

---

### Tenancy & Branding (BC-13)

| Entidade | Tipo | Descrição | Persistência |
|---|---|---|---|
| Tenant | Aggregate | Empresa cliente da plataforma com tenant_id e slug | tenants |
| TenantBranding | Entity | Configurações de white-label: logo, favicon, cores | tenant_brandings |

```sql
tenants (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  slug            TEXT NOT NULL UNIQUE,             -- RN-019: imutável após definição
  display_name    TEXT NOT NULL,
  iana_timezone   TEXT NOT NULL DEFAULT 'America/Sao_Paulo',
  digest_time     TIME NOT NULL DEFAULT '07:00',
  active          BOOLEAN NOT NULL DEFAULT TRUE,
  provisioned_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
  created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
)

tenant_brandings (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id       UUID NOT NULL UNIQUE REFERENCES tenants(id),
  logo_url        TEXT,                             -- PNG/SVG ≤ 1 MB (RN-020)
  favicon_url     TEXT,
  primary_color   CHAR(7),                          -- #RRGGBB
  secondary_color CHAR(7),                          -- #RRGGBB
  wcag_contrast_ok BOOLEAN NOT NULL DEFAULT FALSE,  -- RN-020
  updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
)
```

---

### Audit Log (BC-15)

| Entidade | Tipo | Descrição | Persistência |
|---|---|---|---|
| AuditLog | Event (append-only) | Registro imutável de toda escrita em entidade de negócio | audit_logs |

```sql
audit_logs (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id       UUID NOT NULL,
  user_id         UUID NOT NULL,
  entity_type     VARCHAR(50) NOT NULL,             -- 'Opportunity', 'Account', etc.
  entity_id       UUID NOT NULL,
  action          VARCHAR(20) NOT NULL,             -- 'create', 'update', 'delete'
  delta_json      JSONB NOT NULL,                   -- diff; PII mascarada (RN-025)
  created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
  -- SEM updated_at; SEM DELETE permission; append-only
)
```

---

## 4. Fronteiras de Persistência

| Origem | Destino | Permitido? | Forma Correta | Observação |
|---|---|---|---|---|
| Digest → opportunities | Escrita direta | Não | API do Opportunity Pipeline | Digest não escreve no Pipeline |
| Reporting → opportunities | Leitura direta | Sim (read-only) | View ou query otimizada no mesmo schema | Fase 1: ok; Fase 2: read model assíncrono |
| Opportunity Pipeline → contacts | Leitura direta | Sim (read-only) | API do Account Management | Para exibir dados do contato na oportunidade |
| Data Migration → opportunities | Escrita transacional | Sim (caso de uso específico) | Transação única com rollback (RN-023) | Apenas durante a migração da Fase 1 |
| AI Intelligence → opportunities | Leitura direta | Sim (read-only) | View ou snapshot de dados para IA | Fase 3; sem escrita; dados isolados por tenant |
| Qualquer BC → audit_logs | Escrita direta | Sim (append-only) | INSERT sem UPDATE/DELETE | Via AuditService centralizado |

---

## 5. Eventos Persistidos

| Evento | Contexto Dono | Persistência | Retenção | Consumidores |
|---|---|---|---|---|
| AuditEvent (toda escrita) | Audit Log | audit_logs (append-only) | A definir com jurídico (VAL-08) | Tenant Admin, Gestor de BU |
| DigestSent | Digest | email_digest_logs | 90 dias (KPI-03/KPI-04) | Dashboard de KPI |
| StageTransition | Opportunity Pipeline | opportunity_stage_transitions | Indefinida (linha do tempo) | Reporting, Audit |
| CommissionSnapshot | Opportunity Pipeline | opportunity_partner_commissions (is_snapshot=true) | Indefinida | Reporting, Partner Management |

---

## 6. Read Models e Views

| Read Model / View | Dono (do dado de origem) | Fontes | Consumidores | Atualização |
|---|---|---|---|---|
| KanbanView | Opportunity Pipeline | opportunities, stages | azim-web (Kanban) | Síncrona na query |
| FunnelReport | Opportunity Pipeline | opportunities, stages | Reporting | Síncrona (Fase 1); assíncrona via worker (Fase 2) |
| CommissionReport | Opportunity Pipeline + Partner Management | opportunity_partner_commissions, partners | Reporting | Síncrona (Fase 1) |
| ForecastView | Opportunity Pipeline + Goal & Forecast | opportunities, goals | Goal & Forecast, Digest | Síncrona na query |
| StagnationView | Opportunity Pipeline + Activity Management | opportunities, activities | Digest, Kanban | Calculada em tempo real (índice em activities.completed_at) |
| Account360View | Account Management + Opportunity Pipeline + Activity Management | accounts, opportunities, activities, contacts | azim-web (360° da conta) | Síncrona na query |

---

## 7. Pontos a Validar

- ~~VAL-05~~ **RESOLVIDO (ADR-0008):** multimoeda aprovada; campo `currency` (CHAR(3), ISO-4217) adicionado em `opportunities`, `opportunity_partner_commissions` e `goals`. Moedas pré-cadastradas: BRL, USD, EUR. Conversão entre moedas fora do MVP (cada oportunidade/meta tem moeda única; relatórios consolidam por moeda).
- VAL-08: Política de retenção de audit_logs e contacts (PII) sob LGPD — confirmar com equipe jurídica antes do go-live
- DDD-VAL-01: Se Activity Management se tornar módulo do Opportunity Pipeline, a tabela activities pode ser consolidada no schema do Pipeline
- Índices obrigatórios de performance: (tenant_id, bu_id, stage_id) em opportunities; (tenant_id, opportunity_id, completed_at) em activities; (tenant_id, entity_type, entity_id) em audit_logs
