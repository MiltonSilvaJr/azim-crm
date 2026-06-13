# Tasks — ORG — Organization Management

- Versão: 0.1.1
- Data: 2026-06-13
- Status: Aprovado para desenvolvimento
- Referência base requirements: docs/product/modules/organization/requirements.md v0.1.0
- Referência base design: docs/product/modules/organization/design.md v0.1.0
- ADRs aplicáveis: ADR-0001 (Multi-tenancy pooled DB + RLS), ADR-0009 (Propagação de `correlation_id` e `tenant_id`)
- Rules aplicáveis: `.forge/rules/architecture/clean-architecture.md`, `.forge/rules/architecture/ddd.md`, `.forge/rules/architecture/api-and-contracts.md`, `.forge/rules/architecture/observability.md`, `.forge/rules/architecture/jwt-permissions.md`, `.forge/rules/domain/audit-immutability.md`, `.forge/rules/conventions/database-naming.md`, `.forge/rules/conventions/language-policy.md`

## Histórico de Versões

| Versão | Data | Status | Descrição da alteração |
|--------|------|--------|------------------------|
| 0.1.0 | 2026-06-11 | Rascunho para revisão | Criação inicial do plano de tasks a partir de requirements.md v0.1.0 e design.md v0.1.0 |
| 0.1.1 | 2026-06-13 | Aprovado para desenvolvimento | Aprovação humana (HITL #1); execução via `/forge:coding-loop` autônomo (6 ondas). |

---

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
- round-trip
- anti-enumeração
- state machines
- regras de conservação

Cada PBT mapeia explicitamente para `PBT-NN` do `requirements.md`.

Biblioteca de referência: FsCheck (via FsCheck.Xunit) ou equivalente aprovado no TRD.

### 1.3 Bite-sized Tasks

- Cada subtask deve ser estimada em menos de 2 horas.
- Cada TASK deve ser completável em até 2 dias.
- TASK maior que 2 dias deve ser quebrada.

### 1.4 Branch Model

```text
<tipo>/organization/<NN>-<slug>
```

Exemplos:

```text
feat/organization/01-bootstrap-solution
test/organization/03-domain-value-objects
fix/organization/15-rls-global-filter
```

### 1.5 Git Worktree

```sh
git worktree add ../worktrees/organization/<NN>-<slug> -b <branch>
```

### 1.6 Encerramento de TASK

Cada TASK deve encerrar com testes locais verdes, coverage gate atendido ou justificativa registrada, lint executado, commit Conventional Commits e push da branch.

### 1.7 Encerramento de Onda

Todas as TASKs da onda concluídas, CI verde, conflitos resolvidos, PR da onda aberto ou atualizado.

### 1.8 Early Exit

Se uma subtask falhar: marcar `[-]`, registrar ponto de falha, comando executado e erro principal. Não mascarar falha.

### 1.9 Convenção de Status

- `[ ]` Não iniciado
- `[-]` Em progresso
- `[X]` Concluído
- `[!]` Falhou — exige intervenção humana

### 1.10 Convenção canônica de IDs

```
TASK-NN — <título>          ← unidade atômica de invocação do task-coder
  ST-MM — <subtask>         ← etapas TDD internas; numeração reinicia a cada TASK
```

Onda é apenas agrupamento visual (campo `**Onda**` no header da TASK e seção 3). Nunca entra no ID da TASK.

---

## 2. Status Geral

| TASK | Título | Onda | Branch | Status |
|------|--------|------|--------|--------|
| TASK-01 | Criar solution e projetos Clean Architecture | Onda 1 | `feat/organization/01-bootstrap-solution` | [ ] |
| TASK-02 | Testes de arquitetura e CI mínimo | Onda 1 | `test/organization/02-architecture-tests` | [ ] |
| TASK-03 | Objetos de valor do domínio | Onda 2 | `test/organization/03-domain-value-objects` | [ ] |
| TASK-04 | Agregado BusinessUnit + pipeline entities + policies + seeds | Onda 2 | `test/organization/04-aggregate-business-unit` | [ ] |
| TASK-05 | Agregado User + UserMembership + eventos de domínio | Onda 2 | `test/organization/05-aggregate-user` | [ ] |
| TASK-06 | Agregado UserInvitation + state machine (PBT-04) | Onda 2 | `test/organization/06-aggregate-invitation` | [ ] |
| TASK-07 | Pipeline behaviors (Tenant, RBAC, Validation, Transaction, Logging) | Onda 3 | `feat/organization/07-pipeline-behaviors` | [ ] |
| TASK-08 | Handlers de BusinessUnit (Create, Rename, Deactivate) | Onda 3 | `feat/organization/08-handlers-business-unit` | [ ] |
| TASK-09 | Handlers de convite (Invite, Revoke, Accept) + PBT-03 parcial | Onda 3 | `feat/organization/09-handlers-invitation` | [ ] |
| TASK-10 | Handlers de membership + LastTenantAdminPolicy (PBT-02) | Onda 3 | `feat/organization/10-handlers-membership` | [ ] |
| TASK-11 | Handlers de configuração de pipeline (Stage, Channel, LossReason) | Onda 3 | `feat/organization/11-handlers-pipeline-config` | [ ] |
| TASK-12 | DeactivateUserCommand + FutureActivitiesSpec | Onda 3 | `feat/organization/12-handler-deactivate-user` | [ ] |
| TASK-13 | ProvisionInitialOrganizationCommand + IdempotencyBehavior (PBT-03) | Onda 3 | `feat/organization/13-handler-provision-initial` | [ ] |
| TASK-14 | Queries + MembershipCacheProjector + GetRbacContextQuery | Onda 3 | `feat/organization/14-queries-rbac-cache` | [ ] |
| TASK-15 | EF Core mappings + migration inicial + RLS global filter | Onda 4 | `feat/organization/15-ef-core-schema-rls` | [ ] |
| TASK-16 | Repositórios de domínio (BU, User, Invitation) | Onda 4 | `feat/organization/16-repositories` | [ ] |
| TASK-17 | Redis adapter IMembershipCache + Testcontainers | Onda 4 | `feat/organization/17-redis-membership-cache` | [ ] |
| TASK-18 | Outbox worker + Pub/Sub publisher + Inbox TenantProvisioned | Onda 4 | `feat/organization/18-outbox-inbox-pubsub` | [ ] |
| TASK-19 | Adapters externos (IdentityProvisioner, EmailSender, counters) | Onda 4 | `feat/organization/19-external-adapters` | [ ] |
| TASK-20 | Controllers de BusinessUnit + RBAC middleware | Onda 5 | `feat/organization/20-api-business-units` | [ ] |
| TASK-21 | Controllers de usuário e convite + anti-enumeração | Onda 5 | `feat/organization/21-api-users-invitations` | [ ] |
| TASK-22 | Controllers de pipeline config + OpenAPI + catálogo de erros | Onda 5 | `feat/organization/22-api-pipeline-config` | [ ] |
| TASK-23 | Api.Tests — contrato REST, RBAC papel×operação, isolamento | Onda 5 | `test/organization/23-api-tests-rbac` | [ ] |
| TASK-24 | Testes de contrato de eventos *.v1 | Onda 5 | `test/organization/24-contract-tests-events` | [ ] |
| TASK-25 | Observabilidade (logs estruturados, métricas, health checks, alertas) | Onda 6 | `feat/organization/25-observability` | [ ] |
| TASK-26 | Segurança e PII (mascaramento, token hash, secrets, anti-enumeração) | Onda 6 | `feat/organization/26-security-pii` | [ ] |
| TASK-27 | PBT-01 (isolamento por tenant) + PBT-05 (integridade na desativação) | Onda 6 | `test/organization/27-pbt-isolation-integrity` | [ ] |
| TASK-28 | DoD final, README sync, matriz de rastreabilidade completa | Onda 6 | `chore/organization/28-dod-readme-sync` | [ ] |

---

## 3. Ondas de Implementação

| Onda | Foco | TASKs | Critério de fechamento |
|------|------|-------|------------------------|
| Onda 1 | Bootstrap | TASK-01, TASK-02 | Solution compila; Architecture.Tests verdes; CI executa sem falha |
| Onda 2 | Domain | TASK-03..TASK-06 | Domain.Tests ≥95%; PBT-04, PBT-06, PBT-07 verdes; zero warnings de domínio |
| Onda 3 | Application | TASK-07..TASK-14 | Application.Tests ≥85%; PBT-02, PBT-03 verdes; handlers integram domain sem dependência de infra real |
| Onda 4 | Infrastructure | TASK-15..TASK-19 | Infrastructure.Tests ≥70%; RLS testada e gate CI verde (zero falhas de isolamento); Outbox/Inbox funcionais |
| Onda 5 | API + Contracts | TASK-20..TASK-24 | Api.Tests ≥80%; todos os endpoints do design §8 com RBAC; testes de contrato de eventos verdes |
| Onda 6 | Hardening | TASK-25..TASK-28 | PBT-01, PBT-05 verdes; logs sem PII; métricas ativas; README sincronizado; DoD §19 do design completo |

**Risco principal por onda:**

- Onda 2: `TerminalStagesPolicy` e `StagePositionPolicy` podem ter edge cases complexos — cobrir com geradores exaustivos.
- Onda 3: `LastTenantAdminPolicy` com lock transacional requer teste de corrida (concorrência) — usar testes de integração em Onda 4.
- Onda 4: RLS com `SET app.current_tenant` pode ter comportamento diferente por pool de conexões — validar com Testcontainers.
- Onda 5: endpoint de aceite de convite é o único sem JWT — risco de regressão na configuração de autenticação.
- Onda 6: PBT-01 (isolamento multi-tenant) é gate obrigatório de CI — falha bloqueia merge.

---

## 4. Tarefas

### TASK-01 — Criar solution e projetos Clean Architecture

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 1 — Bootstrap |
| **Branch** | `feat/organization/01-bootstrap-solution` |
| **Worktree** | `git worktree add ../worktrees/organization/01-bootstrap-solution -b feat/organization/01-bootstrap-solution` |
| **Status** | [ ] |
| **Depende de** | Não aplicável |
| **Entregável** | Solution `Organization.sln` com 10 projetos compilando; referências entre camadas corretas |
| **Mapeia** | design §3 (estrutura da solução), ADR-0001 |
| **Camada principal** | DevOps |

#### Objetivo

Criar a structure física da solução conforme design §3: cinco projetos de produção (`Domain`, `Application`, `Infrastructure`, `Api`, `Contracts`) e cinco de teste (`Domain.Tests`, `Application.Tests`, `Infrastructure.Tests`, `Api.Tests`, `Architecture.Tests`). Configurar referências entre projetos respeitando a direção de dependência da Clean Architecture.

#### Subtasks

- [ ] **ST-01 — Red:** escrever teste de compilação/build que falha por ausência dos projetos
- [ ] **ST-02 — Green:** criar `Organization.sln`; criar os 10 projetos `.csproj`; adicionar referências conforme design §3; instalar dependências base (xUnit, FluentAssertions, MediatR, FluentValidation, EF Core, Npgsql, StackExchange.Redis, Serilog)
- [ ] **ST-03 — Refactor:** revisar `.csproj` para remover referências desnecessárias; garantir `<Nullable>enable</Nullable>` e `<ImplicitUsings>enable</ImplicitUsings>` em todos
- [ ] **ST-04 — Encerramento:** `dotnet build Organization.sln` verde; commit `feat(organization): bootstrap solution with 10 projects`; push

#### Critérios de Aceite

- [ ] `dotnet build` retorna 0 warnings e 0 erros
- [ ] Dez projetos presentes e referenciados conforme design §3
- [ ] `Domain` não referencia EF Core, GCP SDK, web framework ou mensageria

---

### TASK-02 — Testes de arquitetura e CI mínimo

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 1 — Bootstrap |
| **Branch** | `test/organization/02-architecture-tests` |
| **Worktree** | `git worktree add ../worktrees/organization/02-architecture-tests -b test/organization/02-architecture-tests` |
| **Status** | [ ] |
| **Depende de** | TASK-01 |
| **Entregável** | `Architecture.Tests` com regras de dependência entre camadas validadas; pipeline CI executando |
| **Mapeia** | design §2 (princípios), §3 (regras de dependência) |
| **Camada principal** | Tests |

#### Objetivo

Implementar testes de arquitetura usando NetArchTest (ou equivalente do TRD) que valide as regras de dependência entre camadas. Configurar pipeline CI mínimo (build + test).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de arquitetura falhando (ex.: `Domain` referencia `Infrastructure` — deve falhar)
- [ ] **ST-02 — Green:** implementar testes cobrindo todas as regras do diagrama do design §3 (`Api → Application`, `Api → Infrastructure`, `Api → Contracts`, `Infrastructure → Application`, `Infrastructure → Domain`, `Application → Domain`, `Application → Contracts`, `Domain → nada`)
- [ ] **ST-03 — Refactor:** agrupar testes por direção proibida e por direção permitida; adicionar mensagem descritiva em cada falha
- [ ] **ST-04 — CI:** criar arquivo de pipeline (GitHub Actions ou equivalente) executando `dotnet build` + `dotnet test Architecture.Tests`
- [ ] **ST-05 — Encerramento:** todos os testes de arquitetura verdes; CI executa sem falha; commit `test(organization): add architecture dependency rules and CI pipeline`; push

#### Critérios de Aceite

- [ ] Toda regra proibida do design §3 tem teste correspondente
- [ ] Teste de arquitetura falha ao introduzir referência proibida (validado manualmente)
- [ ] CI verde no push da branch

---

### TASK-03 — Objetos de valor do domínio

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `test/organization/03-domain-value-objects` |
| **Worktree** | `git worktree add ../worktrees/organization/03-domain-value-objects -b test/organization/03-domain-value-objects` |
| **Status** | [ ] |
| **Depende de** | TASK-01 |
| **Entregável** | Cinco objetos de valor imutáveis com testes unitários completos em `Domain.Tests` |
| **Mapeia** | design §4.3; Req 1.1, 1.3, 5.3, 9.1, 9.7; PBT-07 (unicidade de name) |
| **Camada principal** | Domain |

#### Objetivo

Implementar `BusinessUnitName`, `Role`, `StageCategory`, `Probability` e `InvitationToken` como objetos de valor imutáveis com validação na construção (fail-fast). Cada um deve ter igualdade por valor e rejeitar estado inválido.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes para `BusinessUnitName` (vazio rejeitado, trim aplicado, max 120 chars, igualdade insensível a caixa por valor normalizado)
- [ ] **ST-02 — Green:** implementar `BusinessUnitName`
- [ ] **ST-03 — Red:** testes para `Role` (apenas `TAdmin`, `GestorBU`, `Vendedor`, `Viewer`; `PlatOp` rejeitado)
- [ ] **ST-04 — Green:** implementar `Role`
- [ ] **ST-05 — Red:** testes para `StageCategory` (`open`, `won`, `lost`; qualquer outro rejeitado)
- [ ] **ST-06 — Green:** implementar `StageCategory`
- [ ] **ST-07 — Red:** testes para `Probability` (0..100 válido; -1 e 101 rejeitados; igualdade por valor)
- [ ] **ST-08 — Green:** implementar `Probability`
- [ ] **ST-09 — Red:** testes para `InvitationToken` (valor em claro nunca persistido; hash gerado via `ITokenHasher`; construção sem claro aceita só hash)
- [ ] **ST-10 — Green:** implementar `InvitationToken`
- [ ] **ST-11 — Refactor:** extrair base `ValueObject<T>` se houver repetição; garantir `record` ou `sealed class` com `==` por valor
- [ ] **ST-12 — Encerramento:** Domain.Tests verdes; coverage ≥95% nos objetos de valor; commit `test(organization): domain value objects with validation`; push

#### Critérios de Aceite

- [ ] Nenhum objeto de valor aceita estado inválido na construção
- [ ] Igualdade por valor funciona corretamente para todos
- [ ] `InvitationToken` não expõe valor em claro após construção
- [ ] Coverage ≥95% na camada Domain (acumulado com TASKs anteriores)

---

### TASK-04 — Agregado BusinessUnit + pipeline entities + policies + seeds (PBT-06, PBT-07)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `test/organization/04-aggregate-business-unit` |
| **Worktree** | `git worktree add ../worktrees/organization/04-aggregate-business-unit -b test/organization/04-aggregate-business-unit` |
| **Status** | [ ] |
| **Depende de** | TASK-03 |
| **Entregável** | Agregado `BusinessUnit` com entidades de pipeline, policies de domínio, seed factories e PBT-06 + PBT-07 verdes |
| **Mapeia** | Req 1, 2, 9, 10, 11; design §4.1, §4.2, §4.6; DD-002, DD-006; PBT-06, PBT-07 |
| **Camada principal** | Domain |

#### Objetivo

Implementar o agregado `BusinessUnit` com suas entidades internas (`Stage`, `OriginChannel`, `LossReason`), as policies `TerminalStagesPolicy`, `StagePositionPolicy`, `BusinessUnitEnablementSpec`, `BusinessUnitNameUniquenessSpec`, os factories de seed (`StageSeedFactory`, `OriginChannelSeedFactory`) e os domain events `BusinessUnitCreated`, `BusinessUnitDeactivated`, `StageConfigured`.

#### Subtasks

- [ ] **ST-01 — Red:** testes de `BusinessUnit.Create(name, tenantId)` — BU nasce `active=true`; evento `BusinessUnitCreated` enfileirado
- [ ] **ST-02 — Green:** implementar `BusinessUnit` root, `BusinessUnitId`, `Rename`, `Deactivate`
- [ ] **ST-03 — Red:** testes de `Stage` e `TerminalStagesPolicy` — exatamente um `won`, um `lost`, ≥1 `open`; remoção do último terminal rejeitada
- [ ] **ST-04 — Green:** implementar `Stage`, `TerminalStagesPolicy`, `AddStage`, `RemoveStage` no agregado
- [ ] **ST-05 — Red:** testes de `StagePositionPolicy` — `position` distintas; reordenação mantém ordem total
- [ ] **ST-06 — Green:** implementar `StagePositionPolicy`, `ReorderStages`
- [ ] **ST-07 — Red:** testes de `BusinessUnitEnablementSpec` — BU sem motivo de perda ativo não habilitada
- [ ] **ST-08 — Green:** implementar `LossReason`, `OriginChannel`, `BusinessUnitEnablementSpec`
- [ ] **ST-09 — Red:** testes de `StageSeedFactory` — seed contém exatamente um `won`, um `lost`, ≥1 `open`, 8 estágios conforme DD-002
- [ ] **ST-10 — Green:** implementar `StageSeedFactory` e `OriginChannelSeedFactory`
- [ ] **ST-11 — PBT-06:** implementar PBT usando FsCheck — gerar sequências de add/remove/configure stage; afirmar exatamente um `won`, um `lost`, ≥1 `open` ao final; operações violadoras rejeitadas
- [ ] **ST-12 — PBT-07:** gerar conjuntos de stages; afirmar unicidade de (`tenant_id`,`bu_id`,`name`) e `position` sem empates
- [ ] **ST-13 — Refactor:** extrair invariantes em métodos privados do agregado; garantir que nenhum setter público existe
- [ ] **ST-14 — Encerramento:** Domain.Tests verdes; PBT-06 e PBT-07 verdes; coverage Domain ≥95%; commit `test(organization): BusinessUnit aggregate with pipeline entities and PBT-06 PBT-07`; push

#### Critérios de Aceite

- [ ] Nenhuma sequência de operações viola `TerminalStagesPolicy` sem rejeição
- [ ] PBT-06 e PBT-07 executados com ≥100 exemplos cada; zero falhas
- [ ] `StageSeedFactory` produz seed válido conforme DD-002
- [ ] Agregado não expõe setters; apenas métodos de comportamento

---

### TASK-05 — Agregado User + UserMembership + domain events

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `test/organization/05-aggregate-user` |
| **Worktree** | `git worktree add ../worktrees/organization/05-aggregate-user -b test/organization/05-aggregate-user` |
| **Status** | [ ] |
| **Depende de** | TASK-03 |
| **Entregável** | Agregado `User` com `UserMembership`, `MembershipUniquenessSpec`, soft-delete e domain events |
| **Mapeia** | Req 5, 7; design §4.1, §4.2; PBT-05 |
| **Camada principal** | Domain |

#### Objetivo

Implementar o agregado `User` com `UserMembership` (vínculo usuário-BU-papel), a especificação `MembershipUniquenessSpec` (no máximo um membership por BU), soft-delete (`active = false`, `deactivatedAt`), e os domain events `UserActivated`, `UserDeactivated`, `MembershipRoleChanged`.

#### Subtasks

- [ ] **ST-01 — Red:** testes de `User.Activate(identityUid, memberships)` — usuário nasce `active=true`; evento `UserActivated` com memberships; sem duplicatas de membership por BU
- [ ] **ST-02 — Green:** implementar `User`, `UserId`, `Activate`, `UserMembership`, `UserMembershipId`
- [ ] **ST-03 — Red:** testes de `MembershipUniquenessSpec` — segundo membership para mesma BU rejeitado; papéis distintos em BUs distintas aceitos
- [ ] **ST-04 — Green:** implementar `AssignMembership`, `ChangeMembershipRole`, `RemoveMembership`
- [ ] **ST-05 — Red:** testes de `User.Deactivate()` — `active=false`; `deactivatedAt` preenchido; evento `UserDeactivated`; histórico intacto (memberships preservados)
- [ ] **ST-06 — Green:** implementar `Deactivate`
- [ ] **ST-07 — Refactor:** garantir que `User` não chama repositório ou serviço externo; invariantes puras no domínio
- [ ] **ST-08 — Encerramento:** Domain.Tests verdes; coverage Domain ≥95%; commit `test(organization): User aggregate with memberships and soft-delete`; push

#### Critérios de Aceite

- [ ] `MembershipUniquenessSpec` rejeita segundo membership para mesma BU
- [ ] Desativação preserva todos os memberships (sem deleção física)
- [ ] Domain events corretos emitidos em cada operação
- [ ] Coverage Domain ≥95%

---

### TASK-06 — Agregado UserInvitation + state machine (PBT-04)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `test/organization/06-aggregate-invitation` |
| **Worktree** | `git worktree add ../worktrees/organization/06-aggregate-invitation -b test/organization/06-aggregate-invitation` |
| **Status** | [ ] |
| **Depende de** | TASK-03 |
| **Entregável** | Agregado `UserInvitation` com state machine completa e PBT-04 verde |
| **Mapeia** | Req 3, 4; design §4.5; PBT-04 |
| **Camada principal** | Domain |

#### Objetivo

Implementar o agregado `UserInvitation` com a máquina de estados (`pending → accepted | revoked | expired`) e os eventos `UserInvited`. O aceite valida `tokenHash` e expiração via `IClock`. Toda transição a partir de estado terminal é rejeitada.

#### Subtasks

- [ ] **ST-01 — Red:** testes das transições válidas — `pending → accepted`, `pending → revoked`, `pending → expired`
- [ ] **ST-02 — Green:** implementar `UserInvitation`, `InvitationState`, `Accept(tokenHash, now)`, `Revoke()`, `Expire()`
- [ ] **ST-03 — Red:** testes das transições inválidas — qualquer transição a partir de `accepted`, `revoked`, `expired` lança exceção de domínio
- [ ] **ST-04 — Green:** implementar guard em cada método; usar resultado expressivo ou exceção de domínio
- [ ] **ST-05 — Red:** testes de expiração — convite com `expiresAt < now` no aceite é rejeitado com erro correto
- [ ] **ST-06 — Green:** implementar verificação de expiração no `Accept`
- [ ] **ST-07 — PBT-04:** gerar sequências aleatórias de transições; afirmar que apenas transições válidas a partir de `pending` são aceitas; estados terminais rejeitam toda transição
- [ ] **ST-08 — Refactor:** `InvitationState` como `enum` ou discriminated union; extrair erros de domínio explícitos
- [ ] **ST-09 — Encerramento:** Domain.Tests verdes; PBT-04 verde com ≥100 exemplos; coverage Domain ≥95%; commit `test(organization): UserInvitation aggregate with state machine and PBT-04`; push

#### Critérios de Aceite

- [ ] PBT-04 verde: nenhuma transição ilegal aceita
- [ ] Aceite com token expirado rejeitado com código de erro correto
- [ ] Domain events `UserInvited` emitido na criação do convite
- [ ] Coverage Domain ≥95%

---

### TASK-07 — Pipeline behaviors (TenantContext, RBAC, Validation, Transaction, Logging)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/organization/07-pipeline-behaviors` |
| **Worktree** | `git worktree add ../worktrees/organization/07-pipeline-behaviors -b feat/organization/07-pipeline-behaviors` |
| **Status** | [ ] |
| **Depende de** | TASK-03 |
| **Entregável** | Seis pipeline behaviors implementados e testados com mocks em `Application.Tests` |
| **Mapeia** | design §5.4; RNF 1, 2, 4, 6; Req 4.4, 12.3 |
| **Camada principal** | Application |

#### Objetivo

Implementar os behaviors do pipeline MediatR: `TenantContextBehavior`, `RbacAuthorizationBehavior` (deny-by-default), `ValidationBehavior` (FluentValidation), `TransactionBehavior` (transação + Outbox), `IdempotencyBehavior` (Inbox/chave natural) e `LoggingBehavior` (sem PII).

#### Subtasks

- [ ] **ST-01 — Red:** testes de `TenantContextBehavior` — command sem `tenant_id` rejeitado; `SET app.current_tenant` chamado com valor correto
- [ ] **ST-02 — Green:** implementar `TenantContextBehavior`; definir interface `ITenantContext`
- [ ] **ST-03 — Red:** testes de `RbacAuthorizationBehavior` — command com papel errado retorna `Forbidden`; deny-by-default
- [ ] **ST-04 — Green:** implementar `RbacAuthorizationBehavior` e atributo `[RequiresRole(...)]`
- [ ] **ST-05 — Red:** testes de `ValidationBehavior` — command inválido retorna erros sem executar handler
- [ ] **ST-06 — Green:** implementar `ValidationBehavior`
- [ ] **ST-07 — Red:** testes de `TransactionBehavior` — falha no handler faz rollback; Outbox gravado na mesma transação
- [ ] **ST-08 — Green:** implementar `TransactionBehavior`
- [ ] **ST-09 — Red:** testes de `IdempotencyBehavior` — segundo processamento com mesma chave retorna sem executar handler
- [ ] **ST-10 — Green:** implementar `IdempotencyBehavior`; definir port `IInboxStore`
- [ ] **ST-11 — Green:** implementar `LoggingBehavior` com mascaramento de e-mail/`display_name`
- [ ] **ST-12 — Encerramento:** Application.Tests verdes; commit `feat(organization): pipeline behaviors (tenant, rbac, validation, transaction, idempotency, logging)`; push

#### Critérios de Aceite

- [ ] `RbacAuthorizationBehavior` nega toda ação sem papel explicitamente concedido
- [ ] `TransactionBehavior` garante rollback em falha
- [ ] `LoggingBehavior` não loga e-mail ou `display_name` em texto claro
- [ ] Coverage Application ≥85%

---

### TASK-08 — Handlers de BusinessUnit (Create, Rename, Deactivate)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/organization/08-handlers-business-unit` |
| **Worktree** | `git worktree add ../worktrees/organization/08-handlers-business-unit -b feat/organization/08-handlers-business-unit` |
| **Status** | [ ] |
| **Depende de** | TASK-04, TASK-07 |
| **Entregável** | Handlers de BU com validadores; testes em `Application.Tests` |
| **Mapeia** | Req 1, 2; design §5.1; MSG-015, MSG-016; ORG-ERR-001, ORG-ERR-002 |
| **Camada principal** | Application |

#### Objetivo

Implementar `CreateBusinessUnitCommandHandler` (BU com seeds, unicidade via `IBusinessUnitRepository`), `RenameBusinessUnitCommandHandler` (unicidade + RBAC) e `DeactivateBusinessUnitCommandHandler` (bloqueia se `IOpportunityCounter > 0`).

#### Subtasks

- [ ] **ST-01 — Red:** testes de `CreateBusinessUnitCommandHandler` — BU criada com seeds; nome duplicado retorna `ORG-ERR-001`; apenas `TAdmin` autorizado
- [ ] **ST-02 — Green:** implementar handler; definir `IBusinessUnitRepository`, `CreateBusinessUnitCommand`
- [ ] **ST-03 — Red:** testes de `RenameBusinessUnitCommandHandler` — unicidade; `GestorBU` autorizado só na própria BU
- [ ] **ST-04 — Green:** implementar handler; definir `RenameBusinessUnitCommand`
- [ ] **ST-05 — Red:** testes de `DeactivateBusinessUnitCommandHandler` — bloqueia com oportunidades ativas (`ORG-ERR-002`)
- [ ] **ST-06 — Green:** implementar handler; definir `IOpportunityCounter`, `DeactivateBusinessUnitCommand`
- [ ] **ST-07 — Refactor:** extrair `ActiveOpportunitiesSpec` como specification de aplicação
- [ ] **ST-08 — Encerramento:** Application.Tests verdes; coverage Application ≥85%; commit `feat(organization): business unit command handlers`; push

#### Critérios de Aceite

- [ ] Criação de BU aplica seeds automaticamente
- [ ] Inativação bloqueada com contagem de oportunidades ativas
- [ ] RBAC verificado em todos os handlers
- [ ] Coverage Application ≥85%

---

### TASK-09 — Handlers de convite (Invite, Revoke, Accept) + PBT-03 parcial

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/organization/09-handlers-invitation` |
| **Worktree** | `git worktree add ../worktrees/organization/09-handlers-invitation -b feat/organization/09-handlers-invitation` |
| **Status** | [ ] |
| **Depende de** | TASK-06, TASK-07 |
| **Entregável** | Handlers de convite com idempotência de aceite; PBT-03 para `AcceptInvitation` verde |
| **Mapeia** | Req 3, 4; design §5.1, §6.5; DD-004; PBT-03; ORG-ERR-003..006 |
| **Camada principal** | Application |

#### Objetivo

Implementar `InviteUserCommandHandler` (convite atômico via Outbox, token hash, bloqueia e-mail ativo), `RevokeInvitationCommandHandler` e `AcceptInvitationCommandHandler` (valida token/estado, delega `IIdentityProvisioner`, cria `User`, idempotência por `tokenHash`).

#### Subtasks

- [ ] **ST-01 — Red:** testes de `InviteUserCommandHandler` — convite com token hash; Outbox enfileirado na mesma transação; e-mail ativo bloqueia (`ORG-ERR-003`)
- [ ] **ST-02 — Green:** implementar handler; definir `IUserInvitationRepository`, `IEventOutbox`, `ITokenHasher`
- [ ] **ST-03 — Red:** testes de `RevokeInvitationCommandHandler` — convite `pending` revogado; não-`pending` retorna `ORG-ERR-006`
- [ ] **ST-04 — Green:** implementar handler
- [ ] **ST-05 — Red:** testes de `AcceptInvitationCommandHandler` — token válido aceito; expirado/revogado rejeitado; idempotente no segundo aceite
- [ ] **ST-06 — Green:** implementar handler; definir `IIdentityProvisioner`; criar `User` com memberships
- [ ] **ST-07 — PBT-03:** reprocessar mesmo `(token, evento)` N vezes; afirmar estado final idêntico sem duplicatas
- [ ] **ST-08 — Refactor:** garantir que falha de `IIdentityProvisioner` faz rollback (convite permanece `pending`)
- [ ] **ST-09 — Encerramento:** Application.Tests verdes; PBT-03 verde ≥100 exemplos; commit `feat(organization): invitation handlers with idempotency and PBT-03`; push

#### Critérios de Aceite

- [ ] Falha no envio de e-mail não consolida convite
- [ ] Aceite idempotente: segundo aceite com mesmo token não cria duplicatas
- [ ] PBT-03 verde para aceite de convite
- [ ] Coverage Application ≥85%

---

### TASK-10 — Handlers de membership + LastTenantAdminPolicy (PBT-02)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/organization/10-handlers-membership` |
| **Worktree** | `git worktree add ../worktrees/organization/10-handlers-membership -b feat/organization/10-handlers-membership` |
| **Status** | [ ] |
| **Depende de** | TASK-05, TASK-07 |
| **Entregável** | Handlers de membership com `LastTenantAdminPolicy` e PBT-02 verde |
| **Mapeia** | Req 5, 8; design §4.6, §5.1; DD-003; PBT-02; ORG-ERR-009, ORG-ERR-012 |
| **Camada principal** | Application |

#### Objetivo

Implementar `AssignMembershipCommandHandler`, `ChangeMembershipRoleCommandHandler` e `RemoveMembershipCommandHandler`, todos protegidos por `LastTenantAdminPolicy` com `ITenantAdminCounter` e lock transacional.

#### Subtasks

- [ ] **ST-01 — Red:** testes de `AssignMembershipCommandHandler` — membership criado; duplicata retorna `ORG-ERR-012`; papel fora da lista retorna `ORG-ERR-007`
- [ ] **ST-02 — Green:** implementar handler; definir `IUserRepository`, `ITenantAdminCounter`, `AssignMembershipCommand`
- [ ] **ST-03 — Red:** testes de `ChangeMembershipRoleCommandHandler` — papel alterado; rebaixar último `TAdmin` retorna `ORG-ERR-009`
- [ ] **ST-04 — Green:** implementar handler com `LastTenantAdminPolicy`
- [ ] **ST-05 — Red:** testes de `RemoveMembershipCommandHandler` — membership removido; remover último `TAdmin` retorna `ORG-ERR-009`; cache invalidado
- [ ] **ST-06 — Green:** implementar handler; port `IMembershipCache`
- [ ] **ST-07 — PBT-02:** gerar sequências de assign/change/remove; afirmar `count(TAdmin ativo) ≥ 1`; violações rejeitadas sem alterar estado
- [ ] **ST-08 — Refactor:** consolidar `LastTenantAdminPolicy` em método reutilizável
- [ ] **ST-09 — Encerramento:** Application.Tests verdes; PBT-02 verde ≥200 exemplos; commit `feat(organization): membership handlers with LastTenantAdminPolicy and PBT-02`; push

#### Critérios de Aceite

- [ ] PBT-02 verde: nenhuma sequência deixa o tenant sem TAdmin ativo
- [ ] Cache invalidado em toda alteração de membership
- [ ] Operação rejeitada não altera nenhum estado
- [ ] Coverage Application ≥85%

---

### TASK-11 — Handlers de configuração de pipeline (Stage, OriginChannel, LossReason)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/organization/11-handlers-pipeline-config` |
| **Worktree** | `git worktree add ../worktrees/organization/11-handlers-pipeline-config -b feat/organization/11-handlers-pipeline-config` |
| **Status** | [ ] |
| **Depende de** | TASK-04, TASK-07 |
| **Entregável** | Handlers de configuração de pipeline com RBAC TAdmin/GestorBU e testes completos |
| **Mapeia** | Req 9, 10, 11; design §5.1; ORG-ERR-013..017 |
| **Camada principal** | Application |

#### Objetivo

Implementar `ConfigureStageCommandHandler` (add/rename/reorder/probability), `ConfigureOriginChannelCommandHandler` e `ConfigureLossReasonCommandHandler` (mantém ≥1 ativo). Invariantes delegadas ao agregado.

#### Subtasks

- [ ] **ST-01 — Red:** testes de `ConfigureStageCommandHandler` — estágio adicionado; nome duplicado retorna `ORG-ERR-013`; posição duplicada retorna `ORG-ERR-014`; remoção do último terminal retorna `ORG-ERR-015`
- [ ] **ST-02 — Green:** implementar handler
- [ ] **ST-03 — Red:** testes de `ConfigureOriginChannelCommandHandler` — canal adicionado/renomeado/desativado; RBAC TAdmin
- [ ] **ST-04 — Green:** implementar handler
- [ ] **ST-05 — Red:** testes de `ConfigureLossReasonCommandHandler` — desativar último motivo retorna `ORG-ERR-017`
- [ ] **ST-06 — Green:** implementar handler
- [ ] **ST-07 — Encerramento:** Application.Tests verdes; coverage Application ≥85%; commit `feat(organization): pipeline config handlers`; push

#### Critérios de Aceite

- [ ] `TerminalStagesPolicy` respeitada em todos os caminhos
- [ ] `BusinessUnitEnablementSpec` bloqueia desativação do último motivo de perda
- [ ] GestorBU autorizado apenas na sua BU
- [ ] Coverage Application ≥85%

---

### TASK-12 — DeactivateUserCommand + FutureActivitiesSpec

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/organization/12-handler-deactivate-user` |
| **Worktree** | `git worktree add ../worktrees/organization/12-handler-deactivate-user -b feat/organization/12-handler-deactivate-user` |
| **Status** | [ ] |
| **Depende de** | TASK-05, TASK-10 |
| **Entregável** | `DeactivateUserCommandHandler` com guards de TAdmin e atividades futuras; cache invalidado |
| **Mapeia** | Req 7, 8; design §4.6, §5.1; PBT-05; MSG-017, MSG-018; ORG-ERR-009, ORG-ERR-011 |
| **Camada principal** | Application |

#### Objetivo

Implementar `DeactivateUserCommandHandler`: (1) `LastTenantAdminPolicy` antes de qualquer efeito; (2) `FutureActivitiesSpec` via `IActivityCounter`; (3) soft-delete; (4) invalida cache; (5) emite `UserDeactivated` via Outbox.

#### Subtasks

- [ ] **ST-01 — Red:** testes — usuário desativado; último TAdmin retorna `ORG-ERR-009`; atividades futuras retorna `ORG-ERR-011`
- [ ] **ST-02 — Green:** implementar handler; `DeactivateUserCommand`, `IActivityCounter`
- [ ] **ST-03 — Red:** testes de atomicidade — falha após soft-delete não deixa estado parcial
- [ ] **ST-04 — Green:** garantir cache invalidado dentro da transação ou com compensação
- [ ] **ST-05 — Refactor:** extrair `FutureActivitiesSpec` como specification de aplicação
- [ ] **ST-06 — Encerramento:** Application.Tests verdes; coverage Application ≥85%; commit `feat(organization): deactivate user handler with guards`; push

#### Critérios de Aceite

- [ ] Desativação do último TAdmin rejeitada sem alterar estado
- [ ] Cache invalidado após soft-delete bem-sucedido
- [ ] `UserDeactivated` emitido via Outbox
- [ ] Coverage Application ≥85%

---

### TASK-13 — ProvisionInitialOrganizationCommand + IdempotencyBehavior (PBT-03)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/organization/13-handler-provision-initial` |
| **Worktree** | `git worktree add ../worktrees/organization/13-handler-provision-initial -b feat/organization/13-handler-provision-initial` |
| **Status** | [ ] |
| **Depende de** | TASK-08, TASK-09, TASK-10 |
| **Entregável** | Consumer idempotente de `TenantProvisioned` com PBT-03 para provisionamento verde |
| **Mapeia** | Req 12; design §5.1, §6.5, §6.6; PBT-03 |
| **Camada principal** | Application |

#### Objetivo

Implementar `ProvisionInitialOrganizationCommandHandler` que cria BU inicial com seeds e TAdmin; usa `IInboxStore` para deduplicação por `message_id`/`tenant_id`; reprocessamento não duplica entidades.

#### Subtasks

- [ ] **ST-01 — Red:** testes de provisionamento — BU inicial com seeds; TAdmin criado e ativado; membership TAdmin criado; garantia Req 8 satisfeita
- [ ] **ST-02 — Green:** implementar handler coordenando criação de BU + User + membership
- [ ] **ST-03 — Red:** testes de idempotência — segundo `TenantProvisioned` do mesmo `tenant_id` não duplica entidades
- [ ] **ST-04 — Green:** verificação via `IInboxStore` + chaves naturais
- [ ] **ST-05 — PBT-03:** gerar N reprocessamentos do mesmo evento; afirmar estado final idêntico
- [ ] **ST-06 — Encerramento:** PBT-03 verde ≥100 exemplos; commit `feat(organization): provision initial organization with idempotency`; push

#### Critérios de Aceite

- [ ] Reprocessamento não duplica nenhuma entidade
- [ ] PBT-03 verde para provisionamento
- [ ] ≥1 TAdmin ativo garantido após provisionamento
- [ ] Coverage Application ≥85%

---

### TASK-14 — Queries + MembershipCacheProjector + GetRbacContextQuery

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/organization/14-queries-rbac-cache` |
| **Worktree** | `git worktree add ../worktrees/organization/14-queries-rbac-cache -b feat/organization/14-queries-rbac-cache` |
| **Status** | [ ] |
| **Depende de** | TASK-07, TASK-10 |
| **Entregável** | Sete queries implementadas; `MembershipCacheProjector` com cache-aside, invalidação e degradação segura |
| **Mapeia** | Req 6.1, 13; design §5.2, §6.2; RNF 3.3, 5 |
| **Camada principal** | Application |

#### Objetivo

Implementar as queries de leitura e o `MembershipCacheProjector` com lógica cache-aside: hit retorna do Redis; miss hidrata do banco e escreve no cache; Redis indisponível degrada para banco com deny-by-default.

#### Subtasks

- [ ] **ST-01 — Red:** testes de `GetRbacContextQuery` — cache hit; cache miss (degrada para banco); Redis indisponível não concede acesso indevido; resposta sem e-mail/`display_name`
- [ ] **ST-02 — Green:** implementar handler e `MembershipCacheProjector`
- [ ] **ST-03 — Red:** testes das demais queries — `ListStagesQuery` ordenada por `position`; `ListUsersQuery` restringe PII para TAdmin; BUs inativas ausentes
- [ ] **ST-04 — Green:** implementar os seis handlers de query restantes
- [ ] **ST-05 — Refactor:** garantir que nenhuma query retorna PII em contexto não autorizado
- [ ] **ST-06 — Encerramento:** Application.Tests verdes; coverage Application ≥85%; commit `feat(organization): queries and membership cache projector`; push

#### Critérios de Aceite

- [ ] Cache miss degrada para banco sem conceder acesso indevido
- [ ] `GetRbacContextQuery` não inclui PII na resposta
- [ ] `ListStagesQuery` sempre ordenada por `position`
- [ ] Coverage Application ≥85%

---

### TASK-15 — EF Core mappings + migration inicial + RLS global filter

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/organization/15-ef-core-schema-rls` |
| **Worktree** | `git worktree add ../worktrees/organization/15-ef-core-schema-rls -b feat/organization/15-ef-core-schema-rls` |
| **Status** | [ ] |
| **Depende de** | TASK-04, TASK-05, TASK-06 |
| **Entregável** | `OrganizationDbContext` com mappings, migration aplicada, RLS habilitada e testada via Testcontainers |
| **Mapeia** | design §6.1, §7, §14; RNF 1; DD-001; ADR-0001 |
| **Camada principal** | Infrastructure |

#### Objetivo

Criar `OrganizationDbContext` com `IEntityTypeConfiguration<T>` para todas as entidades (nomes físicos `snake_case`), global query filter por `tenant_id`, migration inicial com todas as tabelas/índices/constraints do design §7, RLS habilitada com policy `USING (tenant_id = current_setting('app.current_tenant')::uuid)`.

#### Subtasks

- [ ] **ST-01 — Red:** testes via Testcontainers Postgres — entidades persistidas e recuperadas; `tenant_id` presente em todas as tabelas
- [ ] **ST-02 — Green:** implementar `OrganizationDbContext`; mapear todas as entidades; criar migration inicial
- [ ] **ST-03 — Red:** testes de global filter — query sem contexto de tenant retorna zero registros de outro tenant
- [ ] **ST-04 — Green:** implementar global query filter; integrar `TenantContextBehavior` com `SET app.current_tenant`
- [ ] **ST-05 — Red:** testes de RLS — acesso SQL direto sem `SET app.current_tenant` retorna zero registros
- [ ] **ST-06 — Green:** adicionar scripts RLS na migration; `FORCE ROW LEVEL SECURITY` para owner
- [ ] **ST-07 — Green:** migration para `outbox_events` e `inbox_messages`
- [ ] **ST-08 — Refactor:** validar nomes físicos `snake_case`; índices conforme design §7
- [ ] **ST-09 — Encerramento:** Infrastructure.Tests verdes; migration aplica em banco limpo; commit `feat(organization): EF Core schema with RLS and global filter`; push

#### Critérios de Aceite

- [ ] Migration aplica sem erro em banco limpo
- [ ] Global filter bloqueia leitura cross-tenant em teste de integração
- [ ] RLS bloqueia acesso SQL direto sem contexto de tenant
- [ ] Coverage Infrastructure ≥70%

---

### TASK-16 — Repositórios de domínio (BU, User, Invitation)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/organization/16-repositories` |
| **Worktree** | `git worktree add ../worktrees/organization/16-repositories -b feat/organization/16-repositories` |
| **Status** | [ ] |
| **Depende de** | TASK-15 |
| **Entregável** | Três repositórios concretos com testes de integração via Testcontainers |
| **Mapeia** | design §6.1; Req 1, 3, 5, 8 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar `BusinessUnitRepository`, `UserRepository` (incluindo `GetActiveTenantAdmins`) e `UserInvitationRepository` (incluindo `GetByTokenHash`). Testes de integração com Testcontainers Postgres.

#### Subtasks

- [ ] **ST-01 — Red:** testes de `BusinessUnitRepository` — `GetByName` respeita `tenant_id`; `Save` persiste BU com entidades de pipeline
- [ ] **ST-02 — Green:** implementar `BusinessUnitRepository`
- [ ] **ST-03 — Red:** testes de `UserRepository` — `GetByEmail` por tenant; `GetActiveTenantAdmins` retorna apenas ativos com TAdmin
- [ ] **ST-04 — Green:** implementar `UserRepository` e `TenantAdminCounter`
- [ ] **ST-05 — Red:** testes de `UserInvitationRepository` — `GetByTokenHash` por tenant; `target_memberships` serializado em JSONB
- [ ] **ST-06 — Green:** implementar `UserInvitationRepository`
- [ ] **ST-07 — Encerramento:** Infrastructure.Tests verdes; coverage Infrastructure ≥70%; commit `feat(organization): domain repositories`; push

#### Critérios de Aceite

- [ ] Nenhum repositório retorna registros de outro tenant
- [ ] `GetByTokenHash` resolve o hash corretamente
- [ ] `GetActiveTenantAdmins` conta apenas `active=true` e papel `TAdmin`
- [ ] Coverage Infrastructure ≥70%

---

### TASK-17 — Redis adapter IMembershipCache + Testcontainers

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/organization/17-redis-membership-cache` |
| **Worktree** | `git worktree add ../worktrees/organization/17-redis-membership-cache -b feat/organization/17-redis-membership-cache` |
| **Status** | [ ] |
| **Depende de** | TASK-15 |
| **Entregável** | `RedisMembershipCache` com TTL, invalidação e degradação segura; testado com Testcontainers Redis |
| **Mapeia** | Req 13; design §6.2; RNF 5 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar `RedisMembershipCache`: chave `org:rbac:{tenant_id}:{user_id}`, serialização sem PII, TTL configurável (~5 min), `DEL` na invalidação. Degradação segura: `ConnectionException` retorna `null`; handler recompõe do banco.

#### Subtasks

- [ ] **ST-01 — Red:** testes com Testcontainers Redis — `Set` persiste; `Get` retorna; TTL expira; `Invalidate` remove chave
- [ ] **ST-02 — Green:** implementar `RedisMembershipCache`; serialização JSON sem PII
- [ ] **ST-03 — Red:** testes de degradação — Redis indisponível em `Get` retorna `null` sem lançar exceção
- [ ] **ST-04 — Green:** implementar tratamento de `RedisConnectionException`
- [ ] **ST-05 — Refactor:** garantir que chave não inclui e-mail ou `display_name`
- [ ] **ST-06 — Encerramento:** Infrastructure.Tests verdes; coverage Infrastructure ≥70%; commit `feat(organization): Redis membership cache with safe degradation`; push

#### Critérios de Aceite

- [ ] Cache não armazena PII
- [ ] TTL configurável via `appsettings`
- [ ] Indisponibilidade do Redis não lança exceção para o handler
- [ ] Coverage Infrastructure ≥70%

---

### TASK-18 — Outbox worker + Pub/Sub publisher + Inbox TenantProvisioned

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/organization/18-outbox-inbox-pubsub` |
| **Worktree** | `git worktree add ../worktrees/organization/18-outbox-inbox-pubsub -b feat/organization/18-outbox-inbox-pubsub` |
| **Status** | [ ] |
| **Depende de** | TASK-15 |
| **Entregável** | Outbox worker + consumer de `tenant.provisioned.v1` com Inbox; testes de integração |
| **Mapeia** | design §6.3, §6.6, §9; RNF 4; Req 12; DD-004, DD-005 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar `OutboxWorker` (lê `outbox_events`, publica para Pub/Sub, marca `published_at`). Consumer de `tenant.provisioned.v1` com deduplicação via `inbox_messages`. Envelope com `tenant_id`, `correlation_id`, `causation_id`.

#### Subtasks

- [ ] **ST-01 — Red:** testes de `OutboxWorker` — evento publicado; `published_at` marcado; falha não marca
- [ ] **ST-02 — Green:** implementar `OutboxWorker`; definir `IEventPublisher`
- [ ] **ST-03 — Red:** testes de consumer — `TenantProvisioned` chama `ProvisionInitialOrganizationCommand`; segundo recebimento com mesmo `message_id` ignorado
- [ ] **ST-04 — Green:** implementar consumer via `IInboxStore`
- [ ] **ST-05 — Refactor:** validar que envelope de eventos não contém PII na carga
- [ ] **ST-06 — Encerramento:** Infrastructure.Tests verdes; coverage Infrastructure ≥70%; commit `feat(organization): Outbox worker and TenantProvisioned consumer`; push

#### Critérios de Aceite

- [ ] Evento publicado exatamente uma vez por registro de Outbox
- [ ] Consumer idempotente por `message_id`
- [ ] Carga dos eventos sem PII (DD-005)
- [ ] Coverage Infrastructure ≥70%

---

### TASK-19 — Adapters externos (IdentityProvisioner, EmailSender, counters)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/organization/19-external-adapters` |
| **Worktree** | `git worktree add ../worktrees/organization/19-external-adapters -b feat/organization/19-external-adapters` |
| **Status** | [ ] |
| **Depende de** | TASK-15 |
| **Entregável** | Quatro adapters externos com resiliência; testes com mocks/stubs |
| **Mapeia** | design §6.4; Req 3.3, 4.2, 2.3, 7.4 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar `IdentityPlatformProvisioner` (GCP Identity Platform, idempotente por e-mail, Polly retry/circuit breaker), `NotificationDeliveryEmailSender` (Outbox), `OpportunityCounterAdapter` e `ActivityCounterAdapter` (stubs in-process para o MVP).

#### Subtasks

- [ ] **ST-01 — Red:** testes de `IdentityPlatformProvisioner` — cria identidade; idempotente em segundo call; retorna `identity_uid`
- [ ] **ST-02 — Green:** implementar com `HttpClient` e Polly
- [ ] **ST-03 — Red:** testes de `NotificationDeliveryEmailSender` — chamada enfileirada via Outbox; não envia síncronamente
- [ ] **ST-04 — Green:** implementar adapter
- [ ] **ST-05 — Green:** implementar stubs de `IOpportunityCounter` e `IActivityCounter` para MVP
- [ ] **ST-06 — Encerramento:** Infrastructure.Tests verdes; coverage Infrastructure ≥70%; commit `feat(organization): external adapters with resilience`; push

#### Critérios de Aceite

- [ ] `IdentityPlatformProvisioner` idempotente por e-mail
- [ ] Retry e circuit breaker configurados
- [ ] `IEmailSender` não envia síncronamente
- [ ] Coverage Infrastructure ≥70%

---

### TASK-20 — Controllers de BusinessUnit + memberships + RBAC middleware

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `feat/organization/20-api-business-units` |
| **Worktree** | `git worktree add ../worktrees/organization/20-api-business-units -b feat/organization/20-api-business-units` |
| **Status** | [ ] |
| **Depende de** | TASK-08, TASK-10, TASK-14 |
| **Entregável** | Endpoints de BU e memberships com RBAC e catálogo de erros |
| **Mapeia** | Req 1, 2, 5; design §8, §10; RNF 2; ORG-ERR-001, 002, 007..012 |
| **Camada principal** | Api |

#### Objetivo

Implementar `BusinessUnitsController` (POST/GET/PUT/DELETE) e `MembershipsController` (GET/POST/PUT/DELETE). Middleware de autorização RBAC (JWT, deny-by-default). Erros em Problem Details com `code` do catálogo.

#### Subtasks

- [ ] **ST-01 — Red:** testes de `BusinessUnitsController` — POST 201; 409 nome duplicado; 403 sem TAdmin; 409 inativação com oportunidades
- [ ] **ST-02 — Green:** implementar controller; configurar middleware RBAC; Problem Details
- [ ] **ST-03 — Red:** testes de `MembershipsController` — POST 201; 409 duplicata; 409 último TAdmin; 403 sem permissão
- [ ] **ST-04 — Green:** implementar controller
- [ ] **ST-05 — Refactor:** extrair mapeamento `DomainException → ProblemDetails`
- [ ] **ST-06 — Encerramento:** Api.Tests verdes; coverage Api ≥80%; commit `feat(organization): business units and memberships API`; push

#### Critérios de Aceite

- [ ] Todos os endpoints de BU e memberships do design §8 implementados
- [ ] Erros retornam Problem Details com `code` do catálogo
- [ ] JWT ausente retorna 401; papel errado retorna 403
- [ ] Coverage Api ≥80%

---

### TASK-21 — Controllers de usuário e convite + anti-enumeração

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `feat/organization/21-api-users-invitations` |
| **Worktree** | `git worktree add ../worktrees/organization/21-api-users-invitations -b feat/organization/21-api-users-invitations` |
| **Status** | [ ] |
| **Depende de** | TASK-09, TASK-12, TASK-20 |
| **Entregável** | Endpoints de usuário, convite e aceite com proteção anti-enumeração |
| **Mapeia** | Req 3, 4, 7; design §8, §10; RNF 3.2; ORG-ERR-003..006, 009, 011 |
| **Camada principal** | Api |

#### Objetivo

Implementar `UsersController` (GET/DELETE) e `InvitationsController` (POST /users/invite, POST /invitations/{id}/revoke, POST /invitations/accept). Endpoint de aceite sem JWT — autenticado por token de convite. Respostas não revelam existência de conta.

#### Subtasks

- [ ] **ST-01 — Red:** testes de `UsersController` — GET lista paginada; DELETE 204; 409 último TAdmin; 409 atividades futuras; 403 sem TAdmin
- [ ] **ST-02 — Green:** implementar controller
- [ ] **ST-03 — Red:** testes de `InvitationsController` — POST /invite 202; revoke 204; accept 200; token inválido retorna `ORG-ERR-004`; accept idempotente
- [ ] **ST-04 — Green:** implementar controller; rota de aceite sem `[Authorize]` JWT
- [ ] **ST-05 — Red:** testes de anti-enumeração — e-mail ativo e inexistente retornam mesma resposta
- [ ] **ST-06 — Refactor:** garantir que erros não revelam existência de conta
- [ ] **ST-07 — Encerramento:** Api.Tests verdes; coverage Api ≥80%; commit `feat(organization): users and invitations API with anti-enumeration`; push

#### Critérios de Aceite

- [ ] Endpoint de aceite não exige JWT
- [ ] Anti-enumeração: resposta não revela existência de conta
- [ ] Coverage Api ≥80%

---

### TASK-22 — Controllers de pipeline config + OpenAPI + catálogo de erros completo

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `feat/organization/22-api-pipeline-config` |
| **Worktree** | `git worktree add ../worktrees/organization/22-api-pipeline-config -b feat/organization/22-api-pipeline-config` |
| **Status** | [ ] |
| **Depende de** | TASK-11, TASK-20 |
| **Entregável** | Endpoints de stages/channels/loss-reasons; OpenAPI anotado; 17 erros do catálogo mapeados |
| **Mapeia** | Req 9, 10, 11; design §8, §12; ORG-ERR-013..017 |
| **Camada principal** | Api |

#### Objetivo

Implementar `StagesController`, `OriginChannelsController`, `LossReasonsController`. Anotar OpenAPI em todos os endpoints. Mapear os 17 erros do catálogo §12 no middleware Problem Details.

#### Subtasks

- [ ] **ST-01 — Red:** testes de `StagesController` — GET/POST/PUT/DELETE; RBAC TAdmin/GestorBU; erros de catálogo corretos
- [ ] **ST-02 — Green:** implementar controllers de stages
- [ ] **ST-03 — Red:** testes de `OriginChannelsController` e `LossReasonsController`
- [ ] **ST-04 — Green:** implementar controllers
- [ ] **ST-05 — Green:** configurar OpenAPI/Swagger com todos os 28 endpoints do design §8
- [ ] **ST-06 — Refactor:** revisar resposta consistente (camelCase, paginação, status codes)
- [ ] **ST-07 — Encerramento:** Swagger acessível; coverage Api ≥80%; commit `feat(organization): pipeline config API with OpenAPI and error catalog`; push

#### Critérios de Aceite

- [ ] Todos os 28 endpoints do design §8 implementados
- [ ] Os 17 códigos de erro do catálogo mapeados em Problem Details
- [ ] Swagger gerado sem erros de schema
- [ ] Coverage Api ≥80%

---

### TASK-23 — Api.Tests — contrato REST, RBAC papel×operação, isolamento por tenant

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `test/organization/23-api-tests-rbac` |
| **Worktree** | `git worktree add ../worktrees/organization/23-api-tests-rbac -b test/organization/23-api-tests-rbac` |
| **Status** | [ ] |
| **Depende de** | TASK-20, TASK-21, TASK-22 |
| **Entregável** | Suíte de `Api.Tests` cobrindo RBAC papel×operação, isolamento entre tenants, contratos REST |
| **Mapeia** | RNF 2; Req 6; design §13; KPI-06 |
| **Camada principal** | Tests |

#### Objetivo

Testes de integração end-to-end (WebApplicationFactory + Testcontainers): (a) toda combinação papel×operação da matriz §10.1 (permissão E negação); (b) isolamento entre tenants; (c) contratos de resposta (schema JSON, status codes, paginação).

#### Subtasks

- [ ] **ST-01 — Red:** matriz RBAC — 4 papéis × N operações; cada combinação negada tem teste
- [ ] **ST-02 — Green:** implementar testes com JWT de teste por papel
- [ ] **ST-03 — Red:** isolamento — token de tenant A não acessa recursos de tenant B; retorna 404
- [ ] **ST-04 — Green:** dois tenants no mesmo Testcontainers
- [ ] **ST-05 — Red:** contratos — schema de resposta válido; paginação; erros em Problem Details
- [ ] **ST-06 — Green:** implementar testes de contrato
- [ ] **ST-07 — Encerramento:** Api.Tests verdes; coverage Api ≥80%; commit `test(organization): API tests for RBAC and tenant isolation`; push

#### Critérios de Aceite

- [ ] Toda combinação papel×operação tem teste de permissão E negação
- [ ] Isolamento entre tenants testado e verde
- [ ] Coverage Api ≥80%

---

### TASK-24 — Testes de contrato de eventos *.v1

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `test/organization/24-contract-tests-events` |
| **Worktree** | `git worktree add ../worktrees/organization/24-contract-tests-events -b test/organization/24-contract-tests-events` |
| **Status** | [ ] |
| **Depende de** | TASK-18 |
| **Entregável** | Testes de contrato para os 5 eventos de integração; schema sem PII validado |
| **Mapeia** | design §9; DD-005; RNF 3, 4 |
| **Camada principal** | Tests |

#### Objetivo

Validar que `bu.created.v1`, `user.invited.v1`, `user.activated.v1`, `user.deactivated.v1` e `user.role_changed.v1` contêm os campos obrigatórios do envelope e não carregam e-mail ou `display_name`.

#### Subtasks

- [ ] **ST-01 — Red:** testes de schema — envelope obrigatório (`tenant_id`, `correlation_id`, `message_id`, `occurred_at`); carga sem PII
- [ ] **ST-02 — Green:** implementar validação de schema
- [ ] **ST-03 — Red:** testes de mapeamento — domain event `MembershipRoleChanged` → `user.role_changed.v1` com campos corretos
- [ ] **ST-04 — Green:** implementar e validar mapeamento
- [ ] **ST-05 — Encerramento:** testes de contrato verdes; commit `test(organization): contract tests for integration events`; push

#### Critérios de Aceite

- [ ] Os 5 eventos têm testes de contrato com schema validado
- [ ] Nenhum evento contém PII na carga
- [ ] Mapeamento domain → integration event testado

---

### TASK-25 — Observabilidade (logs, métricas, health checks, alertas)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `feat/organization/25-observability` |
| **Worktree** | `git worktree add ../worktrees/organization/25-observability -b feat/organization/25-observability` |
| **Status** | [ ] |
| **Depende de** | TASK-22 |
| **Entregável** | Logs estruturados sem PII, métricas, health checks e alerta de último TAdmin conforme design §11 |
| **Mapeia** | RNF 6; design §11; NFR-OBS-01 |
| **Camada principal** | Infrastructure |

#### Objetivo

Serilog + Cloud Logging com mascaramento de PII; métricas `users_invited_total`, `users_deactivated_total`, `bu_created_total`, `membership_cache_hit_ratio`, `tenant_rls_violation_count`; health checks `/health/live` e `/health/ready`; alerta de último TAdmin.

#### Subtasks

- [ ] **ST-01 — Red:** testes de log — nenhum evento contém e-mail em texto claro; `correlation_id` presente
- [ ] **ST-02 — Green:** implementar mascaramento no `LoggingBehavior`; enrichers Serilog
- [ ] **ST-03 — Green:** implementar métricas com `System.Diagnostics.Metrics`; incrementar nos handlers corretos
- [ ] **ST-04 — Green:** implementar `/health/live` e `/health/ready` (Postgres + Redis)
- [ ] **ST-05 — Red:** teste de alerta — mock `ITenantAdminCounter` retornando 1; verificar `Warning` no log
- [ ] **ST-06 — Green:** implementar alerta via log estruturado
- [ ] **ST-07 — Encerramento:** Infrastructure.Tests verdes; `/health/ready` 200 com dependências disponíveis; commit `feat(organization): observability with logs, metrics and health checks`; push

#### Critérios de Aceite

- [ ] Logs sem PII validados por teste
- [ ] Cinco métricas expostas e incrementadas corretamente
- [ ] `/health/live` e `/health/ready` funcionais
- [ ] Alerta de último TAdmin implementado

---

### TASK-26 — Segurança e PII (mascaramento, token hash, secrets, anti-enumeração)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `feat/organization/26-security-pii` |
| **Worktree** | `git worktree add ../worktrees/organization/26-security-pii -b feat/organization/26-security-pii` |
| **Status** | [ ] |
| **Depende de** | TASK-21, TASK-25 |
| **Entregável** | Revisão de segurança e PII: token hash com pepper, secrets gerenciados, anti-enumeração validada |
| **Mapeia** | RNF 3; design §10; DD-007; LGPD |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar `HmacSha256TokenHasher` com pepper via `ISecretProvider` (Secret Manager); confirmar anti-enumeração nos fluxos de convite/aceite; varredura de PII em logs de integração; garantir que erros não expõem stack trace.

#### Subtasks

- [ ] **ST-01 — Red:** testes — `token_hash` é hash com pepper, diferente do token em claro; pepper alterado produz hash diferente
- [ ] **ST-02 — Green:** implementar `HmacSha256TokenHasher` com `ISecretProvider`
- [ ] **ST-03 — Red:** testes de anti-enumeração — e-mail ativo e inexistente retornam mesma estrutura de resposta
- [ ] **ST-04 — Green:** ajustar handlers e controllers para resposta uniforme
- [ ] **ST-05 — Red:** varredura de PII — logs de integração não contêm e-mail em texto claro
- [ ] **ST-06 — Refactor:** revisão final de erros para não expor PII ou stack trace
- [ ] **ST-07 — Encerramento:** testes verdes; commit `feat(organization): security hardening and PII protection`; push

#### Critérios de Aceite

- [ ] `token_hash` verificado como hash com pepper
- [ ] Anti-enumeração: respostas não distinguem conta existente de inexistente
- [ ] Logs sem PII em texto claro
- [ ] Secrets via `ISecretProvider`, não hardcoded

---

### TASK-27 — PBT-01 (isolamento por tenant) + PBT-05 (integridade na desativação)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `test/organization/27-pbt-isolation-integrity` |
| **Worktree** | `git worktree add ../worktrees/organization/27-pbt-isolation-integrity -b test/organization/27-pbt-isolation-integrity` |
| **Status** | [ ] |
| **Depende de** | TASK-15, TASK-16, TASK-23 |
| **Entregável** | PBT-01 e PBT-05 verdes com Testcontainers; PBT-01 como gate obrigatório de CI |
| **Mapeia** | PBT-01, PBT-05; RNF 1; Req 2, 7; KPI-06 |
| **Camada principal** | Tests |

#### Objetivo

PBT-01: gerar N tenants × M registros; toda query por `tenant_id` T retorna apenas registros de T. PBT-05: gerar usuários/BUs com registros vinculados; desativação preserva todas as FKs e contagens. PBT-01 configurado como gate de CI (zero falhas bloqueia merge).

#### Subtasks

- [ ] **ST-01 — Red:** PBT-01 — gerar 2..5 tenants com 10..50 registros; afirmar exclusividade por `tenant_id`
- [ ] **ST-02 — Green:** implementar via FsCheck + Testcontainers Postgres; testar RLS e global filter
- [ ] **ST-03 — Red:** PBT-05 — gerar usuários/BUs com memberships; soft-delete; afirmar contagens idênticas
- [ ] **ST-04 — Green:** implementar PBT-05
- [ ] **ST-05 — CI:** configurar PBT-01 como step separado no pipeline CI; falha bloqueia merge
- [ ] **ST-06 — Encerramento:** PBT-01 e PBT-05 verdes ≥100 exemplos cada; CI configurado; commit `test(organization): PBT-01 isolation and PBT-05 integrity as CI gate`; push

#### Critérios de Aceite

- [ ] PBT-01 verde: zero falhas de isolamento em ≥100 exemplos
- [ ] PBT-05 verde: zero violações de integridade em ≥100 exemplos
- [ ] PBT-01 configurado como gate obrigatório no CI

---

### TASK-28 — DoD final, README sync e matriz de rastreabilidade completa

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `chore/organization/28-dod-readme-sync` |
| **Worktree** | `git worktree add ../worktrees/organization/28-dod-readme-sync -b chore/organization/28-dod-readme-sync` |
| **Status** | [ ] |
| **Depende de** | TASK-25, TASK-26, TASK-27 |
| **Entregável** | DoD §19 completo; README do módulo sincronizado; matriz de rastreabilidade sem órfãos |
| **Mapeia** | design §19; todos os requisitos |
| **Camada principal** | Docs |

#### Objetivo

Verificar todos os 11 itens do DoD (design §19), sincronizar `docs/product/modules/organization/README.md`, atualizar status da matriz de rastreabilidade deste documento e verificar consistência entre os três artefatos do módulo.

#### Subtasks

- [ ] **ST-01:** percorrer os 11 itens do DoD §19; marcar cumprido ou registrar pendência
- [ ] **ST-02:** atualizar `docs/product/modules/organization/README.md` com status, versão, ondas e TASKs
- [ ] **ST-03:** atualizar status da seção 5 (Matriz de Rastreabilidade) deste documento
- [ ] **ST-04:** verificar que nenhum Req/RNF está sem TASK associada
- [ ] **ST-05 — Encerramento:** CI verde; commit `chore(organization): DoD verification and README sync`; push; PR da Onda 6 aberto

#### Critérios de Aceite

- [ ] Todos os 11 itens do DoD marcados como cumpridos ou com pendência justificada
- [ ] README do módulo sincronizado
- [ ] Matriz de rastreabilidade sem requisito órfão
- [ ] CI verde

---

## 5. Matriz de Rastreabilidade

| Origem | Descrição | TASKs | Status |
|--------|-----------|-------|--------|
| Req 1 | Criar BU com nome único por tenant | TASK-04, TASK-08, TASK-20 | [ ] |
| Req 2 | Editar e inativar BU preservando histórico | TASK-04, TASK-08, TASK-20, TASK-27 | [ ] |
| Req 3 | Convidar usuário por e-mail com token temporário | TASK-06, TASK-09, TASK-21 | [ ] |
| Req 4 | Aceitar convite e ativar usuário | TASK-06, TASK-09, TASK-21 | [ ] |
| Req 5 | Gerenciar memberships (papel por BU) | TASK-05, TASK-10, TASK-20 | [ ] |
| Req 6 | Aplicar matriz RBAC papel × BU | TASK-07, TASK-14, TASK-20, TASK-23 | [ ] |
| Req 7 | Desativar usuário preservando histórico | TASK-05, TASK-12, TASK-21, TASK-27 | [ ] |
| Req 8 | Garantir ao menos um Tenant Admin ativo | TASK-10, TASK-12, TASK-13, TASK-20, TASK-21 | [ ] |
| Req 9 | Configurar estágios de pipeline por BU com seed | TASK-04, TASK-11, TASK-22 | [ ] |
| Req 10 | Configurar canais de origem por BU | TASK-04, TASK-11, TASK-22 | [ ] |
| Req 11 | Configurar motivos de perda por BU | TASK-04, TASK-11, TASK-22 | [ ] |
| Req 12 | Provisionar BU inicial e Tenant Admin | TASK-13, TASK-18 | [ ] |
| Req 13 | Exportar contexto RBAC via cache | TASK-14, TASK-17 | [ ] |
| RNF 1 | Isolamento por tenant (defesa em profundidade) | TASK-07, TASK-15, TASK-27 | [ ] |
| RNF 2 | RBAC verificado em todo endpoint | TASK-07, TASK-20, TASK-21, TASK-22, TASK-23 | [ ] |
| RNF 3 | Proteção de PII de usuários (LGPD) | TASK-03, TASK-09, TASK-25, TASK-26 | [ ] |
| RNF 4 | Auditoria imutável de toda escrita | TASK-07, TASK-18, TASK-24 | [ ] |
| RNF 5 | Disponibilidade e consistência do cache | TASK-14, TASK-17 | [ ] |
| RNF 6 | Observabilidade das operações | TASK-25 | [ ] |
| PBT-01 | Isolamento por tenant em qualquer consulta | TASK-27 | [ ] |
| PBT-02 | Invariante de Tenant Admin ativo | TASK-10 | [ ] |
| PBT-03 | Idempotência de provisionamento e aceite de convite | TASK-09, TASK-13 | [ ] |
| PBT-04 | Máquina de estados do convite | TASK-06 | [ ] |
| PBT-05 | Preservação de integridade na desativação | TASK-27 | [ ] |
| PBT-06 | Conservação das categorias terminais de estágio | TASK-04 | [ ] |
| PBT-07 | Unicidade e ordenação de estágios por BU | TASK-04 | [ ] |
| DD-001 | RLS como camada de isolamento (override database-naming) | TASK-15, TASK-27 | [ ] |
| DD-002 | Seed de estágios fixo (Vellus) | TASK-04, TASK-08 | [ ] |
| DD-003 | Invariante de último TAdmin com lock transacional | TASK-10, TASK-12 | [ ] |
| DD-004 | Convite atômico via Outbox de e-mail | TASK-09, TASK-18 | [ ] |
| DD-005 | Reconciliação de nomes de evento (domain → integration) | TASK-18, TASK-24 | [ ] |
| DD-006 | Stage/OriginChannel/LossReason como entidades de BusinessUnit | TASK-04, TASK-11 | [ ] |
| DD-007 | Token de convite persistido como hash | TASK-03, TASK-09, TASK-26 | [ ] |
| ADR-0001 | Multi-tenancy pooled DB + RLS | TASK-01, TASK-15 | [ ] |

---

## 6. Coverage Gates

| Camada | Gate | Tipo de teste esperado |
|--------|------|------------------------|
| Domain | ≥95% | Unitários de invariantes, objetos de valor, state machine; PBT-04, PBT-06, PBT-07 |
| Application | ≥85% | Unitários de handlers, validators, behaviors; PBT-02, PBT-03 |
| Infrastructure | ≥70% | Integração com Testcontainers (Postgres + Redis); Outbox/Inbox; PBT-01, PBT-05 |
| Api | ≥80% | Integração end-to-end (WebApplicationFactory); RBAC; isolamento entre tenants |
| Architecture | 100% das regras críticas | Regras de dependência entre camadas (NetArchTest) |
| Segurança | Cobertura por cenário crítico | Anti-enumeração, isolamento cross-tenant, token hash, mascaramento de PII |
| Observabilidade | Cobertura por fluxo crítico | Logs sem PII, métricas incrementadas, health checks, alerta de último TAdmin |

PBT-01 é gate obrigatório de CI (zero falhas bloqueia merge).

---

## 7. Critérios de Encerramento

### Encerramento de TASK

Uma TASK só pode ser marcada como `[X]` quando:

- Todas as subtasks concluídas
- Testes aplicáveis verdes
- Coverage gate da camada atendido ou justificativa registrada
- Lint/format executado
- Nenhum warning novo introduzido
- Commit em Conventional Commits realizado e push da branch realizado
- Documentação atualizada quando aplicável

### Encerramento de Onda

Todas as TASKs da onda `[X]`, CI verde (incluindo PBT-01 gate quando aplicável), PR da onda aberto/aprovado/mergeado, riscos tratados ou registrados, README do módulo sincronizado quando aplicável.

### Encerramento do Módulo

- Todas as 6 ondas concluídas
- Matriz de rastreabilidade completa (zero requisitos órfãos)
- `requirements.md`, `design.md` e `tasks.md` consistentes
- PBT-01..07 verdes
- Observabilidade mínima implementada (métricas, logs, health checks)
- Segurança mínima validada (RLS, RBAC, anti-enumeração, token hash, PII)
- Catálogo de 17 erros coberto
- DoD §19 do design completamente atendido
- README do módulo atualizado

---

## 8. Riscos de Execução

| Código | Risco | Probabilidade | Impacto | Mitigação |
|--------|-------|---------------|---------|-----------|
| RISK-TASK-01 | Pool de conexões reutiliza conexão sem `SET app.current_tenant` | Média | Alto (vazamento cross-tenant) | Testar com pool habilitado no Testcontainers; `TenantContextBehavior` em toda conexão |
| RISK-TASK-02 | `LastTenantAdminPolicy` sem lock pode ter condição de corrida | Baixa | Alto (tenant sem TAdmin) | `SELECT FOR UPDATE` ou `SERIALIZABLE`; teste de concorrência em Infrastructure.Tests |
| RISK-TASK-03 | `IdentityPlatformProvisioner` indisponível no aceite deixa convite em estado parcial | Média | Médio | Rollback transacional; retry com backoff; aceite não conclui sem `identity_uid` |
| RISK-TASK-04 | Testcontainers lentos tornam CI inviável | Média | Baixo | Job separado para testes de integração; `--parallel` |
| RISK-TASK-05 | Incompatibilidade EF Core/Npgsql com `SET app.current_tenant` | Baixa | Médio | Validar em TASK-15 antes de avançar para Onda 4 completa |

---

## 9. Referências

| Documento | Seção |
|-----------|-------|
| requirements.md (organization) | v0.1.0 — Req 1..13, RNF 1..6, PBT-01..07 |
| design.md (organization) | v0.1.0 — §3..§19 |
| docs/product/modules/organization/README.md | Status, riscos RISK-ORG-01..05 |
| ADR-0001 | Multi-tenancy pooled DB + RLS |
| ADR-0009 | Propagação de `correlation_id` e `tenant_id` |
| `.forge/rules/architecture/clean-architecture.md` | Regras de dependência entre camadas |
| `.forge/rules/domain/audit-immutability.md` | Auditoria append-only |
| `.forge/rules/conventions/database-naming.md` | Nomes físicos `snake_case` (override por DD-001 para RLS) |
| docs/product/frd-nfrd/frd.md | §15 Matriz de Permissões; MSG-015..018 |
| docs/product/data-model/data-model.md | §BC-08 |

