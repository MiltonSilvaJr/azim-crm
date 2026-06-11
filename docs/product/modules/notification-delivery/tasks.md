# Tasks — NOTIF — Notification Delivery

- Versão: 0.1.0
- Data: 2026-06-11
- Status: Rascunho para revisão
- Referência base requirements: docs/product/modules/notification-delivery/requirements.md v0.1.0
- Referência base design: docs/product/modules/notification-delivery/design.md v0.1.0
- ADRs aplicáveis: ADR-0005 (provedor de e-mail transacional — a formalizar após spike), ADR-0007 (stack de observabilidade GCP — a formalizar)
- Rules aplicáveis: `.forge/rules/architecture/clean-architecture.md`, `.forge/rules/architecture/security-and-secrets.md`, `.forge/rules/architecture/observability.md`, `.forge/rules/architecture/ddd.md`, `.forge/rules/testing/tdd.md`, `.forge/rules/testing/quality-gates.md`, `.forge/rules/conventions/language-policy.md`, `.forge/rules/conventions/naming.md`, `.forge/rules/conventions/conventional-commits.md`, `.forge/rules/conventions/git-worktree.md`

## Histórico de Versões

| Versão | Data | Status | Descrição da alteração |
|--------|------|--------|------------------------|
| 0.1.0 | 2026-06-11 | Rascunho para revisão | Criação inicial do plano de tasks a partir de requirements.md v0.1.0 e design.md v0.1.0 (Req 1..11, RNF 1..7, PBT-01..05, DD-001..009) |

## 1. Convenções de Implementação

### 1.1 TDD-first

Toda implementação com lógica verificável deve seguir o ciclo:

1. Red — escrever teste que falha
2. Green — implementar o mínimo para passar
3. Refactor — melhorar sem alterar comportamento

Nenhuma implementação de objeto de valor, adapter, decorator, mapper ou mecanismo de resiliência é considerada concluída sem teste correspondente.

### 1.2 Property-Based Testing

PBT é obrigatório para os cinco invariantes identificados no requirements.md:

- PBT-01: reversibilidade do adapter (round-trip) — Req 1, Req 4, RNF 1
- PBT-02: idempotência de envio sob reenvio — Req 9, NFR-RES-02
- PBT-03: anti-vazamento de PII em telemetria (invariante) — RNF 4, Req 3
- PBT-04: totalidade da classificação de SendResult (state machine) — Req 3, Req 7, seção 4 do requirements
- PBT-05: backoff não duplica entrega (idempotência) — Req 8, RNF 3

Cada PBT usa geradores de `EmailMessage`, respostas de provedor e sequências de falha conforme design seção 13.1.

### 1.3 Bite-sized Tasks

Cada subtask deve ser estimada para menos de 2 horas. Cada TASK cabe em no máximo 1 dia de trabalho.

### 1.4 Branch Model

```text
<tipo>/<modulo>/<NN>-<slug>
```

Exemplos:

```text
feat/notification-delivery/01-bootstrap-solution
test/notification-delivery/02-architecture-tests
feat/notification-delivery/10-resilient-sender
```

### 1.5 Git Worktree

```sh
git worktree add ../worktrees/notification-delivery/<NN>-<slug> -b <branch>
```

### 1.6 Encerramento de TASK

Cada TASK encerra com:

- testes locais verdes
- coverage gate da camada atendido ou justificativa registrada
- `dotnet build` sem warnings novos
- `dotnet format` executado
- commit em Conventional Commits
- push da branch

### 1.7 Encerramento de Onda

Cada onda encerra com:

- todas as TASKs da onda `[X]`
- CI verde (build + test + lint + architecture tests)
- PR da onda aberto e aprovado conforme regra do projeto
- riscos da onda tratados ou registrados

### 1.8 Early Exit

Se uma subtask falhar:

- marcar como `[-]`
- registrar ponto de falha, comando executado e erro principal
- não mascarar com implementação especulativa
- deixar contexto suficiente para retomada por outro agente ou desenvolvedor

### 1.9 Convenção de Status

- `[ ]` Não iniciado
- `[-]` Em progresso
- `[X]` Concluído
- `[!]` Falhou — exige intervenção humana (interrompe a onda no task-coder)

### 1.10 Convenção canônica de IDs

```
TASK-NN — <título>        ← unidade atômica de invocação do task-coder
  ST-MM — <subtask>       ← etapas TDD internas; numeração reinicia a cada TASK
```

Onda é atributo (campo `**Onda**` no header da TASK) e seção de agrupamento visual — nunca entra no ID da TASK.

## 2. Status Geral

| TASK | Título | Onda | Branch | Status |
|------|--------|------|--------|--------|
| TASK-01 | Bootstrap: solution e projetos (DD-003) | Onda 1 | `feat/notification-delivery/01-bootstrap-solution` | [ ] |
| TASK-02 | Architecture.Tests: regra de dependência e proibição de SDK | Onda 1 | `test/notification-delivery/02-architecture-tests` | [ ] |
| TASK-03 | Contracts: SendStatus + FailureReason | Onda 2 | `feat/notification-delivery/03-sendstatus-failurereason` | [ ] |
| TASK-04 | Contracts: EmailMessage (imutável, validação de borda) | Onda 2 | `feat/notification-delivery/04-emailmessage` | [ ] |
| TASK-05 | Contracts: BrandingConfig | Onda 2 | `feat/notification-delivery/05-brandingconfig` | [ ] |
| TASK-06 | Contracts: SendResult + IEmailSender + base de contrato compartilhado | Onda 2 | `feat/notification-delivery/06-sendresult-iemailsender` | [ ] |
| TASK-07 | Application: IEmailProviderClient (porta) + validações de borda | Onda 3 | `feat/notification-delivery/07-provider-port-validations` | [ ] |
| TASK-08 | Application: EmailTemplateRenderer (HTML + plaintext determinístico) | Onda 3 | `feat/notification-delivery/08-template-renderer` | [ ] |
| TASK-09 | Application: BrandingEmailDecorator | Onda 3 | `feat/notification-delivery/09-branding-decorator` | [ ] |
| TASK-10 | Application: ResilientEmailSender (Polly: timeout + retry + circuit breaker) | Onda 3 | `feat/notification-delivery/10-resilient-sender` | [ ] |
| TASK-11 | Infrastructure: ProviderResponseMapper (ACL, mapeamento total) | Onda 4 | `feat/notification-delivery/11-response-mapper` | [ ] |
| TASK-12 | Infrastructure: PostmarkEmailSender | Onda 4 | `feat/notification-delivery/12-postmark-sender` | [ ] |
| TASK-13 | Infrastructure: SendGridEmailSender | Onda 4 | `feat/notification-delivery/13-sendgrid-sender` | [ ] |
| TASK-14 | Infrastructure: SecretManagerProvider + cache com TTL | Onda 4 | `feat/notification-delivery/14-secret-provider` | [ ] |
| TASK-15 | Infrastructure: EmailHasher (mascaramento de PII) | Onda 4 | `feat/notification-delivery/15-email-hasher` | [ ] |
| TASK-16 | Infrastructure: EmailProviderHealthCheck + DI extensions | Onda 4 | `feat/notification-delivery/16-health-check-di` | [ ] |
| TASK-17 | PBT-01 (reversibilidade adapter) + PBT-04 (totalidade ACL) | Onda 5 | `test/notification-delivery/17-pbt-01-04` | [ ] |
| TASK-18 | PBT-02 (idempotência de reenvio) + PBT-05 (backoff não duplica) | Onda 5 | `test/notification-delivery/18-pbt-02-05` | [ ] |
| TASK-19 | PBT-03 (anti-PII) + Serilog destructuring policy integrada | Onda 5 | `test/notification-delivery/19-pbt-03-anti-pii` | [ ] |
| TASK-20 | Observabilidade: métricas email_send_*, logs estruturados, alertas | Onda 5 | `feat/notification-delivery/20-observability` | [ ] |
| TASK-21 | Hardening: teste de caos + gates de go-live + DoD final | Onda 5 | `test/notification-delivery/21-chaos-golive` | [ ] |

## 3. Ondas de Implementação

| Onda | Foco | TASKs | Critério de fechamento |
|------|------|-------|------------------------|
| Onda 1 | Bootstrap | TASK-01..TASK-02 | Solution compila; regra de dependência enforçada por teste; CI verde |
| Onda 2 | Contracts | TASK-03..TASK-06 | Objetos de valor imutáveis com testes; contrato compartilhado compilando |
| Onda 3 | Application | TASK-07..TASK-10 | Decorators, renderer e ResilientEmailSender com testes; nunca lança ao chamador |
| Onda 4 | Infrastructure | TASK-11..TASK-16 | Senders, mapper, secret provider, hasher e health check com testes; coverage ≥ 70% |
| Onda 5 | PBTs + Hardening | TASK-17..TASK-21 | PBT-01..05 verdes (≥ 500 exemplos cada); observabilidade implementada; DoD preenchido |

## 4. Tarefas

