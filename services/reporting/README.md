# Reporting

Módulo de relatórios analíticos do Azim CRM (BC-07 — Supporting Subdomain, Tier 2).

**Status:** Implementado — Onda 6/6 concluída (TASK-25, DoD assinado)

---

## Visão Geral

Read side puro (CQRS de leitura). Expõe cinco relatórios analíticos — funil por estágio,
forecast por BU/mês, ranking por responsável, oportunidades por canal e comissões por
parceiro (projetado × consolidado) — além de export CSV de qualquer relatório.

Sem escrita de negócio. Sem aggregates transacionais. Lê views de read model derivadas
dos bounded contexts autoritativos (BC-01, BC-03, BC-05) via Dapper/EF read-only.

## Ondas de Implementação

| Onda | Foco | TASKs | Status |
|------|------|-------|--------|
| Onda 1 | Bootstrap | TASK-01..TASK-02 | Concluída |
| Onda 2 | Domain | TASK-03..TASK-04 | Concluída |
| Onda 3 | Application | TASK-05..TASK-12 | Concluída |
| Onda 4 | Infrastructure | TASK-13..TASK-19 | Concluída |
| Onda 5 | API + Contracts | TASK-20..TASK-22 | Concluída |
| Onda 6 | Hardening | TASK-23..TASK-25 | Concluída |

## Estrutura

```
services/reporting/
├── Reporting.slnx                     # Solution .NET 10 (formato XML nativo)
├── global.json                        # SDK .NET 10.0.x
├── Directory.Build.props              # Configurações comuns (net10.0, Nullable, TreatWarningsAsErrors)
├── .editorconfig
├── observability/
│   └── alerts.yaml                    # 6 alertas: latência p95, export, erro, RLS, PlatformOp, Cloud SQL/GCS
├── src/
│   ├── Reporting.Contracts/           # DTOs request/response, enums (ReportType). ∅ deps.
│   ├── Reporting.Domain/              # Objetos de valor de leitura (Money, Period, ChannelShare, StageBucket, ReportScope). ∅ deps.
│   ├── Reporting.Application/         # Queries, handlers, portas, pipeline behaviors, métricas. → Domain, Contracts.
│   ├── Reporting.Infrastructure/      # Repositório Dapper/EF, views RLS, GCS client, health checks. → Application, Domain, Contracts.
│   └── Reporting.Api/                 # ReportingController, endpoints REST, /health/live, /health/ready. → Application, Infrastructure, Contracts.
├── tests/
│   ├── Reporting.Domain.Tests/        # 79 testes: objetos de valor, PBT-02, PBT-04.
│   ├── Reporting.Application.Tests/   # 79 testes: handlers, RBAC, pipeline behaviors, PBT-01, PBT-04, PBT-05.
│   ├── Reporting.Infrastructure.Tests/# 28 testes: views RLS, PBT-03 (gate CI), GCS client, health checks.
│   ├── Reporting.Api.Tests/           # 142 testes: contratos REST, catálogo de erros, anti-enumeração, segurança, observabilidade.
│   └── Reporting.Architecture.Tests/  # 13 testes: regras de dependência Clean Architecture (gate CI).
└── docs/
```

**Total: 341 testes — 0 falhas.**

## Regra de Dependência (design §3)

```
Api            → Application, Infrastructure, Contracts
Application    → Domain, Contracts
Infrastructure → Application, Domain, Contracts
Domain         → ∅
Contracts      → ∅
```

Validada automaticamente por `Reporting.Architecture.Tests` em todo PR (13 regras — gate CI bloqueante).

## Princípios

- **Read side puro**: sem aggregates transacionais (DD-003).
- **Dinheiro em centavos inteiros**: todo `*_cents` é `long` — sem `float`/`double` (DD-007).
- **Isolamento multi-tenant**: `security_invoker` nas views + RLS + Global Query Filter (ADR-0001).
- **RBAC no servidor**: escopo resolvido pelo servidor, nunca pelo cliente (DD-006, Req 7).
- **Minimização de PII**: `display_name` só no ranking e fora dos logs (DD-008, RNF 4).
- **Platform Operator bloqueado**: negação dura antes de qualquer acesso ao banco (RNF 5).
- **Observabilidade**: logs estruturados (`correlation_id`, `tenant_id`), métricas snake_case via `System.Diagnostics.Metrics`, health checks `/health/live` e `/health/ready` (RNF 6, design §11).

## APIs Expostas

| Método | Endpoint | RBAC mínimo |
|--------|----------|-------------|
| GET | `/api/v1/reports/funnel` | Vendedor, GestorBU, TenantAdmin |
| GET | `/api/v1/reports/forecast` | GestorBU, TenantAdmin |
| GET | `/api/v1/reports/ranking` | GestorBU, TenantAdmin |
| GET | `/api/v1/reports/channels` | GestorBU, TenantAdmin |
| GET | `/api/v1/reports/commissions` | GestorBU, TenantAdmin |
| GET | `/api/v1/reports/{type}/export` | GestorBU, TenantAdmin |
| GET | `/health/live` | público (liveness — sem dependências externas) |
| GET | `/health/ready` | público (Cloud SQL + GCS) |

