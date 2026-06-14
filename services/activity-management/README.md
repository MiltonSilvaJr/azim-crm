# activity-management

Módulo **BC-04 — Activity Management (Gestão de Atividades Comerciais)** do Azim CRM.

**Status: Implementado** — 6 ondas concluídas (TASK-01..TASK-24), 367 testes verdes, 5 PBTs.

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
| Onda 2 | Domain — objetos de valor, state machine, agregado Activity, specifications | TASK-02..05 | Concluída |
| Onda 3 | Application — commands, queries, handlers, behaviors, ports | TASK-06..12 | Concluída |
| Onda 4 | Infrastructure — DbContext, migrations, repositório, RLS, Outbox, adapters | TASK-13..17 | Concluída |
| Onda 5 | API + Contratos — controllers, DTOs, erros, OpenAPI, Pact | TASK-18..21 | Concluída |
| Onda 6 | Hardening — observabilidade, segurança, DoD final | TASK-22..24 | Concluída |

## Property-Based Tests (PBT-01..05)

Todos os 5 PBTs executam em modo de regressão com ≥ 500 casos por propriedade.

| PBT | Invariante | Arquivo | MaxTest |
|-----|-----------|---------|---------|
| PBT-01 | State machine de ActivityStatus — sequências arbitrárias nunca violam transições | `Domain.Tests/.../ActivityStatusPbt01Tests.cs` | 500 |
| PBT-02 | Idempotência de conclusão — N chamadas → exatamente 1 ActivityCompleted | `Application.Tests/.../CompleteActivityCommandTests.cs` e `ProcessDigestActionCommandTests.cs` | 500 |
| PBT-03 | Anti-enumeração — token inválido indistinguível de atividade inacessível em forma | `Application.Tests/.../ProcessDigestActionCommandTests.cs` | 500 |
| PBT-04 | Invariante overdue — terminal nunca vencida; não-terminal com dueAt < ref sempre vencida | `Domain.Tests/.../OverdueSpecificationTests.cs` | 500 |
| PBT-05 | Saúde do funil — N≥1 follow-up futuro → HasFollowup true; só terminais/passados → false | `Domain.Tests/.../FunnelHealthSpecificationTests.cs` | 500 |

## Totais de Testes por Projeto

| Projeto | Testes | Cobertura alvo |
|---------|--------|----------------|
| `Architecture.Tests` | 10 | — (regras de dependência NetArchTest) |
| `Domain.Tests` | 141 | ≥ 95% |
| `Application.Tests` | 70 | ≥ 85% |
| `Infrastructure.Tests` | 93 | ≥ 70% |
| `Api.Tests` | 53 | ≥ 80% |
| **Total** | **367** | |

## Estrutura de Projetos

```text
services/activity-management/
├── ActivityManagement.slnx
├── Directory.Build.props          # net10.0, Nullable, TreatWarningsAsErrors, coverlet
├── global.json                    # SDK 10.0.107
├── approvals.yaml                 # VAL-ACT-01, VAL-ACT-02, VAL-TRD-05 — bloqueadores go-live
├── README.md
├── CHANGELOG.md
├── observability/
│   └── alerts.yaml                # 5 regras de alerta Prometheus (TASK-22)
├── src/
│   ├── ActivityManagement.Contracts/      # DTOs request/response e eventos v1
│   ├── ActivityManagement.Domain/         # Agregado Activity, objetos de valor, specs
│   ├── ActivityManagement.Application/    # Handlers, behaviors, ports, validators
│   ├── ActivityManagement.Infrastructure/ # EF Core, RLS, Outbox, adapters externos
│   └── ActivityManagement.Api/            # Controllers REST, middleware, DI, OpenAPI
└── tests/
    ├── ActivityManagement.Domain.Tests/         # PBT-01, PBT-04, PBT-05; cobertura >= 95%
    ├── ActivityManagement.Application.Tests/    # PBT-02, PBT-03; cobertura >= 85%
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
| opportunity-pipeline (BC-01) | Saída (leitura) | `IOpportunityReadPort` — HTTP/gRPC interno (mTLS) + Polly retry/circuit-breaker |
| account-management (BC-02) | Saída (leitura) | `IAccountReadPort` — HTTP/gRPC interno (mTLS) + Polly retry/circuit-breaker |
| digest (BC-06) | Leitura/consumo | `IDigestActionTokenPort` — mesmo banco `azim-api` |
| audit-log | Saída (eventos) | Pub/Sub via Outbox |

## Variáveis de Ambiente

| Variável | Descrição |
|----------|-----------|
| `ConnectionStrings__ActivityManagement` | Connection string PostgreSQL (southamerica-east1 em prod) |
| `DigestActionToken__ExpirationHours` | TTL do token de ação (padrão: 24 — VAL-ACT-02) |
| `Adapters__OpportunityBaseUrl` | URL base do serviço opportunity-pipeline (Polly retry ativo) |
| `Adapters__AccountBaseUrl` | URL base do serviço account-management (Polly retry ativo) |
| `OpenTelemetry__Endpoint` | Endpoint OTLP do coletor de traces e métricas |

## Como Executar Localmente

Pré-requisito: Docker Desktop rodando.

```sh
# A partir da raiz do repositório
docker compose up -d postgres

