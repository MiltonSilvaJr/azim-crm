# Digest — azim-digest-worker

Worker assíncrono responsável por selecionar destinatários, compor e enviar o digest diário por
e-mail do Azim CRM. Bounded Context BC-06 (*Supporting Subdomain* SD-06).

**Status:** Implementado (Onda 6 concluída — 27 TASKs, 6 ondas, 6 PBTs verdes)

## Responsabilidades

- Selecionar tenants elegíveis por fuso IANA e dia útil.
- Selecionar destinatários por papel, pendências e opt-out.
- Compor digest de pendências (ter–sex) e azimute da semana (segunda) por usuário.
- Enviar via `IEmailSender` (notification-delivery) com idempotência por `EmailDigestLog`.
- Emitir evento `digest.email_sent.v1` via Outbox para o audit-log.
- Gerar tokens de ação de um clique (`DigestActionToken`, 48h, opaco por SHA-256).
- Purge de retenção: `email_digest_logs` > 90 dias e tokens expirados (diário às 02:00 UTC).

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
  Digest.Infrastructure/   → EF Core, RLS, repositórios, outbox, adaptadores Polly, token factory,
                              observabilidade (DigestMetrics), jobs de purge
  Digest.Api/              → trigger OIDC/WIF, consumer Pub/Sub, health checks, hosted service de purge
  Digest.Contracts/        → DTOs públicos, evento digest.email_sent.v1, enums
tests/
  Digest.Domain.Tests/         → TenantEligibility, RecipientSelectionPolicy, state machines, PBT-01/03
  Digest.Application.Tests/    → composição, idempotência, degradação graciosa (PBT-02/04/06)
  Digest.Infrastructure.Tests/ → repositórios, RLS Testcontainers, outbox, token_hash,
                                  purge jobs, isolamento cross-tenant (SecurityGate), observabilidade
  Digest.Api.Tests/            → autenticação OIDC, resposta 202, contrato consumer, health checks
  Digest.Architecture.Tests/   → regras de dependência e proibição de schema alheio
observability/
  alerts.yaml              → definições de alertas para Cloud Monitoring (IaC)
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

## Gate de isolamento cross-tenant (TASK-25 — gate obrigatório de merge)

Os testes `CrossTenantIsolationGateTests` (`Category=SecurityGate`) verificam:

| Cenário | Descrição |
|---|---|
| (a) | Tenant B não vê `EmailDigestLog` de tenant A (RLS) |
| (b) | `DigestActionToken` de tenant A inacessível ao tenant B |
| (c) | RLS WITH CHECK rejeita inserção com `tenant_id` incorreto |
| (d) | Sem `SET app.current_tenant` → 0 linhas (FORCE ROW LEVEL SECURITY — falha-fechada) |

**Estes testes bloqueiam merge se qualquer cenário falhar (RNF 1.3, ADR-0001, RISK-DIGEST-04).**

## Property-Based Tests (PBTs)

| PBT | Propriedade verificada | Status |
|---|---|---|
| PBT-01 | Elegibilidade neutra a DST (fuso IANA) | Verde |
| PBT-02 | N disparos → exatamente 1 envio efetivo (idempotência) | Verde |
| PBT-03 | Regra de inclusão de destinatário total (RecipientSelectionPolicy) | Verde |
| PBT-04 | Bloco de metas omitido graciosamente quando ausente | Verde |
| PBT-05 | Token com 256 bits de entropia, não previsível, expirável | Verde |
| PBT-06 | Azimute presente ⟺ segunda-feira e papel de gestão | Verde |

## Observabilidade (TASK-26)

### Métricas (RNF 6.2)

| Métrica | Tipo | Descrição |
|---|---|---|
| `digest_jobs_processed_total` | Counter | Jobs processados por tenant |
| `digest_emails_sent_total` | Counter | E-mails enviados com sucesso |
| `digest_emails_failed_total` | Counter | E-mails com falha definitiva |
| `digest_processing_duration_seconds` | Histogram | Duração do job por tenant (RNF 4.4) |
| `digest_delivery_rate` | Gauge | Taxa de entrega: sent/(sent+failed) — KPI-03 |

