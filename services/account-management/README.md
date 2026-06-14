# account-management

Módulo **BC-02 — Account Management (Gestão de Contas e Contatos)** do Azim CRM.

**Status: Implementado** — 6 ondas, 18 TASKs, 5 PBTs verdes, 294 testes.

Subdomínio de Suporte. Deployable: `azim-api`.

## Objetivo

Gerenciar **contas** (empresas clientes, compartilhadas por todo o tenant) e **contatos** (pessoas
físicas vinculadas a uma conta, portadoras de PII). Entrega quatro capacidades técnicas centrais:

1. CRUD de contas com dedupe não-bloqueante por nome normalizado (Req 1, Req 3, Req 4).
2. CRUD de contatos com PII protegida por RBAC e mascaramento em logs/auditoria (Req 5, Req 9).
3. Visão 360° da conta, compondo dados próprios com leituras de opportunity-pipeline e
   activity-management (Req 6).
4. Direito ao esquecimento de contato sob LGPD, preservando integridade referencial (Req 7).

Tudo sob isolamento multi-tenant em profundidade (Req 10, RNF 5) e residência de dados no Brasil
(RNF 10 — Cloud SQL `southamerica-east1`).

## Não responsabilidades

- Não gerencia oportunidades ou atividades (leitura somente, via portas).
- Não gerencia usuários ou permissões de tenant (responsabilidade do módulo `organization`).
- Não processa pagamentos ou dados financeiros.
- Não consome eventos de outros módulos (somente publica).

## Ondas de Implementação

| Onda | Foco | TASKs | Status |
|------|------|-------|--------|
| Onda 1 | Bootstrap — solution, projetos, testes de arquitetura | TASK-01 | Concluída |
| Onda 2 | Domain — objetos de valor, agregado Account, domain events, specifications | TASK-02..03 | Concluída |
| Onda 3 | Application — commands, queries, handlers, behaviors, ports | TASK-04..07 | Concluída |
| Onda 4 | Infrastructure — DbContext, migrations, repositório, PiiMasker, Outbox | TASK-08..12 | Concluída |
| Onda 5 | API + Contracts — controllers, DTOs, erros, OpenAPI, contratos de evento | TASK-13..15 | Concluída |
| Onda 6 | Hardening — observabilidade, scan anti-PII, RBAC final, DoD | TASK-16..18 | Concluída |

## Property-Based Tests (PBTs)

| PBT | Propriedade | Status |
|-----|-------------|--------|
| PBT-01 | Idempotência da normalização de nome (`normalize(normalize(x)) == normalize(x)`) | Verde |
| PBT-02 | Equivalência por forma normalizada (acento/caixa/espaço → mesmo resultado) | Verde |
| PBT-03 | Irreversibilidade da anonimização + `contact_id` preservado | Verde |
| PBT-04 | Anti-cross-tenant: nenhuma operação cruza fronteira de tenant | Verde (gate CI) |
| PBT-05 | Visão 360° respeita escopo de BUs autorizadas do usuário | Verde |

## Estrutura da solution

```text
AccountManagement.slnx
├── src/
│   ├── AccountManagement.Contracts/     — DTOs request/response e contratos de evento
│   ├── AccountManagement.Domain/        — Agregado Account, Contact, objetos de valor,
│   │                                      domain events, NameNormalizer, policies
│   ├── AccountManagement.Application/   — Commands, queries, handlers, behaviors, ports,
│   │                                      AccountMetrics (observabilidade)
│   ├── AccountManagement.Infrastructure/— EF Core, migrations, TenantContext, Outbox,
│   │                                      PiiMasker, AuditPublisher, adapters de leitura,
│   │                                      ObservabilityServiceExtensions (OTel + health checks)
│   └── AccountManagement.Api/           — Controllers REST, middleware, Program.cs
│                                          (/metrics Prometheus, /health/live, /health/ready)
├── tests/
│   ├── AccountManagement.Domain.Tests/
│   ├── AccountManagement.Application.Tests/
│   ├── AccountManagement.Infrastructure.Tests/
│   ├── AccountManagement.Api.Tests/
│   └── AccountManagement.Architecture.Tests/   — regras de dependência (gate CI)
├── observability/
│   └── alerts.yaml                       — alertas Cloud Monitoring (TASK-16)
└── approvals.yaml                        — pendências de aprovação humana/jurídica (TASK-18)
```

## Regra de dependência (Clean Architecture)

```
Api → Application, Infrastructure, Contracts
Application → Domain, Contracts   (NUNCA Infrastructure)
Infrastructure → Application, Domain, Contracts
Domain → ∅
Contracts → ∅
```

Validada automaticamente por `AccountManagement.Architecture.Tests` (NetArchTest) em todo PR.

## Stack

| Componente | Versão |
|---|---|
| .NET / C# | 10.0 / latest |
| EF Core | 9.0.6 |
| Npgsql | 9.0.4 |
| MediatR | 12.5.0 |
| FluentValidation | 11.11.0 |
| xUnit | 2.9.3 |
| FluentAssertions | 7.0.0 (MIT) |
| FsCheck.Xunit | 3.2.0 |
| NetArchTest.Rules | 1.3.2 |
| NSubstitute | 5.3.0 |
| Testcontainers.PostgreSql | 4.4.0 |
| Polly | 8.5.2 |
| Serilog | 4.3.0 |
| OpenTelemetry | 1.16.0 |
| prometheus-net.AspNetCore | 8.2.1 |
| AspNetCore.HealthChecks.NpgSql | 9.0.0 |

## Como executar localmente

```bash
# A partir da raiz do serviço
dotnet build AccountManagement.slnx
dotnet test AccountManagement.slnx
```

