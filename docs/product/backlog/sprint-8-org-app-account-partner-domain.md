# Sprint 8 — org-app-account-partner-domain

- **Objetivo:** Ao final desta sprint, um TAdmin consegue configurar estágios do pipeline, canais de origem e motivos de perda via API, parceiros são cadastráveis com comissões default por componente, e contas B2B são criáveis com dedupe automático por nome normalizado, viabilizando o cadastro base necessário para o pipeline de oportunidades.
- **Período:** 2026-09-21 a 2026-10-04
- **Story Points totais:** 26
- **Status:** Planejada
- **Dependências:** Sprint 7 (organization foundation, RBAC middleware disponível)

---

## 1. Backlog da Sprint

### US-027 — Configurar estágios do pipeline por BU

- **Épico:** organization (EP-005)
- **RF rastreado:** RF org-pipeline
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `POST/PATCH/DELETE /api/v1/stages` — CRUD de estágios com nome, categoria (open/won/lost/abandoned), probabilidade 0-100%, ordem
- [ ] Ao menos 1 estágio de categoria `open` obrigatório por BU; remoção de estágio com oportunidades ativas é rejeitada
- [ ] Controllers de organization com OpenAPI completo e catálogo de erros ORG-ERR

#### Tasks técnicas
- organization/TASK-15: EF Core mappings + migration inicial + RLS global filter — TO DO — pendente Jira
- organization/TASK-16: Repositórios de domínio (BU, User, Invitation) — TO DO — pendente Jira
- organization/TASK-17: Redis adapter IMembershipCache + Testcontainers — TO DO — pendente Jira
- organization/TASK-20: Controllers de BusinessUnit + RBAC middleware — TO DO — pendente Jira
- organization/TASK-22: Controllers de pipeline config + OpenAPI + catálogo de erros — TO DO — pendente Jira

#### Definition of Done
- [ ] Infrastructure.Tests ≥ 70%; Api.Tests ≥ 80%; CI verde; Code review; PR mergeado

---

### US-028 — Configurar canais de origem e motivos de perda

- **Épico:** organization (EP-005)
- **RF rastreado:** RF org-config
- **Story Points:** 3
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `POST/PATCH/DELETE /api/v1/origin-channels` e `/api/v1/loss-reasons` com RBAC (TAdmin)
- [ ] Valores de seed padrão criados no provisionamento inicial

#### Definition of Done
- [ ] Incluso nas tasks de organization acima; CI verde; PR mergeado

---

### US-030 — Criar e pesquisar contas com dedupe automático

- **Épico:** account-management (EP-006)
- **RF rastreado:** RF acc-create, RF acc-dedupe
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `AccountManagement.sln` com 5 projetos compilando; Architecture.Tests verdes
- [ ] `Account` aggregate com `NameNormalizer` (normalização unicode, trim, lowercase para dedupe); PBT-01 e PBT-02 verdes
- [ ] `Account` + `Contact` aggregate com events e `IAccountRepository`; PBT-03 verde
- [ ] `POST /api/v1/accounts` cria conta; busca por nome normalizado retorna candidatos a duplicata antes da criação

#### Tasks técnicas
- account-management/TASK-01: Solution .NET + 5 projetos + Architecture.Tests — TO DO — pendente Jira
- account-management/TASK-02: Objetos de valor + NameNormalizer (PBT-01, PBT-02) — TO DO — pendente Jira
- account-management/TASK-03: Aggregate Account + Contact + eventos + specifications (PBT-03) — TO DO — pendente Jira
- account-management/TASK-04: Commands + Handlers de Account + queries de busca + Validators — TO DO — pendente Jira
- account-management/TASK-05: Behaviors de pipeline (TenantScope, PiiAccess, Transaction, Logging) + Ports — TO DO — pendente Jira

#### Definition of Done
- [ ] Domain.Tests ≥ 95%; Application.Tests ≥ 85%; PBTs 01-03 verdes
- [ ] CI verde; Code review; PR mergeado

---

### US-035 — Cadastrar parceiros com comissões default

- **Épico:** partner-management (EP-007)
- **RF rastreado:** RF prtn-create
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `PartnerManagement.sln` com 5 projetos compilando; Architecture.Tests verdes
- [ ] Objetos de valor: `PartnerName`, `PartnerRole`, `Percentage`, `CommissionDefaults`, `PartnerContact`, `Email`, `Phone` imutáveis
- [ ] Aggregate `Partner` com invariantes, métodos de comportamento, `PartnerStatus` (active/inactive) e PBTs 02, 03 verdes
- [ ] `CreatePartnerCommand` e `UpdatePartnerCommand` com handlers e validators

#### Tasks técnicas
- partner-management/TASK-01: Solution .NET + 5 projetos — TO DO — pendente Jira
- partner-management/TASK-02: Architecture.Tests e CI mínimo — TO DO — pendente Jira
- partner-management/TASK-03: VOs PartnerName, PartnerRole, Percentage, CommissionDefaults — TO DO — pendente Jira
- partner-management/TASK-04: VOs PartnerContact, Email, Phone — TO DO — pendente Jira
- partner-management/TASK-05: Aggregate Partner + invariantes + comportamentos — TO DO — pendente Jira
- partner-management/TASK-06: Domain Events + IPartnerRepository — TO DO — pendente Jira
- partner-management/TASK-07: State machine PartnerStatus + Specifications — TO DO — pendente Jira
- partner-management/TASK-08: PBT-02 e PBT-03 — Domain Tests — TO DO — pendente Jira
- partner-management/TASK-09: Commands CreatePartner e UpdatePartner com handlers e validators — TO DO — pendente Jira

#### Definition of Done
- [ ] Domain.Tests ≥ 95%; PBTs 02, 03 verdes; Application.Tests ≥ 85% (parcial)
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
| organization infrastructure (EF Core + RLS) pode atrasar se migrations forem complexas | Sprint 8 inclui apenas as tasks de infrastructure básica de organization (TASK-15..17); Outbox e Pub/Sub vão para Sprint 9 |

---

## 4. Encerramento

- [ ] Todas as stories em DONE; todos os PRs mergeados
- [ ] Sprint encerrada no Jira; `progress-tracking.md` atualizado