### TASK-01 — Bootstrap: solution e projetos (DD-003)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 1 — Bootstrap |
| **Branch** | `feat/notification-delivery/01-bootstrap-solution` |
| **Worktree** | `git worktree add ../worktrees/notification-delivery/01-bootstrap-solution -b feat/notification-delivery/01-bootstrap-solution` |
| **Status** | [ ] |
| **Depende de** | Não aplicável |
| **Entregável** | `NotificationDelivery.sln` com 3 projetos de produção e 4 de teste compilando; CI mínimo verde |
| **Mapeia** | Req 1, Req 4, RNF 1, DD-002, DD-003 |
| **Camada principal** | DevOps |

#### Objetivo

Criar a estrutura da solution conforme DD-003 (Clean Architecture adaptada para adapter/ACL stateless): 3 projetos de produção (`Contracts`, `Application`, `Infrastructure`) e 4 projetos de teste (`Contracts.Tests`, `Application.Tests`, `Infrastructure.Tests`, `Architecture.Tests`). Configurar referências de projeto respeitando a regra de dependência: `Application → Contracts`; `Infrastructure → Application, Contracts`; `Contracts` sem dependência interna. Nenhum projeto `Domain` nem `Api` (DD-003).

#### Subtasks

- [ ] **ST-01 — Red:** escrever teste de compilação mínimo em `Contracts.Tests` que falha por ausência do projeto `Contracts`.
- [ ] **ST-02 — Green:** criar `NotificationDelivery.sln`; adicionar projetos `Contracts`, `Application`, `Infrastructure` (classlib .NET) e `Contracts.Tests`, `Application.Tests`, `Infrastructure.Tests`, `Architecture.Tests` (xUnit); configurar referências de projeto conforme regra de dependência; adicionar `global.json` com versão de SDK fixada; adicionar configuração de cobertura (`coverlet`).
- [ ] **ST-03 — Refactor:** garantir que nenhum projeto de produção referencia projeto de teste; adicionar `.editorconfig`; verificar `dotnet build` limpo sem warnings.
- [ ] **ST-04 — Docs:** criar esqueleto de `README.md` do projeto com estrutura de pastas e regra de dependência (referenciando DD-003).
- [ ] **ST-05 — Encerramento:** `dotnet build` verde; `dotnet test` (suítes vazias) verde; commit `feat(notification-delivery): bootstrap solution com 3 projetos de producao e 4 de teste (DD-003)`; push.

#### Critérios de Aceite

- [ ] 3 projetos de produção e 4 de teste presentes e compilando.
- [ ] Regra de dependência correta nos `.csproj` (Application → Contracts; Infrastructure → Application, Contracts; Contracts sem dep. interna).
- [ ] `dotnet build` sem warnings.
- [ ] Nenhum tipo de provedor referenciado em qualquer projeto.

---

### TASK-02 — Architecture.Tests: regra de dependência e proibição de SDK de provedor

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 1 — Bootstrap |
| **Branch** | `test/notification-delivery/02-architecture-tests` |
| **Worktree** | `git worktree add ../worktrees/notification-delivery/02-architecture-tests -b test/notification-delivery/02-architecture-tests` |
| **Status** | [ ] |
| **Depende de** | TASK-01 |
| **Entregável** | Suíte `Architecture.Tests` com regras NetArchTest enforçando: (a) `Application` não referencia `Infrastructure`; (b) `Contracts` não referencia `Application`/`Infrastructure`; (c) namespaces de SDK de provedor ausentes de `Contracts` e `Application` |
| **Mapeia** | Req 1.2, Req 4.4, RNF 1, RNF-1.1, DD-003 |
| **Camada principal** | Tests |

#### Objetivo

Garantir por teste automatizado que a fronteira de portabilidade (RNF 1) é inviolável em todo PR: SDK/tipo de provedor só pode existir em `Infrastructure`; `Application` e `Contracts` não podem depender de `Infrastructure`. Esses testes executam em 100% dos PRs e falham o CI em caso de violação.

#### Subtasks

- [ ] **ST-01 — Red:** adicionar `NetArchTest.Fluent` a `Architecture.Tests`; escrever regra que afirma `Application` não depende de `Infrastructure` — confirmar que o teste está bem-formado (pode passar trivialmente se não houver implementação; o valor é que futuras violações sejam detectadas).
- [ ] **ST-02 — Green:** implementar três regras: (a) tipos em `Application` não referenciam `Infrastructure`; (b) tipos em `Contracts` não referenciam `Application` nem `Infrastructure`; (c) nenhum namespace contendo `Postmark` ou `SendGrid` aparece em `Contracts` ou `Application`.
- [ ] **ST-03 — Refactor:** parametrizar os namespaces proibidos em constante `ForbiddenProviderNamespaces`; mapear cada regra ao DD-003 e ao requisito correspondente em comentário de código.
- [ ] **ST-04 — Encerramento:** `dotnet test Architecture.Tests` verde; commit `test(notification-delivery): architecture tests - regra de dependencia e proibicao de SDK de provedor (DD-003, RNF-1)`; push.

#### Critérios de Aceite

- [ ] Regra (a): adicionar referência de `Application` → `Infrastructure` causa falha de teste.
- [ ] Regra (b): adicionar referência de `Contracts` → qualquer projeto interno causa falha de teste.
- [ ] Regra (c): namespace de SDK de provedor em `Contracts`/`Application` causa falha de teste.
- [ ] Testes verdes com a estrutura atual (nenhuma violação existente).
- [ ] Coverage gate: 100% das regras críticas definidas.

---

### TASK-03 — Contracts: SendStatus + FailureReason

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Contracts |
| **Branch** | `feat/notification-delivery/03-sendstatus-failurereason` |
| **Worktree** | `git worktree add ../worktrees/notification-delivery/03-sendstatus-failurereason -b feat/notification-delivery/03-sendstatus-failurereason` |
| **Status** | [ ] |
| **Depende de** | TASK-01 |
| **Entregável** | `SendStatus` enum (5 valores canônicos) e `FailureReason` value object imutável com catálogo de códigos; testes em `Contracts.Tests` |
| **Mapeia** | Req 3, Req 7, seção 4 do requirements, catálogo de erros (design seção 12) |
| **Camada principal** | Contracts |

#### Objetivo

Definir os tipos fundamentais do contrato de saída: o enum `SendStatus` com os cinco estados canônicos (`Sent`, `TransientFailure`, `PermanentFailure`, `Bounced`, `Suppressed`) e o objeto de valor `FailureReason` (code do catálogo, mensagem sem PII, flag `IsRetriable`), imutável, em `NotificationDelivery.Contracts`. Incluir classe estática `FailureCode` com todos os 11 códigos do catálogo (design seção 12).

#### Subtasks

- [ ] **ST-01 — Red:** testes em `Contracts.Tests` que falham: (a) `SendStatus` tem exatamente 5 valores; (b) `FailureReason` com `Code` e `Message` é imutável após construção; (c) construção de `FailureReason` com `Code` vazio lança `ArgumentException`.
- [ ] **ST-02 — Green:** implementar `SendStatus` enum; implementar `FailureReason` como record imutável com `Code` (string, obrigatório), `Message` (string), `IsRetriable` (bool); validar `Code` não nulo/vazio na construção.
- [ ] **ST-03 — Refactor:** adicionar classe estática `FailureCode` com constantes `NOTIF-ERR-001`, `NOTIF-ERR-002`, `NOTIF-ERR-010`, `NOTIF-ERR-011`, `NOTIF-ERR-012`, `NOTIF-ERR-020`, `NOTIF-ERR-021`, `NOTIF-ERR-030`, `NOTIF-ERR-031`, `NOTIF-ERR-040`, `NOTIF-ERR-090` para eliminar strings mágicas no restante da base.
- [ ] **ST-04 — Encerramento:** `dotnet test Contracts.Tests` verde; coverage Contracts ≥ 90%; commit `feat(notification-delivery): SendStatus enum e FailureReason value object com catalogo de erros (Req 3)`; push.

#### Critérios de Aceite

- [ ] `SendStatus` contém exatamente 5 valores sem valor extra.
- [ ] `FailureReason` sem setter público; `Code` vazio lança na construção.
- [ ] `FailureCode` cobre todos os 11 códigos do catálogo.
- [ ] Nenhum tipo de provedor referenciado.

---

### TASK-04 — Contracts: EmailMessage (imutável, validação de borda)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Contracts |
| **Branch** | `feat/notification-delivery/04-emailmessage` |
| **Worktree** | `git worktree add ../worktrees/notification-delivery/04-emailmessage -b feat/notification-delivery/04-emailmessage` |
| **Status** | [ ] |
| **Depende de** | TASK-03, TASK-05 |
| **Entregável** | `EmailMessage` value object imutável com todos os campos (design seção 8.2) e validação na construção; testes de aceite e rejeição |
| **Mapeia** | Req 2, Req 2.1..2.5, RNF 4 |
| **Camada principal** | Contracts |

#### Objetivo

Implementar `EmailMessage` como objeto de valor imutável em `Contracts`, com os campos: `RecipientEmail` (PII, validado sintaticamente), `Subject`, `HtmlBody`, `PlainTextBody?`, `Branding` (`BrandingConfig?`), `TenantId`, `CorrelationId`, `IdempotencyKey?`. Entrada inválida é detectada na construção para permitir o retorno de `PermanentFailure` antes de qualquer chamada ao provedor (Req 2.5). `RecipientEmail` nunca exposto em `ToString()` nem em mensagens de exceção (RNF 4).

