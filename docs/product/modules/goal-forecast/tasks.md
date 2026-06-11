# Tasks — GF — Goal & Forecast (Metas e Forecast Comparativo)

- Versão: 0.1.0
- Data: 2026-06-11
- Status: Rascunho para revisão
- Referência base requirements: docs/product/modules/goal-forecast/requirements.md v0.1.0
- Referência base design: docs/product/modules/goal-forecast/design.md v0.1.0
- ADRs aplicáveis: ADR-0001 (Isolamento multi-tenant — defesa em profundidade)
- Rules aplicáveis: `.forge/rules/architecture/clean-architecture.md`, `.forge/rules/architecture/ddd.md`, `.forge/rules/architecture/api-and-contracts.md`, `.forge/rules/architecture/observability.md`, `.forge/rules/architecture/jwt-permissions.md`, `.forge/rules/domain/money-as-cents.md`, `.forge/rules/domain/audit-immutability.md`, `.forge/rules/conventions/database-naming.md`, `.forge/rules/conventions/language-policy.md`, `.forge/rules/testing/tdd.md`, `.forge/rules/testing/quality-gates.md`

## Histórico de Versões

| Versão | Data | Status | Descrição da alteração |
|--------|------|--------|------------------------|
| 0.1.0 | 2026-06-11 | Rascunho para revisão | Criação inicial do plano de tasks a partir de requirements.md v0.1.0 e design.md v0.1.0 |

---

## 1. Convenções de Implementação

### 1.1 TDD-first

Toda implementação com lógica verificável deve seguir o ciclo:

1. Red — escrever teste que falha
2. Green — implementar o mínimo para passar
3. Refactor — melhorar sem alterar comportamento

Nenhuma implementação de regra de domínio, handler, endpoint, persistência, contrato ou integração deve ser considerada concluída sem teste correspondente.

### 1.2 Property-Based Testing

PBT é obrigatório para invariantes matemáticas, idempotência, round-trip, anti-enumeração e cálculos monetários.

Cada PBT mapeia explicitamente para `PBT-NN` do `requirements.md`:

| PBT | Tipo | Requisito | TASK |
|-----|------|-----------|------|
| PBT-01 | Idempotência | Req 1, Req 2 | TASK-09 |
| PBT-02 | Invariante matemática | Req 7 | TASK-06, TASK-13 |
| PBT-03 | Invariante matemática | Req 5 | TASK-12 |
| PBT-04 | Anti-enumeração / totalidade | Req 6 | TASK-12 |
| PBT-05 | Round-trip monetário | Req 1, RNF 4 | TASK-03, TASK-20 |

Biblioteca de referência: FsCheck (FsCheck.Xunit) ou equivalente aprovado no TRD.

### 1.3 Bite-sized Tasks

- Cada subtask deve ser estimada em menos de 2 horas.
- Cada TASK deve ser completável em até 2 dias.
- TASK maior que 2 dias deve ser quebrada.

### 1.4 Branch Model

```text
<tipo>/goal-forecast/<NN>-<slug>
```

Exemplos:

```text
feat/goal-forecast/01-bootstrap-solution
test/goal-forecast/18-rls-isolation
fix/goal-forecast/19-circuit-breaker
```

### 1.5 Git Worktree

```sh
git worktree add ../worktrees/goal-forecast/<NN>-<slug> -b <branch>
```

### 1.6 Encerramento de TASK

Cada TASK deve encerrar com: testes locais verdes; coverage gate da camada atendido ou justificativa registrada; lint/format executado; documentação atualizada quando aplicável; commit em Conventional Commits; push da branch.

### 1.7 Encerramento de Onda

Todas as TASKs da onda concluídas, CI verde, conflitos resolvidos, PR da onda aberto ou atualizado.

### 1.8 Early Exit

Se uma subtask falhar: marcar `[-]`, registrar ponto de falha, comando executado e erro principal; não mascarar com implementação especulativa; deixar contexto para retomada.

### 1.9 Convenção de Status

- `[ ]` Não iniciado
- `[-]` Em progresso
- `[X]` Concluído
- `[!]` Falhou — exige intervenção humana

### 1.10 Convenção canônica de IDs

```
TASK-NN — <título>      ← unidade atômica de invocação do task-coder
  ST-MM — <subtask>     ← etapas TDD internas; numeração reinicia por TASK
```

Onda é atributo no header da TASK — nunca entra no ID.

---

## 2. Status Geral

| TASK | Título | Onda | Branch | Status |
|------|--------|------|--------|--------|
| TASK-01 | Bootstrap solution e cinco projetos Clean Architecture | Onda 1 | `feat/goal-forecast/01-bootstrap-solution` | [ ] |
| TASK-02 | Testes de arquitetura e dependências entre camadas | Onda 1 | `test/goal-forecast/02-architecture-tests` | [ ] |
| TASK-03 | Objeto de valor Money (centavos inteiros, PBT-05) | Onda 2 | `feat/goal-forecast/03-money-value-object` | [ ] |
| TASK-04 | Objetos de valor GoalPeriod e GoalScope | Onda 2 | `feat/goal-forecast/04-period-scope-vos` | [ ] |
| TASK-05 | Aggregate Goal: Create, ChangeValorMeta e invariantes INV-1..4 | Onda 2 | `feat/goal-forecast/05-goal-aggregate` | [ ] |
| TASK-06 | Domain event GoalUpdated + serviço GoalAggregation (PBT-02) | Onda 2 | `feat/goal-forecast/06-event-aggregation` | [ ] |
| TASK-07 | GoalAuthorizationPolicy + BuMembershipSpecification | Onda 2 | `feat/goal-forecast/07-authorization-policy` | [ ] |
| TASK-08 | Portas de aplicação: IGoalRepository, IPipelineForecastReader, IBuMembershipReader | Onda 3 | `feat/goal-forecast/08-application-ports` | [ ] |
| TASK-09 | CreateOrUpdateGoalCommandHandler + upsert idempotente (PBT-01) | Onda 3 | `feat/goal-forecast/09-upsert-command-handler` | [ ] |
| TASK-10 | Pipeline behaviors: Logging, TenantContext, Validation, Authorization, Audit | Onda 3 | `feat/goal-forecast/10-pipeline-behaviors` | [ ] |
| TASK-11 | ListGoalsQueryHandler + filtro RBAC por escopo de visibilidade | Onda 3 | `feat/goal-forecast/11-list-goals-query` | [ ] |
| TASK-12 | GetForecastPanelQueryHandler + degradação graciosa (PBT-03, PBT-04) | Onda 3 | `feat/goal-forecast/12-forecast-panel-query` | [ ] |
| TASK-13 | GetGoalAggregateQueryHandler + GoalAggregation derivada (PBT-02) | Onda 3 | `feat/goal-forecast/13-aggregate-query` | [ ] |
| TASK-14 | GetGoalDigestBlockQueryHandler com sinal de ausência | Onda 3 | `feat/goal-forecast/14-digest-block-query` | [ ] |
| TASK-15 | GoalForecastDbContext + mapeamentos EF Core + Global Query Filter | Onda 4 | `feat/goal-forecast/15-ef-dbcontext` | [ ] |
| TASK-16 | Migration: tabela goals, UNIQUE, índice parcial BU e RLS policies | Onda 4 | `feat/goal-forecast/16-migration-rls` | [ ] |
| TASK-17 | GoalRepository: implementação e testes de integração | Onda 4 | `feat/goal-forecast/17-goal-repository` | [ ] |
| TASK-18 | Teste de isolamento RLS: cross-tenant bloqueado via API e SQL direto (KPI-06) | Onda 4 | `test/goal-forecast/18-rls-isolation` | [ ] |
| TASK-19 | PipelineForecastReader + circuit breaker + flag pipelineUnavailable | Onda 4 | `feat/goal-forecast/19-pipeline-reader` | [ ] |
| TASK-20 | BuMembershipReader + OutboxDispatcher + PBT-05 round-trip fim a fim | Onda 4 | `feat/goal-forecast/20-membership-outbox` | [ ] |
| TASK-21 | Contracts DTOs + schema evento goal.updated.v1 | Onda 5 | `feat/goal-forecast/21-contracts-dtos` | [ ] |
| TASK-22 | GoalsController: POST /goals + PUT /goals/{id} | Onda 5 | `feat/goal-forecast/22-goals-write-endpoints` | [ ] |
| TASK-23 | GoalsController: GET /goals + testes de RBAC e catálogo de erros | Onda 5 | `feat/goal-forecast/23-goals-read-endpoint` | [ ] |
| TASK-24 | ForecastController: GET /forecast + GET /forecast/aggregate | Onda 5 | `feat/goal-forecast/24-forecast-endpoints` | [ ] |
| TASK-25 | InternalGoalsController: GET /internal/goals/digest-block (mTLS) | Onda 5 | `feat/goal-forecast/25-internal-digest-endpoint` | [ ] |
| TASK-26 | Testes de contrato: schema goal.updated.v1 + porta ForecastView | Onda 5 | `test/goal-forecast/26-contract-tests` | [ ] |
| TASK-27 | Testes de segurança: RBAC, anti-enumeração, cross-tenant e cross-BU | Onda 6 | `test/goal-forecast/27-security-tests` | [ ] |
| TASK-28 | Observabilidade: logs estruturados, métricas, traces e alertas | Onda 6 | `feat/goal-forecast/28-observability` | [ ] |
| TASK-29 | Baseline de performance do painel (p95 ≤ 3.000 ms) | Onda 6 | `test/goal-forecast/29-performance-baseline` | [ ] |
| TASK-30 | DoD final: reconciliar DD-008, coverage gates, README sincronizado | Onda 6 | `docs/goal-forecast/30-dod-final` | [ ] |

---

## 3. Ondas de Implementação

| Onda | Foco | TASKs | Critério de Fechamento |
|------|------|-------|------------------------|
| Onda 1 | Bootstrap | TASK-01..TASK-02 | Projetos compilam, testes de arquitetura verdes, CI mínimo verde |
| Onda 2 | Domain | TASK-03..TASK-07 | Domínio sem dependências externas, todos os PBTs de domínio verdes, 100% das invariantes testadas |
| Onda 3 | Application | TASK-08..TASK-14 | Handlers testados com mocks de porta, PBTs de application verdes, behaviors ativos |
| Onda 4 | Infrastructure | TASK-15..TASK-20 | Migração aplicada, RLS ativa, repositório e readers com testes de integração, isolamento cross-tenant verificado |
| Onda 5 | API + Contracts | TASK-21..TASK-26 | Endpoints respondendo, catálogo de erros coberto, testes de contrato verdes |
| Onda 6 | Hardening | TASK-27..TASK-30 | Segurança verificada, métricas emitidas, p95 medido, DD-008 reconciliado, DoD assinado |

---

## 4. Tarefas

### TASK-01 — Bootstrap solution e cinco projetos Clean Architecture

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 1 — Bootstrap |
| **Branch** | `feat/goal-forecast/01-bootstrap-solution` |
| **Worktree** | `git worktree add ../worktrees/goal-forecast/01-bootstrap-solution -b feat/goal-forecast/01-bootstrap-solution` |
| **Status** | [ ] |
| **Depende de** | Não aplicável |
| **Entregável** | Cinco projetos compilando com referências corretas: Domain, Application, Infrastructure, Api, Contracts |
| **Mapeia** | DD-001, ADR-0001 |
| **Camada principal** | DevOps |

#### Objetivo

