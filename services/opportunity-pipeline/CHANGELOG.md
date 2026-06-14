# CHANGELOG — opportunity-pipeline

Formato: [Keep a Changelog](https://keepachangelog.com/pt-BR/1.0.0/)
Versionamento semântico: [SemVer](https://semver.org/lang/pt-BR/)

## [Não lançado]

## [0.6.0] — 2026-06-14

### Adicionado (Onda 6 — Hardening, TASK-22..24)

- TASK-22 (Onda 6): testes de isolamento cross-tenant com Testcontainers PostgreSQL (CT-01..CT-06 + PBT-10 FsCheck ≥ 100 amostras). Gate CI KPI-06 adicionado ao workflow `staging.yml` (job `opportunity-pipeline-security-isolation-gate`, `Category=SecurityGate`, sev-1 bloqueante). Índice `idx_opportunities_tenant_bu_stage` validado por CT-04.
- TASK-23 (Onda 6): baseline de carga Kanban (LOAD-01..LOAD-05) com 500-2.000 oportunidades por tenant usando SQL direto via Npgsql. Índice `idx_opportunities_tenant_bu_stage` adicionado (filtrado por `stage_category = 'open'`). Resultados gravados em `/tmp/kanban-load-results.txt`. Teste de independência de paginação (LOAD-04) e múltiplos tenants (LOAD-05). Critério formal de p95 ≤ 2.000 ms a ser avaliado com infra de carga dedicada.
- TASK-24 (Onda 6): observabilidade completa — `OpportunityMetrics` (6 métricas snake_case: `opportunities_created_total`, `opportunities_won_total`, `opportunities_lost_total`, `opportunities_stale_total`, `commission_snapshot_created_total`, `kanban_request_duration_ms`, `outbox_pending_events` gauge thread-safe). `OpportunityActivitySource` (spans: WinOpportunity, CreateOpportunity, MoveStage, LoseOpportunity, ReopenOpportunity, GetKanban — sem PII). `PiiLogScanner` (gate CI de PII em logs: regex email, CPF, telefone BR, contact_name). Health check endpoint `/health` (HC-01..HC-04). Diretório `observability/alerts.yaml` com 6 alertas Prometheus (Kanban p95, Outbox warning/critical, StaleScan, RLS violation, API errors). Testes: MET-01..MET-08, TRACE-01..TRACE-02, PII-01..PII-10, HC-01..HC-04.

### DoD Fase 1 — opportunity-pipeline

Todos os critérios de Definition of Done do módulo BC-01 foram atendidos:
- Testes unitários, integração, arquitetura, contrato, PBT e observabilidade implementados.
- 11 Property-Based Tests (PBT-01..PBT-11) cobrindo domínio financeiro, isolamento e conversão.
- KPI-06 (gate de isolamento multi-tenant) verde.
- Cobertura Domain + Application ≥ 80%.
- 10 regras NetArchTest 100% verdes.
- 0 campos PII em logs, métricas e eventos.
- Alertas YAML para Prometheus definidos.
- README, CHANGELOG e tasks.md atualizados.

## [0.1.0] — 2026-06-14

### Adicionado

- TASK-01 (Onda 1): solution `OpportunityPipeline.slnx` com 5 projetos de produção e 5 de teste (Clean Architecture + DDD).
- Projetos de produção: `OpportunityPipeline.Domain`, `OpportunityPipeline.Application`, `OpportunityPipeline.Infrastructure`, `OpportunityPipeline.Api`, `OpportunityPipeline.Contracts`.
- Projetos de teste: `OpportunityPipeline.Domain.Tests`, `OpportunityPipeline.Application.Tests`, `OpportunityPipeline.Infrastructure.Tests`, `OpportunityPipeline.Api.Tests`, `OpportunityPipeline.Architecture.Tests`.
- Regra de dependência entre camadas validada por `Architecture.Tests` (10 testes, 100% verde — NetArchTest.Rules 1.3.2).
- `Directory.Build.props`: `net10.0`, `Nullable enable`, `ImplicitUsings enable`, `TreatWarningsAsErrors true`, `coverlet.collector 6.0.4`, `xunit 2.9.3`, `FluentAssertions 7.0.0`, `FsCheck.Xunit 3.2.0`.
- `global.json`: SDK 10.0.107 com rollForward `latestPatch`.
- `.editorconfig`: formatação C# moderna, `file_scoped` namespaces, nullable diagnostics como erro.
- `AssemblyReference.cs` em todos os projetos de produção (marcadores para resolução via reflexão).
- Pacotes: `MediatR 12.5.0`, `FluentValidation 11.11.0`, `EF Core 9.0.6`, `Npgsql 9.0.4`, `Polly 8.5.2`, `NSubstitute 5.3.0`, `Testcontainers.PostgreSql 4.4.0`, `NetArchTest.Rules 1.3.2`.
