# Tasks — DIG — Digest

- Versão: 0.1.1
- Data: 2026-06-14
- Status: Aprovado para desenvolvimento
- Referência base requirements: docs/product/modules/digest/requirements.md v0.1.0
- Referência base design: docs/product/modules/digest/design.md v0.1.0
- ADRs aplicáveis: ADR-0001 (isolamento multi-tenant — Aceito), ADR-0004 (outbox/idempotência — a formalizar), ADR-0005 (IEmailSender — a formalizar), ADR-0006 (token de link autenticado — a formalizar), ADR-0008 (scheduling por fuso IANA — a formalizar)
- Rules aplicáveis: `.forge/rules/architecture/clean-architecture.md`, `.forge/rules/architecture/ddd.md`, `.forge/rules/architecture/api-and-contracts.md`, `.forge/rules/architecture/security-and-secrets.md`, `.forge/rules/architecture/security-and-compliance.md`, `.forge/rules/architecture/observability.md`, `.forge/rules/domain/money-as-cents.md`, `.forge/rules/domain/audit-immutability.md`, `.forge/rules/conventions/language-policy.md`, `.forge/rules/conventions/naming.md`, `.forge/rules/conventions/conventional-commits.md`, `.forge/rules/conventions/git-worktree.md`, `.forge/rules/testing/tdd.md`, `.forge/rules/testing/quality-gates.md`

## Histórico de Versões