Criar a estrutura de solução do slice `goal-forecast` com os cinco projetos da Clean Architecture (DD-001) e referências conforme design §3: Application → Domain + Contracts; Infrastructure → Application + Domain; Api → Application + Infrastructure + Contracts; Domain → nada; Contracts → nada.

#### Subtasks

- [ ] **ST-01 — Red:** escrever teste de compilação que verifica a existência dos cinco assemblies com namespaces corretos (falha porque os projetos não existem).
- [ ] **ST-02 — Green:** criar os cinco projetos com referências de projeto conforme hierarquia do design §3; registrar a solution no monólito modular `azim-api`.
- [ ] **ST-03 — Refactor:** confirmar namespaces em inglês (`GoalForecast.*`), remover boilerplate, validar ausência de dependências circulares.
- [ ] **ST-04 — Docs:** anotar estrutura na seção de arquitetura do README do módulo, se existir.
- [ ] **ST-05 — Encerramento:** `dotnet build` verde, commit `feat(goal-forecast): bootstrap cinco projetos Clean Architecture`, push.

#### Critérios de Aceite

- [ ] Cinco projetos criados com namespaces `GoalForecast.*`.
- [ ] Referências seguem a hierarquia do design §3 sem dependências circulares.
- [ ] `dotnet build` sem erro ou warning de dependência circular.
- [ ] Domain e Contracts sem referências externas proibidas.

---

### TASK-02 — Testes de arquitetura e dependências entre camadas

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 1 — Bootstrap |
| **Branch** | `test/goal-forecast/02-architecture-tests` |
| **Worktree** | `git worktree add ../worktrees/goal-forecast/02-architecture-tests -b test/goal-forecast/02-architecture-tests` |
| **Status** | [ ] |
| **Depende de** | TASK-01 |
| **Entregável** | Suite `GoalForecast.Architecture.Tests` com regras de dependência automatizadas e bloqueadoras no CI |
| **Mapeia** | DD-001, ADR-0001, Req 12 |
| **Camada principal** | Tests |

#### Objetivo

Implementar testes de arquitetura que validam as regras de dependência entre camadas (design §3) e as políticas de camada pura (Domain e Contracts sem dependências externas). Qualquer violação futura quebra o CI imediatamente.

#### Subtasks

- [ ] **ST-01 — Red:** criar suite `GoalForecast.Architecture.Tests`; escrever testes referenciando os cinco assemblies — falham porque ainda não há conteúdo mínimo para reflexão.
- [ ] **ST-02 — Green:** implementar regras via NetArchTest ou equivalente aprovado: Api não acessa Domain diretamente; Infrastructure não referencia Api; Domain e Contracts sem dependências de terceiros proibidas.
- [ ] **ST-03 — Refactor:** agrupar regras por categoria (dependência proibida, namespace puro, camada de infraestrutura); garantir mensagens de falha descritivas.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes verdes, commit `test(goal-forecast): testes de arquitetura Clean Architecture`, push.

#### Critérios de Aceite

- [ ] Todas as regras de dependência do design §3 cobertas por testes automatizados.
- [ ] Violação de qualquer regra quebra o CI.
- [ ] Domain e Contracts validados como camadas sem dependências externas proibidas.
- [ ] Testes com mensagens de falha descritivas que identificam o violador.

---

### TASK-03 — Objeto de valor Money (centavos inteiros, PBT-05)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `feat/goal-forecast/03-money-value-object` |
| **Worktree** | `git worktree add ../worktrees/goal-forecast/03-money-value-object -b feat/goal-forecast/03-money-value-object` |
| **Status** | [ ] |
| **Depende de** | TASK-01 |
| **Entregável** | `Money` imutável em `long` centavos, com `Add`, `Subtract`, `Zero`, igualdade por valor e PBT-05 green |
| **Mapeia** | Req 1.2, RNF 4, PBT-05, DEC-011 |
| **Camada principal** | Domain |

#### Objetivo

Implementar o objeto de valor `Money` como base de toda manipulação monetária do módulo (RNF 4, design §4.3). Deve proibir construção a partir de `double`/`float`, expor operações inteiras e ser a única representação de valor monetário no domínio. PBT-05 garante round-trip sem perda.

#### Subtasks

- [ ] **ST-01 — Red:** testes unitários para `Money`: igualdade por valor, `Add`/`Subtract` exatos, rejeição de valor negativo na construção, proibição de `double`, `Money.Zero`. PBT-05: para qualquer `long` não-negativo, `Money.Of(cents).Cents == cents` e serialização/desserialização JSON preserva o valor exato.
- [ ] **ST-02 — Green:** implementar `Money` como `record` imutável com `long Cents >= 0`; factory `Money.Of(long)` com guard; `Add(Money)`, `Subtract(Money)` retornando `Money`; `Money.Zero` como constante; construção via `double` não compilável ou lança.
- [ ] **ST-03 — Refactor:** garantir que nenhuma operação usa `float`/`double` internamente; solidificar mensagens de exceção.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes + PBT-05 verdes, coverage Domain ≥ 95%, commit `feat(goal-forecast): objeto de valor Money em centavos inteiros`, push.

#### Critérios de Aceite

- [ ] `Money` imutável com igualdade por valor.
- [ ] Construção a partir de `double`/`float` bloqueada em compile-time ou runtime.
- [ ] `Add`/`Subtract` exatos; `Subtract` lança para resultado negativo.
- [ ] PBT-05 green com gerador de `long` não-negativo incluindo limites `long.MaxValue / 2`.
- [ ] Nenhum uso de ponto flutuante na implementação.

---

### TASK-04 — Objetos de valor GoalPeriod e GoalScope

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `feat/goal-forecast/04-period-scope-vos` |
| **Worktree** | `git worktree add ../worktrees/goal-forecast/04-period-scope-vos -b feat/goal-forecast/04-period-scope-vos` |
| **Status** | [ ] |
| **Depende de** | TASK-03 |
| **Entregável** | `GoalPeriod` (year/month validados) e `GoalScope` (BU/RESPONSAVEL com bu_id obrigatório) com testes unitários verdes |
| **Mapeia** | Req 1.3, requirements §4, INV-2, INV-3 |
| **Camada principal** | Domain |

#### Objetivo

Implementar `GoalPeriod` e `GoalScope` como objetos de valor imutáveis que protegem as invariantes INV-2 (período válido) e INV-3 (escopo coerente com bu_id obrigatório e owner_id condicional) no domínio, sem dependência de camadas externas.

#### Subtasks

- [ ] **ST-01 — Red:** testes para `GoalPeriod`: `month ∉ [1..12]` lança; `year` de dois dígitos lança; igualdade por valor; `Quarter()` retorna 1..4 correto; `YearOf()` retorna year. Testes para `GoalScope`: `bu_id` nulo lança; `owner_id` presente em BU lança; `owner_id` ausente em RESPONSAVEL lança; `Kind` derivado correto.
- [ ] **ST-02 — Green:** implementar `GoalPeriod` como `record` com `Year` (quatro dígitos) e `Month` (1..12); métodos `Quarter()` e `YearOf()`. Implementar `GoalScope` com `GoalScopeKind { BU, RESPONSAVEL }`, `BuId` obrigatório, `OwnerId` condicional, `Kind` derivado da presença de `OwnerId`.
- [ ] **ST-03 — Refactor:** solidificar mensagens de exceção de domínio; garantir igualdade por valor em ambos; revisar nomes em inglês.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes verdes, commit `feat(goal-forecast): objetos de valor GoalPeriod e GoalScope`, push.

#### Critérios de Aceite

- [ ] `GoalPeriod` rejeita `month ∉ [1..12]` e `year` inválido com exceção de domínio.
- [ ] `GoalScope` garante `bu_id` obrigatório e coerência `scope × owner_id`.
- [ ] Igualdade por valor em ambos os objetos.
- [ ] `Quarter()` correto para todos os 12 meses.

---

### TASK-05 — Aggregate Goal: Create, ChangeValorMeta e invariantes INV-1..4

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `feat/goal-forecast/05-goal-aggregate` |
| **Worktree** | `git worktree add ../worktrees/goal-forecast/05-goal-aggregate -b feat/goal-forecast/05-goal-aggregate` |
| **Status** | [ ] |
| **Depende de** | TASK-03, TASK-04 |
| **Entregável** | `Goal` Aggregate Root com `Create`, `ChangeValorMeta` e invariantes INV-1..4 cobertas por testes |
| **Mapeia** | Req 1, Req 4, INV-1..4, design §4.1 |
| **Camada principal** | Domain |

#### Objetivo

Implementar o Aggregate Root `Goal` que protege as quatro invariantes de domínio (INV-1: valor não-negativo; INV-2: período válido; INV-3: escopo coerente; INV-4: tenant_id imutável) e acumula domain events para despacho posterior via outbox.

#### Subtasks

- [ ] **ST-01 — Red:** testes de invariante para cada INV individualmente (violação lança exceção de domínio tipada); teste de `Goal.Create` com dados válidos (acumula evento); teste de `ChangeValorMeta` com valor válido (acumula evento updated) e inválido (lança).
- [ ] **ST-02 — Green:** implementar `Goal` com `GoalId` (UUID), `TenantId`, `Scope: GoalScope`, `Period: GoalPeriod`, `ValorMeta: Money`, `CreatedAt`, `UpdatedAt`; `Goal.Create(...)` valida e acumula `GoalUpdated(action=created)`; `ChangeValorMeta(Money)` revalida INV-1, atualiza `UpdatedAt`, acumula `GoalUpdated(action=updated, valorMetaAnterior, valorMetaNovo)`.
- [ ] **ST-03 — Refactor:** garantir `TenantId` imutável; extrair coleção de domain events como lista interna não exposta diretamente; solidificar exceções tipadas por INV.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes verdes, coverage Domain ≥ 95%, commit `feat(goal-forecast): aggregate Goal com invariantes e acúmulo de domain events`, push.

#### Critérios de Aceite

- [ ] INV-1..4 verificadas em testes independentes; violação lança exceção de domínio tipada.
- [ ] `Goal.Create` válido resulta em aggregate com `GoalUpdated(created)` acumulado.
- [ ] `ChangeValorMeta` com valor inválido lança; com valor válido acumula `GoalUpdated(updated)` com delta.
- [ ] `TenantId` imutável após criação.
- [ ] Coleção de domain events limpa após despacho.

---

### TASK-06 — Domain event GoalUpdated + serviço GoalAggregation (PBT-02)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `feat/goal-forecast/06-event-aggregation` |
| **Worktree** | `git worktree add ../worktrees/goal-forecast/06-event-aggregation -b feat/goal-forecast/06-event-aggregation` |
| **Status** | [ ] |
| **Depende de** | TASK-05 |
| **Entregável** | `GoalUpdated` com payload canônico e `GoalAggregation` com PBT-02 green |
| **Mapeia** | Req 7, Req 10, PBT-02, RNF 5, design §4.4 e §4.6 |
| **Camada principal** | Domain |

#### Objetivo

Formalizar o domain event `GoalUpdated` com o payload completo (design §4.4) e implementar o serviço de domínio puro `GoalAggregation` que soma metas mensais em trimestre/ano com meses ausentes = `Money.Zero`, sem arredondamento (RN-027, PBT-02).

#### Subtasks