cd services/activity-management
dotnet run --project src/ActivityManagement.Api
```

A API estará disponível em `http://localhost:5000`.
OpenAPI UI: `http://localhost:5000/swagger`.

## Como Testar

```sh
cd services/activity-management

# Testes de arquitetura (gate CI — sem Docker)
dotnet test tests/ActivityManagement.Architecture.Tests

# Testes unitários e de domínio (sem Docker — inclui todos os PBTs)
dotnet test tests/ActivityManagement.Domain.Tests
dotnet test tests/ActivityManagement.Application.Tests
dotnet test tests/ActivityManagement.Api.Tests

# Testes de integração (requer Docker — PostgreSQL via Testcontainers)
dotnet test tests/ActivityManagement.Infrastructure.Tests

# Todos os testes
dotnet test
```

## Observabilidade

- **Logs estruturados** JSON com `correlation_id`, `tenant_id`, `activity_id`. Sem PII (title/description) em texto claro — `[MASKED]` via `PiiMasker`.
- **Métricas Prometheus** (snake_case, todas via `IActivityMetrics`):
  - `activities_created_total`
  - `activities_completed_total`
  - `activities_overdue_total`
  - `digest_action_tokens_used_total`
  - `digest_action_tokens_expired_total`
- **Traces OpenTelemetry**: spans por handler de Command/Query com `correlation_id` como atributo root. Exportador Console habilitado; configurar OTLP em produção.
- **Health checks**: `/health/live` e `/health/ready` com verificação de conectividade PostgreSQL.
- **Alertas**: 5 regras configuradas em `observability/alerts.yaml` (ActivitiesOverdueSpiking, OutboxRelayStalled, LastActivityQueryLatencyHigh, ActivityCreationErrorRateHigh, OverdueScanMissed).

## Segurança

- **Comparação de token em tempo constante** (`ConstantTimeComparison`) via `CryptographicOperations.FixedTimeEquals` — previne ataques de timing (PBT-03, RNF 5).
- **Anti-enumeração**: respostas para token inexistente e atividade inacessível são indistinguíveis em forma.
- **Auditoria imutável**: trigger `trg_audit_logs_immutable` bloqueia UPDATE/DELETE em `audit_logs` (ADR-0003).
- **PII**: `title` e `description` nunca aparecem em logs, delta_json de auditoria ou payloads de evento.
- **Multi-tenancy**: Global Query Filter por `tenant_id` + RLS no PostgreSQL (ADR-0001).

## Aprovações Pendentes (Bloqueadores de Go-Live)

Ver `approvals.yaml` para detalhes:

| ID | Descrição | Aprovador |
|----|-----------|-----------|
| VAL-ACT-01 | Fronteira BC digest/activity — ownership de digest_action_tokens | Arquitetura |
| VAL-ACT-02 | TTL padrão do DigestActionToken | Product Owner |
| VAL-TRD-05 | Residência de dados em southamerica-east1 | Platform Engineering |

## Referências

- `docs/product/modules/activity-management/design.md` — design técnico completo
- `docs/product/modules/activity-management/requirements.md` — requisitos
- `docs/product/modules/activity-management/tasks.md` — plano de tasks
- `docs/product/adr/ADR-0001.md` — isolamento multi-tenant
- `docs/product/adr/ADR-0003.md` — auditoria imutável
- `docs/product/adr/ADR-0006.md` — token de link autenticado
- `docs/product/data-model/data-model.md §BC-04/§BC-06` — modelo de dados
