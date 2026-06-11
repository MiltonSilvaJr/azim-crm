# Tasks — AUD — Audit Log

- Versão: 0.1.0
- Data: 2026-06-11
- Status: Rascunho para revisão
- Referência base requirements: docs/product/modules/audit-log/requirements.md v0.1.0
- Referência base design: docs/product/modules/audit-log/design.md v0.1.0
- ADRs aplicáveis: ADR-0003 (política de retenção, a definir), ADR-0007 (stack GCP, a formalizar)
- Rules aplicáveis: `.forge/rules/domain/audit-immutability.md`, `.forge/rules/domain/money-as-cents.md`, `.forge/rules/architecture/clean-architecture.md`, `.forge/rules/architecture/security-and-compliance.md`, `.forge/rules/architecture/observability.md`, `.forge/rules/architecture/ddd.md`, `.forge/rules/conventions/database-naming.md`, `.forge/rules/conventions/conventional-commits.md`, `.forge/rules/testing/tdd.md`, `.forge/rules/testing/quality-gates.md`

## Histórico de Versões

| Versão | Data | Status | Descrição da alteração |
|--------|------|--------|------------------------|
| 0.1.0 | 2026-06-11 | Rascunho para revisão | Criação inicial do plano de tasks a partir do requirements.md v0.1.0 e design.md v0.1.0; 20 TASKs em 6 ondas, 6 PBTs cobertos. |

---

## 1. Convenções de Implementação

### 1.1 TDD-first

Toda implementação com lógica verificável segue o ciclo:

1. Red — escrever teste que falha
2. Green — implementar o mínimo para passar
3. Refactor — melhorar sem alterar comportamento

Nenhuma implementação de regra de domínio, handler, endpoint, persistência, contrato ou integração é considerada concluída sem teste correspondente.

### 1.2 Property-Based Testing

PBT é obrigatório para os seguintes casos do módulo:

| PBT | Propriedade | Camada |
|-----|-------------|--------|
| PBT-01 | Append-only: qualquer UPDATE/DELETE/TRUNCATE pelo role `app` é rejeitado | Infrastructure.Tests (Testcontainers) |
| PBT-02 | Conservação: N escritas → N registros com `user_id` não vazio e `delta_json` coerente | Application.Tests + Infrastructure.Tests |
| PBT-03 | Round-trip do delta: aplicar "depois" sobre "antes" reproduz estado posterior | Domain.Tests |
| PBT-04 | Mascaramento: nenhum valor PII original em texto claro no `delta_json` | Domain.Tests |
| PBT-05 | Isolamento por tenant: consulta no contexto de `tenant_id` X retorna só registros de X | Infrastructure.Tests (RLS) |
| PBT-06 | Idempotência de leitura: N consultas não alteram conjunto/ordem/conteúdo persistido | Infrastructure.Tests / Api.Tests |

Geradores usam FsCheck (ou equivalente da stack .NET) com `Arbitrary` para entidades arbitrárias, sequências de operações e combinações de filtros.

### 1.3 Bite-sized Tasks

Cada subtask deve ser estimada para menos de 2 horas. Cada TASK deve ser completa em até 1 dia; excepcionalmente 2 dias com escopo claro e entregável verificável.

### 1.4 Branch Model

```text
<tipo>/audit-log/<NN>-<slug>
```

Exemplos:
```text
feat/audit-log/01-bootstrap-solution
test/audit-log/05-pbt-pii-masker
feat/audit-log/12-migration-append-only
```

### 1.5 Git Worktree

```sh
git worktree add .forge/worktrees/audit-log/<NN>-<slug> -b <branch>
```

### 1.6 Encerramento de TASK

Cada TASK encerra com:

- testes locais verdes
- coverage gate da camada atendido ou justificativa registrada
- lint/format executado
- documentação atualizada quando aplicável
- commit em Conventional Commits (`feat:`, `test:`, `fix:`, `chore:`)
- push da branch

### 1.7 Encerramento de Onda

- todas as TASKs da onda com status `[X]`
- CI verde
- conflitos resolvidos
- PR aberto ou atualizado (1 PR por onda)
- checklist de revisão preenchido

### 1.8 Early Exit

Se uma subtask falhar:

- marcar status como `[-]`
- registrar ponto de falha, comando executado e erro principal
- não mascarar falha com implementação especulativa
- deixar contexto suficiente para retomada

### 1.9 Convenção de Status

- `[ ]` Não iniciado
- `[-]` Em progresso
- `[X]` Concluído
- `[!]` Falhou — exige intervenção humana

### 1.10 IDs canônicos

```
TASK-NN — <título>          ← unidade atômica
  ST-MM — <subtask>         ← etapas TDD internas (numeração reinicia por TASK)
```

Onda é atributo de agrupamento visual, nunca entra no ID da TASK.

### 1.11 Convenções específicas do módulo

- Valores monetários no `delta_json`: inteiros em centavos (`long`/`BIGINT`); `float`/`double`/`decimal` proibidos (DD-005, `.forge/rules/domain/money-as-cents.md`).
- PII (nome, e-mail, celular de Contact) nunca em texto claro em logs, traces ou `delta_json` persistido.
- Escrita em `audit_logs` exclusivamente via `IAuditWriter` → `AuditService`; nenhum módulo escreve diretamente.
- `tenant_id` nunca aceito do chamador: sempre derivado do contexto autenticado.
- `created_at` definido pelo servidor (`IClock`); nunca fornecido pelo chamador.

---

## 2. Status Geral

| TASK | Título | Onda | Branch | Status |
|------|--------|------|--------|--------|
| TASK-01 | Bootstrap solution e 5 projetos Clean Architecture | Onda 1 | `feat/audit-log/01-bootstrap-solution` | [ ] |
| TASK-02 | Contratos públicos em AuditLog.Contracts | Onda 1 | `feat/audit-log/02-contracts` | [ ] |
| TASK-03 | Objetos de valor do domínio | Onda 2 | `feat/audit-log/03-domain-value-objects` | [ ] |
| TASK-04 | Aggregate root AuditLog e IAuditLogRepository | Onda 2 | `feat/audit-log/04-domain-aggregate` | [ ] |
| TASK-05 | PiiMasker + PiiFieldPolicy + PBT-04 | Onda 2 | `feat/audit-log/05-pii-masker` | [ ] |
| TASK-06 | PBT-03 Round-trip do delta | Onda 2 | `test/audit-log/06-pbt-round-trip` | [ ] |
| TASK-07 | RecordAuditEntryCommand + AuditService handler | Onda 3 | `feat/audit-log/07-record-command` | [ ] |
| TASK-08 | Queries de consulta + handlers + validators | Onda 3 | `feat/audit-log/08-query-handlers` | [ ] |
| TASK-09 | Pipeline behaviors | Onda 3 | `feat/audit-log/09-behaviors` | [ ] |
| TASK-10 | PBT-02 Conservação (Application.Tests) | Onda 3 | `test/audit-log/10-pbt-conservation` | [ ] |
| TASK-11 | EF Core DbContext + AuditLogRepository | Onda 4 | `feat/audit-log/11-ef-repository` | [ ] |
| TASK-12 | Migration append-only: trigger + REVOKE + RLS + índices | Onda 4 | `feat/audit-log/12-migration-append-only` | [ ] |
| TASK-13 | PBT-01 Imutabilidade + PBT-05 Isolamento (Testcontainers) | Onda 4 | `test/audit-log/13-pbt-immutability-isolation` | [ ] |
| TASK-14 | Integração fail-closed (DD-001) + PBT-06 idempotência de leitura | Onda 4 | `test/audit-log/14-fail-closed-pbt06` | [ ] |
| TASK-15 | AuditLogQueryController (endpoints GET) | Onda 5 | `feat/audit-log/15-api-controller` | [ ] |
| TASK-16 | Catálogo de erros (AUD-ERR-001..008) + OpenAPI | Onda 5 | `feat/audit-log/16-error-catalog-openapi` | [ ] |
| TASK-17 | RBAC + escopo de BU (DD-008) + testes de contrato | Onda 5 | `feat/audit-log/17-rbac-bu-scope` | [ ] |
| TASK-18 | Métricas, alertas e health check | Onda 6 | `feat/audit-log/18-observability` | [ ] |
| TASK-19 | PII scan em CI + testes de segurança | Onda 6 | `feat/audit-log/19-security-pii-scan` | [ ] |
| TASK-20 | DoD final e sincronização de documentação | Onda 6 | `chore/audit-log/20-dod-docs` | [ ] |

