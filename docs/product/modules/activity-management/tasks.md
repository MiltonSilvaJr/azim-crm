# Tasks — ACT — Activity Management

- Versão: 0.1.1
- Data: 2026-06-14
- Status: Aprovado para desenvolvimento
- Referência base requirements: docs/product/modules/activity-management/requirements.md v0.1.0
- Referência base design: docs/product/modules/activity-management/design.md v0.1.0
- ADRs aplicáveis: ADR-0001 (isolamento multi-tenant em defesa em profundidade — Aceito), ADR-0003 (auditoria imutável — a confirmar), ADR-0004 (outbox e idempotência de eventos — a confirmar), ADR-0006 (token de link autenticado — a confirmar)
- Rules aplicáveis: `.forge/rules/architecture/clean-architecture.md`, `.forge/rules/architecture/ddd.md`, `.forge/rules/architecture/api-and-contracts.md`, `.forge/rules/architecture/observability.md`, `.forge/rules/architecture/security-and-compliance.md`, `.forge/rules/architecture/jwt-permissions.md`, `.forge/rules/conventions/database-naming.md`, `.forge/rules/domain/audit-immutability.md`

## Histórico de Versões

| Versão | Data | Status | Descrição da alteração |
|--------|------|--------|------------------------|
| 0.1.0 | 2026-06-11 | Rascunho para revisão | Criação inicial do plano de tasks derivado de requirements.md v0.1.0 e design.md v0.1.0 |
| 0.1.1 | 2026-06-14 | Aprovado para desenvolvimento | Aprovação humana (HITL #1); execução via `/forge:coding-loop` autônomo (6 ondas). |

## 1. Convenções de Implementação

### 1.1 TDD-first

Toda implementação com lógica verificável deve seguir o ciclo:

1. Red — escrever teste que falha
2. Green — implementar o mínimo para passar
3. Refactor — melhorar sem alterar comportamento

Nenhuma implementação de regra de domínio, handler, endpoint, persistência, contrato ou integração deve ser considerada concluída sem teste correspondente.

### 1.2 Property-Based Testing

PBT é obrigatório para:

- invariantes matemáticas
- idempotência
- anti-enumeração
- atomicidade
- state machines
- regras de conservação

Cada PBT deve mapear explicitamente para `PBT-NN` do `requirements.md` ou para invariante descrita no `design.md`. Biblioteca de referência: FsCheck ou equivalente (a confirmar conforme stack do projeto).

### 1.3 Bite-sized Tasks

Cada subtask deve ser estimada para menos de 2 horas.

Se uma subtask exceder 2 horas, ela deve ser dividida.

Cada TASK deve ser pequena o suficiente para revisão objetiva, mas grande o suficiente para entregar um incremento verificável.

### 1.4 Branch Model

Padrão de branch:

```text
<tipo>/<modulo>/<NN>-<slug>
```

Exemplos para este módulo:

```text
feat/activity-management/01-bootstrap-clean-architecture
test/activity-management/02-value-objects-pbt01
fix/activity-management/15-rls-tenant-interceptor
```

### 1.5 Git Worktree

Quando aplicável, cada TASK pode usar worktree dedicado:

```sh
git worktree add ../worktrees/activity-management/<NN>-<slug> -b <branch>
```

### 1.6 Encerramento de TASK

Cada TASK deve encerrar com:

- testes locais verdes
- coverage gate da camada atendido ou justificativa registrada
- lint/format executado quando aplicável
- documentação atualizada quando aplicável
- commit em Conventional Commits
- push da branch

PR pode ser aberto por TASK ou por onda, conforme regra do projeto.

### 1.7 Encerramento de Onda

Cada onda deve encerrar com:

- todas as TASKs da onda concluídas
- CI verde
- conflitos resolvidos
- PR aberto ou atualizado
- checklist de revisão preenchido
- documentação sincronizada

### 1.8 Early Exit

Se uma subtask falhar:

- marcar status como `[-]`
- registrar o ponto de falha
- registrar comando executado
- registrar erro principal
- não mascarar falha com implementação especulativa
- deixar contexto suficiente para outro agente ou desenvolvedor retomar

### 1.9 Convenção de Status

- `[ ]` Não iniciado
- `[-]` Em progresso
- `[X]` Concluído
- `[!]` Falhou — exige intervenção humana (interrompe a onda no `task-coder`)

### 1.10 Convenção canônica de IDs

**Formato único permitido:**

```
TASK-NN — <título>        ← unidade atômica de invocação do task-coder
  ST-MM — <subtask>       ← etapas TDD internas (Red/Green/Refactor/Docs/Encerramento)
```

- A **TASK** é a unidade que o `task-coder` invoca contra um specialist.
- **Subtasks** `ST-MM` são etapas internas da TASK; o specialist executa todas em sequência. A numeração `ST-MM` reinicia a cada nova TASK.
- **Onda** é apenas atributo (campo `**Onda**` no header da TASK) e seção `## 3. Ondas de Implementação` para agrupamento visual — **nunca** entra no ID da TASK.
- A unidade de PR é a **onda**: o `sprint-orchestrator` abre 1 PR contendo todas as TASKs da onda fechada.

## 2. Status Geral

| TASK | Título | Onda | Branch | Status |
|------|--------|------|--------|--------|
| TASK-01 | Criar solution e 5 projetos Clean Architecture | Onda 1 | `feat/activity-management/01-bootstrap` | [ ] |
| TASK-02 | Objetos de valor e state machine (PBT-01) | Onda 2 | `feat/activity-management/02-value-objects` | [ ] |
| TASK-03 | Domain events e exceptions | Onda 2 | `feat/activity-management/03-domain-events` | [ ] |
| TASK-04 | Agregado Activity — factory e invariantes I1–I6 | Onda 2 | `feat/activity-management/04-aggregate` | [ ] |
| TASK-05 | Specifications: Overdue, FunnelHealth, Scope (PBT-04, PBT-05) | Onda 2 | `feat/activity-management/05-specifications` | [ ] |
| TASK-06 | Pipeline behaviors e ports interfaces | Onda 3 | `feat/activity-management/06-behaviors-ports` | [ ] |
| TASK-07 | Commands/Handlers CRUD e validators | Onda 3 | `feat/activity-management/07-crud-handlers` | [ ] |
| TASK-08 | CompleteActivity idempotente e SuggestNextActivity (PBT-02) | Onda 3 | `feat/activity-management/08-complete-suggest` | [ ] |
| TASK-09 | ProcessDigestAction — validar e consumir token (PBT-02, PBT-03) | Onda 3 | `feat/activity-management/09-digest-action` | [ ] |
| TASK-10 | RescheduleActivity — reagendamento e ação via token | Onda 3 | `feat/activity-management/10-reschedule` | [ ] |
| TASK-11 | Queries de visão do vendedor (GetMyDay, GetMyWeek, ListActivities) | Onda 3 | `feat/activity-management/11-seller-queries` | [ ] |
| TASK-12 | Queries de sistema e ScanOverdueActivities | Onda 3 | `feat/activity-management/12-system-scan` | [ ] |
| TASK-13 | EF Core DbContext, ActivityRepository e migrations base | Onda 4 | `feat/activity-management/13-ef-repository` | [ ] |
| TASK-14 | Migration DD-001 — status/priority, backfill, constraints, token_hash | Onda 4 | `feat/activity-management/14-migration-dd001` | [ ] |
| TASK-15 | Índices, RLS falha-fechada e TenantConnectionInterceptor | Onda 4 | `feat/activity-management/15-rls-tenant` | [ ] |
| TASK-16 | Outbox transacional, PiiMasker e AuditPublisher | Onda 4 | `feat/activity-management/16-outbox-audit` | [ ] |
| TASK-17 | DigestActionTokenAdapter e adapters de leitura (Opportunity, Account) | Onda 4 | `feat/activity-management/17-adapters` | [ ] |
| TASK-18 | ActivitiesController, middleware e OpenAPI | Onda 5 | `feat/activity-management/18-activities-ctrl` | [ ] |
| TASK-19 | DigestActionsController, InternalController e anti-enumeração | Onda 5 | `feat/activity-management/19-digest-internal-ctrl` | [ ] |
| TASK-20 | Contratos de evento v1 e Pact provider tests | Onda 5 | `feat/activity-management/20-event-contracts` | [ ] |
| TASK-21 | Testes de API — RBAC, erros, cross-tenant CI gate | Onda 5 | `feat/activity-management/21-api-tests` | [ ] |
| TASK-22 | Observabilidade — métricas, logs, traces, alertas | Onda 6 | `feat/activity-management/22-observability` | [ ] |
| TASK-23 | Hardening de segurança — tempo constante, anti-PII, privilégio mínimo | Onda 6 | `feat/activity-management/23-security-hardening` | [ ] |
| TASK-24 | DoD final — coverage gates, PBT-01..05 completos, data-model atualizado | Onda 6 | `feat/activity-management/24-dod-final` | [ ] |

## 3. Ondas de Implementação

| Onda | Foco | TASKs | Critério de fechamento |
|------|------|-------|------------------------|
| Onda 1 | Bootstrap | TASK-01 | Solution compilando; `Architecture.Tests` verde |
| Onda 2 | Domínio | TASK-02..TASK-05 | PBT-01, PBT-04, PBT-05 verdes; `Domain.Tests` ≥ 95% |
| Onda 3 | Application | TASK-06..TASK-12 | PBT-02, PBT-03 verdes; `Application.Tests` ≥ 85%; todos os handlers verdes |
| Onda 4 | Infrastructure | TASK-13..TASK-17 | Migration executada; RLS falha-fechada; `Infrastructure.Tests` ≥ 70%; isolamento cross-tenant verde |
| Onda 5 | API + Contratos | TASK-18..TASK-21 | Todos os endpoints documentados em OpenAPI; `Api.Tests` ≥ 80%; Pact verdes; CI gate cross-tenant verde |
| Onda 6 | Hardening | TASK-22..TASK-24 | DoD completo; coverage gates atendidos; scan anti-PII verde; `approvals.yaml` atualizado |

## 4. Tarefas

### TASK-01 — Criar solution e 5 projetos Clean Architecture

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 1 — Bootstrap |
| **Branch** | `feat/activity-management/01-bootstrap` |
| **Worktree** | `git worktree add ../worktrees/activity-management/01-bootstrap -b feat/activity-management/01-bootstrap` |
| **Status** | [ ] |
| **Depende de** | Não aplicável |
| **Entregável** | Solution .NET com 5 projetos de produção (`Domain`, `Application`, `Infrastructure`, `Api`, `Contracts`) e 5 projetos de teste compilando; `Architecture.Tests` verde validando todas as regras de dependência entre camadas |
| **Mapeia** | design.md §3 (estrutura da solução), design.md Princípio P1 (Clean Architecture estrita) |
| **Camada principal** | DevOps / Tests |

#### Objetivo

Criar a estrutura base da solution .NET conforme design.md §3. Implementar `Architecture.Tests` desde o início garantindo as regras de dependência: `Api → Application, Infrastructure`; `Application → Domain, Contracts`; `Infrastructure → Application, Domain`; `Domain → ∅`; `Contracts → ∅`. Configurar pacotes mínimos (MediatR, FluentValidation, EF Core, NetArchTest ou equivalente).

#### Subtasks

- [ ] **ST-01 — Red:** criar `Architecture.Tests` com as regras de dependência — falham porque os projetos ainda não existem.
- [ ] **ST-02 — Green:** criar solution, 10 projetos, referências entre projetos conforme design.md §3 e instalar pacotes mínimos; `Architecture.Tests` ficam verdes.
- [ ] **ST-03 — Refactor:** organizar `Directory.Build.props`, `.editorconfig`, `global.json`; verificar `dotnet format`.
- [ ] **ST-04 — Docs:** atualizar `docs/product/modules/activity-management/README.md` com estrutura de projetos.
- [ ] **ST-05 — Encerramento:** `dotnet build` sem warnings; `dotnet test Architecture.Tests` verde; commit `feat(act): bootstrap solution clean-architecture 5 projetos` e push.

#### Critérios de Aceite

- [ ] 10 projetos compilam sem erro ou warning.
- [ ] `Architecture.Tests` valida as 5 regras de dependência e está verde.
- [ ] Nenhuma referência proibida entre camadas.
- [ ] Pacotes mínimos instalados e sem vulnerabilidades conhecidas (`dotnet list package --vulnerable`).

---

### TASK-02 — Objetos de valor e state machine (PBT-01)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domínio |
| **Branch** | `feat/activity-management/02-value-objects` |
| **Worktree** | `git worktree add ../worktrees/activity-management/02-value-objects -b feat/activity-management/02-value-objects` |
| **Status** | [ ] |
| **Depende de** | TASK-01 |
| **Entregável** | Seis objetos de valor (`ActivityType`, `Priority`, `ActivityStatus`, `DueDate`, `OpportunityLink`, `AccountLink`) com invariantes, igualdade por valor, imutabilidade e PBT-01 verde |
| **Mapeia** | Req 1 (type, priority default medium), Req 4 (state machine), design.md §4.3, design.md §4.5, PBT-01 |
| **Camada principal** | Domain |

#### Objetivo

Implementar os 6 objetos de valor do agregado `Activity` conforme design.md §4.3. `ActivityStatus` encapsula a state machine com as transições exatas da seção 4.5 do requirements e o invariante I5. O PBT-01 (FsCheck ou equivalente) gera sequências arbitrárias de tentativas de transição e verifica que o estado final só é alcançável por transições válidas, que terminais não têm saída e que `completedAt` está preenchido se e somente se `status=completed`.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes unitários e PBT-01 (sequências arbitrárias de transição de status) que falham; verificar que geradores cobrem pelo menos 100 sequências.
- [ ] **ST-02 — Green:** implementar os 6 objetos de valor com `CanTransitionTo`, `IsTerminal`, listas canônicas de tipo/prioridade e igualdade estrutural.
- [ ] **ST-03 — Refactor:** extrair constantes para enum/sealed class conforme padrão do projeto; garantir cobertura de todos os casos de transição inválida.
- [ ] **ST-04 — Encerramento:** `Domain.Tests` verdes; PBT-01 verde com ≥ 100 sequências; coverage Domain ≥ 95%; commit `feat(act): value objects activitystatus state-machine pbt-01` e push.

#### Critérios de Aceite

- [ ] `ActivityStatus.CanTransitionTo` rejeita toda transição não listada em requirements §4.1.
- [ ] `ActivityStatus.IsTerminal` verdadeiro para `completed` e `cancelled`.
- [ ] `ActivityType` rejeita valor fora de `{meeting, follow_up, call, email, task}`.
- [ ] PBT-01 executado com ≥ 100 sequências aleatórias; nenhuma viola a state machine.
- [ ] Todos os objetos de valor são imutáveis e implementam igualdade por valor.
- [ ] Coverage `Domain.Tests` ≥ 95%.

---

### TASK-03 — Domain events e exceptions

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domínio |
| **Branch** | `feat/activity-management/03-domain-events` |
| **Worktree** | `git worktree add ../worktrees/activity-management/03-domain-events -b feat/activity-management/03-domain-events` |
| **Status** | [ ] |
| **Depende de** | TASK-01 |
| **Entregável** | Classes de evento de domínio (`ActivityCreated`, `ActivityCompleted`, `ActivityOverdue`) e de exceções de domínio (`TitleRequiredException`, `InvalidActivityTypeException`, `InvalidStatusTransitionException`, `ActivityTerminalException`) implementadas com testes |
| **Mapeia** | Req 14 (events), RNF 2 (audit), RNF 7 (sem PII no payload), design.md §4.4 |
| **Camada principal** | Domain |

#### Objetivo

Definir os contratos imutáveis dos eventos de domínio que o agregado `Activity` acumulará. Conforme design.md §4.4, os eventos **não carregam `title` nem `description`** (PII potencial — RNF 7.2). Definir também as exceções de domínio tipadas que as invariantes do agregado lançarão, de modo que handlers e testes possam capturá-las com semântica clara.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes que verificam: (a) eventos contêm os campos obrigatórios e não contêm `title`/`description`; (b) exceções de domínio herdam de uma base comum e têm mensagem não vazia.
- [ ] **ST-02 — Green:** implementar `ActivityCreated`, `ActivityCompleted`, `ActivityOverdue` como records imutáveis com `tenantId`, `correlationId` e campos do design.md §4.4; implementar as 4 exception classes.
- [ ] **ST-03 — Refactor:** unificar base `DomainEvent` com `occurredAt` e `eventId` (UUID); verificar que nenhum campo de texto livre vaza.
- [ ] **ST-04 — Encerramento:** testes verdes; commit `feat(act): domain events activitycreated completed overdue exceptions` e push.

#### Critérios de Aceite

- [ ] `ActivityCreated`, `ActivityCompleted`, `ActivityOverdue` são imutáveis e não contêm `title`/`description`.
- [ ] Cada evento carrega `activityId`, `tenantId`, `occurredAt` e os campos específicos do design.md §4.4.
- [ ] As 4 exceções de domínio herdam de base comum e são distintas entre si.
- [ ] Testes cobrem serialização básica dos payloads de evento.

### TASK-04 — Agregado Activity — factory e invariantes I1–I6

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domínio |
| **Branch** | `feat/activity-management/04-aggregate` |
| **Worktree** | `git worktree add ../worktrees/activity-management/04-aggregate -b feat/activity-management/04-aggregate` |
| **Status** | [ ] |
| **Depende de** | TASK-02, TASK-03 |
| **Entregável** | Aggregate Root `Activity` implementado com factory `Create`/`Reconstitute`, métodos `Complete`, `Cancel`, `Reschedule`, `ChangeStatus`, `UpdateDetails`; invariantes I1–I6 enforçadas; `IActivityRepository` definido no Domain |
| **Mapeia** | Req 1 (criar), Req 2 (atualizar/excluir), Req 4 (state machine), Req 6 (complete idempotente), Req 8 (reschedule), design.md §4.1, DD-004 |
| **Camada principal** | Domain |

#### Objetivo

Implementar o Aggregate Root `Activity` conforme design.md §4.1. A factory `Activity.Create` aplica os defaults (`status=pending`, `priority=medium`) e levanta `ActivityCreated`. O método `Complete` é idempotente: se já `completed`, é no-op sem novo `completedAt` nem novo evento (DD-004, PBT-02). `ActivityTerminalException` protege atividades terminais de qualquer escrita (invariante I6). `IActivityRepository` definido no Domain sem dependência de infraestrutura.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes que cobrem cada invariante (I1..I6), factory válida e inválida, cada método de comportamento e a idempotência de `Complete` (chamada dupla não muda `completedAt`).
- [ ] **ST-02 — Green:** implementar `Activity.cs` com factory, métodos de comportamento delegando para objetos de valor, acumulação de domain events e interface `IActivityRepository`.
- [ ] **ST-03 — Refactor:** garantir que nenhum setter público existe; extrair helpers internos; verificar que `IActivityRepository` não vaza `IQueryable`.
- [ ] **ST-04 — Encerramento:** todos os invariantes cobertos; `Domain.Tests` ≥ 95%; commit `feat(act): aggregate activity invariantes i1-i6 complete-idempotente iactivityrepository` e push.

#### Critérios de Aceite

- [ ] I1 (`title` não vazio) levanta `TitleRequiredException`; I2 (tipo válido) levanta `InvalidActivityTypeException`.
- [ ] I3: factory aplica `status=pending` e `priority=medium` quando não informados.
- [ ] I4: `ChangeStatus` invoca `ActivityStatus.CanTransitionTo` e levanta `InvalidStatusTransitionException`.
- [ ] I5: `completedAt` preenchido se e somente se `status=completed`.
- [ ] I6: qualquer método de escrita em atividade terminal levanta `ActivityTerminalException`.
- [ ] `Complete` chamado duas vezes não altera `completedAt` e não acumula segundo `ActivityCompleted`.

---

### TASK-05 — Specifications: Overdue, FunnelHealth, Scope (PBT-04, PBT-05)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domínio |
| **Branch** | `feat/activity-management/05-specifications` |
| **Worktree** | `git worktree add ../worktrees/activity-management/05-specifications -b feat/activity-management/05-specifications` |
| **Status** | [ ] |
| **Depende de** | TASK-02, TASK-04 |
| **Entregável** | Cinco specifications implementadas (`OverdueSpecification`, `TodaySpecification`, `UpcomingSpecification`, `FunnelHealthSpecification`, `ScopeSpecification`) com PBT-04 e PBT-05 verdes |
| **Mapeia** | Req 5 (faixas de visão), Req 10 (saúde do funil), Req 11 (vencida), Req 13 (escopo), PBT-04, PBT-05, DD-008 (fuso IANA do tenant) |
| **Camada principal** | Domain |

#### Objetivo

Implementar as specifications do design.md §4.6. `OverdueSpecification`: atividade é vencida se e somente se `dueAt < referenceInstant` e status não terminal — atividades terminais nunca são vencidas (PBT-04). As specifications de faixa (`TodaySpecification`, `UpcomingSpecification`) recebem o fuso IANA do tenant via `ITenantClock` (DD-008). `FunnelHealthSpecification` verifica se existe pelo menos uma atividade não terminal com `dueAt` futuro por oportunidade aberta (PBT-05). `ScopeSpecification` filtra por papel (Vendedor=próprias, Gestor de BU=BU) dentro do tenant (Req 13).

#### Subtasks

- [ ] **ST-01 — Red:** escrever PBT-04 (geradores arbitrários de `dueAt`/status; verificar invariante de vencida) e PBT-05 (sugestão aceita preserva ≥1 follow-up futuro); escrever testes unitários para cada specification.
- [ ] **ST-02 — Green:** implementar as 5 specifications; `ITenantClock` definido como port em Application (referenciado aqui por interface).
- [ ] **ST-03 — Refactor:** garantir que os cálculos de faixa de data usam o fuso do tenant e não UTC; verificar cobertura de todos os cenários (terminal nunca overdue, status não terminal com `dueAt` passado sempre overdue).
- [ ] **ST-04 — Encerramento:** PBT-04 e PBT-05 verdes com ≥ 100 casos cada; `Domain.Tests` ≥ 95%; commit `feat(act): specifications overdue funnelhealth scope pbt-04 pbt-05` e push.

#### Critérios de Aceite

- [ ] `OverdueSpecification`: atividade `completed` ou `cancelled` nunca é vencida, independente da `dueAt`.
- [ ] `OverdueSpecification`: atividade `pending` com `dueAt < now()` é vencida.
- [ ] PBT-04 executado com ≥ 100 pares (`dueAt`, `status`) arbitrários; invariante verificada em todos.
- [ ] PBT-05: após sugestão aceita ao concluir última atividade futura, `FunnelHealthSpecification` retorna verdadeiro para a oportunidade.
- [ ] `ScopeSpecification` não retorna atividades de tenant diferente.

---

### TASK-06 — Pipeline behaviors e ports interfaces

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/activity-management/06-behaviors-ports` |
| **Worktree** | `git worktree add ../worktrees/activity-management/06-behaviors-ports -b feat/activity-management/06-behaviors-ports` |
| **Status** | [ ] |
| **Depende de** | TASK-04 |
| **Entregável** | 5 pipeline behaviors MediatR implementados e testados (`CorrelationLoggingBehavior`, `TenantScopeBehavior`, `ValidationBehavior`, `AuthorizationBehavior`, `TransactionBehavior`); interfaces de port definidas (`IOpportunityReadPort`, `IAccountReadPort`, `IDigestActionTokenPort`, `IAuditPublisher`, `IClock`, `ITenantClock`) |
| **Mapeia** | Req 13 (RBAC), RNF 1 (tenant scope), RNF 2 (auditoria), RNF 6 (correlation_id), RNF 7 (sem PII em log), DD-002 (TenantScope), design.md §5.4 |
| **Camada principal** | Application |

#### Objetivo

Implementar a pipeline MediatR na ordem definida no design.md §5.4: (1) `CorrelationLoggingBehavior` injeta `correlation_id`/`tenant_id`/`activity_id` no escopo de log e nunca loga `title`/`description` (RNF 7.2); (2) `TenantScopeBehavior` resolve `TenantContext` do token JWT e garante que toda operação opera no tenant autenticado; (3) `ValidationBehavior` executa FluentValidation sem efeito colateral; (4) `AuthorizationBehavior` aplica RBAC via `ScopeSpecification`; (5) `TransactionBehavior` abre transação para Commands e coleta domain events no Outbox.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes unitários para cada behavior em isolamento (mocks de next/context); verificar que `TenantScopeBehavior` falha-fechada quando tenant ausente; verificar que `CorrelationLoggingBehavior` nunca loga título.
- [ ] **ST-02 — Green:** implementar os 5 behaviors e definir as 6 interfaces de port em `Application/Ports/`.
- [ ] **ST-03 — Refactor:** garantir ordem de registro dos behaviors no DI; verificar que `ValidationBehavior` não deixa efeito colateral em falha.
- [ ] **ST-04 — Encerramento:** `Application.Tests` (behaviors) verdes; commit `feat(act): pipeline behaviors 5 + port interfaces` e push.

#### Critérios de Aceite

- [ ] `TenantScopeBehavior` rejeita request sem `TenantContext` válido (falha-fechada).
- [ ] `CorrelationLoggingBehavior` não inclui `title` ou `description` em nenhum log emitido.
- [ ] `ValidationBehavior` retorna erro de validação sem atingir o handler.
- [ ] `AuthorizationBehavior` retorna ACT-ERR-007 quando papel/escopo insuficiente.
- [ ] `TransactionBehavior` faz commit atômico de escrita de domínio + Outbox.
- [ ] As 6 interfaces de port têm contratos mínimos suficientes para os handlers das Ondas 3 e 4.

### TASK-07 — Commands/Handlers CRUD e validators

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/activity-management/07-crud-handlers` |
| **Worktree** | `git worktree add ../worktrees/activity-management/07-crud-handlers -b feat/activity-management/07-crud-handlers` |
| **Status** | [ ] |
| **Depende de** | TASK-06 |
| **Entregável** | Três commands e handlers (`CreateActivityCommand`, `UpdateActivityCommand`, `DeleteActivityCommand`) com validators FluentValidation e testes de aplicação; `IOpportunityReadPort` e `IAccountReadPort` usados para validar vínculos |
| **Mapeia** | Req 1 (criar), Req 2 (atualizar/excluir), Req 3 (vínculos opportunity/account), design.md §5.1, erros ACT-ERR-001/002/003/005/006/010/011 |
| **Camada principal** | Application |

#### Objetivo

Implementar os três commands de CRUD. `CreateActivityCommand` valida `opportunity_id`/`account_id` via portas (mesmo tenant — Req 3.2/3.3), aplica defaults (`priority=medium`, `status=pending`) e publica `ActivityCreated`. `UpdateActivityCommand` guarda atividade terminal (I6) e respeita RBAC. `DeleteActivityCommand` registra auditoria via `IAuditPublisher`. Validators `CreateActivityValidator`/`UpdateActivityValidator` garantem sintaxe antes do handler (título, tipo, `due_at`).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de handler com mocks de `IActivityRepository`, `IOpportunityReadPort`, `IAccountReadPort`; cobrir: criação válida, título vazio (ACT-ERR-001), tipo inválido (ACT-ERR-002), `due_at` ausente (ACT-ERR-010), vínculo inválido (ACT-ERR-005/006), atualização de terminal (ACT-ERR-011), exclusão com auditoria.
- [ ] **ST-02 — Green:** implementar os 3 handlers e 2 validators; conectar portas de leitura para validação de vínculos.
- [ ] **ST-03 — Refactor:** extrair lógica de validação de vínculo para método privado reutilizável; garantir que handlers não contêm regra de negócio de domínio.
- [ ] **ST-04 — Encerramento:** `Application.Tests` (CRUD) verdes; commit `feat(act): commands crud create update delete validators` e push.

#### Critérios de Aceite

- [ ] `CreateActivityCommand`: `ActivityCreated` acumulado no agregado após criação bem-sucedida.
- [ ] Vínculo com `opportunity_id` de outro tenant retorna ACT-ERR-005 (indistinguível de "não encontrado").
- [ ] `DeleteActivityCommand` registra auditoria com `action=deleted` via `IAuditPublisher`.
- [ ] `UpdateActivityCommand` em atividade `completed`/`cancelled` retorna ACT-ERR-011.
- [ ] Validators rejeitam request antes de atingir handler quando inválido.

---

### TASK-08 — CompleteActivity idempotente e SuggestNextActivity (PBT-02)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/activity-management/08-complete-suggest` |
| **Worktree** | `git worktree add ../worktrees/activity-management/08-complete-suggest -b feat/activity-management/08-complete-suggest` |
| **Status** | [ ] |
| **Depende de** | TASK-06, TASK-07 |
| **Entregável** | `CompleteActivityCommand`/Handler com idempotência verificada por PBT-02 e `SuggestNextActivityQuery`/Handler retornando sugestão de vínculo (sem criar atividade) |
| **Mapeia** | Req 6 (concluir pela lista), Req 9 (sugestão), RNF 3 (idempotência), DD-004, PBT-02, design.md §5.1 |
| **Camada principal** | Application |

#### Objetivo

`CompleteActivityCommand`: invoca `Activity.Complete` (já idempotente no domínio — TASK-04), publica `ActivityCompleted` via Outbox e registra auditoria. Idempotência: N chamadas com a mesma `activityId` produzem exatamente um `ActivityCompleted` processado e um único `completedAt` (PBT-02, DD-004). `SuggestNextActivityQuery`: verifica se a atividade concluída estava vinculada a oportunidade aberta; se sim, retorna pré-preenchimento de vínculo (oportunidade + conta) — nunca cria atividade (Req 9.4).

#### Subtasks

- [ ] **ST-01 — Red:** escrever PBT-02 (N ≥ 1 chamadas de `CompleteActivity` para a mesma atividade → exatamente 1 `ActivityCompleted` acumulado, 1 `completedAt`); escrever teste de `SuggestNextActivity` para atividade com e sem vínculo de oportunidade aberta.
- [ ] **ST-02 — Green:** implementar `CompleteActivityCommand`/Handler e `SuggestNextActivityQuery`/Handler com mock de `IOpportunityReadPort` para verificar oportunidade aberta.
- [ ] **ST-03 — Refactor:** garantir que reconcluir uma atividade já `completed` retorna 200 sem novo evento e sem alteração de `completedAt`.
- [ ] **ST-04 — Encerramento:** PBT-02 (parcial — via lista) verde com ≥ 100 casos; `Application.Tests` verdes; commit `feat(act): complete-idempotente suggest-next-activity pbt-02-parcial` e push.

#### Critérios de Aceite

- [ ] `CompleteActivity` com atividade já `completed`: retorna sucesso, `completedAt` não muda, nenhum novo `ActivityCompleted` acumulado.
- [ ] PBT-02 (pela lista): N chamadas → exatamente 1 evento `ActivityCompleted` acumulado.
- [ ] `SuggestNextActivity` para oportunidade aberta: retorna `opportunityId` + `accountId?` pré-preenchidos.
- [ ] `SuggestNextActivity` para atividade sem vínculo ou oportunidade fechada: retorna sugestão vazia (sem erro).
- [ ] `CompleteActivity` em atividade terminal (`cancelled`) retorna ACT-ERR-004.

---

### TASK-09 — ProcessDigestAction — validar e consumir token (PBT-02, PBT-03)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/activity-management/09-digest-action` |
| **Worktree** | `git worktree add ../worktrees/activity-management/09-digest-action -b feat/activity-management/09-digest-action` |
| **Status** | [ ] |
| **Depende de** | TASK-06, TASK-08 |
| **Entregável** | `ProcessDigestActionCommand`/Handler com validação completa de token (existência, expiração, uso), idempotência e anti-enumeração; PBT-02 e PBT-03 verdes |
| **Mapeia** | Req 7 (conclusão via link), RNF 3 (idempotência), RNF 5 (token uso único), PBT-02, PBT-03, DD-003, DD-004, design.md §5.1, erros ACT-ERR-008/009 |
| **Camada principal** | Application |

#### Objetivo

`ProcessDigestActionCommand`: (1) valida token via `IDigestActionTokenPort` — existência, `expires_at` futuro, `used_at` vazio — antes de qualquer escrita (Req 7.2); (2) token válido conclui a atividade (`Activity.Complete`) e marca `used_at` na mesma transação atômica; (3) token já usado retorna confirmação de sucesso idempotente sem nova escrita (MSG-029); (4) token expirado retorna ACT-ERR-009 (MSG-030); (5) token inexistente/malformado retorna ACT-ERR-008 **indistinguível em forma** de atividade inacessível (PBT-03, anti-enumeração). PBT-02 completo (via lista + via token).

#### Subtasks

- [ ] **ST-01 — Red:** escrever PBT-02 completo (N cliques no mesmo token → exatamente 1 conclusão, 1 `completedAt`, 1 evento); PBT-03 (token inexistente/malformado/inacessível → resposta indistinguível; token válido não usado → escrita ≤ 1 vez; token já usado → no-op idempotente).
- [ ] **ST-02 — Green:** implementar handler com 4 ramos de validação (válido/já-usado/expirado/inválido); atomicidade `used_at` + conclusão + Outbox + auditoria (correlação token↔atividade, Req 7.8).
- [ ] **ST-03 — Refactor:** garantir que a resposta para token inexistente e atividade inacessível tem a mesma forma (ACT-ERR-008); extrair validação de token para método privado.
- [ ] **ST-04 — Encerramento:** PBT-02 e PBT-03 verdes (≥ 100 casos cada); `Application.Tests` verdes; commit `feat(act): processdigestaction token-validation pbt-02-completo pbt-03` e push.

#### Critérios de Aceite

- [ ] Token válido + não usado: atividade concluída, `used_at` preenchido, `ActivityCompleted` publicado — tudo na mesma transação.
- [ ] Token já usado: retorna 200 idempotente sem alterar `completedAt` ou emitir novo evento.
- [ ] Token expirado: retorna ACT-ERR-009 (HTTP 410).
- [ ] Token inexistente/malformado: retorna ACT-ERR-008 (HTTP 404) — forma indistinguível de atividade inacessível.
- [ ] PBT-03: resposta para token inexistente é indistinguível em estrutura da resposta para atividade inacessível.
- [ ] Auditoria registra `correlation_id` correlacionando token↔atividade (RNF 2.4).

### TASK-10 — RescheduleActivity — reagendamento e ação via token

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/activity-management/10-reschedule` |
| **Worktree** | `git worktree add ../worktrees/activity-management/10-reschedule -b feat/activity-management/10-reschedule` |
| **Status** | [ ] |
| **Depende de** | TASK-06, TASK-09 |
| **Entregável** | `RescheduleActivityCommand`/Handler para reagendamento direto (JWT) e via token do digest (ação `reschedule`), com auditoria e atualização das faixas de visão |
| **Mapeia** | Req 8 (reagendar), Req 8.5 (via token), design.md §5.1 (`ProcessDigestActionCommand` ação reschedule) |
| **Camada principal** | Application |

#### Objetivo

Implementar o reagendamento como atualização de `dueAt` em atividade não terminal. Quando disparado via token do digest (Req 8.5), o `ProcessDigestActionCommand` (TASK-09) identifica `action=reschedule` e delega para `Activity.Reschedule`, passando a nova `dueAt` do corpo da requisição. Auditoria registrada com `action=rescheduled` e o delta da `dueAt`.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes para: reagendamento de atividade não terminal (sucesso), reagendamento de terminal (ACT-ERR-011), reagendamento via token com `action=reschedule` (sucesso e token já usado — idempotente).
- [ ] **ST-02 — Green:** implementar `RescheduleActivityCommand`/Handler; estender `ProcessDigestActionCommand` para ramo `action=reschedule`.
- [ ] **ST-03 — Refactor:** garantir que o reagendamento não altera `status`; verificar que a nova `dueAt` reaparece corretamente nas faixas da visão "Meu dia".
- [ ] **ST-04 — Encerramento:** `Application.Tests` (reschedule) verdes; commit `feat(act): reschedule-activity command handler via-token` e push.

#### Critérios de Aceite

- [ ] Reagendamento em atividade `completed` ou `cancelled` retorna ACT-ERR-011.
- [ ] Reagendamento bem-sucedido: `dueAt` atualizado, `status` inalterado, auditoria com delta registrada.
- [ ] Reagendamento via token com `action=reschedule`: token marcado como `used_at`, atividade reagendada.
- [ ] Token já usado para `reschedule`: retorna 200 idempotente sem alterar `dueAt` novamente.

---

### TASK-11 — Queries de visão do vendedor (GetMyDay, GetMyWeek, ListActivities)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/activity-management/11-seller-queries` |
| **Worktree** | `git worktree add ../worktrees/activity-management/11-seller-queries -b feat/activity-management/11-seller-queries` |
| **Status** | [ ] |
| **Depende de** | TASK-05, TASK-06 |
| **Entregável** | Três queries de leitura do vendedor (`GetMyDayQuery`, `GetMyWeekQuery`, `ListActivitiesQuery`) e uma de gestão do funil (`GetOpportunitiesWithoutFollowupQuery`) implementadas com fuso IANA do tenant |
| **Mapeia** | Req 5 (faixas de visão), Req 10 (saúde do funil), Req 13 (escopo), DD-008 (fuso IANA), design.md §5.2 |
| **Camada principal** | Application |

#### Objetivo

`GetMyDayQuery` e `GetMyWeekQuery` usam as specifications `OverdueSpecification`, `TodaySpecification`, `UpcomingSpecification` com o fuso IANA do tenant via `ITenantClock` (DD-008). As faixas "vencidas/hoje/próximas" são calculadas no horário local do tenant, não em UTC. `ListActivitiesQuery` suporta filtros por `owner`, `type`, `status`, `overdue`, `opportunityId` e paginação. Todas respeitam `ScopeSpecification` (Req 13).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes unitários com mock de `IActivityRepository` e `ITenantClock`; verificar agrupamento correto de faixas em diferentes fusos horários; verificar que atividades terminais nunca aparecem nas faixas.
- [ ] **ST-02 — Green:** implementar os 4 query handlers aplicando specifications e `ScopeSpecification`.
- [ ] **ST-03 — Refactor:** garantir que `GetMyDayQuery` usa fuso do tenant para limites de dia; verificar que `ListActivitiesQuery` respeita paginação e não vaza atividades de fora do escopo.
- [ ] **ST-04 — Encerramento:** `Application.Tests` (queries vendedor) verdes; commit `feat(act): queries getmyday getmyweek listactivities getopportunitieswithoutfollowup` e push.

#### Critérios de Aceite

- [ ] Atividades `completed` ou `cancelled` nunca aparecem em nenhuma faixa de "Meu dia".
- [ ] Faixa "vencidas" inclui atividades com `dueAt` anterior ao início do dia corrente no fuso do tenant.
- [ ] `ListActivitiesQuery` retorna apenas atividades do escopo autenticado (tenant + papel).
- [ ] `GetOpportunitiesWithoutFollowupQuery` retorna oportunidades abertas sem atividade não terminal futura.

---

### TASK-12 — Queries de sistema e ScanOverdueActivities

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/activity-management/12-system-scan` |
| **Worktree** | `git worktree add ../worktrees/activity-management/12-system-scan -b feat/activity-management/12-system-scan` |
| **Status** | [ ] |
| **Depende de** | TASK-05, TASK-06 |
| **Entregável** | Três queries de sistema (`GetOverdueByUserQuery`, `GetTodayByUserQuery`, `GetLastCompletedActivityQuery`) e `ScanOverdueActivitiesCommand`/Handler com deduplicação por `(activityId, scanDate)` e publicação de `ActivityOverdue` |
| **Mapeia** | Req 11 (vencidas), Req 12 (última atividade), Req 14 (evento ActivityOverdue), RNF 4 (performance última atividade), DD-005 (overdue por scan), design.md §5.1/§5.2 |
| **Camada principal** | Application |

#### Objetivo

`GetOverdueByUserQuery` e `GetTodayByUserQuery` servem o digest (Req 5.6, Req 11.3/11.4) via API interna. `GetLastCompletedActivityQuery` suporta lote (`opportunityIds[]`) para insumo do opportunity-pipeline (Req 12, RNF 4). `ScanOverdueActivitiesCommand` itera atividades vencidas em lotes por tenant, enfileira `ActivityOverdue` no Outbox com chave de deduplicação `(activity_id, scan_date)` para evitar duplicatas no mesmo scan (DD-005, Req 14.3).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de handler com mocks; verificar que `GetLastCompletedActivityQuery` em lote não gera N+1; verificar deduplicação do scan (segunda chamada do scan no mesmo dia não acumula segundo evento por atividade).
- [ ] **ST-02 — Green:** implementar os 3 query handlers e o scan handler com lote paginado por tenant.
- [ ] **ST-03 — Refactor:** garantir que o scan não altera o estado das atividades (DD-005); confirmar que `GetLastCompletedActivityQuery` delega o índice para o repositório (não faz ordenação em memória).
- [ ] **ST-04 — Encerramento:** `Application.Tests` (queries sistema + scan) verdes; commit `feat(act): queries sistema overdue today lastcompleted scan-overdue dedup` e push.

#### Critérios de Aceite

- [ ] `GetLastCompletedActivityQuery` em lote: consulta suportada por índice; sem table scan aparente nos testes de repositório (mock verifica método correto chamado).
- [ ] `ScanOverdueActivitiesCommand`: enfileira `ActivityOverdue` com `dedup_key = (activityId, scanDate)`.
- [ ] Atividades `completed` ou `cancelled` nunca retornam em `GetOverdueByUserQuery`.
- [ ] `ScanOverdueActivitiesCommand` não altera `status` de nenhuma atividade.

### TASK-13 — EF Core DbContext, ActivityRepository e migrations base

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/activity-management/13-ef-repository` |
| **Worktree** | `git worktree add ../worktrees/activity-management/13-ef-repository -b feat/activity-management/13-ef-repository` |
| **Status** | [ ] |
| **Depende de** | TASK-04, TASK-06 |
| **Entregável** | `ActivityManagementDbContext` com `IEntityTypeConfiguration<Activity>` mapeando `snake_case`, `ActivityRepository` implementando `IActivityRepository`, migrations EF Core inicializadas e testadas com Testcontainers/PostgreSQL |
| **Mapeia** | design.md §6.1 (persistência), design.md §7 (schema), `rules/conventions/database-naming.md` |
| **Camada principal** | Infrastructure |

#### Objetivo

Configurar o EF Core com nomes físicos `snake_case` (rule `database-naming.md`). O `ActivityRepository` implementa `IActivityRepository` do Domain sem vazar `IQueryable` para fora de Infrastructure. As `IEntityTypeConfiguration` mapeiam todos os campos da tabela `activities` incluindo os value objects via owned types ou conversores. A migration base cria a tabela `activities` no estado inicial (sem `status`/`priority` — esses serão adicionados na TASK-14 via migration DD-001).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de repositório com Testcontainers PostgreSQL: salvar e recuperar uma atividade; verificar que nomes de coluna são `snake_case`; verificar que `IQueryable` não é exposto.
- [ ] **ST-02 — Green:** implementar `ActivityManagementDbContext`, configuração EF, `ActivityRepository` com métodos `FindByIdAsync`, `SaveAsync`, `DeleteAsync`; gerar migration base (`dotnet ef migrations add Initial`).
- [ ] **ST-03 — Refactor:** verificar mapeamento de todos os campos (incluindo nullable `opportunity_id`, `account_id`, `completed_at`); confirmar que `updated_at` é atualizado automaticamente.
- [ ] **ST-04 — Encerramento:** `Infrastructure.Tests` (repository) verdes com Testcontainers; `dotnet ef database update` sem erro; commit `feat(act): dbcontext activityrepository ef-core migrations-base` e push.

#### Critérios de Aceite

- [ ] Nomes de coluna no banco são `snake_case` (ex: `owner_id`, `due_at`, `created_at`).
- [ ] `ActivityRepository` não expõe `IQueryable` nem `DbSet` para fora de Infrastructure.
- [ ] Round-trip: salvar `Activity` e recuperar por ID retorna o mesmo agregado.
- [ ] Migration base aplicada em banco limpo sem erro.

---

### TASK-14 — Migration DD-001 — status/priority, backfill, constraints, token_hash

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/activity-management/14-migration-dd001` |
| **Worktree** | `git worktree add ../worktrees/activity-management/14-migration-dd001 -b feat/activity-management/14-migration-dd001` |
| **Status** | [ ] |
| **Depende de** | TASK-13 |
| **Entregável** | Migration EF Core adicionando `status` e `priority` à tabela `activities` com backfill seguro (linhas com `completed_at` → `completed`; demais → `pending`), constraints `CHECK` de domínio, constraint de consistência `chk_activities_completed_consistency`, tabela `digest_action_tokens` com coluna `token_hash` e índice único |
| **Mapeia** | DD-001 (status/priority), design.md §7 (schema completo), RNF 4 (índices), Req 4 (state machine no banco) |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar a migration mais crítica do módulo conforme DD-001. O backfill deve ser seguro: `status = CASE WHEN completed_at IS NOT NULL THEN 'completed' ELSE 'pending' END`. A constraint `chk_activities_completed_consistency` formaliza o invariante I5 no banco: `(status = 'completed') = (completed_at IS NOT NULL)`. Adicionar `priority VARCHAR(10) NOT NULL DEFAULT 'medium'` e criar `digest_action_tokens` com `token_hash TEXT NOT NULL` e índice único `uq_digest_action_tokens_hash`.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de migração com Testcontainers: (a) banco sem `status` migra corretamente; (b) linha com `completed_at` preenchido vira `status='completed'`; (c) linha sem `completed_at` vira `status='pending'`; (d) constraint rejeita `(status='completed', completed_at NULL)`.
- [ ] **ST-02 — Green:** gerar migration EF Core (`AddActivityStatusPriority`) com SQL bruto para o backfill e as constraints; criar `digest_action_tokens` com `token_hash`.
- [ ] **ST-03 — Refactor:** verificar rollback da migration (Down); documentar o backfill na migration com comentário SQL.
- [ ] **ST-04 — Encerramento:** testes de migration verdes com Testcontainers; `dotnet ef database update` em banco existente sem erro; commit `feat(act): migration dd-001 status priority backfill constraints token-hash` e push.

#### Critérios de Aceite

- [ ] Backfill: todas as linhas com `completed_at IS NOT NULL` têm `status='completed'` após a migration.
- [ ] Backfill: todas as linhas com `completed_at IS NULL` têm `status='pending'` após a migration.
- [ ] `CHECK (activity_type IN ('meeting','follow_up','call','email','task'))` aplicado.
- [ ] `CHECK (status IN ('pending','in_progress','completed','cancelled'))` aplicado.
- [ ] `chk_activities_completed_consistency` rejeita inserção `(status='completed', completed_at NULL)` com erro de constraint.
- [ ] `uq_digest_action_tokens_hash` impede dois tokens com o mesmo hash.

---

### TASK-15 — Índices, RLS falha-fechada e TenantConnectionInterceptor

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/activity-management/15-rls-tenant` |
| **Worktree** | `git worktree add ../worktrees/activity-management/15-rls-tenant -b feat/activity-management/15-rls-tenant` |
| **Status** | [ ] |
| **Depende de** | TASK-14 |
| **Entregável** | Dois índices obrigatórios criados, RLS habilitada e falha-fechada em `activities` e `digest_action_tokens`, `TenantConnectionInterceptor` implementado e testado com Testcontainers; teste de isolamento cross-tenant verde como gate de CI |
| **Mapeia** | RNF 1 (isolamento), RNF 4 (performance última atividade), DD-002, ADR-0001, design.md §6.1/§7/§14 |
| **Camada principal** | Infrastructure |

#### Objetivo

Criar `idx_activities_tenant_opportunity_completed` em `(tenant_id, opportunity_id, completed_at)` e `idx_activities_tenant_owner_due` parcial em `(tenant_id, owner_id, due_at) WHERE status IN ('pending','in_progress')` (design.md §7). Habilitar RLS com `ALTER TABLE activities ENABLE ROW LEVEL SECURITY; FORCE ROW LEVEL SECURITY` e política `rls_activities_tenant` comparando `tenant_id = current_setting('app.current_tenant')::uuid`. `TenantConnectionInterceptor` executa `SET app.current_tenant = @tenantId` ao alugar cada conexão do pool. EF Core Global Query Filter por `tenant_id` como segunda camada.

#### Subtasks

- [ ] **ST-01 — Red:** escrever teste de isolamento cross-tenant com Testcontainers: tenant A não enxerga atividades do tenant B, nem via EF nem via SQL direto; verificar que query sem `app.current_tenant` setado retorna zero linhas (RLS falha-fechada).
- [ ] **ST-02 — Green:** criar migration com índices e DDL de RLS; implementar `TenantConnectionInterceptor` registrado no `DbContext`; configurar EF Global Query Filter por `tenant_id`.
- [ ] **ST-03 — Refactor:** verificar que o interceptor é executado antes de qualquer comando EF; garantir que `FORCE ROW LEVEL SECURITY` está ativo; confirmar que o mesmo isolamento vale para `digest_action_tokens`.
- [ ] **ST-04 — Encerramento:** teste de isolamento cross-tenant verde; adicionado ao pipeline de CI como gate de merge (RNF 1.3/1.4); commit `feat(act): indices rls falha-fechada tenant-interceptor cross-tenant-ci-gate` e push.

#### Critérios de Aceite

- [ ] `idx_activities_tenant_opportunity_completed` e `idx_activities_tenant_owner_due` criados e confirmados via `\d activities`.
- [ ] RLS habilitada com `FORCE ROW LEVEL SECURITY` em `activities` e `digest_action_tokens`.
- [ ] Tenant A não enxerga nenhuma atividade do tenant B — testado via API e via SQL direto.
- [ ] Query sem `app.current_tenant` setado retorna zero linhas (falha-fechada).
- [ ] EF Global Query Filter filtra por `tenant_id` como segunda camada de defesa.
- [ ] Teste de isolamento cross-tenant é gate de merge no CI (RNF 1.3).

### TASK-16 — Outbox transacional, PiiMasker e AuditPublisher

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/activity-management/16-outbox-audit` |
| **Worktree** | `git worktree add ../worktrees/activity-management/16-outbox-audit -b feat/activity-management/16-outbox-audit` |
| **Status** | [ ] |
| **Depende de** | TASK-13, TASK-15 |
| **Entregável** | Outbox transacional (`outbox_messages`) com relay worker que publica em Cloud Pub/Sub, `PiiMasker` mascarando título/descrição em logs e deltas de auditoria, `AuditPublisher` gravando em `audit_logs` append-only, trigger de imutabilidade e `REVOKE UPDATE/DELETE/TRUNCATE` |
| **Mapeia** | RNF 2 (auditoria imutável), RNF 7 (PII), DD-007 (outbox), DD-009 (texto livre), Req 14 (eventos), design.md §6.5/§6.6/§11 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar o padrão Outbox: eventos de domínio acumulados pelo agregado são gravados em `outbox_messages` na mesma transação via `TransactionBehavior` (TASK-06). Um relay background worker lê pendentes e publica em Cloud Pub/Sub (`azim-activities`), marcando `published_at`. `PiiMasker` transforma `title` e `description` em `[MASKED]` antes de qualquer log ou inclusão em `delta_json` de auditoria (DD-009). `AuditPublisher` grava em `audit_logs` com `REVOKE UPDATE, DELETE, TRUNCATE ON audit_logs FROM app` e trigger `trg_audit_logs_immutable` prevenindo modificação.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de integração (Testcontainers): (a) escrita + evento acumulado → `outbox_messages` gravado na mesma transação; (b) relay publica e marca `published_at`; (c) tentativa de UPDATE em `audit_logs` levanta exceção (trigger); (d) `PiiMasker` não expõe título/descrição no `delta_json`.
- [ ] **ST-02 — Green:** implementar `OutboxMessage` entity, `OutboxPublisher` (grava via EF na tx), relay worker (`IHostedService`), `PiiMasker`, `AuditPublisher` (grava `audit_logs`); criar migration com trigger de imutabilidade e REVOKE.
- [ ] **ST-03 — Refactor:** garantir deduplicação do relay por `(event_type, dedup_key)` (índice único); confirmar que relay reprocessa falhas de publicação sem duplicar eventos já publicados.
- [ ] **ST-04 — Encerramento:** `Infrastructure.Tests` (outbox, audit) verdes; trigger de imutabilidade testado; commit `feat(act): outbox-transacional relay pii-masker audit-publisher trigger-imutavel` e push.

#### Critérios de Aceite

- [ ] Evento e escrita de domínio persistidos atomicamente: rollback da escrita cancela o evento no outbox.
- [ ] `audit_logs`: UPDATE, DELETE, TRUNCATE falham com erro de trigger.
- [ ] `PiiMasker` substitui `title` e `description` por `[MASKED]` em todo log e `delta_json`.
- [ ] Relay não republica evento já com `published_at` preenchido.
- [ ] `audit_logs` inclui `correlation_id` na conclusão via token (RNF 2.4).

---

### TASK-17 — DigestActionTokenAdapter e adapters de leitura (Opportunity, Account)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/activity-management/17-adapters` |
| **Worktree** | `git worktree add ../worktrees/activity-management/17-adapters -b feat/activity-management/17-adapters` |
| **Status** | [ ] |
| **Depende de** | TASK-15, TASK-16 |
| **Entregável** | `DigestActionTokenAdapter` (implementa `IDigestActionTokenPort` consultando `digest_action_tokens` por hash), `OpportunityReadAdapter` (HTTP/gRPC interno via `IOpportunityReadPort`) e `AccountReadAdapter` (HTTP/gRPC interno via `IAccountReadPort`) com testes de integração |
| **Mapeia** | Req 3 (vínculos), Req 7 (token), DD-003 (token hash), RNF 5 (uso único), design.md §6.4 (integrações externas) |
| **Camada principal** | Infrastructure |

#### Objetivo

`DigestActionTokenAdapter`: consulta `digest_action_tokens` pelo `token_hash` (hash do token opaco apresentado pelo usuário), verifica existência/expiração/uso e marca `used_at` na transação corrente. Como digest e activity-management coabitam o mesmo banco (`azim-api`), o acesso é por repositório EF dedicado sob RLS (DD-003). `OpportunityReadAdapter` e `AccountReadAdapter` chamam APIs internas via mTLS com timeout + retry + circuit breaker (design.md §6.4).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de `DigestActionTokenAdapter` com Testcontainers (token válido, expirado, já usado, inexistente); escrever testes de `OpportunityReadAdapter` e `AccountReadAdapter` com WireMock/HttpMessageHandler fake.
- [ ] **ST-02 — Green:** implementar os 3 adapters; configurar Polly (timeout, retry, circuit breaker) nos adapters HTTP; registrar adapters no DI como implementações das interfaces de port.
- [ ] **ST-03 — Refactor:** garantir que `DigestActionTokenAdapter` usa comparação por hash e não armazena o token em claro; confirmar RLS aplicada na consulta a `digest_action_tokens`.
- [ ] **ST-04 — Encerramento:** `Infrastructure.Tests` (adapters) verdes; commit `feat(act): digestactiontokenadapter opportunity account adapters polly` e push.

#### Critérios de Aceite

- [ ] `DigestActionTokenAdapter.FindByHashAsync` retorna nulo para token inexistente (anti-enumeração na camada de dados).
- [ ] `DigestActionTokenAdapter.MarkUsedAsync` atualiza `used_at` somente quando `used_at` ainda é nulo (uso único).
- [ ] `OpportunityReadAdapter` e `AccountReadAdapter` propagam `correlation_id` e `tenant_id` nas chamadas internas.
- [ ] Falha no adapter HTTP retorna falha-fechada (não cria atividade com vínculo inválido).
- [ ] Circuit breaker testado com falhas consecutivas configuradas.

---

### TASK-18 — ActivitiesController, middleware e OpenAPI

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contratos |
| **Branch** | `feat/activity-management/18-activities-ctrl` |
| **Worktree** | `git worktree add ../worktrees/activity-management/18-activities-ctrl -b feat/activity-management/18-activities-ctrl` |
| **Status** | [ ] |
| **Depende de** | TASK-13, TASK-17 |
| **Entregável** | `ActivitiesController` com 11 endpoints REST documentados em OpenAPI, middleware de `CorrelationId` e exception handling global mapeando exceptions para o catálogo de erros ACT-ERR-001..011 |
| **Mapeia** | Req 1, 2, 5, 6, 8, 13, design.md §8, erros ACT-ERR-001..011 (exceto ACT-ERR-008/009 — em TASK-19) |
| **Camada principal** | Api |

#### Objetivo

Implementar os endpoints conforme design.md §8: `GET /api/v1/activities`, `POST`, `GET /{id}`, `PUT /{id}`, `DELETE /{id}`, `PATCH /{id}/complete`, `PATCH /{id}/reschedule`, `GET /me/day`, `GET /me/week`, `GET /overdue?owner={id}`, `GET /today?owner={id}`. Autenticação Bearer JWT em todos. Middleware global captura exceptions de domínio e aplica o formato `{ "error", "code", "correlationId" }` (rule `api-and-contracts.md`). OpenAPI gerado automaticamente com exemplos de request/response e códigos HTTP corretos.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de contrato (WebApplicationFactory) para cada endpoint: código HTTP esperado, formato de erro, headers de correlação; verificar que GET `/me/day` retorna as 3 faixas.
- [ ] **ST-02 — Green:** implementar `ActivitiesController`, middleware `CorrelationIdMiddleware`, `GlobalExceptionHandlerMiddleware` e configuração OpenAPI (Swashbuckle/NSwag).
- [ ] **ST-03 — Refactor:** validar que todos os erros do catálogo têm mapeamento no exception handler; confirmar que `correlation_id` aparece em todo response de erro.
- [ ] **ST-04 — Encerramento:** `Api.Tests` (activities controller) verdes; OpenAPI documento gerado sem warnings; commit `feat(act): activitiescontroller openapi middleware correlation-id exception-handler` e push.

#### Critérios de Aceite

- [ ] `PATCH /activities/{id}/complete` retorna 200 com `completedAt` preenchido e bloco de sugestão quando aplicável.
- [ ] Reconcluir atividade já `completed` retorna 200 sem alterar `completedAt` (idempotente).
- [ ] `GET /me/day` retorna objeto com campos `overdue[]`, `today[]`, `upcoming[]`.
- [ ] Todos os responses de erro incluem `correlationId`.
- [ ] OpenAPI documento inclui schemas de request/response e códigos HTTP para cada endpoint.

### TASK-19 — DigestActionsController, InternalController e anti-enumeração

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contratos |
| **Branch** | `feat/activity-management/19-digest-internal-ctrl` |
| **Worktree** | `git worktree add ../worktrees/activity-management/19-digest-internal-ctrl -b feat/activity-management/19-digest-internal-ctrl` |
| **Status** | [ ] |
| **Depende de** | TASK-09, TASK-18 |
| **Entregável** | `DigestActionsController` (`POST /api/v1/digest-actions/{token}`) e `InternalController` (`POST /internal/overdue-scan`) implementados; anti-enumeração com resposta indistinguível em forma e tempo para PBT-03 |
| **Mapeia** | Req 7 (conclusão via link), Req 11 (scan), PBT-03 (anti-enumeração), design.md §8/§10, ACT-ERR-008/009 |
| **Camada principal** | Api |

#### Objetivo

`DigestActionsController`: sem autenticação JWT — a autoridade vem do token opaco no path. Roteamento para `ProcessDigestActionCommand` conforme o campo `action` do token (`complete`/`reschedule`). A resposta para token inexistente/malformado deve ser indistinguível em **forma** da resposta para atividade inacessível (PBT-03); a indistinguibilidade de **tempo** (comparação de tempo constante) é reforçada na TASK-23. `InternalController` aceita apenas chamadas via mTLS/OIDC do Cloud Scheduler; dispara `ScanOverdueActivitiesCommand`.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de API (WebApplicationFactory) para: token válido → 200; token já usado → 200 idempotente; token expirado → 410; token inválido → 404 com corpo idêntico a atividade inexistente; POST `/internal/overdue-scan` sem autenticação de scheduler → 401.
- [ ] **ST-02 — Green:** implementar `DigestActionsController` sem `[Authorize]` JWT; mapear os 4 ramos de resposta; implementar `InternalController` com verificação de header de autenticação de scheduler.
- [ ] **ST-03 — Refactor:** garantir que nenhuma mensagem de erro de token revela dados de atividade; confirmar que PBT-03 (forma) é satisfeito nos testes de API.
- [ ] **ST-04 — Encerramento:** `Api.Tests` (digest-actions, internal) verdes; commit `feat(act): digestactionscontroller internalcontroller anti-enumeracao pbt-03-forma` e push.

#### Critérios de Aceite

- [ ] `POST /digest-actions/{tokenValido}` → 200 com atividade concluída.
- [ ] `POST /digest-actions/{tokenJaUsado}` → 200 com confirmação idempotente (MSG-029).
- [ ] `POST /digest-actions/{tokenExpirado}` → 410 ACT-ERR-009 com orientação de portal (MSG-030).
- [ ] `POST /digest-actions/{tokenInexistente}` → 404 ACT-ERR-008 — corpo idêntico ao de atividade inacessível.
- [ ] `POST /internal/overdue-scan` sem header de scheduler → 401.
- [ ] Nenhuma mensagem de erro expõe título ou conteúdo de atividade.

---

### TASK-20 — Contratos de evento v1 e Pact provider tests

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contratos |
| **Branch** | `feat/activity-management/20-event-contracts` |
| **Worktree** | `git worktree add ../worktrees/activity-management/20-event-contracts -b feat/activity-management/20-event-contracts` |
| **Status** | [ ] |
| **Depende de** | TASK-16 |
| **Entregável** | DTOs de evento `ActivityCreated.v1`, `ActivityCompleted.v1`, `ActivityOverdue.v1` no projeto `Contracts` com envelope canônico; Pact provider tests verificando os contratos para os consumidores digest e opportunity-pipeline |
| **Mapeia** | Req 14 (publicar eventos), RNF 7 (sem PII no payload), design.md §9 (AsyncAPI) |
| **Camada principal** | Contracts / Tests |

#### Objetivo

Formalizar os contratos de integração dos eventos publicados no tópico `azim-activities`. Os DTOs em `ActivityManagement.Contracts.Events` carregam o envelope canônico (`eventId`, `eventType`, `eventVersion`, `tenantId`, `correlationId`, `causationId`, `occurredAt`) e os payloads específicos — **sem `title`/`description`** (RNF 7.2). Pact provider tests verificam que o `activity-management` satisfaz os contratos esperados pelo digest (para `activity.created.v1` e `activity.overdue.v1`) e pelo opportunity-pipeline (para `activity.completed.v1`).

#### Subtasks

- [ ] **ST-01 — Red:** escrever Pact provider tests que falham (contratos de consumidor ainda não satisfeitos pelo provider); verificar que `ActivityCompleted.v1` não contém `title` nem `description`.
- [ ] **ST-02 — Green:** implementar os 3 record DTOs em `Contracts/Events/`; garantir que o Outbox serializa usando os DTOs do Contracts; passar nos Pact provider tests.
- [ ] **ST-03 — Refactor:** verificar compatibilidade retroativa do schema v1 (adição de campo opcional não quebra consumidores); confirmar que `eventVersion` é `"v1"`.
- [ ] **ST-04 — Encerramento:** Pact verdes; `dotnet test Contracts` + `Application.Tests` (serialização) verdes; commit `feat(act): contratos-evento v1 activitycreated completed overdue pact-provider` e push.

#### Critérios de Aceite

- [ ] `ActivityCreated.v1`, `ActivityCompleted.v1`, `ActivityOverdue.v1` implementados sem `title`/`description`.
- [ ] Pact provider test para digest (consumidor de `created.v1` e `overdue.v1`) verde.
- [ ] Pact provider test para opportunity-pipeline (consumidor de `completed.v1`) verde.
- [ ] Envelope de evento contém `tenantId` e `correlationId` em todos os eventos (Req 14.5).
- [ ] Versão dos eventos é `"v1"`; mudança incompatível futura criará `"v2"`.

---

### TASK-21 — Testes de API — RBAC, erros, cross-tenant CI gate

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contratos |
| **Branch** | `feat/activity-management/21-api-tests` |
| **Worktree** | `git worktree add ../worktrees/activity-management/21-api-tests -b feat/activity-management/21-api-tests` |
| **Status** | [ ] |
| **Depende de** | TASK-18, TASK-19 |
| **Entregável** | Suite de testes de API cobrindo: todos os 11 erros do catálogo (ACT-ERR-001..011), RBAC por papel (Vendedor/Gestor/Viewer) e isolamento cross-tenant como gate de CI |
| **Mapeia** | Req 13 (visibilidade por papel), RNF 1 (isolamento tenant), design.md §12 (catálogo de erros), ADR-0001 (gate CI cross-tenant) |
| **Camada principal** | Tests / Api |

#### Objetivo

Complementar os testes dos controllers (TASK-18/19) com uma suite explícita de: (a) RBAC — Viewer não cria/edita, Vendedor não acessa atividades de outro usuário, Gestor de BU vê as da sua BU; (b) catálogo de erros — cada ACT-ERR-NNN tem pelo menos um teste que confirma código HTTP e estrutura de resposta; (c) cross-tenant — usuário autenticado como tenant A não acessa, atualiza nem conclui atividades do tenant B, verificado com WebApplicationFactory + banco real Testcontainers. Este teste é registrado como gate de CI conforme ADR-0001/RNF 1.3.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de RBAC (Viewer tentando criar → 403), cross-tenant (tenant A acessando ID de tenant B → 404 indistinguível), e um teste por erro do catálogo ACT-ERR-001..011.
- [ ] **ST-02 — Green:** conectar WebApplicationFactory com Testcontainers PostgreSQL; configurar JWT de teste por papel/tenant; garantir que todos os testes passam.
- [ ] **ST-03 — Refactor:** extrair factory de JWT de teste para helper reutilizável; garantir que os testes de cross-tenant cobrem caminho via EF e via SQL direto.
- [ ] **ST-04 — Encerramento:** `Api.Tests` ≥ 80%; cross-tenant gate adicionado ao CI como job separado; commit `test(act): rbac catalog-erros cross-tenant ci-gate api-tests` e push.

#### Critérios de Aceite

- [ ] Cada erro ACT-ERR-001..011 tem ao menos um teste automatizado verificando código HTTP e campo `code` no response.
- [ ] Viewer não consegue criar nem editar atividade (403 ACT-ERR-007).
- [ ] Tenant A não enxerga nem modifica atividades do tenant B (404 ACT-ERR-003 — indistinguível).
- [ ] Teste cross-tenant é gate de merge no CI (falha bloqueia merge).
- [ ] `Api.Tests` coverage ≥ 80%.

### TASK-22 — Observabilidade — métricas, logs, traces, alertas

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `feat/activity-management/22-observability` |
| **Worktree** | `git worktree add ../worktrees/activity-management/22-observability -b feat/activity-management/22-observability` |
| **Status** | [ ] |
| **Depende de** | TASK-16, TASK-19 |
| **Entregável** | Métricas Prometheus obrigatórias (`activities_created_total`, `activities_completed_total`, `activities_overdue_total`, `digest_action_tokens_used_total`, `digest_action_tokens_expired_total`) expostas; logs estruturados JSON com `correlation_id`/`tenant_id` sem PII; traces OpenTelemetry; health/readiness/liveness; alerta de overdue |
| **Mapeia** | RNF 6 (observabilidade), RNF 7 (PII em logs), DD-009, design.md §11 |
| **Camada principal** | Infrastructure / Api |

#### Objetivo

Implementar os três pilares de observabilidade do design.md §11. Métricas: contadores incrementados nos handlers correspondentes (não no controller). Logs estruturados: `CorrelationLoggingBehavior` (TASK-06) já injeta os campos; esta TASK finaliza o scan anti-PII e verifica que nenhum campo de texto livre aparece. Traces OpenTelemetry: spans para handlers de Command/Query, portas de leitura e banco; `correlation_id` como atributo do root span. Health checks: Cloud SQL, dependências internas. Alerta: configuração de alerta quando `activities_overdue_total` cresce acima da linha de base (RNF 6.4).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes que verificam: (a) as 5 métricas obrigatórias são incrementadas nas operações correspondentes; (b) log de criação/conclusão não contém `title` nem `description`; (c) health endpoint retorna `healthy` com banco disponível e `unhealthy` sem banco.
- [ ] **ST-02 — Green:** adicionar contadores Prometheus aos handlers; configurar OpenTelemetry com trace de handler + banco + portas; implementar health checks via `IHealthCheck`; configurar definição de alerta de overdue.
- [ ] **ST-03 — Refactor:** verificar que `PiiMasker` (TASK-16) cobre todos os pontos de log; confirmar que traces propagam `correlation_id` corretamente.
- [ ] **ST-04 — Encerramento:** testes de observabilidade verdes; commit `feat(act): observability metricas prometheus otel health alertas` e push.

#### Critérios de Aceite

- [ ] `GET /metrics` expõe `activities_created_total`, `activities_completed_total`, `activities_overdue_total`.
- [ ] Log de operação de criação não contém `title` nem `description` em claro.
- [ ] `GET /health/ready` retorna 200 quando banco disponível e 503 quando indisponível.
- [ ] Span de handler inclui `correlation_id`, `tenant_id` e `activity_id` como atributos.
- [ ] Alerta de `activities_overdue_total` configurado com threshold de linha de base.

---

### TASK-23 — Hardening de segurança — tempo constante, anti-PII, privilégio mínimo

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `feat/activity-management/23-security-hardening` |
| **Worktree** | `git worktree add ../worktrees/activity-management/23-security-hardening -b feat/activity-management/23-security-hardening` |
| **Status** | [ ] |
| **Depende de** | TASK-19, TASK-22 |
| **Entregável** | Comparação de tempo constante na validação de token implementada; scan de logs anti-PII no CI verificando que `title`/`description` não vazam; `REVOKE UPDATE, DELETE, TRUNCATE ON audit_logs` confirmado; Workload Identity / Secret Manager configurados |
| **Mapeia** | RNF 1 (isolamento), RNF 5 (token anti-enumeração), RNF 7 (privacidade PII), PBT-03 (indistinguibilidade de tempo), design.md §10 |
| **Camada principal** | Infrastructure / Api |

#### Objetivo

PBT-03 exige que a resposta para token inexistente seja indistinguível em **forma e em tempo** da resposta para atividade inacessível. A indistinguibilidade de tempo é alcançada por comparação de tempo constante no lookup do hash do token (independente de existência ou não). Scan anti-PII: script ou teste que varre os logs emitidos em testes de integração e falha se encontrar campos `title` ou `description` fora de `[MASKED]`. Workload Identity substitui service-account key em segredos. `REVOKE` em `audit_logs` é confirmado ao nível de banco (psql/teste de integração).

#### Subtasks

- [ ] **ST-01 — Red:** escrever PBT-03 completo com medição de tempo (tempo de resposta para token inexistente não deve ser significativamente menor que para token válido); escrever scan anti-PII (grep em logs de integração); escrever teste que confirma `UPDATE audit_logs` falha.
- [ ] **ST-02 — Green:** implementar lookup de token com tempo constante (ex: `CryptographicOperations.FixedTimeEquals` ou equivalente); configurar Workload Identity; confirmar REVOKE aplicado na migration (TASK-16).
- [ ] **ST-03 — Refactor:** verificar que nenhum `Console.Write` ou `ILogger` em claro usa campos de texto livre; confirmar que secrets não estão em repositório (`.env`, `appsettings.json`).
- [ ] **ST-04 — Encerramento:** PBT-03 (tempo) verde; scan anti-PII integrado ao CI; commit `feat(act): constant-time token anti-pii scan workload-identity privilege-minimal` e push.

#### Critérios de Aceite

- [ ] Lookup de token usa comparação de tempo constante: tempo de resposta para token inexistente estatisticamente similar ao de token existente.
- [ ] Scan anti-PII no CI: nenhum log de integração contém `title` ou `description` sem mascaramento.
- [ ] `UPDATE audit_logs` via role `app` falha com permissão negada.
- [ ] Nenhum secret em repositório (confirmado por `git log --all -p | grep -i 'secret\|password\|key'`).
- [ ] PBT-03 (forma + tempo) verde com ≥ 100 casos.

---

### TASK-24 — DoD final — coverage gates, PBT-01..05, data-model atualizado

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `feat/activity-management/24-dod-final` |
| **Worktree** | `git worktree add ../worktrees/activity-management/24-dod-final -b feat/activity-management/24-dod-final` |
| **Status** | [ ] |
| **Depende de** | TASK-22, TASK-23 |
| **Entregável** | PBT-01..05 todos verdes; coverage gates por camada atendidos; `data-model §BC-04` e `§BC-06` atualizados refletindo `status`/`priority`/`token_hash`; `approvals.yaml` registrado com VAL-ACT-01/02 e VAL-TRD-05; DoD do design.md §19 completamente checado |
| **Mapeia** | design.md §19 (DoD completo), DD-001 (data-model BC-04), DD-003 (data-model BC-06), PBT-01..05 |
| **Camada principal** | Docs / Tests |

#### Objetivo

TASK de encerramento do módulo. Verificar que todos os itens do DoD do design.md §19 estão concluídos. Consolidar os coverage gates por camada (Domain ≥ 95%, Application ≥ 85%, Infrastructure ≥ 70%, Api ≥ 80%). Registrar no `approvals.yaml` as pendências VAL-ACT-01 (fronteira do BC digest/activity), VAL-ACT-02 (TTL do token) e VAL-TRD-05 para aprovação humana antes do go-live. Atualizar `data-model §BC-04` com as colunas `status` e `priority`; atualizar `§BC-06` com a coluna `token_hash` em `digest_action_tokens`.

#### Subtasks

- [ ] **ST-01:** executar todos os 5 PBTs em modo de regressão com `--number-of-tests 500`; verificar que todos passam sem falha.
- [ ] **ST-02:** verificar coverage gates por camada: `Domain.Tests ≥ 95%`, `Application.Tests ≥ 85%`, `Infrastructure.Tests ≥ 70%`, `Api.Tests ≥ 80%`; registrar justificativa para qualquer camada abaixo do gate.
- [ ] **ST-03:** percorrer o DoD do design.md §19 item a item; marcar cada checkbox; registrar itens em aberto com justificativa.
- [ ] **ST-04 — Docs:** atualizar `docs/product/data-model/data-model.md §BC-04` e `§BC-06`; registrar VAL-ACT-01, VAL-ACT-02, VAL-TRD-05 no `approvals.yaml`; atualizar README do módulo com status final.
- [ ] **ST-05 — Encerramento:** CI verde em todos os jobs; commit `docs(act): dod-final coverage-gates data-model approvals pbt-01-05` e push.

#### Critérios de Aceite

- [ ] PBT-01..05 todos verdes com ≥ 500 casos cada em modo de regressão.
- [ ] `Domain.Tests` ≥ 95%, `Application.Tests` ≥ 85%, `Infrastructure.Tests` ≥ 70%, `Api.Tests` ≥ 80%.
- [ ] Todos os 15 itens do DoD (design.md §19) marcados como concluídos ou com justificativa registrada.
- [ ] `data-model §BC-04` reflete as colunas `status` e `priority`.
- [ ] `data-model §BC-06` reflete a coluna `token_hash`.
- [ ] VAL-ACT-01, VAL-ACT-02, VAL-TRD-05 registrados em `approvals.yaml` aguardando aprovação humana.

## 5. Matriz de Rastreabilidade

| Origem | Descrição curta | TASKs | Status |
|--------|-----------------|-------|--------|
| Req 1 | Criar atividade (tipo, título, dueAt, responsável, defaults) | TASK-04, TASK-07, TASK-18 | [ ] |
| Req 2 | Atualizar e excluir atividade (RBAC, terminal) | TASK-04, TASK-07, TASK-18 | [ ] |
| Req 3 | Vincular a oportunidade e/ou conta (FK real, mesma tenant) | TASK-04, TASK-07, TASK-17 | [ ] |
| Req 4 | Máquina de estados de status (4 estados, transições válidas) | TASK-02, TASK-04, TASK-14 | [ ] |
| Req 5 | Visão "Meu dia / Minha semana" (3 faixas no fuso do tenant) | TASK-05, TASK-11, TASK-18 | [ ] |
| Req 6 | Concluir em um clique pela lista (idempotente) | TASK-04, TASK-08, TASK-18 | [ ] |
| Req 7 | Concluir via link autenticado do digest (token) | TASK-09, TASK-17, TASK-19 | [ ] |
| Req 8 | Reagendar atividade (via lista e via token) | TASK-04, TASK-10, TASK-18 | [ ] |
| Req 9 | Sugerir próxima atividade ao concluir | TASK-08 | [ ] |
| Req 10 | Saúde do funil: ao menos 1 follow-up futuro por oportunidade | TASK-05, TASK-11 | [ ] |
| Req 11 | Detectar atividade vencida (scan agendado, ActivityOverdue) | TASK-05, TASK-12, TASK-19 | [ ] |
| Req 12 | Expor última atividade concluída por oportunidade (estagnação) | TASK-12, TASK-15, TASK-18 | [ ] |
| Req 13 | Visibilidade conforme papel e BU (RBAC, ScopeSpecification) | TASK-05, TASK-06, TASK-21 | [ ] |
| Req 14 | Publicar eventos de domínio (Created, Completed, Overdue) | TASK-03, TASK-16, TASK-20 | [ ] |
| RNF 1 | Isolamento por tenant (tenant_id + EF filter + RLS) | TASK-15, TASK-21 | [ ] |
| RNF 2 | Auditoria imutável de toda escrita (audit_logs append-only) | TASK-16 | [ ] |
| RNF 3 | Idempotência da conclusão via token | TASK-08, TASK-09 | [ ] |
| RNF 4 | Performance da consulta de última atividade (índice obrigatório) | TASK-15 | [ ] |
| RNF 5 | Token de ação de uso único e expirável (anti-enumeração) | TASK-09, TASK-17, TASK-23 | [ ] |
| RNF 6 | Observabilidade (métricas, logs correlacionados, traces, alertas) | TASK-22 | [ ] |
| RNF 7 | Privacidade em texto livre (PII mascarada em logs e eventos) | TASK-16, TASK-23 | [ ] |
| PBT-01 | Integridade da state machine (sequências arbitrárias de transição) | TASK-02 | [ ] |
| PBT-02 | Idempotência da conclusão (N chamadas → 1 ActivityCompleted) | TASK-08, TASK-09 | [ ] |
| PBT-03 | Anti-enumeração do token (forma e tempo indistinguíveis) | TASK-09, TASK-19, TASK-23 | [ ] |
| PBT-04 | Invariante de atividade vencida (overdue sse dueAt < ref e não terminal) | TASK-05 | [ ] |
| PBT-05 | Saúde do funil após sugestão aceita (≥1 follow-up futuro preservado) | TASK-05, TASK-08 | [ ] |
| DD-001 | Adicionar status/priority à tabela activities + backfill | TASK-14 | [ ] |
| DD-002 | Isolamento multi-tenant em defesa em profundidade | TASK-15 | [ ] |
| DD-003 | Validação e consumo do digest_action_token por hash | TASK-09, TASK-17 | [ ] |
| DD-004 | Conclusão idempotente por estado + dedup de evento | TASK-04, TASK-08, TASK-09 | [ ] |
| DD-005 | ActivityOverdue derivado de scan/consulta, não de transição de estado | TASK-12 | [ ] |
| DD-006 | Sugestão de próxima atividade é não-bloqueante (UI-driven) | TASK-08 | [ ] |
| DD-007 | Publicação de eventos via Outbox transacional + Pub/Sub | TASK-16 | [ ] |
| DD-008 | Faixas de data no fuso horário IANA do tenant | TASK-05, TASK-11 | [ ] |
| DD-009 | Texto livre (título/descrição) tratado como PII potencial | TASK-16, TASK-22, TASK-23 | [ ] |
| ADR-0001 | Isolamento multi-tenant em defesa em profundidade (gate CI) | TASK-15, TASK-21 | [ ] |

## 6. Coverage Gates

| Camada | Gate | Tipo de teste esperado | TASK de referência |
|--------|------|------------------------|--------------------|
| Domain (`Domain.Tests`) | ≥ 95% | Unitários de invariantes, objetos de valor, specifications; PBT-01, PBT-04, PBT-05 | TASK-02..TASK-05 |
| Application (`Application.Tests`) | ≥ 85% | Unitários de handlers, validators, behaviors; PBT-02, PBT-03 (forma) | TASK-06..TASK-12 |
| Infrastructure (`Infrastructure.Tests`) | ≥ 70% | Integração com Testcontainers/PostgreSQL: repositório, RLS, Outbox, migration, adapters | TASK-13..TASK-17 |
| Api (`Api.Tests`) | ≥ 80% | Contrato, RBAC, HTTP codes, catálogo de erros, cross-tenant | TASK-18..TASK-21 |
| Architecture (`Architecture.Tests`) | 100% das regras | Regras de dependência entre camadas Clean Architecture | TASK-01 |
| Segurança | Cobertura por cenário crítico | Cross-tenant (gate CI), anti-enumeração (PBT-03), scan anti-PII, RBAC | TASK-15, TASK-21, TASK-23 |
| Observabilidade | Cobertura por fluxo crítico | Métricas por operação, logs correlacionados, health check | TASK-22 |

**Regras:**
- Coverage gate não substitui qualidade de teste; PBT obrigatório onde há propriedade.
- Camada abaixo do gate deve ter justificativa registrada na TASK de encerramento (TASK-24).
- O teste de isolamento cross-tenant é gate de merge no CI (ADR-0001 / KPI-06, RNF 1.3).

## 7. Critérios de Encerramento

### 7.1 Encerramento de TASK

Uma TASK só pode ser marcada como `[X]` quando:

- todas as subtasks concluídas
- testes aplicáveis verdes (unitários, integração, PBT conforme a TASK)
- coverage gate da camada atendido ou justificativa registrada
- `dotnet format` / lint executado sem warnings novos
- nenhum warning novo introduzido no build
- commit em Conventional Commits realizado
- push da branch realizado
- documentação atualizada quando aplicável

### 7.2 Encerramento de Onda

Uma onda só pode ser considerada concluída quando:

- todas as TASKs da onda estiverem `[X]`
- CI verde (build + testes + lint)
- conflitos com `main`/`develop` resolvidos
- PR da onda aberto, aprovado ou mergeado conforme regra do projeto
- riscos da onda tratados ou registrados
- README do módulo sincronizado quando aplicável

### 7.3 Encerramento do Módulo

O módulo só pode ser considerado pronto quando:

- todas as 6 ondas (TASK-01..TASK-24) concluídas
- matriz de rastreabilidade completa (todos os itens `[X]`)
- `requirements.md`, `design.md` e `tasks.md` consistentes entre si
- PBT-01..05 todos verdes
- coverage gates por camada atendidos
- isolamento cross-tenant confirmado como gate de CI
- catálogo de erros ACT-ERR-001..011 coberto por testes
- Outbox e eventos de domínio validados por Pact
- observabilidade mínima implementada e testada
- segurança validada (anti-enumeração, RLS, privilégio mínimo, anti-PII)
- `data-model §BC-04/§BC-06` atualizados
- VAL-ACT-01, VAL-ACT-02, VAL-TRD-05 registrados em `approvals.yaml`

## 8. Riscos de Execução

| Código | Risco de execução | Onda afetada | Mitigação no plano |
|--------|-------------------|--------------|-------------------|
| RE-01 | Migration DD-001 com backfill em tabela grande pode exceder timeout | Onda 4 (TASK-14) | Executar backfill em lotes com `UPDATE ... WHERE ctid IN (SELECT ctid FROM activities WHERE status IS NULL LIMIT 10000)`; testar em banco clone |
| RE-02 | Testcontainers PostgreSQL lento em CI aumenta tempo de pipeline | Onda 4 (TASK-13..TASK-17) | Paralelizar jobs de infra no CI; usar imagem PostgreSQL pré-aquecida em cache |
| RE-03 | PBT-03 (tempo constante) flaky em ambiente de CI com variação de latência | Onda 3/5/6 (TASK-09, TASK-23) | Usar tolerância estatística (desvio padrão), não comparação rígida de ms; executar com múltiplas iterações |
| RE-04 | `IOpportunityReadPort`/`IAccountReadPort` dependem de serviços externos ainda não disponíveis | Onda 3 (TASK-07) | Usar fake/stub in-memory nos testes de Application; adapters HTTP só testados em Onda 4 (TASK-17) |
| RE-05 | Fronteira do BC digest/activity (`digest_action_tokens` co-proprietária) pode gerar conflito de schema | Onda 4 (TASK-14) | VAL-ACT-01 deve ser resolvido antes da migration de produção; em desenvolvimento usar schema isolado |
| RE-06 | TTL do token (VAL-ACT-02/VAL-TRD-05) ainda em validação; valor padrão 24h pode ser insuficiente | Onda 3 (TASK-09) | Externalizar TTL como `DIGEST_TOKEN_TTL_HOURS` em configuração; default 48h conforme requirements §RNF-5.4 |
| RE-07 | Pact provider tests podem falhar se os contratos de consumidor (digest, opportunity-pipeline) não forem fornecidos em tempo | Onda 5 (TASK-20) | Iniciar alinhamento de contrato com equipes dos consumidores na Onda 3; usar contratos stub documentados no design.md §9 como ponto de partida |

## 9. Referências

| Referência | Relação com este documento |
|------------|---------------------------|
| `docs/product/modules/activity-management/requirements.md` v0.1.0 | Fonte de todos os requisitos funcionais (Req 1..14), RNFs (RNF 1..7) e PBTs (PBT-01..05) |
| `docs/product/modules/activity-management/design.md` v0.1.0 | Fonte de todas as decisões técnicas (DD-001..009), estrutura de solução, schema, contratos de API e DoD |
| `docs/product/modules/activity-management/README.md` | Visão de módulo, riscos (RISK-ACT-01..08) e pontos a validar (VAL-ACT-01/02) |
| `docs/product/adr/0001-isolamento-multi-tenant-defesa-em-profundidade.md` | ADR que exige RLS + filtro global + test cross-tenant como gate de CI |
| `docs/product/data-model/data-model.md §BC-04, §BC-06` | Schema de referência a ser atualizado em TASK-24 (DD-001/DD-003) |
| `docs/product/trd/trd.md §activity-management` | Scheduler `activity-overdue`, eventos e integrações de transporte |
| `.forge/rules/architecture/clean-architecture.md` | Regras de dependência entre camadas enforçadas por TASK-01 |
| `.forge/rules/architecture/ddd.md` | DDD tático: agregado, objetos de valor, invariantes (TASK-02..TASK-05) |
| `.forge/rules/architecture/api-and-contracts.md` | Formato de erro, versionamento de API e contratos de evento |
| `.forge/rules/architecture/observability.md` | Três pilares de observabilidade (TASK-22) |
| `.forge/rules/architecture/security-and-compliance.md` | LGPD by design, privilege mínimo, anti-enumeração (TASK-23) |
| `.forge/rules/architecture/jwt-permissions.md` | RBAC por claim `permissions` (TASK-06, TASK-21) |
| `.forge/rules/conventions/database-naming.md` | `snake_case`, `tenant_id` obrigatório, timestamps (TASK-13, TASK-14) |
| `.forge/rules/domain/audit-immutability.md` | `audit_logs` append-only, trigger + REVOKE (TASK-16) |