- [ ] **ST-01 — Red:** testes para `GoalUpdated`: presença de todos os campos canônicos (design §4.4); `valorMetaAnterior` nulo em `action=created`. PBT-02: para qualquer subconjunto aleatório de metas mensais de um mesmo escopo, soma trimestral = soma dos 3 meses, soma anual = soma dos 12 meses, meses ausentes contribuem com zero, resultado exato em `long`.
- [ ] **ST-02 — Green:** implementar `GoalUpdated` como record com `goalId`, `tenantId`, `buId`, `ownerId`, `year`, `month`, `action`, `valorMetaAnterior`, `valorMetaNovo`, `occurredAt`. Implementar `GoalAggregation` com `SumByQuarter(IEnumerable<Goal>, GoalPeriod)` e `SumByYear(IEnumerable<Goal>, int year)` retornando `Money`.
- [ ] **ST-03 — Refactor:** garantir `GoalAggregation` como função pura sem I/O; validar que meses ausentes contribuem com `Money.Zero`.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes + PBT-02 verdes, commit `feat(goal-forecast): GoalUpdated e GoalAggregation com PBT-02`, push.

#### Critérios de Aceite

- [ ] `GoalUpdated` contém todos os campos do payload canônico (design §4.4).
- [ ] `valorMetaAnterior` é nulo quando `action = created`.
- [ ] PBT-02 green com gerador de subconjuntos de meses e valores `long`.
- [ ] `GoalAggregation` é função pura determinística, sem dependência de I/O.
- [ ] Soma exata em centavos inteiros, sem perda ou arredondamento.

---

### TASK-07 — GoalAuthorizationPolicy + BuMembershipSpecification

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `feat/goal-forecast/07-authorization-policy` |
| **Worktree** | `git worktree add ../worktrees/goal-forecast/07-authorization-policy -b feat/goal-forecast/07-authorization-policy` |
| **Status** | [ ] |
| **Depende de** | TASK-05 |
| **Entregável** | `GoalAuthorizationPolicy` pura com matriz de papéis/escopo e `BuMembershipSpecification` cobertas por testes |
| **Mapeia** | Req 12, Req 1.4, RNF 2, design §4.6 e §10 |
| **Camada principal** | Domain |

#### Objetivo

Implementar `GoalAuthorizationPolicy` como objeto de domínio puro (sem I/O) que decide se um `principal` (role + tenant + bu + ownerId) pode escrever ou ler uma meta de determinado escopo (Req 12, design §10). Implementar `BuMembershipSpecification` que expressa a intenção "o owner pertence à BU" no domínio, avaliada com dados externos fornecidos pela porta `IBuMembershipReader`.

#### Subtasks

- [ ] **ST-01 — Red:** testes para cada célula da matriz de autorização (design §10): Tenant Admin cria qualquer BU; Gestor cria só sua BU; Executivo e Vendedor negados na escrita; Vendedor lê só `owner_id` próprio; Gestor lê sua BU; Admin/Executivo leem o tenant. Teste de negação sem revelar existência (RNF-2.3).
- [ ] **ST-02 — Green:** implementar `GoalAuthorizationPolicy` com método `CanWrite(principal, scope)` e `CanRead(principal, scope)` retornando `AuthorizationResult` (permitido/negado com código `GF-ERR-006`). Implementar `BuMembershipSpecification` como classe de especificação que recebe resultado de membership e expõe `IsSatisfied`.
- [ ] **ST-03 — Refactor:** garantir que `GoalAuthorizationPolicy` é pura (sem I/O, sem injeção); cobrir caso de tenant_id divergente (INV-4).
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes verdes cobrindo todos os papéis e escopos, commit `feat(goal-forecast): GoalAuthorizationPolicy e BuMembershipSpecification`, push.

#### Critérios de Aceite

- [ ] Todos os cenários da matriz de autorização (design §10) cobertos por testes unitários.
- [ ] Negação de autorização retorna `GF-ERR-006` sem revelar existência de metas fora do escopo.
- [ ] `GoalAuthorizationPolicy` é pura: sem I/O, sem injeção de dependência.
- [ ] `BuMembershipSpecification` avaliável com dados externos sem acoplamento ao domínio organization.

---

### TASK-08 — Portas de aplicação: IGoalRepository, IPipelineForecastReader, IBuMembershipReader

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/goal-forecast/08-application-ports` |
| **Worktree** | `git worktree add ../worktrees/goal-forecast/08-application-ports -b feat/goal-forecast/08-application-ports` |
| **Status** | [ ] |
| **Depende de** | TASK-05 |
| **Entregável** | Três interfaces de porta definidas no projeto Application com contratos tipados e DTOs de porta |
| **Mapeia** | Req 8, DD-004, DD-005, design §5.3 e §6.4 |
| **Camada principal** | Application |

#### Objetivo

Definir as três portas de saída da camada Application que isolam handlers de detalhes de infraestrutura: `IGoalRepository` (persistência), `IPipelineForecastReader` (leitura do read model ForecastView) e `IBuMembershipReader` (verificação de membership). Contratos tipados com `ForecastViewQuery`, `ForecastViewResult` e `BuMembershipQuery`.

#### Subtasks

- [ ] **ST-01 — Red:** testes que verificam que cada interface existe no assembly Application e não referencia nada de Infrastructure; teste que `ForecastViewResult` tem campo `available: bool` (DD-007).
- [ ] **ST-02 — Green:** definir `IGoalRepository` com `FindByKey`, `FindById`, `Add`, `Update`, `Query`; `IPipelineForecastReader` com `Read(ForecastViewQuery) → ForecastViewResult { wonTotalCents, forecastPonderadoCents, available }`; `IBuMembershipReader` com `IsOwnerMemberOfBu(tenantId, ownerId, buId) → bool`.
- [ ] **ST-03 — Refactor:** garantir que todos os contratos usam tipos primitivos ou tipos do Domain/Contracts; nenhuma referência a EF Core ou Npgsql.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes verdes, commit `feat(goal-forecast): portas IGoalRepository, IPipelineForecastReader, IBuMembershipReader`, push.

#### Critérios de Aceite

- [ ] Três interfaces definidas no projeto Application sem dependência de Infrastructure.
- [ ] `ForecastViewResult` inclui campo `available: bool` para flag de indisponibilidade (DD-007).
- [ ] Contratos tipados sem uso de `dynamic`, `object` ou `string` como substitutos de tipos fortes.
- [ ] Testes de arquitetura passam após adição das portas.

---

### TASK-09 — CreateOrUpdateGoalCommandHandler + upsert idempotente (PBT-01)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/goal-forecast/09-upsert-command-handler` |
| **Worktree** | `git worktree add ../worktrees/goal-forecast/09-upsert-command-handler -b feat/goal-forecast/09-upsert-command-handler` |
| **Status** | [ ] |
| **Depende de** | TASK-07, TASK-08 |
| **Entregável** | `CreateOrUpdateGoalCommandHandler` com upsert idempotente, membership check e PBT-01 green |
| **Mapeia** | Req 1, Req 2, Req 4, PBT-01, DD-002, design §5.1 |
| **Camada principal** | Application |

#### Objetivo

Implementar o único handler de escrita do módulo com semântica upsert idempotente por chave `(tenant, bu, owner, year, month)`. O handler orquestra: resolução de tenant do contexto autenticado, autorização, membership check em escopo RESPONSAVEL, find-by-key no repositório e create-ou-update no aggregate, finalizando com despacho via outbox.

#### Subtasks

- [ ] **ST-01 — Red:** testes com mocks de porta: criar nova meta (repositório retorna null → `Goal.Create`); atualizar meta existente (repositório retorna goal → `ChangeValorMeta`); membership inválido lança `GF-ERR-004`; autorização negada lança `GF-ERR-006`. PBT-01: para qualquer sequência de N upserts com mesma chave, o estado final tem exatamente 1 registro com o último `valorMeta`.
- [ ] **ST-02 — Green:** implementar `CreateOrUpdateGoalCommandHandler` seguindo os 6 passos do design §5.1: (1) resolve tenant do contexto; (2) autorização; (3) membership se RESPONSAVEL; (4) find-by-key; (5) create ou update; (6) despacha eventos do aggregate via repositório/outbox.
- [ ] **ST-03 — Refactor:** extrair validação de membership para método privado; garantir que `tenant_id` nunca vem do payload.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes + PBT-01 verdes, coverage Application ≥ 85%, commit `feat(goal-forecast): CreateOrUpdateGoalCommandHandler com upsert idempotente`, push.

#### Critérios de Aceite

- [ ] Handler retorna `GoalDto` com `201` em criação e `200` em atualização.
- [ ] `tenant_id` nunca aceito do payload do command — sempre do contexto autenticado.
- [ ] Membership inválido resulta em `GF-ERR-004` sem persistir.
- [ ] PBT-01 green: N upserts com mesma chave resultam em exatamente 1 registro.
- [ ] Autorização negada resulta em `GF-ERR-006`.

---

### TASK-10 — Pipeline behaviors: Logging, TenantContext, Validation, Authorization, Audit

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/goal-forecast/10-pipeline-behaviors` |
| **Worktree** | `git worktree add ../worktrees/goal-forecast/10-pipeline-behaviors -b feat/goal-forecast/10-pipeline-behaviors` |
| **Status** | [ ] |
| **Depende de** | TASK-09 |
| **Entregável** | Cinco behaviors encadeados com testes unitários cobrindo ordem e comportamento de cada um |
| **Mapeia** | RNF 1, RNF 2, RNF 5, RNF 7, ADR-0001, design §5.4 |
| **Camada principal** | Application |

#### Objetivo

Implementar os cinco pipeline behaviors em ordem correta (design §5.4): `LoggingBehavior` → `TenantContextBehavior` → `ValidationBehavior` → `GoalAuthorizationBehavior` → `AuditBehavior` (apenas commands). Garantir que logging inclui `correlation_id`, `tenant_id`, `bu_id`, período e ação (RNF 7.1) e que `AuditBehavior` enfileira `GoalUpdated` no outbox após sucesso.

#### Subtasks

- [ ] **ST-01 — Red:** testes de cada behavior em isolamento: `LoggingBehavior` emite campos obrigatórios; `TenantContextBehavior` injeta `tenant_id` do principal e lança se ausente; `ValidationBehavior` propaga erros de FluentValidation; `GoalAuthorizationBehavior` invoca `GoalAuthorizationPolicy`; `AuditBehavior` enfileira evento após sucesso e não enfileira em falha.
- [ ] **ST-02 — Green:** implementar cada behavior; registrar no container na ordem correta; `ValidationBehavior` com validators do `CreateOrUpdateGoalCommand` (`month ∈ [1..12]`, `year` quatro dígitos, `valorMeta >= 0`, coerência scope × ownerId).
- [ ] **ST-03 — Refactor:** extrair constantes de campo de log; garantir que logs não expõem `valorMeta` em texto sensível.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes verdes, commit `feat(goal-forecast): pipeline behaviors Logging TenantContext Validation Authorization Audit`, push.

#### Critérios de Aceite

- [ ] Cinco behaviors encadeados na ordem correta do design §5.4.
- [ ] `LoggingBehavior` emite `correlation_id`, `tenant_id`, `bu_id`, período e ação.
- [ ] `TenantContextBehavior` lança se `tenant_id` não estiver no principal.
- [ ] `AuditBehavior` não enfileira evento em caso de falha do handler.
- [ ] `ValidationBehavior` rejeita command inválido com códigos `GF-ERR-001/002/003`.

---

### TASK-11 — ListGoalsQueryHandler + filtro RBAC por escopo de visibilidade

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/goal-forecast/11-list-goals-query` |
| **Worktree** | `git worktree add ../worktrees/goal-forecast/11-list-goals-query -b feat/goal-forecast/11-list-goals-query` |
| **Status** | [ ] |
| **Depende de** | TASK-10 |
| **Entregável** | `ListGoalsQueryHandler` com filtro RBAC aplicado por papel, paginação e retorno de centavos inteiros |
| **Mapeia** | Req 3, Req 12, RNF 2, design §5.2 |
| **Camada principal** | Application |

