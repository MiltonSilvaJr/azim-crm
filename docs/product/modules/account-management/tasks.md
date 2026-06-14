# Tasks — BC-02 — Account Management (Gestão de Contas e Contatos)

- Versão: 0.1.1
- Data: 2026-06-13
- Status: Aprovado para desenvolvimento
- Referência base requirements: docs/product/modules/account-management/requirements.md v0.1.0
- Referência base design: docs/product/modules/account-management/design.md v0.1.0
- ADRs aplicáveis: nenhum ADR formal no catálogo; decisões inline DD-001..DD-007 registradas no design.md
- Rules aplicáveis: `.forge/rules/architecture/clean-architecture.md`, `.forge/rules/architecture/api-and-contracts.md`, `.forge/rules/architecture/observability.md`, `.forge/rules/architecture/security-and-compliance.md`, `.forge/rules/architecture/ddd.md`, `.forge/rules/conventions/database-naming.md`, `.forge/rules/domain/audit-immutability.md`, `.forge/rules/architecture/jwt-permissions.md`, `.forge/rules/architecture/mtls-internal-services.md`

## Histórico de Versões

| Versão | Data | Status | Descrição da alteração |
|--------|------|--------|------------------------|
| 0.1.0 | 2026-06-11 | Rascunho para revisão | Criação inicial do plano derivado de requirements.md v0.1.0 e design.md v0.1.0 |
| 0.1.1 | 2026-06-13 | Aprovado para desenvolvimento | Aprovação humana (HITL #1); execução via `/forge:coding-loop` autônomo (6 ondas). |

## 1. Convenções de Implementação

### 1.1 TDD-first

Toda implementação com lógica verificável segue o ciclo:

1. Red — escrever teste que falha
2. Green — implementar o mínimo para passar
3. Refactor — melhorar sem alterar comportamento

Nenhuma regra de domínio, handler, endpoint, persistência ou integração é concluída sem teste correspondente.

### 1.2 Property-Based Testing

PBT é obrigatório para invariantes matemáticas, idempotência, anti-enumeração, state machines e regras de conservação. Cada PBT mapeia explicitamente para `PBT-NN` do `requirements.md` ou invariante descrita no `design.md`. Biblioteca preferida: FsCheck (integrado com xUnit).

### 1.3 Bite-sized Tasks

Cada subtask deve ser estimada em menos de 2 horas. Se exceder, deve ser dividida. Cada TASK deve ter preferencialmente até 1 dia; no máximo 2 dias quando escopo for claro e entregável verificável.

### 1.4 Branch Model

```
<tipo>/account-management/<NN>-<slug>
```

Exemplos: `feat/account-management/01-bootstrap-clean-architecture`, `test/account-management/02-domain-value-objects`.

### 1.5 Git Worktree

```sh
git worktree add ../worktrees/account-management/<NN>-<slug> -b <branch>
```

### 1.6 Encerramento de TASK

- testes locais verdes
- coverage gate da camada atendido ou justificativa registrada
- lint/format executado
- documentação atualizada quando aplicável
- commit em Conventional Commits
- push da branch

### 1.7 Encerramento de Onda

- todas as TASKs da onda em `[X]`
- CI verde
- PR da onda aberto ou atualizado
- documentação sincronizada

### 1.8 Early Exit

Se uma subtask falhar: marcar `[-]`, registrar ponto de falha, comando executado e erro principal; não mascarar com implementação especulativa; deixar contexto suficiente para retomada.

### 1.9 Convenção de Status

- `[ ]` Não iniciado
- `[-]` Em progresso
- `[X]` Concluído
- `[!]` Falhou — exige intervenção humana (interrompe a onda no `task-coder`)

### 1.10 Convenção canônica de IDs

```
TASK-NN — <título>   ← unidade atômica de invocação do task-coder
  ST-MM — <subtask>  ← etapas TDD internas; numeração reinicia a cada TASK
```

Onda é atributo (`campo **Onda**` no header da TASK) e seção de agrupamento visual em §3 — nunca entra no ID da TASK nem nas subtasks.

---

## 2. Status Geral

| TASK | Título | Onda | Branch | Status |
|------|--------|------|--------|--------|
| TASK-01 | Solution .NET + 5 projetos Clean Architecture + Architecture.Tests | Onda 1 | `feat/account-management/01-bootstrap-clean-architecture` | [X] |
| TASK-02 | Objetos de valor + NameNormalizer (PBT-01, PBT-02) | Onda 2 | `test/account-management/02-domain-value-objects-normalizer` | [ ] |
| TASK-03 | Agregado Account + Contact + eventos + specifications + IAccountRepository (PBT-03) | Onda 2 | `test/account-management/03-domain-aggregate-events` | [ ] |
| TASK-04 | Commands + Handlers de Account + queries de busca + Validators | Onda 3 | `feat/account-management/04-application-account-commands` | [ ] |
| TASK-05 | Behaviors de pipeline (TenantScope, PiiAccess, Transaction, Logging) + Ports | Onda 3 | `feat/account-management/05-application-behaviors-ports` | [ ] |
| TASK-06 | Commands + Handlers de Contact + ListContactsQuery + Validators | Onda 3 | `feat/account-management/06-application-contact-commands` | [ ] |
| TASK-07 | GetAccount360Query + Handler + degradação parcial + PBT-05 | Onda 3 | `test/account-management/07-application-query-360` | [ ] |
| TASK-08 | DbContext + mappings EF Core + migrations accounts/contacts + índice normalizado + filtro global | Onda 4 | `feat/account-management/08-infra-dbcontext-migrations` | [ ] |
| TASK-09 | Migrations audit_logs (trigger + REVOKE) + outbox_messages + idempotency_keys | Onda 4 | `feat/account-management/09-infra-audit-outbox-idempotency` | [ ] |
| TASK-10 | IAccountRepository implementação + TenantContext + Testcontainers + PBT-04 gate CI | Onda 4 | `test/account-management/10-infra-repository-tenant-isolation` | [ ] |
| TASK-11 | PiiMasker + AuditPublisher + Outbox relay + Pub/Sub | Onda 4 | `feat/account-management/11-infra-pii-masker-outbox-pubsub` | [ ] |
| TASK-12 | ReadPorts adapters (Opportunity, Activity) + resiliência + IdempotencyKey | Onda 4 | `feat/account-management/12-infra-read-ports-resilience` | [ ] |
| TASK-13 | AccountsController + DTOs de conta + middleware + erros ACC-ERR-001/002/003/009 | Onda 5 | `feat/account-management/13-api-accounts-controller` | [ ] |
| TASK-14 | ContactsController + DTOs + RBAC + ForgetContact + erros ACC-ERR-004..008 (PBT-03) | Onda 5 | `feat/account-management/14-api-contacts-controller` | [ ] |
| TASK-15 | Endpoint 360° + contratos de evento + OpenAPI completo + contract tests (PBT-05) | Onda 5 | `feat/account-management/15-api-360-events-openapi` | [ ] |
| TASK-16 | Observabilidade — métricas + logs estruturados + traces + alertas + health (RNF 9) | Onda 6 | `feat/account-management/16-hardening-observability` | [ ] |
| TASK-17 | Scan anti-PII em logs + isolamento de tenant como gate CI (RNF 1.4, PBT-04) | Onda 6 | `test/account-management/17-hardening-pii-scan-tenant-gate` | [ ] |
| TASK-18 | RBAC final + anti-enumeração + DoD + approvals.yaml + residência de dados (RNF 10) | Onda 6 | `feat/account-management/18-hardening-rbac-dod` | [ ] |

---

## 3. Ondas de Implementação

| Onda | Foco | TASKs |
|------|------|-------|
| Onda 1 | Bootstrap — solution, projetos, testes de arquitetura | TASK-01 |
| Onda 2 | Domain — objetos de valor, agregado Account, domain events, specifications | TASK-02..TASK-03 |
| Onda 3 | Application — commands, queries, handlers, behaviors, ports | TASK-04..TASK-07 |
| Onda 4 | Infrastructure — DbContext, migrations, repositório, PiiMasker, Outbox, adapters | TASK-08..TASK-12 |
| Onda 5 | API + Contracts — controllers, DTOs, erros, OpenAPI, contratos de evento | TASK-13..TASK-15 |
| Onda 6 | Hardening — observabilidade, scan anti-PII, RBAC final, DoD | TASK-16..TASK-18 |

**Critérios de fechamento:**

- **Onda 1:** solution compilando; `Architecture.Tests` verde; CI configurado.
- **Onda 2:** objetos de valor, agregado e eventos com testes unitários e PBT-01/02/03 verdes; cobertura Domain ≥ 95%.
- **Onda 3:** handlers e behaviors com testes unitários verdes; PBT-05 verde; cobertura Application ≥ 85%.
- **Onda 4:** migrations aplicadas em banco de teste; PBT-04 verde como gate CI; `PiiMasker` testado; cobertura Infrastructure ≥ 70%.
- **Onda 5:** todos os endpoints respondendo contratos esperados; catálogo de erros completo; OpenAPI publicado; contract tests verdes; cobertura Api ≥ 80%.
- **Onda 6:** scan anti-PII verde como gate CI; métricas expostas; alertas configurados; DoD completo; `approvals.yaml` atualizado; PR pronto para revisão.

---

## 4. Tarefas

### TASK-01 — Solution .NET + 5 projetos Clean Architecture + Architecture.Tests

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 1 — Bootstrap |
| **Branch** | `feat/account-management/01-bootstrap-clean-architecture` |
| **Worktree** | `git worktree add ../worktrees/account-management/01-bootstrap-clean-architecture -b feat/account-management/01-bootstrap-clean-architecture` |
| **Status** | [X] |
| **Depende de** | Não aplicável |
| **Entregável** | `AccountManagement.slnx` com 5 projetos + `Architecture.Tests` verde + CI mínimo |
| **Mapeia** | design §3, P1 |
| **Camada principal** | DevOps, Tests |

#### Objetivo

Criar a estrutura base do módulo: solution `AccountManagement.sln`, projetos `Domain`, `Application`, `Infrastructure`, `Api`, `Contracts` com referências entre camadas e o projeto `Architecture.Tests` que valide as regras de dependência em tempo de CI.

#### Subtasks

- [X] **ST-01 — Red:** escrever testes NetArchTest que falham verificando: `Domain` sem referência a outros projetos do módulo; `Contracts` sem referência a outros projetos; `Application` referencia apenas `Domain` e `Contracts`; `Infrastructure` referencia `Application` e `Domain`; `Api` referencia `Application` e `Infrastructure`.
- [X] **ST-02 — Green:** criar `AccountManagement.slnx`, 5 projetos `.csproj` com referências corretas, `Architecture.Tests`; instalar dependências mínimas (MediatR, FluentValidation, EF Core, NetArchTest, FsCheck).
- [X] **ST-03 — Refactor:** ajustar namespaces (`AccountManagement.*`), adicionar `.editorconfig`, garantir build limpo sem warnings.
- [X] **ST-04 — Docs:** registrar estrutura de diretórios no README do módulo (seção de setup).
- [X] **ST-05 — Encerramento:** `dotnet build` verde; `Architecture.Tests` verde; commit `chore(account-management): bootstrap solution with clean architecture projects` + push.

#### Critérios de Aceite

- [X] `dotnet build` sem erros em todos os projetos
- [X] `Architecture.Tests` verde com todas as regras de dependência entre camadas
- [ ] CI mínimo configurado (build + architecture tests)
- [X] Nenhum warning novo introduzido

---

### TASK-02 — Objetos de valor + NameNormalizer (PBT-01, PBT-02)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `test/account-management/02-domain-value-objects-normalizer` |
| **Worktree** | `git worktree add ../worktrees/account-management/02-domain-value-objects-normalizer -b test/account-management/02-domain-value-objects-normalizer` |
| **Status** | [ ] |
| **Depende de** | TASK-01 |
| **Entregável** | `AccountName`, `NormalizedName`, `Email`, `Phone`, `ContactInfo` (com `ToMasked()`), `ContactPrivacyState`, `NameNormalizer` com PBT-01 e PBT-02 verdes |
| **Mapeia** | Req 1.1, Req 5.2, Req 5.4, Req 7.5, RNF 1, RNF 2, PBT-01, PBT-02, DD-003, DD-005 |
| **Camada principal** | Domain |

#### Objetivo

Implementar todos os objetos de valor imutáveis do domínio e o domain service `NameNormalizer` (NFD sem diacrítico → minúsculas → sem pontuação → espaços colapsados; determinístico e sem fuzzy — DD-005). Garantir idempotência (PBT-01) e equivalência por forma normalizada (PBT-02) com PBT usando geradores FsCheck de strings Unicode arbitrárias.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes unitários para `AccountName` (vazio → `AccountNameRequiredException`), `Email` (formato inválido → `InvalidEmailException`; lowercase), `Phone` (apenas dígitos significativos), `ContactInfo` (nome obrigatório; `ToMasked()` retorna marcadores sem PII), `ContactPrivacyState` (apenas `Active`/`Anonymized`; sem outros estados), `NormalizedName` (igualdade por valor).
- [ ] **ST-02 — Red PBT:** escrever PBT-01 (`normalize(normalize(x)) == normalize(x)`) e PBT-02 (nomes que diferem apenas por acento/caixa/espaço/pontuação produzem forma normalizada idêntica) com geradores FsCheck sobre strings Unicode arbitrárias (mínimo 1 000 amostras).
- [ ] **ST-03 — Green:** implementar os 6 objetos de valor + `NameNormalizer`; garantir imutabilidade e igualdade por valor (`Equals`/`GetHashCode`) em todos.
- [ ] **ST-04 — Refactor:** extrair constante de marcador de anonimização (ex.: `"[anonimizado]"`) para `AccountManagement.Domain`; remover duplicações entre objetos de valor.
- [ ] **ST-05 — Encerramento:** PBT-01 e PBT-02 verdes; cobertura Domain ≥ 95% nos arquivos desta TASK; commit `test(domain): value objects and NameNormalizer PBT-01 PBT-02` + push.

#### Critérios de Aceite

- [ ] PBT-01 verde: idempotência da normalização com ≥ 1 000 amostras geradas
- [ ] PBT-02 verde: equivalência por forma normalizada com ≥ 1 000 amostras
- [ ] `ContactInfo.ToMasked()` não expõe nome, e-mail nem telefone em texto claro
- [ ] Todos os objetos de valor com igualdade por valor (`Equals`/`GetHashCode`)
- [ ] Cobertura Domain ≥ 95% nos arquivos desta TASK

---

### TASK-03 — Agregado Account + entidade Contact + domain events + specifications + IAccountRepository (PBT-03)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `test/account-management/03-domain-aggregate-events` |
| **Worktree** | `git worktree add ../worktrees/account-management/03-domain-aggregate-events -b test/account-management/03-domain-aggregate-events` |
| **Status** | [ ] |
| **Depende de** | TASK-02 |
| **Entregável** | Agregado `Account` com `Contact`, domain events sem PII, specifications/policies, `IAccountRepository` e PBT-03 verde |
| **Mapeia** | Req 1.5, Req 2.3, Req 4.2, Req 5.1, Req 7.1..7.5, Req 8.1..8.4, Req 9, Req 10, PBT-03, DD-001, DD-006 |
| **Camada principal** | Domain |

#### Objetivo

Implementar o agregado `Account` (sem `bu_id` — Req 2.3) com `Contact` como entidade interna, domain events (`AccountCreated`, `AccountUpdated`, `ContactLinked` com `maskedDelta`, `ContactForgotten`), specifications (`SimilarAccountSpecification`, `TenantScopeSpecification`) e policies (`ForgetContactPolicy`, `PiiAccessPolicy`). Cobrir a state machine de privacidade Active→Anonymized (irreversível — PBT-03) e as invariantes I1/I2/I3 do design §4.1.

#### Subtasks

- [ ] **ST-01 — Red:** testes para invariantes: I1 (nome vazio → `AccountNameRequiredException`), I2 (`Rename` recalcula `NormalizedName`), I3 (`AddContact` herda `tenant_id` e `account_id` do root).
- [ ] **ST-02 — Red:** testes para `Account.AddContact`, `Account.UpdateContact`, `Account.ForgetContact` (Active→Anonymized; PII substituída por marcador; `contact_id` preservado; chamada repetida sobre contato já anonimizado → `ContactAlreadyForgottenException`).
- [ ] **ST-03 — Red PBT:** PBT-03 — para qualquer contato submetido ao esquecimento: nenhuma propriedade de PII retorna valor original após a operação; `contact_id` permanece idêntico; transição Active→Anonymized não admite retorno (gerar estados arbitrários pelo gerador).
- [ ] **ST-04 — Red:** testes para `SimilarAccountSpecification` (mesmo tenant + mesmo `normalized_name` → candidato); `ForgetContactPolicy` (papel abaixo de Tenant Admin → rejeita); `PiiAccessPolicy` (abaixo de Vendedor → rejeita); `TenantScopeSpecification`.
- [ ] **ST-05 — Green:** implementar `Account`, `Contact`, factories `Account.Create`/`Account.Reconstitute`, métodos `Rename`, `AddContact`, `UpdateContact`, `ForgetContact`; events; exceptions (`AccountNameRequiredException`, `InvalidEmailException`, `ContactAlreadyForgottenException`); specifications/policies; `IAccountRepository`.
- [ ] **ST-06 — Refactor:** confirmar que nenhum evento (`ContactLinked`, `ContactForgotten`) carrega PII em texto claro; `ContactLinked.maskedDelta` recebe saída de `ContactInfo.ToMasked()`.
- [ ] **ST-07 — Encerramento:** PBT-03 verde; cobertura Domain ≥ 95% combinando TASK-02 + TASK-03; commit `test(domain): Account aggregate Contact state machine PBT-03` + push.

#### Critérios de Aceite

- [ ] PBT-03 verde: irreversibilidade + `contact_id` preservado após esquecimento
- [ ] Domain events sem PII em texto claro; `ContactLinked.maskedDelta` mascarado
- [ ] `SimilarAccountSpecification` detecta candidatos por `normalized_name` (não por unique constraint — DD-006)
- [ ] Cobertura Domain ≥ 95% (TASK-02 + TASK-03 combinadas)

---

### TASK-04 — Commands + Handlers de Account + queries de busca + Validators

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/account-management/04-application-account-commands` |
| **Worktree** | `git worktree add ../worktrees/account-management/04-application-account-commands -b feat/account-management/04-application-account-commands` |
| **Status** | [ ] |
| **Depende de** | TASK-03 |
| **Entregável** | `CreateAccountCommand`, `UpdateAccountCommand`, `SearchAccountsQuery`, `SearchSimilarAccountsQuery`, `GetAccountByIdQuery`, handlers e validators FluentValidation |
| **Mapeia** | Req 1, Req 2, Req 3, Req 4, RNF 7, DD-005, DD-006 |
| **Camada principal** | Application |

#### Objetivo

Implementar commands e queries de Account. `CreateAccountCommand` aceita flag `confirmCreateDespiteSimilar` (dedupe não-bloqueante, DD-006 — o POST nunca bloqueia por similaridade). `SearchSimilarAccountsQuery` usa `SimilarAccountSpecification`. `SearchAccountsQuery` serve busca paginada restrita ao tenant. Handlers apenas orquestram — sem regra de negócio.

#### Subtasks

- [ ] **ST-01 — Red:** testes de handler para `CreateAccountCommand` (conta criada; `AccountCreated` emitido; mesmo com similar existente, a criação ocorre quando `confirmCreateDespiteSimilar=true`); `UpdateAccountCommand` (renomeia; `NormalizedName` recalculado; `AccountUpdated` emitido).
- [ ] **ST-02 — Red:** testes para `SearchAccountsQuery` (paginação; restrita ao tenant); `SearchSimilarAccountsQuery` (retorna candidatos do mesmo tenant com mesmo `normalized_name`); `GetAccountByIdQuery` (404 `ACC-ERR-003` se fora do tenant ou inexistente).
- [ ] **ST-03 — Red:** testes de validator: `CreateAccountValidator` (nome vazio → `ACC-ERR-001`); `UpdateAccountValidator` (nome vazio → `ACC-ERR-001`); `SearchAccountsValidator` (paginação inválida → `ACC-ERR-002`).
- [ ] **ST-04 — Green:** implementar commands, queries e handlers usando mocks/stubs de `IAccountRepository`; implementar validators FluentValidation.
- [ ] **ST-05 — Refactor:** confirmar que handlers não contêm regra de negócio; todas as invariantes delegadas ao domínio.
- [ ] **ST-06 — Encerramento:** cobertura Application ≥ 85% nos arquivos desta TASK; commit `feat(application): account commands queries and validators` + push.

#### Critérios de Aceite

- [ ] `CreateAccountCommand` não bloqueia por similaridade; alerta é responsabilidade da UI via `SearchSimilarAccountsQuery`
- [ ] `SearchAccountsQuery` e `SearchSimilarAccountsQuery` operam exclusivamente dentro do tenant
- [ ] Validators retornam `ACC-ERR-001` e `ACC-ERR-002` sem vazar PII
- [ ] Cobertura Application ≥ 85% nos arquivos desta TASK

---

### TASK-05 — Behaviors de pipeline (TenantScope, PiiAccess, Transaction, Logging) + Ports

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/account-management/05-application-behaviors-ports` |
| **Worktree** | `git worktree add ../worktrees/account-management/05-application-behaviors-ports -b feat/account-management/05-application-behaviors-ports` |
| **Status** | [ ] |
| **Depende de** | TASK-04 |
| **Entregável** | `CorrelationLoggingBehavior`, `TenantScopeBehavior`, `ValidationBehavior`, `PiiAccessBehavior`, `TransactionBehavior` + interfaces `IOpportunityReadPort`, `IActivityReadPort`, `IAuditPublisher`, `IClock` |
| **Mapeia** | Req 9, Req 10, RNF 1, RNF 5, RNF 6, DD-002, DD-004, DD-007 |
| **Camada principal** | Application |

#### Objetivo

Implementar os behaviors do pipeline MediatR na ordem definida no design §5.4. `CorrelationLoggingBehavior` injeta `correlation_id`, `tenant_id`, `account_id` no escopo de log sem nunca registrar PII. `PiiAccessBehavior` nega antes de tocar PII quando papel insuficiente. `TransactionBehavior` mantém escrita + outbox na mesma transação (DD-007). Declarar as 4 interfaces de port na camada Application.

#### Subtasks

- [ ] **ST-01 — Red:** testes para `TenantScopeBehavior` (injeta `TenantContext`; rejeita request sem tenant); `PiiAccessBehavior` (papel abaixo de Vendedor na BU → 403 sem expor PII; Tenant Admin passa em operações de esquecimento); `TransactionBehavior` (commit após handler; rollback em exceção; outbox na mesma transação).
- [ ] **ST-02 — Red:** testes para `CorrelationLoggingBehavior` (logs contêm `correlation_id`, `tenant_id`; nenhum log da request contém `name`/`email`/`phone` de contato em texto claro).
- [ ] **ST-03 — Green:** implementar os 5 behaviors; declarar `IOpportunityReadPort`, `IActivityReadPort`, `IAuditPublisher`, `IClock` com contratos mínimos.
- [ ] **ST-04 — Refactor:** adicionar testes de ordem de execução do pipeline se o framework não garantir a sequência definida.
- [ ] **ST-05 — Encerramento:** cobertura Application ≥ 85% nos behaviors; commit `feat(application): pipeline behaviors and port interfaces` + push.

#### Critérios de Aceite

- [ ] `PiiAccessBehavior` nega acesso antes de tocar PII (403 sem revelar conteúdo quando papel insuficiente)
- [ ] `TenantScopeBehavior` resolve `TenantContext` antes de qualquer operação de dados
- [ ] `CorrelationLoggingBehavior` nunca registra nome, e-mail nem telefone de contato
- [ ] Interfaces de port na camada Application sem dependência de Infrastructure
- [ ] Cobertura Application ≥ 85% nos arquivos desta TASK

---

### TASK-06 — Commands + Handlers de Contact + ListContactsQuery + Validators

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/account-management/06-application-contact-commands` |
| **Worktree** | `git worktree add ../worktrees/account-management/06-application-contact-commands -b feat/account-management/06-application-contact-commands` |
| **Status** | [ ] |
| **Depende de** | TASK-05 |
| **Entregável** | `CreateContactCommand`, `UpdateContactCommand`, `ForgetContactCommand`, `ListContactsQuery`, handlers e validators |
| **Mapeia** | Req 5, Req 7, Req 9, RNF 1, RNF 2, RNF 6, PBT-03, DD-001 |
| **Camada principal** | Application |

#### Objetivo

Implementar commands e query de contato. `ForgetContactCommand` exige papel Tenant Admin (`ForgetContactPolicy`), aplica anonimização via `Account.ForgetContact` e emite `ContactForgotten` sem PII (DD-001). `ListContactsQuery` é controlada por `PiiAccessBehavior`. `ContactLinked` carrega `maskedDelta` antes de ser despachado.

#### Subtasks

- [ ] **ST-01 — Red:** testes para `CreateContactCommand` (contato criado; `ContactLinked` com `maskedDelta` emitido; e-mail inválido → `ACC-ERR-004`; nome de contato vazio → `ACC-ERR-005`); `UpdateContactCommand` (PII atualizada; `ContactLinked` emitido; contato inexistente → `ACC-ERR-006`).
- [ ] **ST-02 — Red:** testes para `ForgetContactCommand` (Tenant Admin → anonimiza e emite `ContactForgotten`; Vendedor → `ACC-ERR-008`; contato inexistente → `ACC-ERR-006`; contato já anonimizado → `ACC-ERR-007` ou idempotente conforme DD-001).
- [ ] **ST-03 — Red:** testes para `ListContactsQuery` (Vendedor na BU → retorna PII; Viewer → `ACC-ERR-008`; isolamento de tenant — não retorna contatos de outro tenant).
- [ ] **ST-04 — Green:** implementar commands, handlers, `ListContactsQuery`; `ContactValidator` (FluentValidation); integrar `ForgetContactPolicy` e `PiiAccessBehavior`.
- [ ] **ST-05 — Refactor:** confirmar que `ContactLinked` carrega `maskedDelta` (sem PII em claro) antes de despachar ao Outbox.
- [ ] **ST-06 — Encerramento:** cobertura Application ≥ 85%; commit `feat(application): contact commands ForgetContact and ListContactsQuery` + push.

#### Critérios de Aceite

- [ ] `ForgetContactCommand` idempotente sobre contato já anonimizado (DD-001)
- [ ] `ContactLinked` e `ContactForgotten` sem PII em texto claro
- [ ] `ListContactsQuery` retorna 403 sem revelar PII quando papel insuficiente
- [ ] Validators retornam erros do catálogo `ACC-ERR-004`..`ACC-ERR-008`
- [ ] Cobertura Application ≥ 85% nos arquivos desta TASK

---

### TASK-07 — GetAccount360Query + Handler + degradação parcial + PBT-05

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `test/account-management/07-application-query-360` |
| **Worktree** | `git worktree add ../worktrees/account-management/07-application-query-360 -b test/account-management/07-application-query-360` |
| **Status** | [ ] |
| **Depende de** | TASK-06 |
| **Entregável** | `GetAccount360Query`/Handler com composição de portas de leitura, degradação parcial e PBT-05 verde |
| **Mapeia** | Req 6, PBT-05, DD-004 |
| **Camada principal** | Application |

#### Objetivo

Implementar `GetAccount360Query` que compõe dados próprios (conta + contatos) com leituras de `IOpportunityReadPort` (filtradas pelo escopo de BUs do usuário) e `IActivityReadPort`. Aplicar degradação parcial: falha de uma porta retorna seção indisponível sem quebrar a 360° inteira (DD-004, design §5.3). Cobrir com PBT-05 que o conjunto de oportunidades retornado é sempre subconjunto das BUs no escopo do usuário.

#### Subtasks

- [ ] **ST-01 — Red:** testes com mocks de portas de leitura: conta existente retorna 360° completa; `IOpportunityReadPort` falha → 360° parcial (seção de oportunidades como `null`/vazio com flag `unavailable`); `IActivityReadPort` falha → mesmo padrão de degradação.
- [ ] **ST-02 — Red PBT:** PBT-05 — para qualquer combinação gerada de BUs no escopo do usuário e oportunidades de múltiplas BUs, o resultado da 360° contém apenas oportunidades cujas BUs estão no escopo; gerar sets arbitrários de BUs autorizadas e não autorizadas com FsCheck.
- [ ] **ST-03 — Red:** teste de 404 (`ACC-ERR-003`) para conta inexistente ou fora do tenant.
- [ ] **ST-04 — Green:** implementar `GetAccount360Query` e `GetAccount360Handler`; integrar portas; implementar degradação parcial com timeout individual (design §15).
- [ ] **ST-05 — Refactor:** extrair lógica de degradação para método auxiliar; confirmar propagação de `correlation_id` às portas downstream.
- [ ] **ST-06 — Encerramento:** PBT-05 verde; cobertura Application ≥ 85%; commit `test(application): GetAccount360Query partial degradation PBT-05` + push.

#### Critérios de Aceite

- [ ] PBT-05 verde: oportunidades na 360° são subconjunto das BUs autorizadas ao usuário
- [ ] Falha de porta downstream não derruba a 360° inteira (degradação parcial documentada na resposta)
- [ ] `correlation_id` propagado às chamadas downstream
- [ ] Cobertura Application ≥ 85% nos arquivos desta TASK

---

### TASK-08 — DbContext + mappings EF Core + migrations accounts/contacts + índice normalizado + filtro global de tenant

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/account-management/08-infra-dbcontext-migrations` |
| **Worktree** | `git worktree add ../worktrees/account-management/08-infra-dbcontext-migrations -b feat/account-management/08-infra-dbcontext-migrations` |
| **Status** | [ ] |
| **Depende de** | TASK-07 |
| **Entregável** | `AccountManagementDbContext` com mappings, migrations de `accounts` e `contacts`, índice `idx_accounts_tenant_normalized_name` e filtro global por `tenant_id` |
| **Mapeia** | Req 2.3, Req 10.1, RNF 5, RNF 7.1, RNF 10.1, DD-002, DD-006 |
| **Camada principal** | Infrastructure |

#### Objetivo

Criar o `AccountManagementDbContext` com `IEntityTypeConfiguration<T>` para `Account` e `Contact` (nomes físicos `snake_case`, rule `database-naming.md`). Configurar filtro global de query por `tenant_id` (`HasQueryFilter`, resolvido via `TenantContext`). Gerar e aplicar migrations para `accounts` e `contacts` conforme schema do design §7. Criar o índice `idx_accounts_tenant_normalized_name` como não-unique (DD-006). Confirmar configuração de persistência em `southamerica-east1` (RNF 10.1).

#### Subtasks

- [ ] **ST-01 — Red:** testes de integração (Testcontainers + PostgreSQL): tabelas `accounts` e `contacts` existem com todas as colunas e constraints do design §7; índice `idx_accounts_tenant_normalized_name` existe e é não-unique; `idx_contacts_tenant_account` existe; constraint `chk_accounts_name_not_blank` ativa; constraint `chk_contacts_privacy_state` ativa.
- [ ] **ST-02 — Red:** testes de filtro global: inserir registros de dois tenants distintos no mesmo banco; query via DbContext com tenant A não retorna nenhum registro de tenant B.
- [ ] **ST-03 — Green:** implementar `AccountManagementDbContext`, `AccountEntityTypeConfiguration`, `ContactEntityTypeConfiguration`; gerar migration inicial; aplicar em banco de teste.
- [ ] **ST-04 — Green:** configurar `HasQueryFilter` com `TenantContext` no `DbContext`; implementar `TenantContext` como serviço scoped resolvendo `tenant_id` do contexto autenticado.
- [ ] **ST-05 — Refactor:** confirmar que `DbSet` não é exposto fora de Infrastructure; `accounts` e `contacts` sem `bu_id` (Req 2.3).
- [ ] **ST-06 — Encerramento:** migrations aplicadas; filtro global testado; cobertura Infrastructure ≥ 70%; commit `feat(infra): DbContext migrations accounts contacts tenant filter normalized name index` + push.

#### Critérios de Aceite

- [ ] Migration aplicada sem erros em PostgreSQL real (Testcontainers)
- [ ] Índice `idx_accounts_tenant_normalized_name` não-unique (DD-006)
- [ ] Filtro global por `tenant_id` impede retorno de dados de outro tenant via DbContext
- [ ] Tabelas `accounts` e `contacts` sem `bu_id` (Req 2.3)
- [ ] Cobertura Infrastructure ≥ 70% nos arquivos desta TASK

---

### TASK-09 — Migrations audit_logs (trigger + REVOKE) + outbox_messages + idempotency_keys

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/account-management/09-infra-audit-outbox-idempotency` |
| **Worktree** | `git worktree add ../worktrees/account-management/09-infra-audit-outbox-idempotency -b feat/account-management/09-infra-audit-outbox-idempotency` |
| **Status** | [ ] |
| **Depende de** | TASK-08 |
| **Entregável** | Migrations de `audit_logs` (trigger de imutabilidade + REVOKE), `outbox_messages` e `idempotency_keys` |
| **Mapeia** | Req 8.5, RNF 8.1..8.3, DD-007 |
| **Camada principal** | Infrastructure |

#### Objetivo

Criar migrations para as três tabelas de suporte: `audit_logs` (append-only — trigger `BEFORE UPDATE OR DELETE OR TRUNCATE` + `REVOKE UPDATE, DELETE, TRUNCATE ON audit_logs FROM app`, sem `updated_at` — RNF 8); `outbox_messages` (com índice parcial `idx_outbox_unpublished` para relay eficiente); `idempotency_keys` (PK composta `tenant_id + idempotency_key`).

#### Subtasks

- [ ] **ST-01 — Red:** testes de integração: `audit_logs` com colunas `id`, `tenant_id`, `user_id`, `entity_type`, `entity_id`, `action`, `delta_json`, `created_at` (sem `updated_at`); tentativa de UPDATE sobre `audit_logs` → erro de permissão; índice `idx_audit_logs_tenant_entity` existe; `outbox_messages` com índice parcial; `idempotency_keys` com PK composta.
- [ ] **ST-02 — Red:** teste de que `REVOKE UPDATE, DELETE, TRUNCATE` está aplicado ao role `app` sobre `audit_logs` (query `information_schema.role_table_grants`).
- [ ] **ST-03 — Green:** criar migration de `audit_logs` com função PL/pgSQL `prevent_immutable_table_modification()` e trigger; migration de `outbox_messages`; migration de `idempotency_keys`.
- [ ] **ST-04 — Refactor:** confirmar que `audit_logs` não tem `updated_at`; confirmar que `outbox_messages` tem índice apenas sobre registros com `published_at IS NULL`.
- [ ] **ST-05 — Encerramento:** testes de imutabilidade verdes; cobertura Infrastructure ≥ 70%; commit `feat(infra): audit_logs immutable migration trigger REVOKE outbox idempotency` + push.

#### Critérios de Aceite

- [ ] UPDATE/DELETE em `audit_logs` pelo role `app` falha com erro de permissão (Testcontainers)
- [ ] Trigger `trg_audit_logs_immutable` ativo e funcional
- [ ] `audit_logs` sem coluna `updated_at` (append-only por definição)
- [ ] `outbox_messages` com índice parcial para registros pendentes
- [ ] Cobertura Infrastructure ≥ 70% nos arquivos desta TASK

---

### TASK-10 — IAccountRepository implementação + TenantContext + Testcontainers + PBT-04 gate CI

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `test/account-management/10-infra-repository-tenant-isolation` |
| **Worktree** | `git worktree add ../worktrees/account-management/10-infra-repository-tenant-isolation -b test/account-management/10-infra-repository-tenant-isolation` |
| **Status** | [ ] |
| **Depende de** | TASK-09 |
| **Entregável** | `AccountRepository` implementado, testes de integração com Testcontainers e PBT-04 configurado como gate CI obrigatório |
| **Mapeia** | Req 10, RNF 5.1..5.3, PBT-04, DD-002 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar `AccountRepository` (implementação concreta de `IAccountRepository`) carregando o agregado `Account` com seus `Contact`. Escrever PBT-04 anti-cross-tenant como teste de integração com PostgreSQL real (Testcontainers): para qualquer conjunto gerado de contas/contatos de tenants distintos, nenhuma operação de repositório no contexto de um tenant retorna dados de outro tenant. Configurar PBT-04 como gate CI obrigatório (RNF 5.2).

#### Subtasks

- [ ] **ST-01 — Red:** testes de integração (Testcontainers): `GetByIdAsync` retorna `null` para `account_id` de outro tenant; `SearchAsync` por nome normalizado não retorna contas de outro tenant; `SearchSimilarAsync` opera exclusivamente no tenant do contexto.
- [ ] **ST-02 — Red PBT:** PBT-04 — gerar arbitrariamente N tenants com M contas cada; para qualquer tenant escolhido, nenhuma operação de repositório retorna conta ou contato de outro tenant; rodar com FsCheck + Testcontainers (mínimo 100 combinações geradas).
- [ ] **ST-03 — Green:** implementar `AccountRepository` com `GetByIdAsync`, `SearchAsync`, `SearchSimilarAsync`, `SaveAsync`; carregar contatos via EF Core `Include`; nunca expor `IQueryable`.
- [ ] **ST-04 — Refactor:** confirmar que filtro global `HasQueryFilter` está sempre ativo; adicionar teste que falha se o filtro for desativado com `IgnoreQueryFilters`.
- [ ] **ST-05 — Encerramento:** PBT-04 verde e pipeline YAML configurado como gate CI; cobertura Infrastructure ≥ 70%; commit `test(infra): AccountRepository tenant isolation PBT-04 CI gate` + push.

#### Critérios de Aceite

- [ ] PBT-04 verde com ≥ 100 combinações de tenants geradas
- [ ] PBT-04 configurado como step obrigatório no CI (falha o build se quebrar)
- [ ] `AccountRepository` nunca expõe `IQueryable` fora de Infrastructure
- [ ] Cobertura Infrastructure ≥ 70% nos arquivos desta TASK

---

### TASK-11 — PiiMasker + AuditPublisher + Outbox relay + Pub/Sub

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/account-management/11-infra-pii-masker-outbox-pubsub` |
| **Worktree** | `git worktree add ../worktrees/account-management/11-infra-pii-masker-outbox-pubsub -b feat/account-management/11-infra-pii-masker-outbox-pubsub` |
| **Status** | [ ] |
| **Depende de** | TASK-10 |
| **Entregável** | `PiiMasker` centralizado, `AuditPublisher` (implementação de `IAuditPublisher`), Outbox relay e integração Cloud Pub/Sub |
| **Mapeia** | Req 8, RNF 1, RNF 8, DD-003, DD-007 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar `PiiMasker` como componente centralizado (DD-003) aplicado em três pontos: serialização de `delta_json` de auditoria, construção de `ContactLinked.maskedDelta` e enriquecimento de logs/traces. Implementar `AuditPublisher` gravando em `audit_logs` (append-only). Implementar relay do Outbox que publica `outbox_messages` pendentes no Cloud Pub/Sub com at-least-once, propagação de `correlation_id` e `tenant_id`.

#### Subtasks

- [ ] **ST-01 — Red:** testes para `PiiMasker`: `MaskContactDelta(json)` substitui campos `name`, `email`, `phone` por marcadores; `delta_json` resultante não contém nenhum valor de PII original; campos de não-PII (`role`, `account_id`, `action`) são preservados.
- [ ] **ST-02 — Red:** testes para `AuditPublisher`: `PublishAsync(event)` grava linha em `audit_logs` com `delta_json` mascarado; segunda chamada com mesmos dados gera nova linha (append-only, sem deduplicação no publisher).
- [ ] **ST-03 — Red:** testes para relay do Outbox: mensagens com `published_at IS NULL` são lidas e publicadas no Pub/Sub; após publicação, `published_at` é preenchido; falha de publicação não marca como publicado (retry na próxima execução).
- [ ] **ST-04 — Green:** implementar `PiiMasker`, `AuditPublisher`, `OutboxPublisher` (relay como `IHostedService`), integração com Cloud Pub/Sub.
- [ ] **ST-05 — Refactor:** confirmar que payload Pub/Sub nunca carrega PII; envelope de evento inclui `event_id`, `event_type`, `event_version`, `tenant_id`, `correlation_id`, `occurred_at`.
- [ ] **ST-06 — Encerramento:** cobertura Infrastructure ≥ 70%; commit `feat(infra): PiiMasker AuditPublisher Outbox relay PubSub` + push.

#### Critérios de Aceite

- [ ] `PiiMasker` nunca deixa `name`/`email`/`phone` em texto claro no `delta_json`
- [ ] `audit_logs` é append-only — publisher nunca faz UPDATE
- [ ] Relay do Outbox com retry; `published_at` preenchido somente após confirmação do Pub/Sub
- [ ] Payload Pub/Sub sem PII em texto claro; envelope completo com `event_id` e `correlation_id`
- [ ] Cobertura Infrastructure ≥ 70% nos arquivos desta TASK

---

### TASK-12 — ReadPorts adapters (Opportunity, Activity) + resiliência + IdempotencyKey

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/account-management/12-infra-read-ports-resilience` |
| **Worktree** | `git worktree add ../worktrees/account-management/12-infra-read-ports-resilience -b feat/account-management/12-infra-read-ports-resilience` |
| **Status** | [ ] |
| **Depende de** | TASK-11 |
| **Entregável** | `OpportunityReadAdapter`, `ActivityReadAdapter` (mTLS interno), resiliência (timeout + retry + circuit breaker) e `IdempotencyKeyRepository` |
| **Mapeia** | Req 6, RNF (resiliência), DD-004 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar os adaptadores de leitura para opportunity-pipeline e activity-management (HTTP/gRPC interno com mTLS, `mtls-internal-services.md`). Configurar resiliência com Polly: timeout individual, retry com backoff exponencial e circuit breaker por endpoint. Implementar `IdempotencyKeyRepository` para deduplicação de escritas na tabela `idempotency_keys`.

#### Subtasks

- [ ] **ST-01 — Red:** testes para `OpportunityReadAdapter` com servidor HTTP stub: chamada bem-sucedida retorna oportunidades filtradas por BUs; timeout → retorna resultado de degradação (vazio + flag `unavailable`); chamadas propagam `correlation_id` e `tenant_id`.
- [ ] **ST-02 — Red:** testes para `ActivityReadAdapter` com mesmo padrão de resiliência e propagação de contexto.
- [ ] **ST-03 — Red:** testes para `IdempotencyKeyRepository`: chave nova → registra e retorna `null` (nova operação); mesma chave + payload igual → retorna referência anterior; mesma chave + payload divergente → `ACC-ERR-009`.
- [ ] **ST-04 — Green:** implementar adaptadores com `HttpClientFactory` + Polly; `IdempotencyKeyRepository` persistindo em `idempotency_keys`.
- [ ] **ST-05 — Refactor:** confirmar que `TenantContext` e `correlation_id` são propagados nos headers downstream; sem PII nos logs de resiliência (retry/timeout).
- [ ] **ST-06 — Encerramento:** cobertura Infrastructure ≥ 70%; commit `feat(infra): ReadPorts adapters resilience IdempotencyKey` + push.

#### Critérios de Aceite

- [ ] `OpportunityReadAdapter` e `ActivityReadAdapter` propagam `correlation_id` e `tenant_id`
- [ ] Timeout/retry/circuit breaker configurados e testados com servidor stub
- [ ] `IdempotencyKeyRepository` detecta payload divergente e retorna `ACC-ERR-009`
- [ ] Cobertura Infrastructure ≥ 70% nos arquivos desta TASK

---

### TASK-13 — AccountsController + DTOs de conta + middleware + erros ACC-ERR-001/002/003/009

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `feat/account-management/13-api-accounts-controller` |
| **Worktree** | `git worktree add ../worktrees/account-management/13-api-accounts-controller -b feat/account-management/13-api-accounts-controller` |
| **Status** | [ ] |
| **Depende de** | TASK-12 |
| **Entregável** | `AccountsController` com `GET /accounts`, `POST /accounts`, `GET /accounts/{id}`, `PATCH /accounts/{id}`, DTOs de request/response, middleware de `CorrelationId` e tratamento de exceção padronizado |
| **Mapeia** | Req 1, Req 3, Req 4, RNF 7, design §8 (catálogo de erros ACC-ERR-001/002/003/009) |
| **Camada principal** | Api |

#### Objetivo

Implementar `AccountsController` com os 4 endpoints de conta conforme design §8. Criar DTOs em `AccountManagement.Contracts` (request/response, sem PII de contato). Configurar middleware de `CorrelationId` e exception handler padronizado (formato `{ "error", "code", "correlationId" }` — rule `api-and-contracts.md`). Cobrir fluxo de dedupe: `GET /accounts?search=` retorna candidatos similares; `POST /accounts` com `confirmCreateDespiteSimilar=true` cria sem bloquear.

#### Subtasks

- [ ] **ST-01 — Red:** testes de controller (WebApplicationFactory ou TestServer): `GET /accounts?search=pag.ai` retorna lista paginada; `POST /accounts` com nome vazio → 400 `ACC-ERR-001`; `GET /accounts/{id}` de outro tenant → 404 `ACC-ERR-003`; `PATCH /accounts/{id}` com nome vazio → 400 `ACC-ERR-001`; `Idempotency-Key` reutilizada com payload divergente → 409 `ACC-ERR-009`.
- [ ] **ST-02 — Red:** testes de middleware: cada request contém `X-Correlation-Id` na resposta; erros sempre retornam `{ "error", "code", "correlationId" }`.
- [ ] **ST-03 — Green:** implementar `AccountsController`; DTOs `CreateAccountRequest`, `UpdateAccountRequest`, `AccountResponse`, `AccountPageResponse`; middleware `CorrelationIdMiddleware` e `ExceptionHandlingMiddleware`.
- [ ] **ST-04 — Refactor:** confirmar que responses não incluem `bu_id` (Req 2.3); paginação por `page`/`pageSize` conforme rule `api-and-contracts.md`.
- [ ] **ST-05 — Docs:** anotar endpoints com atributos OpenAPI (resumo, resposta, erros).
- [ ] **ST-06 — Encerramento:** cobertura Api ≥ 80%; commit `feat(api): AccountsController DTOs middleware error catalog` + push.

#### Critérios de Aceite

- [ ] `POST /accounts` nunca bloqueia por similaridade (dedupe é consultivo via `GET /accounts?search=`)
- [ ] Erros retornam formato padronizado com `correlationId`
- [ ] `GET /accounts` retorna somente contas do tenant autenticado
- [ ] `Idempotency-Key` duplicada com payload divergente → 409 `ACC-ERR-009`
- [ ] Cobertura Api ≥ 80% nos arquivos desta TASK

---

### TASK-14 — ContactsController + DTOs + RBAC + ForgetContact + erros ACC-ERR-004..008 (PBT-03)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `feat/account-management/14-api-contacts-controller` |
| **Worktree** | `git worktree add ../worktrees/account-management/14-api-contacts-controller -b feat/account-management/14-api-contacts-controller` |
| **Status** | [ ] |
| **Depende de** | TASK-13 |
| **Entregável** | `ContactsController` com `GET`, `POST`, `PATCH` e `DELETE` (ForgetContact), DTOs de contato, RBAC enforcement e PBT-03 nos testes de Api |
| **Mapeia** | Req 5, Req 7, Req 9, RNF 1, RNF 6, PBT-03, DD-001, design §8 (erros ACC-ERR-004..008) |
| **Camada principal** | Api |

#### Objetivo

Implementar `ContactsController` com os 4 endpoints de contato. `DELETE /accounts/{accountId}/contacts/{id}` é semanticamente "anonimizar" (DD-001): responde 204 e `contact_id` permanece válido; PII não recuperável após a operação. PII só é retornada após `PiiAccessBehavior` (papel mínimo Vendedor na BU). Cobrir PBT-03 na camada de Api: nenhum endpoint retorna PII de contato anonimizado.

#### Subtasks

- [ ] **ST-01 — Red:** testes de controller: `GET /accounts/{id}/contacts` com Viewer → 403 `ACC-ERR-008` (sem PII); com Vendedor → 200 com PII; `POST /accounts/{id}/contacts` com e-mail inválido → 400 `ACC-ERR-004`; nome de contato vazio → 400 `ACC-ERR-005`.
- [ ] **ST-02 — Red:** testes para `DELETE /accounts/{accountId}/contacts/{id}`: Tenant Admin → 204 sem PII no body; Vendedor → 403 `ACC-ERR-008`; contato inexistente → 404 `ACC-ERR-006`; contato já anonimizado → 409 `ACC-ERR-007`.
- [ ] **ST-03 — Red PBT:** PBT-03 em Api.Tests — após `DELETE`, nenhum `GET` dos endpoints de contato retorna PII original do contato anonimizado; `contact_id` aparece como referência válida em respostas relacionadas.
- [ ] **ST-04 — Green:** implementar `ContactsController`; DTOs `CreateContactRequest`, `UpdateContactRequest`, `ContactResponse` (com PII mascarada quando não autorizado); integrar `PiiAccessBehavior` e `ForgetContactPolicy`.
- [ ] **ST-05 — Refactor:** confirmar que resposta 403 não vaza existência do contato além do necessário (anti-enumeração); PII mascarada ou ausente quando papel insuficiente.
- [ ] **ST-06 — Encerramento:** PBT-03 verde em Api.Tests; cobertura Api ≥ 80%; commit `feat(api): ContactsController RBAC ForgetContact PBT-03` + push.

#### Critérios de Aceite

- [ ] PBT-03 verde em Api.Tests: nenhum endpoint retorna PII após anonimização
- [ ] `DELETE` de contato retorna 204 e preserva `contact_id` válido (DD-001)
- [ ] 403 não revela existência do contato além do necessário (anti-enumeração)
- [ ] Todos os erros ACC-ERR-004..008 testados e retornando formato padronizado
- [ ] Cobertura Api ≥ 80% nos arquivos desta TASK

---

### TASK-15 — Endpoint 360° + contratos de evento + OpenAPI completo + contract tests (PBT-05)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `feat/account-management/15-api-360-events-openapi` |
| **Worktree** | `git worktree add ../worktrees/account-management/15-api-360-events-openapi -b feat/account-management/15-api-360-events-openapi` |
| **Status** | [ ] |
| **Depende de** | TASK-14 |
| **Entregável** | `GET /accounts/{id}/360` implementado, contratos de evento v1, especificação OpenAPI completa e contract tests (Pact ou equivalente) |
| **Mapeia** | Req 6, Req 8, PBT-05, design §8 (360°), design §9 (eventos) |
| **Camada principal** | Api, Contracts |

#### Objetivo

Implementar `GET /accounts/{id}/360` com resposta `{ account, contacts[], opportunities[] (escopo BU), activities[], history[] (cronológico decrescente) }` e degradação parcial sinalizada (design §8). Publicar contratos de evento `account.created.v1`, `account.updated.v1`, `account.contact_linked.v1`, `account.contact_forgotten.v1` em `AccountManagement.Contracts/Events`. Cobrir PBT-05 em Api.Tests e escrever contract tests do provider para opportunity-pipeline/reporting.

#### Subtasks

- [ ] **ST-01 — Red:** testes de controller para `GET /accounts/{id}/360`: Viewer com acesso à BU A → vê oportunidades de BU A, não de BU B; porta downstream indisponível → 200 com campo `unavailable: true` na seção afetada; conta fora do tenant → 404 `ACC-ERR-003`.
- [ ] **ST-02 — Red PBT:** PBT-05 em Api.Tests — gerar sets arbitrários de BUs autorizadas; resposta da 360° contém apenas oportunidades das BUs autorizadas; sem oportunidades de BUs fora do escopo.
- [ ] **ST-03 — Red:** contract tests (Pact Provider): busca de contas (`GET /accounts?search=`) segue contrato do consumidor opportunity-pipeline; eventos `account.created.v1` e `account.contact_forgotten.v1` aderem ao schema publicado.
- [ ] **ST-04 — Green:** implementar `GET /accounts/{id}/360`; publicar DTOs de evento em `Contracts/Events`; completar anotações OpenAPI em todos os controllers; gerar `openapi.json`.
- [ ] **ST-05 — Refactor:** confirmar que histórico da 360° está em ordem cronológica decrescente (Req 6.4); confirmar que eventos de contato não carregam PII.
- [ ] **ST-06 — Encerramento:** PBT-05 verde em Api.Tests; contract tests verdes; cobertura Api ≥ 80%; commit `feat(api): account 360 endpoint event contracts OpenAPI PBT-05` + push.

#### Critérios de Aceite

- [ ] PBT-05 verde em Api.Tests: oportunidades na 360° restritas ao escopo de BUs autorizadas
- [ ] Degradação parcial sinalizada na resposta sem erro HTTP (200 com flag de seção indisponível)
- [ ] Contratos de evento v1 publicados em `Contracts/Events` sem PII
- [ ] Contract tests (Pact provider) verdes para `GET /accounts?search=`
- [ ] OpenAPI gerado e disponível no endpoint `/swagger`
- [ ] Cobertura Api ≥ 80% nos arquivos desta TASK

---

### TASK-16 — Observabilidade — métricas + logs estruturados + traces + alertas + health (RNF 9)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `feat/account-management/16-hardening-observability` |
| **Worktree** | `git worktree add ../worktrees/account-management/16-hardening-observability -b feat/account-management/16-hardening-observability` |
| **Status** | [ ] |
| **Depende de** | TASK-15 |
| **Entregável** | Métricas Prometheus obrigatórias, logs estruturados sem PII, traces OpenTelemetry, alertas e endpoints de health |
| **Mapeia** | RNF 9.1..9.3, RNF 1, design §11 |
| **Camada principal** | Api, Infrastructure |

#### Objetivo

Garantir os três pilares de observabilidade (design §11): métricas `accounts_created_total`, `contacts_created_total`, `dedupe_blocked_total`, `http_requests_total`, `http_request_duration_seconds` (p50/p95/p99), `domain_events_published_total`; logs estruturados JSON com `correlation_id`, `tenant_id`, `account_id` e sem PII; traces OpenTelemetry em handlers e portas; alertas (taxa de erro criação de conta > 5%, falha de relay do Outbox, latência 360° acima do SLO p95 < 800 ms); endpoints de health/readiness/liveness.

#### Subtasks

- [ ] **ST-01 — Red:** testes verificando que `accounts_created_total` é incrementada após `CreateAccountCommand` bem-sucedido; `dedupe_blocked_total` é incrementada quando `SearchSimilarAccountsQuery` retorna candidatos; `contacts_created_total` é incrementada após `CreateContactCommand`.
- [ ] **ST-02 — Red:** testes de logs: após criar contato com nome e e-mail reais, varrer logs produzidos — nenhuma linha de log contém o nome ou e-mail do contato em texto claro (`CorrelationLoggingBehavior` + `PiiMasker`).
- [ ] **ST-03 — Green:** configurar Prometheus (via `prometheus-net` ou equivalente); configurar OpenTelemetry (spans para handlers, portas, operações de banco); implementar health/readiness/liveness checks (Cloud SQL, Pub/Sub).
- [ ] **ST-04 — Green:** configurar alertas (Cloud Monitoring ou equivalente): erro de criação de conta > 5%; falha do relay de Outbox; latência p95 da 360° > 800 ms.
- [ ] **ST-05 — Refactor:** confirmar que `correlation_id` aparece como atributo root do trace; sem PII em atributos de span.
- [ ] **ST-06 — Encerramento:** métricas verificadas; alertas documentados; commit `feat(hardening): observability metrics traces alerts health` + push.

#### Critérios de Aceite

- [ ] Métricas `accounts_created_total`, `contacts_created_total`, `dedupe_blocked_total` expostas em `/metrics`
- [ ] Logs estruturados com `correlation_id`, `tenant_id`; sem PII de contato em texto claro
- [ ] Traces com `correlation_id` como atributo root
- [ ] Alertas configurados para taxa de erro > 5% e latência 360° > 800 ms p95
- [ ] Endpoints `/health/ready` e `/health/live` funcionais

---

### TASK-17 — Scan anti-PII em logs + isolamento de tenant como gate CI (RNF 1.4, PBT-04)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `test/account-management/17-hardening-pii-scan-tenant-gate` |
| **Worktree** | `git worktree add ../worktrees/account-management/17-hardening-pii-scan-tenant-gate -b test/account-management/17-hardening-pii-scan-tenant-gate` |
| **Status** | [ ] |
| **Depende de** | TASK-16 |
| **Entregável** | Teste automatizado de scan anti-PII que falha o CI se PII de contato for detectada em logs; PBT-04 consolidado como gate CI obrigatório |
| **Mapeia** | RNF 1.4, RNF 5.2, PBT-04 |
| **Camada principal** | Tests |

#### Objetivo

Implementar teste de scan de logs (RNF 1.4): em um ciclo completo de criação e edição de contato com dados de PII reais, varrer todos os logs produzidos e falhar o teste se qualquer valor de PII (nome, e-mail, telefone) aparecer em texto claro. Confirmar que PBT-04 (anti-cross-tenant) está corretamente configurado como gate obrigatório no pipeline CI, falhando o build se quebrar. Validar mascaramento de PII em `delta_json` de `audit_logs`.

#### Subtasks

- [ ] **ST-01 — Red:** escrever teste de scan de logs: executar `CreateContactCommand` com nome, e-mail e telefone reais; capturar output de logs (sink de teste); varrer linhas de log — se qualquer linha contiver o valor real de PII em texto claro, o teste falha imediatamente.
- [ ] **ST-02 — Red:** escrever teste que inspeciona `audit_logs.delta_json` após criação de contato: campo `name`, `email` e `phone` não podem conter o valor original — devem aparecer como marcadores.
- [ ] **ST-03 — Green:** ajustar `PiiMasker` e `CorrelationLoggingBehavior` até que o scan de logs passe; corrigir qualquer ponto de vazamento identificado.
- [ ] **ST-04 — Green:** confirmar configuração de CI: PBT-04 (`Infrastructure.Tests`) como step obrigatório que falha o build; scan anti-PII (`Security.Tests` ou `Integration.Tests`) como step obrigatório.
- [ ] **ST-05 — Encerramento:** scan anti-PII verde; PBT-04 configurado no CI; commit `test(hardening): anti-PII log scan and tenant isolation CI gate` + push.

#### Critérios de Aceite

- [ ] Teste de scan de logs falha se qualquer valor de PII aparecer em texto claro nos logs
- [ ] `audit_logs.delta_json` não contém PII original após criação/edição de contato
- [ ] PBT-04 e scan anti-PII configurados como gates CI obrigatórios (build falha se quebrar)
- [ ] Nenhum warning ou exceção silenciada para contornar o scan

---

### TASK-18 — RBAC final + anti-enumeração + DoD + approvals.yaml + residência de dados (RNF 10)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `feat/account-management/18-hardening-rbac-dod` |
| **Worktree** | `git worktree add ../worktrees/account-management/18-hardening-rbac-dod -b feat/account-management/18-hardening-rbac-dod` |
| **Status** | [ ] |
| **Depende de** | TASK-17 |
| **Entregável** | Revisão de RBAC em todos os endpoints de contato; teste de anti-enumeração; DoD checklist completo; `approvals.yaml` com pendências registradas; confirmação de residência de dados |
| **Mapeia** | Req 7.1, Req 9, RNF 3, RNF 4, RNF 6, RNF 10, PBT-04 (anti-enumeração), DD-001 |
| **Camada principal** | Api, Tests, Docs |

#### Objetivo

Realizar revisão de segurança final: confirmar RBAC em todos os endpoints de contato (Vendedor na BU para PII; Tenant Admin para esquecimento); verificar que respostas de erro não permitem enumeração de entidades (403 e 404 indistinguíveis quando necessário — Req 9, design §10); registrar pendências jurídicas VAL-ACC-01/02/03 no `approvals.yaml`; confirmar que a string de conexão aponta para `southamerica-east1` e que não há replicação cross-region sem ADR. Completar DoD checklist do design §19.

#### Subtasks

- [ ] **ST-01 — Red:** testes de anti-enumeração: usuário sem acesso à BU tentando `GET /accounts/{id}/contacts` deve receber 403 sem revelar se o contato existe ou não além do necessário; `DELETE /accounts/{accountId}/contacts/{id}` por Vendedor → 403 sem vazar estado do contato.
- [ ] **ST-02 — Red:** testes de RBAC em todos os endpoints de contato: varredura de matriz papel × endpoint, confirmando que nenhum papel abaixo do mínimo recebe PII ou executa operação proibida.
- [ ] **ST-03 — Green:** corrigir qualquer lacuna de RBAC ou anti-enumeração identificada nos testes ST-01/ST-02; atualizar `PiiAccessBehavior` se necessário.
- [ ] **ST-04 — Docs:** preencher `approvals.yaml` com pendências VAL-ACC-01 (política de retenção/descarte — RNF 4), VAL-ACC-02 (base legal LGPD — RNF 3), VAL-ACC-03 (regra conta por tenant, não por BU); confirmar string de conexão em `southamerica-east1` (RNF 10.1); completar DoD checklist do design §19.
- [ ] **ST-05 — Encerramento:** testes de RBAC e anti-enumeração verdes; DoD completo; commit `feat(hardening): RBAC review anti-enumeration DoD approvals` + push da onda 6.

#### Critérios de Aceite

- [ ] 403 em endpoints de contato não revela existência de entidade além do necessário (anti-enumeração)
- [ ] Matriz papel × endpoint de contato testada e sem lacuna de RBAC
- [ ] `approvals.yaml` com VAL-ACC-01, VAL-ACC-02, VAL-ACC-03 registrados
- [ ] String de conexão confirmada em `southamerica-east1`; sem replicação cross-region sem ADR
- [ ] DoD checklist do design §19 completo

---

## 5. Matriz de Rastreabilidade

| Origem | Descrição | TASKs | Status |
|--------|-----------|-------|--------|
| Req 1 | Criar conta com dedupe por nome normalizado | TASK-02, TASK-03, TASK-04, TASK-08, TASK-13 | [ ] |
| Req 2 | Conta única por tenant compartilhada entre BUs | TASK-03, TASK-08, TASK-13 | [ ] |
| Req 3 | Buscar e visualizar contas | TASK-04, TASK-08, TASK-13 | [ ] |
| Req 4 | Editar conta | TASK-04, TASK-13 | [ ] |
| Req 5 | Gerenciar contatos (PII) | TASK-02, TASK-03, TASK-06, TASK-14 | [ ] |
| Req 6 | Visão 360° da conta | TASK-07, TASK-12, TASK-15 | [ ] |
| Req 7 | Direito ao esquecimento (LGPD Art. 18) | TASK-03, TASK-06, TASK-14 | [ ] |
| Req 8 | Auditoria imutável de escrita | TASK-09, TASK-11, TASK-17 | [ ] |
| Req 9 | Controle de acesso a PII por RBAC | TASK-05, TASK-06, TASK-14, TASK-18 | [ ] |
| Req 10 | Isolamento de dados por tenant | TASK-05, TASK-08, TASK-10, TASK-18 | [ ] |
| RNF 1 | PII de contatos ausente de logs | TASK-02, TASK-05, TASK-11, TASK-17 | [ ] |
| RNF 2 | Minimização de dados pessoais | TASK-02, TASK-08, TASK-14 | [ ] |
| RNF 3 | Base legal LGPD (pendência) | TASK-18 (VAL-ACC-02 em approvals.yaml) | [ ] |
| RNF 4 | Política de retenção/descarte (pendência) | TASK-18 (VAL-ACC-01 em approvals.yaml) | [ ] |
| RNF 5 | Isolamento multi-tenant em profundidade | TASK-05, TASK-10, TASK-17 | [ ] |
| RNF 6 | Acesso a PII restrito por RBAC | TASK-05, TASK-06, TASK-14, TASK-18 | [ ] |
| RNF 7 | Performance de busca (índice normalizado) | TASK-08 | [ ] |
| RNF 8 | Imutabilidade de registros de auditoria | TASK-09, TASK-11 | [ ] |
| RNF 9 | Observabilidade do módulo | TASK-16 | [ ] |
| RNF 10 | Residência de dados no Brasil | TASK-08, TASK-18 | [ ] |
| PBT-01 | Idempotência da normalização de nome | TASK-02 | [ ] |
| PBT-02 | Equivalência de nomes pela forma normalizada | TASK-02, TASK-04 | [ ] |
| PBT-03 | Irreversibilidade da anonimização preservando referência | TASK-03, TASK-06, TASK-14 | [ ] |
| PBT-04 | Anti-cross-tenant em contas e contatos | TASK-10, TASK-17 | [ ] |
| PBT-05 | Visão 360° respeita escopo de BUs do usuário | TASK-07, TASK-15 | [ ] |
| DD-001 | Anonimização in-place (esquecimento) | TASK-03, TASK-06, TASK-09 | [ ] |
| DD-002 | Isolamento por coluna + filtro global; RLS pendente de ADR | TASK-08, TASK-10 | [ ] |
| DD-003 | PiiMasker centralizado | TASK-11, TASK-17 | [ ] |
| DD-004 | Composição síncrona da 360° via portas de leitura | TASK-07, TASK-12 | [ ] |
| DD-005 | Normalização determinística exata, sem fuzzy | TASK-02 | [ ] |
| DD-006 | Dedupe não-bloqueante; índice não-unique | TASK-03, TASK-04, TASK-08 | [ ] |
| DD-007 | Publicação de eventos via Outbox transacional + Pub/Sub | TASK-09, TASK-11 | [ ] |

---

## 6. Coverage Gates

| Camada | Gate | Tipo de teste esperado |
|--------|------|------------------------|
| Domain (`AccountManagement.Domain.Tests`) | ≥ 95% | Unitários de agregado, objetos de valor, normalização, state machine + PBT-01/02/03 |
| Application (`AccountManagement.Application.Tests`) | ≥ 85% | Unitários de handlers, behaviors (validação, PII, tenant, tx), dedupe, composição 360° + PBT-05 |
| Infrastructure (`AccountManagement.Infrastructure.Tests`) | ≥ 70% | Integração com Testcontainers/PostgreSQL: repositório, filtro global, Outbox, trigger de imutabilidade, PiiMasker + PBT-04 (gate CI obrigatório) |
| Api (`AccountManagement.Api.Tests`) | ≥ 80% | Contrato, integração, RBAC nos endpoints, formato de erro, degradação parcial 360° + PBT-03/05 em Api |
| Architecture (`AccountManagement.Architecture.Tests`) | 100% das regras | Regras de dependência entre camadas Clean Architecture |
| Security | Cobertura por cenário crítico | Anti-enumeração, negação de PII por papel, scan anti-PII em logs (gate CI), `ForgetContactPolicy` |
| Contract | Cobertura por consumidor | Pact provider para opportunity-pipeline (`GET /accounts?search=`) e eventos v1 |

Regras:

- Coverage gate não substitui qualidade de teste.
- PBT-04 (anti-cross-tenant) é gate CI obrigatório — falha o build se quebrar.
- Scan anti-PII (RNF 1.4) é gate CI obrigatório — falha o build se PII detectada.
- Testes de arquitetura são obrigatórios e fazem parte do gate CI desde a Onda 1.
- Testes de contrato são obrigatórios para `GET /accounts?search=` (consumido pelo opportunity-pipeline).

---

## 7. Critérios de Encerramento

### 7.1 Encerramento de TASK

Uma TASK só pode ser marcada como `[X]` quando:

- todas as subtasks em `[X]`
- testes aplicáveis verdes (unitários, PBTs, integração conforme camada)
- coverage gate da camada atendido ou justificativa registrada
- `dotnet format` executado sem warnings novos
- nenhum PII em texto claro identificado nos logs/traces/eventos cobertos pela TASK
- commit em Conventional Commits realizado
- push da branch realizado
- documentação atualizada quando aplicável

### 7.2 Encerramento de Onda

Uma onda só pode ser considerada concluída quando:

- todas as TASKs da onda em `[X]`
- CI verde (incluindo gates PBT-04 e scan anti-PII quando aplicável)
- PR da onda aberto e revisado conforme processo do projeto
- riscos da onda tratados ou registrados
- README do módulo sincronizado com status atual

### 7.3 Encerramento do Módulo

O módulo só pode ser considerado pronto para go-live quando:

- todas as 6 ondas concluídas
- matriz de rastreabilidade completa (todas as origens com status `[X]`)
- PBT-01..05 verdes
- PBT-04 configurado como gate CI ativo
- scan anti-PII configurado como gate CI ativo
- `requirements.md`, `design.md` e `tasks.md` consistentes
- catálogo de erros ACC-ERR-001..009 coberto por testes
- métricas e alertas do design §11 operacionais
- `approvals.yaml` com VAL-ACC-01 e VAL-ACC-02 aprovados pela equipe jurídica antes do go-live
- DD-001 confirmada formalmente antes do go-live
- persistência confirmada em `southamerica-east1` (RNF 10)

---

## 8. Riscos de Execução

| Código | Risco | Probabilidade | Impacto | Mitigação de execução |
|--------|-------|---------------|---------|----------------------|
| RISK-EXEC-01 | `PiiMasker` não cobrir todos os pontos de exposição de PII (logs, traces, delta_json, payload) | Média | Violação LGPD | Teste de scan anti-PII como gate CI obrigatório (TASK-17); `PiiMasker` centralizado (DD-003) |
| RISK-EXEC-02 | Filtro global de tenant desativado acidentalmente (`IgnoreQueryFilters`) em algum path | Baixa | Incidente sev-1 (cross-tenant) | PBT-04 como gate CI (TASK-10); teste explícito de regressão de filtro global |
| RISK-EXEC-03 | Portabilidade do PBT-04 com Testcontainers em CI (tempo de boot de container) | Média | Instabilidade de CI / timeout | Configurar pool de containers; aumentar timeout do step; usar imagem PostgreSQL fixada |
| RISK-EXEC-04 | Pendências jurídicas VAL-ACC-01/02 não resolvidas antes do go-live | Alta | Não conformidade com LGPD | Registrar em `approvals.yaml`; criar bloqueador de release até aprovação; TASK-18 |
| RISK-EXEC-05 | Degradação parcial da 360° não sinalizada corretamente → usuário recebe dados incompletos sem aviso | Média | Experiência de produto degradada | Testes de degradação parcial em TASK-07 e TASK-15; resposta sempre inclui flags de disponibilidade |
| RISK-EXEC-06 | Relay do Outbox falha silenciosamente → eventos de auditoria não publicados | Baixa | Trilha de auditoria incompleta | Alerta de falha de relay configurado (TASK-16); retry com DLQ no Pub/Sub |

---

## 9. Referências

| Referência | Localização |
|------------|-------------|
| Requirements do módulo | docs/product/modules/account-management/requirements.md v0.1.0 |
| Design técnico do módulo | docs/product/modules/account-management/design.md v0.1.0 |
| README do módulo | docs/product/modules/account-management/README.md |
| Clean Architecture | `.forge/rules/architecture/clean-architecture.md` |
| DDD tático | `.forge/rules/architecture/ddd.md` |
| APIs e contratos | `.forge/rules/architecture/api-and-contracts.md` |
| Observabilidade | `.forge/rules/architecture/observability.md` |
| Segurança e conformidade (LGPD) | `.forge/rules/architecture/security-and-compliance.md` |
| Imutabilidade de auditoria | `.forge/rules/domain/audit-immutability.md` |
| Nomenclatura de banco / multi-tenancy | `.forge/rules/conventions/database-naming.md` |
| Permissões JWT (RBAC) | `.forge/rules/architecture/jwt-permissions.md` |
| mTLS interno | `.forge/rules/architecture/mtls-internal-services.md` |
| Pendências LGPD | VAL-ACC-01, VAL-ACC-02, VAL-ACC-03 (registrar em `approvals.yaml`) |
| Decisões inline | DD-001..DD-007 em design.md §17 |
