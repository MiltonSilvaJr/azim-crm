# Changelog — activity-management

Formato: [Keep a Changelog](https://keepachangelog.com/pt-BR/1.0.0/)
Versionamento: [Semantic Versioning](https://semver.org/lang/pt-BR/)

## [0.1.0] — 2026-06-14 — Implementado (Ondas 1-6 concluídas)

### Adicionado (Onda 6 — Hardening: TASK-22..24)

- `IActivityMetrics` (Application port) e `ActivityMetrics` (Infrastructure) com 5 contadores
  Prometheus snake_case: `activities_created_total`, `activities_completed_total`,
  `activities_overdue_total`, `digest_action_tokens_used_total`, `digest_action_tokens_expired_total`.
- Integração OpenTelemetry com tracing (AspNetCore + HttpClient) e métricas, exportador Console.
- Polly retry (3 tentativas, backoff exponencial 200ms) + circuit breaker (5 falhas, 30s break)
  nos HttpClients de `OpportunityReadAdapter` e `AccountReadAdapter`.
- 5 regras de alerta Prometheus em `observability/alerts.yaml`.
- `ConstantTimeComparison` usando `CryptographicOperations.FixedTimeEquals` para comparação
  de hash de token em tempo constante (anti-timing, PBT-03, RNF 5).
- Testes de segurança: `ConstantTimeTokenTests` (9 testes), `AntiPiiScanTests` (13 testes),
  `AuditImmutabilityTests` (3 testes com Testcontainers PostgreSQL real).
- `approvals.yaml` com VAL-ACT-01 (fronteira BC digest/activity), VAL-ACT-02 (TTL do token)
  e VAL-TRD-05 (residência de dados southamerica-east1) — bloqueadores de go-live.
- PBT-01..05 todos atualizados para ≥500 casos em modo de regressão (TASK-24).
- `InternalsVisibleTo` em `ActivityManagement.Infrastructure.csproj` expondo internos para testes.

### Atualizado (TASK-24 — DoD final)

- `data-model.md §BC-04`: adicionadas colunas `status` e `priority` à tabela `activities`.
- `data-model.md §BC-06`: adicionada coluna `token_hash` à tabela `digest_action_tokens`.
- `README.md`: status atualizado para "Implementado"; adicionada tabela de PBTs, totais de
  testes, tabela de variáveis de ambiente, seções de segurança e aprovações pendentes.
- `tasks.md`: matriz de rastreabilidade completa — todos os 24 itens marcados `[X]`;
  PBTs marcados com `≥500 casos`.
- Handlers `CreateActivityCommand`, `CompleteActivityCommand`, `ScanOverdueActivitiesCommand`
  e `ProcessDigestActionCommand` com métricas e logs estruturados (sem PII).

## [Não publicado — histórico de ondas anteriores]

### Adicionado (Onda 1 — Bootstrap)

- Solution `ActivityManagement.slnx` com 5 projetos de produção e 5 de teste.
- Projetos de produção: `ActivityManagement.Contracts`, `ActivityManagement.Domain`,
  `ActivityManagement.Application`, `ActivityManagement.Infrastructure`, `ActivityManagement.Api`.
- Projetos de teste: `ActivityManagement.Domain.Tests`, `ActivityManagement.Application.Tests`,
  `ActivityManagement.Infrastructure.Tests`, `ActivityManagement.Api.Tests`,
  `ActivityManagement.Architecture.Tests`.
- `Architecture.Tests` com NetArchTest validando 9 regras de dependência entre camadas (design §3).
- `Directory.Build.props` com configuração base: net10.0, Nullable, ImplicitUsings,
  TreatWarningsAsErrors, coverlet, xUnit, FluentAssertions 7.x, FsCheck.Xunit 3.x.
- `global.json` fixando SDK 10.0.107.
- `.editorconfig` com convenções UTF-8/LF/4-spaces.
- `AssemblyReference.cs` em cada projeto de produção para descoberta de assembly por reflexão.
- Pacotes mínimos instalados: MediatR 12.5.0, FluentValidation 11.11.0, EF Core 9.0.6,
  Npgsql 9.0.4, Polly 8.5.2, OpenTelemetry 1.16.0, NSubstitute 5.3.0, Testcontainers.PostgreSql 4.4.0.
