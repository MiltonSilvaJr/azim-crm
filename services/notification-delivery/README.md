# notification-delivery

Módulo de entrega de e-mail transacional do Azim CRM.

Adapter/ACL stateless que isola os módulos consumidores (`digest`, `organization`, `authentication`) do contrato específico de cada provedor de e-mail transacional externo.

Empacotado como **biblioteca compartilhada** (DD-002) e referenciado por injeção de dependência nos deployables `azim-digest-worker` e `azim-api`.

## Classificação

- Bounded context: BC-14
- Subdomínio: Genérico (SD-14)
- Criticidade: Tier 2
- Entregabilidade alvo: ≥ 98% (KPI-03)

## Estrutura da solução (DD-003)

Clean Architecture adaptada para adapter/ACL stateless — sem projeto `Domain` com agregados de negócio e sem projeto `Api` (design §3, DD-003).

```text
services/notification-delivery/
├── NotificationDelivery.slnx          ← solution (formato XML nativo .NET 10)
├── Directory.Build.props              ← net10.0, Nullable, ImplicitUsings, TreatWarningsAsErrors
├── global.json                        ← SDK fixado
├── .editorconfig
├── src/
│   ├── NotificationDelivery.Contracts/     ← contrato público estável
│   ├── NotificationDelivery.Application/   ← decorators, renderer, porta, validações
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

Portabilidade (RNF 1): SDK/tipos de provedor (Postmark, SendGrid, Resend) **somente em Infrastructure**. Violações são detectadas automaticamente por `Architecture.Tests` em todo PR.

## Projetos de produção

| Projeto | Responsabilidade |
|---|---|
| `NotificationDelivery.Contracts` | `IEmailSender`, `EmailMessage`, `SendResult`, `SendStatus`, `BrandingConfig`, `FailureReason`, `FailureCode` |
| `NotificationDelivery.Application` | `ResilientEmailSender` (Polly), `BrandingEmailDecorator`, `EmailTemplateRenderer`, porta `IEmailProviderClient`, validações de borda |
| `NotificationDelivery.Infrastructure` | `ResendEmailSender` (primário, DD-001), `PostmarkEmailSender`, `SendGridEmailSender`, `ProviderResponseMapper` (ACL), `SecretManagerProvider`, `EmailHasher`, `EmailProviderHealthCheck`, extensões de DI |

## Projetos de teste

| Projeto | Foco |
|---|---|
| `Contracts.Tests` | Imutabilidade e igualdade por valor dos objetos de valor |
| `Application.Tests` | Resiliência, branding, renderização, validações de borda |
| `Infrastructure.Tests` | Mapeamento ACL, senders (provedor fake/WireMock), secrets, hasher |
| `Architecture.Tests` | Regra de dependência DD-003 + proibição de SDK fora da Infrastructure (NetArchTest) |

## Contrato público

```csharp
// Único ponto de acoplamento permitido (Req 1)
Task<SendResult> SendAsync(EmailMessage message, CancellationToken ct);
Task<HealthCheckResult> CheckAvailabilityAsync(CancellationToken ct);
```

Nenhum tipo de provedor aparece na assinatura. Toda falha vira `SendResult` — nunca exceção ao chamador (Req 3.5).

## Referências

- `docs/product/modules/notification-delivery/design.md` — design técnico completo
- `docs/product/modules/notification-delivery/requirements.md` — requisitos
- `docs/product/modules/notification-delivery/tasks.md` — plano de implementação (21 TASKs, 5 ondas)
- DD-001: Resend como provedor primário
- DD-002: biblioteca compartilhada (não microservice)
- DD-003: Clean Architecture adaptada para adapter/ACL stateless
- DD-004: Polly para resiliência
- DD-005: idempotência por propagação de chave
