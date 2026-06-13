# TenantAdministration

**Status: Implementado** — Onda 6 (Hardening) concluída.

Módulo responsável pelo ciclo de vida do tenant na plataforma Azim CRM: provisionamento, suspensão, reativação, identidade textual imutável (slug), configuração de fuso horário IANA, horário do digest e branding white-label estrito com validação de contraste WCAG 2.1 AA.

Este módulo materializa o `tenant_id` — chave de isolamento cross-cutting de toda a plataforma via Row-Level Security (RLS).

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
    ├── TenantAdministration.Api.Tests             # Contratos de API, autorização, isolamento, segurança
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

| Onda | Foco | TASKs | Status |
|---|---|---|---|
| Onda 1 | Bootstrap: solution, projetos, dependências, testes de arquitetura | TASK-01 | Concluída |
| Onda 2 | Domain: objetos de valor, aggregate, state machine, events, policies, PBTs 01–05, 08 | TASK-02..06 | Concluída |
| Onda 3 | Application: commands, handlers, behaviors, queries, validações | TASK-07..11 | Concluída |
| Onda 4 | Infrastructure: EF Core, RLS, GCS, IdP, Outbox, Pub/Sub; PBT-06 gate CI; PBT-07 | TASK-12..16 | Concluída |
| Onda 5 | API + Contracts: endpoints REST, brand.json, catálogo de erros, contract tests | TASK-17..20 | Concluída |
| Onda 6 | Hardening: segurança, auditoria, observabilidade, DoD final | TASK-21..22 | Concluída |

**22 TASKs implementadas. 285 testes verdes.**

## Property-Based Tests (PBTs)

| PBT | Propriedade | Status |
|---|---|---|
| PBT-01 | Imutabilidade e unicidade global do slug | Verde |
| PBT-02 | Normalização e formato do slug | Verde |
| PBT-03 | White-label estrito — conjunto de campos persistidos | Verde |
| PBT-04 | Monotonicidade da validação de contraste WCAG AA | Verde |
| PBT-05 | Determinismo dos tons derivados | Verde |
| PBT-06 | Isolamento estrito por tenant (anti-cross-tenant) — gate CI | Verde |
| PBT-07 | Atomicidade do provisionamento (ambos-ou-nenhum) | Verde |
| PBT-08 | Transições válidas da state machine do tenant | Verde |

**PBT-06 é gate obrigatório de CI** (RNF 1.1, KPI-06). Testes marcados com `[Trait("Category","SecurityGate")]` interrompem a build se falharem.

## Migrations RLS

**ATENÇÃO:** Qualquer migration que toque `tenant_brandings`, `tenant_id` ou a policy RLS exige **revisão de código obrigatória** antes de ser aplicada em staging/produção (RNF 1.3, design.md §7.5).

## Segurança

- **Isolamento multi-tenant**: EF Core global query filter (Camada 1) + RLS PostgreSQL (Camada 2). Defesa em profundidade.
- **PlatOp bloqueado** de dados comerciais de tenant: `AuthorizationBehavior` rejeita `RequiresTenantAdmin` para PlatOp com 403. Verificado por testes `[SecurityGate]`.
- **Sanitização SVG**: `AssetValidationPolicy` rejeita SVG com `<script>`, handlers `on*` e `xlink:href` externos (DD-007).
- **Auditoria append-only**: eventos `tenant.provisioned`, `tenant.branding_changed`, `tenant.suspended`, `tenant.reactivated` publicados via Outbox para módulo `audit-log` (RNF 5).
- **Integração com audit-log**: desacoplada via porta abstrata `IEventOutbox` — sem dependência circular. O módulo `audit-log` consome eventos do tópico `azim-tenants`.
- **PII**: `adminEmail` nunca exposto em respostas de API nem em payloads de eventos (LGPD, design.md §10).

## Observabilidade

Logs estruturados via Serilog com `correlation_id`, `tenant_id`, `slug`, `actor` e `result` em toda operação.

### Métricas (snake_case)

| Métrica | Descrição |
|---|---|
| `tenant_provisioned_total` | Provisionamentos bem-sucedidos |
| `tenant_provisioning_failed_total` | Falhas de provisionamento (alerta crítico) |
| `tenant_branding_update_total` | Atualizações de branding |
| `tenant_wcag_reject_total` | Rejeições de contraste WCAG |
| `tenant_provisioning_duration_seconds` | Duração do provisionamento (histograma) |
| `cdn_invalidation_total` | Invalidações de CDN disparadas |
| `outbox_failed_total` | Eventos do Outbox com falha após MaxRetries |

Implementação: `TenantAdministrationMetrics` (Infrastructure/Observability), via `System.Diagnostics.Metrics`.

### Alertas

Configurados em `observability/alerts.yaml`:

- **tenant_provisioning_failure** (critical): falha de provisionamento
- **outbox_failed_events** (high): eventos do Outbox com falha
- **tenant_rls_violation** (critical, SEV-1): violação de isolamento entre tenants
- **pubsub_dlq_non_empty** (high): DLQ azim-tenants-dlq com mensagens

## Health Checks

- `/health/live` — liveness: aplicação está rodando (sempre 200 se o processo está vivo)
- `/health/ready` — readiness: verifica conectividade com Cloud SQL (e futuras dependências)
  - Retorna `503` quando Cloud SQL indisponível
  - Tags: `["ready"]` para checks de readiness

## Referências

- `docs/product/modules/tenant-administration/design.md` — design técnico completo (v0.1.0)
- `docs/product/modules/tenant-administration/requirements.md` — requisitos funcionais e não-funcionais (v0.1.0)
- `docs/product/modules/tenant-administration/tasks.md` — plano de tasks por onda (v0.1.1)
- `docs/product/adr/ADR-0001.md` — Multi-tenancy pooled DB + RLS
- `docs/product/adr/ADR-0008.md` — Scheduling do digest por fuso IANA