### Alertas (RNF 6.3)

Definidos em `observability/alerts.yaml` para exportação ao Cloud Monitoring:

| Alerta | Condição |
|---|---|
| `digest_no_job_processed` | Ausência de job após horário esperado (P1) |
| `digest_high_failure_rate` | Taxa de falha > 2% por 5 min (P1) |
| `digest_low_delivery_rate` | `delivery_rate < 95%` por > 1 hora (P2) |

### Traces

`ActivitySource` com nome `azim.digest` para integração OpenTelemetry (RNF 6.4).

### PII em telemetria

Nenhum log, métrica, trace ou evento contém e-mail do destinatário, nome ou conteúdo do digest.
Tags de métricas: somente `tenant_id` (UUID opaco) — DD-011, RNF 3.

## Retenção de dados (TASK-24)

| Tabela | Política | Job |
|---|---|---|
| `email_digest_logs` | Purge após 90 dias | `DigestRetentionHostedService` diário às 02:00 UTC |
| `digest_action_tokens` | Purge após `expires_at` | Idem |

## Como executar localmente

```bash
# Build (0 warnings)
dotnet build services/digest/Digest.slnx

# Todos os testes
dotnet test services/digest/Digest.slnx

# Apenas gate de isolamento CI
dotnet test services/digest/tests/Digest.Infrastructure.Tests --filter "Category=SecurityGate"

# Apenas architecture tests
dotnet test services/digest/tests/Digest.Architecture.Tests

# Formato
dotnet format services/digest/Digest.slnx
```

## Variáveis de ambiente

| Variável | Obrigatória | Descrição |
|---|---|---|
| `ConnectionStrings__DigestDb` | sim | Connection string PostgreSQL (Cloud SQL) |
| `Oidc__Authority` | sim | Authority OIDC (ex.: `https://accounts.google.com`) |
| `Oidc__Audience` | sim | Audience do token OIDC do Cloud Scheduler |
| `Oidc__AuthorizedServiceAccountEmail` | sim | SA do Cloud Scheduler autorizado |
| `InternalApi__BaseUrl` | sim | URL base da azim-api interna |

## Ondas de implementação

| Onda | Foco | TASKs | Status |
|---|---|---|---|
| Onda 1 | Bootstrap: solution, projetos .NET 10, testes de arquitetura | TASK-01, TASK-02 | Concluída |
| Onda 2 | Domínio: aggregates, entidades, objetos de valor, policy, domain event | TASK-03..TASK-08 | Concluída |
| Onda 3 | Application: handlers, queries, composers, pipeline behaviors | TASK-09..TASK-13 | Concluída |
| Onda 4 | Infrastructure: persistência, RLS, factory, repositórios, outbox, adaptadores | TASK-14..TASK-20 | Concluída |
| Onda 5 | Api e Contratos: trigger OIDC/WIF, consumer Pub/Sub, contratos | TASK-21..TASK-23 | Concluída |
| Onda 6 | Hardening: purge, isolamento CI, observabilidade, DoD | TASK-24..TASK-27 | Concluída |

## Riscos remanescentes

| Código | Risco | Mitigação |
|---|---|---|
| RISK-DIGEST-07 | `IUserDigestPreferencePort` depende do organization expor o campo de opt-out | Coordenar com equipe de organization antes do go-live |

## Itens de Fase 2

- **VAL-06 (feriados):** ajuste de elegibilidade por feriados nacionais/locais (fora do escopo MVP).
- **Configuração de horário por usuário:** `digest_time` por usuário (atualmente por tenant).
- **Notificações push:** canal adicional além do e-mail.
- **ADR-0004, ADR-0005, ADR-0006, ADR-0007, ADR-0008:** formalização das ADRs referenciadas.

## Referências

- Design: `docs/product/modules/digest/design.md` v0.1.0
- Requirements: `docs/product/modules/digest/requirements.md` v0.1.0
- Tasks: `docs/product/modules/digest/tasks.md` v0.1.1
- ADR-0001: isolamento multi-tenant (RLS) — Aceito
