# Tasks — PM — Partner Management

- Versão: 0.1.1
- Data: 2026-06-14
- Status: Aprovado para desenvolvimento
- Referência base requirements: docs/product/modules/partner-management/requirements.md v1.0.0
- Referência base design: docs/product/modules/partner-management/design.md v0.1.0
- ADRs aplicáveis: ADR-0001, ADR-0002, ADR-0003
- Rules aplicáveis: `.forge/rules/architecture/clean-architecture.md`, `.forge/rules/architecture/api-and-contracts.md`, `.forge/rules/architecture/observability.md`, `.forge/rules/architecture/security-and-compliance.md`, `.forge/rules/architecture/ddd.md`, `.forge/rules/conventions/database-naming.md`, `.forge/rules/domain/audit-immutability.md`, `.forge/rules/domain/money-as-cents.md`, `.forge/rules/domain/nbr-5891-rounding.md`, `.forge/rules/architecture/jwt-permissions.md`, `.forge/rules/architecture/mtls-internal-services.md`

## Histórico de Versões

| Versão | Data | Status | Descrição da alteração |
|--------|------|--------|------------------------|
| 0.1.0 | 2026-06-11 | Rascunho para revisão | Criação inicial do plano de tasks derivado de requirements.md v1.0.0 e design.md v0.1.0 |
| 0.1.1 | 2026-06-14 | Aprovado para desenvolvimento | Aprovação humana (HITL #1); execução via `/forge:coding-loop` autônomo (6 ondas). |

## 1. Convenções de Implementação

### 1.1 TDD-first

Toda implementação com lógica verificável segue o ciclo:

1. Red — escrever teste que falha
2. Green — implementar o mínimo para passar
3. Refactor — melhorar sem alterar comportamento

Nenhuma regra de domínio, handler, endpoint, persistência, contrato ou integração é concluída sem teste correspondente.

### 1.2 Property-Based Testing

PBT é obrigatório para invariantes matemáticas, idempotência, round-trip, anti-enumeração, state machines e regras de conservação. Cada PBT mapeia explicitamente para `PBT-NN` do `requirements.md` ou invariante descrita no `design.md`. Biblioteca preferida: FsCheck (integrado com xUnit).

### 1.3 Bite-sized Tasks

Cada subtask deve ser estimada em menos de 2 horas. Se exceder, deve ser dividida. Cada TASK deve ter preferencialmente até 1 dia; no máximo 2 dias quando o escopo for claro e o entregável verificável.

### 1.4 Branch Model

```
<tipo>/partner-management/<NN>-<slug>
```

Exemplos: `feat/partner-management/01-bootstrap-solution`, `test/partner-management/08-pbt-domain`.

### 1.5 Git Worktree

```sh
git worktree add ../worktrees/partner-management/<NN>-<slug> -b <branch>
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
| TASK-01 | Solution .NET + 5 projetos Clean Architecture | Onda 1 | `feat/partner-management/01-bootstrap-solution` | [X] |
| TASK-02 | Architecture.Tests e CI mínimo | Onda 1 | `feat/partner-management/02-architecture-tests` | [X] |
| TASK-03 | Objetos de valor PartnerName, PartnerRole, Percentage, CommissionDefaults | Onda 2 | `feat/partner-management/03-value-objects-core` | [X] |
| TASK-04 | Objetos de valor PartnerContact, Email, Phone | Onda 2 | `feat/partner-management/04-value-objects-contact` | [X] |
| TASK-05 | Agregado Partner: invariantes e métodos de comportamento | Onda 2 | `feat/partner-management/05-partner-aggregate` | [X] |
| TASK-06 | Domain Events e interface IPartnerRepository | Onda 2 | `feat/partner-management/06-domain-events-repo-interface` | [X] |
| TASK-07 | State machine PartnerStatus e Specifications | Onda 2 | `feat/partner-management/07-status-specifications` | [X] |
| TASK-08 | PBT-02 e PBT-03 — Domain Tests | Onda 2 | `test/partner-management/08-pbt-domain` | [X] |
| TASK-09 | Commands CreatePartner e UpdatePartner com handlers e validators | Onda 3 | `feat/partner-management/09-create-update-commands` | [X] |
| TASK-10 | Commands DeactivatePartner e ReactivatePartner com handlers | Onda 3 | `feat/partner-management/10-deactivate-reactivate` | [X] |
| TASK-11 | Queries ListPartners, GetPartnerById, GetPartnerEligibility | Onda 3 | `feat/partner-management/11-partner-queries` | [X] |
| TASK-12 | Porta IPartnerCommissionReadPort e queries de comissão | Onda 3 | `feat/partner-management/12-commission-queries` | [X] |
| TASK-13 | Pipeline behaviors MediatR | Onda 3 | `feat/partner-management/13-pipeline-behaviors` | [X] |
| TASK-14 | PBT-01 e PBT-05 — Application Tests | Onda 3 | `test/partner-management/14-pbt-application` | [X] |
| TASK-15 | EF Core DbContext, mapeamento e filtro global de tenant | Onda 4 | `feat/partner-management/15-ef-dbcontext` | [X] |
| TASK-16 | Migrations: partners + outbox + idempotency + índices + RLS | Onda 4 | `feat/partner-management/16-migrations-rls` | [X] |
| TASK-17 | PartnerRepository, TenantContext e RLS interceptor | Onda 4 | `feat/partner-management/17-repository-tenant-rls` | [X] |
| TASK-18 | Outbox transacional e AuditPublisher | Onda 4 | `feat/partner-management/18-outbox-audit` | [X] |
| TASK-19 | PartnerCommissionReadAdapter (adaptador do pipeline) | Onda 4 | `feat/partner-management/19-commission-read-adapter` | [X] |
| TASK-20 | CanonicalRoleProvider, PartnerPiiMasker e idempotência de escrita | Onda 4 | `feat/partner-management/20-roles-pii-idempotency` | [X] |
| TASK-21 | PBT-04 — Isolamento por tenant (gate CI) | Onda 4 | `test/partner-management/21-pbt-tenant-isolation` | [X] |
| TASK-22 | DTOs de contratos (request/response/eventos) | Onda 5 | `feat/partner-management/22-contracts-dtos` | [X] |
| TASK-23 | PartnersController CRUD e middleware | Onda 5 | `feat/partner-management/23-controller-crud` | [X] |
| TASK-24 | Endpoints deactivate/reactivate/eligibility/commissions | Onda 5 | `feat/partner-management/24-controller-actions` | [X] |
| TASK-25 | Testes de contrato Pact, RBAC e catálogo de erros | Onda 5 | `test/partner-management/25-api-contract-rbac` | [X] |
| TASK-26 | Observabilidade: logs estruturados, métricas e traces | Onda 6 | `feat/partner-management/26-observability` | [X] |
| TASK-27 | PartnerPiiMasker aplicado e teste anti-PII (gate CI) | Onda 6 | `feat/partner-management/27-pii-masking` | [X] |
| TASK-28 | Resiliência do IPartnerCommissionReadPort | Onda 6 | `feat/partner-management/28-commission-resilience` | [X] |
| TASK-29 | Documentação OpenAPI, DoD e fechamento do módulo | Onda 6 | `docs/partner-management/29-openapi-dod` | [X] |

---

## 3. Ondas de Implementação

| Onda | Foco | TASKs |
|------|------|-------|
| Onda 1 | Bootstrap — solution, projetos, testes de arquitetura | TASK-01..TASK-02 |
| Onda 2 | Domain — objetos de valor, agregado, eventos, state machine, PBTs de domínio | TASK-03..TASK-08 |
| Onda 3 | Application — commands, queries, handlers, behaviors, PBTs de application | TASK-09..TASK-14 |
| Onda 4 | Infrastructure — EF Core, migrations, RLS, repositório, outbox, adapters | TASK-15..TASK-21 |
| Onda 5 | API + Contratos — controllers, DTOs, RBAC, erros, contratos Pact | TASK-22..TASK-25 |
| Onda 6 | Hardening — observabilidade, PII masking, resiliência, DoD | TASK-26..TASK-29 |

**Critérios de fechamento:**

- **Onda 1:** solution compilando; `Architecture.Tests` verde; CI configurado.
- **Onda 2:** todos os objetos de valor, agregado, eventos e state machine com testes verdes; PBT-02 e PBT-03 verdes; cobertura Domain ≥ 95%.
- **Onda 3:** commands, queries e behaviors testados; PBT-01 e PBT-05 verdes; cobertura Application ≥ 85%.
- **Onda 4:** migrations aplicadas com RLS ativa em Testcontainers; PBT-04 verde como gate CI (KPI-06); cobertura Infrastructure ≥ 70%.
- **Onda 5:** todos os endpoints respondendo; RBAC validado; contratos Pact verdes; catálogo de erros coberto; cobertura Api ≥ 80%.
- **Onda 6:** scan anti-PII verde como gate CI; métricas expostas; resiliência do read port validada; DoD completo; módulo encerrado.

---

## 4. Tarefas

### TASK-01 — Solution .NET + 5 projetos Clean Architecture

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 1 — Bootstrap |
| **Branch** | `feat/partner-management/01-bootstrap-solution` |
| **Worktree** | `git worktree add ../worktrees/partner-management/01-bootstrap-solution -b feat/partner-management/01-bootstrap-solution` |
| **Status** | [ ] |
| **Depende de** | Não aplicável |
| **Entregável** | `PartnerManagement.sln` com 5 projetos de produção e 5 de teste compilando |
| **Mapeia** | design §3, design §2 (P1) |
| **Camada principal** | DevOps |

#### Objetivo

Criar a solução .NET e os 5 projetos (Domain/Application/Infrastructure/Api/Contracts) mais os projetos de teste correspondentes, com referências corretas e estrutura de pastas conforme design §3. Dependências base incluídas: MediatR, FluentValidation, EF Core, xUnit, FsCheck.

#### Subtasks

- [ ] **ST-01 — Red:** escrever um teste de build mínimo que verifica a existência dos 5 projetos de produção — falha por ausência
- [ ] **ST-02 — Green:** criar `PartnerManagement.sln`; criar os 10 projetos; configurar referências conforme grafo design §3; adicionar dependências base
- [ ] **ST-03 — Refactor:** validar namespaces, estrutura de pastas e ausência de referências proibidas; remover dependências desnecessárias
- [ ] **ST-04 — Encerramento:** `dotnet build` verde; commit `feat(partner-management): bootstrap solution and 5-project Clean Architecture` e push

#### Critérios de Aceite

- [ ] Todos os 10 projetos compilam sem erros ou warnings relevantes
- [ ] Referências entre projetos respeitam o grafo de dependência de design §3
- [ ] `Domain` não referencia qualquer projeto de nível superior
- [ ] Commit realizado; push da branch

---

### TASK-02 — Architecture.Tests e CI mínimo

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 1 — Bootstrap |
| **Branch** | `feat/partner-management/02-architecture-tests` |
| **Worktree** | `git worktree add ../worktrees/partner-management/02-architecture-tests -b feat/partner-management/02-architecture-tests` |
| **Status** | [ ] |
| **Depende de** | TASK-01 |
| **Entregável** | `Architecture.Tests` com regras de dependência entre camadas validadas por NetArchTest; pipeline CI rodando build + test |
| **Mapeia** | design §3, design §2 (P1), design §13 (Architecture.Tests) |
| **Camada principal** | Tests / DevOps |

#### Objetivo

Garantir por teste automatizado que a regra de dependência de Clean Architecture seja mantida durante toda a evolução do módulo. CI configurado para executar build + test em cada push.

#### Subtasks

- [ ] **ST-01 — Red:** escrever todos os testes de dependência (Domain → ∅; Contracts → ∅; Application → Domain + Contracts; Infrastructure → Application + Domain; Api → Application + Infrastructure + Contracts) — falham com projetos vazios
- [ ] **ST-02 — Green:** ajustar referências para que todos os testes fiquem verdes
- [ ] **ST-03 — Refactor:** adicionar regra de namespace; documentar regras em comentário dos testes
- [ ] **ST-04 — Encerramento:** CI configurado; `dotnet test Architecture.Tests` verde; commit `test(partner-management): architecture dependency rules` e push

#### Critérios de Aceite

- [ ] Testes de dependência cobrem os 5 projetos e todas as direções proibidas
- [ ] CI executa build + test em cada push à branch
- [ ] Commit realizado; push da branch

---

### TASK-03 — Objetos de valor PartnerName, PartnerRole, Percentage, CommissionDefaults

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `feat/partner-management/03-value-objects-core` |
| **Worktree** | `git worktree add ../worktrees/partner-management/03-value-objects-core -b feat/partner-management/03-value-objects-core` |
| **Status** | [ ] |
| **Depende de** | TASK-02 |
| **Entregável** | `PartnerName`, `PartnerRole`, `Percentage`, `CommissionDefaults` com invariantes, igualdade por valor e exceções `PartnerNameRequiredException`, `InvalidPartnerRoleException`, `PercentageOutOfRangeException` |
| **Mapeia** | Req 1.1, Req 5, Req 6.3, Req 6.4, Req 6.5, RNF 6, PBT-03, design §4.3, DD-004 |
| **Camada principal** | Domain |

#### Objetivo

Implementar os quatro objetos de valor centrais. `Percentage` encapsula `decimal` no intervalo [0,00; 100,00]; `CommissionDefaults` compõe dois `Percentage` com default `0,00`/`0,00`; `PartnerName` garante nome não vazio após trim; `PartnerRole` encapsula o papel tipado (validação contra `ICanonicalRoleProvider` ocorre no aggregate, não no VO).

#### Subtasks

- [ ] **ST-01 — Red:** testes para: `PartnerName` vazio/somente espaços lança exceção; `PartnerName` preserva valor trimado; `Percentage` fora do intervalo lança `PercentageOutOfRangeException`; `Percentage` válido preserva exatamente 2 casas decimais; `CommissionDefaults` default `0,00`/`0,00`; igualdade por valor em todos os VOs; sem `float`/`double`
- [ ] **ST-02 — Green:** implementar `PartnerName`, `Percentage`, `CommissionDefaults`, `PartnerRole` com as exceções correspondentes usando `decimal`
- [ ] **ST-03 — Refactor:** imutabilidade (`init`/`readonly`); igualdade estrutural via `record` ou `IEquatable<T>`; remover duplicação
- [ ] **ST-04 — Encerramento:** `dotnet test Domain.Tests` verde; commit `feat(partner-management): value objects PartnerName, PartnerRole, Percentage, CommissionDefaults` e push

#### Critérios de Aceite

- [ ] `Percentage` rejeita < 0 e > 100; aceita exatamente 2 casas decimais
- [ ] `PartnerName` rejeita string vazia ou somente espaços após trim
- [ ] `CommissionDefaults` default é `0,00`/`0,00`
- [ ] Todos os VOs implementam igualdade por valor; imutáveis
- [ ] Nenhum `float`/`double` (RNF 6.3, DD-004)
- [ ] Commit realizado; push da branch

---

### TASK-04 — Objetos de valor PartnerContact, Email, Phone

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `feat/partner-management/04-value-objects-contact` |
| **Worktree** | `git worktree add ../worktrees/partner-management/04-value-objects-contact -b feat/partner-management/04-value-objects-contact` |
| **Status** | [ ] |
| **Depende de** | TASK-02 |
| **Entregável** | `PartnerContact`, `Email`, `Phone` com validações, método `ToMasked()` e tratamento conservador de PII (DD-008) |
| **Mapeia** | Req 7, RNF 4, design §4.3, DD-008 |
| **Camada principal** | Domain |

#### Objetivo

Implementar os objetos de valor de contato. `Email` valida formato RFC simplificado e normaliza para minúsculas; lança `InvalidPartnerContactException` quando inválido. `Phone` aceita apenas dígitos significativos. `PartnerContact` é opcional e expõe `ToMasked()` para uso seguro em logs e auditoria.

#### Subtasks

- [ ] **ST-01 — Red:** testes para `Email` malformado lança exceção; `Email` válido normalizado para minúsculas; `Phone` aceita dígitos; `PartnerContact.ToMasked()` não retorna e-mail/telefone em claro; igualdade por valor
- [ ] **ST-02 — Green:** implementar `Email`, `Phone` e `PartnerContact` com `ToMasked()` retornando representação mascarada (ex.: `***@***`, `***`)
- [ ] **ST-03 — Refactor:** centralizar regex de e-mail como constante testável; garantir imutabilidade
- [ ] **ST-04 — Encerramento:** testes verdes; commit `feat(partner-management): value objects PartnerContact, Email, Phone with PII masking` e push

#### Critérios de Aceite

- [ ] `Email` malformado lança `InvalidPartnerContactException`
- [ ] `ToMasked()` nunca retorna dado de contato em claro
- [ ] `PartnerContact` pode ser nulo (campo opcional)
- [ ] Commit realizado; push da branch

---

### TASK-05 — Agregado Partner: invariantes e métodos de comportamento

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `feat/partner-management/05-partner-aggregate` |
| **Worktree** | `git worktree add ../worktrees/partner-management/05-partner-aggregate -b feat/partner-management/05-partner-aggregate` |
| **Status** | [ ] |
| **Depende de** | TASK-03, TASK-04 |
| **Entregável** | `Partner` (Aggregate Root) com `Create`, `Reconstitute`, `UpdateProfile`, `UpdateContact`, `Deactivate`, `Reactivate` e invariantes I1..I5 |
| **Mapeia** | Req 1, Req 2, Req 3, Req 5, Req 6, Req 7, design §4.1, design §4.5 |
| **Camada principal** | Domain |

#### Objetivo

Implementar o Aggregate Root `Partner`. Cada método de comportamento protege as invariantes de domínio e acumula domain events para despacho após commit. Handlers apenas orquestram; regras de negócio vivem no aggregate.

#### Subtasks

- [ ] **ST-01 — Red:** testes para `Partner.Create` com nome vazio (falha I1), papel inválido (falha I2), percentual fora do intervalo (falha I3), parceiro nasce `Active` (I4), e-mail inválido (falha I5); testes para `UpdateProfile` reaplicando invariantes; testes para `Deactivate`/`Reactivate`
- [ ] **ST-02 — Green:** implementar `Partner.Create`, `Partner.Reconstitute`, `UpdateProfile`, `UpdateContact`, `Deactivate`, `Reactivate` com acumulação de domain events
- [ ] **ST-03 — Refactor:** garantir que regra de negócio não vaze para handlers; simplificar acumulação de events; extrair métodos privados de guarda
- [ ] **ST-04 — Encerramento:** `dotnet test Domain.Tests` verde; commit `feat(partner-management): Partner aggregate root with invariants I1..I5` e push

#### Critérios de Aceite

- [ ] Invariantes I1..I5 protegidas no Aggregate Root, não no handler
- [ ] `Partner` nasce com `status = Active`
- [ ] `Deactivate`/`Reactivate` são idempotentes sem exceção em repetição
- [ ] Domain events acumulados antes do commit
- [ ] Commit realizado; push da branch

---

### TASK-06 — Domain Events e interface IPartnerRepository

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `feat/partner-management/06-domain-events-repo-interface` |
| **Worktree** | `git worktree add ../worktrees/partner-management/06-domain-events-repo-interface -b feat/partner-management/06-domain-events-repo-interface` |
| **Status** | [ ] |
| **Depende de** | TASK-05 |
| **Entregável** | `PartnerCreated`, `PartnerCommissionPercentagesUpdated`, `PartnerDeactivated`, `PartnerReactivated`; interface `IPartnerRepository` |
| **Mapeia** | Req 1.8, Req 2.5, Req 3.6, RNF 2, RNF 2.4, design §4.4, design §3 |
| **Camada principal** | Domain |

#### Objetivo

Definir os quatro domain events (carga sem PII, com `partnerId`/`tenantId`/`occurredAt`) e a interface `IPartnerRepository` no Domain. Cada evento é acumulado pelo aggregate em suas transições e despachado após commit via Outbox.

#### Subtasks

- [ ] **ST-01 — Red:** testes para: `Partner.Create` acumula `PartnerCreated`; `UpdateProfile` com mudança de percentual acumula `PartnerCommissionPercentagesUpdated`; `Deactivate` em transição efetiva acumula `PartnerDeactivated`; `Reactivate` acumula `PartnerReactivated`; transição idempotente não acumula evento de transição
- [ ] **ST-02 — Green:** implementar os quatro records de domain event; definir `IPartnerRepository` (`GetByIdAsync`, `AddAsync`, `UpdateAsync`, `FindByNameAsync`); integrar acumulação no aggregate
- [ ] **ST-03 — Refactor:** garantir que payloads de evento não contenham `name` nem `contact` em claro (RNF 4); envelope base com `EventId`, `OccurredAt`, `CorrelationId`
- [ ] **ST-04 — Encerramento:** testes verdes; commit `feat(partner-management): domain events and IPartnerRepository interface` e push

#### Critérios de Aceite

- [ ] Cada transição efetiva acumula exatamente um domain event
- [ ] Transição idempotente não acumula evento de transição (DD-006)
- [ ] Payloads de evento sem `name`/`contact` em claro (RNF 4)
- [ ] `IPartnerRepository` definida no Domain sem referência a EF Core
- [ ] Commit realizado; push da branch

---

### TASK-07 — State machine PartnerStatus e Specifications

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `feat/partner-management/07-status-specifications` |
| **Worktree** | `git worktree add ../worktrees/partner-management/07-status-specifications -b feat/partner-management/07-status-specifications` |
| **Status** | [ ] |
| **Depende de** | TASK-05 |
| **Entregável** | `PartnerStatus` (enum Active/Inactive) com transições idempotentes; `CanonicalRoleSpecification`, `ActivePartnerSpecification`, `TriagePendingSpecification`, `TenantScopeSpecification` |
| **Mapeia** | Req 3, Req 4.2, Req 8, Req 11, design §4.5, design §4.6 |
| **Camada principal** | Domain |

#### Objetivo

Formalizar a state machine `Active ↔ Inactive` (idempotente, sem exclusão física) e as quatro Specifications do módulo: `CanonicalRoleSpecification` (papel pertence à lista vigente do tenant), `ActivePartnerSpecification` (elegível à vinculação), `TriagePendingSpecification` (percentuais em zero — derivado), `TenantScopeSpecification` (filtro de tenant para leitura).

#### Subtasks

- [ ] **ST-01 — Red:** testes para state machine: `Deactivate` sobre `Active` → `Inactive`; `Deactivate` sobre `Inactive` → `Inactive` (idempotente); `Reactivate` sobre `Inactive` → `Active`; `Reactivate` sobre `Active` → `Active` (idempotente); testes para cada Specification com casos válidos e inválidos
- [ ] **ST-02 — Green:** implementar `PartnerStatus`; implementar as quatro Specifications com interface `ISpecification<T>`
- [ ] **ST-03 — Refactor:** extrair lógica de guard das transições para métodos nomeados; garantir que `TriagePendingSpecification` seja derivada (não campo persistido)
- [ ] **ST-04 — Encerramento:** testes verdes; commit `feat(partner-management): PartnerStatus state machine and domain specifications` e push

#### Critérios de Aceite

- [ ] `Deactivate`/`Reactivate` são idempotentes (PBT-02 preparado)
- [ ] Quatro Specifications implementadas com testes para todos os casos limítrofes
- [ ] `TriagePendingSpecification` derivada de percentuais (não flag persistido)
- [ ] Commit realizado; push da branch

---

### TASK-08 — PBT-02 e PBT-03 — Domain Tests

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `test/partner-management/08-pbt-domain` |
| **Worktree** | `git worktree add ../worktrees/partner-management/08-pbt-domain -b test/partner-management/08-pbt-domain` |
| **Status** | [ ] |
| **Depende de** | TASK-07 |
| **Entregável** | PBT-02 (idempotência de inativação/reativação) e PBT-03 (round-trip de percentuais) verdes no `Domain.Tests` usando FsCheck |
| **Mapeia** | PBT-02, PBT-03, Req 3.5, Req 6.5, RNF 6.2 |
| **Camada principal** | Tests |

#### Objetivo

Implementar os property-based tests do Domain: PBT-02 verifica que aplicar `Deactivate` N≥1 vezes sempre resulta em `Inactive` e `Reactivate` N≥1 vezes em `Active`; PBT-03 verifica que todo percentual válido em [0,00; 100,00] com 2 casas é criado e lido sem perda de precisão, e todo valor fora do intervalo é rejeitado.

#### Subtasks

- [ ] **ST-01 — Red:** escrever PBT-02 com gerador de N aplicações aleatórias de `Deactivate`/`Reactivate`; escrever PBT-03 com gerador de `decimal` em [0,00; 100,00] com 2 casas e gerador de valores fora do intervalo
- [ ] **ST-02 — Green:** ajustar implementações de `PartnerStatus` e `Percentage` até os PBTs passarem consistentemente
- [ ] **ST-03 — Refactor:** parametrizar geradores para cobrir extremos (0,00; 100,00; 0,01; 99,99)
- [ ] **ST-04 — Encerramento:** `dotnet test Domain.Tests --filter Category=PBT` verde; commit `test(partner-management): PBT-02 idempotency and PBT-03 percentage round-trip` e push

#### Critérios de Aceite

- [ ] PBT-02: N≥1 aplicações de `Deactivate` → `Inactive`; N≥1 `Reactivate` → `Active`
- [ ] PBT-03: todo percentual válido preservado sem erro de arredondamento; todo valor fora do intervalo rejeitado
- [ ] Nenhum `float`/`double` nos geradores nem na implementação
- [ ] Commit realizado; push da branch

---

### TASK-09 — Commands CreatePartner e UpdatePartner com handlers e validators

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/partner-management/09-create-update-commands` |
| **Worktree** | `git worktree add ../worktrees/partner-management/09-create-update-commands -b feat/partner-management/09-create-update-commands` |
| **Status** | [ ] |
| **Depende de** | TASK-06, TASK-07 |
| **Entregável** | `CreatePartnerCommand`/Handler/Validator e `UpdatePartnerCommand`/Handler/Validator; alerta de nome duplicado (MSG-021) com `confirmCreateDespiteDuplicate`; triagem pós-import (percentuais zerados aceitos do `data-migration`) |
| **Mapeia** | Req 1, Req 2, Req 5, Req 6, Req 7, Req 11, Req 12, design §5.1 |
| **Camada principal** | Application |

#### Objetivo

Implementar os dois commands de escrita de cadastro. O handler de criação invoca `Partner.Create` via `IPartnerRepository`; checa nome duplicado via `FindByNameAsync` e expõe flag `confirmCreateDespiteDuplicate` (não bloqueia). Aceita percentuais zerados quando originado pelo `data-migration` (Req 11.1). O handler de atualização invoca `UpdateProfile`/`UpdateContact`.

#### Subtasks

- [ ] **ST-01 — Red:** testes unitários para: `CreatePartnerCommand` com nome vazio retorna erro PM-ERR-001; papel inválido retorna PM-ERR-002; percentual fora do intervalo retorna PM-ERR-003; e-mail inválido retorna PM-ERR-004; criação bem-sucedida acumula `PartnerCreated`; alerta de nome duplicado não bloqueia; `UpdatePartnerCommand` com id inexistente retorna PM-ERR-007
- [ ] **ST-02 — Green:** implementar `CreatePartnerCommand`, `UpdatePartnerCommand`, seus handlers e validators FluentValidation
- [ ] **ST-03 — Refactor:** garantir que regra de negócio não vaze do Domain para o Application; extrair lógica de triagem em method nomeado
- [ ] **ST-04 — Encerramento:** `dotnet test Application.Tests` verde; commit `feat(partner-management): CreatePartner and UpdatePartner commands with handlers` e push

#### Critérios de Aceite

- [ ] Nome duplicado gera alerta (MSG-021) sem bloquear criação
- [ ] Percentuais zerados aceitos quando `data-migration` os omite (Req 11.1)
- [ ] Erros PM-ERR-001..004 e PM-ERR-007 retornados nos casos corretos
- [ ] Domain events acumulados pelo aggregate e coletados pelo handler
- [ ] Commit realizado; push da branch

---

### TASK-10 — Commands DeactivatePartner e ReactivatePartner com handlers

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/partner-management/10-deactivate-reactivate` |
| **Worktree** | `git worktree add ../worktrees/partner-management/10-deactivate-reactivate -b feat/partner-management/10-deactivate-reactivate` |
| **Status** | [ ] |
| **Depende de** | TASK-09 |
| **Entregável** | `DeactivatePartnerCommand`/Handler e `ReactivatePartnerCommand`/Handler, idempotentes, gerando auditoria mesmo em repetição |
| **Mapeia** | Req 3, PBT-02, design §5.1, DD-006 |
| **Camada principal** | Application |

#### Objetivo

Implementar os commands de ciclo de vida do parceiro. Aplicar `Deactivate`/`Reactivate` sobre o estado já vigente não lança erro nem emite evento de transição, mas registra auditoria da tentativa (DD-006, PBT-02). Sem erro tipo "já inativo/ativo".

#### Subtasks

- [ ] **ST-01 — Red:** testes para: `DeactivatePartnerCommand` sobre parceiro ativo → `Inactive` + `PartnerDeactivated`; sobre parceiro inativo → sem evento de transição + auditoria registrada; `ReactivatePartnerCommand` análogo; `partner_id` inexistente → PM-ERR-007
- [ ] **ST-02 — Green:** implementar handlers invocando `Partner.Deactivate`/`Partner.Reactivate`; coletar apenas eventos de transição efetiva para Outbox; registrar auditoria sempre
- [ ] **ST-03 — Refactor:** extrair lógica de auditoria de tentativa para método reutilizável
- [ ] **ST-04 — Encerramento:** testes verdes; commit `feat(partner-management): DeactivatePartner and ReactivatePartner commands, idempotent` e push

#### Critérios de Aceite

- [ ] Idempotência: operação repetida retorna sucesso sem evento de transição adicional
- [ ] Auditoria sempre registrada (transição efetiva ou tentativa)
- [ ] PM-ERR-007 retornado para `partner_id` inexistente ou fora do tenant
- [ ] Commit realizado; push da branch

---

### TASK-11 — Queries ListPartners, GetPartnerById, GetPartnerEligibility

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/partner-management/11-partner-queries` |
| **Worktree** | `git worktree add ../worktrees/partner-management/11-partner-queries -b feat/partner-management/11-partner-queries` |
| **Status** | [ ] |
| **Depende de** | TASK-09 |
| **Entregável** | `ListPartnersQuery`/Handler (filtros `active`, `triagePending`, paginação), `GetPartnerByIdQuery`/Handler e `GetPartnerEligibilityQuery`/Handler |
| **Mapeia** | Req 4, Req 8.1, Req 11.3, design §5.2, RNF 7.1 |
| **Camada principal** | Application |

#### Objetivo

Implementar as queries de leitura de parceiros. `ListPartnersQuery` retorna página de parceiros com filtro `active=true` por padrão e suporte a `triagePending=true` (derivado de percentuais zerados). `GetPartnerEligibilityQuery` retorna `{ partnerId, active }` para o pipeline validar elegibilidade antes de vincular.

#### Subtasks

- [ ] **ST-01 — Red:** testes para: `ListPartnersQuery` default retorna apenas ativos; filtro `triagePending=true` retorna apenas parceiros com percentuais zerados; paginação funciona; `GetPartnerByIdQuery` com id inexistente retorna PM-ERR-007; `GetPartnerEligibilityQuery` retorna `active=false` para parceiro inativo
- [ ] **ST-02 — Green:** implementar handlers; mock de `IPartnerRepository` nos testes unitários
- [ ] **ST-03 — Refactor:** extrair mapeamento de domínio para DTOs em método separado
- [ ] **ST-04 — Encerramento:** testes verdes; commit `feat(partner-management): ListPartners, GetPartnerById and GetPartnerEligibility queries` e push

#### Critérios de Aceite

- [ ] `ListPartnersQuery` padrão retorna apenas parceiros `active=true` do tenant
- [ ] `triagePending=true` filtra apenas parceiros com `pct_setup = 0 AND pct_recorrente = 0`
- [ ] `GetPartnerEligibilityQuery` expõe `active` para o pipeline (Req 8.1)
- [ ] PM-ERR-007 retornado para id inexistente ou fora do tenant
- [ ] Commit realizado; push da branch

---

### TASK-12 — Porta IPartnerCommissionReadPort e queries de comissão

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/partner-management/12-commission-queries` |
| **Worktree** | `git worktree add ../worktrees/partner-management/12-commission-queries -b feat/partner-management/12-commission-queries` |
| **Status** | [ ] |
| **Depende de** | TASK-11 |
| **Entregável** | Interface `IPartnerCommissionReadPort`; `GetPartnerCommissionViewQuery`/Handler e `GetPartnerCommissionReportQuery`/Handler consumindo o read model sem recalcular comissão |
| **Mapeia** | Req 9, Req 10, PBT-01, PBT-05, DD-003, design §5.2, design §6.4 |
| **Camada principal** | Application |

#### Objetivo

Definir a porta de leitura `IPartnerCommissionReadPort` (declarada no Application) e implementar as duas queries que compõem dados do cadastro `partners` com o read model `opportunity_partner_commissions` do pipeline. Este módulo nunca recalcula nem persiste comissão (DD-003). Em indisponibilidade do read port, retorna dados de cadastro com seção de comissão marcada como indisponível.

#### Subtasks

- [ ] **ST-01 — Red:** testes para: `GetPartnerCommissionViewQuery` com read model simulado retorna soma projetada das oportunidades abertas e soma consolidada dos snapshots; PM-ERR-011 para período inválido; PM-ERR-007 para `partner_id` inexistente; degradação parcial quando read port indisponível
- [ ] **ST-02 — Green:** definir `IPartnerCommissionReadPort`; implementar `GetPartnerCommissionViewHandler` e `GetPartnerCommissionReportHandler` com mock do port
- [ ] **ST-03 — Refactor:** extrair montagem da resposta de comissão em método separado; garantir que nenhuma fórmula de comissão seja implementada aqui
- [ ] **ST-04 — Encerramento:** testes verdes; commit `feat(partner-management): IPartnerCommissionReadPort and commission view/report queries` e push

#### Critérios de Aceite

- [ ] Nenhuma fórmula de comissão implementada neste módulo (DD-003)
- [ ] Visão projetada = soma de oportunidades abertas; consolidada = soma de snapshots
- [ ] Degradação parcial funciona sem derrubar o cadastro (design §5.3)
- [ ] Período inválido retorna PM-ERR-011; parceiro inexistente retorna PM-ERR-007
- [ ] Commit realizado; push da branch

---

### TASK-13 — Pipeline behaviors MediatR

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/partner-management/13-pipeline-behaviors` |
| **Worktree** | `git worktree add ../worktrees/partner-management/13-pipeline-behaviors -b feat/partner-management/13-pipeline-behaviors` |
| **Status** | [ ] |
| **Depende de** | TASK-09 |
| **Entregável** | Behaviors MediatR: `CorrelationLoggingBehavior`, `TenantScopeBehavior`, `ValidationBehavior`, `AuthorizationBehavior`, `TransactionBehavior`; portas `IAuditPublisher`, `IClock`, `ICanonicalRoleProvider` |
| **Mapeia** | RNF 1, RNF 4, RNF 5, design §5.4 |
| **Camada principal** | Application |

#### Objetivo

Implementar o pipeline de behaviors MediatR na ordem de execução correta. `CorrelationLoggingBehavior` injeta `correlation_id`/`tenant_id`/`partner_id` sem PII. `TenantScopeBehavior` resolve `TenantContext`. `ValidationBehavior` executa FluentValidation. `AuthorizationBehavior` verifica RBAC por `permissions` do JWT. `TransactionBehavior` abre transação para Commands e coleta events no Outbox.

#### Subtasks

- [ ] **ST-01 — Red:** testes de integração de behaviors: request com papel insuficiente retorna PM-ERR-008; request sem `tenant_id` é rejeitado; Command com entidade inválida não chega ao handler; logs não contêm `name`/`contact`
- [ ] **ST-02 — Green:** implementar os cinco behaviors na ordem correta; definir portas `IAuditPublisher`, `IClock`, `ICanonicalRoleProvider`
- [ ] **ST-03 — Refactor:** extrair mapeamento de `permissions` JWT para enum interno; garantir que PII não vaze no `CorrelationLoggingBehavior`
- [ ] **ST-04 — Encerramento:** testes verdes; commit `feat(partner-management): MediatR pipeline behaviors and application ports` e push

#### Critérios de Aceite

- [ ] Ordem de execução: Logging → Tenant → Validation → Authorization → Transaction
- [ ] Escrita exclusiva a Tenant Admin / Gestor de BU; relatório de comissão idem; listagem a Viewer+
- [ ] Logs não contêm `name`/`contact_email`/`contact_phone` em claro (RNF 4)
- [ ] `TransactionBehavior` coleta domain events no Outbox na mesma transação
- [ ] Commit realizado; push da branch

---

### TASK-14 — PBT-01 e PBT-05 — Application Tests

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `test/partner-management/14-pbt-application` |
| **Worktree** | `git worktree add ../worktrees/partner-management/14-pbt-application -b test/partner-management/14-pbt-application` |
| **Status** | [ ] |
| **Depende de** | TASK-12, TASK-13 |
| **Entregável** | PBT-01 (conservação da soma de comissão) e PBT-05 (imutabilidade da comissão consolidada frente à edição de percentuais) verdes no `Application.Tests` |
| **Mapeia** | PBT-01, PBT-05, Req 9, Req 2.3 |
| **Camada principal** | Tests |

#### Objetivo

PBT-01: para qualquer conjunto de oportunidades gerado aleatoriamente, a soma projetada exibida = soma das comissões das abertas, e a consolidada = soma dos snapshots das ganhas, sem dupla contagem. PBT-05: editar `pct_setup`/`pct_recorrente` do parceiro mantém inalterada a comissão consolidada (snapshot do pipeline); pode alterar a projetada.

#### Subtasks

- [ ] **ST-01 — Red:** escrever PBT-01 com gerador de conjuntos de oportunidades mistas (abertas + ganhas) e mock do `IPartnerCommissionReadPort`; escrever PBT-05 com gerador de pares (percentual antes, percentual depois) verificando consolidada inalterada
- [ ] **ST-02 — Green:** ajustar `GetPartnerCommissionViewHandler` até os PBTs passarem consistentemente
- [ ] **ST-03 — Refactor:** melhorar geradores para cobrir casos de conjunto vazio, conjunto só aberto, conjunto só ganho
- [ ] **ST-04 — Encerramento:** `dotnet test Application.Tests --filter Category=PBT` verde; commit `test(partner-management): PBT-01 commission conservation and PBT-05 snapshot immutability` e push

#### Critérios de Aceite

- [ ] PBT-01: sem dupla contagem nem omissão na soma de comissão
- [ ] PBT-05: consolidada imutável após qualquer edição de percentuais
- [ ] Geradores cobrem conjunto vazio, misto e extremos
- [ ] Commit realizado; push da branch

---

### TASK-15 — EF Core DbContext, mapeamento e filtro global de tenant

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/partner-management/15-ef-dbcontext` |
| **Worktree** | `git worktree add ../worktrees/partner-management/15-ef-dbcontext -b feat/partner-management/15-ef-dbcontext` |
| **Status** | [ ] |
| **Depende de** | TASK-13 |
| **Entregável** | `PartnerManagementDbContext` com `HasQueryFilter` por `tenant_id`; `IEntityTypeConfiguration` para `Partner`, `OutboxMessage`, `IdempotencyKey`; owned types `CommissionDefaults`, `PartnerRole`, `PartnerContact` mapeados como colunas inline |
| **Mapeia** | RNF 1, DD-001, design §6.1, design §7 |
| **Camada principal** | Infrastructure |

#### Objetivo

Configurar o DbContext com filtro global que garante isolamento por tenant em toda query de leitura. Mapear os objetos de valor como owned types/colunas inline conforme esquema design §7. `HasQueryFilter` aplica `tenant_id = TenantContext.CurrentTenantId` em todas as entidades multi-tenant.

#### Subtasks

- [ ] **ST-01 — Red:** teste de integração com Testcontainers: inserir dois parceiros de tenants distintos; query sem filtro explícito retorna apenas os do tenant do contexto
- [ ] **ST-02 — Green:** implementar `PartnerManagementDbContext` com `HasQueryFilter`; configurações EF Core via `IEntityTypeConfiguration`; mapear owned types e colunas snake_case
- [ ] **ST-03 — Refactor:** extrair constantes de nomes de coluna; validar que nenhum `DbSet` vaza para fora da Infrastructure
- [ ] **ST-04 — Encerramento:** teste de integração verde; commit `feat(partner-management): EF Core DbContext with global tenant filter and owned type mappings` e push

#### Critérios de Aceite

- [ ] `HasQueryFilter` aplicado em `Partner`, `OutboxMessage`, `IdempotencyKey`
- [ ] Owned types mapeados como colunas inline (`pct_setup`, `pct_recorrente`, `partner_type`, `contact_email`, `contact_phone`)
- [ ] Nomes físicos de coluna em `snake_case` (rule `database-naming.md`)
- [ ] Commit realizado; push da branch

---

### TASK-16 — Migrations: partners + outbox + idempotency + índices + RLS

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/partner-management/16-migrations-rls` |
| **Worktree** | `git worktree add ../worktrees/partner-management/16-migrations-rls -b feat/partner-management/16-migrations-rls` |
| **Status** | [ ] |
| **Depende de** | TASK-15 |
| **Entregável** | Migration EF Core criando as três tabelas com CHECKs, índices `idx_partners_tenant_active` e `idx_partners_tenant_name`, e RLS habilitada nas três tabelas no mesmo arquivo de migration |
| **Mapeia** | RNF 1, DD-001, ADR-0001, design §7 |
| **Camada principal** | Infrastructure |

#### Objetivo

Criar a migration versionada que materializa o esquema design §7: tabela `partners` (com CHECKs `chk_partners_name_not_blank`, `chk_partners_pct_setup_range`, `chk_partners_pct_recorrente_range`), `outbox_messages`, `idempotency_keys`, os dois índices e a política de RLS `partners_tenant_isolation` habilitada na mesma migration (gate de conformidade ADR-0001).

#### Subtasks

- [ ] **ST-01 — Red:** teste com Testcontainers que aplica a migration e verifica: existência das três tabelas; CHECKs de percentual rejeitem valores inválidos no banco; índices criados; RLS ativa (`SELECT * FROM pg_policies WHERE tablename = 'partners'`)
- [ ] **ST-02 — Green:** gerar migration EF Core; adicionar SQL raw para CHECKs, índices e `ALTER TABLE ... ENABLE ROW LEVEL SECURITY; CREATE POLICY ...`
- [ ] **ST-03 — Refactor:** garantir idempotência da migration; documentar no comentário da migration a conformidade ADR-0001
- [ ] **ST-04 — Encerramento:** `dotnet ef database update` verde em Testcontainers; commit `feat(partner-management): EF Core migrations with RLS, indexes and constraints` e push

#### Critérios de Aceite

- [ ] RLS habilitada em `partners`, `outbox_messages`, `idempotency_keys` na mesma migration
- [ ] CHECKs de nome e percentuais rejeitam valores inválidos no banco (segunda camada de defesa)
- [ ] Dois índices criados: `idx_partners_tenant_active` e `idx_partners_tenant_name`
- [ ] Migration aplicável e reversível sem erros
- [ ] Commit realizado; push da branch

---

### TASK-17 — PartnerRepository, TenantContext e RLS interceptor

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/partner-management/17-repository-tenant-rls` |
| **Worktree** | `git worktree add ../worktrees/partner-management/17-repository-tenant-rls -b feat/partner-management/17-repository-tenant-rls` |
| **Status** | [ ] |
| **Depende de** | TASK-16 |
| **Entregável** | `PartnerRepository` implementando `IPartnerRepository`; `TenantContext`; interceptor de conexão executando `SET app.current_tenant` antes de qualquer comando |
| **Mapeia** | RNF 1, DD-001, design §6.1 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar o repositório concreto com Testcontainers. O interceptor de conexão executa `SET app.current_tenant = '<tenantId>'` antes de qualquer comando, ativando a política de RLS. `TenantContext` é resolvido do request atual via `TenantScopeBehavior`.

#### Subtasks

- [ ] **ST-01 — Red:** teste de integração com Testcontainers: `GetByIdAsync` de parceiro de outro tenant retorna null; `AddAsync` + `GetByIdAsync` do mesmo tenant retorna o parceiro; `FindByNameAsync` busca case-insensitive no tenant
- [ ] **ST-02 — Green:** implementar `PartnerRepository`; implementar `TenantContext` e interceptor de conexão DbCommandInterceptor
- [ ] **ST-03 — Refactor:** garantir que repositório não exponha `IQueryable`/`DbSet`; centralizar mapeamento de entidade para domínio
- [ ] **ST-04 — Encerramento:** testes de integração verdes; commit `feat(partner-management): PartnerRepository, TenantContext and RLS session interceptor` e push

#### Critérios de Aceite

- [ ] `SET app.current_tenant` executado antes de todo comando de negócio
- [ ] `GetByIdAsync` de parceiro de outro tenant retorna null (RLS ativa)
- [ ] Repositório não expõe `IQueryable` para fora da Infrastructure
- [ ] Commit realizado; push da branch

---

### TASK-18 — Outbox transacional e AuditPublisher

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/partner-management/18-outbox-audit` |
| **Worktree** | `git worktree add ../worktrees/partner-management/18-outbox-audit -b feat/partner-management/18-outbox-audit` |
| **Status** | [ ] |
| **Depende de** | TASK-17 |
| **Entregável** | `OutboxMessage`, `OutboxPublisher` (relay background), implementação de `IAuditPublisher`; integração com `TransactionBehavior` garantindo atomicidade escrita + outbox |
| **Mapeia** | RNF 2, RNF 3, design §6.3, design §6.6 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar o padrão Outbox transacional: domain events são persistidos em `outbox_messages` na mesma transação da escrita de domínio; o relay background publica no Pub/Sub e marca como enviado. `IAuditPublisher` registra cada escrita em `audit_logs` (append-only, via `PartnerPiiMasker`). Garantia at-least-once com deduplicação por `event_id` no consumidor.

#### Subtasks

- [ ] **ST-01 — Red:** teste de integração com Testcontainers: criar parceiro; verificar que `outbox_messages` contém um registro `published_at = null` com payload sem PII; simular commit e verificar que relay marca `published_at`
- [ ] **ST-02 — Green:** implementar `OutboxMessage`, `OutboxPublisher`; implementar `IAuditPublisher` aplicando `PartnerPiiMasker` no `delta_json`; integrar no `TransactionBehavior`
- [ ] **ST-03 — Refactor:** garantir idempotência do relay (não republica mensagem já marcada); validar que `audit_logs` é append-only (sem UPDATE/DELETE pela role `app`)
- [ ] **ST-04 — Encerramento:** testes verdes; commit `feat(partner-management): Outbox transactional pattern and AuditPublisher` e push

#### Critérios de Aceite

- [ ] Escrita + Outbox na mesma transação (atomicidade)
- [ ] Payload de evento sem PII (`partner.name`/`contact` mascarados)
- [ ] `audit_logs` append-only: sem UPDATE/DELETE acessível pela role `app`
- [ ] Relay idempotente (at-least-once sem duplicação observável)
- [ ] Commit realizado; push da branch

---

### TASK-19 — PartnerCommissionReadAdapter (adaptador do pipeline)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/partner-management/19-commission-read-adapter` |
| **Worktree** | `git worktree add ../worktrees/partner-management/19-commission-read-adapter -b feat/partner-management/19-commission-read-adapter` |
| **Status** | [ ] |
| **Depende de** | TASK-17 |
| **Entregável** | `PartnerCommissionReadAdapter` implementando `IPartnerCommissionReadPort`; leitura de `opportunity_partner_commissions` via API/SQL interno com mTLS; filtro por `tenant_id`, `partner_id` e período |
| **Mapeia** | Req 9, Req 10, DD-003, design §6.4 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar o adaptador do read model do pipeline. Consulta `opportunity_partner_commissions` filtrando `tenant_id` + `partner_id` + período. Propaga `correlation_id` e `tenant_id` no cabeçalho. Nunca implementa fórmula de comissão; apenas lê e mapeia o resultado para os tipos do Application.

#### Subtasks

- [ ] **ST-01 — Red:** teste de integração com servidor stub (WireMock.Net): adapter retorna linhas projetadas e snapshots para o período; adapter retorna coleção vazia para parceiro sem oportunidades; propagação de `correlation_id` verificada no stub
- [ ] **ST-02 — Green:** implementar `PartnerCommissionReadAdapter` com `HttpClient` configurado para mTLS interno; mapear resposta para `CommissionLine` records
- [ ] **ST-03 — Refactor:** garantir que nenhuma lógica de agregação (soma) ocorra no adapter — soma fica no handler
- [ ] **ST-04 — Encerramento:** testes verdes; commit `feat(partner-management): PartnerCommissionReadAdapter consuming pipeline read model` e push

#### Critérios de Aceite

- [ ] Adapter não implementa fórmula de comissão (DD-003)
- [ ] `correlation_id` e `tenant_id` propagados em toda chamada
- [ ] Filtro por `tenant_id` + `partner_id` + período aplicado
- [ ] Commit realizado; push da branch

---

### TASK-20 — CanonicalRoleProvider, PartnerPiiMasker e idempotência de escrita

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/partner-management/20-roles-pii-idempotency` |
| **Worktree** | `git worktree add ../worktrees/partner-management/20-roles-pii-idempotency -b feat/partner-management/20-roles-pii-idempotency` |
| **Status** | [ ] |
| **Depende de** | TASK-17 |
| **Entregável** | `CanonicalRoleProvider` com cache por tenant; `PartnerPiiMasker` com `MaskName` e `MaskContact`; deduplicação por `Idempotency-Key` em `idempotency_keys` |
| **Mapeia** | Req 5, RNF 4, DD-005, DD-008, design §6.2, design §6.5 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar o provider de papéis canônicos (seed: Indicador, Revendedor, Distribuidor, Integrador; cache por tenant com TTL curto). Implementar `PartnerPiiMasker` usado por Outbox/AuditPublisher/logs para mascarar `name` e contato. Implementar deduplicação por `Idempotency-Key` para operações do `data-migration`.

#### Subtasks

- [ ] **ST-01 — Red:** testes para: `CanonicalRoleProvider` retorna seed para tenant sem configuração própria; cache é usado na segunda chamada; `PartnerPiiMasker.MaskName("Acme")` não retorna "Acme"; `Idempotency-Key` repetida com payload idêntico retorna PM-ERR-010 sem reprocessar
- [ ] **ST-02 — Green:** implementar `CanonicalRoleProvider` com cache `IMemoryCache`; implementar `PartnerPiiMasker`; implementar deduplicação em `IdempotencyKeyRepository`
- [ ] **ST-03 — Refactor:** validar que cache inclui `tenant_id` na chave; garantir que `PartnerPiiMasker` é usado em todos os pontos de log/evento
- [ ] **ST-04 — Encerramento:** testes verdes; commit `feat(partner-management): CanonicalRoleProvider, PartnerPiiMasker and idempotency deduplication` e push

#### Critérios de Aceite

- [ ] `CanonicalRoleProvider` cacheado com `tenant_id` na chave; seed canônico provisionado
- [ ] `PartnerPiiMasker` mascara `name` e `contact` em todos os pontos de saída
- [ ] Idempotência da criação via `data-migration` validada (PM-ERR-010 para payload divergente)
- [ ] Commit realizado; push da branch

---

### TASK-21 — PBT-04 — Isolamento por tenant (gate CI)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `test/partner-management/21-pbt-tenant-isolation` |
| **Worktree** | `git worktree add ../worktrees/partner-management/21-pbt-tenant-isolation -b test/partner-management/21-pbt-tenant-isolation` |
| **Status** | [ ] |
| **Depende de** | TASK-20 |
| **Entregável** | PBT-04 verde no `Infrastructure.Tests` com Testcontainers + PostgreSQL real; registrado como gate CI (KPI-06) |
| **Mapeia** | PBT-04, RNF 1.4, DD-001, ADR-0001 |
| **Camada principal** | Tests |

#### Objetivo

Para qualquer `partner_id` pertencente ao tenant B, nenhuma requisição executada no contexto do tenant A deve retornar, listar ou modificar esse parceiro — por EF Core e por RLS do PostgreSQL. O teste usa Testcontainers com banco real e gerador de pares de tenant aleatórios. É gate obrigatório de CI.

#### Subtasks

- [ ] **ST-01 — Red:** escrever PBT-04 com gerador de pares `(tenantA, tenantB)` e gerador de `partner_id` de tenantB; verificar que `GetByIdAsync` no contexto de tenantA retorna null; que `ListPartnersQuery` no contexto de tenantA não inclui parceiros de tenantB; que tentativa de UPDATE via repositório de tenantA não afeta parceiro de tenantB
- [ ] **ST-02 — Green:** ajustar `PartnerRepository` e interceptor até o PBT-04 passar consistentemente com banco real
- [ ] **ST-03 — Refactor:** configurar o PBT-04 como gate CI (categoria `TenantIsolation`); documentar KPI-06
- [ ] **ST-04 — Encerramento:** `dotnet test Infrastructure.Tests --filter Category=TenantIsolation` verde; CI configurado para exigir esse gate; commit `test(partner-management): PBT-04 cross-tenant isolation gate CI` e push

#### Critérios de Aceite

- [ ] PBT-04 usa PostgreSQL real via Testcontainers (não mock)
- [ ] Nenhum parceiro de tenantB visível ou modificável no contexto de tenantA
- [ ] Gate CI configurado e bloqueante (KPI-06)
- [ ] Commit realizado; push da branch

---

### TASK-22 — DTOs de contratos (request/response/eventos)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contratos |
| **Branch** | `feat/partner-management/22-contracts-dtos` |
| **Worktree** | `git worktree add ../worktrees/partner-management/22-contracts-dtos -b feat/partner-management/22-contracts-dtos` |
| **Status** | [ ] |
| **Depende de** | TASK-12 |
| **Entregável** | DTOs em `Contracts`: `CreatePartnerRequest`, `UpdatePartnerRequest`, `PartnerResponse`, `PartnerEligibilityResponse`, `CommissionViewResponse`, `CommissionReportResponse`; envelopes de evento `PartnerCreated.v1`, `PartnerCommissionPercentagesUpdated.v1`, `PartnerDeactivated.v1`, `PartnerReactivated.v1` |
| **Mapeia** | design §8, design §9 |
| **Camada principal** | Contracts |

#### Objetivo

Definir todos os contratos de request/response e os envelopes de evento versionados (sufixo `.v1`). Contratos de request incluem anotações de validação; response inclui campos obrigatórios de design §8. Envelopes de evento sem PII em claro (RNF 4).

#### Subtasks

- [ ] **ST-01 — Red:** testes de serialização/deserialização para cada DTO; testes de validação de anotações em `CreatePartnerRequest` e `UpdatePartnerRequest`
- [ ] **ST-02 — Green:** implementar todos os DTOs e envelopes de evento no projeto `Contracts`
- [ ] **ST-03 — Refactor:** garantir que envelopes de evento não exponham `name`/`contact`; usar tipos `decimal` (não `float`) para percentuais; valores monetários como `long` (centavos inteiros)
- [ ] **ST-04 — Encerramento:** testes de serialização verdes; commit `feat(partner-management): Contracts DTOs and event envelopes v1` e push

#### Critérios de Aceite

- [ ] Todos os DTOs de request/response implementados conforme design §8
- [ ] Quatro envelopes de evento `.v1` sem PII em claro
- [ ] Percentuais como `decimal`; comissão como `long` (centavos)
- [ ] Commit realizado; push da branch

---

### TASK-23 — PartnersController CRUD e middleware

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contratos |
| **Branch** | `feat/partner-management/23-controller-crud` |
| **Worktree** | `git worktree add ../worktrees/partner-management/23-controller-crud -b feat/partner-management/23-controller-crud` |
| **Status** | [ ] |
| **Depende de** | TASK-22, TASK-21 |
| **Entregável** | `PartnersController` com `GET /api/v1/partners`, `POST /api/v1/partners`, `GET /api/v1/partners/{id}`, `PATCH /api/v1/partners/{id}`; middleware de `CorrelationId`, `TenantResolution`, tratamento de exceções |
| **Mapeia** | Req 1, Req 2, Req 4, design §8 |
| **Camada principal** | Api |

#### Objetivo

Implementar os quatro endpoints CRUD básicos e os middleware de suporte. `POST /partners` aceita header `Idempotency-Key` opcional (para `data-migration`). Todos os endpoints retornam erros no formato padrão `{ "error", "code", "correlationId" }`.

#### Subtasks

- [ ] **ST-01 — Red:** testes de integração (WebApplicationFactory): `POST` com nome vazio → 400 PM-ERR-001; `POST` bem-sucedido → 201 com `Location`; `GET` lista somente ativos por padrão; `PATCH` com id inexistente → 404 PM-ERR-007; `POST` sem papel adequado → 403 PM-ERR-008
- [ ] **ST-02 — Green:** implementar `PartnersController` e três middleware; configurar DI e routing
- [ ] **ST-03 — Refactor:** extrair mapeamento de exceções de domínio para HTTP responses em handler de exceções centralizado
- [ ] **ST-04 — Encerramento:** testes de integração verdes; commit `feat(partner-management): PartnersController CRUD endpoints and middleware` e push

#### Critérios de Aceite

- [ ] `GET /partners` retorna apenas ativos por padrão; suporta `triagePending` e paginação
- [ ] `POST /partners` retorna 201 com `Location` header
- [ ] Formato de erro padrão com `correlationId` em todos os 4xx/5xx
- [ ] Middleware de `CorrelationId` e `TenantResolution` ativos
- [ ] Commit realizado; push da branch

---

### TASK-24 — Endpoints deactivate/reactivate/eligibility/commissions

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contratos |
| **Branch** | `feat/partner-management/24-controller-actions` |
| **Worktree** | `git worktree add ../worktrees/partner-management/24-controller-actions -b feat/partner-management/24-controller-actions` |
| **Status** | [ ] |
| **Depende de** | TASK-23 |
| **Entregável** | `POST /api/v1/partners/{id}/deactivate`, `POST /api/v1/partners/{id}/reactivate`, `GET /api/v1/partners/{id}/eligibility`, `GET /api/v1/partners/{id}/commissions?from={date}&to={date}` |
| **Mapeia** | Req 3, Req 8, Req 9, Req 10, design §8 |
| **Camada principal** | Api |

#### Objetivo

Implementar os quatro endpoints de ação e de consulta especializada. `deactivate`/`reactivate` são idempotentes (200 em qualquer caso). `eligibility` é endpoint interno consumido pelo pipeline via mTLS. `commissions` retorna visão projetada × consolidada em centavos inteiros, restrito a Tenant Admin e Gestor de BU.

#### Subtasks

- [ ] **ST-01 — Red:** testes de integração: `POST deactivate` sobre parceiro já inativo → 200 (sem erro); `GET eligibility` com parceiro inativo → `{ active: false }`; `GET commissions` sem período → 400 PM-ERR-011; `GET commissions` com papel Viewer → 403 PM-ERR-008
- [ ] **ST-02 — Green:** implementar os quatro endpoints; configurar autenticação mTLS no endpoint `eligibility`
- [ ] **ST-03 — Refactor:** garantir que `commissions` retorna centavos inteiros (não BRL formatado); validar degradação parcial quando read port indisponível
- [ ] **ST-04 — Encerramento:** testes de integração verdes; commit `feat(partner-management): deactivate, reactivate, eligibility and commissions endpoints` e push

#### Critérios de Aceite

- [ ] `deactivate`/`reactivate` idempotentes (200 em repetição)
- [ ] `eligibility` restrito a mTLS interno; retorna `{ partnerId, active }`
- [ ] `commissions` retorna centavos inteiros; restrito a Tenant Admin/Gestor de BU
- [ ] PM-ERR-011 para período ausente/inválido
- [ ] Commit realizado; push da branch

---

### TASK-25 — Testes de contrato Pact, RBAC e catálogo de erros

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contratos |
| **Branch** | `test/partner-management/25-api-contract-rbac` |
| **Worktree** | `git worktree add ../worktrees/partner-management/25-api-contract-rbac -b test/partner-management/25-api-contract-rbac` |
| **Status** | [ ] |
| **Depende de** | TASK-24 |
| **Entregável** | `Api.Tests` cobrindo RBAC por papel, todos os códigos HTTP esperados e todos os erros PM-ERR-001..011; contratos Pact para `opportunity-pipeline` (provider de lista e eligibility) e consumer do read model de comissão |
| **Mapeia** | RNF 1, design §8, design §12, design §13 |
| **Camada principal** | Tests |

#### Objetivo

Cobrir por testes automatizados o catálogo completo de erros (PM-ERR-001..011), a matriz RBAC (quem pode chamar cada endpoint) e os contratos de API com o `opportunity-pipeline`. Garantir anti-enumeração: `partner_id` de outro tenant retorna 404, não 403.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de RBAC para cada endpoint × papel (Viewer, Gestor de BU, Tenant Admin, sem token); escrever testes de catálogo de erros para cada PM-ERR; escrever contratos Pact para `GET /partners` e `GET /partners/{id}/eligibility`
- [ ] **ST-02 — Green:** ajustar controllers e middleware até todos os testes passarem
- [ ] **ST-03 — Refactor:** garantir que 404 seja retornado para `partner_id` de outro tenant (anti-enumeração, PBT-04)
- [ ] **ST-04 — Encerramento:** `dotnet test Api.Tests` verde; contratos Pact publicados; commit `test(partner-management): RBAC matrix, error catalog and Pact contracts` e push

#### Critérios de Aceite

- [ ] Todos os 11 erros do catálogo testados
- [ ] Matriz RBAC cobrindo Viewer, Gestor de BU, Tenant Admin e sem token para cada endpoint
- [ ] Anti-enumeração: `partner_id` de outro tenant → 404
- [ ] Contratos Pact verdes para endpoints consumidos pelo `opportunity-pipeline`
- [ ] Commit realizado; push da branch

---

### TASK-26 — Observabilidade: logs estruturados, métricas e traces

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `feat/partner-management/26-observability` |
| **Worktree** | `git worktree add ../worktrees/partner-management/26-observability -b feat/partner-management/26-observability` |
| **Status** | [X] |
| **Depende de** | TASK-25 |
| **Entregável** | Logs estruturados JSON com `correlation_id`/`tenant_id`/`partner_id`/`action` (sem PII); métricas Prometheus `partners_created_total`, `partners_deactivated_total`, `partners_reactivated_total`, `partner_commission_view_duration_seconds`; spans OpenTelemetry em handlers e no read port; health/readiness/liveness |
| **Mapeia** | RNF 5, design §11, ADR-0001 |
| **Camada principal** | Infrastructure / Api |

#### Objetivo

Implementar os três pilares de observabilidade conforme design §11. Logs estruturados em JSON sem PII; métricas Prometheus expostas em `/metrics`; spans OpenTelemetry cobrindo handlers, read port e operações de banco. Health checks verificam Cloud SQL e disponibilidade do read port.

#### Subtasks

- [ ] **ST-01 — Red:** testes: `CorrelationLoggingBehavior` inclui `correlation_id` e `tenant_id` no log; métricas `partners_created_total` incrementam após criação; span de `GetPartnerCommissionViewHandler` é criado com atributo `partner_id`
- [ ] **ST-02 — Green:** configurar Serilog/structured logging; registrar métricas Prometheus; configurar OpenTelemetry com spans; implementar health checks para Cloud SQL e read port
- [ ] **ST-03 — Refactor:** validar que nenhum log contém `name`/`contact` em claro (pré-requisito para TASK-27); configurar alertas de SLO no formato esperado
- [ ] **ST-04 — Encerramento:** testes verdes; commit `feat(partner-management): structured logging, Prometheus metrics and OpenTelemetry traces` e push

#### Critérios de Aceite

- [ ] Logs em JSON com `correlation_id`, `tenant_id`, `partner_id`, `action` em toda operação de escrita
- [ ] Métricas `partners_created_total` e `partners_deactivated_total` expostas e incrementando
- [ ] Spans OpenTelemetry cobrem handlers e chamada ao read port
- [ ] Health/readiness/liveness respondendo
- [ ] Commit realizado; push da branch

---

### TASK-27 — PartnerPiiMasker aplicado e teste anti-PII (gate CI)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `feat/partner-management/27-pii-masking` |
| **Worktree** | `git worktree add ../worktrees/partner-management/27-pii-masking -b feat/partner-management/27-pii-masking` |
| **Status** | [X] |
| **Depende de** | TASK-26 |
| **Entregável** | `PartnerPiiMasker` aplicado em todos os pontos de saída (logs, traces, `delta_json` de auditoria, payloads de evento); teste automatizado de scan anti-PII verde como gate CI; pendência VAL-PARTNER-01 registrada em `approvals.yaml` |
| **Mapeia** | RNF 4, DD-008, design §11, RISK-PM-02, RISK-PM-03 |
| **Camada principal** | Infrastructure / Tests |

#### Objetivo

Garantir por teste automatizado que `partner.name`, `contact_email` e `contact_phone` nunca aparecem em texto claro em logs, traces, `delta_json` de auditoria ou payloads de evento. O scan anti-PII executa fluxos completos de criação/edição e verifica os logs capturados. Registrar pendência VAL-PARTNER-01 em `approvals.yaml`.

#### Subtasks

- [ ] **ST-01 — Red:** escrever teste de scan: executar `CreatePartner` com nome e e-mail reais; capturar todos os logs e payloads de evento gerados; verificar que nenhum contém o nome ou e-mail em claro
- [ ] **ST-02 — Green:** aplicar `PartnerPiiMasker` nos pontos ainda descobertos (logs de behavior, payloads de evento, `delta_json` de auditoria)
- [ ] **ST-03 — Refactor:** configurar gate CI para a categoria `PiiScan`; registrar VAL-PARTNER-01 em `approvals.yaml` com status `pending`
- [ ] **ST-04 — Encerramento:** `dotnet test --filter Category=PiiScan` verde; CI bloqueante; commit `feat(partner-management): PII masking applied and anti-PII scan gate CI` e push

#### Critérios de Aceite

- [ ] Scan anti-PII verde: nenhum `partner.name`/`contact_email`/`contact_phone` em texto claro em nenhum ponto de saída
- [ ] Gate CI configurado e bloqueante para categoria `PiiScan`
- [ ] VAL-PARTNER-01 registrada em `approvals.yaml` como pendência aberta
- [ ] Commit realizado; push da branch

---

### TASK-28 — Resiliência do IPartnerCommissionReadPort

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `feat/partner-management/28-commission-resilience` |
| **Worktree** | `git worktree add ../worktrees/partner-management/28-commission-resilience -b feat/partner-management/28-commission-resilience` |
| **Status** | [X] |
| **Depende de** | TASK-27 |
| **Entregável** | Timeout, retry com backoff exponencial e circuit breaker configurados no `PartnerCommissionReadAdapter`; degradação parcial da seção de comissão sem derrubar o cadastro |
| **Mapeia** | RISK-PM-06, design §15, design §6.4, DD-003 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar políticas de resiliência no adaptador do read port usando Polly: timeout configurável, retry com backoff exponencial (max 3 tentativas), circuit breaker. Em indisponibilidade, `GetPartnerCommissionViewHandler` retorna dados de cadastro com seção `commission: null` e `commissionUnavailable: true`, sem retornar 5xx ao cliente.

#### Subtasks

- [ ] **ST-01 — Red:** testes com WireMock.Net simulando: timeout do read port → cadastro retornado com `commissionUnavailable: true`; read port retornando 500 → retry + circuit breaker abre após threshold; read port voltando → circuit breaker fecha
- [ ] **ST-02 — Green:** configurar políticas Polly no `HttpClient` do adapter; implementar lógica de degradação parcial no handler
- [ ] **ST-03 — Refactor:** extrair configuração de resiliência para classe separada; garantir que métricas contabilizam falhas do read port
- [ ] **ST-04 — Encerramento:** testes verdes; commit `feat(partner-management): commission read port resilience with Polly timeout, retry and circuit breaker` e push

#### Critérios de Aceite

- [ ] Timeout configurável; retry com backoff exponencial (max 3 tentativas)
- [ ] Circuit breaker abre após threshold de falhas configurado
- [ ] Degradação parcial: cadastro disponível; seção de comissão marcada como indisponível
- [ ] Métricas de falha do read port expostas
- [ ] Commit realizado; push da branch

---

### TASK-29 — Documentação OpenAPI, DoD e fechamento do módulo

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `docs/partner-management/29-openapi-dod` |
| **Worktree** | `git worktree add ../worktrees/partner-management/29-openapi-dod -b docs/partner-management/29-openapi-dod` |
| **Status** | [X] |
| **Depende de** | TASK-28 |
| **Entregável** | OpenAPI atualizado com todos os endpoints, erros e esquemas; DoD de design §19 verificado item a item; README do módulo sincronizado; RISK-PM-05 (divergência PUT vs PATCH) resolvido no README |
| **Mapeia** | Req 1..12, RNF 1..7, design §19 |
| **Camada principal** | Docs |

#### Objetivo

Encerrar o módulo verificando cada item do DoD de design §19. Atualizar OpenAPI para cobrir todos os 8 endpoints, 11 erros do catálogo e todos os schemas de request/response. Sincronizar `README.md` do módulo com status final. Resolver a divergência RISK-PM-05 (README usava `PUT`; TRD usa `PATCH`) no README.

#### Subtasks

- [ ] **ST-01 — Red:** verificar cada item do DoD de design §19 como lista de checklist; identificar itens ainda abertos
- [ ] **ST-02 — Green:** completar eventuais lacunas de DoD; gerar/atualizar OpenAPI via Swashbuckle; atualizar README com status `Aprovado para desenvolvimento` + tabela de status dos artefatos
- [ ] **ST-03 — Refactor:** revisar matriz de rastreabilidade deste `tasks.md`; atualizar status de cada TASK para `[X]`; garantir que RISK-PM-05 está resolvido e VAL-PARTNER-01 em `approvals.yaml`
- [ ] **ST-04 — Encerramento:** OpenAPI válido; DoD completo; commit `docs(partner-management): OpenAPI, DoD checklist and module README sync` e push

#### Critérios de Aceite

- [ ] Todos os 15 itens do DoD de design §19 verificados e marcados
- [ ] OpenAPI cobre os 8 endpoints, 11 erros e schemas de request/response
- [ ] README do módulo sincronizado com versão, ondas e status dos artefatos
- [ ] RISK-PM-05 resolvido; VAL-PARTNER-01 em `approvals.yaml`
- [ ] Commit realizado; push da branch

---

## 5. Matriz de Rastreabilidade

| Origem | Descrição | TASKs | Status |
|--------|-----------|-------|--------|
| Req 1 | Cadastrar parceiro (nome, papel, percentuais, ativo) | TASK-03, TASK-05, TASK-09, TASK-22, TASK-23 | [X] |
| Req 2 | Editar parceiro (sem alterar comissão consolidada) | TASK-05, TASK-09, TASK-22, TASK-23 | [X] |
| Req 3 | Inativar e reativar parceiro (soft-delete idempotente) | TASK-07, TASK-10, TASK-24 | [X] |
| Req 4 | Listar parceiros para seleção (ativos, paginado) | TASK-11, TASK-23 | [X] |
| Req 5 | Papel tipado canônico (`partner_type`) | TASK-03, TASK-07, TASK-20 | [X] |
| Req 6 | Percentuais padrão de comissão por componente | TASK-03, TASK-05, TASK-08, TASK-16 | [X] |
| Req 7 | Gerir contato do parceiro (e-mail, telefone) | TASK-04, TASK-05, TASK-09 | [X] |
| Req 8 | Bloquear vinculação de parceiro inativo | TASK-07, TASK-11, TASK-24 | [X] |
| Req 9 | Visão de comissão projetada × consolidada | TASK-12, TASK-14, TASK-19, TASK-24 | [X] |
| Req 10 | Relatório de comissões por parceiro por período | TASK-12, TASK-19, TASK-24, TASK-25 | [X] |
| Req 11 | Marcar percentuais pendentes pós-importação | TASK-07, TASK-09, TASK-11 | [X] |
| Req 12 | Parceiro sem credencial de acesso no MVP | TASK-05 (sem identity), TASK-29 (DoD) | [X] |
| RNF 1 | RBAC e isolamento por tenant | TASK-13, TASK-15, TASK-16, TASK-17, TASK-21, TASK-25 | [X] |
| RNF 2 | Auditoria append-only de toda escrita | TASK-06, TASK-18 | [X] |
| RNF 3 | Retenção indefinida de auditoria | TASK-18, TASK-29 | [X] |
| RNF 4 | Tratamento de PII no cadastro (LGPD) | TASK-04, TASK-06, TASK-20, TASK-27 | [X] |
| RNF 5 | Observabilidade da gestão de parceiros | TASK-13, TASK-26 | [X] |
| RNF 6 | Integridade dos percentuais e valores monetários | TASK-03, TASK-08, TASK-16 | [X] |
| RNF 7 | Desempenho da listagem e da visão de comissão | TASK-16 (índices), TASK-19, TASK-28 | [X] |
| PBT-01 | Conservação da soma de comissão por parceiro | TASK-14 | [X] |
| PBT-02 | Idempotência da inativação/reativação | TASK-07, TASK-08, TASK-10 | [X] |
| PBT-03 | Round-trip e domínio dos percentuais | TASK-03, TASK-08 | [X] |
| PBT-04 | Isolamento por tenant (anti-enumeração) | TASK-21 | [X] |
| PBT-05 | Imutabilidade da comissão consolidada | TASK-14 | [X] |
| DD-001 | Isolamento multi-tenant em profundidade (RLS) | TASK-15, TASK-16, TASK-17, TASK-21 | [X] |
| DD-002 | Parceiro sem credencial no MVP | TASK-05, TASK-29 | [X] |
| DD-003 | Visão de comissão via read model; sem recálculo | TASK-12, TASK-19, TASK-24 | [X] |
| DD-004 | Percentuais como `decimal` NUMERIC(5,2); comissão em centavos no pipeline | TASK-03, TASK-08, TASK-16 | [X] |
| DD-005 | Papel tipado validado contra lista canônica configurável | TASK-07, TASK-20 | [X] |
| DD-006 | Inativação/reativação soft-delete idempotente | TASK-07, TASK-10 | [X] |
| DD-007 | Bloqueio de vínculo de inativo enforced no pipeline | TASK-11, TASK-24 | [X] |
| DD-008 | `partner.name`/contato como possível PII | TASK-04, TASK-06, TASK-20, TASK-27 | [X] |
| ADR-0001 | Isolamento multi-tenant defesa em profundidade | TASK-15, TASK-16, TASK-17, TASK-21 | [X] |

---

## 6. Coverage Gates

| Camada | Gate | Tipo de teste |
|--------|------|---------------|
| Domain | ≥ 95% | Unitários + PBT-02 + PBT-03 (invariantes de VO, agregado, state machine) |
| Application | ≥ 85% | Unitários de handlers, validators, behaviors; PBT-01, PBT-05 |
| Infrastructure | ≥ 70% | Integração com PostgreSQL real (Testcontainers); PBT-04 gate CI |
| Api | ≥ 80% | Contrato (Pact), integração (WebApplicationFactory), RBAC, catálogo de erros |
| Architecture | 100% das regras | NetArchTest — grafo de dependência Clean Architecture |
| Security | Por cenário crítico | RBAC, anti-enumeração, PII scan (gate CI), PBT-04 (gate CI) |
| Observability | Por fluxo crítico | Logs com `correlation_id`; métricas incrementando; spans criados |

Regras adicionais:
- PBT-04 (isolamento de tenant) é gate CI bloqueante independentemente de percentual de coverage.
- Scan anti-PII é gate CI bloqueante (categoria `PiiScan`).
- Testes de contrato Pact são obrigatórios para endpoints consumidos pelo `opportunity-pipeline`.

---

## 7. Critérios de Encerramento

### Encerramento de TASK

Uma TASK só pode ser marcada como `[X]` quando:

- todas as subtasks ST estiverem concluídas
- testes aplicáveis verdes localmente
- coverage gate da camada atendido ou justificativa registrada
- lint/format executado
- nenhum warning novo relevante introduzido
- commit em Conventional Commits realizado
- push da branch realizado
- documentação atualizada quando aplicável

### Encerramento de Onda

Uma onda só pode ser considerada concluída quando:

- todas as TASKs da onda estiverem `[X]`
- CI verde (build + test + gates)
- PR da onda aberto, aprovado ou mergeado conforme regra do projeto
- coverage gate da camada principal da onda atendido
- riscos da onda tratados ou registrados

### Encerramento do Módulo

O módulo está pronto quando:

- [ ] 5 projetos Clean Architecture criados; `Architecture.Tests` verde
- [ ] Agregado `Partner` com objetos de valor e invariantes I1..I5 implementados
- [ ] Commands/Queries/Handlers e behaviors implementados
- [ ] Schema migrado com CHECKs, índices e RLS habilitada (ADR-0001)
- [ ] Endpoints `/api/v1/partners` (CRUD, deactivate/reactivate, eligibility, commissions) funcionando
- [ ] Eventos `partner.*.v1` publicados via Outbox + Pub/Sub sem PII
- [ ] `IPartnerCommissionReadPort` integrado com resiliência; visão sem recálculo
- [ ] `PartnerPiiMasker` aplicado; scan anti-PII verde como gate CI
- [ ] RBAC validado; listagem a Viewer+; escrita e relatório a Tenant Admin/Gestor de BU
- [ ] Métricas `partners_created_total` / `partners_deactivated_total` expostas; logs com `correlation_id`/`tenant_id`/`partner_id` sem PII; traces
- [ ] PBT-04 (isolamento de tenant) verde como gate CI (KPI-06)
- [ ] PBT-01..PBT-05 implementados e verdes
- [ ] Idempotência de inativação/reativação validada (PBT-02)
- [ ] `requirements.md`, `design.md` e `tasks.md` consistentes
- [ ] VAL-PARTNER-01 e RISK-PM-05 registrados em `approvals.yaml`/README

---

## 8. Riscos de Execução

| Risco | Probabilidade | Impacto | Mitigação |
|-------|---------------|---------|-----------|
| RISK-PM-08: vazamento cross-tenant por bug no filtro global EF ou RLS | Baixa | Sev-1 | PBT-04 como gate CI bloqueante (TASK-21); RLS como segunda camada (TASK-16) |
| RISK-PM-02: PII de `partner.name`/contato vazado em log/trace | Média | Alto | `PartnerPiiMasker` aplicado (TASK-20, TASK-27); scan anti-PII gate CI (TASK-27) |
| RISK-PM-03: classificação LGPD de `partner.name` não confirmada | Alta | Médio | Tratamento conservador no MVP (DD-008); VAL-PARTNER-01 em `approvals.yaml` (TASK-29) |
| RISK-PM-06: indisponibilidade do pipeline degrada visão de comissão | Média | Médio | Circuit breaker + degradação parcial implementados (TASK-28) |
| RISK-PM-07: edição de percentuais altera comissão consolidada | Baixa | Alto | PBT-05 valida imutabilidade do snapshot (TASK-14); snapshot pertence ao pipeline (DD-003) |
| Complexidade de Testcontainers para PBT-04 com banco real | Média | Médio | Isolar configuração de Testcontainers em fixture compartilhada desde TASK-17; reutilizar em TASK-21 |

---

## 9. Referências

| Referência | Origem |
|------------|--------|
| requirements.md v1.0.0 | docs/product/modules/partner-management/requirements.md |
| design.md v0.1.0 | docs/product/modules/partner-management/design.md |
| ADR-0001 — Isolamento multi-tenant | docs/product/adr/0001-isolamento-multi-tenant-defesa-em-profundidade.md |
| ADR-0002 — Snapshot imutável de comissão | docs/product/adr/ (pertence ao opportunity-pipeline) |
| ADR-0003 — Retenção de logs/auditoria | docs/product/adr/ |
| Clean Architecture | `.forge/rules/architecture/clean-architecture.md` |
| DDD tático | `.forge/rules/architecture/ddd.md` |
| APIs e contratos | `.forge/rules/architecture/api-and-contracts.md` |
| Observabilidade | `.forge/rules/architecture/observability.md` |
| Segurança e conformidade (LGPD) | `.forge/rules/architecture/security-and-compliance.md` |
| Imutabilidade de auditoria | `.forge/rules/domain/audit-immutability.md` |
| Valor monetário em centavos | `.forge/rules/domain/money-as-cents.md` |
| Arredondamento NBR 5891 ToEven | `.forge/rules/domain/nbr-5891-rounding.md` |
| Nomenclatura de banco | `.forge/rules/conventions/database-naming.md` |
| Permissões JWT (RBAC) | `.forge/rules/architecture/jwt-permissions.md` |
| mTLS interno | `.forge/rules/architecture/mtls-internal-services.md` |

---

**Observação sobre README do módulo:** `docs/product/modules/partner-management/README.md` existe e deve ser atualizado na TASK-29 com: status `tasks.md v0.1.0 — Rascunho para revisão`, 6 ondas, 29 TASKs, e tabela de status dos três artefatos (requirements.md v1.0.0 / design.md v0.1.0 / tasks.md v0.1.0).