#### Subtasks

- [ ] **ST-01 — Red:** testes que falham: (a) `RecipientEmail` inválido (`"nao-e-email"`) lança `ArgumentException` sem o endereço na mensagem; (b) `Subject` vazio lança; (c) `HtmlBody` vazio lança; (d) `TenantId` nulo lança; (e) `CorrelationId` nulo lança; (f) construção válida produz objeto sem setter público (reflexão).
- [ ] **ST-02 — Green:** implementar `EmailMessage` como record imutável; validação sintática de `RecipientEmail` (regex RFC 5321 simplificado ou `System.Net.Mail.MailAddress`); lançar `ArgumentException` com mensagem sem PII para cada campo inválido.
- [ ] **ST-03 — Refactor:** garantir que `RecipientEmail` nunca é exposto via `ToString()`; extrair limite de tamanho de `Subject` e `HtmlBody` como constantes.
- [ ] **ST-04 — Encerramento:** `dotnet test Contracts.Tests` verde; coverage Contracts ≥ 90%; commit `feat(notification-delivery): EmailMessage value object imutavel com validacao de borda sem PII (Req 2)`; push.

#### Critérios de Aceite

- [ ] `EmailMessage` sem setter público.
- [ ] `RecipientEmail` inválido rejeitado com mensagem sem o endereço em claro.
- [ ] `Subject` ou `HtmlBody` vazio rejeitado.
- [ ] `TenantId` e `CorrelationId` ausentes rejeitados.
- [ ] `ToString()` não expõe `RecipientEmail`.

---

### TASK-05 — Contracts: BrandingConfig

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Contracts |
| **Branch** | `feat/notification-delivery/05-brandingconfig` |
| **Worktree** | `git worktree add ../worktrees/notification-delivery/05-brandingconfig -b feat/notification-delivery/05-brandingconfig` |
| **Status** | [ ] |
| **Depende de** | TASK-01 |
| **Entregável** | `BrandingConfig` value object imutável com validação de cores hex; testes |
| **Mapeia** | Req 5, Req 5.2, DD-006 |
| **Camada principal** | Contracts |

#### Objetivo

Implementar `BrandingConfig` com `LogoUrl`, `PrimaryColor` e `SecondaryColor`, validando formato hex (`#RRGGBB`) para as cores. Pode ser desenvolvida em paralelo com TASK-03; é pré-requisito de TASK-04 apenas para compor o campo `Branding` em `EmailMessage`.

#### Subtasks

- [ ] **ST-01 — Red:** testes que falham: (a) `PrimaryColor = "red"` lança; (b) `PrimaryColor = "#GGG"` lança; (c) `LogoUrl` vazio lança; (d) objeto imutável após construção (sem setter público).
- [ ] **ST-02 — Green:** implementar `BrandingConfig` record imutável; validar cores com regex `^#[0-9A-Fa-f]{6}$`; validar `LogoUrl` não vazio.
- [ ] **ST-03 — Refactor:** anotar decisão sobre `BrandingConfig.Default` (tema padrão como instância nula tipada ou `null` tratado pelo decorator em TASK-09).
- [ ] **ST-04 — Encerramento:** testes verdes; Architecture.Tests verde; commit `feat(notification-delivery): BrandingConfig value object com validacao de cores hex (Req 5, DD-006)`; push.

#### Critérios de Aceite

- [ ] Cor hex inválida rejeitada na construção.
- [ ] `LogoUrl` vazio rejeitado.
- [ ] Imutabilidade verificada.
- [ ] Architecture.Tests ainda verde.

---

### TASK-06 — Contracts: SendResult + IEmailSender + base de contrato compartilhado

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Contracts |
| **Branch** | `feat/notification-delivery/06-sendresult-iemailsender` |
| **Worktree** | `git worktree add ../worktrees/notification-delivery/06-sendresult-iemailsender -b feat/notification-delivery/06-sendresult-iemailsender` |
| **Status** | [ ] |
| **Depende de** | TASK-03, TASK-04 |
| **Entregável** | `SendResult` value object com invariantes; `IEmailSender` interface definida; `EmailSenderContractTestBase` (classe abstrata de teste) estruturada |
| **Mapeia** | Req 1, Req 3, Req 3.1..3.5, PBT-01 (base estrutural) |
| **Camada principal** | Contracts |

#### Objetivo

Completar o contrato público do módulo: `IEmailSender` com `SendAsync(EmailMessage, CancellationToken) → Task<SendResult>` e `CheckAvailabilityAsync(CancellationToken) → Task<HealthCheckResult>`; `SendResult` com invariantes do design seção 4.3 (`MessageId` presente sse `Sent`; `Reason` presente sse falha; `CorrelationId` sempre propagado; sem exceção de provedor — Req 3.5). Criar `EmailSenderContractTestBase` (classe abstrata de teste compartilhada) que qualquer sender deve satisfazer — base estrutural do PBT-01 implementado em TASK-17.

#### Subtasks

- [ ] **ST-01 — Red:** testes que falham: (a) `SendResult(Status=Sent)` sem `MessageId` lança na construção; (b) `SendResult` com status de falha sem `Reason` lança; (c) `SendResult` sem setter público; (d) `SendResult.CorrelationId` igual ao da `EmailMessage` de origem.
- [ ] **ST-02 — Green:** implementar `SendResult` record imutável com invariantes; definir `IEmailSender` em `Contracts` sem qualquer tipo de provedor na assinatura; criar `EmailSenderContractTestBase` como classe abstrata com asserções de forma de `SendResult` válidas para qualquer implementação.
- [ ] **ST-03 — Refactor:** verificar que `SendResult.ToString()` e campos não expõem PII; confirmar que `IEmailSender` não importa nenhum namespace de provedor.
- [ ] **ST-04 — Encerramento:** `dotnet test Contracts.Tests` verde; coverage Contracts ≥ 90%; commit `feat(notification-delivery): SendResult value object e IEmailSender interface (Req 1, Req 3, base PBT-01)`; push.

#### Critérios de Aceite

- [ ] `IEmailSender` sem tipo de provedor na assinatura.
- [ ] `SendResult.MessageId` nulo para qualquer status diferente de `Sent`.
- [ ] `SendResult.Reason` nulo para `Sent`.
- [ ] `CorrelationId` propagado em todos os casos.
- [ ] `EmailSenderContractTestBase` compilando (suíte vazia até TASK-17 populá-la).

---

### TASK-07 — Application: IEmailProviderClient (porta de saída) + validações de borda

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/notification-delivery/07-provider-port-validations` |
| **Worktree** | `git worktree add ../worktrees/notification-delivery/07-provider-port-validations -b feat/notification-delivery/07-provider-port-validations` |
| **Status** | [ ] |
| **Depende de** | TASK-06 |
| **Entregável** | `IEmailProviderClient` (porta em Application) + `EmailMessageValidator` com testes; validação inválida mapeada para `PermanentFailure` com `NOTIF-ERR-001`/`NOTIF-ERR-002` sem chamar provedor |
| **Mapeia** | Req 2.5, Req 3.5, catálogo de erros NOTIF-ERR-001/002, design seção 5.5 |
| **Camada principal** | Application |

#### Objetivo

Definir a porta de saída `IEmailProviderClient` em `Application` (implementada por senders em `Infrastructure`); centralizar a validação de borda de `EmailMessage` em `EmailMessageValidator`, cujo resultado produz `PermanentFailure` antes de qualquer chamada ao provedor, sem consumir cota de envio.

#### Subtasks

- [ ] **ST-01 — Red:** testes que falham: (a) `EmailMessageValidator.Validate` retorna `SendResult(PermanentFailure, NOTIF-ERR-001)` para `RecipientEmail` malformado; (b) retorna `NOTIF-ERR-002` para `HtmlBody` vazio; (c) retorna `null` (válida) para mensagem correta.
- [ ] **ST-02 — Green:** definir `IEmailProviderClient` com `SendAsync(EmailMessage, CancellationToken) → Task<ProviderResponse>`; implementar `EmailMessageValidator` retornando `SendResult` de falha ou `null`.
- [ ] **ST-03 — Refactor:** garantir que `EmailMessageValidator` não loga `RecipientEmail` em claro; extrair verificação de `CorrelationId`/`TenantId` como método separado.
- [ ] **ST-04 — Encerramento:** `dotnet test Application.Tests` verde; commit `feat(notification-delivery): IEmailProviderClient porta e validacoes de borda (Req 2.5, NOTIF-ERR-001/002)`; push.

#### Critérios de Aceite

- [ ] `IEmailProviderClient` definido em `Application` (não em `Contracts` nem `Infrastructure`).
- [ ] Validação inválida retorna `SendResult` com `NOTIF-ERR-001` ou `NOTIF-ERR-002`.
- [ ] Nenhuma chamada a provedor ocorre para entrada inválida.
- [ ] Architecture.Tests ainda verde.

---

### TASK-08 — Application: EmailTemplateRenderer (HTML + plaintext determinístico)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/notification-delivery/08-template-renderer` |
| **Worktree** | `git worktree add ../worktrees/notification-delivery/08-template-renderer -b feat/notification-delivery/08-template-renderer` |
| **Status** | [ ] |
| **Depende de** | TASK-05 |
| **Entregável** | `EmailTemplateRenderer` com renderização determinística de HTML responsivo e derivação de plaintext; testes de determinismo e preservação de links de 1 clique |
| **Mapeia** | Req 6, Req 6.1..6.4, DD-006 |
| **Camada principal** | Application |

