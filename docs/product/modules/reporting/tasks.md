# Tasks — REPORT — Reporting (Relatórios)

- Versão: 0.1.1
- Data: 2026-06-14
- Status: Aprovado para desenvolvimento
- Referência base requirements: docs/product/modules/reporting/requirements.md v0.1.0
- Referência base design: docs/product/modules/reporting/design.md v0.1.0
- ADRs aplicáveis: ADR-0001 (isolamento multi-tenant em defesa em profundidade — `tenant_id` + EF Core Global Query Filter + RLS com `security_invoker` nas views)
- Rules aplicáveis: `.forge/rules/architecture/clean-architecture.md`, `.forge/rules/architecture/ddd.md`, `.forge/rules/architecture/api-and-contracts.md`, `.forge/rules/architecture/observability.md`, `.forge/rules/architecture/jwt-permissions.md`, `.forge/rules/domain/money-as-cents.md`, `.forge/rules/conventions/database-naming.md`, `.forge/rules/conventions/language-policy.md`, `.forge/rules/testing/tdd.md`, `.forge/rules/testing/quality-gates.md`

## Histórico de Versões

| Versão | Data | Status | Descrição da alteração |
|--------|------|--------|------------------------|
| 0.1.0 | 2026-06-11 | Rascunho para revisão | Criação inicial do plano de tasks a partir de requirements.md v0.1.0 e design.md v0.1.0 |
| 0.1.1 | 2026-06-14 | Aprovado para desenvolvimento | Aprovação humana (HITL #1); execução via `/forge:coding-loop` autônomo (6 ondas). |

---

## 1. Convenções de Implementação

### 1.1 TDD-first

Toda implementação com lógica verificável deve seguir o ciclo:

1. Red — escrever teste que falha
2. Green — implementar o mínimo para passar
3. Refactor — melhorar sem alterar comportamento

Nenhuma implementação de query, handler, view de read model, endpoint, contrato ou integração deve ser considerada concluída sem teste correspondente.

### 1.2 Property-Based Testing

PBT é obrigatório para invariantes matemáticas, round-trip, anti-enumeração e conservação de centavos. Como este módulo é read side puro sem escrita de negócio, os PBTs concentram-se em aritmética de leitura, isolamento e fidelidade de export.

| PBT | Tipo | Requisito | TASK |
|-----|------|-----------|------|
| PBT-01 | Invariante — snapshot de comissão imutável após mudança de percentual do parceiro | Req 4, RNF 2 | TASK-10 |
| PBT-02 | Invariante — conservação da soma de comissão em centavos inteiros (sem perda/duplicação) | Req 4 | TASK-03, TASK-10 |
| PBT-03 | Anti-enumeração — isolamento cross-tenant via RLS (`security_invoker`) | Req 8, Req 7 | TASK-18 |
| PBT-04 | Invariante — soma dos percentuais por canal = 100% em basis points inteiros | Req 3 | TASK-03, TASK-09 |
| PBT-05 | Round-trip — linhas e totais do relatório equivalentes ao conteúdo do CSV exportado | Req 5 | TASK-11 |

Biblioteca de referência: FsCheck (FsCheck.Xunit) ou equivalente aprovado no TRD.

### 1.3 Bite-sized Tasks

- Cada subtask deve ser estimada em menos de 2 horas.
- Cada TASK deve ser completável em até 2 dias.
- TASK maior que 2 dias deve ser quebrada.

### 1.4 Branch Model

```text
<tipo>/reporting/<NN>-<slug>
```

Exemplos:

```text
feat/reporting/01-bootstrap-solution
test/reporting/18-rls-isolation-pbt03
feat/reporting/21-reporting-controller
```

### 1.5 Git Worktree

```sh
git worktree add ../worktrees/reporting/<NN>-<slug> -b <branch>
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

```text
TASK-NN — <título>      ← unidade atômica de invocação do task-coder
  ST-MM — <subtask>     ← etapas TDD internas; numeração reinicia por TASK
```

Onda é atributo no header da TASK — nunca entra no ID.

---

## 2. Status Geral

| TASK | Título | Onda | Branch | Status |
|------|--------|------|--------|--------|
| TASK-01 | Bootstrap solution e dez projetos Clean Architecture | Onda 1 | `feat/reporting/all-waves` | [X] |
| TASK-02 | Testes de arquitetura e dependências entre camadas | Onda 1 | `feat/reporting/all-waves` | [X] |
| TASK-03 | Objetos de valor: Money, Period, ChannelShare e StageBucket | Onda 2 | `feat/reporting/03-domain-value-objects` | [ ] |
| TASK-04 | ReportScope, ReportType e enums de papel RBAC | Onda 2 | `feat/reporting/04-report-scope-enum` | [ ] |
| TASK-05 | Portas de aplicação: IReportingReadRepository, IScopeResolver, ICsvReportWriter, ICsvStorage | Onda 3 | `feat/reporting/05-application-ports` | [ ] |
| TASK-06 | GetFunnelReportQuery e handler (Req 1) | Onda 3 | `feat/reporting/06-funnel-query-handler` | [ ] |
| TASK-07 | GetForecastReportQuery e handler com degradação graciosa (Req 6) | Onda 3 | `feat/reporting/07-forecast-query-handler` | [ ] |
| TASK-08 | GetRankingReportQuery e handler com PiiMinimizationPolicy (Req 2, RNF 4) | Onda 3 | `feat/reporting/08-ranking-query-handler` | [ ] |
| TASK-09 | GetChannelReportQuery e handler (Req 3, PBT-04) | Onda 3 | `feat/reporting/09-channel-query-handler` | [ ] |
| TASK-10 | GetCommissionReportQuery e handler (Req 4, PBT-01, PBT-02) | Onda 3 | `feat/reporting/10-commission-query-handler` | [ ] |
| TASK-11 | ExportReportCsvQuery, ICsvReportWriter e PBT-05 (Req 5) | Onda 3 | `feat/reporting/11-export-csv-query` | [ ] |
| TASK-12 | Pipeline behaviors: Correlation, TenantContext, Validation, Authorization, LoggingMetrics, QueryTimeout | Onda 3 | `feat/reporting/12-pipeline-behaviors` | [ ] |
| TASK-13 | View vw_funnel_report com security_invoker e migration idempotente | Onda 4 | `feat/reporting/13-view-funnel` | [ ] |
| TASK-14 | View vw_forecast_report com security_invoker e tratamento de meta ausente | Onda 4 | `feat/reporting/14-view-forecast` | [ ] |
| TASK-15 | Views vw_ranking_report e vw_channel_report com security_invoker | Onda 4 | `feat/reporting/15-views-ranking-channel` | [ ] |
| TASK-16 | View vw_commission_report com security_invoker e distinção snapshot/projetado | Onda 4 | `feat/reporting/16-view-commission` | [ ] |
| TASK-17 | Índices críticos e migration consolidada idempotente | Onda 4 | `feat/reporting/17-indexes-migration` | [ ] |
| TASK-18 | Teste de isolamento RLS cross-tenant: PBT-03 como gate de CI | Onda 4 | `test/reporting/18-rls-isolation-pbt03` | [ ] |
| TASK-19 | IReportingReadRepository com Dapper e ICsvStorage GCS client | Onda 4 | `feat/reporting/19-read-repo-gcs-client` | [ ] |
| TASK-20 | Contracts DTOs: requests, responses e CsvExportResponse | Onda 5 | `feat/reporting/20-contracts-dtos` | [ ] |
| TASK-21 | ReportingController: cinco endpoints GET de relatório e GET export | Onda 5 | `feat/reporting/21-reporting-controller` | [ ] |
| TASK-22 | Testes de API: contratos, RBAC, catálogo de erros e anti-enumeração | Onda 5 | `test/reporting/22-api-tests` | [ ] |
| TASK-23 | Testes de segurança: PII em logs, Platform Operator bloqueado, cross-tenant (RNF 4, RNF 5) | Onda 6 | `test/reporting/23-security-tests` | [ ] |
| TASK-24 | Observabilidade: logs estruturados, métricas, traces, alertas e health checks (RNF 6) | Onda 6 | `feat/reporting/24-observability` | [ ] |
| TASK-25 | Performance baseline e DoD final (RNF 1, RNF 3, PTV-01, RISK-REPORT-06) | Onda 6 | `test/reporting/25-performance-dod` | [ ] |

---

## 3. Ondas de Implementação

| Onda | Foco | TASKs | Critério de Fechamento |
|------|------|-------|------------------------|
| Onda 1 | Bootstrap | TASK-01..TASK-02 | Projetos compilam; testes de arquitetura verdes; CI mínimo verde |
| Onda 2 | Domain | TASK-03..TASK-04 | Objetos de valor com invariantes testadas; base de PBT-02 e PBT-04 verde em `Reporting.Domain.Tests` |
| Onda 3 | Application | TASK-05..TASK-12 | Todos os handlers testados com mocks de porta; PBT-01, PBT-04, PBT-05 verdes; behaviors ativos; scope resolver testado |
| Onda 4 | Infrastructure | TASK-13..TASK-19 | Todas as views com `security_invoker`; índices aplicados; PBT-03 green e bloqueante no CI; GCS client testado |
| Onda 5 | API + Contracts | TASK-20..TASK-22 | Endpoints respondendo; catálogo de erros coberto; anti-enumeração verificada; testes de contrato verdes |
| Onda 6 | Hardening | TASK-23..TASK-25 | PII ausente em logs verificada; Platform Operator bloqueado; p95 ≤ 3 s confirmado; export ≤ 10 s; DoD assinado |

---

## 4. Tarefas

### TASK-01 — Bootstrap solution e dez projetos Clean Architecture

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 1 — Bootstrap |
| **Branch** | `feat/reporting/01-bootstrap-solution` |
| **Worktree** | `git worktree add ../worktrees/reporting/01-bootstrap-solution -b feat/reporting/01-bootstrap-solution` |
| **Status** | [X] |
| **Depende de** | Não aplicável |
| **Entregável** | Dez projetos (cinco fonte + cinco de teste) compilando com referências corretas conforme design §3 |
| **Mapeia** | DD-003, ADR-0001, design §3 |
| **Camada principal** | DevOps |

#### Objetivo

Criar a estrutura de solução do slice `reporting` com os cinco projetos da Clean Architecture (design §3) e seus cinco projetos de teste correspondentes. Referências: `Application → Domain + Contracts`; `Infrastructure → Application + Domain`; `Api → Application + Infrastructure + Contracts`; `Domain → ∅`; `Contracts → ∅`. Como o módulo é read side puro (DD-003), `Reporting.Domain` não contém aggregates transacionais.

#### Subtasks

- [X] **ST-01 — Red:** escrever teste de compilação que verifica a existência dos assemblies `Reporting.Domain`, `Reporting.Application`, `Reporting.Infrastructure`, `Reporting.Api` e `Reporting.Contracts` — falha porque os projetos não existem.
- [X] **ST-02 — Green:** criar os dez projetos com referências conforme design §3; registrar na solution do `azim-api`.
- [X] **ST-03 — Refactor:** confirmar namespaces em inglês (`Reporting.*`); remover boilerplate; validar ausência de dependências circulares.
- [X] **ST-04 — Docs:** anotar estrutura no README do módulo.
- [X] **ST-05 — Encerramento:** `dotnet build` verde; commit `feat(reporting): bootstrap dez projetos Clean Architecture read-side`; push.

#### Critérios de Aceite

- [X] Dez projetos criados com namespaces `Reporting.*`.
- [X] Referências seguem hierarquia do design §3 sem dependências circulares.
- [X] `dotnet build` sem erro ou warning de dependência circular.
- [X] `Domain` e `Contracts` sem referências externas proibidas.

---

### TASK-02 — Testes de arquitetura e dependências entre camadas

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 1 — Bootstrap |
| **Branch** | `test/reporting/02-architecture-tests` |
| **Worktree** | `git worktree add ../worktrees/reporting/02-architecture-tests -b test/reporting/02-architecture-tests` |
| **Status** | [X] |
| **Depende de** | TASK-01 |
| **Entregável** | Suite `Reporting.Architecture.Tests` com regras de dependência automatizadas e bloqueadoras no CI |
| **Mapeia** | design §3, DD-003, ADR-0001 |
| **Camada principal** | Tests |

#### Objetivo

Implementar testes de arquitetura que validam as regras de dependência entre camadas (design §3). Como o módulo é read side puro, validar também que `Reporting.Domain` não contém aggregates transacionais — apenas objetos de valor e enums (DD-003).

#### Subtasks

- [X] **ST-01 — Red:** criar `Reporting.Architecture.Tests`; escrever testes referenciando os assemblies — falham porque não há conteúdo mínimo.
- [X] **ST-02 — Green:** implementar via NetArchTest ou equivalente: `Api` não acessa `Domain` diretamente; `Infrastructure` não referencia `Api`; `Domain` e `Contracts` sem dependências proibidas; `Domain` não contém classes que herdam de aggregate base transacional.
- [X] **ST-03 — Refactor:** agrupar por categoria; garantir mensagens de falha que identificam o violador.
- [X] **ST-04 — Docs:** não aplicável.
- [X] **ST-05 — Encerramento:** testes verdes; commit `test(reporting): testes de arquitetura Clean Architecture read-side`; push.

#### Critérios de Aceite

- [X] Todas as regras de dependência do design §3 cobertas por testes automatizados.
- [X] Violação de qualquer regra quebra o CI imediatamente.
- [X] `Domain` validado como camada sem aggregate transacional.
- [X] Mensagens de falha descritivas.

---

### TASK-03 — Objetos de valor: Money, Period, ChannelShare e StageBucket

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `feat/reporting/03-domain-value-objects` |
| **Worktree** | `git worktree add ../worktrees/reporting/03-domain-value-objects -b feat/reporting/03-domain-value-objects` |
| **Status** | [ ] |
| **Depende de** | TASK-01 |
| **Entregável** | Quatro objetos de valor imutáveis em `Reporting.Domain` com invariantes testadas; base de PBT-02 e PBT-04 verde |
| **Mapeia** | design §4.3, DD-007, DD-010, PBT-02, PBT-04, RNF 2 |
| **Camada principal** | Domain |

#### Objetivo

Implementar os quatro objetos de valor de leitura que garantem corretude aritmética e tipagem do módulo. `Money`: `long cents`, sem `float`/`double`, soma fechada em inteiros (base PBT-02). `Period`: `from <= to`, janela ≤ 12 meses para SLO. `ChannelShare`: percentual em basis points inteiros (base 10.000), conservação de soma = 100% (PBT-04, DD-010). `StageBucket`: categoria refletida da origem sem reclassificação.

#### Subtasks

- [ ] **ST-01 — Red:** testes unitários para `Money` (igualdade por valor, `Add`/`Subtract` exatos em `long`, rejeição de negativo, proibição de `double`); para `Period` (`from > to` lança, janela de 0 a 12 meses aceita); para `ChannelShare` (invariante `percentBasisPoints` ∈ [0, 10000]); para `StageBucket` (categoria imutável após criação). PBT-02: para quaisquer listas de centavos inteiros, soma via `Money` = soma aritmética exata. PBT-04: para qualquer lista de `ChannelShare`, soma de `percentBasisPoints` = 10.000.
- [ ] **ST-02 — Green:** implementar os quatro objetos como `record` imutáveis em `Reporting.Domain`; factory methods com guards; operações retornam novos objetos.
- [ ] **ST-03 — Refactor:** garantir que nenhuma operação usa `float`/`double`; solidificar mensagens de exceção de domínio.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes + PBT-02 + PBT-04 verdes; coverage `Reporting.Domain.Tests` ≥ 95%; commit `feat(reporting): objetos de valor Money Period ChannelShare StageBucket`; push.

#### Critérios de Aceite

- [ ] `Money` imutável; construção a partir de `double`/`float` bloqueada em compile-time ou runtime.
- [ ] `Period` rejeita `from > to` e janelas inválidas com exceção de domínio.
- [ ] `ChannelShare` em basis points inteiros; `percentBasisPoints` ∈ [0, 10000].
- [ ] PBT-02 green: soma de `Money` exata para geradores aleatórios de `long`.
- [ ] PBT-04 green: soma de `percentBasisPoints` = 10.000 para listas aleatórias de `ChannelShare`.

---

### TASK-04 — ReportScope, ReportType e enums de papel RBAC

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `feat/reporting/04-report-scope-enum` |
| **Worktree** | `git worktree add ../worktrees/reporting/04-report-scope-enum -b feat/reporting/04-report-scope-enum` |
| **Status** | [ ] |
| **Depende de** | TASK-03 |
| **Entregável** | `ReportScope`, `ReportType` e `ReportingRole` em `Reporting.Domain`; `RbacScopeSpecification` em `Reporting.Application` com testes de predicado por papel |
| **Mapeia** | design §4.3, §4.6, DD-006, Req 7, Req 8, RNF 5 |
| **Camada principal** | Domain / Application |

#### Objetivo

Implementar o objeto de valor `ReportScope` (resolvido no servidor a partir do token e memberships, nunca do cliente), o enum `ReportType` (lista canônica fechada: `funnel, forecast, ranking, channel, commissions`) e a `RbacScopeSpecification` que traduz cada papel no predicado SQL correto: Vendedor → `owner_id = :sub`; Gestor de BU → `bu_id IN :allowed_bus`; Tenant Admin → sem predicado adicional; Platform Operator → negação dura.

#### Subtasks

- [ ] **ST-01 — Red:** testes para `ReportScope`: `tenantId` nulo lança; `allowedBuIds` vazio com papel `GestorBU` lança; `ReportType` com valor fora do enum lança. Testes para `RbacScopeSpecification`: Vendedor gera predicado `owner_id = :sub`; Gestor de BU gera predicado `bu_id IN (...)`; Tenant Admin gera predicado vazio; Platform Operator lança `AccessDeniedException`.
- [ ] **ST-02 — Green:** implementar `ReportScope` como `record` imutável; `ReportType` como enum fechado; `ReportingRole` como enum; `RbacScopeSpecification` com método `BuildPredicate(ReportScope) → SqlPredicate`.
- [ ] **ST-03 — Refactor:** garantir que `ReportScope` nunca é construído a partir de entrada de cliente sem validação server-side.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes verdes; commit `feat(reporting): ReportScope ReportType RbacScopeSpecification RBAC`; push.

#### Critérios de Aceite

- [ ] `ReportScope` só pode ser construído pelo `IScopeResolver` no servidor.
- [ ] `ReportType` lista canônica de cinco valores; valor fora do enum rejeitado com erro tipado.
- [ ] `RbacScopeSpecification` mapeia cada papel no predicado correto.
- [ ] Platform Operator lança exceção de acesso negado antes de tocar o banco.

### TASK-05 — Portas de aplicação: IReportingReadRepository, IScopeResolver, ICsvReportWriter, ICsvStorage

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/reporting/05-application-ports` |
| **Worktree** | `git worktree add ../worktrees/reporting/05-application-ports -b feat/reporting/05-application-ports` |
| **Status** | [ ] |
| **Depende de** | TASK-04 |
| **Entregável** | Quatro interfaces de porta em `Reporting.Application` com contratos completos e testes de contrato de porta verdes |
| **Mapeia** | design §5.2, §5.3, §6.4, DD-004, Req 5, Req 7 |
| **Camada principal** | Application |

#### Objetivo

Definir as quatro interfaces de porta que desacoplam a Application das implementações de infraestrutura. `IReportingReadRepository`: métodos de leitura por tipo de relatório com `ReportScope` e `Period`. `IScopeResolver`: resolve `ReportScope` a partir do `tenantId` + `userId` + papel do token. `ICsvReportWriter`: serializa linhas de relatório em CSV UTF-8 com BOM, cabeçalhos pt-BR, valores monetários em R$. `ICsvStorage`: faz upload de bytes para GCS e retorna signed URL com validade configurável.

#### Subtasks

- [ ] **ST-01 — Red:** testes de contrato de porta (usando double/stub) verificando assinaturas: `IReportingReadRepository` retorna coleções tipadas por tipo de relatório; `IScopeResolver` lança para `tenantId` nulo; `ICsvReportWriter` serializa linhas de relatório arbitrárias; `ICsvStorage.Upload` retorna `CsvUploadResult` com `signedUrl` e `expiresAt`.
- [ ] **ST-02 — Green:** criar as quatro interfaces em `Reporting.Application`; criar tipos de retorno necessários.
- [ ] **ST-03 — Refactor:** garantir que os contratos usam tipos do `Domain` e `Contracts` sem vazar tipos de infraestrutura.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes verdes; commit `feat(reporting): portas de aplicação IReportingReadRepository IScopeResolver ICsvReportWriter ICsvStorage`; push.

#### Critérios de Aceite

- [ ] Quatro interfaces definidas em `Reporting.Application` sem referência a implementações de infraestrutura.
- [ ] Todos os métodos tipados com tipos de `Domain`/`Contracts` (sem EF, Dapper ou GCS SDK nas assinaturas).
- [ ] `IScopeResolver` declara negação explícita para Platform Operator.
- [ ] Testes de contrato de porta verdes com doubles em memória.

---

### TASK-06 — GetFunnelReportQuery e handler (Req 1)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/reporting/06-funnel-query-handler` |
| **Worktree** | `git worktree add ../worktrees/reporting/06-funnel-query-handler -b feat/reporting/06-funnel-query-handler` |
| **Status** | [ ] |
| **Depende de** | TASK-05 |
| **Entregável** | `GetFunnelReportQuery` + handler testado com mocks; retorna linhas por estágio com `count`, `totalCents` e `weightedForecastCents` em centavos inteiros |
| **Mapeia** | Req 1, design §5.2, §5.3, DD-007 |
| **Camada principal** | Application |

#### Objetivo

Implementar a query e o handler do relatório de funil por estágio (Req 1). O handler recebe `Period` e `buIds?` opcionais, aplica o `ReportScope` resolvido, chama `IReportingReadRepository.GetFunnelAsync` com o predicado de escopo e período, e mapeia as linhas para `FunnelReportResponse` em centavos inteiros. Não recalcula `valor_total` nem `forecast_ponderado` (P2).

#### Subtasks

- [ ] **ST-01 — Red:** testes do handler com mock de `IReportingReadRepository`: scope Vendedor retorna apenas linhas do `owner_id` do mock; scope Gestor de BU retorna apenas as BUs permitidas; `Period` com `from > to` lança antes de chamar o repositório; linhas com `totalCents` em centavos preservadas sem conversão.
- [ ] **ST-02 — Green:** implementar `GetFunnelReportQuery(Period, BuIds?, ReportScope)` e `GetFunnelReportQueryHandler`; chamar porta; mapear para `FunnelReportResponse` (lista de `FunnelStageRow`: `stageId`, `stageName`, `category`, `count`, `totalCents`, `weightedForecastCents`).
- [ ] **ST-03 — Refactor:** extrair mapeamento para método puro; garantir que nenhum `float` entra no caminho de centavos.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes verdes; coverage Application ≥ 85%; commit `feat(reporting): GetFunnelReportQuery handler Req1`; push.

#### Critérios de Aceite

- [ ] Handler filtra por `ReportScope` antes de chamar o repositório.
- [ ] Linhas retornam `totalCents` e `weightedForecastCents` em `long` centavos sem conversão.
- [ ] Handler não recalcula `forecast_ponderado` — reflete o valor do repositório.
- [ ] Testes cobrem os três papéis (Vendedor, Gestor de BU, Tenant Admin).

---

### TASK-07 — GetForecastReportQuery e handler com degradação graciosa (Req 6)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/reporting/07-forecast-query-handler` |
| **Worktree** | `git worktree add ../worktrees/reporting/07-forecast-query-handler -b feat/reporting/07-forecast-query-handler` |
| **Status** | [ ] |
| **Depende de** | TASK-05 |
| **Entregável** | `GetForecastReportQuery` + handler testado; suporta comparativo de meta opcional sem lançar erro quando meta ausente |
| **Mapeia** | Req 6, design §5.2, §5.3, §4.6, DD-007, RNF 2 |
| **Camada principal** | Application |

#### Objetivo

Implementar a query e o handler do relatório de forecast por BU e mês (Req 6). O handler agrega `weightedForecastCents` (oportunidades abertas) e `realizedCents` (oportunidades Ganhas) por BU/mês no período. Quando há meta cadastrada (`goalCents?`), inclui no comparativo; quando ausente, retorna `null` sem erro (degradação graciosa, Req 6.3 e P8).

#### Subtasks

- [ ] **ST-01 — Red:** testes do handler: sem meta → `goalCents` é `null` e nenhum erro lançado; com meta → `goalCents` preenchido; valores `weightedForecastCents` e `realizedCents` preservados em centavos; período inválido lança antes do repositório.
- [ ] **ST-02 — Green:** implementar `GetForecastReportQuery(Period, BuIds?, ReportScope)` e handler; mesclar opcionalmente os dados de meta via `IReportingReadRepository.GetGoalAsync`; retornar `ForecastReportResponse` (linhas por BU/mês: `buId`, `buName`, `year`, `month`, `weightedForecastCents`, `realizedCents`, `goalCents?`).
- [ ] **ST-03 — Refactor:** garantir que ausência de meta não produz nenhum `placeholder` inválido (nem zero artificial).
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes verdes; coverage Application ≥ 85%; commit `feat(reporting): GetForecastReportQuery handler degradacao-gracosa Req6`; push.

#### Critérios de Aceite

- [ ] `goalCents` é `null` quando meta ausente — nunca zero artificial nem exceção.
- [ ] Handler não recalcula `forecast_ponderado` nem `realizado`.
- [ ] Testes cobrem cenário com meta e sem meta para a mesma BU.
- [ ] Scope RBAC aplicado antes de chamar o repositório.

---

### TASK-08 — GetRankingReportQuery e handler com PiiMinimizationPolicy (Req 2, RNF 4)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/reporting/08-ranking-query-handler` |
| **Worktree** | `git worktree add ../worktrees/reporting/08-ranking-query-handler -b feat/reporting/08-ranking-query-handler` |
| **Status** | [ ] |
| **Depende de** | TASK-05 |
| **Entregável** | `GetRankingReportQuery` + handler + `PiiMinimizationPolicy`; Vendedor vê apenas própria linha; `display_name` presente apenas no escopo RBAC correto |
| **Mapeia** | Req 2, RNF 4, design §4.6, §5.2, DD-008, Req 7 |
| **Camada principal** | Application |

#### Objetivo

Implementar a query e o handler do ranking por responsável (Req 2). Vendedor obtém apenas sua própria linha (Req 2.4). `display_name` é tratado como PII e incluído somente no escopo RBAC do usuário e somente neste relatório (DD-008). Ordenação padrão por `wonValueCents` decrescente (Req 2.2). `PiiMinimizationPolicy` centraliza a decisão de inclusão de PII.

#### Subtasks

- [ ] **ST-01 — Red:** testes: Vendedor → `RankingReportResponse` contém apenas a linha com `ownerId = sub`; Gestor de BU → apenas linhas da BU; `display_name` ausente quando papel não tem escopo; ordenação por `wonValueCents` desc por padrão; `pipelineForecastCents` em centavos preservados.
- [ ] **ST-02 — Green:** implementar `GetRankingReportQuery` e handler; aplicar `PiiMinimizationPolicy.Include(scope)` para decidir se inclui `displayName`; retornar `RankingReportResponse` (lista de `RankingRow`: `ownerId`, `displayName?`, `wonCount`, `wonValueCents`, `pipelineForecastCents`).
- [ ] **ST-03 — Refactor:** extrair `PiiMinimizationPolicy` como classe testável separada; garantir que `displayName` nunca aparece em logs mesmo quando incluído na response.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes verdes; commit `feat(reporting): GetRankingReportQuery handler PiiMinimizationPolicy DD008`; push.

#### Critérios de Aceite

- [ ] Vendedor recebe exatamente uma linha (a própria) — sem linhas de outros.
- [ ] `displayName` ausente nos relatórios onde `PiiMinimizationPolicy` determina omissão.
- [ ] `PiiMinimizationPolicy` testada independentemente por papel.
- [ ] Ordenação `wonValueCents desc` aplicada antes do retorno.

---

### TASK-09 — GetChannelReportQuery e handler (Req 3, PBT-04)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/reporting/09-channel-query-handler` |
| **Worktree** | `git worktree add ../worktrees/reporting/09-channel-query-handler -b feat/reporting/09-channel-query-handler` |
| **Status** | [ ] |
| **Depende de** | TASK-05 |
| **Entregável** | `GetChannelReportQuery` + handler; percentuais em basis points inteiros; PBT-04 green no handler |
| **Mapeia** | Req 3, PBT-04, design §5.2, DD-010 |
| **Camada principal** | Application |

#### Objetivo

Implementar a query e o handler do relatório de oportunidades por canal (Req 3). O handler usa `ChannelShare` (basis points, DD-010) para garantir que a soma dos percentuais por canal = 10.000 (100%) sem erro de ponto flutuante. PBT-04 no handler valida a conservação de distribuição para qualquer conjunto de linhas.

#### Subtasks

- [ ] **ST-01 — Red:** testes unitários: soma de `percentBasisPoints` de todas as linhas = 10.000 para conjuntos arbitrários; canal com zero oportunidades pode aparecer com `percentBasisPoints = 0`; scope RBAC aplicado antes do repositório. PBT-04 no handler: para qualquer lista de rows retornada pelo mock, soma de `percentBasisPoints` = 10.000.
- [ ] **ST-02 — Green:** implementar handler que recebe linhas brutas (`count`, `totalCents` por canal) do repositório e calcula `percentBasisPoints` usando aritmética inteira; retornar `ChannelReportResponse` (lista de `ChannelRow`: `channelId`, `channelName`, `count`, `totalCents`, `percentBasisPoints`).
- [ ] **ST-03 — Refactor:** extrair cálculo de basis points para método puro testável.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes + PBT-04 verdes; commit `feat(reporting): GetChannelReportQuery handler basis-points PBT04`; push.

#### Critérios de Aceite

- [ ] Soma de `percentBasisPoints` = 10.000 para qualquer conjunto de linhas não-vazio.
- [ ] Nenhum `float`/`double` no caminho de cálculo de percentual.
- [ ] PBT-04 green com gerador de listas aleatórias de contagens positivas.
- [ ] `totalCents` preservado em `long` sem conversão.

---

### TASK-10 — GetCommissionReportQuery e handler (Req 4, PBT-01, PBT-02)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/reporting/10-commission-query-handler` |
| **Worktree** | `git worktree add ../worktrees/reporting/10-commission-query-handler -b feat/reporting/10-commission-query-handler` |
| **Status** | [ ] |
| **Depende de** | TASK-05 |
| **Entregável** | `GetCommissionReportQuery` + handler; PBT-01 (snapshot imutável) e PBT-02 (conservação de soma) verdes |
| **Mapeia** | Req 4, PBT-01, PBT-02, design §5.2, DD-007, RN-007 |
| **Camada principal** | Application |

#### Objetivo

Implementar a query e o handler do relatório de comissões por parceiro (Req 4). `comissao_consolidada` vem exclusivamente de linhas `is_snapshot = true` (RN-007, Req 4.2). `comissao_projetada` vem de linhas `is_snapshot = false` com `stage_category = open` (Req 4.3). PBT-01 valida que alterações de percentual do parceiro após o snapshot não alteram `comissao_consolidada`. PBT-02 valida conservação de centavos na soma (sem perda ou duplicação).

#### Subtasks

- [ ] **ST-01 — Red:** testes: dado conjunto de snapshots imutáveis e percentuais alterados → `consolidatedCents` igual à soma dos snapshots originais (PBT-01). PBT-02: para qualquer lista de comissões projetadas/consolidadas, total = soma exata em centavos. Testes de handler: `projectedCents` = soma de `commission_cents` de linhas open não-snapshot; `consolidatedCents` = soma de linhas snapshot; scope RBAC aplicado.
- [ ] **ST-02 — Green:** implementar `GetCommissionReportQuery(Period, BuIds?, ReportScope)` e handler; separar linhas por `isSnapshot`; somar com `Money` (inteiros); retornar `CommissionReportResponse` (lista de `CommissionRow`: `partnerId`, `partnerName`, `projectedCents`, `consolidatedCents`, `opportunityCount`).
- [ ] **ST-03 — Refactor:** extrair separação snapshot/projetado para método puro; garantir que `long.MaxValue` não estoura (volume de referência ≤ 2.000 oportunidades × máx comissão por oportunidade).
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes + PBT-01 + PBT-02 verdes; commit `feat(reporting): GetCommissionReportQuery handler PBT01 PBT02 snapshot-immutable`; push.

#### Critérios de Aceite

- [ ] `consolidatedCents` derivado **exclusivamente** de `isSnapshot = true` — nunca rederivado de percentuais atuais.
- [ ] PBT-01 green: mutação de percentual após snapshot não altera `consolidatedCents`.
- [ ] PBT-02 green: soma de centavos sem perda para geradores aleatórios de listas de comissões.
- [ ] Handler não reimplementa fórmula de comissão — apenas soma os valores autoritativos.

---

### TASK-11 — ExportReportCsvQuery, ICsvReportWriter e PBT-05 (Req 5)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/reporting/11-export-csv-query` |
| **Worktree** | `git worktree add ../worktrees/reporting/11-export-csv-query -b feat/reporting/11-export-csv-query` |
| **Status** | [ ] |
| **Depende de** | TASK-06, TASK-07, TASK-08, TASK-09, TASK-10 |
| **Entregável** | `ExportReportCsvQuery` + handler + lógica de `ICsvReportWriter`; PBT-05 (round-trip relatório × CSV) verde |
| **Mapeia** | Req 5, PBT-05, design §5.1, §5.2, §6.5, DD-004, RNF 3, RNF 4 |
| **Camada principal** | Application |

#### Objetivo

Implementar a query de export que **reusa exatamente** a query do relatório correspondente (mesmas linhas, mesmo escopo) antes de serializar para CSV (design §5.2 — "uma só fonte de verdade"). Serialização: UTF-8 com BOM, cabeçalhos em pt-BR, valores monetários formatados em R$ (apenas na serialização), sem PII além da mínima necessária. Nome do objeto GCS determinístico (`reports/{tenant_id}/{type}/{period_hash}/{scope_hash}.csv`) para idempotência. PBT-05 valida que o CSV contém exatamente as mesmas linhas e totais do relatório.

#### Subtasks

- [ ] **ST-01 — Red:** PBT-05: para qualquer relatório e filtros arbitrários (mocados), o CSV serializado contém exatamente as mesmas linhas e totais que o handler de relatório retornaria. Testes unitários: CSV começa com BOM UTF-8; cabeçalhos em pt-BR; valores monetários exibem `R$`; sem PII além de `displayName` no ranking.
- [ ] **ST-02 — Green:** implementar `ExportReportCsvQuery(ReportType, Period, BuIds?, ReportScope)` e handler que despacha para o handler de relatório correto, serializa via `ICsvReportWriter` (implementação em Application — lógica pura, sem IO), e delega upload para `ICsvStorage` (porta).
- [ ] **ST-03 — Refactor:** garantir que nenhum relatório de outro tipo expõe `displayName` no CSV.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes + PBT-05 verdes; commit `feat(reporting): ExportReportCsvQuery handler ICsvReportWriter PBT05`; push.

#### Critérios de Aceite

- [ ] Export reusa handler de relatório — nenhuma query independente de geração de linhas.
- [ ] PBT-05 green: CSV equivalente ao relatório para filtros e escopos arbitrários.
- [ ] CSV começa com BOM (`\xEF\xBB\xBF`); cabeçalhos em pt-BR; `R$` na formatação de valores.
- [ ] Nome do objeto GCS determinístico para idempotência (Req 5.4).

---

### TASK-12 — Pipeline behaviors: Correlation, TenantContext, Validation, Authorization, LoggingMetrics, QueryTimeout

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/reporting/12-pipeline-behaviors` |
| **Worktree** | `git worktree add ../worktrees/reporting/12-pipeline-behaviors -b feat/reporting/12-pipeline-behaviors` |
| **Status** | [ ] |
| **Depende de** | TASK-05 |
| **Entregável** | Seis pipeline behaviors registrados e testados individualmente; Platform Operator bloqueado antes do banco; timeout ativo |
| **Mapeia** | design §5.4, ADR-0001, RNF 5, RNF 6, RNF 1.3, RISK-REPORT-01 |
| **Camada principal** | Application |

#### Objetivo

Implementar os seis pipeline behaviors (design §5.4) na ordem: `CorrelationBehavior` (propaga `correlation_id` e `tenant_id`); `TenantContextBehavior` (falha-fechada sem `tenant_id` → `REPORT-ERR-409`); `ValidationBehavior` (período, UUIDs, enum); `AuthorizationBehavior` (resolve scope, bloqueia Platform Operator); `LoggingMetricsBehavior` (log sem PII, métricas); `QueryTimeoutBehavior` (statement_timeout para proteger o transacional).

#### Subtasks

- [ ] **ST-01 — Red:** testes por behavior: `TenantContextBehavior` sem `tenant_id` → lança `TenantNotResolvedException`; `AuthorizationBehavior` com Platform Operator → lança `AccessDeniedException` sem chegar ao handler; `ValidationBehavior` com período inválido → `REPORT-ERR-001`; `LoggingMetricsBehavior` gera log com `correlation_id` mas sem `display_name`; `QueryTimeoutBehavior` configura timeout antes de chamar o handler.
- [ ] **ST-02 — Green:** implementar os seis behaviors como `IPipelineBehavior<TRequest, TResponse>` (MediatR ou equivalente); registrar na DI na ordem definida.
- [ ] **ST-03 — Refactor:** garantir que `LoggingMetricsBehavior` nunca loga PII (verificável em teste de string).
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes verdes; commit `feat(reporting): pipeline behaviors Correlation TenantContext Validation Authorization LoggingMetrics QueryTimeout`; push.

#### Critérios de Aceite

- [ ] Platform Operator bloqueado em `AuthorizationBehavior` antes de qualquer acesso ao banco.
- [ ] `TenantContextBehavior` sem `tenant_id` retorna `REPORT-ERR-409` (falha-fechada).
- [ ] `LoggingMetricsBehavior` verifica ausência de PII no corpo do log em teste automatizado.
- [ ] `QueryTimeoutBehavior` configura `statement_timeout` configurável (não hardcoded).

### TASK-13 — View vw_funnel_report com security_invoker e migration idempotente

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/reporting/13-view-funnel` |
| **Worktree** | `git worktree add ../worktrees/reporting/13-view-funnel -b feat/reporting/13-view-funnel` |
| **Status** | [ ] |
| **Depende de** | TASK-01 |
| **Entregável** | `vw_funnel_report` com `security_invoker = true`; EF Core keyless entity mapeada; migration `CREATE OR REPLACE VIEW` idempotente |
| **Mapeia** | design §7.2, DD-005, ADR-0001, Req 1, Req 8, RISK-REPORT-03, RISK-REPORT-06 |
| **Camada principal** | Infrastructure |

#### Objetivo

Criar a view `vw_funnel_report` conforme design §7.2 com `WITH (security_invoker = true)` para que a RLS das tabelas base (`opportunities`, `stages`) seja avaliada com o `app.current_tenant` do chamador — nunca com o owner da view. Confirmar que o Cloud SQL está em Postgres 15+ (RISK-REPORT-06) antes de aplicar; documentar alternativa `WHERE tenant_id = current_setting(...)` se versão inferior.

#### Subtasks

- [ ] **ST-01 — Red:** teste de integração local: com `app.current_tenant` de tenant A, query na view retorna zero linhas de tenant B.
- [ ] **ST-02 — Green:** escrever migration `CREATE OR REPLACE VIEW vw_funnel_report WITH (security_invoker = true) AS ...` conforme SQL do design §7.2; mapear como EF keyless entity em `ReportingDbContext`; confirmar versão Postgres.
- [ ] **ST-03 — Refactor:** validar plano de execução (EXPLAIN) para confirmar que RLS das tabelas base é aplicada.
- [ ] **ST-04 — Docs:** registrar confirmação ou fallback de versão Postgres no arquivo de decisões.
- [ ] **ST-05 — Encerramento:** migration aplicada; teste de isolamento local verde; commit `feat(reporting): view vw_funnel_report security_invoker`; push.

#### Critérios de Aceite

- [ ] View criada com `security_invoker = true` em Postgres 15+ (ou fallback documentado para versão inferior).
- [ ] Teste de integração: query com `tenant_id = A` retorna zero linhas de tenant B.
- [ ] EF keyless entity mapeada sem tracking nem `SaveChanges`.
- [ ] Migration é idempotente (`CREATE OR REPLACE VIEW`).

---

### TASK-14 — View vw_forecast_report com security_invoker e tratamento de meta ausente

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/reporting/14-view-forecast` |
| **Worktree** | `git worktree add ../worktrees/reporting/14-view-forecast -b feat/reporting/14-view-forecast` |
| **Status** | [ ] |
| **Depende de** | TASK-13 |
| **Entregável** | `vw_forecast_report` com `security_invoker = true`; `LEFT JOIN goals` preservando BU sem meta (`goalCents = NULL`); migration idempotente |
| **Mapeia** | design §7.2, DD-005, Req 6, Req 6.3 |
| **Camada principal** | Infrastructure |

#### Objetivo

Criar a view `vw_forecast_report` com `security_invoker = true`. A view expõe `tenant_id`, `bu_id`, `owner_id`, `year`, `month`, `weighted_forecast_cents` e `realized_cents`. O `LEFT JOIN goals` garante que BUs sem meta cadastrada retornam `null` no campo de meta (degradação graciosa, Req 6.3) — sem excluir a linha.

#### Subtasks

- [ ] **ST-01 — Red:** teste de integração: BU sem meta → linha presente com `goal_cents = NULL`; BU com meta → `goal_cents` preenchido; isolamento de tenant verificado (zero linhas de tenant B com `app.current_tenant = A`).
- [ ] **ST-02 — Green:** escrever migration `CREATE OR REPLACE VIEW vw_forecast_report WITH (security_invoker = true) AS SELECT ... LEFT JOIN goals g ON ...`; mapear EF keyless entity.
- [ ] **ST-03 — Refactor:** confirmar que `LEFT JOIN` não filtra BUs sem meta.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes de integração verdes; commit `feat(reporting): view vw_forecast_report security_invoker graceful-degradation`; push.

#### Critérios de Aceite

- [ ] Linhas de BU sem meta retornam `goal_cents = NULL` — nunca zero artificial.
- [ ] View com `security_invoker = true`; teste de isolamento verde.
- [ ] Migration idempotente.

---

### TASK-15 — Views vw_ranking_report e vw_channel_report com security_invoker

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/reporting/15-views-ranking-channel` |
| **Worktree** | `git worktree add ../worktrees/reporting/15-views-ranking-channel -b feat/reporting/15-views-ranking-channel` |
| **Status** | [ ] |
| **Depende de** | TASK-13 |
| **Entregável** | `vw_ranking_report` e `vw_channel_report` com `security_invoker = true`; `display_name` exposto apenas na view de ranking; migrations idempotentes |
| **Mapeia** | design §7.2, DD-005, DD-008, Req 2, Req 3, RNF 4 |
| **Camada principal** | Infrastructure |

#### Objetivo

Criar `vw_ranking_report` (join com `users` para `display_name` — PII, DD-008) e `vw_channel_report` (join com `origin_channels`). Ambas com `security_invoker = true`. A view de ranking expõe `display_name` como coluna adicional; a decisão de incluir ou omitir PII na response é do handler (via `PiiMinimizationPolicy`, DD-008) — a view apenas disponibiliza o dado para quem tem escopo.

#### Subtasks

- [ ] **ST-01 — Red:** testes de integração: isolamento cross-tenant em ambas as views; ranking com `owner_id` do tenant A retorna zero linhas do tenant B.
- [ ] **ST-02 — Green:** criar migrations `CREATE OR REPLACE VIEW vw_ranking_report WITH (security_invoker = true)` e `CREATE OR REPLACE VIEW vw_channel_report WITH (security_invoker = true)`; mapear EF keyless entities.
- [ ] **ST-03 — Refactor:** confirmar que `vw_channel_report` não expõe PII; confirmar que `display_name` na ranking view não aparece em logs de query (ADR-0001).
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes verdes; commit `feat(reporting): views vw_ranking_report vw_channel_report security_invoker DD008`; push.

#### Critérios de Aceite

- [ ] `vw_channel_report` não contém colunas de PII.
- [ ] `vw_ranking_report` expõe `display_name` — mas handler decide inclusão na response (PiiMinimizationPolicy).
- [ ] Isolamento cross-tenant verificado por teste de integração em ambas as views.

---

### TASK-16 — View vw_commission_report com security_invoker e distinção snapshot/projetado

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/reporting/16-view-commission` |
| **Worktree** | `git worktree add ../worktrees/reporting/16-view-commission -b feat/reporting/16-view-commission` |
| **Status** | [ ] |
| **Depende de** | TASK-13 |
| **Entregável** | `vw_commission_report` com `security_invoker = true`; coluna `is_snapshot` exposta para distinção no handler; migration idempotente |
| **Mapeia** | design §7.2, DD-005, Req 4, Req 4.2, PBT-01, RN-007 |
| **Camada principal** | Infrastructure |

#### Objetivo

Criar `vw_commission_report` conforme SQL do design §7.2: join entre `opportunity_partner_commissions`, `opportunities` e `partners`; expõe `is_snapshot`, `commission_cents`, `stage_category`, `bu_id`, `owner_id`. A view não agrega — o handler aplica `SUM WHERE is_snapshot` para consolidado e `SUM WHERE NOT is_snapshot AND stage_category = 'open'` para projetado (PBT-01 validado no handler). Isolamento por `security_invoker`.

#### Subtasks

- [ ] **ST-01 — Red:** teste de integração: dado tenant A com snapshots e linhas projetadas, query com `app.current_tenant = A` retorna exatamente as linhas de A; zero linhas de tenant B.
- [ ] **ST-02 — Green:** criar migration `CREATE OR REPLACE VIEW vw_commission_report WITH (security_invoker = true) AS ...`; mapear EF keyless entity com campo `IsSnapshot`.
- [ ] **ST-03 — Refactor:** confirmar que `is_snapshot` está exposto como booleano confiável (sem cast frágil).
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes de integração verdes; commit `feat(reporting): view vw_commission_report security_invoker is_snapshot`; push.

#### Critérios de Aceite

- [ ] `is_snapshot` exposto como booleano na projeção EF.
- [ ] Isolamento cross-tenant verificado por teste de integração.
- [ ] View não realiza agregação — colunas brutas para o handler.

---

### TASK-17 — Índices críticos e migration consolidada idempotente

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/reporting/17-indexes-migration` |
| **Worktree** | `git worktree add ../worktrees/reporting/17-indexes-migration -b feat/reporting/17-indexes-migration` |
| **Status** | [ ] |
| **Depende de** | TASK-13 |
| **Entregável** | Cinco índices do design §7.4 criados; plano de execução (EXPLAIN) confirma uso dos índices nas queries de relatório |
| **Mapeia** | design §7.4, RNF 1, RISK-REPORT-01 |
| **Camada principal** | Infrastructure |

#### Objetivo

Criar os cinco índices críticos do design §7.4 que mitigam RISK-REPORT-01 (latência de queries impactando o transacional). Migration `CREATE INDEX IF NOT EXISTS` (idempotente). Validar com EXPLAIN que as queries das cinco views utilizam os índices corretos para o volume de referência.

#### Subtasks

- [ ] **ST-01 — Red:** teste de performance local (ou script EXPLAIN): sem índices, a query de funil sobre 2.000 oportunidades apresenta full scan.
- [ ] **ST-02 — Green:** criar migration com `CREATE INDEX IF NOT EXISTS ix_opp_tenant_bu_created ON opportunities (tenant_id, bu_id, created_at)`, `ix_opp_tenant_owner_cat ON opportunities (tenant_id, owner_id, stage_category)`, `ix_opp_tenant_channel ON opportunities (tenant_id, origin_channel_id)`, `ix_opp_tenant_closed ON opportunities (tenant_id, closed_at) WHERE stage_category = 'won'`, `ix_opc_tenant_partner_snap ON opportunity_partner_commissions (tenant_id, partner_id, is_snapshot)`.
- [ ] **ST-03 — Refactor:** confirmar com EXPLAIN ANALYZE que cada índice é usado pela query correspondente; ajustar se necessário.
- [ ] **ST-04 — Docs:** registrar resultado do EXPLAIN em comentário na migration.
- [ ] **ST-05 — Encerramento:** migration aplicada; EXPLAIN confirma uso de índices; commit `feat(reporting): cinco indices criticos views read-model RNF1`; push.

#### Critérios de Aceite

- [ ] Cinco índices criados de forma idempotente (`IF NOT EXISTS`).
- [ ] EXPLAIN ANALYZE confirma uso de índice para cada tipo de relatório.
- [ ] Migration não cria locks excessivos (usar `CREATE INDEX CONCURRENTLY` quando suportado).

---

### TASK-18 — Teste de isolamento RLS cross-tenant: PBT-03 como gate de CI

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `test/reporting/18-rls-isolation-pbt03` |
| **Worktree** | `git worktree add ../worktrees/reporting/18-rls-isolation-pbt03 -b test/reporting/18-rls-isolation-pbt03` |
| **Status** | [ ] |
| **Depende de** | TASK-13, TASK-14, TASK-15, TASK-16 |
| **Entregável** | Suite `Reporting.Infrastructure.Tests` com PBT-03 bloqueante no CI: query nas cinco views com `tenant_id = A` retorna zero linhas de tenant B para qualquer filtro |
| **Mapeia** | PBT-03, Req 8, Req 8.3, ADR-0001, DD-005, RISK-REPORT-03 |
| **Camada principal** | Tests |

#### Objetivo

Implementar o teste automatizado de isolamento multi-tenant (PBT-03, Req 8.3) nas cinco views de read model. Deve ser bloqueante no CI (falha → CI vermelho). Dois cenários obrigatórios: (1) `app.current_tenant = A` → zero linhas de tenant B nas cinco views; (2) sem `app.current_tenant` (falha-fechada) → zero linhas em qualquer view. PBT-03 com gerador de tenants e filtros arbitrários.

#### Subtasks

- [ ] **ST-01 — Red:** criar suite de integração; escrever teste que insere dados de tenant A e B no banco de teste, seta `app.current_tenant = A` e verifica que cada uma das cinco views retorna zero linhas de B.
- [ ] **ST-02 — Green:** implementar usando banco de teste real (PostgreSQL em container); interceptor de conexão aplica `SET app.current_tenant`; queries nas views verificadas linha a linha por `tenant_id`.
- [ ] **ST-03 — Refactor:** transformar em PBT-03 com geradores de múltiplos tenants e filtros; adicionar cenário sem `app.current_tenant` (falha-fechada → zero linhas).
- [ ] **ST-04 — Docs:** documentar o gate no README do módulo e no pipeline CI.
- [ ] **ST-05 — Encerramento:** PBT-03 green; gate registrado no CI; commit `test(reporting): PBT03 isolamento RLS cross-tenant gate CI`; push.

#### Critérios de Aceite

- [ ] PBT-03 green: zero linhas de outro tenant para qualquer filtro em todas as cinco views.
- [ ] Falha-fechada verificada: sem `app.current_tenant` → zero linhas.
- [ ] Teste bloqueante no CI (`Reporting.Infrastructure.Tests` no pipeline).
- [ ] Banco de teste usa Postgres real (não in-memory) para validar RLS.

---

### TASK-19 — IReportingReadRepository com Dapper e ICsvStorage GCS client

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/reporting/19-read-repo-gcs-client` |
| **Worktree** | `git worktree add ../worktrees/reporting/19-read-repo-gcs-client -b feat/reporting/19-read-repo-gcs-client` |
| **Status** | [ ] |
| **Depende de** | TASK-13, TASK-14, TASK-15, TASK-16, TASK-17 |
| **Entregável** | `ReportingReadRepository` (Dapper, read-only, queries parametrizadas) e `GcsCsvStorage` (upload idempotente, signed URL) com testes de integração |
| **Mapeia** | design §6.1, §6.4, §6.5, DD-004, ADR-0001, Req 5, RNF 3 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar `ReportingReadRepository` usando Dapper (SQL parametrizado — anti-SQLi) sobre conexão read-only com `app.current_tenant` setado pelo interceptor. Cinco métodos de query (um por tipo de relatório) mais `GetGoalAsync` para forecast. Implementar `GcsCsvStorage` que faz upload com nome determinístico e retorna signed URL de curta validade (~15 min); retry idempotente para falhas transitórias de upload.

#### Subtasks

- [ ] **ST-01 — Red:** testes de integração: `GetFunnelAsync` retorna apenas linhas do tenant configurado no interceptor; `GcsCsvStorage.UploadAsync` com mesmo nome de objeto → sobrescreve (idempotente); signed URL expira dentro do TTL configurado.
- [ ] **ST-02 — Green:** implementar `ReportingReadRepository` com queries Dapper SQL parametrizado; implementar `GcsCsvStorage` com retry com backoff exponencial (máx 3 tentativas) apenas para falhas de conexão, não de timeout.
- [ ] **ST-03 — Refactor:** confirmar que nenhuma query concatena strings de filtro — apenas parâmetros `@`; confirmar que `GcsCsvStorage` não expõe a chave de serviço em logs.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes de integração verdes; coverage `Reporting.Infrastructure.Tests` ≥ 70%; commit `feat(reporting): ReportingReadRepository Dapper GcsCsvStorage`; push.

#### Critérios de Aceite

- [ ] Nenhuma query usa concatenação de strings — apenas parâmetros Dapper.
- [ ] Upload GCS idempotente: mesmo objeto (`tenant_id/type/period_hash/scope_hash.csv`) sobrescreve sem criar duplicata.
- [ ] Retry apenas para falhas de conexão transitórias — nunca para timeout de query.
- [ ] Signed URL com TTL configurável (default ~15 min).

### TASK-20 — Contracts DTOs: requests, responses e CsvExportResponse

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `feat/reporting/20-contracts-dtos` |
| **Worktree** | `git worktree add ../worktrees/reporting/20-contracts-dtos -b feat/reporting/20-contracts-dtos` |
| **Status** | [ ] |
| **Depende de** | TASK-04 |
| **Entregável** | DTOs em `Reporting.Contracts` para os cinco relatórios + export; campos monetários em `long` centavos (`*_cents`); sem referência a tipos de domínio interno |
| **Mapeia** | design §8.1, §8.2, §8.3, DD-007, Req 5.2 |
| **Camada principal** | Contracts |

#### Objetivo

Definir os DTOs de request (filtros) e response (linhas de relatório) em `Reporting.Contracts` sem dependência de `Reporting.Domain` (Contracts → ∅). Todos os campos monetários como `long` (`*Cents`) no JSON — formatação R$ apenas no CSV. Incluir: `ReportFilterRequest`, `FunnelReportResponse` (+ `FunnelStageRow`), `ForecastReportResponse` (+ `ForecastRow`), `RankingReportResponse` (+ `RankingRow`), `ChannelReportResponse` (+ `ChannelRow`), `CommissionReportResponse` (+ `CommissionRow`), `CsvExportResponse`.

#### Subtasks

- [ ] **ST-01 — Red:** testes de serialização JSON: `totalCents` serializa como `long` (não `string`); `displayName` ausente quando `null`; `CsvExportResponse.signedUrl` não pode ser `null` ou vazio; `percentBasisPoints` serializa como inteiro.
- [ ] **ST-02 — Green:** implementar os DTOs em `Reporting.Contracts`; usar `[JsonIgnore]` ou equivalente para `displayName` quando `null` (não serializar campo ausente no JSON).
- [ ] **ST-03 — Refactor:** confirmar que `Contracts` não referencia `Domain`, `Application` nem `Infrastructure`.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes de serialização verdes; commit `feat(reporting): contracts DTOs requests responses CsvExportResponse`; push.

#### Critérios de Aceite

- [ ] Todos os campos monetários como `long` com sufixo `Cents`.
- [ ] `displayName` omitido no JSON quando `null` (não serializado como `"displayName": null`).
- [ ] `Contracts` sem referências a `Domain`, `Application` ou `Infrastructure`.
- [ ] `CsvExportResponse` contém `signedUrl`, `expiresAt` e `filename`.

---

### TASK-21 — ReportingController: cinco endpoints GET de relatório e GET export

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `feat/reporting/21-reporting-controller` |
| **Worktree** | `git worktree add ../worktrees/reporting/21-reporting-controller -b feat/reporting/21-reporting-controller` |
| **Status** | [ ] |
| **Depende de** | TASK-20, TASK-12 |
| **Entregável** | `ReportingController` com seis endpoints (`GET /api/v1/reports/{funnel,forecast,ranking,channels,commissions}` + `GET /api/v1/reports/{type}/export`); auth e mapeamento de filtros |
| **Mapeia** | design §8.2, Req 1..6, Req 7.4, Req 8 |
| **Camada principal** | Api |

#### Objetivo

Implementar `ReportingController` com base path `/api/v1/reports` (design §8.2). Auth obrigatória (`[Authorize]`). Mapeamento de query string → `ReportFilterRequest` (default: mês corrente). `X-Correlation-Id` gerado se ausente. Os endpoints despacham via MediatR para os handlers. Catálogo de erros mapeado em middleware de exceção (REPORT-ERR-001..009).

#### Subtasks

- [ ] **ST-01 — Red:** testes de integração do controller: `GET /api/v1/reports/funnel` sem auth → 401; com auth e escopo inválido → 403 `REPORT-ERR-005`; com `from > to` → 400 `REPORT-ERR-001`; `buId` fora do escopo → 404 `REPORT-ERR-004`; `GET /api/v1/reports/invalid/export` → 400 `REPORT-ERR-003`.
- [ ] **ST-02 — Green:** implementar `ReportingController`; registrar mapeamento de exceções tipadas para HTTP + código de catálogo; default de período = mês corrente quando filtro ausente.
- [ ] **ST-03 — Refactor:** confirmar que nenhuma mensagem de erro expõe PII ou dados de outro tenant; confirmar que `X-Correlation-Id` é propagado no response.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes de integração verdes; commit `feat(reporting): ReportingController seis endpoints catalogo-erros`; push.

#### Critérios de Aceite

- [ ] Seis endpoints respondendo com status correto para cenários de sucesso e erro.
- [ ] Catálogo de erros (REPORT-ERR-001..009) mapeado e estável.
- [ ] Erros de autorização (`005/006`) não revelam existência de recurso fora do escopo.
- [ ] `X-Correlation-Id` presente em todo response.

---

### TASK-22 — Testes de API: contratos, RBAC, catálogo de erros e anti-enumeração

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `test/reporting/22-api-tests` |
| **Worktree** | `git worktree add ../worktrees/reporting/22-api-tests -b test/reporting/22-api-tests` |
| **Status** | [ ] |
| **Depende de** | TASK-21 |
| **Entregável** | Suite `Reporting.Api.Tests` cobrindo contratos de response, RBAC por papel/endpoint, catálogo de erros e anti-enumeração |
| **Mapeia** | design §13.4, Req 7, Req 5.4, RNF 4.3, PBT-05 |
| **Camada principal** | Tests |

#### Objetivo

Implementar a suite de testes de API que cobre: (1) contrato de response estável (campos e tipos de cada endpoint); (2) RBAC — Vendedor obtém apenas própria linha, Gestor de BU obtém apenas BUs de membership, Tenant Admin obtém tenant inteiro; (3) catálogo de erros — código, HTTP status e ausência de PII nas mensagens; (4) anti-enumeração — `buId` fora do escopo retorna 404 genérico sem revelar existência; (5) equivalência export × relatório (PBT-05 ao nível de API).

#### Subtasks

- [ ] **ST-01 — Red:** testes de contrato: campos `*Cents` sempre presentes como `long`; 200 com scope correto; 403 com papel errado; 404 anti-enumeração; catálogo estável. PBT-05 ao nível de API: filtros arbitrários → response de relatório e response de export equivalentes.
- [ ] **ST-02 — Green:** implementar testes em `Reporting.Api.Tests` usando WebApplicationFactory ou equivalent; fixtures por papel.
- [ ] **ST-03 — Refactor:** garantir que mensagem de 403/404 não inclui `display_name`, e-mail ou dados de tenant B.
- [ ] **ST-04 — Docs:** não aplicável.
- [ ] **ST-05 — Encerramento:** testes verdes; coverage `Reporting.Api.Tests` ≥ 80%; commit `test(reporting): suite Api contratos RBAC catalogo-erros anti-enumeracao`; push.

#### Critérios de Aceite

- [ ] Cobertura de todos os seis endpoints nos cenários de sucesso e erro.
- [ ] Anti-enumeração: `buId` fora do escopo sempre 404 `REPORT-ERR-004` — nunca diferencia "não existe" de "sem permissão".
- [ ] Nenhuma mensagem de erro contém PII ou dados de outro tenant.
- [ ] PBT-05 ao nível de API green: relatório e export com mesmos filtros produzem dados equivalentes.

---

### TASK-23 — Testes de segurança: PII em logs, Platform Operator bloqueado, cross-tenant (RNF 4, RNF 5)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `test/reporting/23-security-tests` |
| **Worktree** | `git worktree add ../worktrees/reporting/23-security-tests -b test/reporting/23-security-tests` |
| **Status** | [ ] |
| **Depende de** | TASK-22, TASK-18 |
| **Entregável** | Testes automatizados verificando: zero PII em logs; Platform Operator bloqueado em todos os endpoints; cross-tenant em export impossível |
| **Mapeia** | RNF 4, RNF 5, design §13.5, DD-008, ADR-0001, Req 7.4 |
| **Camada principal** | Tests |

#### Objetivo

Implementar os testes de segurança obrigatórios: (1) "zero PII em log" — injetar `ILogger` spy e verificar que `display_name`, e-mail e telefone nunca aparecem em nenhuma entrada de log durante geração de relatório ou export; (2) Platform Operator bloqueado — requisição com papel `platform_operator` retorna 403 `REPORT-ERR-005`/`006` em todos os seis endpoints, antes de qualquer acesso ao banco; (3) cross-tenant no export — signed URL do CSV de tenant A não contém linhas de tenant B.

#### Subtasks

- [ ] **ST-01 — Red:** teste "zero PII em log": gerar relatório de ranking com `display_name` presente na response; verificar que logger spy não capturou nenhuma ocorrência de `display_name`, e-mail ou telefone. Teste Platform Operator: todos os seis endpoints → 403. Teste cross-tenant no export: CSV gerado para tenant A não contém linhas de tenant B.
- [ ] **ST-02 — Green:** implementar usando logger spy (in-memory log sink); simular papéis via JWT de teste; validar CSV conteúdo em teste de integração com banco real.
- [ ] **ST-03 — Refactor:** transformar "zero PII em log" em teste parametrizado pelos cinco tipos de relatório.
- [ ] **ST-04 — Docs:** documentar no README que PBT-03 (infra) e estes testes formam o conjunto de gates de segurança.
- [ ] **ST-05 — Encerramento:** testes verdes; commit `test(reporting): segurança PII-em-logs Platform-Operator cross-tenant-export`; push.

#### Critérios de Aceite

- [ ] Zero ocorrências de `display_name`, e-mail ou telefone em logs durante qualquer geração de relatório.
- [ ] Platform Operator retorna 403 em todos os seis endpoints sem tocar o banco.
- [ ] CSV de tenant A verificado linha a linha sem dados de tenant B.

---

### TASK-24 — Observabilidade: logs estruturados, métricas, traces, alertas e health checks (RNF 6)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `feat/reporting/24-observability` |
| **Worktree** | `git worktree add ../worktrees/reporting/24-observability -b feat/reporting/24-observability` |
| **Status** | [ ] |
| **Depende de** | TASK-12, TASK-21 |
| **Entregável** | Logs estruturados com `correlation_id`/`tenant_id`; métricas `reports_generated_total` e `report_generation_duration_seconds`; health checks `live`/`ready`; configuração de alerta documentada |
| **Mapeia** | RNF 6, design §11, ADR-0001, RNF 4.2 |
| **Camada principal** | Api / Infrastructure |

#### Objetivo

Garantir que toda geração de relatório e export emite: (1) log estruturado JSON com `correlation_id`, `tenant_id`, `report_type`, `filters_hash`, `actor_user_id`, `duration_ms`, `result_rows`, `outcome` — nunca PII (RNF 6.1); (2) métricas Prometheus/OpenTelemetry `reports_generated_total{report_type, outcome}` e histograma `report_generation_duration_seconds{report_type}` (RNF 6.2); (3) health checks `/health/live` e `/health/ready` (Cloud SQL + GCS); (4) configuração de alerta quando `p95 > SLA` (RNF 6.3).

#### Subtasks

- [ ] **ST-01 — Red:** testes: log contém `correlation_id` e `tenant_id` mas não `display_name` nem nome de arquivo PII; métricas incrementam após geração de relatório; `/health/ready` retorna 200 com Cloud SQL e GCS acessíveis.
- [ ] **ST-02 — Green:** implementar emissão de log no `LoggingMetricsBehavior`; registrar métricas via OpenTelemetry (ou Prometheus.NET); implementar health checks para Cloud SQL e Cloud Storage; documentar configuração de alerta.
- [ ] **ST-03 — Refactor:** confirmar que `filters_hash` é hash dos filtros — nunca os valores brutos no log.
- [ ] **ST-04 — Docs:** documentar métricas e alertas no README do módulo.
- [ ] **ST-05 — Encerramento:** testes verdes; commit `feat(reporting): observabilidade logs-estruturados metricas traces health-checks RNF6`; push.

#### Critérios de Aceite

- [ ] Log de geração contém `correlation_id`, `tenant_id`, `report_type`, `duration_ms` — zero PII.
- [ ] Métricas `reports_generated_total` e `report_generation_duration_seconds` emitidas e verificáveis.
- [ ] `/health/live` e `/health/ready` respondendo e cobrindo Cloud SQL + GCS.
- [ ] Configuração de alerta de latência documentada.

---

### TASK-25 — Performance baseline e DoD final (RNF 1, RNF 3, PTV-01, RISK-REPORT-06)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `test/reporting/25-performance-dod` |
| **Worktree** | `git worktree add ../worktrees/reporting/25-performance-dod -b test/reporting/25-performance-dod` |
| **Status** | [ ] |
| **Depende de** | TASK-24, TASK-17 |
| **Entregável** | Teste de carga pré-release confirmando p95 ≤ 3 s e export ≤ 10 s com 2.000 oportunidades/tenant; PTV-01 e RISK-REPORT-06 reconciliados; DoD assinado |
| **Mapeia** | RNF 1, RNF 3, RNF 7, design §15, §19, RISK-REPORT-01, RISK-REPORT-05, RISK-REPORT-06 |
| **Camada principal** | Tests / Docs |

#### Objetivo

Executar o teste de carga pré-release com volume de referência (2.000 oportunidades/tenant, período de 12 meses) e confirmar: p95 ≤ 3.000 ms para os cinco relatórios (RNF 1.1/PTV-01) e export ≤ 10 s (RNF 3.1). Reconciliar: PTV-01 (alvo de latência confirmado com produto) e RISK-REPORT-06 (versão Postgres com `security_invoker` confirmada). Assinar o DoD do design §19.

#### Subtasks

- [ ] **ST-01 — Red:** executar script de carga sem índices ou com banco frio — confirmar violação de SLO (baseline sem otimização como evidência de necessidade).
- [ ] **ST-02 — Green:** executar teste de carga com índices ativos; medir p95 de cada tipo de relatório e do export; registrar resultados.
- [ ] **ST-03 — Refactor:** ajustar `statement_timeout` e pool de conexão se algum relatório ultrapassar o SLO; reexecutar e registrar.
- [ ] **ST-04 — Docs:** atualizar `tasks.md` com status de PTV-01 e RISK-REPORT-06; assinar checklist do DoD (design §19); sincronizar README do módulo.
- [ ] **ST-05 — Encerramento:** resultados de carga registrados; DoD assinado; matriz de rastreabilidade `[X]`; commit `test(reporting): performance-baseline DoD-final PTV01 RISK-REPORT-06`; push.

#### Critérios de Aceite

- [ ] p95 ≤ 3.000 ms para os cinco relatórios com 2.000 oportunidades/tenant e período de 12 meses.
- [ ] Export ≤ 10 s para o mesmo volume.
- [ ] PTV-01 registrado como confirmado ou pendente com SLO provisório justificado.
- [ ] RISK-REPORT-06 registrado como mitigado (Postgres 15+ confirmado) ou fallback documentado.
- [ ] Todos os itens do DoD do design §19 marcados e evidenciados.

---

## 5. Matriz de Rastreabilidade

| Origem | Descrição | TASKs | Status |
|--------|-----------|-------|--------|
| Req 1 | Relatório de funil por estágio | TASK-06, TASK-13 | [ ] |
| Req 2 | Ranking por responsável (PII mínima) | TASK-08, TASK-15 | [ ] |
| Req 3 | Oportunidades por canal de origem | TASK-09, TASK-15 | [ ] |
| Req 4 | Comissões por parceiro (projetado × consolidado) | TASK-10, TASK-16 | [ ] |
| Req 5 | Export CSV (UTF-8 BOM, pt-BR, R$, sem PII desnecessária) | TASK-11, TASK-19, TASK-20 | [ ] |
| Req 6 | Forecast por BU e mês (degradação graciosa) | TASK-07, TASK-14 | [ ] |
| Req 7 | RBAC por escopo verificado no servidor | TASK-04, TASK-08, TASK-12, TASK-21, TASK-22 | [ ] |
| Req 8 | Isolamento por tenant em toda leitura | TASK-04, TASK-13, TASK-14, TASK-15, TASK-16, TASK-18 | [ ] |
| RNF 1 | Latência p95 ≤ 3.000 ms para relatórios ≤ 12 meses | TASK-17, TASK-25 | [ ] |
| RNF 2 | Consistência eventual declarada (Fase 1 síncrona) | TASK-14, TASK-25 | [ ] |
| RNF 3 | Export CSV gerado em ≤ 10 s | TASK-11, TASK-19, TASK-25 | [ ] |
| RNF 4 | Privacidade PII: minimização, zero PII em logs | TASK-03, TASK-08, TASK-12, TASK-23, TASK-24 | [ ] |
| RNF 5 | Platform Operator bloqueado para dados comerciais | TASK-04, TASK-12, TASK-23 | [ ] |
| RNF 6 | Observabilidade: logs, métricas, traces, alertas | TASK-12, TASK-24 | [ ] |
| RNF 7 | Disponibilidade Tier 2; relatório não bloqueia transacional | TASK-12, TASK-25 | [ ] |
| PBT-01 | Snapshot de comissão imutável após mudança de percentual | TASK-10 | [ ] |
| PBT-02 | Conservação da soma de comissão em centavos | TASK-03, TASK-10 | [ ] |
| PBT-03 | Isolamento cross-tenant via RLS anti-vazamento | TASK-18 | [ ] |
| PBT-04 | Soma % por canal = 100% em basis points | TASK-03, TASK-09 | [ ] |
| PBT-05 | Round-trip relatório × export CSV equivalentes | TASK-11, TASK-22 | [ ] |
| DD-001 | Reporting síncrono Fase 1, worker assíncrono Fase 2 | TASK-01, TASK-25 | [ ] |
| DD-002 | Views diretas Fase 1 × projeção por evento Fase 2 | TASK-13..TASK-16 | [ ] |
| DD-003 | Read side sem aggregate transacional | TASK-01, TASK-02, TASK-03 | [ ] |
| DD-004 | Export via signed URL GCS (idempotente) | TASK-11, TASK-19 | [ ] |
| DD-005 | RLS obrigatória com security_invoker nas views | TASK-13..TASK-16, TASK-18 | [ ] |
| DD-006 | Escopo RBAC como predicado de aplicação separado de RLS | TASK-04, TASK-05, TASK-12 | [ ] |
| DD-007 | Money em centavos inteiros fim a fim | TASK-03, TASK-06..TASK-11, TASK-20 | [ ] |
| DD-008 | PII minimizada: display_name só no ranking e fora dos logs | TASK-08, TASK-15, TASK-23 | [ ] |
| DD-009 | Sem cache nem paginação no MVP | Não aplicável (ponto de extensão documentado) | N/A |
| DD-010 | Percentual por canal em basis points inteiros | TASK-03, TASK-09 | [ ] |
| ADR-0001 | Defesa em profundidade multi-tenant | TASK-01, TASK-12, TASK-13..TASK-19 | [ ] |

---

## 6. Coverage Gates

Como o módulo é **read side puro** (DD-003), não há camada de Domain rica com aggregates. Os gates são ajustados para refletir onde a lógica real reside.

| Camada | Gate | Tipo de teste esperado | Observação |
|--------|------|------------------------|------------|
| Domain | ≥ 95% | Unitários de objetos de valor (Money, Period, ChannelShare, StageBucket) + PBT-02 + PBT-04 | Camada fina — sem aggregate; 95% é atingível e obrigatório |
| Application | ≥ 85% | Unitários de handlers (5 queries + export), scope resolver, RbacScopeSpecification, PiiMinimizationPolicy, pipeline behaviors; PBT-01, PBT-04, PBT-05 | Gate reduzido em relação ao padrão (90%) pois sem regra de escrita de negócio |
| Infrastructure | ≥ 70% | Integração com banco (views + RLS), GCS client; PBT-03 obrigatório como gate de CI | Banco real (Postgres) obrigatório para testes de RLS |
| Api | ≥ 80% | Integração e contrato dos seis endpoints; RBAC por papel; catálogo de erros; anti-enumeração | WebApplicationFactory ou equivalent |
| Architecture | 100% das regras críticas | Regras de dependência entre camadas; ausência de aggregate em Domain | Bloqueante no CI |
| Security | Cobertura por cenário crítico | Zero PII em logs; Platform Operator bloqueado; cross-tenant no export | Gates manuais + testes automatizados de segurança |

---

## 7. Critérios de Encerramento

### 7.1 Encerramento de TASK

Uma TASK só pode ser marcada como `[X]` quando:

- Subtasks concluídas.
- Testes aplicáveis verdes.
- Coverage gate da camada atendido ou justificativa registrada.
- Lint/format executado.
- Nenhum warning novo relevante.
- Commit em Conventional Commits realizado.
- Push realizado.
- Documentação atualizada quando aplicável.

### 7.2 Encerramento de Onda

Uma onda só pode ser considerada concluída quando:

- Todas as TASKs da onda estiverem `[X]`.
- CI estiver verde.
- PR da onda estiver aberto, aprovado ou mergeado conforme regra do projeto.
- Riscos da onda estiverem tratados ou registrados.

### 7.3 Encerramento do Módulo

O módulo `reporting` só pode ser considerado pronto quando:

- Todas as seis ondas concluídas.
- Cinco relatórios (funil, forecast, ranking, canal, comissões) funcionando com filtros de período e BU.
- Export CSV UTF-8 com BOM via signed URL GCS funcionando.
- RBAC verificado no servidor em todos os endpoints; Platform Operator bloqueado.
- RLS `security_invoker` ativa nas cinco views; PBT-03 green e bloqueante no CI.
- PBT-01..PBT-05 todos verdes.
- Valores monetários em centavos inteiros fim a fim; zero `float`/`double`/`decimal` monetário.
- p95 ≤ 3 s confirmado para relatórios ≤ 12 meses; export ≤ 10 s.
- Logs/métricas/traces sem PII; health checks `live`/`ready` ativos.
- Catálogo de erros REPORT-ERR-001..009 coberto por testes.
- `Reporting.Architecture.Tests` validando regras de dependência.
- PTV-01 e RISK-REPORT-06 reconciliados.
- `requirements.md`, `design.md` e `tasks.md` consistentes.
- README do módulo sincronizado.

---

## 8. Riscos de Execução

| Código | Risco | Impacto | Mitigação na execução |
|--------|-------|---------|----------------------|
| RISK-REPORT-01 | Queries de relatório impactam latência do transacional | Degradação do pipeline | `QueryTimeoutBehavior` (TASK-12) + índices (TASK-17) + validar em TASK-25 |
| RISK-REPORT-03 | View sem `security_invoker` vaza entre tenants | Incidente sev-1 / LGPD | PBT-03 bloqueante no CI (TASK-18) + `security_invoker` obrigatório em TASK-13..16 |
| RISK-REPORT-04 | PII vazada em log ou export | Violação LGPD | Teste "zero PII em log" (TASK-23) como gate de CI |
| RISK-REPORT-05 / PTV-01 | Alvo p95 não confirmado com produto | SLO indefinido | Confirmar em TASK-25 (teste de carga pré-release) |
| RISK-REPORT-06 | Postgres < 15 sem suporte a `security_invoker` | RLS de view inviável | Confirmar versão em TASK-13; documentar fallback em TASK-25 |

---

## 9. Referências

- docs/product/modules/reporting/requirements.md v0.1.0 (Req 1..8, RNF 1..7, PBT-01..05)
- docs/product/modules/reporting/design.md v0.1.0 (DD-001..010, views, export, catálogo de erros, observabilidade)
- docs/product/modules/reporting/README.md
- docs/product/adr/0001-isolamento-multi-tenant-defesa-em-profundidade.md (ADR-0001)
- docs/product/trd/trd.md §6, §7.4, §8.4, §9, §10.4, §11
- docs/product/data-model/data-model.md §3, §6
- `.forge/rules/architecture/clean-architecture.md`
- `.forge/rules/domain/money-as-cents.md`
- `.forge/rules/conventions/database-naming.md`
- `.forge/rules/testing/tdd.md`, `.forge/rules/testing/quality-gates.md`

