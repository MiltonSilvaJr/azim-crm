# Changelog — partner-management

Todas as alterações notáveis deste módulo são documentadas aqui.

Formato: [Conventional Commits](https://www.conventionalcommits.org/pt-br/v1.0.0/) com escopo `(partner-management)`.

---

## [0.1.0] — Onda 1 Bootstrap

### Adicionado

- Solution `PartnerManagement.slnx` com 5 projetos de produção e 5 de teste (TASK-01).
- `Directory.Build.props` com net10.0, Nullable, ImplicitUsings, TreatWarningsAsErrors.
- `global.json` fixando SDK 10.0.107.
- `.editorconfig` com convenções do módulo.
- Projetos de produção: Domain, Application, Infrastructure, Api, Contracts.
- Projetos de teste: Domain.Tests, Application.Tests, Infrastructure.Tests, Api.Tests, Architecture.Tests.
- Referências entre projetos conforme regra de dependência de design §3.
- `AssemblyReference.cs` em cada projeto de produção para NetArchTest.
- Testes de dependência em `Architecture.Tests` (TASK-02).
- Pipeline CI (`.github/workflows/partner-management.yml`) executando build + test (TASK-02).

---

## [0.2.0] — Ondas 2 e 3: Domain e Application

### Adicionado

- Objetos de valor: `PartnerName`, `PartnerRole`, `Percentage`, `CommissionDefaults`, `PartnerContact`, `Email`, `Phone` (TASK-03, TASK-04).
- Agregado `Partner` com invariantes, métodos de comportamento e domain events: `PartnerCreatedEvent`, `PartnerCommissionPercentagesUpdatedEvent`, `PartnerDeactivatedEvent`, `PartnerReactivatedEvent` (TASK-05, TASK-06).
- State machine `PartnerStatus` e Specifications de elegibilidade (TASK-07).
- PBT-02 (idempotência de inativação/reativação) e PBT-03 (round-trip de percentuais) via FsCheck (TASK-08).
- Commands e handlers: `CreatePartnerCommand`, `UpdatePartnerCommand`, `DeactivatePartnerCommand`, `ReactivatePartnerCommand` com `FluentValidation` (TASK-09, TASK-10).
- Queries e handlers: `ListPartnersQuery`, `GetPartnerByIdQuery`, `GetPartnerEligibilityQuery` (TASK-11).
- Port `IPartnerCommissionReadPort` e handlers: `GetPartnerCommissionViewHandler`, `GetPartnerCommissionReportHandler` com degradação parcial (TASK-12).
- Pipeline behaviors MediatR: `CorrelationLoggingBehavior`, `TenantScopeBehavior`, `ValidationBehavior`, `AuthorizationBehavior`, `TransactionBehavior` (TASK-13).
- PBT-01 (conservação da soma de comissão) e PBT-05 (imutabilidade da comissão consolidada) via FsCheck (TASK-14).

---

## [0.3.0] — Onda 4: Infrastructure

### Adicionado

- `PartnerManagementDbContext` com EF Core 9, mapeamento de entidades e filtro global de tenant_id (TASK-15).
- Migrations versionadas: tabelas `partners`, `outbox_messages`, `idempotency_keys`; índices `idx_partners_tenant_active`, `idx_partners_tenant_name`; RLS habilitada (TASK-16).
- `PartnerRepository` com `FindByNameAsync`, `GetByIdAsync`, `AddAsync`, `UpdateAsync`; `TenantContext` e `RlsSessionInterceptor` (TASK-17).
- Outbox transacional (`OutboxMessage`, `OutboxPublisher` background service) e `AuditPublisher` (TASK-18).
- `PartnerCommissionReadAdapter`: HttpClient para o opportunity-pipeline com propagação de `correlation_id` e `tenant_id` (TASK-19).
- `CanonicalRoleProvider` com cache em memória; `PartnerPiiMasker` para mascaramento de name/email/phone; `IdempotencyKeyRepository` (TASK-20).
- PBT-04 (isolamento por tenant) via FsCheck com Testcontainers + PostgreSQL real; gate CI obrigatório (KPI-06) (TASK-21).

---

## [0.4.0] — Onda 5: API REST

### Adicionado

- DTOs de contratos: request, response e eventos (TASK-22).
- `PartnersController` com endpoints `GET /api/v1/partners`, `POST /api/v1/partners`, `GET /api/v1/partners/{id}`, `PATCH /api/v1/partners/{id}` (TASK-23).
- Endpoints `POST /{id}/deactivate`, `POST /{id}/reactivate`, `GET /{id}/eligibility`, `GET /{id}/commissions` (TASK-24).
- Middlewares: `CorrelationIdMiddleware`, `TenantResolutionMiddleware`, `ExceptionHandlingMiddleware` (TASK-23).
- Testes de API: RBAC, catálogo de erros, WebApplicationFactory com TestAuthHandler (TASK-25).

---

## [0.5.0] — Onda 6: Hardening

### Adicionado

- **TASK-26 Observabilidade:**
  - `IPartnerMetrics` (port em Application) + `PartnerMetrics` (singleton, Prometheus): contadores `partners_created_total`, `partners_deactivated_total`, `partners_reactivated_total`; histograma `partner_commission_view_duration_seconds`.
  - `PartnerReadPortHealthCheck`: retorna `Degraded` (não `Unhealthy`) em indisponibilidade do pipeline.
  - Health checks: `partner_sql` (Npgsql) e `partner_commission_read_port` com tags `readiness`.
  - Endpoints: `/healthz/live` (liveness), `/healthz/ready` (readiness com Degraded→200, Unhealthy→503), `/metrics` (prometheus-net).
  - OpenTelemetry `ActivitySource` em `GetPartnerCommissionViewHandler` com atributos `partner_id`, `tenant_id`, `correlation_id`.
  - 7 novos testes de observabilidade em `Infrastructure.Tests`.
  - Alertas YAML em `observability/alerts.yaml` (5 regras: ALERT-PM-01..05).

- **TASK-27 PII Masking:**
  - Gate anti-PII CI: job `partner-management-anti-pii-gate` em `staging.yml` com dois steps bloqueantes (`Category=PiiScan`).
  - `AntiPiiScanTests` (Infrastructure.Tests): 5 testes verificam `PartnerPiiMasker` em payloads JSON e texto livre.
  - `AntiPiiHandlerScanTests` (Application.Tests): 1 teste executa `CreatePartnerHandler` com PII real e verifica ausência em logs capturados.
  - `approvals.yaml` criado com VAL-PARTNER-01 (classificação de partner.name como PII — pendente).

- **TASK-28 Resiliência:**
  - `CommissionReadResilienceOptions`: configuração de timeout, retry e circuit breaker via appsettings.
  - `CommissionReadResiliencePipeline`: Polly v8 com timeout por tentativa → retry exponencial com jitter → circuit breaker.
  - `PartnerCommissionReadAdapter`: refatorado para executar via `ResiliencePipeline<HttpResponseMessage>`; trata `BrokenCircuitException`, `TimeoutRejectedException`, `HttpRequestException` e `TaskCanceledException` com degradação parcial (lista vazia).
  - `InternalsVisibleTo` adicionado em `Infrastructure` para `Infrastructure.Tests`.
  - 6 novos testes de resiliência com DelegatingHandler stubs (sem rede real).

- **TASK-29 DoD e Fechamento:**
  - README do módulo sincronizado: status `Implementado`, 6 ondas, 29 TASKs, PBTs, tabela de APIs, observabilidade, resiliência, pendências.
  - RISK-PM-05 resolvido: endpoint de atualização usa `PATCH` (não `PUT`).
  - Matriz de rastreabilidade completa: 29 TASKs `[X]`, todos os Req/RNF/PBT/DD/ADR verificados.
  - OpenAPI disponível via Swashbuckle em `/swagger` (Development).

### Total de testes

| Projeto | Testes |
|---------|--------|
| Domain.Tests | 128 |
| Architecture.Tests | 10 |
| Application.Tests | 62 |
| Api.Tests | 70 |
| Infrastructure.Tests | 78 |
| **Total** | **348** |
