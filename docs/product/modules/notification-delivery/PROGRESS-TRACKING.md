# PROGRESS-TRACKING — notification-delivery

> Tracker de execução do `/forge:coding-loop notification-delivery` (modo autônomo). Fonte de verdade do estado de codificação.
> Legenda: `[ ]` pendente · `[-]` em progresso · `[X]` concluído (build + testes OK) · `[!]` falhou (HALT).

- Módulo: `notification-delivery` (Generic Subdomain — Sprint 1)
- tasks.md base: v0.1.1 (Aprovado para desenvolvimento)
- Raiz da solution: `services/notification-delivery/`
- Início da execução: 2026-06-13
- Estratégia: branch contínua `feat/notification-delivery/all-waves`, **PR único ao fim do módulo**, merge autônomo após verificação verde.

## Ondas

| Onda | Foco | TASKs | Status |
|------|------|-------|--------|
| 1 | Bootstrap | TASK-01..02 | [X] |
| 2 | Contracts | TASK-03..06 | [X] |
| 3 | Application | TASK-07..10 | [X] |
| 4 | Infrastructure | TASK-11..16 | [X] |
| 5 | PBTs + Hardening | TASK-17..21 | [X] |

## Decisão de provedor (DD-001)

O `tasks.md` (TASK-12/13) menciona Postmark/SendGrid, mas **DD-001 do design é a decisão canônica aprovada (HITL #1, ADR-0005): provedor primário = Resend** (`ResendEmailSender`). Aplicação na Onda 4: TASK-12 → `ResendEmailSender` (primário); TASK-13 → `SendGridEmailSender` (implementação alternativa, prova portabilidade da ACL / RNF 1). Nenhum SDK de provedor pode vazar para `Contracts`/`Application` (NetArchTest enforça).

## Detalhe por onda

### Onda 1 — Bootstrap ✅
🧪 5 testes Architecture verdes · build limpo (0 warn). Solution `NotificationDelivery.slnx`, 3 prod + 4 teste (DD-003, sem Domain/Api). FluentAssertions fixado em 7.0.0 (MIT — 8.x é licença comercial).
- TASK-01 `535491c` · TASK-02 `1122b90`

### Onda 2 — Contracts ✅
🧪 63 testes verdes (Contracts 58 + Architecture 5) · build limpo. VOs imutáveis: `SendStatus`, `FailureReason`/`FailureCode`, `BrandingConfig`, `EmailMessage` (sem PII em ToString/exceções), `SendResult`, `IEmailSender`, `EmailSenderContractTestBase`.
- TASK-03 `f34bd06` · TASK-05 `c7d7359` · TASK-04 `e5a3188` · TASK-06 `a313a17`
- Dívida leve: `Microsoft.Extensions.Diagnostics.HealthChecks` em Contracts (para `CheckAvailabilityAsync`→`HealthCheckResult`, design §8.1); não é provedor, NetArchTest verde.

### Onda 3 — Application ✅
🧪 105 testes verdes (Contracts 58 + Application 42 + Architecture 5) · build limpo. `IEmailProviderClient`, `EmailMessageValidator` (NOTIF-ERR-001/002), `EmailTemplateRenderer` (determinístico), `BrandingEmailDecorator`, `ResilientEmailSender` (Polly 8.7.0: timeout/retry/circuit-breaker, nunca lança ao chamador).
- TASK-07 `d2663c0` · TASK-08 `d1e8f35` · TASK-09 `36088f7` · TASK-10 `400f6ee`

### Onda 4 — Infrastructure ✅
🧪 209 testes verdes (Contracts 58 + Application 42 + Infrastructure 104 + Architecture 5) · build limpo. **DD-001 aplicado:** `ResendEmailSender` (primário) + `SendGridEmailSender` (alternativa/ACL). `ProviderResponseMapper` (mapeamento total, NOTIF-ERR-090 fallback), `SecretManagerProvider` (cache TTL, NOTIF-ERR-040), `EmailHasher` + Serilog destructuring anti-PII, `EmailProviderHealthCheck`, `AddNotificationDelivery()`. Senders testados com `HttpMessageHandler` stub (sem rede). `Google.Cloud.SecretManager.V1` 2.7.0.
- TASK-11 `01b85fb` · TASK-14 `73ce77e` · TASK-12 `20a4210` · TASK-13 `4473416` · TASK-15 `b06c1ad` · TASK-16 `9f56362`
- Pendência operacional: `SecretManagerServiceClient` deve ser registrado no DI do deployable consumidor (`SecretManagerServiceClient.Create()`).

### Onda 5 — PBTs + Hardening ✅
🧪 Verificação final: **231 testes verdes** (Contracts 58 + Application 56 + Infrastructure 112 + Architecture 5) · build limpo. 5 PBTs (FsCheck 3.2.0, ≥500 exemplos cada): PBT-01 reversibilidade, PBT-02 idempotência, PBT-03 anti-PII, PBT-04 totalidade ACL, PBT-05 backoff não duplica. 7 métricas `email_send_*` + traces + 4 alertas Cloud Monitoring. Teste de caos + isolamento por mensagem. DoD §19 preenchido.
- TASK-17 `3a01685` · TASK-18 `22ccb81` · TASK-19 `36d3e0c` · TASK-20 `8fe3efe` · TASK-21 `74c820c`

---

## 🎯 Módulo notification-delivery — 5/5 ondas concluídas

21/21 TASKs com TDD; 5 PBTs verdes; build limpo; 231 testes. **PR #5** abrange todo o módulo.

**Gates de go-live pendentes (operacionais, não-código):** SPF/DKIM/DMARC no DNS; DPA com Resend; ADR-0005 publicada; secrets de produção no GCP Secret Manager. `SecretManagerServiceClient` a registrar no DI do deployable.

## Última falha

_(nenhuma)_