---

## 3. Ondas de Implementação

| Onda | Foco | TASKs | Critério de fechamento |
|------|------|-------|------------------------|
| Onda 1 | Bootstrap | TASK-01, TASK-02 | Solution buildável; regra de dependência verde; contratos públicos compilando |
| Onda 2 | Domínio | TASK-03, TASK-04, TASK-05, TASK-06 | Domain.Tests ≥ 95%; PBT-03 e PBT-04 verdes; nenhum objeto de valor mutável |
| Onda 3 | Application | TASK-07, TASK-08, TASK-09, TASK-10 | Application.Tests ≥ 85%; PBT-02 verde; behaviors registrados no container |
| Onda 4 | Infrastructure | TASK-11, TASK-12, TASK-13, TASK-14 | Infrastructure.Tests ≥ 70% com Testcontainers; PBT-01, PBT-05, PBT-06 verdes; migration aplicada |
| Onda 5 | API + Contracts | TASK-15, TASK-16, TASK-17 | Api.Tests ≥ 80%; catálogo de erros completo; RBAC e escopo de BU validados |
| Onda 6 | Hardening | TASK-18, TASK-19, TASK-20 | Métricas ativas; PII scan no CI verde; DoD do design.md § 19 completo; PR final aprovado |

**Risco principal por onda:**
- Onda 2: complexidade do mascaramento de PII por `entity_type` (RISK-AUDIT-02)
- Onda 4: configuração de Testcontainers + trigger PostgreSQL (RISK-AUDIT-01)
- Onda 5: resolução de escopo de BU via read model externo (RISK-AUDIT-05)
- Onda 6: integração do scan de PII com o pipeline CI existente

---

## 4. Tarefas

### TASK-01 — Bootstrap solution e 5 projetos Clean Architecture

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 1 — Bootstrap |
| **Branch** | `feat/audit-log/01-bootstrap-solution` |
| **Worktree** | `git worktree add .forge/worktrees/audit-log/01-bootstrap-solution -b feat/audit-log/01-bootstrap-solution` |
| **Status** | [ ] |
| **Depende de** | Não aplicável |
| **Entregável** | Solution `AuditLog.sln` com 10 projetos (.NET) criada, compilando e com regra de dependência validada por `AuditLog.Architecture.Tests` |
| **Mapeia** | design.md § 3 (estrutura de solução); `.forge/rules/architecture/clean-architecture.md` |
| **Camada principal** | DevOps / Architecture |

#### Objetivo

Criar a estrutura de solução Clean Architecture com 5 projetos de produção (`AuditLog.Domain`, `AuditLog.Application`, `AuditLog.Infrastructure`, `AuditLog.Api`, `AuditLog.Contracts`) e 5 de teste (`*.Domain.Tests`, `*.Application.Tests`, `*.Infrastructure.Tests`, `*.Api.Tests`, `*.Architecture.Tests`), configurar referências entre projetos respeitando a regra de dependência, e validar via NetArchTest que nenhuma camada viola a hierarquia.

#### Subtasks

- [ ] **ST-01 — Red:** escrever `AuditLog.Architecture.Tests` com testes NetArchTest que falham (regra de dependência: Domain sem referência a infraestrutura, Application sem referência a EF Core, etc.)
- [ ] **ST-02 — Green:** criar solution + 10 projetos; configurar `<ProjectReference>` conforme grafo do design.md § 3; instalar pacotes mínimos (MediatR, FluentValidation, EF Core, NetArchTest)
- [ ] **ST-03 — Refactor:** garantir que `AuditLog.Architecture.Tests` passe; revisar `global.json` e `Directory.Build.props` se necessário
- [ ] **ST-04 — Encerramento:** `dotnet build` verde, testes de arquitetura verdes; `feat: bootstrap AuditLog solution com 5 projetos Clean Architecture`; push

#### Critérios de Aceite

- [ ] `dotnet build AuditLog.sln` sem erros
- [ ] `AuditLog.Architecture.Tests` verdes (regra de dependência validada)
- [ ] Nenhuma referência proibida entre camadas
- [ ] Projetos de teste referenciando corretamente os projetos de produção

---

### TASK-02 — Contratos públicos em AuditLog.Contracts

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 1 — Bootstrap |
| **Branch** | `feat/audit-log/02-contracts` |
| **Worktree** | `git worktree add .forge/worktrees/audit-log/02-contracts -b feat/audit-log/02-contracts` |
| **Status** | [ ] |
| **Depende de** | TASK-01 |
| **Entregável** | `IAuditWriter`, `AuditEntryRequest` e `AuditAction` implementados em `AuditLog.Contracts`; testes de contrato de compilação e estabilidade |
| **Mapeia** | REQ-006, DD-006, design.md § 8.3 |
| **Camada principal** | Contracts |

#### Objetivo

Publicar a porta pública `IAuditWriter` (com `RecordAsync`) e os tipos associados (`AuditEntryRequest`, `AuditAction`) em `AuditLog.Contracts`, que é o único ponto de integração entre os módulos de escrita e o audit-log. O contrato deve ser estável e versionado.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de contrato verificando: `IAuditWriter` tem exatamente `RecordAsync(AuditEntryRequest, CancellationToken)`; `AuditEntryRequest` é imutável (`sealed record`); `AuditAction` tem apenas `Create`, `Update`, `Delete`; `RawBefore`/`RawAfter` são `IReadOnlyDictionary<string, object?>`
- [ ] **ST-02 — Green:** implementar `IAuditWriter`, `AuditEntryRequest` (record), `AuditAction` (enum) em `AuditLog.Contracts`; `TenantId` e `correlation_id` explicitamente fora do record (derivados de contexto)
- [ ] **ST-03 — Refactor:** adicionar XML docs; verificar imutabilidade dos records; confirmar que `AuditLog.Contracts` não referencia nenhum projeto interno
- [ ] **ST-04 — Encerramento:** testes de contrato verdes; `feat: adicionar IAuditWriter e AuditEntryRequest em AuditLog.Contracts`; push

#### Critérios de Aceite

- [ ] Testes de contrato verdes
- [ ] `AuditLog.Contracts` sem dependência de projetos internos
- [ ] `AuditAction` serializa como `create`/`update`/`delete` (lowercase)
- [ ] `AuditEntryRequest` imutável (`sealed record`, sem setters)

---

### TASK-03 — Objetos de valor do domínio

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domínio |
| **Branch** | `feat/audit-log/03-domain-value-objects` |
| **Worktree** | `git worktree add .forge/worktrees/audit-log/03-domain-value-objects -b feat/audit-log/03-domain-value-objects` |
| **Status** | [ ] |
| **Depende de** | TASK-01 |
| **Entregável** | Seis objetos de valor implementados em `AuditLog.Domain`: `AuditLogId`, `TenantId`, `ActorId`, `EntityReference`, `AuditAction` (espelho do Contracts), `AuditDelta`; todos imutáveis e com igualdade por valor |
| **Mapeia** | REQ-002, REQ-003, DD-005; design.md § 4.3 |
| **Camada principal** | Domain |

#### Objetivo

Implementar os objetos de valor que compõem o aggregate `AuditLog`, garantindo imutabilidade, igualdade por valor, e que `AuditDelta` represente corretamente as três estruturas (create/update/delete) com valores monetários em centavos inteiros.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes unitários de: igualdade por valor em todos os VOs; rejeição de valores nulos/inválidos; `AuditDelta` estrutura `create` sem "before", `update` com pares before/after, `delete` sem "after"; money em centavos (`long`, sem `double`/`decimal`)
- [ ] **ST-02 — Green:** implementar os 6 objetos de valor conforme design.md § 4.3; `AuditDelta` como `sealed record` com factory methods `ForCreate`, `ForUpdate`, `ForDelete`
- [ ] **ST-03 — Refactor:** garantir que DD-005 está enforçado (proibir campos `double`/`float` em `AuditDelta`); consolidar lógica de validação em guards
- [ ] **ST-04 — Encerramento:** Domain.Tests verdes; `feat: implementar objetos de valor do domínio audit-log`; push

#### Critérios de Aceite

- [ ] Todos os VOs imutáveis (sem setters públicos)
- [ ] Igualdade por valor implementada corretamente
- [ ] `AuditDelta.ForUpdate` preserva apenas atributos efetivamente alterados (REQ-003.4)
- [ ] Nenhum campo `float`/`double` em `AuditDelta` (DD-005)
- [ ] Coverage Domain ≥ 95% acumulado