#### Objetivo

Implementar `ListGoalsQueryHandler` que lista metas do tenant com filtro RBAC conforme escopo de visibilidade: Vendedor vê só `owner_id` próprio; Gestor vê sua BU; Admin e Executivo veem o tenant (Req 12.3). Paginação com `page`, `pageSize` (default 50, máx 200).

#### Subtasks

- [ ] **ST-01 — Red:** testes com mocks: Vendedor não vê metas de outro owner; Gestor não vê outra BU; Admin vê todas as BUs do tenant; filtro por `buId`, `ownerId`, `year`, `month`; `valorMeta` retornado como inteiro de centavos.
- [ ] **ST-02 — Green:** implementar handler aplicando `GoalAuthorizationPolicy.CanRead` e traduzindo em filtros para `IGoalRepository.Query`; mapear `Goal` para `GoalDto` com `valorMeta` em centavos.
- [ ] **ST-03 — Refactor:** garantir que filtro de tenant é aplicado antes de qualquer outro; extrair mapeamento de `Goal` para `GoalDto`.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes verdes, commit `feat(goal-forecast): ListGoalsQueryHandler com RBAC e paginação`, push.

#### Critérios de Aceite

- [ ] Vendedor recebe apenas metas onde é `owner_id`.
- [ ] Gestor não acessa metas de BU diferente da sua.
- [ ] `valorMeta` retornado como inteiro de centavos em `GoalDto`.
- [ ] Paginação funciona com `pageSize` máximo de 200.
- [ ] Negação de autorização não revela existência de metas fora do escopo.

---

### TASK-12 — GetForecastPanelQueryHandler + degradação graciosa (PBT-03, PBT-04)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/goal-forecast/12-forecast-panel-query` |
| **Worktree** | `git worktree add ../worktrees/goal-forecast/12-forecast-panel-query -b feat/goal-forecast/12-forecast-panel-query` |
| **Status** | [ ] |
| **Depende de** | TASK-10, TASK-08 |
| **Entregável** | `GetForecastPanelQueryHandler` com composição correta do painel, degradação graciosa e PBT-03 + PBT-04 verdes |
| **Mapeia** | Req 5, Req 6, Req 8, PBT-03, PBT-04, DD-006, DD-007, design §5.2 |
| **Camada principal** | Application |

#### Objetivo

Implementar `GetForecastPanelQueryHandler` que orquestra a composição do painel comparativo "Direção": lê meta (pode ser nula), lê `won_total`/`forecast_ponderado` via porta, compõe `ForecastPanelResult` com gap/pct quando há meta, com campos nulos quando não há (DD-006), com `pipelineUnavailable=true` quando pipeline falha (DD-007). Operação total: nunca 404, nunca NaN (PBT-04).

#### Subtasks

- [ ] **ST-01 — Red:** testes: com meta + pipeline disponível → gap = valorMeta - realizado, pct = realizado/valorMeta; sem meta → valorMeta/gap/pct nulos, realizado/pipeline presentes, sem 404; pipeline indisponível → pipelineUnavailable=true, sem exceção propagada; valorMeta=0 → pct ausente. PBT-03: gap = valorMeta − realizado exato; realizado=valorMeta → gap=0, pct=1. PBT-04: para qualquer combinação meta/pipeline válida, resultado bem-formado sem NaN.
- [ ] **ST-02 — Green:** implementar handler seguindo os 4 passos do design §5.2; modelar `ForecastPanelResult` com campos anuláveis para meta/gap/pct; tratar `available=false` da porta como `pipelineUnavailable=true`.
- [ ] **ST-03 — Refactor:** extrair lógica de composição do painel para método privado; garantir que `pct_atingimento` é derivada apenas quando `valorMeta > 0`.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes + PBT-03 + PBT-04 verdes, coverage Application ≥ 85%, commit `feat(goal-forecast): GetForecastPanelQueryHandler com degradação graciosa`, push.

#### Critérios de Aceite

- [ ] Sem meta: `valorMeta`, `gap`, `pct_atingimento` nulos; `realizado` e `pipelineDisponivel` presentes.
- [ ] Pipeline indisponível: `pipelineUnavailable=true`; sem exceção propagada; sem zero confundível.
- [ ] PBT-03 green: `gap = valorMeta − realizado` exato; `realizado = valorMeta → gap = 0, pct = 1`.
- [ ] PBT-04 green: toda combinação válida de meta/pipeline retorna resultado bem-formado.
- [ ] `pct_atingimento` calculado apenas quando `valorMeta > 0`.

---

### TASK-13 — GetGoalAggregateQueryHandler + GoalAggregation derivada (PBT-02)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/goal-forecast/13-aggregate-query` |
| **Worktree** | `git worktree add ../worktrees/goal-forecast/13-aggregate-query -b feat/goal-forecast/13-aggregate-query` |
| **Status** | [ ] |
| **Depende de** | TASK-06, TASK-11 |
| **Entregável** | `GetGoalAggregateQueryHandler` retornando soma derivada por trimestre ou ano, com meses ausentes = zero e PBT-02 green |
| **Mapeia** | Req 7, PBT-02, DD-003, RN-027, design §5.2 |
| **Camada principal** | Application |

#### Objetivo

Implementar `GetGoalAggregateQueryHandler` que carrega metas do período via repositório e delega a soma para `GoalAggregation` (serviço de domínio puro). Nunca persiste metas trimestrais/anuais (DD-003, RN-027). Resultado em centavos inteiros.

#### Subtasks

- [ ] **ST-01 — Red:** testes: trimestre com 3 meses presentes; trimestre com 1 mês ausente (ausente = 0); ano completo; ano parcial; granularidade inválida retorna erro 400.
- [ ] **ST-02 — Green:** implementar handler que lê metas do repositório por `(tenant, scope, year)` e invoca `GoalAggregation.SumByQuarter` ou `SumByYear`; retornar `valorMetaAgregado` como `long` de centavos.
- [ ] **ST-03 — Refactor:** garantir que handler não contém lógica de soma; toda soma delegada ao `GoalAggregation` do domínio.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes verdes (PBT-02 coberto em TASK-06 e reutilizado aqui via integração), commit `feat(goal-forecast): GetGoalAggregateQueryHandler`, push.

#### Critérios de Aceite

- [ ] Granularidade `quarter` retorna soma dos 3 meses; `year` retorna soma dos 12 meses.
- [ ] Meses sem meta cadastrada contribuem com zero, sem erro.
- [ ] Resultado em centavos inteiros (`long`).
- [ ] Lógica de soma delegada ao domínio (`GoalAggregation`) — handler é orquestrador.

---

### TASK-14 — GetGoalDigestBlockQueryHandler com sinal de ausência

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/goal-forecast/14-digest-block-query` |
| **Worktree** | `git worktree add ../worktrees/goal-forecast/14-digest-block-query -b feat/goal-forecast/14-digest-block-query` |
| **Status** | [ ] |
| **Depende de** | TASK-12 |
| **Entregável** | `GetGoalDigestBlockQueryHandler` retornando bloco com meta ou `{ present: false }` para Digest worker |
| **Mapeia** | Req 9, RN-018, RN-029, design §5.2 e §8.6 |
| **Camada principal** | Application |

#### Objetivo

Implementar `GetGoalDigestBlockQueryHandler` que fornece o bloco de metas ao Digest worker (BC-06). Quando há meta: retorna `{ present: true, valorMeta, realizado, gap, pipelineDisponivel }`. Sem meta: retorna `{ present: false }` para que o Digest omita o bloco completamente (Req 9.2, RN-018).

#### Subtasks

- [ ] **ST-01 — Red:** testes: com meta presente → `present=true` com todos os campos em centavos inteiros; sem meta → `present=false` sem outros campos; pipeline indisponível com meta → `present=true` com `pipelineUnavailable=true`.
- [ ] **ST-02 — Green:** implementar handler reutilizando lógica de composição do painel (ou delegando ao `GetForecastPanelQueryHandler`); retornar `DigestBlockResult` com campo `present` e campos opcionais.
- [ ] **ST-03 — Refactor:** garantir que `present=false` é o único sinal de ausência, sem campos vazios ou zero confundível.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes verdes, commit `feat(goal-forecast): GetGoalDigestBlockQueryHandler com sinal de ausência`, push.

#### Critérios de Aceite

- [ ] Sem meta: `present=false` e nenhum campo monetário na resposta.
- [ ] Com meta: `present=true` com todos os campos em centavos inteiros.
- [ ] Valores monetários no bloco são `long` de centavos inteiros.
- [ ] Resposta é total: para qualquer período/BU válidos, retorna resultado bem-formado.

---

### TASK-15 — GoalForecastDbContext + mapeamentos EF Core + Global Query Filter

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/goal-forecast/15-ef-dbcontext` |
| **Worktree** | `git worktree add ../worktrees/goal-forecast/15-ef-dbcontext -b feat/goal-forecast/15-ef-dbcontext` |
| **Status** | [ ] |
| **Depende de** | TASK-08 |
| **Entregável** | `GoalForecastDbContext` com mapeamentos de `Money`, `GoalPeriod`, `GoalScope` e Global Query Filter por `tenant_id` |
| **Mapeia** | RNF 1, RNF 4, ADR-0001, DD-008, design §6.1 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar `GoalForecastDbContext` (EF Core / Npgsql) com mapeamento da entidade `Goal` para a tabela `goals`. `Money` e `GoalPeriod` como `ValueConverter`/owned types preservando `BIGINT`/`SMALLINT`. Global Query Filter por `tenant_id` como primeira linha de defesa do isolamento multi-tenant (ADR-0001). `bu_id NOT NULL` conforme DD-008.

#### Subtasks

- [ ] **ST-01 — Red:** testes de integração (banco de testes em memória ou Npgsql com contêiner): inserir `Goal` e recuperar com `valor_meta` preservado como `BIGINT`; filtro por `tenant_id` bloqueia acesso de tenant diferente no contexto de aplicação.
- [ ] **ST-02 — Green:** implementar `GoalForecastDbContext` com `DbSet<Goal>`; configurar `ValueConverter` para `Money → BIGINT`, `GoalPeriod → (SMALLINT year, SMALLINT month)`, `GoalScope → (UUID bu_id, UUID? owner_id, GoalScopeKind kind)`; adicionar Global Query Filter `x => x.TenantId == _currentTenantId`.
- [ ] **ST-03 — Refactor:** garantir que `_currentTenantId` vem do principal autenticado injetado, não de configuração estática; verificar que `bu_id` é `NOT NULL` no mapeamento (DD-008).
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes de integração verdes, commit `feat(goal-forecast): GoalForecastDbContext com mapeamentos EF Core e Global Query Filter`, push.

#### Critérios de Aceite

- [ ] `Money` mapeado para `BIGINT` sem conversão para `double` em nenhum ponto.
- [ ] Global Query Filter aplica `tenant_id` em todas as consultas automaticamente.
- [ ] `bu_id` mapeado como `NOT NULL` (DD-008).
- [ ] Inserir e recuperar `Goal` preserva `valor_meta` como `long` exato (PBT-05 infra).

---

