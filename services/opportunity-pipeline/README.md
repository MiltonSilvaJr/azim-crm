# opportunity-pipeline

**Módulo BC-01 — Core Domain (Tier 1)**

Ciclo de vida completo de oportunidades comerciais no Azim CRM: criação, movimentação de estágio, comissão nativa de parceiro com snapshot imutável, forecast ponderado e detecção de estagnação.

## Status da implementação

**Status geral: Implementado** — 24 TASKs concluídas, 6 ondas entregues, DoD Fase 1 aprovado.

| Onda | Foco | TASKs | Status |
|------|------|-------|--------|
| Onda 1 | Bootstrap — solution, projetos, regra de dependência | TASK-01 | **Concluída** |
| Onda 2 | Domain — VOs, entidades, aggregate, state machine, calculators, PBTs | TASK-02..07 | **Concluída** |
| Onda 3 | Application — CQRS handlers, behaviors, queries, StagnationDetectionService | TASK-08..12 | **Concluída** |
| Onda 4 | Infrastructure — EF, migrations, RLS, numeração atômica, outbox, portas | TASK-13..18 | **Concluída** |
| Onda 5 | API + Contracts — controllers, contratos, RBAC, OpenAPI, endpoints internos | TASK-19..21 | **Concluída** |
| Onda 6 | Hardening — isolamento CI, carga Kanban, observabilidade, DoD | TASK-22..24 | **Concluída** |

### Métricas de qualidade (DoD Fase 1)

| KPI | Critério | Resultado |
|-----|----------|-----------|
| Testes totais | > 400 | > 420 (estimado) |
| Property-Based Tests | ≥ 11 PBTs | 11 PBTs (PBT-01..11) |
| Regras de arquitetura | 10 regras NetArchTest | 10/10 verde |
| Isolamento multi-tenant | 0 vazamentos cross-tenant | KPI-06 verde (CT-01..06 + PBT-10) |
| Cobertura de testes unitários | ≥ 80% Domain + Application | Atingido |
| Latência Kanban | p95 ≤ 2.000 ms | Baseline registrado (LOAD-01..05) |
| PII em logs/traces/eventos | 0 campos PII | Gate verde (PII-01..10) |
| Métricas OpenTelemetry | 6 métricas snake_case | MET-01..08 verde |
| Health check | /health 200 OK | HC-01..04 verde |

## Estrutura

```text
services/opportunity-pipeline/
├── OpportunityPipeline.slnx
├── Directory.Build.props        # net10.0, Nullable, TreatWarningsAsErrors, coverlet
├── global.json                  # SDK 10.0.107
├── .editorconfig
├── README.md
├── CHANGELOG.md
├── observability/               # Alertas YAML para Prometheus/Alertmanager (TASK-24)
│   └── alerts.yaml              # 6 alertas: Kanban p95, Outbox, StaleScan, RLS, API errors
├── src/
│   ├── OpportunityPipeline.Domain/          # Aggregate root, VOs, eventos, serviços puros
│   ├── OpportunityPipeline.Application/     # CQRS, behaviors, portas, validators
│   ├── OpportunityPipeline.Infrastructure/  # EF Core, RLS, Outbox, adapters, Observability
│   ├── OpportunityPipeline.Api/             # Controllers REST, RBAC, OpenAPI
│   └── OpportunityPipeline.Contracts/       # DTOs públicos, envelopes .v1, OP-ERR-*
└── tests/
    ├── OpportunityPipeline.Domain.Tests/         # INV-1..13, PBT-02..08 (FsCheck)
    ├── OpportunityPipeline.Application.Tests/    # Handlers, behaviors, PBT-09
    ├── OpportunityPipeline.Infrastructure.Tests/ # EF, RLS, Outbox, PBT-10, LOAD-01..05, PII-01..10
    ├── OpportunityPipeline.Api.Tests/            # RBAC 200/403, contratos HTTP, HC-01..04
    └── OpportunityPipeline.Architecture.Tests/   # 10 regras de dependência (NetArchTest)
```

## Regra de dependência (Clean Architecture)

```text
Api            -> Application, Infrastructure, Contracts
Application    -> Domain, Contracts  (NUNCA Infrastructure)
Infrastructure -> Application, Domain, Contracts
Domain         -> ∅
Contracts      -> ∅
```

Validada automaticamente por `OpportunityPipeline.Architecture.Tests` (10 regras, 100% verde).

## Como executar localmente

### Pré-requisitos

- .NET SDK 10.0.107 (`global.json`)
- Docker Desktop (para testes de integração com PostgreSQL — Ondas 4+)

### Build

```sh
cd services/opportunity-pipeline
dotnet build OpportunityPipeline.slnx
```

### Testes

```sh
dotnet test OpportunityPipeline.slnx
```

### Cobertura (após Onda 2+)

```sh
dotnet test OpportunityPipeline.slnx \
  --collect:"XPlat Code Coverage" \
  --results-directory ./coverage
```

## Decisões arquiteturais

| ADR | Título | Status |
|-----|--------|--------|
| ADR-0001 | Isolamento multi-tenant em defesa em profundidade (RLS) | Aceito |
| ADR-0002 | Snapshot imutável de comissão | A formalizar |
| ADR-0003 | Unicidade de `opportunity_number` por tenant | A formalizar |
| ADR-0004 | Outbox pattern + idempotência de consumers | A formalizar |

Decisões inline do módulo: DD-001..007 em `docs/product/modules/opportunity-pipeline/design.md §17`.

## Referências

- Requisitos: `docs/product/modules/opportunity-pipeline/requirements.md`
- Design técnico: `docs/product/modules/opportunity-pipeline/design.md`
- Tasks: `docs/product/modules/opportunity-pipeline/tasks.md`
- ADR-0001: `docs/product/adr/0001-isolamento-multi-tenant-defesa-em-profundidade.md`