---

### TASK-04 — Aggregate root AuditLog e IAuditLogRepository

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domínio |
| **Branch** | `feat/audit-log/04-domain-aggregate` |
| **Worktree** | `git worktree add .forge/worktrees/audit-log/04-domain-aggregate -b feat/audit-log/04-domain-aggregate` |
| **Status** | [ ] |
| **Depende de** | TASK-03 |
| **Entregável** | Aggregate `AuditLog` com factory `Create` e `Reconstitute`; interface `IAuditLogRepository` com apenas `Add` + métodos de leitura; invariantes documentadas e testadas |
| **Mapeia** | REQ-001, REQ-002, RNF-001; design.md § 4.1, § 4.2 |
| **Camada principal** | Domain |

#### Objetivo

Implementar o único aggregate root do módulo. `AuditLog` é criado via factory (com `created_at` sempre derivado do clock do servidor) e nunca modificado após persistência. A interface do repositório expõe apenas operações compatíveis com append-only.

#### Subtasks

- [ ] **ST-01 — Red:** testes de: `AuditLog.Create` com campos obrigatórios (rejeita nulos); `created_at` definido pelo `IClock` do servidor, nunca pelo chamador; ausência de métodos de mutação públicos; `IAuditLogRepository` com assinatura apenas `Add`/leitura
- [ ] **ST-02 — Green:** implementar `AuditLog` (`sealed class`, sem setters públicos); factory `Create(tenantId, actorId, entityRef, action, maskedDelta, clock)`; `Reconstitute` para leitura; `IAuditLogRepository` em Domain
- [ ] **ST-03 — Refactor:** verificar `AppendOnlyInvariant` (ausência de qualquer método de mutação); remover qualquer `updated_at` (REQ-002.5)
- [ ] **ST-04 — Encerramento:** Domain.Tests verdes; coverage ≥ 95%; `feat: implementar aggregate AuditLog e IAuditLogRepository`; push

#### Critérios de Aceite

- [ ] `AuditLog` sem `Update`/`Delete`/setter público
- [ ] `created_at` nunca aceito como parâmetro externo
- [ ] `IAuditLogRepository` sem `Update`/`Remove`
- [ ] Testes de invariante verdes

---

### TASK-05 — PiiMasker + PiiFieldPolicy + PBT-04

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domínio |
| **Branch** | `feat/audit-log/05-pii-masker` |
| **Worktree** | `git worktree add .forge/worktrees/audit-log/05-pii-masker -b feat/audit-log/05-pii-masker` |
| **Status** | [ ] |
| **Depende de** | TASK-03 |
| **Entregável** | `PiiMasker` (serviço de domínio) + `PiiFieldPolicy` com política para `Contact` (name, email, phone); PBT-04 verde |
| **Mapeia** | REQ-004, RNF-002, DD-004, PBT-04; design.md § 4.6 |
| **Camada principal** | Domain |

#### Objetivo

Implementar o `PiiMasker` como serviço de domínio puro (sem dependência de infraestrutura). Para cada `entity_type`, a `PiiFieldPolicy` define quais campos são PII. O mascaramento substitui o valor por `[MASKED]`, preservando a chave do campo no delta (REQ-004.4). PBT-04 valida que nenhum valor PII arbitrário vaza em texto claro.

#### Subtasks

- [ ] **ST-01 — Red (PBT-04):** escrever PBT com FsCheck gerando entidades `Contact` com `name`, `email` e `phone` arbitrários; verificar que `delta_json` resultante não contém nenhum valor original em texto claro; escrever testes unitários de: mascaramento por `entity_type`; preservação da chave do campo; extensibilidade (nova `entity_type` sem alterar `PiiMasker`)
- [ ] **ST-02 — Green:** implementar `PiiFieldPolicy` (mapa `entity_type → campos PII`); `PiiMasker.Mask(delta, entityType, policy) → AuditDelta`; política inicial para `Contact: [name, email, phone]`; marcador `[MASKED]` como constante
- [ ] **ST-03 — Refactor:** extrair a constante `PiiMasker.MaskedMarker`; garantir que `PiiMasker` não tem dependência de EF Core/infraestrutura
- [ ] **ST-04 — Encerramento:** PBT-04 verde; Domain.Tests verdes; coverage ≥ 95%; `feat: implementar PiiMasker e PiiFieldPolicy com PBT-04`; push

#### Critérios de Aceite

- [ ] PBT-04 verde com pelo menos 100 amostras geradas
- [ ] `PiiMasker` sem dependência de infraestrutura
- [ ] Mascaramento aplica-se a todos os campos PII da política
- [ ] Chave do campo PII preservada no delta após mascaramento (REQ-004.4)
- [ ] Política extensível sem alterar `PiiMasker`

---

### TASK-06 — PBT-03 Round-trip do delta

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domínio |
| **Branch** | `test/audit-log/06-pbt-round-trip` |
| **Worktree** | `git worktree add .forge/worktrees/audit-log/06-pbt-round-trip -b test/audit-log/06-pbt-round-trip` |
| **Status** | [ ] |
| **Depende de** | TASK-03, TASK-04 |
| **Entregável** | PBT-03 verde em `AuditLog.Domain.Tests`; `AuditDelta` validado quanto à propriedade de round-trip para os três tipos de operação |
| **Mapeia** | REQ-003, PBT-03; design.md § 4.3, § 13.1 |
| **Camada principal** | Domain / Tests |

#### Objetivo

Validar via PBT que a estrutura de `AuditDelta` possui a propriedade de round-trip: para `create`, o delta reconstrói o estado inicial; para `update`, aplicar "depois" sobre "antes" reproduz o estado posterior nos atributos alterados; para `delete`, o delta reconstrói o último estado. Valores monetários são em centavos inteiros.

#### Subtasks

- [ ] **ST-01 — Red (PBT-03):** escrever três PBTs com FsCheck (create/update/delete); geradores de pares `(stateBefore, stateAfter)` com campos arbitrários incluindo campos monetários em centavos; verificar propriedade de round-trip
- [ ] **ST-02 — Green:** ajustar `AuditDelta` se necessário para garantir a propriedade; garantir serialização/deserialização idempotente
- [ ] **ST-03 — Refactor:** remover código especulativo; verificar que `long` é usado para campos monetários (DD-005)
- [ ] **ST-04 — Encerramento:** PBT-03 verde; `test: adicionar PBT-03 round-trip do delta em AuditLog.Domain.Tests`; push

#### Critérios de Aceite

- [ ] PBT-03 verde para create, update e delete com pelo menos 100 amostras cada
- [ ] Campos monetários representados como `long` (centavos inteiros) nos geradores e no `AuditDelta`
- [ ] Nenhuma perda de dados na serialização do delta

---

### TASK-07 — RecordAuditEntryCommand + AuditService handler

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/audit-log/07-record-command` |
| **Worktree** | `git worktree add .forge/worktrees/audit-log/07-record-command -b feat/audit-log/07-record-command` |
| **Status** | [ ] |
| **Depende de** | TASK-04, TASK-05 |
| **Entregável** | `RecordAuditEntryCommand`, `RecordAuditEntryCommandValidator` e `AuditService` (handler) implementados; `IAuditWriter` implementado apontando para o handler |
| **Mapeia** | REQ-001, REQ-004, REQ-006, DD-001; design.md § 5.1, § 5.3 |
| **Camada principal** | Application |

#### Objetivo

Implementar o único command de escrita do módulo. O `AuditService` recebe `RecordAuditEntryCommand`, aplica `PiiMasker`, constrói o aggregate `AuditLog` com `created_at` via `IClock` do servidor e persiste via `IAuditLogRepository` na transação corrente (DD-001). O `TenantId` vem do contexto autenticado, nunca do chamador.

#### Subtasks

- [ ] **ST-01 — Red:** testes de handler com mock de `IAuditLogRepository` e `IClock`; verificar: mascaramento aplicado antes de persistir; `created_at` não aceito do chamador; `TenantId` do contexto; `user_id` não vazio rejeita o command; `action` fora do enum rejeita
- [ ] **ST-02 — Green:** implementar `RecordAuditEntryCommand`; `RecordAuditEntryCommandValidator` (FluentValidation); `AuditService : IRequestHandler<RecordAuditEntryCommand>` com sequência: mascara PII → constrói `AuditLog` → `Add` no repositório; implementar `AuditServiceWriter : IAuditWriter` que dispara o command via MediatR
- [ ] **ST-03 — Refactor:** garantir que `AuditService` não contém lógica de negócio do CRM; isolar chamada ao `PiiMasker` em método privado
- [ ] **ST-04 — Encerramento:** Application.Tests verdes; `feat: implementar RecordAuditEntryCommand e AuditService handler`; push

#### Critérios de Aceite

- [ ] `AuditService` aplica mascaramento antes de qualquer persistência (REQ-004)
- [ ] `created_at` definido pelo `IClock`, nunca pelo chamador (REQ-002.4)
- [ ] Validator rejeita `user_id` vazio, `entity_type` vazio ou > 50 chars, `action` inválida
- [ ] `TenantId` derivado do contexto (não parâmetro do command para o chamador externo)

---

### TASK-08 — Queries de consulta + handlers + validators

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/audit-log/08-query-handlers` |
| **Worktree** | `git worktree add .forge/worktrees/audit-log/08-query-handlers -b feat/audit-log/08-query-handlers` |
| **Status** | [ ] |
| **Depende de** | TASK-07 |
| **Entregável** | `ListAuditLogsQuery` + `ListAuditLogsQueryValidator` + `ListAuditLogsHandler`; `GetEntityAuditHistoryQuery` + `GetEntityAuditHistoryHandler`; ambas paginadas, filtradas e ordenadas |
| **Mapeia** | REQ-007, DD-008; design.md § 5.2, § 5.3, § 5.5 |
| **Camada principal** | Application |