## Como testar

```bash
# Todos os testes (architecture + domain + application + infrastructure + api)
dotnet test AccountManagement.slnx

# Apenas testes de arquitetura (gate CI rápido)
dotnet test tests/AccountManagement.Architecture.Tests

# Testes de integração (requer Docker — Testcontainers + PostgreSQL)
dotnet test tests/AccountManagement.Infrastructure.Tests

# Gate anti-PII (TASK-17, RNF 1.4)
dotnet test tests/AccountManagement.Infrastructure.Tests --filter "FullyQualifiedName~AntiPiiLogScan"
dotnet test tests/AccountManagement.Application.Tests --filter "FullyQualifiedName~NoPiiInLogs"

# Gate anti-cross-tenant PBT-04
dotnet test tests/AccountManagement.Infrastructure.Tests --filter "FullyQualifiedName~TenantIsolation"
```

## APIs expostas

Base: `/api/v1`. OpenAPI disponível em `/swagger` (ambientes de desenvolvimento e teste).

| Método | Path | Papel mínimo |
|---|---|---|
| GET | `/api/v1/accounts` | Viewer |
| POST | `/api/v1/accounts` | Vendedor |
| GET | `/api/v1/accounts/{id}` | Viewer |
| PATCH | `/api/v1/accounts/{id}` | Vendedor |
| GET | `/api/v1/accounts/{id}/360` | Viewer |
| GET | `/api/v1/accounts/{id}/contacts` | Vendedor (na BU) |
| POST | `/api/v1/accounts/{id}/contacts` | Vendedor (na BU) |
| PATCH | `/api/v1/accounts/{accountId}/contacts/{id}` | Vendedor (na BU) |
| DELETE | `/api/v1/accounts/{accountId}/contacts/{id}` | Tenant Admin |

## Eventos publicados

Via Outbox transacional → Cloud Pub/Sub. Sem PII em claro (RNF 1.2).

| Evento | Channel |
|---|---|
| `account.created.v1` | `account-management.account.created` |
| `account.updated.v1` | `account-management.account.updated` |
| `account.contact_linked.v1` | `account-management.contact.linked` |
| `account.contact_forgotten.v1` | `account-management.contact.forgotten` |

## Eventos consumidos

Nenhum nesta versão.

## Dependências externas

| Serviço | Direção | Mecanismo |
|---|---|---|
| opportunity-pipeline | Saída (leitura) | API HTTP/gRPC interna (mTLS) |
| activity-management | Saída (leitura) | API HTTP/gRPC interna (mTLS) |
| audit-log | Saída (eventos) | Cloud Pub/Sub via Outbox |
| Cloud SQL / PostgreSQL | Leitura e escrita | EF Core + Npgsql (`southamerica-east1`) |

## Configurações e variáveis de ambiente

| Variável | Descrição |
|---|---|
| `ConnectionStrings__AccountManagement` | Connection string PostgreSQL (`southamerica-east1`) |
| `Auth__Authority` | Emissor JWT (GCP Identity Platform) |
| `Auth__Audience` | Audiência JWT |

## Observabilidade

- **Métricas** Prometheus em `/metrics`: `accounts_created_total`, `contacts_created_total`,
  `dedupe_blocked_total`, `domain_events_published_total`, `http_requests_total`,
  `http_request_duration_seconds` (p50/p95/p99).
- **Logs** estruturados JSON com `correlation_id`, `tenant_id`, `account_id`. Sem PII em texto claro (RNF 1).
- **Traces** OpenTelemetry: spans para handlers MediatR, chamadas às portas de leitura (360°)
  e operações HTTP. `correlation_id` como atributo root do trace.
- **Health checks**: `/health/live` (processo) e `/health/ready` (PostgreSQL + processo).
- **Alertas**: `observability/alerts.yaml` — erro criação > 5%, falha Outbox, latência 360° > 800ms p95.

## Gates CI (staging.yml)

| Gate | Descrição | Nível |
|---|---|---|
| `account-management-tenant-isolation-gate` | PBT-04 + Architecture.Tests | sev-1 obrigatório |
| `account-management-anti-pii-gate` | AntiPiiLogScan + PiiMasker + NoPiiInLogs | obrigatório |

## Pendências de go-live

Consulte `approvals.yaml` para as pendências bloqueadoras de go-live:

- **VAL-ACC-01** — política de retenção/descarte LGPD (jurídico)
- **VAL-ACC-02** — base legal LGPD para PII de contatos (jurídico)
- **VAL-ACC-03** — confirmação de conta por tenant (não por BU) pelo Product Owner
- **Residência de dados** — confirmar string de conexão de produção em `southamerica-east1`

## Troubleshooting

| Problema | Diagnóstico |
|---|---|
| `/health/ready` retorna 503 | Verificar conectividade com Cloud SQL; checar logs de pool de conexão |
| `dedupe_blocked_total` não incrementa | Verificar se `SearchSimilarAccountsQuery` está sendo chamada corretamente |
| Alerta de relay do Outbox | Verificar credenciais de Cloud Pub/Sub e conectividade; checar tabela `outbox_messages` |
| PBT-04 falhando em CI | Verificar se Docker está disponível no runner; aumentar timeout do Testcontainers |

## Referências

- `docs/product/modules/account-management/design.md` — design técnico completo
- `docs/product/modules/account-management/requirements.md` — requisitos
- `docs/product/modules/account-management/tasks.md` — plano de implementação (6 ondas, 18 TASKs)
- `observability/alerts.yaml` — alertas operacionais
- `approvals.yaml` — pendências de aprovação humana/jurídica
