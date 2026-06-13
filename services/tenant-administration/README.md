# TenantAdministration

Módulo responsável pelo ciclo de vida do tenant na plataforma Azim CRM: provisionamento, suspensão, reativação, identidade textual imutável (slug), configuração de fuso horário IANA, horário do digest e branding white-label estrito com validação de contraste WCAG 2.1 AA.

Este serviço materializa o `tenant_id` — chave de isolamento cross-cutting de toda a plataforma via Row-Level Security (RLS).

## Estrutura da Solution

```text
TenantAdministration.slnx
├── src/
│   ├── TenantAdministration.Domain          # Agregado Tenant, objetos de valor, eventos, policies, state machine
│   ├── TenantAdministration.Application     # Commands, Queries, Handlers, ports (interfaces), saga
│   ├── TenantAdministration.Infrastructure  # EF Core, repositórios, GCP IdP adapter, GCS, Pub/Sub outbox, CDN
│   ├── TenantAdministration.Api             # Controllers REST, autorização, mapeamento DTO
│   └── TenantAdministration.Contracts       # DTOs públicos, contratos de evento (envelopes), brand.json schema
└── tests/
    ├── TenantAdministration.Domain.Tests          # Invariantes, objetos de valor, PBTs de domínio
    ├── TenantAdministration.Application.Tests     # Handlers, saga, validações de aplicação
    ├── TenantAdministration.Infrastructure.Tests  # Repositórios, RLS, adapters (Testcontainers)
    ├── TenantAdministration.Api.Tests             # Contratos de API, autorização, isolamento
    └── TenantAdministration.Architecture.Tests    # Regras de dependência entre camadas
```

## Regras de Dependência (Clean Architecture)

```
Api  → Application, Infrastructure, Contracts
Infrastructure → Application, Domain, Contracts
Application → Domain, Contracts
Domain → ∅   (sem dependências externas)
Contracts → ∅ (sem dependências internas)
```

A regra crítica `Api não acessa Domain diretamente` é validada automaticamente em `TenantAdministration.Architecture.Tests`.

## Configuração Requerida

| Variável de ambiente | Descrição |
|---|---|
| `ConnectionStrings__Platform` | Connection string da role de plataforma (PlatOp) |
| `ConnectionStrings__Tenant` | Connection string da role de tenant (RLS ativa) |
| `Gcp__ProjectId` | ID do projeto GCP |
| `Gcp__IdentityPlatform__ApiKey` | Chave da API do GCP Identity Platform |
| `Gcp__Storage__BucketName` | Nome do bucket GCS para assets de branding |
| `Gcp__PubSub__TopicId` | ID do tópico Pub/Sub (`azim-tenants`) |

## Como Executar Localmente

```sh
# Na raiz do serviço
dotnet build TenantAdministration.slnx
dotnet run --project src/TenantAdministration.Api
```

## Como Testar

```sh
dotnet test TenantAdministration.slnx
```

Para testes de integração com Testcontainers (requer Docker):

```sh
dotnet test tests/TenantAdministration.Infrastructure.Tests
```

## Ondas de Implementação

| Onda | Foco | Status |
|---|---|---|
| Onda 1 | Bootstrap: solution, projetos, dependências, testes de arquitetura | Concluída |
| Onda 2 | Domain: objetos de valor, aggregate, state machine, events, policies, PBTs 01–05, 08 | Pendente |
| Onda 3 | Application: commands, handlers, behaviors, queries, validações | Pendente |
| Onda 4 | Infrastructure: EF Core, RLS, GCS, IdP, Outbox, Pub/Sub; PBT-06 gate CI; PBT-07 | Pendente |
| Onda 5 | API + Contracts: endpoints REST, brand.json, catálogo de erros, contract tests | Pendente |
| Onda 6 | Hardening: segurança, auditoria, observabilidade, DoD final | Pendente |

## Health Checks

- `/health/live` — liveness (app está vivo)
- `/health/ready` — readiness (Cloud SQL, Pub/Sub e Identity Platform acessíveis)

## Observabilidade

Logs estruturados via Serilog com `correlation_id`, `tenant_id`, `slug`, `actor` e `result` em toda operação. Traces distribuídos via OpenTelemetry. Métricas: `tenant_provisioned_total`, `tenant_provisioning_failed_total`, `tenant_branding_update_total`, `tenant_wcag_reject_total`, `cdn_invalidation_total`.

## Referências

- `docs/product/modules/tenant-administration/design.md` — design técnico completo
- `docs/product/modules/tenant-administration/requirements.md` — requisitos funcionais e não-funcionais
- `docs/product/modules/tenant-administration/tasks.md` — plano de tasks por onda
- `docs/product/adr/ADR-0001.md` — Multi-tenancy pooled DB + RLS
- `docs/product/adr/ADR-0008.md` — Scheduling do digest por fuso IANA