#### Objetivo

Implementar as duas queries de leitura (sem efeito colateral) com filtros, paginação e ordenação por `created_at` desc. `ListAuditLogsHandler` aplica filtro adicional de BU para `GestorBU` (DD-008). O `tenant_id` nunca é parâmetro do chamador: vem do contexto e é aplicado por RLS.

#### Subtasks

- [ ] **ST-01 — Red:** testes de: `ListAuditLogsHandler` retorna página correta com filtros; `pageSize` máximo 200; filtro de período rejeita `from > to`; `GetEntityAuditHistoryHandler` retorna histórico de entidade; nenhum handler altera estado da trilha
- [ ] **ST-02 — Green:** implementar `ListAuditLogsQuery` (campos: `entityType?`, `entityId?`, `userId?`, `from?`, `to?`, `page`, `pageSize`); `ListAuditLogsQueryValidator`; `ListAuditLogsHandler` com filtro de BU para `GestorBU`; `GetEntityAuditHistoryQuery` + handler análogo
- [ ] **ST-03 — Refactor:** extrair mapeamento de filtros para método auxiliar; garantir `pageSize` default 50, máx 200
- [ ] **ST-04 — Encerramento:** Application.Tests verdes; `feat: implementar ListAuditLogsQuery e GetEntityAuditHistoryQuery com handlers`; push

#### Critérios de Aceite

- [ ] Filtros por `entity_type`, `entity_id`, `user_id` e período funcionando (REQ-007.2)
- [ ] `pageSize` default 50, máx 200 (AUD-ERR-001 para excedente)
- [ ] `from > to` retorna AUD-ERR-006 (400)
- [ ] Handlers não produzem efeito colateral na trilha (REQ-007.5)

---

### TASK-09 — Pipeline behaviors

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/audit-log/09-behaviors` |
| **Worktree** | `git worktree add .forge/worktrees/audit-log/09-behaviors -b feat/audit-log/09-behaviors` |
| **Status** | [ ] |
| **Depende de** | TASK-07, TASK-08 |
| **Entregável** | Quatro behaviors implementados e registrados no container DI: `ValidationBehavior`, `AuthorizationBehavior`, `TenantContextBehavior`, `LoggingBehavior` |
| **Mapeia** | REQ-005, REQ-008, RNF-002, DD-003, DD-007; design.md § 5.4 |
| **Camada principal** | Application |

#### Objetivo

Implementar os behaviors da pipeline MediatR garantindo que: validação sintática ocorre na borda; consultas sem `tenant_id` no contexto são bloqueadas com AUD-ERR-008 e alerta (DD-007); papéis sem permissão recebem 403 (AUD-ERR-002); logs nunca expõem PII (RNF-002).

#### Subtasks

- [ ] **ST-01 — Red:** testes de: `AuthorizationBehavior` bloqueia papel `Vendedor` com 403; `TenantContextBehavior` bloqueia consulta sem `tenant_id` no contexto retornando AUD-ERR-008 e incrementando contador; `LoggingBehavior` não loga PII (verificar campos de log gerados); `ValidationBehavior` rejeita comando inválido antes do handler
- [ ] **ST-02 — Green:** implementar os 4 behaviors; registrar no container DI na ordem correta (Validation → Tenant → Authorization → Logging → Handler); `TenantContextBehavior` incrementa `audit_query_without_tenant_context_total` (contador injetado)
- [ ] **ST-03 — Refactor:** garantir ordem correta na pipeline; verificar que `RecordAuditEntryCommand` **não** passa por `AuthorizationBehavior` de consulta (design.md § 5.4)
- [ ] **ST-04 — Encerramento:** Application.Tests verdes; `feat: implementar pipeline behaviors do módulo audit-log`; push

#### Critérios de Aceite

- [ ] `TenantContextBehavior` bloqueia consulta sem contexto de tenant e incrementa contador (DD-007)
- [ ] `AuthorizationBehavior` rejeita papéis não autorizados com 403
- [ ] `LoggingBehavior` registra apenas `correlation_id`, `tenant_id`, `entity_type`, `entity_id` (RNF-002.1)
- [ ] Behaviors registrados na ordem correta no container

---

### TASK-10 — PBT-02 Conservação (Application.Tests)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `test/audit-log/10-pbt-conservation` |
| **Worktree** | `git worktree add .forge/worktrees/audit-log/10-pbt-conservation -b test/audit-log/10-pbt-conservation` |
| **Status** | [ ] |
| **Depende de** | TASK-07 |
| **Entregável** | PBT-02 verde em `AuditLog.Application.Tests`; propriedade de conservação validada |
| **Mapeia** | REQ-001, REQ-002, PBT-02; design.md § 13.1 |
| **Camada principal** | Application / Tests |

#### Objetivo

Validar via PBT que para qualquer sequência de N operações de escrita enviadas ao `AuditService`, exatamente N registros são gerados, cada um com `user_id` não vazio e `delta_json` não vazio coerente com a operação (PBT-02). Usa mock de repositório em memória.

#### Subtasks

- [ ] **ST-01 — Red (PBT-02):** escrever PBT com FsCheck gerando sequências arbitrárias de `RecordAuditEntryCommand` (N ∈ [1..50]); repositório in-memory como spy; verificar `Count == N`, todos com `user_id` != null e `delta_json` != null
- [ ] **ST-02 — Green:** ajustar `AuditService` se necessário para garantir a conservação; garantir que nenhuma escrita é perdida ou duplicada
- [ ] **ST-03 — Refactor:** remover código especulativo; verificar que spy de repositório reseta entre execuções do gerador
- [ ] **ST-04 — Encerramento:** PBT-02 verde; Application.Tests ≥ 85%; `test: adicionar PBT-02 conservação em AuditLog.Application.Tests`; push

#### Critérios de Aceite

- [ ] PBT-02 verde com pelo menos 200 amostras geradas
- [ ] Application.Tests ≥ 85% de coverage
- [ ] Nenhum registro perdido ou duplicado nas sequências geradas

---

### TASK-11 — EF Core DbContext + AuditLogRepository

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/audit-log/11-ef-repository` |
| **Worktree** | `git worktree add .forge/worktrees/audit-log/11-ef-repository -b feat/audit-log/11-ef-repository` |
| **Status** | [ ] |
| **Depende de** | TASK-04 |
| **Entregável** | `AuditLogDbContext` + `AuditLogEntityConfiguration` + `AuditLogRepository` implementados; filtro global de `tenant_id` no DbContext; repositório expõe apenas `Add` e métodos de leitura |
| **Mapeia** | REQ-005, RNF-001, DD-001, DD-003; design.md § 6.1 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar a camada de persistência EF Core mapeando o aggregate `AuditLog` para a tabela `audit_logs`. O `DbContext` aplica filtro global de `tenant_id` (DD-003) e o repositório não expõe `Update`/`Remove`. O `created_at` é gerado pelo banco via `DEFAULT now()` e também pelo `IClock` do servidor.

#### Subtasks