### TASK-16 — Migration: tabela goals, UNIQUE, índice parcial BU e RLS policies

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/goal-forecast/16-migration-rls` |
| **Worktree** | `git worktree add ../worktrees/goal-forecast/16-migration-rls -b feat/goal-forecast/16-migration-rls` |
| **Status** | [ ] |
| **Depende de** | TASK-15 |
| **Entregável** | Migration EF Core com tabela `goals`, dois constraints de unicidade, dois índices de consulta e RLS ativa com policy de isolamento |
| **Mapeia** | Req 2, RNF 1, ADR-0001, DD-002, DD-008, design §7 |
| **Camada principal** | Infrastructure |

#### Objetivo

Criar a migration completa que materializa o schema `goals` (design §7): tabela, constraints, índices, índice parcial BU para unicidade com `owner_id IS NULL`, e policies RLS (`ENABLE ROW LEVEL SECURITY`, `FORCE ROW LEVEL SECURITY`, policy por `app.tenant_id`). RLS não é opcional (ADR-0001).

#### Subtasks

- [ ] **ST-01 — Red:** teste de integração com banco real: duas inserções com mesma chave `(tenant_id, bu_id, owner_id, year, month)` resultam em violação de unicidade; inserção com `owner_id = NULL` e mesma `(tenant_id, bu_id, year, month)` também viola; consulta sem `SET app.tenant_id` retorna vazio (RLS bloqueando).
- [ ] **ST-02 — Green:** criar migration EF Core com: tabela `goals` (campos do design §7); UNIQUE composta `(tenant_id, bu_id, owner_id, year, month)` para escopo RESPONSAVEL; índice único parcial `ux_goals_bu_scope ON goals (tenant_id, bu_id, year, month) WHERE owner_id IS NULL`; índices `(tenant_id, bu_id, year, month)` e `(tenant_id, owner_id, year, month)`; blocos SQL de RLS via `migrationBuilder.Sql`.
- [ ] **ST-03 — Refactor:** verificar que `CHECK (valor_meta >= 0)` e `CHECK (month BETWEEN 1 AND 12)` estão na migration; garantir `DEFAULT gen_random_uuid()` para `id`.
- [ ] **ST-04 — Docs:** anotar DD-008 (divergência `bu_id NOT NULL` vs data-model nullable) no corpo da migration como comentário SQL.
- [ ] **ST-05 — Encerramento:** `dotnet ef database update` aplicado com sucesso em banco de integração, testes verdes, commit `feat(goal-forecast): migration goals com UNIQUE índice parcial BU e RLS`, push.

#### Critérios de Aceite

- [ ] Tabela `goals` criada com todos os campos, tipos e constraints do design §7.
- [ ] Índice único parcial `ux_goals_bu_scope` impede duplicata de meta BU com `owner_id IS NULL`.
- [ ] RLS habilitada com `FORCE ROW LEVEL SECURITY`; policy filtra por `app.tenant_id`.
- [ ] Consulta sem contexto de tenant retorna vazio (RLS ativa como segunda camada de defesa).
- [ ] DD-008 anotado na migration.

---

### TASK-17 — GoalRepository: implementação e testes de integração

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/goal-forecast/17-goal-repository` |
| **Worktree** | `git worktree add ../worktrees/goal-forecast/17-goal-repository -b feat/goal-forecast/17-goal-repository` |
| **Status** | [ ] |
| **Depende de** | TASK-16 |
| **Entregável** | `GoalRepository` implementando `IGoalRepository` com testes de integração contra banco real |
| **Mapeia** | Req 1, Req 2, Req 3, RNF 1, design §6.1 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar `GoalRepository` que satisfaz a porta `IGoalRepository` usando `GoalForecastDbContext`. Testes de integração com banco real (Testcontainers Postgres) verificam: persistência, unicidade sob corrida, Global Query Filter, e que mapeamentos de `Money`/`GoalPeriod` preservam valores.

#### Subtasks

- [ ] **ST-01 — Red:** testes de integração com banco real: `FindByKey` retorna null para chave inexistente; `Add` persiste e `FindById` recupera com `valor_meta` inteiro exato; `Update` após `Add` altera `valor_meta` e `updated_at`; `Query` com filtro de `bu_id` retorna só metas da BU.
- [ ] **ST-02 — Green:** implementar `GoalRepository` com `FindByKey`, `FindById`, `Add`, `Update`, `Query`; tratar violação de UNIQUE como retorno de meta existente (upsert semantics para corrida — DD-002).
- [ ] **ST-03 — Refactor:** garantir que `Query` sempre passa pelo Global Query Filter (não usa `IgnoreQueryFilters`); extrair helpers de mapeamento.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes de integração verdes, coverage Infrastructure ≥ 70%, commit `feat(goal-forecast): GoalRepository com testes de integração`, push.

#### Critérios de Aceite

- [ ] `FindByKey` retorna `null` para chave inexistente, `Goal` para chave existente do mesmo tenant.
- [ ] `Add` + `FindById` preservam `valor_meta` como `long` exato.
- [ ] Violação de UNIQUE tratada de forma controlada (não propaga `DbUpdateException` cru).
- [ ] `Query` sempre filtrada por tenant via Global Query Filter.

---

### TASK-18 — Teste de isolamento RLS: cross-tenant bloqueado via API e SQL direto (KPI-06)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `test/goal-forecast/18-rls-isolation` |
| **Worktree** | `git worktree add ../worktrees/goal-forecast/18-rls-isolation -b test/goal-forecast/18-rls-isolation` |
| **Status** | [ ] |
| **Depende de** | TASK-17 |
| **Entregável** | Suite de testes de isolamento cross-tenant que bloqueia o merge se qualquer caso falhar (KPI-06: 100%) |
| **Mapeia** | RNF 1, Req 12, ADR-0001, design §14 |
| **Camada principal** | Tests |

#### Objetivo

Implementar testes de isolamento multi-tenant que verificam a defesa em profundidade (ADR-0001): (1) via API, um tenant não acessa metas de outro tenant; (2) via SQL direto sem `SET app.tenant_id`, a RLS bloqueia o retorno de qualquer linha. Estes testes são bloqueadores de merge (KPI-06: 100% de cobertura dos cenários de vazamento).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de isolamento: tenant A cria meta; tenant B faz GET /goals → 200 com lista vazia (não 404, não dados de A); tenant B faz GET /forecast com `buId` de A → 200 com realizado/pipeline zerados (sem meta — degradação graciosa); SQL direto sem `app.tenant_id` → 0 linhas retornadas pela RLS.
- [ ] **ST-02 — Green:** executar testes contra banco de integração com Testcontainers; configurar dois contextos de tenant distintos; confirmar que Global Query Filter + RLS cobrem ambas as camadas.
- [ ] **ST-03 — Refactor:** garantir que o teste de SQL direto usa conexão sem `SET app.tenant_id` (não via DbContext); cobrir caso de `FORCE ROW LEVEL SECURITY` (bypassa superuser).
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** todos os casos verdes, adicionados ao pipeline de CI como bloqueadores de merge, commit `test(goal-forecast): isolamento RLS cross-tenant KPI-06`, push.

#### Critérios de Aceite

- [ ] Tenant B nunca recebe metas de tenant A via API.
- [ ] SQL direto sem contexto de tenant retorna 0 linhas (RLS ativa).
- [ ] Testes marcados como bloqueadores de merge no CI.
- [ ] KPI-06: 100% dos cenários de vazamento cobertos.

---

### TASK-19 — PipelineForecastReader + circuit breaker + flag pipelineUnavailable

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/goal-forecast/19-pipeline-reader` |
| **Worktree** | `git worktree add ../worktrees/goal-forecast/19-pipeline-reader -b feat/goal-forecast/19-pipeline-reader` |
| **Status** | [ ] |
| **Depende de** | TASK-08, TASK-15 |
| **Entregável** | `PipelineForecastReader` com circuit breaker, timeout e retorno `available=false` em falha |
| **Mapeia** | Req 8, RNF 6, DD-005, DD-007, RISK-GOAL-01, design §6.4 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar `PipelineForecastReader` que satisfaz a porta `IPipelineForecastReader` lendo o read model `ForecastView` in-process do opportunity-pipeline (DD-005). Resiliência obrigatória: timeout curto + circuit breaker; em falha ou timeout retorna `ForecastViewResult { available = false }` em vez de lançar ou retornar zero (DD-007, RNF 6.1 e 6.2).

#### Subtasks

- [ ] **ST-01 — Red:** testes: leitura bem-sucedida retorna `wonTotalCents` e `forecastPonderadoCents` corretos com `available=true`; simulação de timeout → `available=false` sem exceção propagada; simulação de circuit breaker aberto → `available=false` imediato; log estruturado emitido em falha (RNF-6.3).
- [ ] **ST-02 — Green:** implementar `PipelineForecastReader` consultando `ForecastView` in-process; adicionar `Polly` (ou equivalente aprovado) com `TimeoutPolicy` curto e `CircuitBreakerPolicy`; capturar exceções e retornar `available=false`.
- [ ] **ST-03 — Refactor:** garantir que nenhuma exceção interna da leitura do pipeline propaga para o caller; emitir métrica `pipeline_reader_failures_total` em falha.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes verdes, commit `feat(goal-forecast): PipelineForecastReader com circuit breaker e degradação controlada`, push.

#### Critérios de Aceite

- [ ] Timeout na leitura do pipeline retorna `available=false` sem propagar exceção.
- [ ] Circuit breaker aberto retorna `available=false` imediatamente.
- [ ] Leitura bem-sucedida retorna valores em centavos inteiros `long`.
- [ ] Falha gera log estruturado com `correlation_id`, `tenant_id`, `bu_id` e período (RNF-6.3).
- [ ] Métrica `pipeline_reader_failures_total` emitida em falha.

---

### TASK-20 — BuMembershipReader + OutboxDispatcher + PBT-05 round-trip fim a fim

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/goal-forecast/20-membership-outbox` |
| **Worktree** | `git worktree add ../worktrees/goal-forecast/20-membership-outbox -b feat/goal-forecast/20-membership-outbox` |
| **Status** | [ ] |
| **Depende de** | TASK-17 |
| **Entregável** | `BuMembershipReader` satisfazendo `IBuMembershipReader`, `OutboxDispatcher` garantindo atomicidade, e PBT-05 round-trip fim a fim green |
| **Mapeia** | Req 1.4, Req 10, RNF 5, DD-004, PBT-05, design §6.5 e §6.6 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar `BuMembershipReader` (consulta ao contexto organization) e `OutboxDispatcher` (grava `GoalUpdated` na mesma transação da escrita de `goals`). PBT-05 fim a fim: persistir e ler `Goal` com valor arbitrário preserva exatamente o `long`, sem conversão para `double` em nenhuma camada.

#### Subtasks

- [ ] **ST-01 — Red:** testes: owner membro da BU → `true`; owner não membro → `false`; outbox com `GoalUpdated` gravado na mesma transação que `Goal` (rollback reverte ambos); PBT-05 fim a fim com banco real: `valor_meta = X → POST → GET → valor_meta = X` exato para valores arbitrários `long`.
- [ ] **ST-02 — Green:** implementar `BuMembershipReader` consultando a tabela `bu_members` (ou equivalente) do contexto organization in-process; implementar `OutboxDispatcher` gravando evento serializado em `outbox_events` na mesma transação via `DbContext`; serializar `GoalUpdated` preservando `long` sem conversão.
- [ ] **ST-03 — Refactor:** garantir que `OutboxDispatcher` usa `long` na serialização JSON (sem `double`); verificar atomicidade com rollback de teste.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes + PBT-05 fim a fim verdes, commit `feat(goal-forecast): BuMembershipReader OutboxDispatcher e PBT-05 round-trip`, push.

