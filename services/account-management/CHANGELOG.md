# Changelog — account-management

Todas as mudanças notáveis neste serviço são documentadas aqui.
Formato baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/).

---

## [Não lançado]

### Adicionado (ADR-0009 — VAL-ACC-03: segmentação por Business Unit)

- Campo `bu_id` (UUID, NOT NULL, imutável) no agregado `Account` e na tabela `accounts`.
- `BuScopeContext` (Application) — análogo ao `TenantContext`; propaga o escopo de BU via DI.
- `BuScopeBehavior` — MediatR pipeline behavior na posição 3; valida que o escopo de BU está inicializado antes de qualquer comando/query.
- `InfrastructureBuScopeContext` — adaptador de infra que expõe `BuScopeContext` para o `DbContext`.
- `TenantBuScopeConnectionInterceptor` — `DbConnectionInterceptor` que aplica `SET LOCAL` de `app.current_tenant`, `app.current_bu_scope` e `app.bu_tenant_wide` em cada conexão PostgreSQL, garantindo RLS fail-closed.
- EF Core Global Query Filter em `Account` ampliado: `tenant_id = CurrentTenantId AND (isTenantWide OR bu_id IN CurrentBuIds)`.
- Migration `20260615000003_AddBuIdToAccounts`:
  - Coluna `bu_id UUID NOT NULL` em `accounts`.
  - Índice `idx_accounts_tenant_bu (tenant_id, bu_id)`.
  - Função PL/pgSQL `current_bu_scope_array()` (STABLE SECURITY DEFINER).
  - RLS em `accounts` com policy `bu_scope_isolation` (tenant + BU).
  - RLS em `contacts` com policy `tenant_isolation`.
- `BuIdRequiredForMultiBuUserException` (ACC-ERR-010, HTTP 422) — lançada quando usuário multi-BU omite `bu_id` na criação de conta.
- `BuNotInScopeException` (ACC-ERR-011, HTTP 403) — lançada quando `bu_id` solicitado não está no escopo autorizado do usuário.
- `bu_id` em `CreateAccountCommand`, `CreateAccountRequest` e `AccountResponse`.
- Resolução automática de BU no `CreateAccountHandler`: usuário single-BU não precisa informar `bu_id`; multi-BU/tenant-wide deve informar explicitamente.
- Extração de claims `bu_ids` e `tenant_wide` nos três controllers (Accounts, Account360, Contacts).
- Testes de isolamento por BU: `BuScopeIsolationTests` (BU-ISO-01..05 + PBT-BU, 50 combinações).
- Testes de handler: cenários de resolução automática, erro ACC-ERR-010, erro ACC-ERR-011.

### Alterado

- `Account.Create()` e `Account.Reconstitute()`: novo parâmetro `buId` obrigatório.
- `PostgresFixture.CreateDbContext()`: usa `isTenantWide: true` por padrão (compatibilidade backward).
- `PostgresFixture`: novo método `CreateDbContextWithBuScope()` para testes de isolamento por BU.
- `DbContextSchemaTests`: atualizado para verificar presença de `bu_id` em `accounts`; INSERTs diretos incluem `bu_id`.
- `requirements.md` e `design.md` do módulo: refletem ADR-0009 (bu_id como atributo da conta).

---

## [0.1.0] — 2026-06-13

### Adicionado

- Estrutura inicial do serviço: Domain, Application, Infrastructure, Api, Contracts.
- Agregado `Account` com entidade `Contact` e state machine `ContactPrivacyState`.
- Casos de uso: `CreateAccount`, `UpdateAccount`, `GetAccountById`, `SearchAccounts`, `GetAccount360`, `CreateContact`, `UpdateContact`, `ListContacts`, `ForgetContact`.
- Pipeline MediatR: `CorrelationLoggingBehavior`, `TenantScopeBehavior`, `ValidationBehavior`, `PiiAccessBehavior`, `TransactionBehavior`.
- Persistência: EF Core + PostgreSQL, migrations `20260613000001_InitialAccountsContacts` e `20260613000002_AuditOutboxIdempotency`.
- Observabilidade: OpenTelemetry traces, métricas Prometheus, health checks.
- Testes: Domain (95), Application (68), Infrastructure (94), Api (39), Architecture (10).