- [ ] **ST-01 — Red:** testes de repositório com Testcontainers PostgreSQL (ou SQLite para unitários rápidos): `Add` persiste corretamente; métodos `Update`/`Remove` inexistentes; filtro global de `tenant_id` aplicado em toda consulta; `created_at` nunca nulo após INSERT
- [ ] **ST-02 — Green:** implementar `AuditLogDbContext` (com `HasQueryFilter` para `tenant_id`); `AuditLogEntityConfiguration` (mapeamento colunas, constraints de CHECK, `HasNoKey` não aplicável — PK existe); `AuditLogRepository : IAuditLogRepository` com `Add`, `FindByEntityAsync`, `ListAsync`
- [ ] **ST-03 — Refactor:** verificar que `DbContext.OnModelCreating` não expõe operação de remoção; garantir que `tenant_id` é lido do contexto de execução via `ITenantContext`
- [ ] **ST-04 — Encerramento:** testes de repositório verdes; `feat: implementar AuditLogDbContext e AuditLogRepository`; push

#### Critérios de Aceite

- [ ] `AuditLogRepository` sem métodos `Update`/`Remove`
- [ ] Filtro global de `tenant_id` aplicado em todas as consultas
- [ ] `created_at` `DEFAULT now()` configurado no mapeamento
- [ ] Nenhuma referência de EF Core no projeto `AuditLog.Domain`

---

