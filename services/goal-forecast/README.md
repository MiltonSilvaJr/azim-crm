# goal-forecast

**BC-05 — Goal & Forecast (Metas e Forecast Comparativo)**
Subdomínio de suporte. Deployable: `azim-api`.

## Objetivo

Gerenciar metas mensais de vendas por BU e responsável, e compor o painel comparativo
"Direção" (meta vs realizado vs pipeline disponível), com agregações trimestral/anual
derivadas e degradação graciosa quando não há meta ou o pipeline está indisponível.

## Responsabilidades

- CRUD de metas mensais (`Goal`) em centavos inteiros, com unicidade por `(tenant_id, bu_id, owner_id, year, month)`.
- Painel comparativo: `GET /forecast` com gap, pct_atingimento e degradação graciosa.
- Agregação trimestral/anual derivada em memória (nunca persistida — RN-027).
- Bloco para o Digest worker: `GET /internal/goals/digest-block`.
- Auditoria via domain event `goal.updated.v1` publicado por outbox.

## Não responsabilidades

- Não calcula forecast por oportunidade (responsabilidade do `opportunity-pipeline`).
- Não gerencia memberships de BU (responsabilidade do `organization`).
- Não agrega dados de outros BCs além da leitura do `ForecastView`.

## Estrutura

```text
services/goal-forecast/
├── src/
│   ├── GoalForecast.Contracts/     # DTOs e schema do evento goal.updated.v1
│   ├── GoalForecast.Domain/        # Aggregate Goal, objetos de valor, policies
│   ├── GoalForecast.Application/   # CQRS handlers, portas, behaviors
│   ├── GoalForecast.Infrastructure/# EF Core, Npgsql, circuit breaker, outbox
│   └── GoalForecast.Api/           # Controllers REST
├── tests/
│   ├── GoalForecast.Domain.Tests/
│   ├── GoalForecast.Application.Tests/
│   ├── GoalForecast.Infrastructure.Tests/
│   ├── GoalForecast.Api.Tests/
│   └── GoalForecast.Architecture.Tests/  # Regras de dependência CI-bloqueadoras
├── docs/
├── config/
├── GoalForecast.slnx
├── Directory.Build.props
├── global.json
└── README.md
```

## APIs expostas

| Método | Rota | Descrição |
|--------|------|-----------|
| POST | `/api/v1/goals` | Criar/atualizar meta (upsert idempotente) |
| PUT | `/api/v1/goals/{id}` | Atualizar valor_meta |
| GET | `/api/v1/goals` | Listar metas (filtro RBAC, paginação) |
| GET | `/api/v1/forecast` | Painel comparativo "Direção" |
| GET | `/api/v1/forecast/aggregate` | Agregação trimestral/anual |
| GET | `/api/v1/internal/goals/digest-block` | Bloco para Digest (mTLS, interno) |

## Eventos publicados

| Evento | Tópico | Quando |
|--------|--------|--------|
| `goal.updated.v1` | `azim-goals` | Após criar ou atualizar meta |

## Eventos consumidos

Nenhum no MVP. Dados de realizado/pipeline lidos de forma síncrona via `IPipelineForecastReader`.

## Dependências

| Serviço | Tipo | Papel |
|---------|------|-------|
| `opportunity-pipeline` | Leitura síncrona (in-process) | Fornece `won_total` e `forecast_ponderado` via `ForecastView` |
| `organization` | Leitura síncrona | Verifica membership `owner_id ∈ bu_id` |

## Configurações e variáveis de ambiente

| Variável | Descrição |
|----------|-----------|
| `ConnectionStrings__GoalForecast` | Connection string Postgres para a tabela `goals` |
| `PipelineForecastReader__TimeoutMs` | Timeout do circuit breaker para leitura do ForecastView |

## Health checks

- Participação no health check do `azim-api`.
- Readiness: banco Postgres disponível.
- A indisponibilidade do pipeline **não** torna o módulo unhealthy (degrada graciosamente).

## Como executar localmente

```sh
cd services/goal-forecast
dotnet build GoalForecast.slnx
dotnet test GoalForecast.slnx
```

## Observabilidade

- Logs estruturados com `correlation_id`, `tenant_id`, `bu_id`, período e ação.
- Métricas: `goals_created_total`, `goals_updated_total`, `forecast_panel_latency_ms`.
- Traces: span por command/query com `correlation_id`.

## Referências

- `docs/product/modules/goal-forecast/design.md` — Design técnico completo
- `docs/product/modules/goal-forecast/requirements.md` — Requisitos
- `docs/product/modules/goal-forecast/tasks.md` — Tarefas e critérios de aceite
- `docs/product/adr/0001-isolamento-multi-tenant-defesa-em-profundidade.md` — ADR-0001 (RLS obrigatória)