#### Objetivo

Implementar `EmailTemplateRenderer` que recebe `HtmlBody`, `BrandingConfig?` e metadados, aplica um template responsivo fixo (mobile + desktop) e deriva `PlainTextBody` quando ausente. Renderização deve ser **determinística** (mesma entrada → mesma saída, Req 6.4) e preservar links de 1 clique fornecidos pelo chamador sem alteração (Req 6.2).

#### Subtasks

- [ ] **ST-01 — Red:** testes que falham: (a) mesma `EmailMessage` renderizada duas vezes produz HTML idêntico; (b) link `https://app.azim.com.br/digest?token=abc123` preservado literalmente no output; (c) `PlainTextBody` derivado não é nulo quando apenas `HtmlBody` fornecido; (d) HTML contém `<meta name="viewport"`.
- [ ] **ST-02 — Green:** implementar `EmailTemplateRenderer` com engine inline (interpolação tipada ou Scriban/Razor estático) e recurso embutido (`EmbeddedResource`); derivar plaintext via remoção de tags HTML; garantir saída determinística (sem timestamp interno, sem randomização).
- [ ] **ST-03 — Refactor:** extrair template para arquivo de recurso embutido; testar que o recurso carrega corretamente; verificar ausência de injeção de CSS arbitrário.
- [ ] **ST-04 — Encerramento:** testes verdes; commit `feat(notification-delivery): EmailTemplateRenderer deterministico com HTML responsivo e plaintext (Req 6, DD-006)`; push.

#### Critérios de Aceite

- [ ] Determinismo verificado por teste: saída igual para entrada igual.
- [ ] Links de 1 clique preservados literalmente.
- [ ] `PlainTextBody` gerado quando ausente na entrada.
- [ ] HTML contém indicador de responsividade (`viewport`).

---

### TASK-09 — Application: BrandingEmailDecorator

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/notification-delivery/09-branding-decorator` |
| **Worktree** | `git worktree add ../worktrees/notification-delivery/09-branding-decorator -b feat/notification-delivery/09-branding-decorator` |
| **Status** | [ ] |
| **Depende de** | TASK-07, TASK-08 |
| **Entregável** | `BrandingEmailDecorator` com testes: aplica branding estrito DEC-004, degrada para tema padrão quando ausente, não altera `RecipientEmail`/`Subject`/`CorrelationId` |
| **Mapeia** | Req 5, Req 5.1..5.4, DD-006 |
| **Camada principal** | Application |

#### Objetivo

Implementar `BrandingEmailDecorator` como decorator que injeta `LogoUrl`, `PrimaryColor`, `SecondaryColor` do `BrandingConfig` no template via `EmailTemplateRenderer` antes de delegar ao sender interno; aplica tema padrão quando `Branding` é nulo (Req 5.3); nunca CSS arbitrário (Req 5.2); não modifica `RecipientEmail`, `Subject` nem `CorrelationId` (Req 5.4).

#### Subtasks

- [ ] **ST-01 — Red:** testes que falham: (a) mensagem com `BrandingConfig` válido produz HTML com `PrimaryColor` injetada; (b) mensagem sem `BrandingConfig` não lança e usa cor padrão; (c) `Subject` e `RecipientEmail` da mensagem interna são idênticos aos da entrada; (d) CSS arbitrário não é aceito.
- [ ] **ST-02 — Green:** implementar `BrandingEmailDecorator` delegando a `EmailTemplateRenderer` com os valores de branding; usar tema padrão (`#0F4C81` / `#FFFFFF`) quando `Branding` é nulo; logar aviso (sem PII) quando tema padrão aplicado.
- [ ] **ST-03 — Refactor:** mover constantes de tema padrão para `BrandingDefaults` estático.
- [ ] **ST-04 — Encerramento:** `dotnet test Application.Tests` verde; coverage Application ≥ 85%; commit `feat(notification-delivery): BrandingEmailDecorator com branding estrito DEC-004 e tema padrao (Req 5)`; push.

#### Critérios de Aceite

- [ ] `PrimaryColor` injetado no HTML quando `BrandingConfig` presente.
- [ ] Tema padrão aplicado sem falha quando `BrandingConfig` ausente.
- [ ] `RecipientEmail`, `Subject`, `CorrelationId` inalterados após decoração.
- [ ] Nenhum CSS arbitrário aceito.

---

### TASK-10 — Application: ResilientEmailSender (Polly: timeout + retry + circuit breaker)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/notification-delivery/10-resilient-sender` |
| **Worktree** | `git worktree add ../worktrees/notification-delivery/10-resilient-sender -b feat/notification-delivery/10-resilient-sender` |
| **Status** | [ ] |
| **Depende de** | TASK-07, TASK-09 |
| **Entregável** | `ResilientEmailSender` implementando `IEmailSender`; políticas Polly (timeout 10 s, até 5 retries com backoff exponencial + jitter, circuit breaker após 5 falhas consecutivas); nunca lança ao chamador |
| **Mapeia** | Req 8, Req 8.1..8.5, RNF 3, RNF-3.1..3.3, DD-004, PBT-05 (base estrutural) |
| **Camada principal** | Application |

#### Objetivo

Implementar o `ResilientEmailSender` como o `IEmailSender` registrado no DI do consumidor. Monta a cadeia `ResilientEmailSender → BrandingEmailDecorator → ProviderEmailSender`. Aplica Polly para timeout por tentativa, retry com backoff exponencial + jitter e circuit breaker; `Bounced`/`Suppressed` excluídos da contagem do breaker (Req 7.3). Toda exceção residual capturada e convertida em `SendResult` (nunca lança). Renderização ocorre uma única vez antes do loop de retry (PBT-05 estrutural).

#### Subtasks

- [ ] **ST-01 — Red:** testes com sender fake que falha: (a) uma falha transiente → retenta; (b) N falhas consecutivas abrem o circuit breaker; (c) `Bounced` não abre o breaker; (d) timeout por tentativa ≤ 10 s; (e) após esgotamento retorna `TransientFailure(NOTIF-ERR-010)` sem lançar; (f) branding/renderização chamado exatamente uma vez mesmo com retries.
- [ ] **ST-02 — Green:** implementar `ResilientEmailSender` com `ResiliencePipeline` (Polly v8) ou `PolicyWrap` (Polly v7): `Timeout(10s)` + `Retry(5, backoff: exponential+jitter, handle: only transient)` + `CircuitBreaker(5, excludes: Bounced/Suppressed)`; `try/catch` externo captura qualquer `Exception` residual e retorna `SendResult(TransientFailure, NOTIF-ERR-010)`.
- [ ] **ST-03 — Refactor:** extrair configuração de políticas para `ResilientEmailSenderOptions` (injetável via `IOptions<T>`); garantir que `BrandingEmailDecorator` é chamado uma única vez por envio.
- [ ] **ST-04 — Encerramento:** `dotnet test Application.Tests` verde; coverage Application ≥ 85%; commit `feat(notification-delivery): ResilientEmailSender com Polly timeout retry circuit-breaker (Req 8, RNF 3, DD-004)`; push.

#### Critérios de Aceite

- [ ] N retries configurável (padrão 5); backoff exponencial com jitter.
- [ ] `Bounced` e `Suppressed` não incrementam contador do breaker.
- [ ] Timeout por tentativa não excede 10 s.
- [ ] Nenhuma exceção propagada ao chamador.
- [ ] Renderização executada exatamente uma vez por chamada `SendAsync`.

---

### TASK-11 — Infrastructure: ProviderResponseMapper (ACL, mapeamento total)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/notification-delivery/11-response-mapper` |
| **Worktree** | `git worktree add ../worktrees/notification-delivery/11-response-mapper -b feat/notification-delivery/11-response-mapper` |
| **Status** | [ ] |
| **Depende de** | TASK-06 |
| **Entregável** | `ProviderResponseMapper` cobrindo todos os casos de resposta de provedor → `SendStatus` + `FailureReason`; fallback `NOTIF-ERR-090` para resposta inesperada; nenhum estado indefinido |
| **Mapeia** | Req 3, Req 7, Req 7.1..7.4, PBT-04 (implementação), catálogo de erros (design seção 12) |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar a ACL central: `ProviderResponseMapper` traduz toda resposta/exceção do provedor (HTTP 200, 400, 401, 403, 422, 429, 500, 503, timeout, hard bounce, supressão, resposta inesperada) para `SendStatus` + `FailureReason` canônicos. Mapeamento **total** (PBT-04): nenhuma resposta resulta em estado indefinido ou exceção propagada; `NOTIF-ERR-090` é o fallback conservador.

#### Subtasks

