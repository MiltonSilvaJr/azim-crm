# activity-management

Módulo **BC-04 — Activity Management (Gestão de Atividades Comerciais)** do Azim CRM.

**Status: Em desenvolvimento** — Onda 1 (Bootstrap) concluída.

Subdomínio de Suporte. Deployable: `azim-api`.

## Objetivo

Gerenciar o ciclo de vida das **atividades comerciais** (`activities`): criação, atualização,
conclusão, reagendamento e cancelamento, sob uma máquina de estados explícita. Entrega valor a
três consumidores:

1. **Vendedor / Gestor de BU** (`azim-web`): CRUD, visão "Meu dia / Minha semana" e conclusão
   em um clique.
2. **digest (BC-06)**: consulta de atividades vencidas e do dia por usuário; conclusão/reagendamento
   via link autenticado de um clique processando o `digest_action_token`.
3. **opportunity-pipeline (BC-01)**: consulta da última atividade concluída por oportunidade,
   insumo da detecção de estagnação.

## Não responsabilidades

- Não gerencia oportunidades (leitura somente, via porta `IOpportunityReadPort`).
- Não gerencia contas (leitura somente, via porta `IAccountReadPort`).
- Não emite `digest_action_tokens` — somente valida e consome (emissão pertence ao digest, BC-06).
- Não consome eventos de outros módulos (somente publica via Outbox + Pub/Sub).
- Não detecta estagnação de oportunidades (regra pertence ao opportunity-pipeline, BC-01).

## Ondas de Implementação

| Onda | Foco | TASKs | Status |
|------|------|-------|--------|
| Onda 1 | Bootstrap — solution, projetos, testes de arquitetura | TASK-01 | Concluída |
| Onda 2 | Domain — objetos de valor, state machine, agregado Activity, specifications | TASK-02..05 | Pendente |
| Onda 3 | Application — commands, queries, handlers, behaviors, ports | TASK-06..12 | Pendente |
| Onda 4 | Infrastructure — DbContext, migrations, repositório, RLS, Outbox, adapters | TASK-13..17 | Pendente |
| Onda 5 | API + Contratos — controllers, DTOs, erros, OpenAPI, Pact | TASK-18..21 | Pendente |
| Onda 6 | Hardening — observabilidade, segurança, DoD final | TASK-22..24 | Pendente |

## Estrutura de Projetos

```text
services/activity-management/
├── ActivityManagement.slnx
├── Directory.Build.props          # net10.0, Nullable, TreatWarningsAsErrors, coverlet
├── global.json                    # SDK 10.0.107
├── .editorconfig
├── README.md
├── CHANGELOG.md
├── Dockerfile                     # (Onda 5)
├── src/
│   ├── ActivityManagement.Contracts/      # DTOs request/response e eventos v1
│   ├── ActivityManagement.Domain/         # Agregado Activity, objetos de valor, specs
│   ├── ActivityManagement.Application/    # Handlers, behaviors, ports, validators
│   ├── ActivityManagement.Infrastructure/ # EF Core, RLS, Outbox, adapters externos
│   └── ActivityManagement.Api/            # Controllers REST, middleware, DI, OpenAPI
└── tests/
    ├── ActivityManagement.Domain.Tests/         # PBT-01, PBT-04, PBT-05; cobertura >= 95%
    ├── ActivityManagement.Application.Tests/    # PBT-02, PBT-03, PBT-05; cobertura >= 85%
    ├── ActivityManagement.Infrastructure.Tests/ # Testcontainers + Postgres; cobertura >= 70%
    ├── ActivityManagement.Api.Tests/            # WebApplicationFactory; cobertura >= 80%
    └── ActivityManagement.Architecture.Tests/  # NetArchTest — regras de dependência
```

## Regra de Dependência (Clean Architecture)

```text
Api            -> Application, Infrastructure, Contracts
Application    -> Domain, Contracts    (NUNCA Infrastructure)
Infrastructure -> Application, Domain, Contracts
Domain         -> ∅
Contracts      -> ∅
```