#### Critérios de Aceite

- [ ] `BuMembershipReader` retorna `true`/`false` corretamente para owner membro/não-membro.
- [ ] Rollback de transação reverte tanto `goals` quanto `outbox_events`.
- [ ] PBT-05 fim a fim green: `long` arbitrário preservado exatamente após persitência e leitura via API.
- [ ] Serialização de `valorMeta` no evento usa `long`, sem conversão para `double`.

---

### TASK-21 — Contracts DTOs + schema evento goal.updated.v1

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `feat/goal-forecast/21-contracts-dtos` |
| **Worktree** | `git worktree add ../worktrees/goal-forecast/21-contracts-dtos -b feat/goal-forecast/21-contracts-dtos` |
| **Status** | [ ] |
| **Depende de** | TASK-08 |
| **Entregável** | DTOs de request/response e schema `goal.updated.v1` no projeto Contracts, com valores monetários como `long` |
| **Mapeia** | RNF 4, design §8 e §9.1 |
| **Camada principal** | Contracts |

#### Objetivo

Definir todos os DTOs de request/response (design §8) e o schema do evento de integração `goal.updated.v1` (design §9.1) no projeto `GoalForecast.Contracts`. Todos os valores monetários como `long` (centavos inteiros). Campos anuláveis onde o design especifica (degradação graciosa, pipeline indisponível).

#### Subtasks

- [ ] **ST-01 — Red:** testes que verificam que `GoalDto.valorMeta` é `long` (não `decimal`, não `double`); `ForecastPanelResponse.valorMeta` é `long?` (anulável); `GoalUpdatedEvent.valorMetaNovo` é `long`.
- [ ] **ST-02 — Green:** criar DTOs: `CreateOrUpdateGoalRequest`, `GoalDto`, `GoalListResponse`, `ForecastPanelResponse` (campos anuláveis para meta/gap/pct, `pipelineUnavailable: bool`), `ForecastAggregateResponse`, `DigestBlockResponse { present, valorMeta?, realizado?, gap?, pipelineDisponivel? }`, `GoalUpdatedEvent`.
- [ ] **ST-03 — Refactor:** garantir que nenhum DTO usa `decimal` ou `double` para valor monetário; `over-posting` protegido (nenhum DTO aceita `tenantId` ou `id`).
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes verdes, testes de arquitetura passam (Contracts sem dependências externas), commit `feat(goal-forecast): Contracts DTOs e schema goal.updated.v1`, push.

#### Critérios de Aceite

- [ ] Todos os valores monetários em `long` (centavos inteiros).
- [ ] `ForecastPanelResponse.valorMeta`, `gap`, `pctAtingimento` são anuláveis.
- [ ] `GoalUpdatedEvent` com todos os campos do design §9.1.
- [ ] Nenhum DTO aceita `tenantId` ou `id` pelo corpo (over-posting protegido).
- [ ] Projeto Contracts sem dependências externas (testes de arquitetura passam).

---

### TASK-22 — GoalsController: POST /goals + PUT /goals/{id}

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `feat/goal-forecast/22-goals-write-endpoints` |
| **Worktree** | `git worktree add ../worktrees/goal-forecast/22-goals-write-endpoints -b feat/goal-forecast/22-goals-write-endpoints` |
| **Status** | [ ] |
| **Depende de** | TASK-21, TASK-09 |
| **Entregável** | Endpoints de escrita com códigos HTTP corretos, catálogo de erros `GF-ERR-001..007` e testes de contrato |
| **Mapeia** | Req 1, Req 2, Req 4, design §8.1 e §8.2 |
| **Camada principal** | Api |

#### Objetivo

Implementar `GoalsController` com os dois endpoints de escrita: `POST /api/v1/goals` (upsert, retorna 201 ou 200) e `PUT /api/v1/goals/{id}` (atualiza `valor_meta`, retorna 200). Catálogo completo de erros do design §12 referenciado em cada endpoint.

#### Subtasks

- [ ] **ST-01 — Red:** testes de integração de API: POST com dados válidos → 201 + `GoalDto`; POST com mesma chave → 200 (upsert); POST com `valorMeta` negativo → 400 `GF-ERR-001`; POST com `month=13` → 400 `GF-ERR-002`; POST com RESPONSAVEL sem `ownerId` → 400 `GF-ERR-003`; POST com owner não membro → 422 `GF-ERR-004`; POST sem autorização → 403 `GF-ERR-006`; PUT com `id` inexistente → 404 `GF-ERR-007`.
- [ ] **ST-02 — Green:** implementar `GoalsController` com `[Authorize]`; derivar `tenantId` do token JWT (nunca do body); mapear `CreateOrUpdateGoalRequest` → `CreateOrUpdateGoalCommand`; capturar exceções de domínio e mapear para respostas HTTP com código de erro `GF-ERR-*`.
- [ ] **ST-03 — Refactor:** extrair mapeamento de exceção de domínio para filtro global ou middleware; garantir que `tenantId` é ignorado se presente no body.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes verdes, coverage Api ≥ 80%, commit `feat(goal-forecast): GoalsController POST e PUT com catálogo de erros`, push.

#### Critérios de Aceite

- [ ] POST válido → 201; POST com mesma chave → 200 (upsert idempotente).
- [ ] Cada erro do catálogo `GF-ERR-001..007` coberto por teste de API.
- [ ] `tenantId` derivado do token JWT, nunca do body.
- [ ] `valorMeta` retornado como `long` inteiro de centavos no `GoalDto`.
- [ ] Negação de autorização retorna 403 sem revelar existência da meta.

---

### TASK-23 — GoalsController: GET /goals + testes de RBAC e catálogo de erros

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `feat/goal-forecast/23-goals-read-endpoint` |
| **Worktree** | `git worktree add ../worktrees/goal-forecast/23-goals-read-endpoint -b feat/goal-forecast/23-goals-read-endpoint` |
| **Status** | [ ] |
| **Depende de** | TASK-22, TASK-11 |
| **Entregável** | `GET /api/v1/goals` com escopo de visibilidade RBAC verificado, paginação e testes de isolamento por papel |
| **Mapeia** | Req 3, Req 12, RNF 2, design §8.3 |
| **Camada principal** | Api |

#### Objetivo

Completar `GoalsController` com `GET /api/v1/goals`. O endpoint aplica filtros de RBAC (Vendedor vê só as suas; Gestor vê sua BU; Admin/Executivo veem o tenant) e paginação com `page`, `pageSize` (default 50, máx 200).

#### Subtasks

- [ ] **ST-01 — Red:** testes de API: Vendedor recebe apenas metas com `ownerId` próprio; Gestor não vê BU diferente da sua; Admin vê todas as BUs; filtros `buId`, `ownerId`, `year`, `month` funcionam; `pageSize > 200` → 400; `valorMeta` retornado como `long`.
- [ ] **ST-02 — Green:** adicionar action `GET` no `GoalsController`; mapear query params para `ListGoalsQuery`; retornar `GoalListResponse { items, page, pageSize, total }`.
- [ ] **ST-03 — Refactor:** garantir que filtro de tenant é aplicado antes dos filtros de RBAC; ordenação padrão por `year, month, bu_id`.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes verdes, commit `feat(goal-forecast): GoalsController GET com RBAC e paginação`, push.

#### Critérios de Aceite

- [ ] Escopo de visibilidade RBAC aplicado corretamente para todos os quatro papéis.
- [ ] `pageSize` máximo de 200 validado com erro 400.
- [ ] `valorMeta` em `long` na resposta.
- [ ] Negação de autorização não revela metas fora do escopo (403 genérico).

---

### TASK-24 — ForecastController: GET /forecast + GET /forecast/aggregate

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `feat/goal-forecast/24-forecast-endpoints` |
| **Worktree** | `git worktree add ../worktrees/goal-forecast/24-forecast-endpoints -b feat/goal-forecast/24-forecast-endpoints` |
| **Status** | [ ] |
| **Depende de** | TASK-23, TASK-12, TASK-13 |
| **Entregável** | `ForecastController` com painel comparativo e agregação trimestral/anual, nunca 404, nunca NaN |
| **Mapeia** | Req 5, Req 6, Req 7, PBT-03, PBT-04, design §8.4 e §8.5 |
| **Camada principal** | Api |

#### Objetivo

Implementar `ForecastController` com `GET /api/v1/forecast` (painel comparativo "Direção") e `GET /api/v1/forecast/aggregate` (soma derivada por trimestre ou ano). O painel é operação total: retorna 200 para qualquer período/escopo válido, com ou sem meta (PBT-04). Pipeline indisponível resulta em `pipelineUnavailable=true` no body, nunca em erro HTTP.

#### Subtasks

- [ ] **ST-01 — Red:** testes de API: GET /forecast sem meta → 200 com `valorMeta=null`, `gap=null`, `pctAtingimento=null`, `realizado` presente; GET /forecast com pipeline indisponível → 200 com `pipelineUnavailable=true`; GET /forecast/aggregate com `granularity=quarter` → soma dos 3 meses; `granularity` inválido → 400; meses ausentes na agregação → contribuem com zero.
- [ ] **ST-02 — Green:** implementar `ForecastController` com ações `GetPanel` e `GetAggregate`; mapear query params; retornar `ForecastPanelResponse` e `ForecastAggregateResponse`; garantir que nenhuma exceção da camada de application resulta em 404 ou 500 para o painel (apenas 400/403).
- [ ] **ST-03 — Refactor:** garantir que `pct_atingimento` nunca aparece como `NaN` na serialização JSON; extrair helper de validação de `granularity`.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes verdes, coverage Api ≥ 80%, commit `feat(goal-forecast): ForecastController GET forecast e aggregate`, push.

#### Critérios de Aceite

- [ ] GET /forecast retorna 200 para qualquer período/escopo válido (nunca 404).
- [ ] Sem meta: `valorMeta`, `gap`, `pctAtingimento` nulos no JSON.
- [ ] Pipeline indisponível: `pipelineUnavailable=true`, sem erro HTTP 5xx.
- [ ] GET /forecast/aggregate retorna `valorMetaAgregado` como `long` inteiro.
- [ ] `pctAtingimento` nunca serializado como `NaN`.

---

### TASK-25 — InternalGoalsController: GET /internal/goals/digest-block (mTLS)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `feat/goal-forecast/25-internal-digest-endpoint` |
| **Worktree** | `git worktree add ../worktrees/goal-forecast/25-internal-digest-endpoint -b feat/goal-forecast/25-internal-digest-endpoint` |
| **Status** | [ ] |
| **Depende de** | TASK-24, TASK-14 |
| **Entregável** | `InternalGoalsController` em rota `/api/v1/internal/goals/digest-block` segregada, com autenticação de serviço e `present=false` para ausência de meta |
| **Mapeia** | Req 9, RN-018, RN-029, design §8.6 e §10 |
| **Camada principal** | Api |

#### Objetivo

Implementar `InternalGoalsController` para consumo exclusivo do Digest worker (BC-06). A rota é segregada em `/internal`, autenticada por mTLS ou escopo de serviço (não exposta publicamente). Retorna `{ present: true, ... }` com meta ou `{ present: false }` sem meta, para que o Digest omita o bloco completamente.

#### Subtasks