- [ ] **ST-01 — Red:** testes parametrizados cobrindo todos os casos: `200+message_id → Sent`, `400/422 → PermanentFailure/NOTIF-ERR-020`, `401 → PermanentFailure/NOTIF-ERR-021`, `429/5xx → TransientFailure/NOTIF-ERR-010`, `timeout → TransientFailure/NOTIF-ERR-012`, `hard_bounce → Bounced/NOTIF-ERR-030`, `suppressed → Suppressed/NOTIF-ERR-031`, `resposta_inesperada → TransientFailure/NOTIF-ERR-090`.
- [ ] **ST-02 — Green:** implementar `ProviderResponseMapper` com `Map(HttpStatusCode, ProviderResponseBody?) → SendResult` e `MapException(Exception) → SendResult`; case de fallback `NOTIF-ERR-090` para qualquer resposta não mapeada.
- [ ] **ST-03 — Refactor:** garantir que nenhuma mensagem de `FailureReason` expõe PII ou credencial; adicionar comentário mapeando cada case ao estado canônico de `SendResult` e ao código do catálogo.
- [ ] **ST-04 — Encerramento:** testes verdes; commit `feat(notification-delivery): ProviderResponseMapper ACL com mapeamento total (Req 3, Req 7, PBT-04)`; push.

#### Critérios de Aceite

- [ ] Todos os 8 casos de resposta mapeados para `SendStatus` canônico.
- [ ] Nenhuma resposta produz estado indefinido ou lança exceção.
- [ ] `NOTIF-ERR-090` captura qualquer resposta inesperada.
- [ ] `FailureReason.Message` sem PII em todos os casos.

---

### TASK-12 — Infrastructure: PostmarkEmailSender

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/notification-delivery/12-postmark-sender` |
| **Worktree** | `git worktree add ../worktrees/notification-delivery/12-postmark-sender -b feat/notification-delivery/12-postmark-sender` |
| **Status** | [ ] |
| **Depende de** | TASK-11, TASK-14 |
| **Entregável** | `PostmarkEmailSender` implementando `IEmailProviderClient`; testes com `HttpMessageHandler` stub/WireMock; `IdempotencyKey` propagada via `Message-ID` |
| **Mapeia** | Req 4, Req 4.1..4.3, Req 9, DD-001, DD-005 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar o adapter Postmark: traduz `EmailMessage` para payload JSON da API Postmark, chama o endpoint via `HttpClientFactory` com handler tipado, usa `ProviderResponseMapper` para classificar resposta; propaga `IdempotencyKey` via header `Message-ID` estável quando presente (DD-005). Nenhum tipo Postmark cruza a fronteira de `Application`/`Contracts`.

#### Subtasks

- [ ] **ST-01 — Red:** testes com `HttpMessageHandler` stub: (a) sucesso retorna `SendResult(Sent)` com `MessageId`; (b) 5xx retorna `TransientFailure`; (c) hard bounce retorna `Bounced`; (d) `IdempotencyKey` presente no header `Message-ID` da requisição HTTP.
- [ ] **ST-02 — Green:** implementar `PostmarkEmailSender` usando `HttpClientFactory`; montar payload JSON conforme API Postmark; delegar classificação a `ProviderResponseMapper`; ler credencial via `ISecretProvider`; configurar TLS 1.2+.
- [ ] **ST-03 — Refactor:** extrair endpoint e header `X-Postmark-Server-Token` como constantes; garantir que SDK Postmark (se usado) não vaza para `Contracts`/`Application`.
- [ ] **ST-04 — Encerramento:** testes verdes; Architecture.Tests verde; commit `feat(notification-delivery): PostmarkEmailSender adapter com idempotency_key (Req 4, DD-001, DD-005)`; push.

#### Critérios de Aceite

- [ ] `SendResult(Sent)` com `MessageId` para resposta 200 com `message_id`.
- [ ] SDK/tipos Postmark presentes apenas em `Infrastructure`.
- [ ] `IdempotencyKey` propagada via `Message-ID` quando presente.
- [ ] Architecture.Tests verde.

---

### TASK-13 — Infrastructure: SendGridEmailSender

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/notification-delivery/13-sendgrid-sender` |
| **Worktree** | `git worktree add ../worktrees/notification-delivery/13-sendgrid-sender -b feat/notification-delivery/13-sendgrid-sender` |
| **Status** | [ ] |
| **Depende de** | TASK-11, TASK-14 |
| **Entregável** | `SendGridEmailSender` implementando `IEmailProviderClient`; mesma semântica de `SendResult` que `PostmarkEmailSender` para entradas equivalentes; `EmailSenderContractTestBase` verde |
| **Mapeia** | Req 4, Req 4.1..4.3, Req 9, DD-001, DD-005 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar o adapter SendGrid como alternativa intercambiável por configuração. A semântica de `SendResult` deve ser equivalente à do `PostmarkEmailSender` para a mesma entrada (base do PBT-01 em TASK-17). Seleção de sender ativo via `IConfiguration` sem alteração de código dos consumidores.

#### Subtasks

- [ ] **ST-01 — Red:** testes com stub: (a) sucesso retorna `Sent`; (b) 5xx retorna `TransientFailure`; (c) bounce retorna `Bounced`; (d) rodar `EmailSenderContractTestBase` contra `SendGridEmailSender` — falha por ausência de implementação.
- [ ] **ST-02 — Green:** implementar `SendGridEmailSender` usando `HttpClientFactory`; payload SendGrid v3 (`POST /v3/mail/send`); propagar `IdempotencyKey` via header de deduplicação do SendGrid; delegar classificação a `ProviderResponseMapper`.
- [ ] **ST-03 — Refactor:** confirmar que nenhum tipo SendGrid vaza para `Contracts`/`Application`; rodar Architecture.Tests.
- [ ] **ST-04 — Encerramento:** `EmailSenderContractTestBase` verde; Architecture.Tests verde; commit `feat(notification-delivery): SendGridEmailSender adapter intercambiavel (Req 4, DD-001)`; push.

#### Critérios de Aceite

- [ ] `EmailSenderContractTestBase` verde contra `SendGridEmailSender`.
- [ ] SDK/tipos SendGrid ausentes de `Contracts`/`Application`.
- [ ] Seleção de sender por configuração sem alteração de código de consumidores.

---

### TASK-14 — Infrastructure: SecretManagerProvider + cache com TTL

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/notification-delivery/14-secret-provider` |
| **Worktree** | `git worktree add ../worktrees/notification-delivery/14-secret-provider -b feat/notification-delivery/14-secret-provider` |
| **Status** | [ ] |
| **Depende de** | TASK-07 |
| **Entregável** | `ISecretProvider` (porta em Application) + `SecretManagerProvider` (Infrastructure) com cache em memória de TTL curto; falha de acesso retorna `NOTIF-ERR-040`; credencial nunca em log |
| **Mapeia** | Req 10, Req 10.1..10.4, RNF 6, RNF-6.1..6.3, DD-007 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar a cadeia de leitura de credencial: `ISecretProvider` (porta em `Application`) → `SecretManagerProvider` (Infrastructure) lê `POSTMARK_API_KEY` ou `SENDGRID_API_KEY` do GCP Secret Manager. Cache em memória com TTL configurável (padrão 5 min) para evitar uma chamada ao Secret Manager por envio (DD-007). A credencial nunca é logada em nenhuma condição.

#### Subtasks

- [ ] **ST-01 — Red:** testes com Secret Manager fake: (a) `SecretManagerProvider` retorna credencial; (b) segunda leitura dentro do TTL usa cache (manager fake chamado apenas uma vez); (c) leitura após expiração do TTL relê do manager; (d) falha de acesso resulta em exceção que o sender converte em `SendResult(PermanentFailure, NOTIF-ERR-040)`.
- [ ] **ST-02 — Green:** definir `ISecretProvider` com `GetSecretAsync(string secretName, CancellationToken) → Task<string>`; implementar `SecretManagerProvider` com `IMemoryCache` e TTL configurável; integrar com `Google.Cloud.SecretManager.V1`.
- [ ] **ST-03 — Refactor:** adicionar teste de que o `ILogger` não registra o valor da credencial (capturar log via sink in-memory e verificar ausência do valor).
- [ ] **ST-04 — Encerramento:** testes verdes; `gitleaks` não encontra credencial (checagem CI); commit `feat(notification-delivery): SecretManagerProvider com cache TTL (Req 10, RNF 6, DD-007)`; push.

#### Critérios de Aceite

- [ ] Credencial lida do Secret Manager fake, não de env ou constante de código.
- [ ] Cache evita chamada repetida dentro do TTL.
- [ ] Falha de acesso ao Secret Manager → `NOTIF-ERR-040`.
- [ ] Credencial ausente de qualquer log (verificado por teste de logger).

---

### TASK-15 — Infrastructure: EmailHasher (mascaramento de PII)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/notification-delivery/15-email-hasher` |
| **Worktree** | `git worktree add ../worktrees/notification-delivery/15-email-hasher -b feat/notification-delivery/15-email-hasher` |
| **Status** | [ ] |
| **Depende de** | TASK-01 |
| **Entregável** | `EmailHasher` com hash truncado determinístico; Serilog `DestructuringPolicy` bloqueando campo de e-mail em logs; testes confirmando ausência do e-mail em claro no identificador mascarado |
| **Mapeia** | RNF 4, RNF-4.1..4.4, DD-008, PBT-03 (base estrutural) |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar `EmailHasher.Hash(string email) → string` produzindo identificador pseudônimo (SHA-256 truncado em 12 chars hex, sem reversibilidade prática); implementar Serilog `DestructuringPolicy` que intercepta qualquer propriedade de log com nome `RecipientEmail`, `Email` ou `recipient` e substitui pelo hash. Base estrutural do PBT-03 (TASK-19).