Enforçada por `Architecture.Tests` em cada PR (TASK-01).

## APIs Expostas

Base: `/api/v1`. Autenticação: Bearer JWT (Req 13.4) — exceto ação do digest (token opaco).

| Método | Path | Papel mínimo |
|--------|------|--------------|
| GET | `/api/v1/activities` | Viewer |
| POST | `/api/v1/activities` | Vendedor |
| GET | `/api/v1/activities/{id}` | Viewer |
| PUT | `/api/v1/activities/{id}` | Vendedor |
| DELETE | `/api/v1/activities/{id}` | Vendedor |
| PATCH | `/api/v1/activities/{id}/complete` | Vendedor |
| PATCH | `/api/v1/activities/{id}/reschedule` | Vendedor |
| GET | `/api/v1/activities/me/day` | Vendedor |
| GET | `/api/v1/activities/me/week` | Vendedor |
| GET | `/api/v1/activities/overdue` | Viewer / digest |
| GET | `/api/v1/activities/today` | Viewer / digest |
| GET | `/api/v1/opportunities/{id}/last-activity` | Viewer / pipeline |
| POST | `/api/v1/digest-actions/{token}` | Token do digest |
| POST | `/internal/overdue-scan` | Scheduler (mTLS/OIDC) |

## Eventos Publicados

Tópico Pub/Sub: `azim-activities`. Sem PII (título/descrição) nos payloads.

| Evento | Quando |
|--------|--------|
| `activity.created.v1` | Atividade criada |
| `activity.completed.v1` | Conclusão efetiva (primeira) |
| `activity.overdue.v1` | Scan detecta atividade vencida não terminal |

## Eventos Consumidos

Nenhum nesta versão.

## Dependências

| Serviço | Direção | Mecanismo |
|---------|---------|-----------|
| opportunity-pipeline (BC-01) | Saída (leitura) | `IOpportunityReadPort` — HTTP/gRPC interno (mTLS) |
| account-management (BC-02) | Saída (leitura) | `IAccountReadPort` — HTTP/gRPC interno (mTLS) |
| digest (BC-06) | Leitura/consumo | `IDigestActionTokenPort` — mesmo banco `azim-api` |
| audit-log | Saída (eventos) | Pub/Sub via Outbox |

## Variáveis de Ambiente

> Documentação completa será adicionada na Onda 5 (TASK-18).

| Variável | Descrição |
|----------|-----------|
| `ConnectionStrings__ActivityManagement` | Connection string PostgreSQL |
| `DigestActionToken__ExpirationHours` | TTL do token de ação (padrão: 24) |

## Como Executar Localmente

> Instruções completas serão adicionadas na Onda 5. Pré-requisito: Docker Desktop.

```sh
# A partir da raiz do repositório
docker compose up -d postgres
cd services/activity-management
dotnet run --project src/ActivityManagement.Api
```

## Como Testar

```sh
cd services/activity-management

# Testes de arquitetura (gate CI)
dotnet test tests/ActivityManagement.Architecture.Tests

# Todos os testes (sem Testcontainers)
dotnet test --filter "Category!=Integration"

# Testes de integração (requer Docker)
dotnet test tests/ActivityManagement.Infrastructure.Tests
```

## Observabilidade

- Logs estruturados JSON com `correlation_id`, `tenant_id`, `activity_id`.
- Métricas Prometheus: `activities_created_total`, `activities_completed_total`,
  `activities_overdue_total`.
- Traces OpenTelemetry: spans por handler de Command/Query.
- Health checks: `/health/live` e `/health/ready`.

## Referências

- `docs/product/modules/activity-management/design.md` — design técnico completo
- `docs/product/modules/activity-management/requirements.md` — requisitos
- `docs/product/modules/activity-management/tasks.md` — plano de tasks
- `docs/product/adr/ADR-0001.md` — isolamento multi-tenant