- [ ] **ST-01 — Red:** testes: chamador interno autorizado com meta → 200 `present=true` com campos em centavos; sem meta → 200 `present=false` sem campos monetários; chamador sem autorização de serviço → 403; rota não acessível externamente (validar prefixo `/internal` bloqueado no gateway).
- [ ] **ST-02 — Green:** implementar `InternalGoalsController` com `[Authorize(Policy = "ServiceScope")]`; mapear query params `buId`, `year`, `month`; retornar `DigestBlockResponse`.
- [ ] **ST-03 — Refactor:** garantir que rota `/internal` não é mapeada pelo Swagger público; segregar registro de rota.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes verdes, commit `feat(goal-forecast): InternalGoalsController digest-block com autenticação de serviço`, push.

#### Critérios de Aceite

- [ ] `present=false` quando não há meta — sem campos monetários no JSON.
- [ ] `present=true` com todos os campos monetários em `long` quando há meta.
- [ ] Acesso sem autorização de serviço retorna 403.
- [ ] Rota não exposta no Swagger público.

---

### TASK-26 — Testes de contrato: schema goal.updated.v1 + porta ForecastView

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `test/goal-forecast/26-contract-tests` |
| **Worktree** | `git worktree add ../worktrees/goal-forecast/26-contract-tests -b test/goal-forecast/26-contract-tests` |
| **Status** | [ ] |
| **Depende de** | TASK-21, TASK-20 |
| **Entregável** | Testes de contrato do schema `goal.updated.v1` e do contrato da porta `IPipelineForecastReader` |
| **Mapeia** | Req 8, Req 10, design §9 e §13 |
| **Camada principal** | Tests |

#### Objetivo

Verificar que o schema do evento `goal.updated.v1` publicado no outbox é compatível com os consumidores declarados (audit-log, digest, reporting) e que a porta `IPipelineForecastReader` respeita o contrato definido em `ForecastViewQuery`/`ForecastViewResult`. Mudanças de schema quebram testes de contrato.

#### Subtasks

- [ ] **ST-01 — Red:** testes de contrato: desserializar `GoalUpdatedEvent` serializado pelo outbox e verificar presença de todos os campos do design §9.1; campo `valorMetaNovo` como `long` (não `double`); schema JSON válido com campos obrigatórios/opcionais corretos. Testes da porta: `ForecastViewResult` tem `available`, `wonTotalCents`, `forecastPonderadoCents` como `long`.
- [ ] **ST-02 — Green:** implementar testes de contrato com serialização real (System.Text.Json ou equivalente); verificar que `long` grande não é convertido para `double` na serialização.
- [ ] **ST-03 — Refactor:** garantir que testes de contrato são executados em CI separadamente dos testes de integração.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes verdes, commit `test(goal-forecast): testes de contrato goal.updated.v1 e porta ForecastView`, push.

#### Critérios de Aceite

- [ ] Schema `goal.updated.v1` validado com todos os campos canônicos (design §9.1).
- [ ] `valorMetaNovo` serializado como `long`, sem conversão para `double`.
- [ ] Contrato da porta `IPipelineForecastReader` coberto por testes de contrato.
- [ ] Mudança de campo obrigatório no schema quebra o CI.

---

### TASK-27 — Testes de segurança: RBAC, anti-enumeração, cross-tenant e cross-BU

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `test/goal-forecast/27-security-tests` |
| **Worktree** | `git worktree add ../worktrees/goal-forecast/27-security-tests -b test/goal-forecast/27-security-tests` |
| **Status** | [ ] |
| **Depende de** | TASK-24, TASK-18 |
| **Entregável** | Suite de segurança cobrindo RBAC por papel, anti-enumeração, cross-tenant e cross-BU em todos os endpoints |
| **Mapeia** | Req 12, RNF 2, design §10 e §12 |
| **Camada principal** | Tests |

#### Objetivo

Implementar testes de segurança que verificam: (1) RBAC por papel em todos os endpoints (Vendedor, Gestor, Executivo, Admin); (2) anti-enumeração — negação de autorização retorna 403 sem distinguir "não existe" de "sem permissão"; (3) cross-tenant — tenant B não acessa dados de tenant A; (4) cross-BU — Gestor de BU A não acessa BU B no mesmo tenant.

#### Subtasks

- [ ] **ST-01 — Red:** testes: Vendedor fazendo POST /goals → 403; Gestor fazendo POST /goals de BU diferente → 403; Executivo fazendo POST /goals → 403; cross-tenant GET /goals → 200 lista vazia (não 403 nem 404 revelando existência); GET /goals/{id} de meta de outro tenant → 403 genérico; GET /forecast de buId de outro tenant → 200 com degradação graciosa.
- [ ] **ST-02 — Green:** executar testes com múltiplos tokens JWT de papéis/tenants distintos contra stack de integração.
- [ ] **ST-03 — Refactor:** garantir que nenhum endpoint retorna 404 para recurso de outro tenant — sempre 403 ou lista vazia.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** todos os testes verdes, commit `test(goal-forecast): testes de segurança RBAC anti-enumeração cross-tenant`, push.

#### Critérios de Aceite

- [ ] Cada papel (Vendedor, Gestor, Executivo, Admin) testado em write e read.
- [ ] 403 não distingue "não existe" de "sem permissão" (anti-enumeração RNF-2.3).
- [ ] Cross-tenant retorna lista vazia ou degradação graciosa, nunca dados do tenant alvo.
- [ ] Cross-BU dentro do mesmo tenant bloqueado para Gestor.

---

### TASK-28 — Observabilidade: logs estruturados, métricas, traces e alertas

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `feat/goal-forecast/28-observability` |
| **Worktree** | `git worktree add ../worktrees/goal-forecast/28-observability -b feat/goal-forecast/28-observability` |
| **Status** | [ ] |
| **Depende de** | TASK-27 |
| **Entregável** | Logs estruturados com campos obrigatórios, métricas `goals_created_total`/`goals_updated_total` e métricas de circuit breaker |
| **Mapeia** | RNF 7, design §11 |
| **Camada principal** | Infrastructure |

#### Objetivo

Verificar e complementar a observabilidade do módulo: logs estruturados com `correlation_id`, `tenant_id`, `bu_id`, período e ação em toda operação (RNF 7.1); métricas de negócio `goals_created_total` e `goals_updated_total` (RNF 7.2); métricas de circuit breaker `pipeline_reader_failures_total` e `pipeline_circuit_open_total`; span por command/query com `correlation_id`.

#### Subtasks

- [ ] **ST-01 — Red:** testes: após POST /goals criar meta, métrica `goals_created_total` incrementada; após PUT /goals/{id}, `goals_updated_total` incrementada; falha do pipeline reader incrementa `pipeline_reader_failures_total`; log de escrita contém `correlation_id` e `tenant_id`.
- [ ] **ST-02 — Green:** adicionar `Counter` para `goals_created_total` e `goals_updated_total` no handler; emitir `pipeline_reader_failures_total` e `pipeline_circuit_open_total` no reader; garantir que `LoggingBehavior` inclui todos os campos obrigatórios do RNF 7.1; adicionar span por command/query.
- [ ] **ST-03 — Refactor:** garantir que logs não expõem `valorMeta` como texto sensível (RNF-7.3); rever campos de span para incluir `tenant_id` como atributo.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes de métricas verdes, commit `feat(goal-forecast): observabilidade logs métricas e traces`, push.

#### Critérios de Aceite

- [ ] `goals_created_total` e `goals_updated_total` incrementadas corretamente por operação.
- [ ] `pipeline_reader_failures_total` incrementada em cada falha de leitura do pipeline.
- [ ] Logs de operação incluem `correlation_id`, `tenant_id`, `bu_id`, período e ação.
- [ ] Logs não expõem `valorMeta` como texto sensível em campos de diagnóstico.

---

### TASK-29 — Baseline de performance do painel (p95 ≤ 3.000 ms)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `test/goal-forecast/29-performance-baseline` |
| **Worktree** | `git worktree add ../worktrees/goal-forecast/29-performance-baseline -b test/goal-forecast/29-performance-baseline` |
| **Status** | [ ] |
| **Depende de** | TASK-28 |
| **Entregável** | Resultado documentado do baseline de p95 do GET /forecast para volume de referência (até 12 meses, ~2.000 oportunidades/tenant) |
| **Mapeia** | RNF 3, design §15 |
| **Camada principal** | Tests |

#### Objetivo

Medir o p95 de latência do `GET /api/v1/forecast` para o volume de referência definido no RNF-3.1 (até 12 meses agregados, ~2.000 oportunidades por tenant) e registrar o resultado. Se p95 > 3.000 ms, registrar como risco e acionar investigação dos índices (design §15).

#### Subtasks

- [ ] **ST-01 — Red:** preparar dataset de benchmark: tenant com ~2.000 oportunidades distribuídas em 12 meses; 50 metas cadastradas; executar script de carga e medir latência.
- [ ] **ST-02 — Green:** executar benchmark com NBomber, k6 ou equivalente aprovado; coletar p50, p95, p99; verificar se p95 ≤ 3.000 ms; revisar plano de query com `EXPLAIN ANALYZE` se p95 > 3.000 ms.
- [ ] **ST-03 — Refactor:** se p95 > 3.000 ms, verificar índices `(tenant_id, bu_id, year, month)` e `(tenant_id, owner_id, year, month)`; documentar ajuste aplicado.
- [ ] **ST-04 — Docs:** registrar resultado do baseline no design.md §15 como comentário de resultados.
- [ ] **ST-05 — Encerramento:** resultado documentado, commit `test(goal-forecast): baseline de performance painel p95`, push.

#### Critérios de Aceite

- [ ] Baseline executado com volume de referência do RNF-3.1.
- [ ] Resultado de p50, p95 e p99 documentado.
- [ ] Se p95 > 3.000 ms: causa raiz identificada, ajuste aplicado ou risco registrado com justificativa.
- [ ] Índices críticos verificados com `EXPLAIN ANALYZE`.

---

### TASK-30 — DoD final: reconciliar DD-008, coverage gates e README sincronizado

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `docs/goal-forecast/30-dod-final` |
| **Worktree** | `git worktree add ../worktrees/goal-forecast/30-dod-final -b docs/goal-forecast/30-dod-final` |
| **Status** | [ ] |
| **Depende de** | TASK-27, TASK-28, TASK-29 |
| **Entregável** | DD-008 reconciliado com data-model, coverage gates verificados, README do módulo sincronizado, DoD assinado |
| **Mapeia** | DD-008, design §19, todos os requisitos |
| **Camada principal** | Docs |

#### Objetivo

Fechar o módulo: reconciliar a divergência `bu_id NOT NULL` com a data-model (DD-008), verificar que todos os coverage gates foram atingidos, sincronizar o README do módulo com estado final das ondas e TASKs, e assinar o Definition of Done (design §19).

#### Subtasks

- [ ] **ST-01 — Red:** verificar coverage gates: Domain ≥ 95%, Application ≥ 85%, Infrastructure ≥ 70%, Api ≥ 80%; listar qualquer gate não atingido como bloqueador.
- [ ] **ST-02 — Green:** abrir PR ou issue na data-model para reconciliar `bu_id NOT NULL` (DD-008); atingir gates faltantes ou registrar justificativa formal; verificar que todos os 30 itens do DoD do design §19 estão atendidos.
- [ ] **ST-03 — Refactor:** atualizar README do módulo (`docs/product/modules/goal-forecast/README.md`) com status final das TASKs, ondas e links para design.md/requirements.md aprovados.
- [ ] **ST-04 — Docs:** marcar DoD como concluído no design.md §19; registrar data de conclusão no tasks.md (atualizar Status para `Aprovado para desenvolvimento` após revisão humana).
- [ ] **ST-05 — Encerramento:** todos os gates verdes ou justificados, commit `docs(goal-forecast): DoD final coverage gates e reconciliação DD-008`, push.