#### Subtasks

- [ ] **ST-01 — Red:** testes: (a) `EmailHasher.Hash("user@example.com")` não contém `"user"`, `"@"`, `"example"` nem `".com"`; (b) hash é determinístico (mesmo input, mesmo output); (c) dois e-mails distintos produzem hashes distintos.
- [ ] **ST-02 — Green:** implementar `EmailHasher` com SHA-256 sobre o e-mail normalizado (lowercase trim), truncado para 12 chars hex; implementar Serilog `DestructuringPolicy` registrada no pipeline de log do módulo.
- [ ] **ST-03 — Refactor:** adicionar constante de comprimento do hash; garantir que `EmailHasher` é stateless.
- [ ] **ST-04 — Encerramento:** testes verdes; commit `feat(notification-delivery): EmailHasher e Serilog destructuring policy anti-PII (RNF 4, DD-008)`; push.

#### Critérios de Aceite

- [ ] Hash não contém nenhum fragmento do e-mail original.
- [ ] Determinístico: `Hash(x) == Hash(x)` sempre.
- [ ] Serilog policy compilando e registrada no pipeline de log.
- [ ] Architecture.Tests verde.

---

### TASK-16 — Infrastructure: EmailProviderHealthCheck + DI extensions

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/notification-delivery/16-health-check-di` |
| **Worktree** | `git worktree add ../worktrees/notification-delivery/16-health-check-di -b feat/notification-delivery/16-health-check-di` |
| **Status** | [ ] |
| **Depende de** | TASK-12, TASK-13, TASK-15 |
| **Entregável** | `EmailProviderHealthCheck` implementando `IHealthCheck`; extensão `AddNotificationDelivery(IServiceCollection, IConfiguration)` registrando toda a cadeia; testes de health check |
| **Mapeia** | Req 11, Req 11.1..11.3, RNF 5, design seção 11.5 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar `EmailProviderHealthCheck` que faz ping leve no provedor configurado (Postmark `GET /deliverystats` ou SendGrid `GET /v3/scopes`) sem enviar e-mail real nem consumir cota (Req 11.2). Falha reportada de forma estruturada sem PII e sem credencial (Req 11.3). Criar extensão de DI `AddNotificationDelivery(IServiceCollection, IConfiguration)` registrando toda a cadeia: `ResilientEmailSender → BrandingEmailDecorator → ProviderEmailSender → SecretManagerProvider → EmailHasher`.

#### Subtasks

- [ ] **ST-01 — Red:** testes: (a) health check com provedor fake respondendo 200 retorna `Healthy`; (b) health check com provedor fake respondendo 5xx retorna `Unhealthy` com descrição sem PII; (c) `AddNotificationDelivery` registra `IEmailSender` resolvível no container.
- [ ] **ST-02 — Green:** implementar `EmailProviderHealthCheck` usando `HttpClient` leve; implementar `NotificationDeliveryServiceExtensions.AddNotificationDelivery`; registrar health check no `IHealthChecksBuilder`.
- [ ] **ST-03 — Refactor:** garantir que credencial não aparece em `HealthCheckResult.Description`; garantir que o ping não envia e-mail real.
- [ ] **ST-04 — Encerramento:** testes verdes; coverage Infrastructure ≥ 70%; commit `feat(notification-delivery): EmailProviderHealthCheck e DI extensions (Req 11, RNF 5)`; push.

#### Critérios de Aceite

- [ ] Health check retorna `Healthy`/`Unhealthy` sem PII e sem credencial na descrição.
- [ ] Ping não consome cota de envio do provedor.
- [ ] `IEmailSender` resolvível via `AddNotificationDelivery`.
- [ ] Coverage Infrastructure ≥ 70%.

---

### TASK-17 — PBT-01 (reversibilidade do adapter) + PBT-04 (totalidade da classificação ACL)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — PBTs + Hardening |
| **Branch** | `test/notification-delivery/17-pbt-01-04` |
| **Worktree** | `git worktree add ../worktrees/notification-delivery/17-pbt-01-04 -b test/notification-delivery/17-pbt-01-04` |
| **Status** | [ ] |
| **Depende de** | TASK-12, TASK-13, TASK-11 |
| **Entregável** | PBT-01 e PBT-04 verdes com FsCheck ou CsCheck; geradores de `EmailMessage` e respostas de provedor implementados; ≥ 500 exemplos cada |
| **Mapeia** | PBT-01 (Req 1, Req 4, RNF 1), PBT-04 (Req 3, Req 7, seção 4 do requirements) |
| **Camada principal** | Tests |

#### Objetivo

**PBT-01:** para qualquer `EmailMessage` válida gerada, `PostmarkEmailSender` e `SendGridEmailSender` produzem `SendResult` com a mesma forma (mesmo discriminante `SendStatus`, `MessageId` presente sse `Sent`, `CorrelationId` igual ao da entrada); nenhum chamador distingue o provedor pelo tipo da saída.

**PBT-04:** para qualquer resposta de provedor gerada do conjunto `{200, 400, 401, 422, 429, 500, 503, timeout, hard_bounce, suppressed, resposta_inesperada}`, `ProviderResponseMapper` produz exatamente um `SendStatus` canônico sem lançar exceção.

#### Subtasks

- [ ] **ST-01 — Red:** definir geradores: `Arb.EmailMessageValid` (destinatário RFC 5321, assunto e corpo não vazios, `TenantId` e `CorrelationId` como GUIDs); `Arb.ProviderResponse` (conjunto discreto dos 11 casos acima); escrever propriedades PBT-01 e PBT-04 que falham por suíte vazia.
- [ ] **ST-02 — Green:** popular `EmailSenderContractTestBase` com a propriedade PBT-01; conectar geradores ao `ProviderResponseMapper` para PBT-04; configurar 500 exemplos em `FsCheckConfig` ou equivalente.
- [ ] **ST-03 — Refactor:** registrar seeds de reprodução para falhas de shrinking; documentar geradores em comentário de código.
- [ ] **ST-04 — Encerramento:** PBT-01 e PBT-04 verdes; commit `test(notification-delivery): PBT-01 reversibilidade adapter e PBT-04 totalidade ACL (Req 1, Req 4, Req 7)`; push.

#### Critérios de Aceite

- [ ] PBT-01: `SendStatus` idêntico entre Postmark e SendGrid para a mesma entrada simulada.
- [ ] PBT-04: toda resposta do conjunto gerado produz exatamente um `SendStatus` canônico.
- [ ] Nenhuma exceção propagada em PBT-04.
- [ ] ≥ 500 exemplos por propriedade.

---

### TASK-18 — PBT-02 (idempotência de reenvio) + PBT-05 (backoff não duplica entrega)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — PBTs + Hardening |
| **Branch** | `test/notification-delivery/18-pbt-02-05` |
| **Worktree** | `git worktree add ../worktrees/notification-delivery/18-pbt-02-05 -b test/notification-delivery/18-pbt-02-05` |
| **Status** | [ ] |
| **Depende de** | TASK-10, TASK-12 |
| **Entregável** | PBT-02 e PBT-05 verdes; gerador de sequências de falha transiente + sucesso; contador de entregas efetivas ≤ 1 verificado por propriedade |
| **Mapeia** | PBT-02 (Req 9, NFR-RES-02), PBT-05 (Req 8, RNF 3) |
| **Camada principal** | Tests |

#### Objetivo

**PBT-02:** para qualquer `EmailMessage` com `IdempotencyKey` fixa, chamar `SendAsync` N ≥ 1 vezes com provedor fake que aceita apenas uma entrega por chave resulta em no máximo uma entrega efetiva e `SendResult` equivalentes entre as chamadas.

**PBT-05:** para qualquer sequência gerada de até 4 falhas transientes seguida de sucesso, com a mesma `IdempotencyKey`, o número de entregas efetivas ao provedor fake é ≤ 1 (renderização única + mesma chave = sem duplicata).

#### Subtasks

- [ ] **ST-01 — Red:** gerador `Arb.TransientThenSuccessSequence(maxFailures: 4)` (até 4 falhas antes do sucesso, dentro do limite de retries); gerador de N ∈ [1, 10] chamadas com mesma `IdempotencyKey`; escrever propriedades PBT-02 e PBT-05 — falham por ausência de provedor fake com contador.
- [ ] **ST-02 — Green:** implementar provedor fake com `Dictionary<string, int>` contando entregas por `IdempotencyKey`; conectar ao `ResilientEmailSender`; rodar propriedades.
- [ ] **ST-03 — Refactor:** confirmar que renderização é pré-computada antes do loop de retry em `ResilientEmailSender` (mesma chave + mesmo payload em todas as tentativas sustenta PBT-05).
- [ ] **ST-04 — Encerramento:** PBT-02 e PBT-05 verdes; commit `test(notification-delivery): PBT-02 idempotencia reenvio e PBT-05 backoff nao duplica (Req 9, Req 8)`; push.

#### Critérios de Aceite

- [ ] PBT-02: entregas efetivas ≤ 1 para N chamadas com mesma chave.
- [ ] PBT-05: entregas efetivas ≤ 1 em sequência de falhas transientes + sucesso.
- [ ] `ResilientEmailSender` renderiza exatamente uma vez antes do loop de retry.
- [ ] ≥ 500 exemplos por propriedade.

---

### TASK-19 — PBT-03 (anti-PII) + Serilog destructuring policy integrada

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — PBTs + Hardening |
| **Branch** | `test/notification-delivery/19-pbt-03-anti-pii` |
| **Worktree** | `git worktree add ../worktrees/notification-delivery/19-pbt-03-anti-pii -b test/notification-delivery/19-pbt-03-anti-pii` |
| **Status** | [ ] |
| **Depende de** | TASK-15, TASK-10 |
| **Entregável** | PBT-03 verde; Serilog `InMemorySink` capturando output durante `SendAsync`; regex de e-mail não encontra match em nenhum log/erro gerado |
| **Mapeia** | PBT-03 (RNF 4, Req 3), RNF-4.1..4.4, DD-008 |
| **Camada principal** | Tests |

#### Objetivo

**PBT-03:** para qualquer `EmailMessage` com destinatário aleatório, todo log, métrica e `FailureReason.Message` produzido durante o ciclo `SendAsync` não contém o endereço de e-mail em claro nem a credencial do provedor. Implementar com Serilog sink in-memory e scan por regex de e-mail sobre o output capturado.

#### Subtasks

- [ ] **ST-01 — Red:** configurar Serilog `InMemorySink` no pipeline de teste; gerador `Arb.UniqueEmailMessage` com e-mails RFC 5321 aleatórios; escrever propriedade PBT-03 — falha por ausência da destructuring policy ativa.
- [ ] **ST-02 — Green:** ativar `EmailHasher` e Serilog destructuring policy (TASK-15) no pipeline de teste; confirmar que regex `[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}` não encontra match no log capturado.
- [ ] **ST-03 — Refactor:** estender o scan para `FailureReason.Message` de todos os estados de falha gerados; adicionar verificação de ausência da credencial mock no log.
- [ ] **ST-04 — Encerramento:** PBT-03 verde; commit `test(notification-delivery): PBT-03 anti-vazamento de PII em telemetria (RNF 4, DD-008)`; push.

#### Critérios de Aceite

- [ ] Regex de e-mail não encontra match em nenhum log capturado.
- [ ] Credencial mock não aparece em nenhum log.
- [ ] `FailureReason.Message` sem e-mail para qualquer status de falha gerado.
- [ ] ≥ 500 exemplos.

---

### TASK-20 — Observabilidade: métricas email_send_*, logs estruturados, alertas

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — PBTs + Hardening |
| **Branch** | `feat/notification-delivery/20-observability` |
| **Worktree** | `git worktree add ../worktrees/notification-delivery/20-observability -b feat/notification-delivery/20-observability` |
| **Status** | [ ] |
| **Depende de** | TASK-10, TASK-15 |
| **Entregável** | 7 métricas do catálogo emitidas (design seção 11.2); logs estruturados com campos obrigatórios sem PII; 4 alertas definidos em configuração Cloud Monitoring; span de trace correlacionado |
| **Mapeia** | RNF 5, RNF-5.1..5.4, RNF 4, Req 7.5, design seção 11 |
| **Camada principal** | Application / Infrastructure |

#### Objetivo

Instrumentar `ResilientEmailSender` e `ProviderEmailSender` com métricas via `System.Diagnostics.Metrics` (ou OpenTelemetry): `email_send_attempts_total`, `email_send_success_total`, `email_send_failure_total`, `email_bounce_total`, `email_suppressed_total`, `email_send_duration_seconds`, `email_circuit_breaker_state`. Emitir logs estruturados (Serilog) com `correlation_id`, `tenant_id`, `provider`, `status`, `message_id`, `attempt`, `recipient_hash` — nunca o e-mail em claro. Definir os 4 alertas como documento de configuração Cloud Monitoring.

#### Subtasks

- [ ] **ST-01 — Red:** testes com `MeterListener` ou `MetricCollector` in-memory: (a) após `SendAsync` bem-sucedido, `email_send_attempts_total` e `email_send_success_total` incrementados com labels `tenant_id` e `provider`; (b) após falha, `email_send_failure_total` incrementado com label `status`; (c) log capturado contém `correlation_id` e `tenant_id`.
- [ ] **ST-02 — Green:** adicionar `Meter("NotificationDelivery")` ao `ResilientEmailSender`; emitir 7 contadores e histograma; emitir span com `Activity`; garantir labels `tenant_id` e `provider`.
- [ ] **ST-03 — Refactor:** extrair nomes de métrica para `NotificationDeliveryMetrics` estático; criar arquivo `alerts.yaml` com os 4 alertas: taxa de falha > 2% em 1 h (RNF-5.3), bounce > 5% (RNF-2.2), circuit breaker aberto (Req 8.3), falha de Secret Manager (RISK-NOTIF-02).
- [ ] **ST-04 — Encerramento:** testes verdes; commit `feat(notification-delivery): observabilidade completa metricas logs traces alertas (RNF 5, RNF 4)`; push.

#### Critérios de Aceite

- [ ] 7 métricas emitidas conforme catálogo do design seção 11.2.
- [ ] Log com `correlation_id`, `tenant_id`, `provider`, `status`, `recipient_hash` (sem e-mail em claro).
- [ ] 4 alertas definidos em arquivo de configuração.
- [ ] Span de trace com `correlation_id` como atributo.

---

### TASK-21 — Hardening: teste de caos + gates de go-live + DoD final

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — PBTs + Hardening |
| **Branch** | `test/notification-delivery/21-chaos-golive` |
| **Worktree** | `git worktree add ../worktrees/notification-delivery/21-chaos-golive -b test/notification-delivery/21-chaos-golive` |
| **Status** | [ ] |
| **Depende de** | TASK-17, TASK-18, TASK-19, TASK-20 |
| **Entregável** | Teste de caos verde (provedor indisponível → worker re-tenta, abre breaker, retorna `TransientFailure` sem bloquear); isolamento por mensagem verificado; DoD checklist preenchido; gates de go-live documentados |
| **Mapeia** | RNF 3, RNF-3.4, RNF 2, RNF 7, Req 8.5, DD-001 (gate de go-live) |
| **Camada principal** | Tests / Docs |

#### Objetivo

Executar o teste de caos: provedor completamente indisponível (WireMock retornando 503 em todos os endpoints) → `ResilientEmailSender` re-tenta N vezes, abre o circuit breaker, retorna `SendResult(TransientFailure, NOTIF-ERR-011)` sem lançar ao chamador e sem bloquear o processamento das demais mensagens (Req 8.5). Documentar checklist de gates de go-live (SPF/DKIM/DMARC, DPA, ADR-0005) como itens rastreáveis pendentes.

#### Subtasks

- [ ] **ST-01 — Red:** teste de caos com WireMock respondendo 503 para todos os POSTs: (a) `SendAsync` retorna `TransientFailure(NOTIF-ERR-011)` em tempo ≤ `(timeout × retries + jitter_max)` sem lançar; (b) segunda e terceira mensagens do lote não são impedidas pela falha da primeira (isolamento por mensagem Req 8.5).
- [ ] **ST-02 — Green:** configurar WireMock com stub de falha total; executar `ResilientEmailSender` em loop de 3 mensagens distintas; verificar `SendResult` de cada uma individualmente.
- [ ] **ST-03 — Refactor:** revisar todos os itens do DoD (design seção 19); preencher checklist no arquivo de `tasks.md`; registrar como pendências operacionais: gate SPF/DKIM/DMARC (Fase 0, RNF-2.1), gate DPA (RNF 7), gate ADR-0005 (DD-001).
- [ ] **ST-04 — Docs:** atualizar `README.md` do módulo com status das ondas, versão `0.1.0`, artefatos e gates de go-live pendentes; sincronizar matriz de rastreabilidade (seção 5) com status `[X]` nos itens concluídos.
- [ ] **ST-05 — Encerramento:** todos os testes verdes; PBT-01..05 verdes; coverage gates atendidos; commit `test(notification-delivery): teste de caos e hardening final (RNF 3, DoD)`; push.

#### Critérios de Aceite

- [ ] Teste de caos verde: `TransientFailure(NOTIF-ERR-011)` retornado dentro do timeout esperado.
- [ ] 3 mensagens processadas independentemente (isolamento por mensagem).
- [ ] DoD checklist do design seção 19 preenchido.
- [ ] Gates de go-live (SPF/DKIM/DMARC, DPA, ADR-0005) registrados como pendências operacionais.
- [ ] README do módulo sincronizado.

---

## 5. Matriz de Rastreabilidade

| Origem | Descrição | TASKs | Status |
|--------|-----------|-------|--------|
| Req 1 | Interface estável `IEmailSender` como único ponto de acoplamento | TASK-02, TASK-06, TASK-10 | [ ] |
| Req 2 | Objeto de valor `EmailMessage` imutável com validação de borda | TASK-04, TASK-07 | [ ] |
| Req 3 | Objeto de valor `SendResult` como contrato de saída | TASK-03, TASK-06, TASK-11 | [ ] |
| Req 4 | Implementações concretas intercambiáveis por provedor | TASK-12, TASK-13, TASK-17 | [ ] |
| Req 5 | Aplicação de branding do tenant no template | TASK-05, TASK-09 | [ ] |
| Req 6 | Corpo HTML responsivo + plaintext | TASK-08 | [ ] |
| Req 7 | Bounce e supressão classificados e retornados ao chamador | TASK-11, TASK-17 | [ ] |
| Req 8 | Retry com backoff + circuit breaker sem bloquear chamador | TASK-10, TASK-21 | [ ] |
| Req 9 | Suporte à idempotência do chamador (`IdempotencyKey`) | TASK-12, TASK-13, TASK-18 | [ ] |
| Req 10 | Credenciais exclusivamente via Secret Manager | TASK-14 | [ ] |
| Req 11 | Health check do provedor | TASK-16 | [ ] |
| RNF 1 | Portabilidade de provedor sem impacto no domínio | TASK-02, TASK-06, TASK-17 | [ ] |
| RNF 2 | Entregabilidade e pré-condições SPF/DKIM/DMARC | TASK-20, TASK-21 | [ ] |
| RNF 3 | Resiliência e não bloqueio do chamador | TASK-10, TASK-18, TASK-21 | [ ] |
| RNF 4 | Ausência de PII em logs e mensagens de erro | TASK-15, TASK-19, TASK-20 | [ ] |
| RNF 5 | Observabilidade de envio | TASK-16, TASK-20 | [ ] |
| RNF 6 | Gestão de segredos do provedor | TASK-14 | [ ] |
| RNF 7 | Conformidade LGPD na transferência de PII ao provedor | TASK-21 | [ ] |
| PBT-01 | Reversibilidade do adapter (round-trip) | TASK-06, TASK-17 | [ ] |
| PBT-02 | Idempotência de envio sob reenvio | TASK-18 | [ ] |
| PBT-03 | Anti-vazamento de PII em telemetria (invariante) | TASK-15, TASK-19 | [ ] |
| PBT-04 | Totalidade da classificação de SendResult (state machine) | TASK-11, TASK-17 | [ ] |
| PBT-05 | Backoff não duplica entrega (idempotência) | TASK-10, TASK-18 | [ ] |
| DD-001 | Postmark primário, SendGrid alternativa; seleção por configuração | TASK-12, TASK-13, TASK-21 | [ ] |
| DD-002 | notification-delivery como biblioteca compartilhada | TASK-01, TASK-16 | [ ] |
| DD-003 | Clean Architecture adaptada: 3 projetos sem Domain/Api | TASK-01, TASK-02 | [ ] |
| DD-004 | Resiliência via Polly no ResilientEmailSender | TASK-10, TASK-21 | [ ] |
| DD-005 | Idempotência por propagação de chave, sem estado próprio | TASK-12, TASK-13, TASK-18 | [ ] |
| DD-006 | Renderização determinística com branding estrito | TASK-08, TASK-09 | [ ] |
| DD-007 | Credencial via Secret Manager com cache TTL curto | TASK-14 | [ ] |
| DD-008 | Mascaramento de PII na borda da telemetria | TASK-15, TASK-19, TASK-20 | [ ] |
| DD-009 | Webhook de bounce/supressão fora do MVP (mapeamento preparado) | TASK-11 | [ ] |

## 6. Coverage Gates

| Camada / Projeto | Gate | Tipo de teste esperado |
|---|---|---|
| `Contracts.Tests` | ≥ 90% | Unitários: imutabilidade, igualdade por valor, invariantes de construção |
| `Application.Tests` | ≥ 85% | Unitários: resiliência, branding, renderização, validações; PBT-02/05 |
| `Infrastructure.Tests` | ≥ 70% | Integração: mapeamento ACL, senders (stub/WireMock), secret provider, hasher |
| `Architecture.Tests` | 100% das regras críticas definidas | Regras NetArchTest: dependência entre camadas + proibição de SDK de provedor |
| Anti-PII (segurança) | Cobertura por PBT-03 + scan de log no CI | PBT: scan de log por regex de e-mail; todos os caminhos de log cobertos |
| PBTs (propriedades) | PBT-01..05 verdes com ≥ 500 exemplos cada | FsCheck/CsCheck com geradores tipados (design seção 13.1) |

Notas:
- Coverage gate não substitui qualidade de teste.
- Caminhos críticos de `Infrastructure.Tests` (bounce, supressão, circuit breaker, Secret Manager falhando) são obrigatórios mesmo que não contribuam para o percentual geral.
- PBTs são contabilizados separadamente e obrigatórios; falha em qualquer PBT bloqueia encerramento da Onda 5.

## 7. Critérios de Encerramento

### Encerramento de TASK

Uma TASK só pode ser marcada `[X]` quando:

- todas as subtasks `[X]`
- testes aplicáveis verdes
- coverage gate da camada atendido ou justificativa registrada
- `dotnet build` sem warnings novos
- `dotnet format` executado
- commit em Conventional Commits realizado
- push da branch realizado
- documentação atualizada quando aplicável

### Encerramento de Onda

Uma onda só pode ser considerada concluída quando:

- todas as TASKs da onda `[X]`
- CI verde (build + test + lint + architecture tests)
- PR da onda aberto e aprovado conforme regra do projeto
- riscos da onda tratados ou registrados no arquivo de tasks

### Encerramento do Módulo

O módulo só pode ser considerado pronto quando:

- todas as 5 ondas concluídas
- PBT-01..05 verdes com ≥ 500 exemplos cada
- Architecture.Tests verdes (regra de dependência + proibição de SDK de provedor)
- coverage gates atendidos: Contracts ≥ 90%, Application ≥ 85%, Infrastructure ≥ 70%
- PBT-03 confirma que nenhum log ou métrica contém PII
- `gitleaks` verde (nenhuma credencial no repositório)
- `EmailProviderHealthCheck` integrado ao readiness do deployable consumidor
- 7 métricas `email_send_*` emitidas e 4 alertas configurados
- DoD checklist do design seção 19 preenchido
- gates de go-live registrados como pendências operacionais: SPF/DKIM/DMARC validados, DPA assinado, ADR-0005 publicada
- README do módulo sincronizado com versão, ondas e status dos artefatos

## 8. Riscos de Execução

| Código | Risco | Onda afetada | Mitigação |
|--------|-------|--------------|-----------|
| RISK-EXEC-01 | Spike de ADR-0005 não concluído antes da Onda 4 impede validar deduplicação nativa do provedor | Onda 4 — TASK-12/13 | Implementar propagação de `IdempotencyKey` via `Message-ID` como melhor esforço; documentar limitação; liberar TASKs com nota de pendência até ADR-0005 ser publicada |
| RISK-EXEC-02 | SDK de provedor (Postmark/SendGrid) evolui e quebra mapeamento ACL | Onda 4, Onda 5 | `ProviderResponseMapper` com fallback `NOTIF-ERR-090`; PBT-04 detecta regressão em nova versão de SDK automaticamente |
| RISK-EXEC-03 | GCP Secret Manager indisponível no ambiente de CI | Onda 4 — TASK-14 | Usar `ISecretProvider` fake injetável no CI; a dependência real do Secret Manager nunca deve estar em testes unitários |
| RISK-EXEC-04 | Quebra de API entre Polly v7 e v8 no ambiente .NET alvo | Onda 3 — TASK-10 | Fixar versão de Polly no `Directory.Packages.props` antes de iniciar TASK-10; documentar versão escolhida |
| RISK-EXEC-05 | PBT-03 falha por e-mail logado em caminho de exceção não coberto | Onda 5 — TASK-19 | Revisar todos os `catch` e caminhos de erro em `ResilientEmailSender` antes de rodar PBT-03; adicionar testes de caminho de exceção na Onda 3 |

## 9. Referências

| Referência | Caminho |
|------------|---------|
| requirements.md v0.1.0 | `docs/product/modules/notification-delivery/requirements.md` |
| design.md v0.1.0 | `docs/product/modules/notification-delivery/design.md` |
| README do módulo | `docs/product/modules/notification-delivery/README.md` |
| Rules de arquitetura | `.forge/rules/architecture/` |
| Rules de teste | `.forge/rules/testing/` |
| Rules de convenções | `.forge/rules/conventions/` |
| TRD §4.4, §7.3, §11.1..3, §12.6, §14.5, §17 | `docs/product/trd/trd.md` |
| ADR-0005 (a formalizar após spike) | `docs/product/trd/trd.md §21` |
| ADR-0007 (stack de observabilidade GCP — a formalizar) | `docs/product/trd/trd.md §21` |
