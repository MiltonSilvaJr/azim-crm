# opportunity-pipeline

**Módulo BC-01 — Core Domain (Tier 1)**

Ciclo de vida completo de oportunidades comerciais no Azim CRM: criação, movimentação de estágio, comissão nativa de parceiro com snapshot imutável, forecast ponderado e detecção de estagnação.

## Status da implementação

| Onda | Foco | TASKs | Status |
|------|------|-------|--------|
| Onda 1 | Bootstrap — solution, projetos, regra de dependência | TASK-01 | **Concluída** |
| Onda 2 | Domain — VOs, entidades, aggregate, state machine, calculators, PBTs | TASK-02..07 | Pendente |
| Onda 3 | Application — CQRS handlers, behaviors, queries, StagnationDetectionService | TASK-08..12 | Pendente |
| Onda 4 | Infrastructure — EF, migrations, RLS, numeração atômica, outbox, portas | TASK-13..18 | Pendente |
| Onda 5 | API + Contracts — controllers, contratos, RBAC, OpenAPI, endpoints internos | TASK-19..21 | Pendente |
| Onda 6 | Hardening — isolamento CI, carga Kanban, observabilidade, DoD | TASK-22..24 | Pendente |

## Estrutura

```text
services/opportunity-pipeline/
├── OpportunityPipeline.slnx
├── Directory.Build.props        # net10.0, Nullable, TreatWarningsAsErrors, coverlet
├── global.json                  # SDK 10.0.107
├── .editorconfig
├── README.md
├── CHANGELOG.md
├── src/
│   ├── OpportunityPipeline.Domain/          # Aggregate root, VOs, eventos, serviços puros
│   ├── OpportunityPipeline.Application/     # CQRS, behaviors, portas, validators
│   ├── OpportunityPipeline.Infrastructure/  # EF Core, RLS, Outbox, adapters
│   ├── OpportunityPipeline.Api/             # Controllers REST, RBAC, OpenAPI
│   └── OpportunityPipeline.Contracts/       # DTOs públicos, envelopes .v1, OP-ERR-*
└── tests/
    ├── OpportunityPipeline.Domain.Tests/         # INV-1..13, PBT-02..08 (FsCheck)
    ├── OpportunityPipeline.Application.Tests/    # Handlers, behaviors, PBT-09
    ├── OpportunityPipeline.Infrastructure.Tests/ # EF, RLS, numeração atômica, PBT-01/07
    ├── OpportunityPipeline.Api.Tests/            # RBAC 200/403, contratos HTTP
    └── OpportunityPipeline.Architecture.Tests/   # Regra de dependência (NetArchTest)
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