#### Critérios de Aceite

- [ ] Coverage gates atendidos: Domain ≥ 95%, Application ≥ 85%, Infrastructure ≥ 70%, Api ≥ 80%.
- [ ] DD-008 reconciliado com a data-model (PR aberto ou issue criada).
- [ ] README do módulo sincronizado com estado final.
- [ ] Todos os 13 itens do DoD do design §19 marcados como concluídos ou com justificativa formal.
- [ ] Matriz de rastreabilidade completa e sem origens sem TASK.

---

## 5. Matriz de Rastreabilidade

| Origem | Descrição | TASKs | Status |
|--------|-----------|-------|--------|
| Req 1 | Cadastrar meta mensal em centavos inteiros | TASK-03, TASK-05, TASK-09, TASK-22 | [ ] |
| Req 2 | Unicidade por escopo e período (upsert idempotente) | TASK-05, TASK-09, TASK-16, TASK-17 | [ ] |
| Req 3 | Consultar metas por período e escopo | TASK-11, TASK-23 | [ ] |
| Req 4 | Atualizar valor_meta existente | TASK-05, TASK-09, TASK-22 | [ ] |
| Req 5 | Painel comparativo realizado vs meta vs pipeline | TASK-12, TASK-24 | [ ] |
| Req 6 | Degradação graciosa sem meta cadastrada | TASK-12, TASK-24, TASK-30 | [ ] |
| Req 7 | Agregação trimestral e anual derivada | TASK-06, TASK-13, TASK-24 | [ ] |
| Req 8 | Derivar realizado e pipeline do opportunity-pipeline | TASK-08, TASK-19 | [ ] |
| Req 9 | Bloco de metas para o Digest com sinal de ausência | TASK-14, TASK-25 | [ ] |
| Req 10 | Auditoria imutável de toda escrita de meta | TASK-06, TASK-10, TASK-20, TASK-26 | [ ] |
| Req 11 | Projeção por data de fechamento (Fase 2) | Fora do escopo do MVP — não possui TASK | N/A |
| Req 12 | Isolamento por tenant e RBAC por escopo de visibilidade | TASK-07, TASK-10, TASK-11, TASK-18, TASK-27 | [ ] |
| RNF 1 | Isolamento multi-tenant com defesa em profundidade | TASK-15, TASK-16, TASK-18 | [ ] |
| RNF 2 | RBAC verificado em todo endpoint | TASK-07, TASK-10, TASK-23, TASK-27 | [ ] |
| RNF 3 | Latência do painel p95 ≤ 3.000 ms | TASK-29 | [ ] |
| RNF 4 | Integridade monetária em centavos inteiros | TASK-03, TASK-15, TASK-21, TASK-20 | [ ] |
| RNF 5 | Auditoria imutável via outbox append-only | TASK-10, TASK-20, TASK-26 | [ ] |
| RNF 6 | Resiliência à indisponibilidade do opportunity-pipeline | TASK-19, TASK-12 | [ ] |
| RNF 7 | Observabilidade: logs estruturados e métricas | TASK-10, TASK-28 | [ ] |
| PBT-01 | Idempotência do upsert de meta | TASK-09 | [ ] |
| PBT-02 | Soma exata de agregação trimestral e anual | TASK-06, TASK-13 | [ ] |
| PBT-03 | Invariantes do painel: gap e pct_atingimento | TASK-12 | [ ] |
| PBT-04 | Consulta do painel é função total (graceful degradation) | TASK-12 | [ ] |
| PBT-05 | Round-trip monetário sem perda em centavos inteiros | TASK-03, TASK-20 | [ ] |
| DD-001 | Clean Architecture em cinco projetos com CQRS leve | TASK-01, TASK-02 | [ ] |
| DD-002 | Upsert idempotente com índice parcial BU | TASK-09, TASK-16, TASK-17 | [ ] |
| DD-003 | Agregações derivadas, nunca persistidas | TASK-06, TASK-13 | [ ] |
| DD-004 | Membership owner-BU validada na aplicação | TASK-08, TASK-09, TASK-20 | [ ] |
| DD-005 | Leitura do pipeline via porta in-process | TASK-08, TASK-19 | [ ] |
| DD-006 | Degradação graciosa com meta nula como resultado total | TASK-12, TASK-24 | [ ] |
| DD-007 | Circuit breaker e flag pipelineUnavailable | TASK-19, TASK-12 | [ ] |
| DD-008 | bu_id NOT NULL divergindo da data-model | TASK-15, TASK-16, TASK-30 | [ ] |
| ADR-0001 | Isolamento multi-tenant defesa em profundidade | TASK-02, TASK-15, TASK-16, TASK-18 | [ ] |

---

## 6. Coverage Gates

| Camada | Gate | Tipo de teste esperado | TASK de verificação |
|--------|------|------------------------|---------------------|
| Domain | ≥ 95% | Unitários das invariantes de `Goal`, `Money`, `GoalPeriod`, `GoalScope`; PBTs PBT-02 e PBT-05 | TASK-03, TASK-04, TASK-05, TASK-06, TASK-07 |
| Application | ≥ 85% | Unitários de handlers, behaviors, policies, validators; PBTs PBT-01, PBT-03, PBT-04 | TASK-09, TASK-10, TASK-11, TASK-12, TASK-13, TASK-14 |
| Infrastructure | ≥ 70% | Integração com banco real (Testcontainers), repositório, leitores, circuit breaker, outbox | TASK-15, TASK-17, TASK-19, TASK-20 |
| Api | ≥ 80% | Contrato de endpoints, catálogo de erros GF-ERR-*, RBAC por papel, degradação graciosa | TASK-22, TASK-23, TASK-24, TASK-25 |
| Architecture | 100% das regras | Dependências entre camadas, fronteiras Domain/Contracts puras | TASK-02 |
| Isolamento RLS | 100% dos cenários | Cross-tenant via API e SQL direto; KPI-06 bloqueador de merge | TASK-18 |
| Segurança | Cobertura por cenário | RBAC por papel, anti-enumeração, cross-BU | TASK-27 |
| Contrato | Schema completo | `goal.updated.v1` e porta `ForecastView` | TASK-26 |

Regras:

- Coverage gate não substitui qualidade de teste: um teste de PBT verde vale mais do que dez testes parametrizados rígidos.
- Testes de arquitetura são obrigatórios e bloqueadores de merge (TASK-02).
- Testes de isolamento RLS são obrigatórios e bloqueadores de merge (TASK-18, KPI-06).
- Gate de Domain e Application verificados em cada subtask Encerramento das TASKs da onda correspondente.
- Gates de Infrastructure e Api verificados na TASK-30.

---

## 7. Critérios de Encerramento

### Encerramento de TASK

Uma TASK só pode ser marcada como `[X]` quando:

- Todas as subtasks concluídas.
- Testes aplicáveis locais verdes.
- Coverage gate da camada atendido ou justificativa formal registrada.
- Lint/format executado (quando aplicável).
- Nenhum warning novo de compilação introduzido.
- Commit em Conventional Commits realizado.
- Push da branch realizado.
- Documentação atualizada (quando aplicável).

### Encerramento de Onda

Uma onda só pode ser considerada concluída quando:

- Todas as TASKs da onda estiverem `[X]`.
- CI verde na branch da onda.
- Conflitos resolvidos.
- PR da onda aberto, aprovado ou mergeado conforme regra do projeto.
- Riscos da onda tratados ou registrados com justificativa.

### Encerramento do Módulo

O módulo só pode ser considerado pronto quando:

- Todas as seis ondas concluídas.
- Matriz de rastreabilidade completa: toda origem (Req, RNF, PBT, DD, ADR) com TASK correspondente.
- `requirements.md`, `design.md` e `tasks.md` consistentes entre si e sincronizados.
- Todos os PBTs (PBT-01 a PBT-05) verdes.
- Isolamento cross-tenant verificado em CI (KPI-06: 100%).
- RLS ativa e testada com banco real.
- `goal.updated.v1` publicado via outbox com schema correto.
- Logs estruturados e métricas `goals_created_total`/`goals_updated_total` emitidas.
- Painel p95 medido e documentado.
- DD-008 reconciliado com a data-model.
- DoD do design §19 assinado (todos os 13 itens).
- README do módulo sincronizado com estado final.

---

## 8. Riscos de Execução

| Código | Risco | Impacto | Onda afetada | Mitigação na execução |
|--------|-------|---------|--------------|----------------------|
| RISK-GOAL-01 | Indisponibilidade do opportunity-pipeline durante desenvolvimento | Impossibilidade de testar o painel com dados reais | Onda 4 | Usar mock/stub da porta `IPipelineForecastReader` nas ondas 2-3; implementar reader real na onda 4 |
| RISK-GOAL-02 | Meta duplicada por corrida em testes de carga | Unicidade violada | Onda 4 | UNIQUE + índice parcial + tratamento de conflito no repositório (DD-002); testar com Testcontainers |
| RISK-GOAL-03 | Vazamento cross-tenant se Global Query Filter for desabilitado | Violação sev-1 | Ondas 4-5 | Teste TASK-18 bloqueador de merge; RLS como segunda camada (ADR-0001) |
| RISK-GOAL-04 | Divergência DD-008 (`bu_id` nullable na data-model) | Inconsistência entre módulos | Onda 4-6 | Anotar na migration (TASK-16); reconciliar na TASK-30 antes do merge final |
| RISK-GOAL-05 | `long` serializado como `double` em JSON | Perda de precisão monetária | Onda 5 | PBT-05 fim a fim (TASK-20); testes de contrato (TASK-26) bloqueiam regressão |
| RISK-EXEC-01 | Leitura do `ForecastView` in-process acoplada ao build de opportunity-pipeline | Falha de compilação em mudança de interface | Onda 4 | Porta `IPipelineForecastReader` desacopla logicamente; teste de contrato garante compatibilidade |

---

## 9. Referências

| Documento | Relação |
|-----------|---------|
| `docs/product/modules/goal-forecast/requirements.md` v0.1.0 | Base funcional (Req 1-12, RNF 1-7, PBT-01-05) — origem de toda TASK |
| `docs/product/modules/goal-forecast/design.md` v0.1.0 | Base técnica (DD-001-008, schema, APIs, eventos, comportamentos) |
| `docs/product/adr/0001-isolamento-multi-tenant-defesa-em-profundidade.md` | Decisão vinculante de RLS obrigatória — TASK-16 e TASK-18 |
| `docs/product/trd/trd.md` § goal-forecast, § ForecastView, § eventos | Endpoints, read model, topic `azim-goals` |
| `docs/product/data-model/data-model.md` § Goal & Forecast (BC-05) | Tabela `goals`; divergência DD-008 reconciliada em TASK-30 |
| `.forge/rules/architecture/clean-architecture.md` | Regras de dependência entre camadas — TASK-01 e TASK-02 |
| `.forge/rules/domain/money-as-cents.md` | Integridade monetária em centavos inteiros — TASK-03 |
| `.forge/rules/domain/audit-immutability.md` | Auditoria append-only — TASK-10 e TASK-20 |
| `.forge/rules/testing/tdd.md` | Ciclo Red-Green-Refactor obrigatório em toda TASK |
| `.forge/rules/testing/quality-gates.md` | Coverage gates por camada — TASK-30 |
| `docs/product/modules/opportunity-pipeline/` | Origem de `realizado` e `forecast_ponderado` — porta TASK-08, reader TASK-19 |
| `docs/product/modules/digest/` | Consumidor do bloco de metas — TASK-14 e TASK-25 |