| Versão | Data | Status | Descrição da alteração |
|--------|------|--------|------------------------|
| 0.1.0 | 2026-06-11 | Rascunho para revisão | Criação inicial do plano de tasks a partir de requirements.md v0.1.0 e design.md v0.1.0 (Req 1..11, RNF 1..10, PBT-01..06, DD-001..012) — 27 TASKs em 6 ondas |
| 0.1.1 | 2026-06-14 | Aprovado para desenvolvimento | Aprovação humana (HITL #1); execução via `/forge:coding-loop` autônomo (6 ondas). |

## 1. Convenções de Implementação

### 1.1 TDD-first

Toda implementação com lógica verificável deve seguir o ciclo:

1. Red — escrever teste que falha
2. Green — implementar o mínimo para passar
3. Refactor — melhorar sem alterar comportamento

Nenhuma implementação de objeto de valor, aggregate, policy, handler, repositório, adaptador de porta, endpoint ou mecanismo de segurança é considerada concluída sem teste correspondente.

### 1.2 Property-Based Testing

PBT é obrigatório para os seis invariantes identificados no requirements.md e rastreados no design.md §13.1:

- PBT-01 — Seleção por fuso IANA neutra a DST → TASK-03 (`TenantEligibility`)
- PBT-02 — Idempotência do envio sob N retentativas → TASK-12 (`EmailDigestLog` reserva + UNIQUE)
- PBT-03 — Regra de inclusão de destinatário (máquina de decisão completa) → TASK-07 (`RecipientSelectionPolicy`)
- PBT-04 — Omissão graciosa do bloco de metas → TASK-11 (`AzimuteSectionBuilder`)
- PBT-05 — Token não previsível, expirável e anti-enumerável → TASK-16 (`ActionTokenFactory`)
- PBT-06 — Azimute presente apenas na segunda-feira com papel de gestão → TASK-11 (`DigestContentComposer`)

Cada PBT usa geradores de entrada arbitrária (FsCheck ou Hedgehog) conforme design §13.1.

### 1.3 Bite-sized Tasks

Cada subtask deve ser estimada para menos de 2 horas. Cada TASK cabe em no máximo 1 dia de trabalho. TASK com escopo maior deve ser dividida antes de iniciar.

### 1.4 Branch Model

```text
<tipo>/digest/<NN>-<slug>
```

Exemplos:

```text
feat/digest/01-bootstrap-solution
test/digest/02-architecture-tests
feat/digest/03-tenant-eligibility
```

### 1.5 Git Worktree

```sh
git worktree add ../worktrees/digest/<NN>-<slug> -b <branch>
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
- CI verde
- conflitos resolvidos
- PR da onda aberto ou atualizado
- checklist de revisão preenchido
- README do módulo sincronizado

### 1.8 Early Exit

Se uma subtask falhar:

- marcar subtask como `[-]` e TASK como `[-]`
- registrar ponto de falha, comando executado e erro principal
- não mascarar falha com implementação especulativa
- deixar contexto suficiente para retomada por outro agente ou desenvolvedor

### 1.9 Convenção de Status

- `[ ]` Não iniciado
- `[-]` Em progresso
- `[X]` Concluído
- `[!]` Falhou — exige intervenção humana

### 1.10 Convenção canônica de IDs

```text
TASK-NN — <título>        ← unidade atômica de invocação do task-coder
  ST-MM — <subtask>       ← etapas TDD internas (Red/Green/Refactor/Encerramento)
```

Onda é atributo da TASK (campo `**Onda**` no header) e seção de agrupamento visual — nunca entra no ID da TASK.

## 2. Status Geral

| TASK | Título | Onda | Branch | Status |
|------|--------|------|--------|--------|
| TASK-01 | Criar solution e projetos Clean Architecture | Onda 1 | `feat/digest/01-bootstrap-solution` | [X] |
| TASK-02 | Testes de arquitetura e CI mínimo | Onda 1 | `test/digest/02-architecture-tests` | [X] |
| TASK-03 | TenantEligibility e DigestDate — PBT-01 | Onda 2 | `feat/digest/03-tenant-eligibility` | [X] |
| TASK-04 | MoneyCents, ActionToken, DigestSection e DigestContent | Onda 2 | `feat/digest/04-value-objects` | [X] |
| TASK-05 | EmailDigestLog — entidade e state machine | Onda 2 | `feat/digest/05-email-digest-log` | [X] |
| TASK-06 | DigestActionToken — entidade e ciclo de vida | Onda 2 | `feat/digest/06-digest-action-token` | [X] |
| TASK-07 | RecipientSelectionPolicy — PBT-03 | Onda 2 | `feat/digest/07-recipient-selection-policy` | [X] |
| TASK-08 | DigestJob (aggregate root) e DigestEmailSent | Onda 2 | `feat/digest/08-digest-job-aggregate` | [X] |
| TASK-09 | Portas de leitura — 6 interfaces de Application | Onda 3 | `feat/digest/09-read-ports` | [X] |
| TASK-10 | SelectEligibleTenantsQuery e SelectRecipientsQuery | Onda 3 | `feat/digest/10-selection-queries` | [X] |
| TASK-11 | DigestContentComposer e AzimuteSectionBuilder — PBT-04 e PBT-06 | Onda 3 | `feat/digest/11-content-composer` | [X] |
| TASK-12 | SendUserDigestHandler e idempotência — PBT-02 | Onda 3 | `feat/digest/12-send-user-digest-handler` | [X] |
| TASK-13 | RunDigestForTenantHandler, UpdateDeliveryStatusHandler e pipeline behaviors | Onda 3 | `feat/digest/13-orchestration-handlers` | [X] |
| TASK-14 | DbContext, Global Query Filter e migrations SQL | Onda 4 | `feat/digest/14-dbcontext-migrations` | [X] |
| TASK-15 | RLS falha-fechada e TenantConnectionInterceptor | Onda 4 | `feat/digest/15-rls-tenant-interceptor` | [X] |
| TASK-16 | ActionTokenFactory — PBT-05 | Onda 4 | `feat/digest/16-action-token-factory` | [X] |
| TASK-17 | EmailDigestLogRepository e DigestActionTokenRepository | Onda 4 | `feat/digest/17-repositories` | [X] |
| TASK-18 | OutboxPublisher — DigestEmailSent para digest.email_sent.v1 | Onda 4 | `feat/digest/18-outbox-publisher` | [X] |
| TASK-19 | Adaptadores das portas de leitura com resiliência Polly | Onda 4 | `feat/digest/19-read-port-adapters` | [X] |
| TASK-20 | EmailTemplateRenderer — HTML e branding do tenant | Onda 4 | `feat/digest/20-email-template-renderer` | [X] |
| TASK-21 | DigestTriggerEndpoint OIDC/WIF e health checks | Onda 5 | `feat/digest/21-trigger-endpoint` | [X] |
| TASK-22 | PerTenantConsumer Pub/Sub e fan-out | Onda 5 | `feat/digest/22-per-tenant-consumer` | [X] |
| TASK-23 | Contratos — DTOs, evento digest.email_sent.v1 e enums | Onda 5 | `feat/digest/23-contracts` | [X] |
| TASK-24 | Purge de email_digest_logs (90 dias) e digest_action_tokens | Onda 6 | `feat/digest/24-purge-jobs` | [X] |
| TASK-25 | Gate de isolamento CI cross-tenant | Onda 6 | `test/digest/25-cross-tenant-isolation-gate` | [X] |
| TASK-26 | Observabilidade completa — métricas, traces, alertas e logs sem PII | Onda 6 | `feat/digest/26-observability` | [X] |
| TASK-27 | Documentação final e DoD | Onda 6 | `docs/digest/27-dod-final` | [X] |

## 3. Ondas de Implementação

| Onda | Foco | TASKs |
|------|------|-------|
| Onda 1 | Bootstrap — solution, projetos .NET 10 e testes de arquitetura | TASK-01..TASK-02 |
| Onda 2 | Domínio — aggregates, entidades, objetos de valor, policy e domain event | TASK-03..TASK-08 |
| Onda 3 | Application — handlers, queries, composers e pipeline behaviors | TASK-09..TASK-13 |
| Onda 4 | Infrastructure — persistência, RLS, factory, repositórios, outbox, adaptadores e template | TASK-14..TASK-20 |
| Onda 5 | Api e Contratos — trigger OIDC/WIF, consumer Pub/Sub e contratos | TASK-21..TASK-23 |
| Onda 6 | Hardening — purge, isolamento CI, observabilidade e DoD final | TASK-24..TASK-27 |

## 4. Tarefas

### TASK-01 — Criar solution e projetos Clean Architecture

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 1 — Bootstrap |
| **Branch** | `feat/digest/01-bootstrap-solution` |
| **Worktree** | `git worktree add ../worktrees/digest/01-bootstrap-solution -b feat/digest/01-bootstrap-solution` |
| **Status** | [ ] |
| **Depende de** | Não aplicável |
| **Entregável** | `azim-digest-worker` compilável com 10 projetos; pacotes NuGet instalados; `dotnet build` verde sem warnings |
| **Mapeia** | design §3 (estrutura da solução), design §3.1 (regra de dependência), RNF 8, DD-012 (NodaTime) |
| **Camada principal** | DevOps |

#### Objetivo

Criar a solution `azim-digest-worker` com os projetos da Clean Architecture conforme design §3: `Digest.Domain`, `Digest.Application`, `Digest.Infrastructure`, `Digest.Api`, `Digest.Contracts` e cinco projetos de teste (`Digest.Domain.Tests`, `Digest.Application.Tests`, `Digest.Infrastructure.Tests`, `Digest.Api.Tests`, `Digest.Architecture.Tests`). Instalar pacotes NuGet base em cada camada correta: MediatR, EF Core, NodaTime (Infrastructure), FluentValidation, FsCheck, xUnit, NetArchTest (Architecture.Tests), Polly, Testcontainers.

#### Subtasks

- [ ] **ST-01 — Red:** verificar que `dotnet build` falha por ausência da solution.
- [ ] **ST-02 — Green:** criar `Digest.sln` e os 10 projetos .NET 10; adicionar referências entre camadas conforme §3.1; instalar pacotes NuGet base em cada projeto.
- [ ] **ST-03 — Refactor:** validar namespaces, referências e ausência de dependências circulares.
- [ ] **ST-04 — Encerramento:** `dotnet build` verde sem warnings; commit `chore(digest): scaffold clean-architecture solution`; push.

#### Critérios de Aceite

- [ ] 10 projetos criados com referências conforme design §3.1 (Domain → ∅, Application → Domain+Contracts, Infrastructure → Application+Domain, Api → Application+Infrastructure+Contracts, Contracts → ∅).
- [ ] Pacotes NuGet instalados nos projetos corretos (NodaTime em Infrastructure; NetArchTest em Architecture.Tests).
- [ ] `dotnet build` sem erros nem warnings novos.

---

### TASK-02 — Testes de arquitetura e CI mínimo

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 1 — Bootstrap |
| **Branch** | `test/digest/02-architecture-tests` |
| **Worktree** | `git worktree add ../worktrees/digest/02-architecture-tests -b test/digest/02-architecture-tests` |
| **Status** | [ ] |
| **Depende de** | TASK-01 |
| **Entregável** | `Digest.Architecture.Tests` com testes verdes impondo regra de dependência; pipeline CI com build + test |
| **Mapeia** | design §3.1, Req 6.2 (sem escrita em schema alheio), RNF 8 |
| **Camada principal** | Tests |

#### Objetivo

Implementar testes de arquitetura em `Digest.Architecture.Tests` usando NetArchTest: (a) regra de dependência Clean Architecture; (b) proibição de `Digest.Domain` referenciar EF Core, HTTP client ou Pub/Sub SDK; (c) proibição de mapeamento de tabelas de outros BCs (`opportunities`, `activities`, `goals`, `users`) em qualquer `DbSet` do módulo. Adicionar pipeline CI mínima (GitHub Actions: build + test em PR).

#### Subtasks

- [ ] **ST-01 — Red:** criar `ArchitectureTests.cs` com testes que falham — assemblies sem restrição de dependência ainda.
- [ ] **ST-02 — Green:** implementar testes NetArchTest para regras (a), (b) e (c); corrigir referências de projeto para que todos passem.
- [ ] **ST-03 — Refactor:** organizar testes por categoria; extrair constantes de nome de assembly.
- [ ] **ST-04 — Docs:** registrar as regras enforçadas no README do módulo.
- [ ] **ST-05 — Encerramento:** `dotnet test Digest.Architecture.Tests` verde; CI pipeline configurada; commit `test(digest): architecture dependency rules and CI pipeline`; push.

#### Critérios de Aceite

- [ ] Regra de dependência das 5 camadas coberta por teste automatizado.
- [ ] `Digest.Domain` não pode referenciar EF Core, HTTP ou Pub/Sub SDK (falha em build se violado).
- [ ] Mapeamento de tabelas alheias no DbContext do digest é detectado e falha o teste.
- [ ] CI executa `dotnet build` e `dotnet test` em todo pull request.

---

### TASK-03 — TenantEligibility e DigestDate — PBT-01

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domínio |
| **Branch** | `feat/digest/03-tenant-eligibility` |
| **Worktree** | `git worktree add ../worktrees/digest/03-tenant-eligibility -b feat/digest/03-tenant-eligibility` |
| **Status** | [ ] |
| **Depende de** | TASK-01 |
| **Entregável** | `TenantEligibility` e `DigestDate` com PBT-01 verde em `Digest.Domain.Tests` |
| **Mapeia** | Req 2 (Req 2.1–2.6), PBT-01, DD-012 (NodaTime), ADR-0008 |
| **Camada principal** | Domain |

#### Objetivo

Implementar `TenantEligibility` (objeto de valor imutável) e `DigestDate` (data local do tenant — não UTC). `TenantEligibility` encapsula a regra de elegibilidade a partir de `(utc_trigger_hour, iana_timezone, digest_time, active)`, expondo `IsEligible()`, `LocalDate` e `LocalWeekday`. A conversão UTC→local usa NodaTime com TZDB IANA embutido, neutralizando transições de horário de verão (DST). O `IDateTimeZoneProvider` é injetável. PBT-01: para qualquer fuso IANA, digest_time e hora UTC gerados (incluindo datas em transição DST), `IsEligible()` respeita exatamente o critério de Req 2.

#### Subtasks

- [ ] **ST-01 — Red:** escrever teste unitário e PBT-01 que falham; geradores FsCheck para fusos IANA válidos, digest_time arbitrário e datas incluindo bordas DST.
- [ ] **ST-02 — Green:** implementar `TenantEligibility` e `DigestDate` usando NodaTime; `IsEligible()` compara `LocalTime` da conversão com `digest_time` e verifica `DayOfWeek` Seg–Sex; tenant inativo sempre falso.
- [ ] **ST-03 — Refactor:** garantir imutabilidade; encapsular `IDateTimeZoneProvider`; adicionar teste nominal `America/Sao_Paulo` 10:00 UTC → 07:00 local (elegível, Req 2.4).
- [ ] **ST-04 — Encerramento:** `dotnet test Digest.Domain.Tests` verde (unit + PBT-01); coverage Domain ≥ 95%; commit `feat(digest): TenantEligibility VO with IANA DST-neutral eligibility`; push.

#### Critérios de Aceite

- [ ] PBT-01 verde: `IsEligible()` retorna verdadeiro se, e somente se, hora local = `digest_time` e dia local é Seg–Sex.
- [ ] Tenant inativo (`active = false`) nunca é elegível.
- [ ] Sábado e domingo no fuso do tenant não geram elegibilidade.
- [ ] Sem dependência de `TimeZoneInfo` do SO; NodaTime com TZDB embutido (DD-012).
- [ ] Coverage `Digest.Domain.Tests` ≥ 95%.

---

### TASK-04 — MoneyCents, ActionToken, DigestSection e DigestContent

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domínio |
| **Branch** | `feat/digest/04-value-objects` |
| **Worktree** | `git worktree add ../worktrees/digest/04-value-objects -b feat/digest/04-value-objects` |
| **Status** | [ ] |
| **Depende de** | TASK-01 |
| **Entregável** | Quatro objetos de valor em `Digest.Domain` com testes unitários verdes |
| **Mapeia** | Req 5.5 (money em centavos), Req 4.6 (ao menos um bloco), DD-007 (token_hash), DD-010 (MoneyCents) |
| **Camada principal** | Domain |

#### Objetivo

Implementar os quatro objetos de valor conforme design §4.3: `MoneyCents` (valor monetário como `long` em centavos BRL; proíbe construção de `double`/`float`); `ActionToken` (par token em claro + `token_hash` SHA-256; igualdade por hash; o claro nunca é persistido — DD-007); `DigestSection` (bloco canônico de conteúdo, imutável, expõe `IsEmpty()`); `DigestContent` (coleção de `DigestSection`s por usuário; `HasContent()` retorna verdadeiro quando ao menos um bloco não estiver vazio — Req 4.6).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes que falham: `MoneyCents` rejeita `double`; `ActionToken.Equals` por hash; `DigestContent.HasContent()` falso quando vazio, verdadeiro com ao menos um bloco.
- [ ] **ST-02 — Green:** implementar os quatro VOs; `MoneyCents` usa `long`; `ActionToken` calcula SHA-256 internamente sobre o token em claro; `DigestSection` e `DigestContent` imutáveis.
- [ ] **ST-03 — Refactor:** extrair lógica de hash para método privado; garantir que nenhum VO expõe `double`/`float`.
- [ ] **ST-04 — Encerramento:** `dotnet test Digest.Domain.Tests` verde; commit `feat(digest): domain value objects (MoneyCents, ActionToken, DigestSection, DigestContent)`; push.

#### Critérios de Aceite

- [ ] `MoneyCents` não aceita construção via `double` ou `float` (exceção em construtor/factory).
- [ ] `ActionToken.Equals` compara por `token_hash`; token em claro acessível somente no momento de criação.
- [ ] `DigestContent.HasContent()` retorna verdadeiro com ao menos um `DigestSection` não vazio.
- [ ] Nenhum VO referencia EF Core, HTTP ou qualquer framework de infraestrutura.

---

### TASK-05 — EmailDigestLog — entidade e state machine

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domínio |
| **Branch** | `feat/digest/05-email-digest-log` |
| **Worktree** | `git worktree add ../worktrees/digest/05-email-digest-log -b feat/digest/05-email-digest-log` |
| **Status** | [ ] |
| **Depende de** | TASK-04 |
| **Entregável** | Entidade `EmailDigestLog` com state machine completa e testes unitários verdes |
| **Mapeia** | Req 8 (registro de envio), Req 9 (idempotência), RNF 2, DD-008 (reserva antes do envio), design §4.5 |
| **Camada principal** | Domain |

#### Objetivo

Implementar a entidade `EmailDigestLog` conforme design §4.2 e §4.5. A entidade tem identidade `id` (UUID) e chave de idempotência `(tenant_id, user_id, digest_date)`. A state machine é: `scheduled → sent → delivered → opened`; `scheduled → failed`; `sent → bounced`. Terminais: `bounced`, `failed`. Transições inválidas lançam exceção de domínio. O status `scheduled` é a reserva de idempotência inserida antes de chamar `IEmailSender` (DD-008). `digest_date` é data local do tenant (não UTC — Req 8.3).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de estado: transição inválida lança exceção; `scheduled → sent` persiste `sent_at`; `failed` é terminal.
- [ ] **ST-02 — Green:** implementar `EmailDigestLog` com estado encapsulado; transições via métodos explícitos (`MarkSent`, `MarkFailed`, `MarkDelivered`, `MarkOpened`, `MarkBounced`).
- [ ] **ST-03 — Refactor:** extrair enum `DigestStatus`; garantir que `digest_date` é do tipo `DigestDate` (VO de TASK-04, ou `DateOnly` com semântica local).
- [ ] **ST-04 — Encerramento:** testes verdes; commit `feat(digest): EmailDigestLog entity with status state machine`; push.

#### Critérios de Aceite

- [ ] Todas as transições válidas cobertas por teste; todas as transições inválidas geram exceção.
- [ ] `digest_date` representa data local do tenant; nenhum campo armazena hora UTC como date (Req 8.3).
- [ ] Terminais `bounced` e `failed` não aceitam novas transições.
- [ ] A entidade não contém lógica de persistência nem referência a EF Core.

---

### TASK-06 — DigestActionToken — entidade e ciclo de vida

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domínio |
| **Branch** | `feat/digest/06-digest-action-token` |
| **Worktree** | `git worktree add ../worktrees/digest/06-digest-action-token -b feat/digest/06-digest-action-token` |
| **Status** | [ ] |
| **Depende de** | TASK-04 |
| **Entregável** | Entidade `DigestActionToken` com ciclo de vida modelado e testes unitários verdes |
| **Mapeia** | Req 7 (token de ação), RNF 7 (segurança), DD-004 (expires_at 48h), DD-007 (token_hash), ADR-0006 |
| **Camada principal** | Domain |

#### Objetivo

Implementar a entidade `DigestActionToken` conforme design §4.2 e §4.5. Campos: `id`, `tenant_id`, `user_id`, `activity_id`, `action` (complete | reschedule), `token_hash` (SHA-256 do token opaco — somente o hash é persistido; o claro nunca é armazenado), `expires_at` (= emissão + 48h, DD-004), `used_at` (setado pelo activity-management, fora deste escopo). O token em claro só existe durante a emissão via `ActionToken` VO (TASK-04). Ciclo de vida modelado no domínio: `issued → expired` (purge pós-expiração); `issued → used` (fora deste worker).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes: token com `expires_at` no passado é inválido; `action` fora de `complete`/`reschedule` lança exceção; `tenant_id` é obrigatório.
- [ ] **ST-02 — Green:** implementar `DigestActionToken`; construtor aceita `ActionToken` VO e extrai o hash; `expires_at` calculado como emissão + 48h.
- [ ] **ST-03 — Refactor:** extrair enum `ActionType`; garantir que o token em claro nunca é exposto após construção da entidade.
- [ ] **ST-04 — Encerramento:** testes verdes; commit `feat(digest): DigestActionToken entity with 48h expiry and hash-only persistence`; push.

#### Critérios de Aceite

- [ ] `token_hash` é o único campo relacionado ao token armazenado na entidade; o claro não é acessível após construção.
- [ ] `expires_at` = `created_at` + 48h (DD-004).
- [ ] `action` só aceita `complete` ou `reschedule`.
- [ ] Entidade sem dependência de infraestrutura.

---

### TASK-07 — RecipientSelectionPolicy — PBT-03

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domínio |
| **Branch** | `feat/digest/07-recipient-selection-policy` |
| **Worktree** | `git worktree add ../worktrees/digest/07-recipient-selection-policy -b feat/digest/07-recipient-selection-policy` |
| **Status** | [ ] |
| **Depende de** | TASK-03 |
| **Entregável** | `RecipientSelectionPolicy` total (cobre todo o espaço de entrada) com PBT-03 verde |
| **Mapeia** | Req 3 (Req 3.1–3.7), Req 10, PBT-03, RN-011, RN-029 |
| **Camada principal** | Domain |

#### Objetivo

Implementar `RecipientSelectionPolicy` (domain policy) conforme design §4.6. A política é total: dado `(papel, hasOwnPendencias, optOut, weekday, active)`, decide exatamente se o usuário recebe o digest de pendências e/ou o azimute. Regras: usuário inativo ou `Viewer` nunca recebe; tem pendência própria e sem opt-out → recebe pendências; segunda-feira e papel `GestorBU`/`TAdmin` → recebe azimute mesmo sem pendências (opt-out não suprime azimute — Req 10.2); sem pendência e sem papel de gestão → nunca recebe. O recorte do Vendedor é restrito a `owner_id = user_id` (Req 3.4). PBT-03 verifica totalidade com geradores arbitrários.

#### Subtasks

- [ ] **ST-01 — Red:** escrever PBT-03 com gerador de `(papel, hasOwnPendencias, optOut, weekday, active)` arbitrários; definir a função esperada; rodar e falhar.
- [ ] **ST-02 — Green:** implementar `RecipientSelectionPolicy.Evaluate()` com as cinco ramificações conforme design §4.6; garantir que `Viewer` e inativo sempre retornam false.
- [ ] **ST-03 — Refactor:** extrair enum `RecipientPapel`; adicionar testes nominais para cada aresta (segunda com `TAdmin` sem pendências, opt-out com azimute etc.).
- [ ] **ST-04 — Encerramento:** PBT-03 + testes unitários verdes; commit `feat(digest): RecipientSelectionPolicy with total coverage and PBT-03`; push.

#### Critérios de Aceite

- [ ] PBT-03 verde: a policy é total — nenhuma combinação de entrada produz resultado indefinido.
- [ ] `Viewer` e usuário inativo nunca recebem (Req 3.5, 3.7).
- [ ] Opt-out suprime pendências mas não o azimute de segunda para `GestorBU`/`TAdmin` (Req 10.2).
- [ ] Sem dependência de infraestrutura ou persistência.

---

### TASK-08 — DigestJob (aggregate root) e DigestEmailSent

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domínio |
| **Branch** | `feat/digest/08-digest-job-aggregate` |
| **Worktree** | `git worktree add ../worktrees/digest/08-digest-job-aggregate -b feat/digest/08-digest-job-aggregate` |
| **Status** | [ ] |
| **Depende de** | TASK-05, TASK-06, TASK-07 |
| **Entregável** | `DigestJob` aggregate root efêmero + `DigestEmailSent` domain event com testes de invariante verdes |
| **Mapeia** | Req 4 (Req 4.6), Req 5 (Req 5.7), Req 11 (RNF 10.1), RNF 10, DD-009, design §4.1 |
| **Camada principal** | Domain |

#### Objetivo

Implementar `DigestJob` (aggregate root efêmero, design §4.1): opera sobre um único `(tenant_id, digest_date)`; coordena a lista de destinatários selecionados; impõe que o bloco de azimute só integra o conteúdo quando `digest_date` é segunda-feira e o papel é `GestorBU` ou `TAdmin` (PBT-06 raiz); registra exatamente um `DigestEmailSent` por envio bem-sucedido (RNF 10.1). Implementar o domain event `DigestEmailSent` com campos `tenant_id`, `user_id`, `digest_date`, `message_id`, `occurred_at` — sem PII (RNF 3.4).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de invariante: `DigestJob` com `tenant_id` nulo lança exceção; azimute em terça não é incluído; dois envios bem-sucedidos para o mesmo usuário geram apenas um `DigestEmailSent`.
- [ ] **ST-02 — Green:** implementar `DigestJob` e `DigestEmailSent`; métodos `RegisterSent` registra o event; `AddRecipient` valida papel e dia da semana.
- [ ] **ST-03 — Refactor:** garantir que `DigestEmailSent` não contém e-mail, nome nem conteúdo do digest.
- [ ] **ST-04 — Encerramento:** testes de invariante verdes; commit `feat(digest): DigestJob aggregate root and DigestEmailSent domain event`; push.

#### Critérios de Aceite

- [ ] `DigestJob` opera exatamente sobre um `tenant_id` e um `digest_date`.
- [ ] Azimute só é incluído no `DigestJob` quando `digest_date` é segunda-feira e papel é de gestão (Req 5.7).
- [ ] `DigestEmailSent` não contém PII — apenas `tenant_id`, `user_id`, `digest_date`, `message_id`, `occurred_at` (RNF 3.4).
- [ ] Exatamente um evento `DigestEmailSent` por usuário com envio bem-sucedido (RNF 10.1).

---

### TASK-09 — Portas de leitura — 6 interfaces de Application

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/digest/09-read-ports` |
| **Worktree** | `git worktree add ../worktrees/digest/09-read-ports -b feat/digest/09-read-ports` |
| **Status** | [ ] |
| **Depende de** | TASK-08 |
| **Entregável** | 6 interfaces de Application (5 read ports + `IActionTokenFactory`) com stubs de teste e testes de contrato de interface verdes |
| **Mapeia** | Req 6 (Req 6.1–6.4), Req 7, Req 10, DD-001 (leitura direta), DD-003 (opt-out em organization) |
| **Camada principal** | Application |

#### Objetivo

Definir as 6 interfaces de porta em `Digest.Application` conforme design §6.4 e §3: `IActivityReadPort` (atividades vencidas e do dia por `owner_id`), `IOpportunityReadPort` (oportunidades estagnadas, fechamentos vencidos e pipeline ponderado), `IForecastReadPort` (bloco de metas em centavos — retorna `null` quando ausente), `IUserDirectoryPort` (usuários ativos, papéis, BU, fuso do tenant), `IUserDigestPreferencePort` (preferência de opt-out por usuário — dono: organization, DD-003), `IActionTokenFactory` (emissão de `ActionToken` por atividade). Todas somente leitura; restritas a `tenant_id`; retornam `null`/lista vazia para ausência de dados (nunca lançam exceção por dado faltante).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de contrato de interface: stubs que retornam coleções vazias/null; invocar cada interface e verificar que o caller trata ausência sem exceção.
- [ ] **ST-02 — Green:** definir as 6 interfaces em `Digest.Application`; implementar stubs `InMemory*` em `Digest.Application.Tests` para uso nos testes da Onda 3.
- [ ] **ST-03 — Refactor:** garantir que nenhuma interface retorna tipos EF Core ou modelos de infraestrutura — somente DTOs/modelos de Application.
- [ ] **ST-04 — Encerramento:** testes de contrato verdes; commit `feat(digest): application read ports and IActionTokenFactory interfaces`; push.

#### Critérios de Aceite

- [ ] 6 interfaces definidas em `Digest.Application`; nenhuma em `Digest.Infrastructure` ou `Digest.Domain`.
- [ ] Stubs in-memory implementados para uso em `Digest.Application.Tests`.
- [ ] Todas as interfaces restritas a `tenant_id` em cada método de leitura (Req 6.4).
- [ ] `IForecastReadPort` retorna `null` quando não há meta cadastrada (suporte à degradação graciosa — Req 5.4).

---

### TASK-10 — SelectEligibleTenantsQuery e SelectRecipientsQuery

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/digest/10-selection-queries` |
| **Worktree** | `git worktree add ../worktrees/digest/10-selection-queries -b feat/digest/10-selection-queries` |
| **Status** | [ ] |
| **Depende de** | TASK-03, TASK-07, TASK-09 |
| **Entregável** | Dois query handlers com testes unitários verdes usando stubs das portas |
| **Mapeia** | Req 1.4 (disparo único), Req 2 (seleção por fuso), Req 3 (seleção de destinatários), design §5.2 |
| **Camada principal** | Application |

#### Objetivo

Implementar `SelectEligibleTenantsQuery(reference_utc)` e seu handler: usa `IUserDirectoryPort` para obter lista de tenants ativos com `iana_timezone` e `digest_time`; instancia `TenantEligibility` para cada tenant; retorna os `tenant_id` elegíveis. Este handler roda **antes** de setar `app.current_tenant` (cross-tenant administrativo por natureza — design §5.2). Implementar `SelectRecipientsQuery(tenant_id, digest_date)` e seu handler: obtém usuários ativos via `IUserDirectoryPort`; pendências via `IActivityReadPort` e `IOpportunityReadPort`; opt-out via `IUserDigestPreferencePort`; aplica `RecipientSelectionPolicy` a cada candidato; retorna lista de `RecipientCandidate`.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes unitários que falham usando stubs — tenant inativo não aparece na lista de elegíveis; usuário sem pendências e sem papel de gestão não é `RecipientCandidate`.
- [ ] **ST-02 — Green:** implementar os dois handlers com MediatR; `SelectEligibleTenantsQuery` usa `TenantEligibility.IsEligible()`; `SelectRecipientsQuery` aplica `RecipientSelectionPolicy`.
- [ ] **ST-03 — Refactor:** testar segunda-feira com `TAdmin` sem pendências (deve ser selecionado); fuso `America/New_York` (UTC-5) elegível às 12:00 UTC.
- [ ] **ST-04 — Encerramento:** testes unitários verdes; commit `feat(digest): SelectEligibleTenantsQuery and SelectRecipientsQuery handlers`; push.

#### Critérios de Aceite

- [ ] Tenant inativo nunca aparece como elegível.
- [ ] `RecipientCandidate` de segunda com `GestorBU` sem pendências é selecionado (Req 3.3).
- [ ] `Viewer` e usuário inativo nunca são `RecipientCandidate` (Req 3.5, 3.7).
- [ ] Opt-out de segunda não suprime azimute para `GestorBU`/`TAdmin` (Req 10.2).

---

### TASK-11 — DigestContentComposer e AzimuteSectionBuilder — PBT-04 e PBT-06

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/digest/11-content-composer` |
| **Worktree** | `git worktree add ../worktrees/digest/11-content-composer -b feat/digest/11-content-composer` |
| **Status** | [ ] |
| **Depende de** | TASK-04, TASK-09, TASK-10 |
| **Entregável** | `DigestContentComposer` e `AzimuteSectionBuilder` com PBT-04 e PBT-06 verdes |
| **Mapeia** | Req 4 (Req 4.1–4.6), Req 5 (Req 5.1–5.7), PBT-04, PBT-06, RN-018, RN-029, DD-010 |
| **Camada principal** | Application |

#### Objetivo

Implementar `DigestContentComposer` (serviço de Application) que monta os blocos canônicos `overdue_activities`, `today_activities`, `stale_opportunities`, `overdue_closings` via portas de leitura; delega a `AzimuteSectionBuilder` na segunda-feira. Implementar `AzimuteSectionBuilder`: compõe `azimute_pipeline` (pipeline ponderado, variação em `MoneyCents`, status, ganhos, fechamentos) e `azimute_metas`; **omite completamente** o bloco de metas quando `IForecastReadPort` retorna `null` para o período — sem erro, sem placeholder (RN-018, Req 5.4, PBT-04). Todos os valores monetários via `MoneyCents` (DD-010). PBT-06: azimute presente se, e somente se, dia local = segunda e papel é de gestão.

#### Subtasks

- [ ] **ST-01 — Red:** escrever PBT-04 (gerador de presença/ausência de meta; azimute sempre composto sem erro) e PBT-06 (gerador de dia da semana e papel; azimute presença ⟺ segunda + gestão); ambos falham.
- [ ] **ST-02 — Green:** implementar `DigestContentComposer` e `AzimuteSectionBuilder`; `IForecastReadPort` retornando `null` → omite `azimute_metas` sem lançar exceção.
- [ ] **ST-03 — Refactor:** garantir que nenhum cálculo monetário usa `double`/`float`; testar terça com `TAdmin` — sem bloco de azimute.
- [ ] **ST-04 — Encerramento:** PBT-04 + PBT-06 + testes unitários verdes; coverage Application ≥ 85%; commit `feat(digest): DigestContentComposer and AzimuteSectionBuilder with PBT-04 and PBT-06`; push.

#### Critérios de Aceite

- [ ] PBT-04 verde: azimute composto e enviado sem erro/placeholder quando não há meta.
- [ ] PBT-06 verde: azimute presente ⟺ segunda-feira e papel `GestorBU`/`TAdmin`.
- [ ] Valores de pipeline/variação/realizado/meta em `MoneyCents` (nenhum `double`/`float` de domínio).
- [ ] `DigestContent.HasContent()` retorna verdadeiro quando ao menos um bloco está presente (Req 4.6).

---

### TASK-12 — SendUserDigestHandler e idempotência — PBT-02

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/digest/12-send-user-digest-handler` |
| **Worktree** | `git worktree add ../worktrees/digest/12-send-user-digest-handler -b feat/digest/12-send-user-digest-handler` |
| **Status** | [ ] |
| **Depende de** | TASK-09, TASK-11 |
| **Entregável** | `SendUserDigestHandler` com fluxo de idempotência e PBT-02 verde em `Digest.Application.Tests` |
| **Mapeia** | Req 8, Req 9 (Req 9.1–9.5), RNF 2, PBT-02, DD-008 (reserva antes do envio) |
| **Camada principal** | Application |

#### Objetivo

Implementar `SendUserDigestHandler` conforme design §5.3: (1) reserva idempotência inserindo status `scheduled` via `IEmailDigestLogRepository.ReserveAsync(ON CONFLICT DO NOTHING)`; se já existe registro `sent`/`delivered`/`opened`, retorna sem enviar (Req 9.2); (2) invoca `DigestContentComposer`; se sem bloco e usuário não é gestor em segunda, aborta; (3) delega emissão de tokens para `IActionTokenFactory`; (4) chama `IEmailSender.Send`; (5) atualiza log para `sent`/`failed` e enfileira `DigestEmailSent` no Outbox na mesma transação (DD-009). PBT-02: N disparos para o mesmo `(tenant_id, user_id, digest_date)` resultam em exatamente um envio efetivo.

#### Subtasks

- [ ] **ST-01 — Red:** escrever PBT-02 com stubs: N ≥ 1 chamadas ao handler para o mesmo usuário e data → `IEmailSender.Send` chamado exatamente uma vez; segundo `ReserveAsync` retorna conflito.
- [ ] **ST-02 — Green:** implementar `SendUserDigestHandler`; `ReserveAsync` retorna `ReservationResult.AlreadySent` → early return; caso contrário, prossegue com composição e envio.
- [ ] **ST-03 — Refactor:** testar cenário de falha do provedor → status `failed`; garantir que `IEmailSender` nunca é chamado após conflito de reserva (RNF 2.3).
- [ ] **ST-04 — Encerramento:** PBT-02 + testes unitários verdes; commit `feat(digest): SendUserDigestHandler with idempotency reserve pattern`; push.

#### Critérios de Aceite

- [ ] PBT-02 verde: N disparos → exatamente uma chamada ao `IEmailSender` por `(tenant_id, user_id, digest_date)`.
- [ ] Verificação de idempotência ocorre antes de invocar `IEmailSender` (RNF 2.3).
- [ ] Falha definitiva do provedor marca `status = failed` e emite alerta (RNF 5.2).
- [ ] Usuário sem bloco de conteúdo e sem papel de gestão em segunda não recebe e-mail.

---

### TASK-13 — RunDigestForTenantHandler, UpdateDeliveryStatusHandler e pipeline behaviors

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/digest/13-orchestration-handlers` |
| **Worktree** | `git worktree add ../worktrees/digest/13-orchestration-handlers -b feat/digest/13-orchestration-handlers` |
| **Status** | [ ] |
| **Depende de** | TASK-12 |
| **Entregável** | Dois handlers de orquestração e quatro pipeline behaviors com testes unitários verdes |
| **Mapeia** | Req 1 (Req 1.3), Req 8, Req 11 (Req 11.1), RNF 3, RNF 5.3, RNF 6.1, design §5.1–§5.4 |
| **Camada principal** | Application |

#### Objetivo

Implementar `RunDigestForTenantHandler`: seta contexto de tenant, executa `SelectRecipientsQuery`, dispara `SendUserDigestCommand` por usuário elegível; falha de um usuário não interrompe os demais (try/catch por usuário com log e métrica — RNF 5.3). Implementar `UpdateDeliveryStatusHandler`: mapeia eventos do provedor para a state machine de `EmailDigestLog`; idempotente por `message_id`. Implementar os quatro pipeline behaviors conforme design §5.4: `TenantScopeBehavior` (seta contexto + filtro global antes de qualquer command), `LoggingBehavior` (sem PII — RNF 3, RNF 6.1), `ValidationBehavior` (FluentValidation), `UnitOfWorkBehavior` (encerra a transação que abrange `EmailDigestLog` + Outbox).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes: falha de um usuário não propaga para o próximo; `UpdateDeliveryStatusHandler` idempotente para o mesmo `message_id`; `LoggingBehavior` não loga e-mail do usuário.
- [ ] **ST-02 — Green:** implementar os dois handlers e os quatro behaviors.
- [ ] **ST-03 — Refactor:** garantir que `LoggingBehavior` usa destructuring policy para mascarar campos PII; `TenantScopeBehavior` não passa por tenant sem `tenant_id` válido.
- [ ] **ST-04 — Encerramento:** testes unitários verdes; commit `feat(digest): orchestration handlers and pipeline behaviors`; push.

#### Critérios de Aceite

- [ ] Falha no `SendUserDigestCommand` de um usuário não interrompe os demais (Req RNF 5.3).
- [ ] `UpdateDeliveryStatusHandler` idempotente: mesmo `message_id` processado N vezes não duplica transição de estado.
- [ ] `LoggingBehavior` não inclui endereço de e-mail, conteúdo de digest ou títulos de atividades em nenhuma saída de log (RNF 3).
- [ ] `TenantScopeBehavior` é o primeiro behavior aplicado em comandos de tenant.

---

### TASK-14 — DbContext, Global Query Filter e migrations SQL

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/digest/14-dbcontext-migrations` |
| **Worktree** | `git worktree add ../worktrees/digest/14-dbcontext-migrations -b feat/digest/14-dbcontext-migrations` |
| **Status** | [ ] |
| **Depende de** | TASK-05, TASK-06 |
| **Entregável** | `DigestDbContext` com Global Query Filter; migrations EF Core que criam `email_digest_logs` e `digest_action_tokens` com UNIQUE, índices e scripts RLS idempotentes |
| **Mapeia** | design §6.1, design §7 (schema SQL completo), RNF 1, DD-002, ADR-0001 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar `DigestDbContext` em `Digest.Infrastructure`: `DbSet<EmailDigestLog>` e `DbSet<DigestActionToken>` com Global Query Filter por `tenant_id` em cada entidade (primeira camada de defesa em profundidade — ADR-0001). Gerar EF Core Migrations que criem as tabelas conforme schema do design §7: UNIQUE `(tenant_id, user_id, digest_date)` em `email_digest_logs`, UNIQUE `token_hash` em `digest_action_tokens`, índices `ix_email_digest_logs_tenant_date`, `ix_email_digest_logs_status` e `ix_digest_action_tokens_expires`. Adicionar script idempotente de RLS pós-migration (executa `ENABLE ROW LEVEL SECURITY`, `FORCE ROW LEVEL SECURITY` e políticas). O worker **não possui** `DbSet` de `opportunities`, `activities`, `goals` ou `users`.

#### Subtasks

- [ ] **ST-01 — Red:** escrever teste de integração (Testcontainers + Postgres real) que falha por ausência das tabelas.
- [ ] **ST-02 — Green:** criar `DigestDbContext`; configurar Global Query Filter; gerar migration; executar migration + script RLS no Testcontainer.
- [ ] **ST-03 — Refactor:** verificar que `Architecture.Tests` não detecta mapeamento de tabelas alheias; garantir CHECK constraint de `status` e `action`.
- [ ] **ST-04 — Encerramento:** teste de integração verde (tabelas criadas, constraints presentes); commit `feat(digest): DigestDbContext with global query filter and EF migrations`; push.

#### Critérios de Aceite

- [ ] UNIQUE `(tenant_id, user_id, digest_date)` presente em `email_digest_logs` (RN-010).
- [ ] UNIQUE `token_hash` presente em `digest_action_tokens`.
- [ ] Global Query Filter por `tenant_id` configurado em ambas as entidades.
- [ ] Script RLS executado após migration; políticas criadas para ambas as tabelas.
- [ ] Sem `DbSet` de tabelas de outros BCs.

---

### TASK-15 — RLS falha-fechada e TenantConnectionInterceptor

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/digest/15-rls-tenant-interceptor` |
| **Worktree** | `git worktree add ../worktrees/digest/15-rls-tenant-interceptor -b feat/digest/15-rls-tenant-interceptor` |
| **Status** | [ ] |
| **Depende de** | TASK-14 |
| **Entregável** | `TenantConnectionInterceptor` + teste de isolamento com Testcontainers verificando que RLS falha-fechada bloqueia acesso sem `SET app.current_tenant` |
| **Mapeia** | RNF 1 (Req RNF-1.1–1.4), DD-002, ADR-0001 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar `TenantConnectionInterceptor` (EF Core `DbConnectionInterceptor`): executa `SET app.current_tenant = @tenant_id` ao alugar a conexão do pool, antes de qualquer comando. Isso garante que a política RLS `tenant_id = current_setting('app.current_tenant')::uuid` seja satisfeita (DD-002). Escrever teste de integração com Testcontainers que verifica a defesa em profundidade: (a) com `SET app.current_tenant` correto, acessa somente dados do tenant; (b) **sem** `SET app.current_tenant`, o banco retorna zero linhas (RLS falha-fechada com `FORCE ROW LEVEL SECURITY`); (c) com `tenant_id` de outro tenant setado, não acessa dados do tenant A.

#### Subtasks

- [ ] **ST-01 — Red:** escrever teste de isolamento que falha: tenant A insere registro; conexão sem `SET app.current_tenant` tenta ler → espera-se 0 linhas; sem o interceptor, retorna dados.
- [ ] **ST-02 — Green:** implementar `TenantConnectionInterceptor`; registrar no DI; rodar teste com interceptor ativo.
- [ ] **ST-03 — Refactor:** adicionar cenário (c): tenant B setado → não vê dados do tenant A; adicionar asserção que FORCE RLS bloqueia o proprietário da tabela sem SET.
- [ ] **ST-04 — Encerramento:** teste de isolamento com Testcontainers verde (3 cenários); commit `feat(digest): TenantConnectionInterceptor and RLS fail-closed isolation test`; push.

#### Critérios de Aceite

- [ ] `SET app.current_tenant` executado em cada conexão alugada, antes do primeiro comando.
- [ ] Teste de isolamento verde: sem SET → 0 linhas (RLS bloqueia — RNF 1.4).
- [ ] Teste de cross-tenant: conexão com tenant B não retorna dados de tenant A.
- [ ] Teste registrado como gate de CI obrigatório (RNF 1.3 — deve ser green para merge).

---

### TASK-16 — ActionTokenFactory — PBT-05

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/digest/16-action-token-factory` |
| **Worktree** | `git worktree add ../worktrees/digest/16-action-token-factory -b feat/digest/16-action-token-factory` |
| **Status** | [ ] |
| **Depende de** | TASK-06, TASK-09 |
| **Entregável** | `ActionTokenFactory` com 256 bits de entropia e PBT-05 verde em `Digest.Infrastructure.Tests` |
| **Mapeia** | Req 7 (Req 7.1–7.5), RNF 7 (Req RNF-7.2–7.4), PBT-05, DD-004 (48h), DD-007 (token_hash), ADR-0006 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar `ActionTokenFactory` (classe de Infrastructure, implementa `IActionTokenFactory`) que gera tokens com 256 bits de entropia via CSPRNG (`RandomNumberGenerator.GetBytes(32)`). Para cada atividade, emite um `ActionToken` VO: token em claro (32 bytes em Base64Url) + `token_hash` (SHA-256 do claro). O claro nunca é persistido; apenas o hash vai para `digest_action_tokens`. `expires_at = now() + 48h` (DD-004). PBT-05: para qualquer conjunto de atividades gerado, os tokens emitidos são distintos, não sequenciais, não deriváveis uns dos outros, cada um vinculado a `(tenant_id, user_id, activity_id)` com `expires_at` no futuro; a emissão não permite inferir tokens de outros usuários ou tenants.

#### Subtasks

- [ ] **ST-01 — Red:** escrever PBT-05 com gerador de conjuntos de atividades arbitrários; verificar distinção, não derivabilidade e `expires_at` futuro; falha porque a factory não existe.
- [ ] **ST-02 — Green:** implementar `ActionTokenFactory`; `RandomNumberGenerator.GetBytes(32)` para entropia; `SHA256.HashData` para o hash; `expires_at = IClock.UtcNow + 48h`.
- [ ] **ST-03 — Refactor:** injetar `IClock` para controlar tempo em testes; testar que nenhum token repetido é gerado em 10.000 emissões (sanity check de entropia).
- [ ] **ST-04 — Encerramento:** PBT-05 + testes verdes; commit `feat(digest): ActionTokenFactory with 256-bit CSPRNG and PBT-05`; push.

#### Critérios de Aceite

- [ ] PBT-05 verde: tokens distintos, não sequenciais e não deriváveis para qualquer conjunto de atividades.
- [ ] `expires_at` sempre no futuro em relação ao momento de emissão.
- [ ] Token em claro nunca é logado, armazenado ou exposto após retorno do factory (RNF 3).
- [ ] Implementação usa CSPRNG (`RandomNumberGenerator`), não `Random`.

---

### TASK-17 — EmailDigestLogRepository e DigestActionTokenRepository

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/digest/17-repositories` |
| **Worktree** | `git worktree add ../worktrees/digest/17-repositories -b feat/digest/17-repositories` |
| **Status** | [ ] |
| **Depende de** | TASK-14, TASK-15 |
| **Entregável** | Dois repositórios com testes de integração (Testcontainers) verdes, incluindo o caminho de reserva de idempotência |
| **Mapeia** | Req 9 (Req 9.1–9.5), RNF 2, PBT-02 (infra), DD-008 (reserva ON CONFLICT DO NOTHING), design §6.1 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar `EmailDigestLogRepository`: métodos `ReserveAsync` (`INSERT ... status='scheduled' ON CONFLICT DO NOTHING`; retorna `ReservationResult.Reserved` ou `AlreadySent`), `MarkSentAsync`, `MarkFailedAsync`, `GetByKeyAsync(tenant_id, user_id, digest_date)`. Implementar `DigestActionTokenRepository`: `IssueTokenAsync` (persiste `token_hash` + `expires_at`), `GetByHashAsync`. Escrever testes de integração com Testcontainers verificando: UNIQUE vence a corrida entre duas chamadas concorrentes ao `ReserveAsync` (somente uma persiste), e PBT-02 infrastructure side (N `ReserveAsync` para a mesma chave → exatamente um registro `scheduled`).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de integração (Testcontainers + Postgres): duas chamadas concorrentes ao `ReserveAsync` → somente um registro na tabela.
- [ ] **ST-02 — Green:** implementar os dois repositórios; `ReserveAsync` usa `INSERT ... ON CONFLICT DO NOTHING` e retorna enum de resultado.
- [ ] **ST-03 — Refactor:** testar `MarkSentAsync` atualiza `sent_at` e `message_id`; `GetByHashAsync` retorna `null` para hash inexistente.
- [ ] **ST-04 — Encerramento:** testes de integração verdes; commit `feat(digest): EmailDigestLogRepository and DigestActionTokenRepository`; push.

#### Critérios de Aceite

- [ ] `ReserveAsync` com `ON CONFLICT DO NOTHING`: segunda chamada para a mesma chave retorna `AlreadySent` sem inserir novo registro.
- [ ] Teste de corrida concorrente verde (duas tasks concorrentes → 1 registro).
- [ ] `DigestActionTokenRepository.IssueTokenAsync` persiste apenas `token_hash` (nunca o claro).
- [ ] Todos os testes rodam com RLS ativa (interceptor configurado no Testcontainer).

---

### TASK-18 — OutboxPublisher — DigestEmailSent para digest.email_sent.v1

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/digest/18-outbox-publisher` |
| **Worktree** | `git worktree add ../worktrees/digest/18-outbox-publisher -b feat/digest/18-outbox-publisher` |
| **Status** | [ ] |
| **Depende de** | TASK-08, TASK-14 |
| **Entregável** | `OutboxPublisher` que grava `DigestEmailSent` em `outbox_messages` na mesma transação do `UPDATE sent`, com relay que publica `digest.email_sent.v1`; teste de atomicidade verde |
| **Mapeia** | Req 11 (Req 11.3), RNF 10 (Req RNF-10.1–10.3), DD-009, ADR-0004 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar `OutboxPublisher` (Infrastructure): na mesma transação do `UPDATE status='sent'` em `EmailDigestLog`, insere o evento `DigestEmailSent` serializado em `outbox_messages`. Um relay (background service ou job agendado) lê `outbox_messages` pendentes e publica no tópico Pub/Sub `azim-digest` como `digest.email_sent.v1`. O relay é idempotente por `event_id`. Sem PII no payload do evento: apenas `tenant_id`, `user_id`, `digest_date`, `message_id`, `correlation_id`, `occurred_at` (design §9.1). Garantir que retentativa idempotente não gera evento duplicado processado pelo consumidor (RNF 10.3).

#### Subtasks

- [ ] **ST-01 — Red:** escrever teste de atomicidade: mock `IEmailSender` retorna sucesso; transação faz `UPDATE sent` + `INSERT outbox_messages`; rollback total se a transação falhar após INSERT.
- [ ] **ST-02 — Green:** implementar `OutboxPublisher`; `UnitOfWorkBehavior` encerra a transação que contém ambas as operações; relay lê e publica usando Pub/Sub SDK.
- [ ] **ST-03 — Refactor:** verificar que o payload do evento não contém PII (RNF 10.2); relay com deduplicação por `event_id`.
- [ ] **ST-04 — Encerramento:** teste de atomicidade verde; commit `feat(digest): OutboxPublisher for DigestEmailSent → digest.email_sent.v1`; push.

#### Critérios de Aceite

- [ ] `DigestEmailSent` gravado em `outbox_messages` na mesma transação do `UPDATE sent` (DD-009).
- [ ] Rollback da transação remove tanto o `UPDATE` quanto o `INSERT` no outbox.
- [ ] Payload do evento sem PII: nenhum campo de e-mail ou conteúdo (RNF 10.2).
- [ ] Relay idempotente: processar o mesmo evento N vezes publica exatamente uma mensagem Pub/Sub.

---

### TASK-19 — Adaptadores das portas de leitura com resiliência Polly

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/digest/19-read-port-adapters` |
| **Worktree** | `git worktree add ../worktrees/digest/19-read-port-adapters -b feat/digest/19-read-port-adapters` |
| **Status** | [ ] |
| **Depende de** | TASK-09, TASK-14 |
| **Entregável** | Cinco adaptadores concretos das portas de leitura com Polly (timeout 10 s, retry com backoff, circuit breaker) e testes de resiliência verdes |
| **Mapeia** | Req 6 (Req 6.1–6.5), RNF 5 (Req RNF-5.1–5.4), DD-001 (leitura direta de read model), DD-003 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar os cinco adaptadores concretos em `Digest.Infrastructure` para as interfaces de `Digest.Application`: `ActivityReadAdapter` (`IActivityReadPort`), `OpportunityReadAdapter` (`IOpportunityReadPort`), `ForecastReadAdapter` (`IForecastReadPort`), `UserDirectoryAdapter` (`IUserDirectoryPort`), `UserDigestPreferenceAdapter` (`IUserDigestPreferencePort`). No MVP monolítico modular, as portas resolvem por leitura direta do read model (`StagnationView`, `ForecastView`) sob RLS; se/quando separados em deployables, migram para HTTP sem mudar a interface (DD-001). Cada adaptador usa Polly: timeout 10 s, retry com backoff exponencial (3 tentativas), circuit breaker. Falha de leitura de dado opcional (ex.: `IForecastReadPort`) retorna `null` sem lançar exceção (degradação graciosa — RNF 5.4).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de resiliência: simulação de timeout → retenta com backoff; circuit breaker abre após N falhas; `IForecastReadPort` com fonte indisponível → retorna `null`.
- [ ] **ST-02 — Green:** implementar os cinco adaptadores com Polly pipelines; registrar no DI com `AddHttpClient`/`AddResiliencePipeline`.
- [ ] **ST-03 — Refactor:** verificar que nenhum adaptador lança exceção por dado opcional faltante; testar que toda consulta passa `tenant_id` como filtro (Req 6.4).
- [ ] **ST-04 — Encerramento:** testes de resiliência verdes; commit `feat(digest): read port adapters with Polly timeout/retry/circuit breaker`; push.

#### Critérios de Aceite

- [ ] Timeout 10 s configurado em cada adaptador.
- [ ] Retry com backoff exponencial em pelo menos 3 tentativas.
- [ ] Circuit breaker abre após falhas consecutivas e emite alerta (DIG-ERR-030).
- [ ] `IForecastReadPort` retorna `null` (não lança) quando fonte indisponível (RNF 5.4).
- [ ] Toda consulta restringe dados ao `tenant_id` em processamento (Req 6.4).

---

### TASK-20 — EmailTemplateRenderer — HTML e branding do tenant

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/digest/20-email-template-renderer` |
| **Worktree** | `git worktree add ../worktrees/digest/20-email-template-renderer -b feat/digest/20-email-template-renderer` |
| **Status** | [ ] |
| **Depende de** | TASK-04, TASK-09 |
| **Entregável** | `EmailTemplateRenderer` que converte `DigestContent` em HTML + texto simples com branding do tenant; nenhum conteúdo logado |
| **Mapeia** | Req 4.3 (branding do tenant), Req 4.2 (link autenticado), RNF 3 (sem PII em log), design §5.3 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar `EmailTemplateRenderer` (Infrastructure): recebe `DigestContent`, dados de branding do tenant (logo URL, cores) e links autenticados; renderiza HTML responsivo + texto simples para o `IEmailSender`. Template funcional com branding básico (MVP — design §2.3). Inclui os blocos canônicos conforme presença em `DigestContent` (omite blocos vazios). Links autenticados gerados a partir dos `ActionToken` em claro. Nenhum conteúdo do digest é logado (RNF 3.2).

#### Subtasks

- [ ] **ST-01 — Red:** escrever teste que falha: `EmailTemplateRenderer.Render()` lança `NotImplementedException`.
- [ ] **ST-02 — Green:** implementar o renderer com Razor/Scriban; blocos incluídos somente quando `DigestSection.IsEmpty() = false`; link autenticado = `{base_url}/digest/actions/{token_clear}`.
- [ ] **ST-03 — Refactor:** testar que HTML renderizado com `azimute_metas` ausente não contém placeholder vazio (RN-018); verificar que o renderer não loga conteúdo.
- [ ] **ST-04 — Encerramento:** testes de snapshot HTML verdes; commit `feat(digest): EmailTemplateRenderer with tenant branding and action links`; push.

#### Critérios de Aceite

- [ ] HTML gerado inclui somente blocos com conteúdo (`IsEmpty() = false`).
- [ ] Sem bloco de metas no HTML quando `azimute_metas` não está presente (sem placeholder — Req 5.4).
- [ ] Link autenticado embutido no HTML para cada atividade (Req 4.2).
- [ ] Conteúdo do digest (títulos, nomes de conta) não é logado em nenhum nível (RNF 3.2).

---

### TASK-21 — DigestTriggerEndpoint OIDC/WIF e health checks

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — Api e Contratos |
| **Branch** | `feat/digest/21-trigger-endpoint` |
| **Worktree** | `git worktree add ../worktrees/digest/21-trigger-endpoint -b feat/digest/21-trigger-endpoint` |
| **Status** | [ ] |
| **Depende de** | TASK-13, TASK-19 |
| **Entregável** | `DigestTriggerEndpoint` com autenticação OIDC/WIF, resposta 202 assíncrona e health checks; testes de API verdes (401, 403, 202) |
| **Mapeia** | Req 1 (Req 1.1–1.5), RNF 7 (Req RNF-7.1), RNF 8, DD-005, DD-006, design §8.1, §8.2 |
| **Camada principal** | Api |

#### Objetivo

Implementar `POST /internal/digest/trigger` em `Digest.Api`: valida token OIDC via Workload Identity Federation (audience e SA email da service account do Cloud Scheduler); chama `SelectEligibleTenantsQuery`; publica uma mensagem Pub/Sub por tenant elegível no tópico `azim-digest-fanout`; responde `202 Accepted` com `{ "accepted": true, "eligible_tenants": N, "correlation_id": "uuid" }` **antes** da conclusão dos envios (RNF 8.1). Chamadas sem token OIDC → `401` (DIG-ERR-010); identidade não autorizada → `403` (DIG-ERR-011); payload inválido → `400` (DIG-ERR-001). Implementar health checks `GET /health/live` e `GET /health/ready` (design §8.2).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de API: chamada sem Authorization header → 401; token com SA diferente → 403; token válido com body válido → 202.
- [ ] **ST-02 — Green:** implementar endpoint com middleware de autenticação OIDC; publicação fan-out no Pub/Sub; health checks live/ready.
- [ ] **ST-03 — Refactor:** testar que `reference_utc` ausente no body usa hora cheia atual em UTC (Req 1.5); testar que endpoint retorna 202 antes de processar envios (mock Pub/Sub confirma enfileiramento, não envio).
- [ ] **ST-04 — Encerramento:** testes de API verdes (401, 403, 202); commit `feat(digest): DigestTriggerEndpoint with OIDC/WIF auth and health checks`; push.

#### Critérios de Aceite

- [ ] Chamada anônima → 401 (RNF 7.1).
- [ ] SA não autorizada → 403 (DIG-ERR-011); mensagem não revela identidades válidas.
- [ ] Resposta 202 devolvida antes de processar envios (RNF 8.1).
- [ ] `GET /health/ready` verifica Cloud SQL, Pub/Sub e `IEmailSender` ping (design §8.2).

---

### TASK-22 — PerTenantConsumer Pub/Sub e fan-out

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — Api e Contratos |
| **Branch** | `feat/digest/22-per-tenant-consumer` |
| **Worktree** | `git worktree add ../worktrees/digest/22-per-tenant-consumer -b feat/digest/22-per-tenant-consumer` |
| **Status** | [ ] |
| **Depende de** | TASK-13, TASK-21 |
| **Entregável** | `PerTenantConsumer` que recebe push Pub/Sub e despacha `RunDigestForTenantCommand`; DLQ configurada; testes de contrato do consumer verdes |
| **Mapeia** | Req 1.3 (assíncrono), Req 1.4 (disparo único global), RNF 4.1 (1.000 dest. ≤ 5 min), RNF 5.3, DD-005, design §6.3 |
| **Camada principal** | Api |

#### Objetivo

Implementar `PerTenantConsumer` em `Digest.Api`: recebe mensagens Pub/Sub push autenticadas (`azim-digest-fanout`); deserializa `tenant_id` e `reference_utc`; despacha `RunDigestForTenantCommand` via MediatR. Falha de um tenant não afeta os demais (RNF 5.3): exceção não propagada → Pub/Sub retenta via DLQ por subscription. Configurar subscription com `ack deadline`, retry policy e DLQ (`azim-digest-fanout-dlq`). Webhook de entrega do provedor (`UpdateDeliveryStatusCommand`) hospedado no mesmo endpoint ou em rota dedicada.

#### Subtasks

- [ ] **ST-01 — Red:** escrever teste de contrato do consumer: payload Pub/Sub válido → `RunDigestForTenantCommand` despachado; payload inválido → `400` (sem retentar).
- [ ] **ST-02 — Green:** implementar `PerTenantConsumer`; configurar DLQ e retry na subscription.
- [ ] **ST-03 — Refactor:** testar falha no handler de um tenant → consumer retorna 200 (não 500) para que Pub/Sub não retente o mesmo tenant indefinidamente; a falha vai para DLQ após N tentativas.
- [ ] **ST-04 — Encerramento:** testes de contrato verdes; commit `feat(digest): PerTenantConsumer with Pub/Sub push and DLQ`; push.

#### Critérios de Aceite

- [ ] Consumer processa mensagem válida e despacha `RunDigestForTenantCommand`.
- [ ] Falha de um tenant não propaga exceção para o Pub/Sub de forma a bloquear outros tenants.
- [ ] DLQ configurada para mensagens que esgotam retentativas.
- [ ] Consumer de webhook de entrega do provedor atualiza `EmailDigestLog` via `UpdateDeliveryStatusCommand`.

---

### TASK-23 — Contratos — DTOs, evento digest.email_sent.v1 e enums

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — Api e Contratos |
| **Branch** | `feat/digest/23-contracts` |
| **Worktree** | `git worktree add ../worktrees/digest/23-contracts -b feat/digest/23-contracts` |
| **Status** | [ ] |
| **Depende de** | TASK-09 |
| **Entregável** | `Digest.Contracts` com DTOs do trigger, schema do evento `digest.email_sent.v1` e enums de status/action; testes de schema (serialização/deserialização) verdes |
| **Mapeia** | Req 11 (Req 11.3), RNF 10.2, design §8.1, §9.1, §4.2 (lista canônica de status) |
| **Camada principal** | Contracts |

#### Objetivo

Definir em `Digest.Contracts`: `TriggerRequest` (campo `reference_utc` opcional), `TriggerResponse` (campos `accepted`, `eligible_tenants`, `correlation_id`), `DigestEmailSentEvent` (schema do evento de integração `digest.email_sent.v1` — design §9.1), enum `DigestStatus` (scheduled, sent, delivered, opened, bounced, failed), enum `ActionType` (complete, reschedule). Escrever testes de contrato: serialização e deserialização do evento; garantir que `DigestEmailSentEvent` não contém campos de e-mail ou conteúdo do digest. Versionamento por sufixo `.v1` com compatibilidade retroativa aditiva.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de round-trip JSON: `DigestEmailSentEvent` serializa e deserializa sem perda de dados; campo extra desconhecido é ignorado (tolerância retroativa).
- [ ] **ST-02 — Green:** implementar os contratos; serializar com `System.Text.Json`; garantir que `DigestEmailSentEvent` só tem os campos do design §9.1.
- [ ] **ST-03 — Refactor:** adicionar atributo de versão no evento; verificar que nenhum campo PII (e-mail, nome) está presente nos contratos.
- [ ] **ST-04 — Encerramento:** testes de schema verdes; commit `feat(digest): contracts - DTOs, DigestEmailSentEvent v1, DigestStatus and ActionType enums`; push.

#### Critérios de Aceite

- [ ] `DigestEmailSentEvent` contém exatamente os campos do design §9.1: `event`, `version`, `tenant_id`, `user_id`, `digest_date`, `message_id`, `correlation_id`, `causation_id`, `occurred_at`.
- [ ] Sem PII (sem e-mail, sem conteúdo) nos contratos (RNF 10.2).
- [ ] Testes de serialização/deserialização verdes (round-trip).
- [ ] `DigestStatus` e `ActionType` cobrem todas as listas canônicas do requirements §4.2 e §7.

---

### TASK-24 — Purge de email_digest_logs (90 dias) e digest_action_tokens

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `feat/digest/24-purge-jobs` |
| **Worktree** | `git worktree add ../worktrees/digest/24-purge-jobs -b feat/digest/24-purge-jobs` |
| **Status** | [ ] |
| **Depende de** | TASK-17 |
| **Entregável** | Jobs de purge com testes de integração verificando que registros expirados são removidos e registros válidos são preservados |
| **Mapeia** | RNF 9 (Req RNF-9.1–9.3), DD-004 (expires_at tokens), design §7 (notas de retenção) |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar dois jobs de purge em `Digest.Infrastructure`: (1) purge de `email_digest_logs` com `scheduled_at < now() - 90 dias` (RNF 9.1); (2) purge de `digest_action_tokens` com `expires_at < now()` (RNF 9.2). Os jobs podem ser implementados como `IHostedService` agendado (ex.: execução diária às 02:00 UTC) ou como background service no Cloud Run. Cada purge opera por `tenant_id` em lote (sem varredura global sem SET — respeita RLS). O `EmailDigestLog` armazena somente identificadores e status, nunca o conteúdo do e-mail (RNF 9.3).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de integração (Testcontainers): inserir registros de `email_digest_logs` com 91 dias e com 89 dias; executar purge; verificar que somente o de 91 dias é removido.
- [ ] **ST-02 — Green:** implementar os dois jobs de purge; executar `DELETE FROM email_digest_logs WHERE scheduled_at < @cutoff AND tenant_id = @tenant_id` por tenant.
- [ ] **ST-03 — Refactor:** logar contagem de registros purgados por tenant (sem PII); garantir que tokens com `used_at` não nulo e `expires_at` passado também são purgados.
- [ ] **ST-04 — Encerramento:** testes de integração verdes; commit `feat(digest): purge jobs for email_digest_logs (90d) and expired action tokens`; push.

#### Critérios de Aceite

- [ ] Registros de `email_digest_logs` com mais de 90 dias são removidos (RNF 9.1).
- [ ] `digest_action_tokens` com `expires_at < now()` são removidos (RNF 9.2).
- [ ] Registros dentro do prazo não são afetados.
- [ ] Log de purge registra contagem por tenant sem PII.

---

### TASK-25 — Gate de isolamento CI cross-tenant

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `test/digest/25-cross-tenant-isolation-gate` |
| **Worktree** | `git worktree add ../worktrees/digest/25-cross-tenant-isolation-gate -b test/digest/25-cross-tenant-isolation-gate` |
| **Status** | [ ] |
| **Depende de** | TASK-15, TASK-17 |
| **Entregável** | Suíte de isolamento cross-tenant em `Digest.Infrastructure.Tests` registrada como gate obrigatório de CI (merge bloqueado se falhar) |
| **Mapeia** | RNF 1 (Req RNF-1.3), RNF 1.4, DD-002, ADR-0001, RISK-DIGEST-04 |
| **Camada principal** | Tests |

#### Objetivo

Implementar suíte de testes de isolamento cross-tenant com Testcontainers (Postgres real): (a) tenant A processa digest → `EmailDigestLog` criado para tenant A; consulta direta com contexto do tenant B retorna 0 linhas; (b) `DigestActionToken` do tenant A é inacessível ao tenant B; (c) inserção com `tenant_id` de B rejeitada quando contexto setado para A (RLS WITH CHECK); (d) sem `SET app.current_tenant` → 0 linhas (falha-fechada). Estes testes devem ser marcados como gate de CI: PR não pode ser mergeado se qualquer um falhar (RNF 1.3).

#### Subtasks

- [ ] **ST-01 — Red:** escrever os quatro cenários de isolamento; falham porque o interceptor/RLS ainda não está configurado no Testcontainer do teste.
- [ ] **ST-02 — Green:** configurar Testcontainer com RLS + interceptor; executar os quatro cenários; todos verdes.
- [ ] **ST-03 — Refactor:** adicionar cenário (d) verificando que sem `SET`, nem a SA proprietária da tabela vê dados (FORCE ROW LEVEL SECURITY).
- [ ] **ST-04 — Docs:** registrar no README do módulo que estes testes são gate obrigatório de CI.
- [ ] **ST-05 — Encerramento:** suíte verde; CI configurada para bloquear merge se falhar; commit `test(digest): cross-tenant isolation gate with RLS fail-closed scenarios`; push.

#### Critérios de Aceite

- [ ] Tenant B não acessa dados do tenant A em nenhum dos cenários (RNF 1.1).
- [ ] RLS WITH CHECK impede inserção com `tenant_id` incorreto.
- [ ] Sem `SET app.current_tenant` → 0 linhas (falha-fechada — RNF 1.4).
- [ ] Gate registrado na CI: PR bloqueado se qualquer cenário de isolamento falhar.

---

### TASK-26 — Observabilidade completa — métricas, traces, alertas e logs sem PII

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `feat/digest/26-observability` |
| **Worktree** | `git worktree add ../worktrees/digest/26-observability -b feat/digest/26-observability` |
| **Status** | [ ] |
| **Depende de** | TASK-13, TASK-21 |
| **Entregável** | Métricas `digest_*` expostas, traces OpenTelemetry ativos, alertas configurados e teste de ausência de PII em telemetria verde |
| **Mapeia** | RNF 3 (sem PII), RNF 4.4 (métrica de duração), RNF 6 (Req RNF-6.1–6.4), DD-011, design §11 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar e validar a observabilidade completa conforme design §11: (1) logs estruturados JSON com `correlation_id`, `tenant_id`, `digest_date`, `recipients_selected`, `status` — destructuring policy do Serilog mascara e-mail e conteúdo (DD-011, RNF 3); (2) métricas: `digest_jobs_processed_total`, `digest_emails_sent_total`, `digest_emails_failed_total`, `digest_processing_duration_seconds` (histogram), `digest_delivery_rate` (gauge); (3) traces OpenTelemetry cobrindo seleção → composição → envio por usuário (RNF 6.4); (4) alertas: nenhum job processado após horário esperado; taxa de falha > 2%; `digest_delivery_rate < 95%` por > 1h (RNF 6.3).

#### Subtasks

- [ ] **ST-01 — Red:** escrever teste de PII em telemetria: processar um digest simulado; verificar que os logs capturados não contêm nenhum endereço de e-mail ou título de atividade.
- [ ] **ST-02 — Green:** configurar Serilog com destructuring policy; registrar as 5 métricas com `System.Diagnostics.Metrics`; configurar OpenTelemetry com spans por etapa do handler.
- [ ] **ST-03 — Refactor:** adicionar teste que verifica que `digest_processing_duration_seconds` é registrado ao final de cada job; configurar alertas no Cloud Monitoring (ou exportar definições de alerta como IaC).
- [ ] **ST-04 — Encerramento:** teste de PII verde; métricas verificáveis em teste de integração; commit `feat(digest): structured logs, digest_* metrics, OTEL traces, PII-free telemetry`; push.

#### Critérios de Aceite

- [ ] Nenhum log contém e-mail do destinatário em texto claro (RNF 3.1 — teste automatizado).
- [ ] Métricas `digest_jobs_processed_total`, `digest_emails_sent_total`, `digest_emails_failed_total`, `digest_processing_duration_seconds` e `digest_delivery_rate` expostas (RNF 6.2).
- [ ] Trace cobre seleção → composição → envio (RNF 6.4).
- [ ] Definições de alerta para os três cenários de RNF 6.3 documentadas ou configuradas como IaC.

---

### TASK-27 — Documentação final e DoD

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `docs/digest/27-dod-final` |
| **Worktree** | `git worktree add ../worktrees/digest/27-dod-final -b docs/digest/27-dod-final` |
| **Status** | [ ] |
| **Depende de** | TASK-24, TASK-25, TASK-26 |
| **Entregável** | Todos os itens do DoD do design §19 marcados; README do módulo atualizado; matriz de rastreabilidade completa |
| **Mapeia** | design §19 (DoD completo), requirements §10 (referências cruzadas), tasks.md §7 (critérios de encerramento do módulo) |
| **Camada principal** | Docs |

#### Objetivo

Verificar e registrar o atendimento de todos os itens do DoD do design §19. Atualizar o README do módulo digest com status atual das ondas, versão dos artefatos e links para TASKs. Verificar consistência entre `requirements.md v0.1.0`, `design.md v0.1.0` e `tasks.md v0.1.0`. Garantir que os `tasks.md` refletem o estado real de todas as TASKs. Registrar quaisquer pontos a validar não resolvidos (ex.: feriados — VAL-06 — para Fase 2).

#### Subtasks

- [ ] **ST-01:** verificar cada item do design §19 e marcar `[X]` ou registrar pendência com justificativa.
- [ ] **ST-02:** atualizar README do módulo: ondas concluídas, status de TASKs, versão dos artefatos, riscos remanescentes.
- [ ] **ST-03:** revisar `tasks.md §5` (Matriz de Rastreabilidade) — todas as origens mapeadas para TASK com status correto.
- [ ] **ST-04:** abrir PR da Onda 6 com checklist de revisão preenchido; registrar itens de Fase 2 (feriados, configuração de horário por usuário, notificações push).
- [ ] **ST-05 — Encerramento:** PR da Onda 6 aberto; commit `docs(digest): final DoD verification and README update`; push.

#### Critérios de Aceite

- [ ] Todos os itens do design §19 marcados `[X]` ou com pendência justificada para Fase 2.
- [ ] README do módulo atualizado com versão, ondas e status de artefatos.
- [ ] Matriz de rastreabilidade completa: nenhum Req ou RNF sem TASK correspondente.
- [ ] PR da Onda 6 aberto com checklist de revisão preenchido.

---

## 5. Matriz de Rastreabilidade

| Origem | Descrição | TASKs | Status |
|--------|-----------|-------|--------|
| Req 1 | Trigger horário do Cloud Scheduler | TASK-21, TASK-22 | [X] |
| Req 2 | Seleção de tenants por fuso IANA e dia útil | TASK-03, TASK-10 | [X] |
| Req 3 | Seleção de destinatários por papel e pendências | TASK-07, TASK-10 | [X] |
| Req 4 | Digest de pendências (terça a sexta) | TASK-11, TASK-20 | [X] |
| Req 5 | Azimute da semana (segunda-feira) | TASK-04, TASK-08, TASK-11 | [X] |
| Req 6 | Consumo via read model dos módulos donos | TASK-02, TASK-09, TASK-19 | [X] |
| Req 7 | Token de ação de um clique | TASK-04, TASK-06, TASK-16 | [X] |
| Req 8 | Envio via IEmailSender e registro do envio | TASK-12, TASK-20 | [X] |
| Req 9 | Idempotência por usuário e data | TASK-05, TASK-12, TASK-17 | [X] |
| Req 10 | Opt-out individual do digest | TASK-07, TASK-09 | [X] |
| Req 11 | Registro de status de entrega e auditoria | TASK-14, TASK-18, TASK-22 | [X] |
| RNF 1 | Isolamento por tenant | TASK-14, TASK-15, TASK-25 | [X] |
| RNF 2 | Idempotência resiliente a retentativas | TASK-12, TASK-17 | [X] |
| RNF 3 | Ausência de PII em logs | TASK-13, TASK-26 | [X] |
| RNF 4 | Pontualidade e desempenho (≤ 5 min / 1.000 dest.) | TASK-21, TASK-22, TASK-26 | [X] |
| RNF 5 | Resiliência de envio e isolamento de falhas | TASK-13, TASK-19 | [X] |
| RNF 6 | Observabilidade do worker | TASK-26 | [X] |
| RNF 7 | Segurança do trigger e do token | TASK-16, TASK-21 | [X] |
| RNF 8 | Execução assíncrona obrigatória | TASK-21, TASK-22 | [X] |
| RNF 9 | Retenção e minimização dos logs de digest | TASK-24 | [X] |
| RNF 10 | Auditoria do envio (DigestEmailSent sem PII) | TASK-18 | [X] |
| PBT-01 | Elegibilidade neutra a DST | TASK-03 | [X] |
| PBT-02 | Idempotência sob N retentativas | TASK-12, TASK-17 | [X] |
| PBT-03 | Regra de inclusão de destinatário (total) | TASK-07 | [X] |
| PBT-04 | Omissão graciosa do bloco de metas | TASK-11 | [X] |
| PBT-05 | Token não previsível e expirável (anti-enumeração) | TASK-16 | [X] |
| PBT-06 | Azimute apenas na segunda-feira com papel de gestão | TASK-11 | [X] |
| DD-001 | Leitura direta de read model (resolve VAL-TRD-13) | TASK-09, TASK-19 | [X] |
| DD-002 | Isolamento multi-tenant em defesa em profundidade | TASK-14, TASK-15, TASK-25 | [X] |
| DD-003 | Opt-out pertence ao organization (IUserDigestPreferencePort) | TASK-09 | [X] |
| DD-004 | TTL do token = 48h | TASK-06, TASK-16 | [X] |
| DD-005 | Fan-out por tenant via Pub/Sub | TASK-21, TASK-22 | [X] |
| DD-006 | Trigger HTTP direto + fan-out interno | TASK-21 | [X] |
| DD-007 | Token opaco persistido por hash SHA-256 | TASK-04, TASK-16 | [X] |
| DD-008 | Idempotência por reserva antes do envio | TASK-12, TASK-17 | [X] |
| DD-009 | DigestEmailSent via Outbox transacional | TASK-18 | [X] |
| DD-010 | Valores monetários do azimute em centavos inteiros | TASK-04, TASK-11 | [X] |
| DD-011 | Telemetria sem PII (destructuring policy) | TASK-26 | [X] |
| DD-012 | Conversão de fuso via NodaTime (TZDB IANA) | TASK-01, TASK-03 | [X] |
| ADR-0001 | Isolamento multi-tenant (RLS falha-fechada) | TASK-14, TASK-15, TASK-25 | [X] |
| ADR-0004 | Outbox/idempotência | TASK-18 | [X] |
| ADR-0006 | Token de link autenticado | TASK-06, TASK-16 | [X] |
| ADR-0008 | Scheduling por Cloud Scheduler UTC + fuso IANA | TASK-03, TASK-10 | [X] |

## 6. Coverage Gates

| Camada | Gate | Tipo de teste esperado |
|--------|------|------------------------|
| Domain (`Digest.Domain.Tests`) | ≥ 95% | Unitários de VOs, entidades, state machine; PBT-01, PBT-03 |
| Application (`Digest.Application.Tests`) | ≥ 85% | Unitários de handlers, queries, composers, factory interface; PBT-02, PBT-04, PBT-06 |
| Infrastructure (`Digest.Infrastructure.Tests`) | ≥ 70% | Integração (Testcontainers); RLS; repositórios; outbox; adapters; PBT-02 infra; PBT-05 |
| Api (`Digest.Api.Tests`) | ≥ 80% | Contrato trigger (401/403/202); contrato consumer; health checks |
| Architecture (`Digest.Architecture.Tests`) | 100% das regras críticas | Regra de dependência; proibição de schema alheio |
| Isolamento CI (gate de merge) | 100% dos cenários | Cross-tenant isolation (TASK-25): tenant A vs B, falha-fechada sem SET |
| Segurança | Cobertura por cenário | Entropia do token (PBT-05), ausência de PII em telemetria, 401/403 do trigger |

Regras:
- Coverage gate não substitui qualidade do teste; PBT deve existir para toda propriedade identificada.
- Testes de `Digest.Architecture.Tests` são gate de build: falha bloqueia CI.
- Suíte de isolamento cross-tenant (TASK-25) é gate de merge: PR não mergea se qualquer cenário falhar.

## 7. Critérios de Encerramento

### 7.1 Encerramento de TASK

Uma TASK só pode ser marcada `[X]` quando:

- todas as subtasks concluídas
- testes da camada executados com sucesso
- coverage gate atendido ou justificativa registrada
- `dotnet build` sem warnings novos
- `dotnet format` executado
- commit em Conventional Commits realizado
- push da branch realizado
- documentação atualizada quando aplicável

### 7.2 Encerramento de Onda

Uma onda só pode ser considerada concluída quando:

- todas as TASKs da onda estão `[X]`
- CI verde
- PR da onda aberto, aprovado ou mergeado conforme regra do projeto
- riscos da onda tratados ou registrados
- README do módulo sincronizado

### 7.3 Encerramento do Módulo

O módulo digest só pode ser considerado pronto quando:

- todas as 6 ondas concluídas
- todos os 27 `[X]` na seção Status Geral e na seção Tarefas
- todos os itens do DoD do design §19 marcados ou com justificativa de Fase 2
- PBT-01..06 verdes em CI
- gate de isolamento CI (TASK-25) verde e obrigatório em todo PR
- métricas `digest_*` e health checks ativos em ambiente de staging
- `email_digest_logs` e `digest_action_tokens` com RLS falha-fechada verificada
- catálogo de erros DIG-ERR-001/010/011/020/030/040/050 coberto por teste ou documentado
- VAL-TRD-13, VAL-DIGEST-01/02/03/04 e VAL-TRD-05 resolvidos via DDs registrados
- `requirements.md`, `design.md` e `tasks.md` consistentes

## 8. Riscos de Execução

| Código | Risco | Impacto | Mitigação | TASKs |
|--------|-------|---------|-----------|-------|
| RISK-DIGEST-01 | Digest não entregue antes do `digest_time` | Diferencial não cumprido | Fan-out Pub/Sub para paralelização; métrica `digest_processing_duration_seconds`; alerta CI | TASK-21, TASK-22, TASK-26 |
| RISK-DIGEST-02 | Envio duplicado por retentativa | Usuário recebe e-mails repetidos | Reserva `scheduled` + UNIQUE + PBT-02 | TASK-12, TASK-17 |
| RISK-DIGEST-03 | Falha do provedor de e-mail | Nenhum digest entregue | Backoff + circuit breaker (notification-delivery); alerta de taxa de falha | TASK-19, TASK-26 |
| RISK-DIGEST-04 | Vazamento cross-tenant por falha de filtro | Incidente sev-1 | RLS falha-fechada + gate de isolamento CI obrigatório | TASK-15, TASK-25 |
| RISK-DIGEST-05 | Acoplamento de schema a tabelas de outros BCs | Rigidez na separação de deployables | Sem FK física; acesso somente por porta de leitura; Architecture.Tests | TASK-02, TASK-09 |
| RISK-DIGEST-06 | Defasagem do read model no disparo | Conteúdo desatualizado | Leitura no disparo (snapshot); read models calculados em tempo real | TASK-19 |
| RISK-DIGEST-07 | Dependência de opt-out no organization atrasa Req 10 | Opt-out indisponível no MVP | Coordenar `IUserDigestPreferencePort` com organization antes da Onda 4; Req 10 é Should | TASK-09 |

## 9. Referências

| Documento | Relação |
|-----------|---------|
| docs/product/modules/digest/requirements.md v0.1.0 | Base funcional/não-funcional e PBTs (Req 1..11, RNF 1..10, PBT-01..06) |
| docs/product/modules/digest/design.md v0.1.0 | Design técnico base (DD-001..012, schema SQL, catálogo de erros, DoD §19) |
| docs/product/modules/digest/README.md | Visão de módulo, riscos e pontos a validar |
| docs/product/adr/0001-isolamento-multi-tenant-defesa-em-profundidade.md | ADR-0001 — Aceito — base para TASK-14, TASK-15, TASK-25 |
| ADR-0004 (outbox/idempotência — a formalizar) | Base para TASK-18 |
| ADR-0006 (token de link autenticado — a formalizar) | Base para TASK-06, TASK-16 |
| ADR-0008 (scheduling por fuso IANA — a formalizar) | Base para TASK-03, TASK-10 |
| `.forge/rules/architecture/clean-architecture.md` | Regra de dependência enforçada em TASK-02 |
| `.forge/rules/domain/money-as-cents.md` | Centavos inteiros enforçado em TASK-04, TASK-11 |
| `.forge/rules/domain/audit-immutability.md` | Auditoria append-only enforçada em TASK-18 |
| `.forge/rules/architecture/security-and-compliance.md` | RLS, OIDC/WIF, anti-enumeração — TASK-15, TASK-16, TASK-21 |
| `.forge/rules/architecture/observability.md` | Sem PII em telemetria — TASK-26 |
| docs/product/modules/notification-delivery/design.md | Contrato `IEmailSender`/`SendResult` consumido por TASK-12 |
| docs/product/modules/activity-management/design.md | Consumo/validação do token de ação (Req 7.4 — fora deste módulo) |
