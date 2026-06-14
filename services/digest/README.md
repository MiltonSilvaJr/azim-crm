# Digest — azim-digest-worker

Worker assíncrono responsável por selecionar destinatários, compor e enviar o digest diário por
e-mail do Azim CRM. Bounded Context BC-06 (*Supporting Subdomain* SD-06).

## Responsabilidades

- Selecionar tenants elegíveis por fuso IANA e dia útil.
- Selecionar destinatários por papel, pendências e opt-out.
- Compor digest de pendências (ter–sex) e azimute da semana (segunda) por usuário.
- Enviar via `IEmailSender` (notification-delivery) com idempotência por `EmailDigestLog`.
- Emitir evento `digest.email_sent.v1` via Outbox para o audit-log.
- Gerar tokens de ação de um clique (`DigestActionToken`, 48h, opaco por SHA-256).

## Não responsabilidades

- Calcular estagnação de oportunidades (responsibility: opportunity-pipeline).
- Calcular forecast de metas (responsibility: goal-forecast).
- Executar ações dos tokens (responsibility: activity-management).
- Envio físico de e-mail via provedor (responsibility: notification-delivery).

## Estrutura da solution

```text
Digest.slnx
src/
  Digest.Domain/           → aggregates, entidades, objetos de valor, policies, domain events
  Digest.Application/      → handlers, queries, composers, pipeline behaviors, portas de leitura
  Digest.Infrastructure/   → EF Core, RLS, repositórios, outbox, adaptadores Polly, token factory
  Digest.Api/              → trigger OIDC/WIF, consumer Pub/Sub, health checks
  Digest.Contracts/        → DTOs públicos, evento digest.email_sent.v1, enums
tests/
  Digest.Domain.Tests/         → TenantEligibility, RecipientSelectionPolicy, state machines
  Digest.Application.Tests/    → composição, idempotência, degradação gracosa (PBT-02/04/06)
  Digest.Infrastructure.Tests/ → repositórios, RLS Testcontainers, outbox, token_hash
  Digest.Api.Tests/            → autenticação OIDC, resposta 202, contrato consumer
  Digest.Architecture.Tests/   → regras de dependência e proibição de schema alheio
```

## Regra de dependência (design §3.1)

```text
Api          -> Application, Infrastructure, Contracts
Application  -> Domain, Contracts
Infrastructure -> Application, Domain
Domain       -> ∅
Contracts    -> ∅
```

`Digest.Architecture.Tests` valida e enforça estas setas em todo PR.

## Testes de arquitetura enforçados (TASK-02)

| Regra | Verificação |
|---|---|
| (a) Regra de dependência Clean Architecture | `DependencyRuleTests` — 7 asserções |
| (b) Domain sem EF Core, HTTP ou Pub/Sub SDK | `Domain_ShouldNotContain_InfrastructureNamespaces` |
| (c) Sem DbSet de tabelas alheias (`opportunities`, `activities`, `goals`, `users`) | `Infrastructure_ShouldNotMap_ExternalBoundedContextEntities` |

## Como executar localmente

```bash
# Build
dotnet build services/digest/Digest.slnx

# Testes (incluindo arquitetura)
dotnet test services/digest/Digest.slnx

# Testes de arquitetura apenas
dotnet test services/digest/tests/Digest.Architecture.Tests

# Formato
dotnet format services/digest/Digest.slnx
```

## Variáveis de ambiente (configuração completa em TASK-21)

| Variável | Obrigatória | Descrição |
|---|---|---|
| `ConnectionStrings__Digest` | sim | Connection string PostgreSQL (Cloud SQL) |
| `Digest__PubSubTopic` | sim | Tópico Pub/Sub do fan-out por tenant |
| `Digest__TriggerAudience` | sim | Audience OIDC esperado no trigger |

## Ondas de implementação

| Onda | Foco | Status |
|---|---|---|
| Onda 1 | Bootstrap: solution, projetos, testes de arquitetura | Concluída (TASK-01, TASK-02) |
| Onda 2 | Domínio: aggregates, entidades, objetos de valor, policy | Pendente |
| Onda 3 | Application: handlers, queries, composers | Pendente |
| Onda 4 | Infrastructure: persistência, RLS, factory, repositórios | Pendente |
| Onda 5 | Api e Contratos: trigger, consumer, contratos | Pendente |
| Onda 6 | Hardening: purge, isolamento CI, observabilidade, DoD | Pendente |

## Referências

- Design: `docs/product/modules/digest/design.md`
- Requirements: `docs/product/modules/digest/requirements.md`
- Tasks: `docs/product/modules/digest/tasks.md`
- ADR-0001: isolamento multi-tenant (RLS)
