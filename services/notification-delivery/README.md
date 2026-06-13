# notification-delivery

Módulo de entrega de e-mail transacional do Azim CRM.

**Status: Implementado** (Ondas 1–5 concluídas, 231 testes verdes, 5 PBTs com ≥ 500 exemplos cada).

Adapter/ACL stateless que isola os módulos consumidores (`digest`, `organization`, `authentication`) do contrato específico de cada provedor de e-mail transacional externo.

Empacotado como **biblioteca compartilhada** (DD-002) e referenciado por injeção de dependência nos deployables `azim-digest-worker` e `azim-api`.

## Classificação

- Bounded context: BC-14
- Subdomínio: Genérico (SD-14)
- Criticidade: Tier 2
- Entregabilidade alvo: ≥ 98% (KPI-03)

## Ondas de implementação

| Onda | TASKs | Foco | Status |
|---|---|---|---|
| 1 — Bootstrap | TASK-01..02 | Estrutura da solução, contratos base | [X] Completo |
| 2 — Contracts | TASK-03..06 | `EmailMessage`, `SendResult`, `BrandingConfig`, `FailureCode` | [X] Completo |
| 3 — Application | TASK-07..10 | Architecture.Tests, `ResilientEmailSender`, `BrandingEmailDecorator`, `EmailTemplateRenderer` | [X] Completo |
| 4 — Infrastructure | TASK-11..16 | `ProviderResponseMapper`, `SecretManagerProvider`, `EmailHasher`, `ResendEmailSender`, `SendGridEmailSender`, health check, DI | [X] Completo |
| 5 — PBTs + Hardening | TASK-17..21 | 5 PBTs (≥ 500 exemplos), métricas `email_send_*`, alertas, chaos test, DoD | [X] Completo |

## Estrutura da solução (DD-003)

Clean Architecture adaptada para adapter/ACL stateless — sem projeto `Domain` com agregados de negócio e sem projeto `Api` (design §3, DD-003).

```text
services/notification-delivery/
├── NotificationDelivery.slnx          ← solution (formato XML nativo .NET 10)
├── Directory.Build.props              ← net10.0, Nullable, ImplicitUsings, TreatWarningsAsErrors
├── global.json                        ← SDK fixado
├── observability/
│   └── alerts.yaml                    ← 4 alertas Cloud Monitoring (TASK-20)
├── src/
│   ├── NotificationDelivery.Contracts/     ← contrato público estável
│   ├── NotificationDelivery.Application/   ← decorators, renderer, porta, validações, métricas
│   └── NotificationDelivery.Infrastructure/← adapters de provedor, ACL, secrets, hasher
└── tests/
    ├── NotificationDelivery.Contracts.Tests/
    ├── NotificationDelivery.Application.Tests/
    ├── NotificationDelivery.Infrastructure.Tests/
    └── NotificationDelivery.Architecture.Tests/    ← guarda de arquitetura (NetArchTest)
```

## Regra de dependência (DD-003, inviolável)

```text
Application    → Contracts
Infrastructure → Application, Contracts
Contracts      → (nenhum projeto interno)
```

Portabilidade (RNF 1): SDK/tipos de provedor (SendGrid, Resend) **somente em Infrastructure**. Violações são detectadas automaticamente por `Architecture.Tests` em todo PR.

## Projetos de produção

| Projeto | Responsabilidade |
|---|---|
| `NotificationDelivery.Contracts` | `IEmailSender`, `EmailMessage`, `SendResult`, `SendStatus`, `BrandingConfig`, `FailureReason`, `FailureCode` |
| `NotificationDelivery.Application` | `ResilientEmailSender` (Polly), `BrandingEmailDecorator`, `EmailTemplateRenderer`, `NotificationDeliveryMetrics` (7 instruments), porta `IEmailProviderClient`, validações de borda |
| `NotificationDelivery.Infrastructure` | `ResendEmailSender` (primário, DD-001), `SendGridEmailSender`, `ProviderResponseMapper` (ACL), `SecretManagerProvider`, `EmailHasher`, `EmailDestructuringPolicy`, `EmailPiiScrubberEnricher`, `EmailProviderHealthCheck`, extensões de DI |

## Projetos de teste

| Projeto | Foco | Testes |
|---|---|---|
| `Contracts.Tests` | Imutabilidade e igualdade por valor dos objetos de valor | 58 |
| `Application.Tests` | Resiliência, branding, renderização, validações, métricas (TASK-20), PBT-02/05 (TASK-18), caos e isolamento (TASK-21) | 56 |
| `Infrastructure.Tests` | Mapeamento ACL, senders (provedor fake), secrets, hasher, PBT-01/04 (TASK-17), PBT-03 (TASK-19) | 112 |
| `Architecture.Tests` | Regra de dependência DD-003 + proibição de SDK fora da Infrastructure (NetArchTest) | 5 |

**Total: 231 testes (0 falhas).**

## Contrato público

```csharp
// Único ponto de acoplamento permitido (Req 1)
Task<SendResult> SendAsync(EmailMessage message, CancellationToken ct);
Task<HealthCheckResult> CheckAvailabilityAsync(CancellationToken ct);
```

Nenhum tipo de provedor aparece na assinatura. Toda falha vira `SendResult` — nunca exceção ao chamador (Req 3.5).

## Observabilidade (TASK-20)