### TASK-12 — Migration append-only: trigger + REVOKE + RLS + índices

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/audit-log/12-migration-append-only` |
| **Worktree** | `git worktree add .forge/worktrees/audit-log/12-migration-append-only -b feat/audit-log/12-migration-append-only` |
| **Status** | [ ] |
| **Depende de** | TASK-11 |
| **Entregável** | Migration `migration_NNNN_create_immutable_audit_logs.sql` aplicada e validada: tabela criada, constraints de CHECK, função `prevent_immutable_table_modification`, trigger, REVOKE, RLS, 3 índices de leitura |
| **Mapeia** | RNF-001, RNF-005, REQ-005, DD-002, DD-003; design.md § 7, § 7.1, § 7.2 |
| **Camada principal** | Infrastructure |

#### Objetivo

Criar a migration que torna `audit_logs` verdadeiramente append-only e multi-tenant-isolada no banco. Inclui: tabela com todas as colunas e constraints de CHECK; função e trigger de imutabilidade; REVOKE de UPDATE/DELETE/TRUNCATE do role `app`; RLS por `tenant_id`; 3 índices compostos de leitura.

#### Subtasks

- [ ] **ST-01 — Red:** teste de integração com Testcontainers (PostgreSQL real): verificar que tabela existe com todas as colunas; CHECK constraints validadas; trigger `trg_audit_logs_immutable` existe; tentativa de UPDATE retorna exceção; RLS ativo; índices presentes
- [ ] **ST-02 — Green:** escrever migration SQL com as 4 partes do design.md § 7.2 (função, trigger, REVOKE, RLS); adicionar DDL da tabela + constraints CHECK (`action IN ('create','update','delete')`, `char_length(entity_type) > 0`); criar 3 índices (`ix_audit_logs_tenant_entity`, `ix_audit_logs_tenant_created`, `ix_audit_logs_tenant_user`)
- [ ] **ST-03 — Refactor:** verificar nomes de objetos de banco conforme `.forge/rules/conventions/database-naming.md`; testar rollback da migration
- [ ] **ST-04 — Encerramento:** teste de integração verde; migration aplicável e reversível; `feat: migration create_immutable_audit_logs com trigger RLS e índices`; push

#### Critérios de Aceite

- [ ] Trigger rejeita UPDATE/DELETE/TRUNCATE (RNF-001.1)
- [ ] REVOKE aplicado ao role `app` (RNF-001, RNF-005.2)
- [ ] RLS ativa com policy `rls_audit_logs_tenant` (REQ-005.2)
- [ ] 3 índices compostos criados (design.md § 7.1)
- [ ] Sem coluna `updated_at` nem soft-delete (REQ-002.5)

---

### TASK-13 — PBT-01 Imutabilidade + PBT-05 Isolamento por tenant (Testcontainers)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `test/audit-log/13-pbt-immutability-isolation` |
| **Worktree** | `git worktree add .forge/worktrees/audit-log/13-pbt-immutability-isolation -b test/audit-log/13-pbt-immutability-isolation` |
| **Status** | [ ] |
| **Depende de** | TASK-12 |
| **Entregável** | PBT-01 e PBT-05 verdes em `AuditLog.Infrastructure.Tests` com PostgreSQL real via Testcontainers |
| **Mapeia** | RNF-001, REQ-005, PBT-01, PBT-05; design.md § 13.1 |
| **Camada principal** | Infrastructure / Tests |

#### Objetivo

Validar via PBT com banco real que: (PBT-01) qualquer sequência de UPDATE/DELETE/TRUNCATE pelo role `app` é rejeitada e o registro permanece byte-a-byte idêntico; (PBT-05) consulta no contexto de `tenant_id` X retorna exclusivamente registros de X, independentemente dos filtros aplicados.

#### Subtasks

- [ ] **ST-01 — Red (PBT-01 e PBT-05):** escrever dois PBTs com Testcontainers PostgreSQL (banco sobe uma vez por fixture); PBT-01: gerador de registros arbitrários + sequências de tentativas de mutação; PBT-05: gerador de conjuntos multi-tenant + filtros variados; verificar propriedades
- [ ] **ST-02 — Green:** configurar `AuditLogTestFixture` com Testcontainers; aplicar migration antes dos PBTs; ajustar conexão para usar role `app` (sem superuser) nos PBTs de imutabilidade
- [ ] **ST-03 — Refactor:** extrair helpers de geração e reset de estado; garantir isolamento entre execuções do gerador (truncate via superuser antes de cada batch)
- [ ] **ST-04 — Encerramento:** PBT-01 e PBT-05 verdes com ≥ 50 amostras cada; Infrastructure.Tests ≥ 70%; `test: adicionar PBT-01 imutabilidade e PBT-05 isolamento por tenant`; push

#### Critérios de Aceite

- [ ] PBT-01 verde: toda tentativa de UPDATE/DELETE/TRUNCATE pelo role `app` lança exceção e o registro permanece idêntico
- [ ] PBT-05 verde: nenhum registro de tenant Y retorna em consulta de tenant X
- [ ] Testcontainers usando PostgreSQL com migration aplicada
- [ ] Infrastructure.Tests ≥ 70% de coverage

---

### TASK-14 — Integração fail-closed (DD-001) + PBT-06 idempotência de leitura

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `test/audit-log/14-fail-closed-pbt06` |
| **Worktree** | `git worktree add .forge/worktrees/audit-log/14-fail-closed-pbt06 -b test/audit-log/14-fail-closed-pbt06` |
| **Status** | [ ] |
| **Depende de** | TASK-11, TASK-12, TASK-13 |
| **Entregável** | Teste de integração de fail-closed (DD-001) verde; PBT-06 verde; log de falha em sink separado (sem PII) validado |
| **Mapeia** | REQ-001, RNF-004, DD-001, PBT-06; design.md § 6.1, § 6.5, § 13.1 |
| **Camada principal** | Infrastructure / Tests |

#### Objetivo

Validar que a falha no INSERT de auditoria reverte também a escrita de negócio na mesma transação (fail-closed, DD-001). Validar via PBT-06 que N execuções de qualquer consulta não alteram o conjunto, a ordem nem o conteúdo dos registros persistidos. Validar que falhas de persistência são registradas em log separado sem PII (RNF-004.3).

#### Subtasks

- [ ] **ST-01 — Red:** teste de integração com Testcontainers: injetar falha no INSERT de `audit_logs` (ex.: violação de constraint); verificar que a escrita de negócio também foi revertida; PBT-06: gerador de estados de trilha + combinações de filtros + N repetições da consulta; verificar que estado persiste inalterado
- [ ] **ST-02 — Green:** garantir que `AuditLogDbContext` e o `DbContext` de negócio compartilham a Unit of Work (mesma transação); configurar sink de log separado para falhas de auditoria sem PII (RNF-004.3)
- [ ] **ST-03 — Refactor:** extrair a lógica de detecção de falha de auditoria para serviço de resiliência; revisar se o sink de dead-letter está no lugar correto (Infrastructure)
- [ ] **ST-04 — Encerramento:** testes de integração verdes; PBT-06 verde; `test: integração fail-closed DD-001 e PBT-06 idempotência de leitura`; push

#### Critérios de Aceite

- [ ] Falha no INSERT de `audit_logs` reverte a escrita de negócio (DD-001)
- [ ] PBT-06 verde com ≥ 100 amostras
- [ ] Falhas de auditoria registradas em log separado sem PII (RNF-004.3)
- [ ] Contador `audit_insert_failures_total` incrementado em cada falha (RNF-004.1)

---

### TASK-15 — AuditLogQueryController (endpoints GET)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `feat/audit-log/15-api-controller` |
| **Worktree** | `git worktree add .forge/worktrees/audit-log/15-api-controller -b feat/audit-log/15-api-controller` |
| **Status** | [ ] |
| **Depende de** | TASK-08, TASK-09, TASK-11 |
| **Entregável** | `AuditLogQueryController` com dois endpoints GET; envelope paginado; 405 para POST/PUT/PATCH/DELETE; testes de contrato da API |
| **Mapeia** | REQ-007, design.md § 8.1, § 8.2 |
| **Camada principal** | Api |

#### Objetivo

Expor os dois endpoints de leitura da trilha de auditoria conforme design.md § 8.1 e § 8.2, retornando o envelope paginado com campos corretos. Nenhum endpoint de escrita é exposto; qualquer tentativa retorna 405. Autenticação JWT obrigatória em ambos.

#### Subtasks

- [ ] **ST-01 — Red:** testes de API (WebApplicationFactory): GET /api/v1/audit-logs retorna 200 com envelope; GET /api/v1/audit-logs/{entityType}/{entityId} retorna histórico; POST/PUT/PATCH/DELETE retornam 405; sem JWT retorna 401 (AUD-ERR-003); filtros de query params mapeados corretamente
- [ ] **ST-02 — Green:** implementar `AuditLogQueryController` com `[HttpGet]` em dois métodos; mapear query params para `ListAuditLogsQuery` e `GetEntityAuditHistoryQuery`; serializar resposta no envelope `{ items, page, pageSize, total }`; registrar no DI e configurar rota `api/v1/`
- [ ] **ST-03 — Refactor:** garantir `createdAt` em UTC ISO-8601 na resposta; revisar mapeamento DTO → `AuditLogResponse`
- [ ] **ST-04 — Encerramento:** Api.Tests verdes; `feat: implementar AuditLogQueryController com GET /api/v1/audit-logs`; push

#### Critérios de Aceite

- [ ] GET /api/v1/audit-logs retorna 200 com envelope paginado
- [ ] GET /api/v1/audit-logs/{entityType}/{entityId} retorna histórico da entidade
- [ ] POST/PUT/PATCH/DELETE retornam 405 (AUD-ERR-007)
- [ ] JWT ausente retorna 401 (AUD-ERR-003)
- [ ] `createdAt` em UTC ISO-8601 na resposta

---

### TASK-16 — Catálogo de erros (AUD-ERR-001..008) + OpenAPI

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `feat/audit-log/16-error-catalog-openapi` |
| **Worktree** | `git worktree add .forge/worktrees/audit-log/16-error-catalog-openapi -b feat/audit-log/16-error-catalog-openapi` |
| **Status** | [ ] |
| **Depende de** | TASK-15 |
| **Entregável** | 8 códigos de erro mapeados para HTTP status correto; middleware de tradução de exceções; especificação OpenAPI gerada; mensagens de erro sem PII |
| **Mapeia** | RNF-002, design.md § 12; `.forge/rules/architecture/api-and-contracts.md` |
| **Camada principal** | Api |

#### Objetivo

Implementar o mapeamento de todas as exceções de domínio e de aplicação para os códigos do catálogo de erros (AUD-ERR-001 a AUD-ERR-008), com os status HTTP corretos. Garantir que mensagens de erro nunca expõem PII nem conteúdo de `delta_json` (RNF-002.3). Gerar especificação OpenAPI com Swashbuckle/NSwag.

#### Subtasks

- [ ] **ST-01 — Red:** testes de: cada código de erro retornado no status HTTP correto (400/401/403/404/405/500); mensagem de AUD-ERR-005 não expõe delta nem PII; AUD-ERR-002 e AUD-ERR-004 com mensagem uniforme (anti-enumeração)
- [ ] **ST-02 — Green:** implementar `AuditErrorMiddleware` ou `IExceptionFilter` mapeando exceções para `ProblemDetails` com campo `errorCode`; documentar cada endpoint com `[ProducesResponseType]`; configurar Swashbuckle
- [ ] **ST-03 — Refactor:** extrair dicionário de erros para classe `AuditErrors`; garantir que AUD-ERR-002/004 retornam mensagem idêntica (anti-enumeração, REQ-005.3)
- [ ] **ST-04 — Encerramento:** Api.Tests verdes; OpenAPI gerada e validável; `feat: catálogo de erros AUD-ERR-001..008 e especificação OpenAPI`; push

#### Critérios de Aceite

- [ ] Todos os 8 códigos de erro mapeados para HTTP correto
- [ ] AUD-ERR-005 não expõe `delta_json` nem PII (RNF-002.3)
- [ ] AUD-ERR-002 e AUD-ERR-004 com mensagem uniforme (sem enumeração)
- [ ] OpenAPI gerada e referenciando os erros por endpoint

---

### TASK-17 — RBAC + escopo de BU (DD-008) + testes de contrato

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `feat/audit-log/17-rbac-bu-scope` |
| **Worktree** | `git worktree add .forge/worktrees/audit-log/17-rbac-bu-scope -b feat/audit-log/17-rbac-bu-scope` |
| **Status** | [ ] |
| **Depende de** | TASK-09, TASK-15 |
| **Entregável** | `AuthorizationBehavior` completo para queries; filtro de BU no handler para `GestorBU`; testes de contrato de `IAuditWriter`; todos os cenários RBAC cobrindo REQ-008 |
| **Mapeia** | REQ-008, DD-008; design.md § 10, § 5.3 |
| **Camada principal** | Application / Api / Contracts |

#### Objetivo

Validar os quatro cenários RBAC: TAdmin acessa todo o tenant; GestorBU filtrado pelas suas BUs; papéis sem permissão (Vendedor, Viewer, Platform Operator por padrão) recebem 403; acesso autorizado de Platform Operator gera registro de auditoria próprio (REQ-008.5). Publicar testes de contrato de `IAuditWriter` para que módulos consumidores possam validar compatibilidade.

#### Subtasks

- [ ] **ST-01 — Red:** testes de autorização com WebApplicationFactory: TAdmin vê registros de todo o tenant; GestorBU vê apenas registros das suas BUs; Vendedor recebe 403; Platform Operator sem acesso por padrão; testes de contrato do `IAuditWriter` verificando assinatura estável
- [ ] **ST-02 — Green:** completar `AuthorizationBehavior` com lógica de papel; implementar filtro de BU no `ListAuditLogsHandler` (DD-008) — resolver BU a partir do `entity_id` via read model; implementar teste de registro de acesso de Platform Operator (REQ-008.5)
- [ ] **ST-03 — Refactor:** isolar a lógica de resolução de BU em serviço dedicado `IBuScopeResolver`; garantir resposta uniforme para 403 (anti-enumeração)
- [ ] **ST-04 — Encerramento:** Api.Tests verdes; testes de contrato do IAuditWriter verdes; `feat: RBAC completo e escopo de BU para consulta de audit-log`; push

#### Critérios de Aceite

- [ ] TAdmin: acesso irrestrito dentro do tenant (REQ-008.2)
- [ ] GestorBU: acesso restrito às suas BUs (REQ-008.3)
- [ ] Vendedor/Viewer/Platform Operator por padrão: 403 (REQ-008.4)
- [ ] Acesso autorizado de Platform Operator gera registro na trilha (REQ-008.5)
- [ ] Testes de contrato de `IAuditWriter` verdes

---

### TASK-18 — Métricas, alertas e health check

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `feat/audit-log/18-observability` |
| **Worktree** | `git worktree add .forge/worktrees/audit-log/18-observability -b feat/audit-log/18-observability` |
| **Status** | [ ] |
| **Depende de** | TASK-14, TASK-15 |
| **Entregável** | 5 contadores/histograma registrados; `AuditInsertCapabilityHealthCheck` funcional; definições de alerta para as 3 condições do design.md § 11.4 |
| **Mapeia** | RNF-004, RNF-006, DD-007; design.md § 11.1..11.5 |
| **Camada principal** | Infrastructure / Api |

#### Objetivo

Implementar toda a camada de observabilidade do módulo: 5 métricas (design.md § 11.2), health check de capacidade de INSERT, traces no `AuditService.Record` e definições dos 3 alertas operacionais. Nenhuma métrica ou log deve conter PII.

#### Subtasks

- [ ] **ST-01 — Red:** testes de health check: banco disponível + INSERT permitido → `Healthy`; banco indisponível → `Unhealthy`; INSERT sem permissão → `Degraded`; testes de que contadores são incrementados nos cenários corretos
- [ ] **ST-02 — Green:** registrar `audit_events_received_total`, `audit_insert_failures_total`, `audit_insert_latency_seconds` (histogram), `audit_query_without_tenant_context_total`, `audit_pii_masking_applied_total` com OpenTelemetry; implementar `AuditInsertCapabilityHealthCheck`; instrumentar `AuditService.Record` com span de trace; criar arquivos de definição de alerta (YAML/JSON)
- [ ] **ST-03 — Refactor:** garantir que nomes das métricas seguem convenção (snake_case, prefixo `audit_`); verificar que nenhuma label de métrica contém PII
- [ ] **ST-04 — Encerramento:** health check funcionando no endpoint `/health`; métricas expostas; `feat: métricas alertas e health check do módulo audit-log`; push

#### Critérios de Aceite

- [ ] `AuditInsertCapabilityHealthCheck` retorna estado correto nas 3 situações (RNF-006.1, 006.2, 006.3)
- [ ] `audit_insert_failures_total` incrementado em cada falha de INSERT (RNF-004.1)
- [ ] `audit_query_without_tenant_context_total` incrementado pelo `TenantContextBehavior` (DD-007)
- [ ] Nenhuma label de métrica contém PII (RNF-002)
- [ ] 3 definições de alerta documentadas (design.md § 11.4)

---

### TASK-19 — PII scan em CI + testes de segurança

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `feat/audit-log/19-security-pii-scan` |
| **Worktree** | `git worktree add .forge/worktrees/audit-log/19-security-pii-scan -b feat/audit-log/19-security-pii-scan` |
| **Status** | [ ] |
| **Depende de** | TASK-05, TASK-09, TASK-17, TASK-18 |
| **Entregável** | Script de scan de PII integrado ao CI como gate obrigatório; testes de segurança cobrindo isolamento cross-tenant, anti-enumeração e RBAC |
| **Mapeia** | RNF-002, REQ-004, REQ-005; design.md § 13 (testes de segurança) |
| **Camada principal** | Tests / DevOps |

#### Objetivo

Implementar gate de CI que escaneia os logs emitidos pelo módulo e falha o build se detectar padrões de e-mail (RFC 5322) ou telefone brasileiro nos logs. Implementar testes de segurança: cross-tenant (tentativa de acessar registros de outro tenant recebe 403/404 uniforme), anti-enumeração (resposta idêntica para "não existe" e "sem acesso"), e Serilog destructuring correto para mascarar PII em logs/traces.

#### Subtasks

- [ ] **ST-01 — Red:** escrever script de scan (regex para e-mail e telefone BR) que falha ao receber log de amostra com PII; testes de segurança: acesso cross-tenant retorna 403; resposta de AUD-ERR-002 idêntica para "sem acesso" e "entidade inexistente"; log de auditoria não expõe conteúdo do delta
- [ ] **ST-02 — Green:** configurar Serilog com `Destructure.ByTransforming` para campos PII; adicionar script de scan ao pipeline CI como gate pós-testes; completar testes de segurança em `AuditLog.Api.Tests`
- [ ] **ST-03 — Refactor:** verificar cobertura de todos os campos PII (name, email, phone) nas regras de destructuring; garantir que o scan cobre logs de todos os componentes do módulo
- [ ] **ST-04 — Encerramento:** gate de CI verde; testes de segurança verdes; `feat: PII scan em CI e testes de segurança do módulo audit-log`; push

#### Critérios de Aceite

- [ ] Scan detecta e-mail (padrão RFC 5322) e telefone BR nos logs e falha o CI (RNF-002.2)
- [ ] Acesso cross-tenant retorna resposta uniforme sem revelar existência de dados
- [ ] AUD-ERR-002 e AUD-ERR-004 com mensagem idêntica (anti-enumeração, REQ-005.3)
- [ ] Serilog destructuring configurado para todos os campos PII (REQ-004)
- [ ] Nenhum valor PII em logs de trace do `AuditService`

---

### TASK-20 — DoD final e sincronização de documentação

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `chore/audit-log/20-dod-docs` |
| **Worktree** | `git worktree add .forge/worktrees/audit-log/20-dod-docs -b chore/audit-log/20-dod-docs` |
| **Status** | [ ] |
| **Depende de** | TASK-18, TASK-19 |
| **Entregável** | Checklist DoD do design.md § 19 completo; README do módulo atualizado; PR final da Onda 6 aberto |
| **Mapeia** | design.md § 19 (Definition of Done); todos os requisitos e RNFs |
| **Camada principal** | Docs |

#### Objetivo

Verificar item a item o Definition of Done do design.md § 19. Atualizar o README do módulo com status atual, versões dos artefatos, ondas concluídas e número de TASKs. Abrir ou finalizar o PR da Onda 6. Registrar VAL-AUDIT-01/02/03 como explicitamente diferidos com justificativa.

#### Subtasks

- [ ] **ST-01:** percorrer checklist do design.md § 19 (13 itens); registrar qualquer item aberto com justificativa formal; marcar VAL-AUDIT-01 (retenção LGPD), VAL-AUDIT-02 (DD-001 aceito) e VAL-AUDIT-03 (SOX para comissão) com status explícito
- [ ] **ST-02:** atualizar `docs/product/modules/audit-log/README.md` com: status `Implementado` ou `Em implementação`; versões de requirements.md, design.md e tasks.md; ondas concluídas; contagem de TASKs (20); cobertura dos 6 PBTs
- [ ] **ST-03:** verificar CI verde para todas as ondas; confirmar que coverage gates estão atendidos em todas as camadas; verificar que matriz de rastreabilidade está sincronizada
- [ ] **ST-04 — Encerramento:** `chore: sincronizar documentação e verificar DoD do módulo audit-log`; push; PR final da Onda 6 aberto

#### Critérios de Aceite

- [ ] Todos os 13 itens do DoD do design.md § 19 marcados ou diferidos com justificativa
- [ ] README do módulo atualizado com status atual
- [ ] CI verde em todas as ondas
- [ ] Coverage gates atendidos: Domain ≥ 95%, Application ≥ 85%, Infrastructure ≥ 70%, Api ≥ 80%
- [ ] 6 PBTs cobertos e verdes

---

## 5. Matriz de Rastreabilidade

| Origem | Descrição | TASKs | Status |
|--------|-----------|-------|--------|
| REQ-001 | Registrar toda escrita em entidade de negócio | TASK-07, TASK-10, TASK-14 | [ ] |
| REQ-002 | Conteúdo mínimo do registro de auditoria | TASK-03, TASK-04, TASK-12 | [ ] |
| REQ-003 | Delta antes/depois da operação | TASK-03, TASK-06 | [ ] |
| REQ-004 | Mascaramento de PII no delta | TASK-05, TASK-07, TASK-19 | [ ] |
| REQ-005 | Isolamento por tenant na trilha | TASK-09, TASK-11, TASK-12, TASK-13 | [ ] |
| REQ-006 | Recepção centralizada via AuditService | TASK-02, TASK-07 | [ ] |
| REQ-007 | Consulta somente-leitura da trilha | TASK-08, TASK-15 | [ ] |
| REQ-008 | Restrição de acesso por papel e escopo | TASK-09, TASK-17 | [ ] |
| RNF-001 | Imutabilidade append-only garantida na persistência | TASK-04, TASK-11, TASK-12, TASK-13 | [ ] |
| RNF-002 | Ausência de PII em logs operacionais e traces | TASK-09, TASK-18, TASK-19 | [ ] |
| RNF-003 | Não bloqueio do caso de uso principal | TASK-14 | [ ] |
| RNF-004 | Alerta em falha de auditoria (sem perda silenciosa) | TASK-14, TASK-18 | [ ] |
| RNF-005 | Retenção sem purge automático | TASK-11, TASK-12 | [ ] |
| RNF-006 | Health check de capacidade de inserção | TASK-18 | [ ] |
| PBT-01 | Append-only: UPDATE/DELETE/TRUNCATE pelo role `app` rejeitados | TASK-13 | [ ] |
| PBT-02 | Conservação: N escritas → N registros com user_id e delta coerentes | TASK-10 | [ ] |
| PBT-03 | Round-trip do delta (create/update/delete) | TASK-06 | [ ] |
| PBT-04 | Mascaramento: nenhum valor PII em texto claro no delta_json | TASK-05 | [ ] |
| PBT-05 | Isolamento: consulta de tenant X retorna só registros de X | TASK-13 | [ ] |
| PBT-06 | Idempotência de leitura: N consultas não alteram a trilha | TASK-14 | [ ] |
| DD-001 | Persistência síncrona transacional fail-closed | TASK-07, TASK-14 | [ ] |
| DD-002 | Imutabilidade enforçada no banco (REVOKE + trigger) | TASK-12, TASK-13 | [ ] |
| DD-003 | Isolamento por tenant via RLS + filtro global | TASK-11, TASK-12, TASK-13 | [ ] |
| DD-004 | Mascaramento de PII configurável por entity_type | TASK-05, TASK-07 | [ ] |
| DD-005 | Valores monetários em centavos inteiros no delta | TASK-03, TASK-06 | [ ] |
| DD-006 | IAuditWriter como contrato público em AuditLog.Contracts | TASK-02, TASK-17 | [ ] |
| DD-007 | Alerta de consulta sem contexto de tenant | TASK-09, TASK-18 | [ ] |
| DD-008 | Escopo de consulta do GestorBU restrito às suas BUs | TASK-08, TASK-17 | [ ] |
| design.md § 3 (Clean Architecture) | Regra de dependência entre camadas | TASK-01 | [ ] |
| design.md § 8.1 / § 8.2 (API) | Endpoints GET e contratos REST | TASK-15, TASK-16 | [ ] |
| design.md § 12 (Catálogo de erros) | AUD-ERR-001..008 | TASK-16 | [ ] |
| design.md § 11 (Observabilidade) | Métricas, alertas, health check, traces | TASK-18 | [ ] |
| design.md § 19 (DoD) | Checklist de encerramento do módulo | TASK-20 | [ ] |

---

## 6. Coverage Gates

| Camada | Gate | Tipo de teste esperado | Verificado em |
|--------|------|------------------------|---------------|
| Domain | ≥ 95% | Unitários de invariantes, objetos de valor, PiiMasker; PBT-03, PBT-04 | TASK-03..TASK-06 |
| Application | ≥ 85% | Unitários de handlers, validators, behaviors; PBT-02 | TASK-07..TASK-10 |
| Infrastructure | ≥ 70% | Integração com Testcontainers (trigger, RLS, repositório, fail-closed); PBT-01, PBT-05, PBT-06 | TASK-11..TASK-14 |
| Api | ≥ 80% | Contrato REST, RBAC, paginação, catálogo de erros, 405 em escrita | TASK-15..TASK-17 |
| Architecture | 100% das regras críticas | Regra de dependência entre camadas (NetArchTest) | TASK-01 |
| Security | Cobertura por cenário crítico | Anti-enumeração, cross-tenant, mascaramento de PII em logs, RBAC completo | TASK-17, TASK-19 |
| Observability | Cobertura por fluxo crítico | Contadores, health check, traces sem PII | TASK-18 |

Regras:

- Coverage gate não substitui qualidade de teste; PBTs são obrigatórios onde há propriedade.
- Infrastructure.Tests deve usar PostgreSQL real via Testcontainers para validar trigger e RLS (banco em memória não valida esses mecanismos).
- Testes de arquitetura (NetArchTest) são executados no CI em todos os PRs.
- Scan de PII em logs é gate obrigatório de CI a partir da Onda 6 (TASK-19).

---

## 7. Critérios de Encerramento

### 7.1 Encerramento de TASK

Uma TASK só pode ser marcada como `[X]` quando:

- todas as subtasks concluídas
- testes aplicáveis verdes
- coverage gate da camada atendido ou justificativa registrada
- lint/format executado (`dotnet format`)
- nenhum warning novo relevante introduzido
- commit realizado em Conventional Commits
- push realizado na branch

### 7.2 Encerramento de Onda

Uma onda só pode ser considerada concluída quando:

- todas as TASKs da onda com status `[X]`
- CI verde (build + testes + scan de PII quando aplicável)
- PR da onda aberto, aprovado ou mergeado conforme regra do projeto
- riscos da onda tratados ou registrados com justificativa
- README do módulo sincronizado quando aplicável

### 7.3 Encerramento do Módulo

O módulo Audit Log só pode ser considerado pronto quando:

- todas as 6 ondas concluídas
- matriz de rastreabilidade completa (todos os status `[X]`)
- `requirements.md`, `design.md` e `tasks.md` consistentes entre si
- PBT-01..PBT-06 verdes com amostras suficientes (≥ 50 por PBT de infraestrutura, ≥ 100 por PBT de domínio/application)
- observabilidade mínima implementada: 5 métricas, 3 alertas, health check, traces
- segurança mínima validada: mascaramento de PII, RBAC completo, isolamento multi-tenant, scan de log no CI
- catálogo de erros (AUD-ERR-001..008) coberto e referenciado por endpoint
- DoD do design.md § 19 com todos os 13 itens marcados ou diferidos com justificativa formal
- VAL-AUDIT-01 (retenção LGPD), VAL-AUDIT-02 (DD-001 aceito por Arquitetura) e VAL-AUDIT-03 (SOX comissão) com status registrado
- README do módulo atualizado

---

## 8. Riscos de Execução

| Código | Risco | Impacto na execução | Mitigação |
|--------|-------|---------------------|-----------|
| RISK-AUDIT-01 | Falha silenciosa no INSERT de auditoria | Perda de trilha — Tier 1 | DD-001 fail-closed (TASK-14); `audit_insert_failures_total` (TASK-18) |
| RISK-AUDIT-02 | PII vazada em `delta_json` sem mascaramento | Violação LGPD | PiiMasker obrigatório antes de persistir (TASK-05, TASK-07); PBT-04 (TASK-05); scan em CI (TASK-19) |
| RISK-AUDIT-03 | Crescimento não controlado de `audit_logs` | Custo e performance | Índices de leitura (TASK-12); particionamento avaliado pós-MVP |
| RISK-AUDIT-04 | Fail-closed bloqueia escrita de negócio em incidente de banco | Indisponibilidade de escrita | Health check (TASK-18) sinaliza antes; runbook de incidente |
| RISK-AUDIT-05 | Escopo de BU do GestorBU depende de read model externo | Consulta incorreta de escopo | DD-008 + `IBuScopeResolver` (TASK-17); avaliar denormalizar `bu_id` em `audit_logs` na evolução |
| RISK-AUDIT-06 | Configuração de Testcontainers PostgreSQL no CI | Lentidão ou falha de infraestrutura de teste | Singleton container por fixture; paralelismo controlado (TASK-13) |
| RISK-EXEC-01 | VAL-AUDIT-01 (retenção LGPD) bloqueando go-live | Bloqueio de release | Registrar como dívida técnica explícita no DoD; ADR-0003 a definir com jurídico (TASK-20) |

---

## 9. Referências

| Referência | Localização |
|------------|-------------|
| requirements.md v0.1.0 (REQ-001..008, RNF-001..006, PBT-01..06) | `docs/product/modules/audit-log/requirements.md` |
| design.md v0.1.0 (DD-001..008, schema, contratos, observabilidade) | `docs/product/modules/audit-log/design.md` |
| README do módulo Audit Log | `docs/product/modules/audit-log/README.md` |
| Imutabilidade de auditoria (REVOKE + trigger) | `.forge/rules/domain/audit-immutability.md` |
| Money como centavos inteiros | `.forge/rules/domain/money-as-cents.md` |
| Clean Architecture | `.forge/rules/architecture/clean-architecture.md` |
| Segurança e compliance | `.forge/rules/architecture/security-and-compliance.md` |
| Observabilidade | `.forge/rules/architecture/observability.md` |
| DDD | `.forge/rules/architecture/ddd.md` |
| Convenção de banco | `.forge/rules/conventions/database-naming.md` |
| Conventional Commits | `.forge/rules/conventions/conventional-commits.md` |
| TDD | `.forge/rules/testing/tdd.md` |
| Quality Gates | `.forge/rules/testing/quality-gates.md` |
| ADR-0003 (retenção, a definir) | `docs/product/adr/` (sugerido) |
| ADR-0007 (stack GCP, a formalizar) | `docs/product/adr/` (sugerido) |