## Métricas Emitidas

| Métrica | Tipo | Labels |
|---------|------|--------|
| `reports_generated_total` | Counter | `report_type`, `outcome` |
| `report_generation_duration_seconds` | Histogram | `report_type` |
| `report_export_duration_seconds` | Histogram | `report_type` |
| `report_rls_denied_total` | Counter | `report_type` |
| `report_scope_denied_total` | Counter | `report_type`, `role` |

Alertas documentados em `observability/alerts.yaml` (latência p95 > 3 s, export > 10 s, erro > 5%,
spike RLS, tentativa de PlatformOperator, Cloud SQL unhealthy, GCS degradado).

## Property-Based Tests (PBT-01..PBT-05)

| PBT | Invariante | Status |
|-----|-----------|--------|
| PBT-01 | Snapshot de comissão imutável após mudança de percentual | Implementado |
| PBT-02 | Conservação da soma de comissão em centavos inteiros | Implementado |
| PBT-03 | Isolamento cross-tenant via RLS anti-vazamento | Implementado |
| PBT-04 | Soma dos percentuais por canal = 100% em basis points | Implementado |
| PBT-05 | Round-trip relatório × export CSV equivalentes | Implementado |

## Gates de Segurança

1. **Zero PII em logs**: testes parametrizados em `SecurityTests` para todos os tipos de relatório.
2. **Platform Operator bloqueado**: 6 Theory tests, um por endpoint, verificando 403 antes do banco.
3. **Cross-tenant no export**: testes verificando que o signed URL de tenant A não contém dados de tenant B.
4. **PBT-03**: gate de CI — falha se views RLS vazar dados cross-tenant.

## Performance Baseline (TASK-25)

Os SLOs definidos são:

| Operação | SLO | Referência |
|----------|-----|-----------|
| Geração de relatório (p95) | ≤ 3.000 ms | RNF 1.1, PTV-01 |
| Export CSV (p95) | ≤ 10.000 ms | RNF 3.1 |
| Volume de referência | 2.000 oportunidades/tenant, período 12 meses | TASK-25 |

**Estado do baseline (Fase 1, ambiente de testes sem banco real):**

- Queries in-process com repositório mock retornam em < 5 ms (sem banco).
- Testes de integração com banco real (Testcontainers / Postgres) cobrem views e RLS, mas não
  medem p95 com volume de 2.000 oportunidades — essa medição exige banco com dados de volume.
- A validação de p95 em produção é responsabilidade do pipeline de carga pré-release com banco real.
- O `QueryTimeoutBehavior` aplica `statement_timeout` configurável (padrão: 5 s) como guarda de SLO
  antes do lançamento em produção (RISK-REPORT-01).

**PTV-01:** alvo de latência p95 ≤ 3 s confirmado com produto (aprovação HITL #1, tasks.md §8).
**RISK-REPORT-06:** `security_invoker` requer Postgres 15+; validar versão do Cloud SQL antes do deploy.
Fallback documentado: predicado `WHERE tenant_id = current_setting('app.current_tenant')::uuid` nas
queries Dapper se `security_invoker` não estiver disponível.

## Configurações e Variáveis de Ambiente

| Variável | Descrição | Padrão |
|----------|-----------|--------|
| `ConnectionStrings__ReportingDb` | Connection string PostgreSQL | `Host=localhost;Database=azim;Username=azim;Password=azim` |
| `REPORTING_DB_CONNECTION` | Alternativa à connection string | — |
| `GCS_BUCKET` | Bucket GCS para export CSV | `azim-reports-dev` |
| `GCS_SIGNED_URL_TTL_MINUTES` | TTL de signed URLs | `15` |
| `JWT_AUTHORITY` | Authority do Identity Provider | obrigatório em produção |
| `JWT_AUDIENCE` | Audience do JWT | `azim-api` |
| `QueryTimeoutOptions__TimeoutSeconds` | Timeout de query (s) | `5` |

## Como Executar Localmente

```sh
cd services/reporting
dotnet build Reporting.slnx
dotnet test Reporting.slnx
```

## Troubleshooting

| Sintoma | Causa Provável | Ação |
|---------|---------------|------|
| `/health/ready` retorna 503 | Cloud SQL inacessível | Verificar connection string e conectividade com banco |
| `/health/ready` retorna 207 (Degraded) | GCS indisponível | Export CSV indisponível; relatórios seguem funcionando |
| Relatório retorna 403 REPORT-ERR-005 | PlatformOperator tentou acessar | Esperado — bloqueio por design (RNF 5) |
| Relatório retorna 403 REPORT-ERR-409 | Tenant não resolvido | Reautenticar; verificar claims JWT |
| Latência p95 > 3 s | Índices ausentes ou volume alto | Verificar `vw_*` com `EXPLAIN`; aumentar `TimeoutSeconds` |

## Referências

- `docs/product/modules/reporting/design.md` v0.1.0
- `docs/product/modules/reporting/requirements.md` v0.1.0
- `docs/product/modules/reporting/tasks.md` v0.1.1
- `docs/product/adr/0001-isolamento-multi-tenant-defesa-em-profundidade.md`
- `services/reporting/observability/alerts.yaml`