### Métricas (snake_case, prefixo `email_send_`)

| Métrica | Tipo | Descrição |
|---|---|---|
| `email_send_attempts_total` | Counter | Tentativas de envio (por provider, tenant_id) |
| `email_send_success_total` | Counter | Envios bem-sucedidos |
| `email_send_failure_total` | Counter | Falhas (por failure_code, is_transient) |
| `email_bounce_total` | Counter | Hard bounces |
| `email_suppressed_total` | Counter | Supressões |
| `email_send_duration_seconds` | Histogram | Latência por tentativa |
| `email_circuit_breaker_state` | ObservableGauge | 0=fechado, 1=aberto, 2=half-open |

### Alertas Cloud Monitoring (`observability/alerts.yaml`)

| Alerta | Condição | Severidade |
|---|---|---|
| ALERT-01 | Taxa de falha > 10% em 5 min | WARNING |
| ALERT-02 | Circuit breaker aberto > 60s | CRITICAL |
| ALERT-03 | Latência p95 > 5s em 10 min | WARNING |
| ALERT-04 | Taxa de bounces > 5% em 30 min | WARNING |

### ActivitySource

`NotificationDelivery.EmailSend` — spans com atributos sem PII: `correlation_id`, `tenant_id`, `provider`, `send.status`, `send.failure_code`.

## Gates de go-live (operacionais — pendentes)

Antes do primeiro envio em produção, os itens abaixo devem ser validados manualmente:

- [ ] **SPF**: `v=spf1 include:_spf.resend.com ~all` no DNS do domínio remetente.
- [ ] **DKIM**: habilitado no painel Resend; registro DNS publicado e verificado.
- [ ] **DMARC**: `v=DMARC1; p=quarantine; rua=mailto:dmarc@azim.com.br` publicado.
- [ ] **DPA**: Data Processing Agreement assinado com Resend (RNF 7, LGPD).
- [ ] **ADR-0005**: formalização do Resend como provedor primário em `docs/product/adr/`.
- [ ] **Secrets de produção**: `RESEND__ApiKey` e `RESEND__FromEmail` provisionados no GCP Secret Manager.

## Matriz de rastreabilidade

| Requisito | Implementação | Status |
|---|---|---|
| Req 1 Interface estável `IEmailSender` | `Contracts/IEmailSender.cs` | [X] |
| Req 2 `EmailMessage` validado | `Contracts/EmailMessage.cs` + `Application/Validation/` | [X] |
| Req 3 `SendResult` sem exceção | `Contracts/SendResult.cs` + `ResilientEmailSender` | [X] |
| Req 4 Senders intercambiáveis | `ResendEmailSender`, `SendGridEmailSender` | [X] |
| Req 5 Branding do tenant | `BrandingEmailDecorator` | [X] |
| Req 6 HTML + plaintext | `EmailTemplateRenderer` | [X] |
| Req 7 Bounce e supressão | `ProviderResponseMapper` + `SendStatus.Bounced/Suppressed` | [X] |
| Req 8 Retry/backoff/circuit breaker | `ResilientEmailSender` (Polly v8) | [X] |
| Req 9 Idempotência | `IdempotencyKey` propagado; PBT-02 verde | [X] |
| Req 10 Credenciais via Secret Manager | `SecretManagerProvider` | [X] |
| Req 11 Health check | `EmailProviderHealthCheck` | [X] |
| RNF 1 Portabilidade | `Architecture.Tests` (NetArchTest) | [X] |
| RNF 2 SPF/DKIM/DMARC | Gate operacional | [GATE] |
| RNF 3 Resiliência | `ResilientEmailSender`; chaos test TASK-21 | [X] |
| RNF 4 Sem PII em logs | `EmailHasher` + `EmailPiiScrubberEnricher`; PBT-03 verde | [X] |
| RNF 5 Observabilidade | `NotificationDeliveryMetrics` + `alerts.yaml` | [X] |
| RNF 6 Gestão de segredos | `SecretManagerProvider`; gitleaks CI | [X] |
| RNF 7 LGPD / DPA | Gate operacional (DPA Resend) | [GATE] |
| PBT-01 Reversibilidade adapter | `Pbt01And04Tests.cs` (500 exemplos) | [X] |
| PBT-02 Idempotência reenvio | `Pbt02And05Tests.cs` (500 exemplos) | [X] |
| PBT-03 Anti-PII telemetria | `Pbt03Tests.cs` (500 exemplos) | [X] |
| PBT-04 Totalidade ACL | `Pbt01And04Tests.cs` (500 exemplos) | [X] |
| PBT-05 Backoff não duplica | `Pbt02And05Tests.cs` (500 exemplos) | [X] |

## Referências

- `docs/product/modules/notification-delivery/design.md` — design técnico completo
- `docs/product/modules/notification-delivery/requirements.md` — requisitos
- `docs/product/modules/notification-delivery/tasks.md` — plano de implementação (21 TASKs, 5 ondas)
- DD-001: Resend como provedor primário
- DD-002: biblioteca compartilhada (não microservice)
- DD-003: Clean Architecture adaptada para adapter/ACL stateless
- DD-004: Polly para resiliência
- DD-005: idempotência por propagação de chave
- DD-006: branding estrito + renderização determinística
- DD-007: credenciais via Secret Manager
- DD-008: mascaramento de PII via EmailHasher
