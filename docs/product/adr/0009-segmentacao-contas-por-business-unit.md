---
title: Segmentação de contas por Business Unit (visibilidade por BU sobre o isolamento de tenant)
status: Aceito
date: 2026-06-15
deciders:
  - "@MiltonSilvaJr"
consulted:
  - Decisão HITL VAL-ACC-03 (approvals.yaml — account-management)
  - ADR-0001 (isolamento multi-tenant em defesa em profundidade)
informed:
  - Times dos bounded contexts account-management, organization, opportunity-pipeline, reporting
---

# ADR-0009 — Segmentação de contas por Business Unit

- **Status:** Aceito
- **Data:** 2026-06-15
- **Autores:** @MiltonSilvaJr
- **Supersede:** —
- **Substituído por:** —
- **Relacionado:** estende o **ADR-0001** (isolamento multi-tenant); resolve a pendência **VAL-ACC-03**.

## Contexto e Problema

O design original do `account-management` (Req 2.3, DD-001) implementou **contas compartilhadas por
todo o tenant**: qualquer usuário do tenant enxergava qualquer conta, sem `bu_id`. A decisão de
produto **VAL-ACC-03 (2026-06-15)** alterou essa regra: **contas devem ser segmentadas por Business
Unit (BU)**, de modo que vendedores de uma BU não vejam, por padrão, as contas de outra BU do mesmo
tenant — evitando conflito comercial entre equipes e atendendo a exigências de segmentação de
clientes enterprise.

O `BusinessUnit` e o vínculo usuário↔BU (`UserMembership`, com `Role`) são **owned pelo módulo
organization**. O `account-management` é consumidor desse escopo. O desafio é estender a fronteira de
isolamento já estabelecida pelo ADR-0001 (tenant) com uma **segunda dimensão de escopo (BU)** sem
enfraquecer a defesa em profundidade (RLS + EF Global Query Filter).

## Decisão

Adotamos **segmentação de contas por BU sobre o isolamento de tenant**, com as seguintes regras
(decididas em VAL-ACC-03):

1. **Cardinalidade (a):** cada conta pertence a **exatamente uma BU dona** — coluna `bu_id` **NOT
   NULL** em `accounts` (e herdada logicamente por `contacts`, que já herdam `tenant_id` e
   `account_id` do agregado). Projeto greenfield: não há backfill.

2. **Visibilidade por papel (b):** o **escopo efetivo de BU** do usuário é resolvido a partir de
   seu(s) `UserMembership`(s):
   - Papéis de **gestão do tenant** (ex.: *Tenant Admin*, *Gestor*) têm **visão tenant-wide**:
     enxergam contas de **todas as BUs do tenant** (bypass do filtro de BU, **sempre dentro do
     tenant** — o isolamento do ADR-0001 nunca é relaxado).
   - **Membros** (ex.: vendedor) enxergam apenas as contas das BUs em que possuem membership.
   - **Acesso cross-BU para membros é configurável pelo Tenant Admin**: um membro pode receber
     acesso a BUs adicionais. Isso é modelado como **memberships/grants adicionais** (o usuário passa
     a ter escopo sobre mais de uma BU); o resolver de escopo honra **o conjunto** de BUs, não uma
     única. A gestão desses grants (UI/endpoints) é responsabilidade do **organization** (fora do
     escopo deste módulo); o `account-management` apenas consome o escopo resultante.

3. **Atribuição na criação (c):** ao criar uma conta:
   - se o usuário pertence a **uma** BU, a conta nasce nessa BU automaticamente;
   - se pertence a **várias**, o request **deve informar `bu_id`**, validado contra o conjunto de
     memberships do usuário (rejeita BU fora do escopo — `ACC-ERR` apropriado).

### Mecanismo de propagação e enforcement

Replicamos o padrão do ADR-0001 (que propaga o tenant via `TenantContext` → `InfrastructureTenantContext`
→ sessão PostgreSQL/Query Filter), adicionando uma dimensão de BU:

- **Contexto de aplicação `BuScopeContext`** (análogo a `TenantContext`): carrega o **conjunto de
  `bu_id`** do usuário e um flag **`IsTenantWide`** (true para papéis de gestão). Populado pelo
  middleware de autenticação a partir dos claims do principal (memberships), **da mesma forma** que o
  tenant — o módulo confia no escopo assinado, sem dependência de runtime do organization no caminho
  de leitura.
- **Defesa em profundidade (duas camadas), mantendo o ADR-0001:**
  - **RLS (PostgreSQL):** a policy de `accounts`/`contacts` passa a exigir
    `tenant_id = current_tenant` **E** (`app.bu_tenant_wide = true` **OU** `bu_id = ANY(app.current_bu_scope)`).
    Variáveis de sessão `app.current_bu_scope` (array de uuid) e `app.bu_tenant_wide` (bool) são
    setadas por requisição, fail-closed (escopo vazio + não-tenant-wide ⇒ nenhuma linha).
  - **EF Global Query Filter:** espelha a mesma condição na aplicação.
- O `bu_id` de uma conta é **imutável** após a criação no MVP (não há reatribuição de BU — evita
  brecha de vazamento por troca de escopo; reatribuição, se necessária, será ADR futuro).

## Consequências

**Positivas**
- Segmentação comercial entre equipes com a **mesma garantia estrutural** do isolamento de tenant
  (RLS fail-closed + Query Filter), sem confiar só na camada de aplicação.
- Governança preservada: gestão tem visão tenant-wide; flexibilidade de cross-BU via grants do admin.
- Consistente com o `BuScopeResolver` já previsto como pendência (RISK-AUDIT-05).

**Negativas / Riscos**
- **Superfície de RLS mais complexa** (comparação de array + flag). Exige teste de isolamento por BU
  com PostgreSQL real (Testcontainers, role NOSUPERUSER) cobrindo: membro vê só sua BU; admin vê
  todas; membro com grant vê o conjunto; escopo vazio não vê nada (fail-closed).
- **Dependência de claims confiáveis:** o `BuScopeContext` confia no principal autenticado. O IdP/auth
  middleware deve popular memberships/role corretamente; um claim adulterado quebraria a segmentação
  (mitigado pela RLS, que exige a variável de sessão coerente setada server-side por requisição).
- A **gestão de grants cross-BU** (organization) é dependência adjacente — enquanto não existir UI,
  cross-BU é configurável apenas via dados de membership.

## Alternativas consideradas

- **Conta M:N com BUs (tabela de junção):** rejeitada para o MVP — RLS/queries mais complexas sem
  caso de uso comprovado. Pode ser revisitada se surgir conta corporativa multi-equipe.
- **Segmentação só na aplicação (sem RLS por BU):** rejeitada — violaria o princípio de defesa em
  profundidade do ADR-0001; um `IgnoreQueryFilters()` ou query crua vazaria entre BUs.
- **Todos os papéis restritos à BU (sem visão tenant-wide):** rejeitada em VAL-ACC-03(b) — inviabiliza
  governança/relatórios sem exigir membership do admin em todas as BUs.

## Rastreabilidade

- Resolve: **VAL-ACC-03** (`services/account-management/approvals.yaml`).
- Estende: **ADR-0001**.
- Impacta: `account-management` (Domain/Application/Infrastructure/Api/Contracts), Req 2 e design §4.1
  do módulo; consumidor do escopo de BU do **organization** (`UserMembership`).
