# account-management

Módulo **BC-02 — Account Management (Gestão de Contas e Contatos)** do Azim CRM.

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

## Estrutura da solution

```text
AccountManagement.slnx
├── src/
│   ├── AccountManagement.Contracts/     — DTOs request/response e contratos de evento
│   ├── AccountManagement.Domain/        — Agregado Account, Contact, objetos de valor,
│   │                                      domain events, NameNormalizer, policies
│   ├── AccountManagement.Application/   — Commands, queries, handlers, behaviors, ports
│   ├── AccountManagement.Infrastructure/— EF Core, migrations, TenantContext, Outbox,
│   │                                      PiiMasker, AuditPublisher, adapters de leitura
│   └── AccountManagement.Api/           — Controllers REST, middleware, Program.cs
└── tests/
    ├── AccountManagement.Domain.Tests/
    ├── AccountManagement.Application.Tests/
    ├── AccountManagement.Infrastructure.Tests/
    ├── AccountManagement.Api.Tests/
    └── AccountManagement.Architecture.Tests/   — regras de dependência (gate CI)
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
```

## APIs expostas

Base: `/api/v1`. Documentação completa em `docs/openapi/` após Onda 5.

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

Documentação completa em `docs/` após Onda 4. Variáveis essenciais:

| Variável | Descrição |
|---|---|
| `ConnectionStrings__AccountManagement` | Connection string PostgreSQL |
| `Jwt__Authority` | Emissor JWT (GCP Identity Platform) |
| `Jwt__Audience` | Audiência JWT |

## Observabilidade

- Logs estruturados (JSON) com `correlation_id`, `tenant_id`, `account_id`. Sem PII.
- Métricas Prometheus: `accounts_created_total`, `contacts_created_total`, `http_request_duration_seconds`.
- Traces OpenTelemetry: spans para handlers e chamadas downstream.
- Health checks: `/health/live`, `/health/ready` (após Onda 5).

## Referências

- `docs/product/modules/account-management/design.md` — design técnico completo
- `docs/product/modules/account-management/requirements.md` — requisitos
- `docs/product/modules/account-management/tasks.md` — plano de implementação (6 ondas)
