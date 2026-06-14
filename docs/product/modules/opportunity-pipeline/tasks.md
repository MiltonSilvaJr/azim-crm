# Tasks — OP — Opportunity Pipeline (Pipeline de Oportunidades)

- Versão: 0.1.1
- Data: 2026-06-14
- Status: Aprovado para desenvolvimento
- Referência base requirements: docs/product/modules/opportunity-pipeline/requirements.md v0.1.0
- Referência base design: docs/product/modules/opportunity-pipeline/design.md v0.1.0
- ADRs aplicáveis: ADR-0001 (isolamento multi-tenant — Aceito); ADR-0002 (snapshot imutável — a formalizar); ADR-0003 (unicidade opportunity_number — a formalizar); ADR-0004 (Outbox + idempotência de consumers — a formalizar)
- Rules aplicáveis: `.forge/rules/architecture/clean-architecture.md`, `.forge/rules/architecture/ddd.md`, `.forge/rules/architecture/api-and-contracts.md`, `.forge/rules/architecture/observability.md`, `.forge/rules/architecture/security-and-compliance.md`, `.forge/rules/architecture/jwt-authentication.md`, `.forge/rules/architecture/mtls-internal-services.md`, `.forge/rules/domain/money-as-cents.md`, `.forge/rules/domain/nbr-5891-rounding.md`, `.forge/rules/domain/audit-immutability.md`, `.forge/rules/conventions/database-naming.md`, `.forge/rules/conventions/language-policy.md`, `.forge/rules/conventions/document-versioning.md`

> **VAL-07 RESOLVIDA (decisão de produto, HITL #1, 11/06/2026):** ao mover para **Ganho** com parceiro vinculado e percentuais de comissão em branco, o sistema **alerta com confirmação** (não bloqueia). `WinOpportunityCommand` (TASK-10) implementa o ponto de extensão `commission_required_on_win = false` (default: snapshot com comissão zero permitido após confirmação explícita do usuário). Deixa de ser pré-condição bloqueante para "Aprovado para desenvolvimento".

## Histórico de Versões

| Versão | Data | Status | Descrição da alteração |
|--------|------|--------|------------------------|
| 0.1.0 | 2026-06-11 | Rascunho para revisão | Criação inicial derivada de requirements.md v0.1.0 e design.md v0.1.0 (Req 1..20, RNF 1..12, PBT-01..11, DD-001..007, INV-1..13). |
| 0.1.1 | 2026-06-14 | Aprovado para desenvolvimento | Aprovação humana (HITL #1, VAL-07 resolvida); execução via `/forge:coding-loop` autônomo (6 ondas). |

---

## 1. Convenções de Implementação

### 1.1 TDD-first

Toda implementação com lógica verificável segue o ciclo:

1. Red — escrever teste que falha
2. Green — implementar o mínimo para passar
3. Refactor — melhorar sem alterar comportamento

Nenhuma regra de domínio, handler, endpoint, persistência, cálculo monetário ou integração é concluída sem teste correspondente.

### 1.2 Property-Based Testing

PBT é obrigatório para invariantes matemáticas (TCV, forecast, comissão, forecast líquido), idempotência (estagnação), imutabilidade (snapshot, opportunity_number), state machines (OpportunityLifecycle) e anti-enumeração (isolamento cross-tenant). Biblioteca: FsCheck integrado com xUnit. Cada PBT mapeia explicitamente para `PBT-NN` do requirements.md.

### 1.3 Bite-sized Tasks

Cada subtask deve ser estimada em menos de 2 horas. Cada TASK deve ter preferencialmente até 1 dia; no máximo 2 dias com escopo claro e entregável verificável.

### 1.4 Branch Model

```text
<tipo>/opportunity-pipeline/<NN>-<slug>
```

Exemplos: `feat/opportunity-pipeline/01-bootstrap-clean-architecture`, `test/opportunity-pipeline/06-domain-aggregate-create-move`.

### 1.5 Git Worktree

```sh
git worktree add ../worktrees/opportunity-pipeline/<NN>-<slug> -b <branch>
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

```text
TASK-NN — <título>   ← unidade atômica de invocação do task-coder
  ST-MM — <subtask>  ← etapas TDD internas; numeração reinicia a cada TASK
```

Onda é atributo (`**Onda**` no header da TASK) e seção de agrupamento visual em §3 — nunca entra no ID da TASK nem nas subtasks.

---

## 2. Status Geral

| TASK | Título | Onda | Branch | Status |
|------|--------|------|--------|--------|
| TASK-01 | Solution .NET + 5 projetos Clean Architecture + Architecture.Tests | Onda 1 | `feat/opportunity-pipeline/01-bootstrap-clean-architecture` | [X] |
| TASK-02 | Objetos de valor financeiros — Money, NbrRounding, ContractValue, Probability | Onda 2 | `test/opportunity-pipeline/02-domain-financial-value-objects` | [X] |
| TASK-03 | Objetos de valor de domínio — OpportunityNumber, StageCategory, *Ref, CommissionTerms/Calculation/Role, ContactLink | Onda 2 | `test/opportunity-pipeline/03-domain-reference-value-objects` | [X] |
| TASK-04 | Serviços de domínio puros + Policies — calculators, forecast, NbrRounding (PBT-03..06) | Onda 2 | `test/opportunity-pipeline/04-domain-calculators-policies` | [X] |
| TASK-05 | Entidades internas + eventos de domínio + exceções + interfaces de repositório e portas | Onda 2 | `feat/opportunity-pipeline/05-domain-entities-events-ports` | [X] |
| TASK-06 | Aggregate Opportunity — Create, MoveStage, OpportunityLifecycle (PBT-02, PBT-08) | Onda 2 | `test/opportunity-pipeline/06-domain-aggregate-create-move` | [X] |
| TASK-07 | Aggregate — Win, Lose, Reopen, MarkStale, SetPartnerCommission, LinkContact (PBT-07, PBT-11) | Onda 2 | `test/opportunity-pipeline/07-domain-aggregate-win-lose-reopen` | [X] |
| TASK-08 | Pipeline behaviors — Logging, Tenant, Rbac, Idempotency, Validation, Transaction | Onda 3 | `feat/opportunity-pipeline/08-application-pipeline-behaviors` | [X] |
| TASK-09 | Commands de criação/edição/movimentação — CreateOpportunity, UpdateOpportunity, MoveStage | Onda 3 | `feat/opportunity-pipeline/09-application-create-update-move` | [X] |
| TASK-10 | Commands de fechamento — WinOpportunity, LoseOpportunity, ReopenOpportunity + handlers transacionais | Onda 3 | `feat/opportunity-pipeline/10-application-win-lose-reopen` | [X] |
| TASK-11 | Commands de comissão e contatos — SetPartnerCommission, LinkContact, UnlinkContact, SaveFilter | Onda 3 | `feat/opportunity-pipeline/11-application-commission-contact-filter` | [X] |
| TASK-12 | Queries (8) + StagnationDetectionService (Application Service) — PBT-09 | Onda 3 | `feat/opportunity-pipeline/12-application-queries-stagnation` | [X] |
| TASK-13 | DbContext + EF mapeamentos + Global Query Filter + migrations (7 tabelas) + RLS + REVOKE | Onda 4 | `feat/opportunity-pipeline/13-infra-dbcontext-migrations-rls` | [X] |
| TASK-14 | Trigger trg_block_snapshot_mutation + constraint uq_active_snapshot + imutabilidade no banco (PBT-07) | Onda 4 | `feat/opportunity-pipeline/14-infra-snapshot-trigger` | [X] |
| TASK-15 | OpportunityNumberGenerator + lock atômico por tenant + teste de concorrência (PBT-01) | Onda 4 | `test/opportunity-pipeline/15-infra-opportunity-number-generator` | [X] |
| TASK-16 | RlsConnectionInterceptor + OutboxPublisher + AuditPublisher + PiiMasker | Onda 4 | `feat/opportunity-pipeline/16-infra-rls-outbox-audit` | [X] |
| TASK-17 | Adaptadores de portas (Organization, Account, Partner, Activity) + Polly + cache de configuração | Onda 4 | `feat/opportunity-pipeline/17-infra-read-port-adapters` | [X] |
| TASK-18 | StaleScanEndpointHandler + stale_detection_runs + idempotência de estagnação (PBT-09 infra) | Onda 4 | `feat/opportunity-pipeline/18-infra-stale-scan-handler` | [X] |
| TASK-19 | Contratos públicos — DTOs, requests/responses, envelopes .v1, catálogo OP-ERR-001..017 | Onda 5 | `feat/opportunity-pipeline/19-contracts-dtos-events-errors` | [X] |
| TASK-20 | Controllers (OpportunitiesController, CommissionsController, KanbanController) + RBAC + ProblemDetails + OpenAPI | Onda 5 | `feat/opportunity-pipeline/20-api-controllers-rbac-openapi` | [X] |
| TASK-21 | InternalPipelineController + mTLS + testes RBAC 200/403 por papel × capacidade | Onda 5 | `feat/opportunity-pipeline/21-api-internal-rbac-tests` | [X] |
| TASK-22 | Testes de isolamento cross-tenant + gate CI KPI-06 (PBT-10, RNF 3) | Onda 6 | `test/opportunity-pipeline/22-hardening-cross-tenant-isolation` | [X] |
| TASK-23 | Teste de carga Kanban 500–2.000 oportunidades p95 ≤ 2.000 ms + índices + paginação (RNF 1) | Onda 6 | `test/opportunity-pipeline/23-hardening-kanban-load-test` | [X] |
| TASK-24 | Observabilidade completa (logs, métricas, traces, health checks, alertas) + scan PII + DoD final | Onda 6 | `feat/opportunity-pipeline/24-hardening-observability-dod` | [X] |

---

## 3. Ondas de Implementação

| Onda | Foco | TASKs | Critério de fechamento |
|------|------|-------|------------------------|
| Onda 1 | Bootstrap — solution, projetos, regra de dependência, CI mínimo | TASK-01 | Architecture.Tests verde; build limpo |
| Onda 2 | Domain — VOs, entidades, aggregate, state machine, calculators, PBTs | TASK-02..TASK-07 | Domain.Tests ≥ 95%; PBT-02..08 verdes |
| Onda 3 | Application — CQRS handlers, behaviors, queries, StagnationDetectionService | TASK-08..TASK-12 | Application.Tests ≥ 85%; PBT-09 verde |
| Onda 4 | Infrastructure — EF, migrations, RLS, numeração atômica, outbox, portas | TASK-13..TASK-18 | Infrastructure.Tests ≥ 70%; PBT-01, PBT-07 verdes; RLS habilitada |
| Onda 5 | API + Contracts — controllers, contratos, RBAC, OpenAPI, endpoints internos | TASK-19..TASK-21 | Api.Tests ≥ 80%; todas rotas com RBAC verificado |
| Onda 6 | Hardening — isolamento CI, carga Kanban, observabilidade, DoD | TASK-22..TASK-24 | PBT-10 verde; p95 Kanban ≤ 2.000 ms; sem PII em logs; DoD assinado |

---

## 4. Tarefas

---

### TASK-01 — Solution .NET + 5 projetos Clean Architecture + Architecture.Tests

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 1 — Bootstrap |
| **Branch** | `feat/opportunity-pipeline/01-bootstrap-clean-architecture` |
| **Worktree** | `git worktree add ../worktrees/opportunity-pipeline/01-bootstrap-clean-architecture -b feat/opportunity-pipeline/01-bootstrap-clean-architecture` |
| **Status** | [ ] |
| **Depende de** | Não aplicável |
| **Entregável** | Solution `OpportunityPipeline.sln` com 5 projetos de produção + 5 projetos de teste; regra de dependência validada por `Architecture.Tests`; CI mínimo verde. |
| **Mapeia** | P1 (Clean Architecture); design §3; Req 1..20 (estrutura base) |
| **Camada principal** | DevOps / Architecture |

#### Objetivo

Criar a estrutura de solution conforme design §3: `OpportunityPipeline.Domain`, `OpportunityPipeline.Application`, `OpportunityPipeline.Infrastructure`, `OpportunityPipeline.Api`, `OpportunityPipeline.Contracts` e os projetos de teste correspondentes (`*.Domain.Tests`, `*.Application.Tests`, `*.Infrastructure.Tests`, `*.Api.Tests`, `*.Architecture.Tests`). Validar a regra de dependência (Domain → ∅; Application → Domain, Contracts; Infrastructure → Application, Domain; Api → Application, Infrastructure, Contracts; Contracts → ∅) via ArchUnitNET ou equivalente em `Architecture.Tests`.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes em `Architecture.Tests` declarando as regras de dependência entre as 5 camadas; verificar que falham (sem código de produção ainda).
- [ ] **ST-02 — Green:** criar solution com `dotnet new sln` + 5 projetos de produção + 5 de teste com as referências corretas; executar testes de arquitetura — devem passar.
- [ ] **ST-03 — Refactor:** configurar `.editorconfig`, analyzers (Nullable enable, TreatWarningsAsErrors), `Directory.Build.props` com versões de pacote centralizadas.
- [ ] **ST-04 — Docs:** atualizar README do módulo com status da onda.
- [ ] **ST-05 — Encerramento:** build limpo sem warnings; `Architecture.Tests` 100% verde; commit `feat(opportunity-pipeline): bootstrap solution clean architecture` + push.

#### Critérios de Aceite

- [ ] 5 projetos de produção + 5 de teste criados e compilando.
- [ ] Regra de dependência entre camadas validada por teste automatizado (100% das regras declaradas).
- [ ] Nullable habilitado; zero warnings de build.
- [ ] CI executa `dotnet build` e `dotnet test` sem falha.

---

### TASK-02 — Objetos de valor financeiros — Money, NbrRounding, ContractValue, Probability

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `test/opportunity-pipeline/02-domain-financial-value-objects` |
| **Worktree** | `git worktree add ../worktrees/opportunity-pipeline/02-domain-financial-value-objects -b test/opportunity-pipeline/02-domain-financial-value-objects` |
| **Status** | [ ] |
| **Depende de** | TASK-01 |
| **Entregável** | Objetos de valor `Money`, `NbrRounding`, `ContractValue`, `Probability` implementados com invariantes verificadas por testes unitários e PBT. |
| **Mapeia** | Req 8 (valor_total, forecast), RNF 11 (centavos, NBR 5891), PBT-03 (invariante TCV), PBT-04 (invariante forecast), DD-004 (Money em centavos) |
| **Camada principal** | Domain |

#### Objetivo

Implementar os objetos de valor monetários da base do domínio. `Money` armazena centavos em `long` (nunca `float`/`double`); `NbrRounding` encapsula arredondamento ToEven (NBR 5891) para toda divisão monetária; `ContractValue` agrega `valor_setup + valor_mensal × duracao_meses`; `Probability` restringe ao intervalo [0, 100]. Todos imutáveis, com igualdade por valor e validação no construtor. PBT-03 e PBT-04 são cobertos com FsCheck.

#### Subtasks

- [ ] **ST-01 — Red:** escrever PBT (FsCheck) para `ContractValue`: para quaisquer `setup ≥ 0`, `mensal ≥ 0`, `meses ≥ 0`, `valor_total = setup + mensal × meses` sem perda de precisão (PBT-03).
- [ ] **ST-02 — Red:** escrever PBT para `NbrRounding.ToEven`: para `valor_total ≥ 0` e `probabilidade` ∈ [0, 100], `forecast = round(valor_total × probabilidade / 100)` com `0 ≤ forecast ≤ valor_total` (PBT-04 parcial); escrever testes unitários para `Money` (não negativo, aritmética inteira) e `Probability` (bounds e exceção em fora do intervalo).
- [ ] **ST-03 — Green:** implementar `Money` (centavos `long`, validação ≥ 0, sem construtores com `float`/`double`), `NbrRounding` (método estático `RoundHalfToEven(long numerator, long denominator)`), `ContractValue` (construtor valida `duracao_meses > 0` quando `valor_mensal > 0`), `Probability` (validação [0, 100] com `DomainException`).
- [ ] **ST-04 — Refactor:** garantir igualdade por valor (`record` C# ou `IEquatable`), imutabilidade (`init`-only ou `readonly`), sem dependências externas.
- [ ] **ST-05 — Encerramento:** PBT-03 e PBT-04 verdes; cobertura Domain.Tests ≥ 95% nas classes implementadas; commit `test(opportunity-pipeline): domain financial value objects PBT-03 PBT-04` + push.

#### Critérios de Aceite

- [ ] PBT-03 (invariante TCV) verde com FsCheck ≥ 100 amostras.
- [ ] PBT-04 (invariante forecast_ponderado) verde.
- [ ] `Money` não aceita valor negativo; construtor sem `float`/`double`.
- [ ] `NbrRounding.RoundHalfToEven` produz resultados idênticos ao `MidpointRounding.ToEven` do .NET para todos os casos de aresta testados.
- [ ] `Probability(101)` e `Probability(-1)` lançam exceção de domínio.
- [ ] Zero uso de `float`/`double` no código de produção dessas classes.

---

### TASK-03 — Objetos de valor de domínio — OpportunityNumber, StageCategory, *Ref, CommissionTerms/Calculation/Role, ContactLink

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `test/opportunity-pipeline/03-domain-reference-value-objects` |
| **Worktree** | `git worktree add ../worktrees/opportunity-pipeline/03-domain-reference-value-objects -b test/opportunity-pipeline/03-domain-reference-value-objects` |
| **Status** | [ ] |
| **Depende de** | TASK-02 |
| **Entregável** | Objetos de valor de referência e comissão implementados: `OpportunityNumber`, `StageCategory`, `StageRef`, `OriginChannelRef`, `LossReasonRef`, `CommissionRole`, `CommissionTerms`, `CommissionCalculation`, `CommissionDefaults`, `ContactLink`. |
| **Mapeia** | Req 3 (OpportunityNumber imutável), Req 4 (OriginChannelRef), Req 6 (StageCategory), Req 11 (CommissionTerms, CommissionRole), Req 16 (ContactLink), PBT-02 (imutabilidade number), PBT-05 (comissão por componente parcial) |
| **Camada principal** | Domain |

#### Objetivo

Implementar os objetos de valor de referência e de comissão. `OpportunityNumber` valida o formato `^AZ-\d{4,}$` e é imutável. `StageCategory` é enum com `open`, `won`, `lost`. `StageRef`, `OriginChannelRef`, `LossReasonRef` são snapshots leves. `CommissionTerms` encapsula papel, percentuais e a regra de mutual exclusão `valor_fixo` × percentuais. `CommissionCalculation` é resultado imutável. `ContactLink` guarda `contact_id` + `is_primary`. PBT-02 verifica que `OpportunityNumber` não muta sob sequência de operações.

#### Subtasks

- [ ] **ST-01 — Red:** escrever PBT para `OpportunityNumber`: para qualquer string `AZ-NNNN` válida, o valor após criar e recuperar é idêntico (PBT-02 parcial); escrever testes de formato inválido (ex.: `AZ-12`, `BZ-0001`, `AZ-abc`).
- [ ] **ST-02 — Red:** escrever testes para `CommissionTerms`: mutual exclusão `valor_fixo` × percentuais; percentuais em [0, 100]; `meses_comissionados ≥ 0`.
- [ ] **ST-03 — Green:** implementar `OpportunityNumber` (validação regex, `ToString()` retorna string formatada), `StageCategory` (enum), `StageRef`, `OriginChannelRef` (com `is_partner_channel`), `LossReasonRef`.
- [ ] **ST-04 — Green:** implementar `CommissionRole` (enum), `CommissionTerms` (validações, mutual exclusão), `CommissionCalculation` (imutável com `comissao_setup`, `comissao_recorrente`, `comissao_total`), `CommissionDefaults`, `ContactLink`.
- [ ] **ST-05 — Refactor:** revisar imutabilidade (`record`/`init`), consistência de nomenclatura, sem dependências externas.
- [ ] **ST-06 — Encerramento:** PBT-02 parcial verde; testes unitários de todos os objetos verdes; cobertura ≥ 95% nas classes; commit `test(opportunity-pipeline): domain reference commission value objects PBT-02` + push.

#### Critérios de Aceite

- [ ] `OpportunityNumber("AZ-0001")` válido; `OpportunityNumber("AZ-12")` e `OpportunityNumber("BZ-001")` lançam exceção de domínio.
- [ ] `CommissionTerms` com `valor_fixo > 0` e `pct_setup > 0` ao mesmo tempo lança exceção (mutual exclusão).
- [ ] Todos os objetos de valor são imutáveis (sem setters públicos mutáveis).
- [ ] `StageCategory.open`, `won`, `lost` cobrem todos os estados da máquina de estados.
- [ ] PBT-02 parcial verde (imutabilidade de `OpportunityNumber`).

---

### TASK-04 — Serviços de domínio puros + Policies (PBT-03..06)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `test/opportunity-pipeline/04-domain-calculators-policies` |
| **Worktree** | `git worktree add ../worktrees/opportunity-pipeline/04-domain-calculators-policies -b test/opportunity-pipeline/04-domain-calculators-policies` |
| **Status** | [ ] |
| **Depende de** | TASK-02, TASK-03 |
| **Entregável** | Serviços de domínio puros e policies implementados: `CommissionCalculator`, `ForecastCalculator`, `NetForecastCalculator`, `ExpectedCloseDatePolicy`, `StagnationSpecification`, `OverdueSpecification`. PBT-03..06 verdes. |
| **Mapeia** | Req 8 (forecast_ponderado), Req 12 (comissão), Req 13 (forecast líquido), Req 9 (ExpectedCloseDatePolicy), Req 17 (StagnationSpecification), RNF 11, PBT-03, PBT-04, PBT-05, PBT-06, DD-004 |
| **Camada principal** | Domain |

#### Objetivo

Implementar os serviços de domínio puros (sem dependências de infraestrutura). `CommissionCalculator` calcula comissão por componente com `NbrRounding.ToEven` (PBT-05). `ForecastCalculator` calcula `forecast_ponderado` (PBT-04). `NetForecastCalculator` calcula `comissao_ponderada` e `forecast_liquido` (PBT-06). `ExpectedCloseDatePolicy` decide se `expected_close_date` é obrigatória por `stage.order`. `StagnationSpecification` detecta oportunidades com `last_activity_at` há ≥ 14 dias corridos. `OverdueSpecification` detecta `expected_close_date` no passado.

#### Subtasks

- [ ] **ST-01 — Red:** escrever PBT-04 completo: para `valor_total ≥ 0` e `probabilidade ∈ [0, 100]`, `ForecastCalculator.Calculate` retorna `round(valor_total × prob / 100)` com `0 ≤ resultado ≤ valor_total`.
- [ ] **ST-02 — Red:** escrever PBT-05: para valores e percentuais válidos, `CommissionCalculator` produz `comissao_setup = round(setup × pct_setup / 100)`, `comissao_recorrente = round(mensal × meses × pct_recorrente / 100)`, `comissao_total = setup + recorrente`; quando `valor_fixo` definido, `comissao_total = valor_fixo`; todos não negativos.
- [ ] **ST-03 — Red:** escrever PBT-06: para qualquer oportunidade, `forecast_liquido = forecast_ponderado − round(comissao_total × prob / 100)` e `forecast_liquido ≤ forecast_ponderado`; sem comissão, `forecast_liquido = forecast_ponderado`.
- [ ] **ST-04 — Green:** implementar `ForecastCalculator`, `CommissionCalculator` (via `NbrRounding`), `NetForecastCalculator`.
- [ ] **ST-05 — Green:** implementar `ExpectedCloseDatePolicy` (compara `stage.order` com `orderOfPropostaEnviada`), `StagnationSpecification` (14 dias corridos, usa `IClock`), `OverdueSpecification` (data esperada < hoje, usa `IClock`).
- [ ] **ST-06 — Refactor:** garantir pureza (sem I/O, sem dependências de infra), 100% determinístico dado o `IClock`.
- [ ] **ST-07 — Encerramento:** PBT-03..06 verdes; cobertura Domain ≥ 95% nas classes novas; commit `test(opportunity-pipeline): domain calculators policies PBT-04 PBT-05 PBT-06` + push.

#### Critérios de Aceite

- [ ] PBT-04 verde (ForecastCalculator).
- [ ] PBT-05 verde (CommissionCalculator, incluindo caso `valor_fixo`).
- [ ] PBT-06 verde (NetForecastCalculator, incluindo caso sem comissão).
- [ ] Nenhum uso de `float`/`double` nos calculators.
- [ ] `ExpectedCloseDatePolicy.IsRequired(stage)` retorna `true` para estágio com order ≥ "Proposta Enviada" e `false` para anteriores.
- [ ] `StagnationSpecification.IsSatisfiedBy(opp, now)` retorna `true` apenas para oportunidades `open` com `last_activity_at < now − 14 dias`.

---

### TASK-05 — Entidades internas + eventos de domínio + exceções + interfaces de repositório e portas

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `feat/opportunity-pipeline/05-domain-entities-events-ports` |
| **Worktree** | `git worktree add ../worktrees/opportunity-pipeline/05-domain-entities-events-ports -b feat/opportunity-pipeline/05-domain-entities-events-ports` |
| **Status** | [ ] |
| **Depende de** | TASK-04 |
| **Entregável** | Entidades internas (`OpportunityStageTransition`, `OpportunityPartnerCommission`, `OpportunityContactLink`), 8 eventos de domínio, 9 exceções de domínio tipadas, interfaces `IOpportunityRepository`, `IOpportunityNumberGenerator` e portas de aplicação. |
| **Mapeia** | Req 5 (transições), Req 11 (comissão entidade), Req 16 (contacts), Req 20 (eventos), RNF 6 (AuditLog), RNF 7 (append-only), DD-002 (mecânica snapshot), DD-003 (comissão como entidade) |
| **Camada principal** | Domain |

#### Objetivo

Definir as fronteiras das entidades internas do agregado (sem o agregado ainda): `OpportunityStageTransition` (append-only, imutável após inserção), `OpportunityPartnerCommission` (dois estados: projetada → snapshot via `Freeze`), `OpportunityContactLink`. Criar os 8 eventos de domínio com payload sem PII. Criar exceções de domínio tipadas (uma por invariante de negócio). Declarar as interfaces `IOpportunityRepository`, `IOpportunityNumberGenerator` e as portas de aplicação (`IOrganizationReadPort`, `IAccountReadPort`, `IPartnerReadPort`, `IActivityReadPort`, `IAuditPublisher`, `IDomainEventDispatcher`, `IClock`).

#### Subtasks

- [ ] **ST-01 — Red:** escrever teste que verifica `OpportunityPartnerCommission.Freeze()` cria snapshot com `is_snapshot = true`, `snapshot_at` preenchido e que os termos congelados são iguais aos termos da projetada no momento do freeze.
- [ ] **ST-02 — Red:** escrever teste que verifica que a tentativa de alterar qualquer campo de uma `OpportunityPartnerCommission` com `is_snapshot = true` lança `SnapshotImmutableException`.
- [ ] **ST-03 — Green:** implementar `OpportunityStageTransition` (todos os campos `init`-only), `OpportunityPartnerCommission` (com método `Freeze()` que retorna novo registro snapshot), `OpportunityContactLink`.
- [ ] **ST-04 — Green:** implementar os 8 eventos de domínio como `record` imutável com campos sem PII: `OpportunityCreated`, `OpportunityStageChanged`, `OpportunityWon`, `OpportunityLost`, `OpportunityStale`, `OpportunityReopened`, `CommissionCalculated`, `CommissionSnapshotCreated`.
- [ ] **ST-05 — Green:** implementar exceções tipadas (herdam de `DomainException`): `OwnerRequiredException`, `StageRequiredException`, `OriginChannelRequiredException`, `PartnerRequiredException`, `LossReasonRequiredException`, `ExpectedCloseDateRequiredException`, `InvalidStageTransitionException`, `SnapshotImmutableException`, `PrimaryContactRequiredException`.
- [ ] **ST-06 — Green:** declarar interfaces: `IOpportunityRepository` (`GetById`, `GetByNumber`, `Add`, `Save`), `IOpportunityNumberGenerator` (`NextAsync`), e as 7 portas de aplicação com contratos mínimos.
- [ ] **ST-07 — Encerramento:** testes de `Freeze()` e imutabilidade da entidade verdes; sem PII nos eventos; commit `feat(opportunity-pipeline): domain entities events exceptions ports` + push.

#### Critérios de Aceite

- [ ] `OpportunityPartnerCommission.Freeze()` retorna nova instância com `is_snapshot = true` e termos congelados.
- [ ] Tentativa de mutar snapshot lança `SnapshotImmutableException`.
- [ ] 8 eventos de domínio declarados como `record` sem campos PII (sem `contact_name`, `email`, etc.).
- [ ] 9 exceções de domínio tipadas, cada uma mapeando para 1 código `OP-ERR-*` do catálogo.
- [ ] Interfaces de repositório e portas declaradas no `Domain` (sem dependências de infraestrutura).

---

### TASK-06 — Aggregate Opportunity — Create, MoveStage, OpportunityLifecycle (PBT-02, PBT-08)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `test/opportunity-pipeline/06-domain-aggregate-create-move` |
| **Worktree** | `git worktree add ../worktrees/opportunity-pipeline/06-domain-aggregate-create-move -b test/opportunity-pipeline/06-domain-aggregate-create-move` |
| **Status** | [ ] |
| **Depende de** | TASK-05 |
| **Entregável** | Aggregate root `Opportunity.Create` com invariantes INV-1..INV-9 + método `MoveStage` + `OpportunityLifecycle` (máquina de estados). PBT-02 e PBT-08 verdes. |
| **Mapeia** | Req 1 (criar), Req 2 (owner), Req 4 (canal), Req 5 (transição), Req 6 (state machine), Req 7 (probabilidade default), Req 8 (TCV, INV-8), Req 9 (data fechamento), PBT-02 (imutabilidade number), PBT-08 (máquina de estados), INV-1..9 |
| **Camada principal** | Domain |

#### Objetivo

Implementar a fábrica estática `Opportunity.Create(...)` validando INV-1..5, INV-8, INV-9, emitindo `OpportunityCreated` e registrando a transição inicial. Implementar `Opportunity.MoveStage(targetStage, actor)` com delegação para `OpportunityLifecycle` (guardas INV-6, transição `open → open` válida, rejeição de transições inválidas sem efeito colateral). PBT-08 aplica sequências aleatórias de transições e verifica que apenas as válidas são aceitas.

#### Subtasks

- [ ] **ST-01 — Red:** escrever PBT-08 (FsCheck): para qualquer sequência de `StageCategory` gerada aleatoriamente aplicada sobre uma oportunidade `open`, apenas transições válidas (`open → open`, `open → won`, `open → lost`) alteram o estado; transições inválidas (`won → lost`, `lost → won`, etc.) lançam `InvalidStageTransitionException` sem registrar transição nem emitir evento.
- [ ] **ST-02 — Red:** escrever PBT-02 completo: para qualquer sequência de chamadas `MoveStage`, o `opportunity_number` permanece idêntico ao gerado na criação.
- [ ] **ST-03 — Red:** escrever testes unitários para `Opportunity.Create`: ausência de `owner_id` → `OwnerRequiredException`; canal Parceiro sem `partner_id` → `PartnerRequiredException`; `probability` fora de [0, 100] → `DomainException`; criação válida emite `OpportunityCreated` e tem `stage_category = open`.
- [ ] **ST-04 — Green:** implementar `Opportunity` (aggregate root), fábrica `Create`, estado principal (campos do design §4.1), lista interna de entidades e domain events.
- [ ] **ST-05 — Green:** implementar `OpportunityLifecycle` (guardas de transição por `StageCategory`; lança `InvalidStageTransitionException` antes de qualquer mutação para transições inválidas).
- [ ] **ST-06 — Green:** implementar `MoveStage` (valida `ExpectedCloseDatePolicy` via guarda INV-6; adiciona `OpportunityStageTransition`; atualiza `stage` e `probability` default; recalcula `ContractValue`/forecast via INV-8; emite `OpportunityStageChanged`).
- [ ] **ST-07 — Refactor:** garantir que `InvalidStageTransitionException` é lançada **antes** de qualquer mutação (aggregate permanece inalterado em caso de transição inválida).
- [ ] **ST-08 — Encerramento:** PBT-02 e PBT-08 verdes; INV-1..9 com testes unitários verdes; commit `test(opportunity-pipeline): domain aggregate create move-stage PBT-02 PBT-08` + push.

#### Critérios de Aceite

- [ ] PBT-02 verde: `opportunity_number` imutável sob qualquer sequência de operações.
- [ ] PBT-08 verde: transições inválidas rejeitadas sem efeito colateral (sem registro de transição, sem evento emitido).
- [ ] INV-1 (`owner_id` não nulo): `Create` sem `owner_id` lança exceção.
- [ ] INV-4 (canal Parceiro exige `partner_id`): guarda funciona corretamente.
- [ ] INV-6 (data de fechamento obrigatória): `MoveStage` para estágio ≥ "Proposta Enviada" sem data lança exceção.
- [ ] Criação válida emite exatamente 1 `OpportunityCreated` e registra 1 `OpportunityStageTransition`.

---

### TASK-07 — Aggregate — Win, Lose, Reopen, MarkStale, SetPartnerCommission, LinkContact (PBT-07, PBT-11)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `test/opportunity-pipeline/07-domain-aggregate-win-lose-reopen` |
| **Worktree** | `git worktree add ../worktrees/opportunity-pipeline/07-domain-aggregate-win-lose-reopen -b test/opportunity-pipeline/07-domain-aggregate-win-lose-reopen` |
| **Status** | [ ] |
| **Depende de** | TASK-06 |
| **Entregável** | Métodos `Win`, `Lose`, `Reopen`, `MarkStale`, `SetPartnerCommission`, `UpdateContractValue`, `OverrideProbability`, `LinkContact`, `UnlinkContact`, `PromotePrimary` implementados com invariantes e PBTs verdes. |
| **Mapeia** | Req 10 (perder com motivo), Req 11 (comissão), Req 12 (cálculo), Req 14 (win + snapshot), Req 15 (reopen), Req 16 (contacts INV-11), Req 17 (stale), RNF 5 (imutabilidade snapshot), PBT-07 (snapshot imutável), PBT-11 (snapshot preservado após alterar pct), INV-7, INV-10..13, DD-002 |
| **Camada principal** | Domain |

#### Objetivo

Completar os comportamentos do agregado. `Win(actor)` executa a guarda de categoria (`open → won`), chama `Freeze()` na `OpportunityPartnerCommission` projetada, define `closed_at`, adiciona transição, emite `OpportunityWon` + `CommissionSnapshotCreated`. `Lose` valida INV-7 (motivo obrigatório), define `closed_at`, emite `OpportunityLost`. `Reopen` guarda `won|lost → open`, preserva snapshot, limpa `closed_at`, emite `OpportunityReopened`. PBT-07 e PBT-11 garantem imutabilidade total do snapshot mesmo após reabertura e alteração de percentuais.

#### Subtasks

- [ ] **ST-01 — Red:** escrever PBT-07: para qualquer oportunidade ganha com snapshot (`is_snapshot = true`), qualquer sequência de operações (`Reopen`, `SetPartnerCommission`, `UpdateContractValue`) deixa os campos do snapshot inalterados; snapshot não pode ter `is_snapshot` alterado para `false`.
- [ ] **ST-02 — Red:** escrever PBT-11: para qualquer oportunidade ganha com snapshot S, alterar `CommissionTerms` (via `SetPartnerCommission` em nova projetada após reabertura) não altera S.
- [ ] **ST-03 — Red:** escrever testes unitários: `Lose` sem `loss_reason` → `LossReasonRequiredException`; `Win` em oportunidade `won` → `InvalidStageTransitionException`; `Reopen` emite `OpportunityReopened` e `stage_category = open`; contatos com 0 principais → `PrimaryContactRequiredException`.
- [ ] **ST-04 — Green:** implementar `Win` (guarda open→won, `Freeze`, `closed_at`, transição, eventos `OpportunityWon` + `CommissionSnapshotCreated`); implementar `Lose` (guarda INV-7, `closed_at`, transição, evento `OpportunityLost`).
- [ ] **ST-05 — Green:** implementar `Reopen` (guarda `won|lost → open`, preserva snapshot existente, limpa `closed_at`, transição, evento `OpportunityReopened`); `MarkStale` (idempotente via flag interna, evento `OpportunityStale`).
- [ ] **ST-06 — Green:** implementar `SetPartnerCommission` (cria/atualiza projetada não-snapshot, valida INV-4/INV-10, recalcula comissão via `CommissionCalculator`, emite `CommissionCalculated`); `UpdateContractValue`/`OverrideProbability` (recalculam INV-8 e comissão projetada).
- [ ] **ST-07 — Green:** implementar `LinkContact`, `UnlinkContact`, `PromotePrimary` (mantêm INV-11: exatamente 1 principal quando ≥ 1 contato).
- [ ] **ST-08 — Refactor:** revisar transações de estado; garantir que `Win` é atômica (snapshot criado na mesma operação de domínio que a transição).
- [ ] **ST-09 — Encerramento:** PBT-07 e PBT-11 verdes; INV-7..13 com testes unitários verdes; cobertura Domain ≥ 95%; commit `test(opportunity-pipeline): domain aggregate win-lose-reopen PBT-07 PBT-11` + push.

#### Critérios de Aceite

- [ ] PBT-07 verde: snapshot imutável sob qualquer sequência de operações.
- [ ] PBT-11 verde: snapshot preservado após alterar percentuais do parceiro via `SetPartnerCommission`.
- [ ] `Win` emite exatamente `OpportunityWon` + `CommissionSnapshotCreated` e registra `OpportunityStageTransition`.
- [ ] `Lose` sem `loss_reason_id` lança `LossReasonRequiredException` (INV-7).
- [ ] `Reopen` preserva snapshot com `is_snapshot = true`; `stage_category` volta para `open`.
- [ ] INV-11: `PromotePrimary` garante exatamente 1 principal; `UnlinkContact` do principal exige promoção de outro.
- [ ] `MarkStale` idempotente: segunda chamada não emite segundo `OpportunityStale`.

---

### TASK-08 — Pipeline behaviors — Logging, Tenant, Rbac, Idempotency, Validation, Transaction

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/opportunity-pipeline/08-application-pipeline-behaviors` |
| **Worktree** | `git worktree add ../worktrees/opportunity-pipeline/08-application-pipeline-behaviors -b feat/opportunity-pipeline/08-application-pipeline-behaviors` |
| **Status** | [ ] |
| **Depende de** | TASK-07 |
| **Entregável** | 6 pipeline behaviors implementados e testados: `LoggingBehavior`, `TenantBehavior`, `RbacBehavior`, `IdempotencyBehavior`, `ValidationBehavior`, `TransactionBehavior`. Ordem de execução correta verificada por teste. |
| **Mapeia** | RNF 3 (tenant isolation), RNF 4 (RBAC), RNF 10 (logging sem PII), Req 20 (eventos consistentes via transação), NFR-RES-02 (idempotência Idempotency-Key), design §5.4 |
| **Camada principal** | Application |

#### Objetivo

Implementar o pipeline de behaviors MediatR (ou equivalente) na ordem: `Logging → Tenant → Rbac → Idempotency → Validation → Transaction`. `LoggingBehavior`: loga `correlation_id`, `tenant_id`, `bu_id`, ação, duração — sem PII. `TenantBehavior`: resolve `TenantContext` do JWT. `RbacBehavior`: valida política RBAC da operação; retorna 403 antes de tocar o domínio. `IdempotencyBehavior`: deduplica por `Idempotency-Key` (chave `tenant_id + key + rota`) para escritas. `ValidationBehavior`: executa FluentValidation. `TransactionBehavior`: abre transação, executa handler, persiste Outbox + auditoria, faz commit/rollback.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de `RbacBehavior`: Vendedor em operação de reabertura deve receber `ForbiddenException`; TenantAdmin deve passar; Viewer em escrita deve receber `ForbiddenException`.
- [ ] **ST-02 — Red:** escrever testes de `IdempotencyBehavior`: mesma `Idempotency-Key` no mesmo tenant retorna resposta cacheada sem invocar o handler uma segunda vez.
- [ ] **ST-03 — Green:** implementar `LoggingBehavior` (sem PII; campos: `correlation_id`, `tenant_id`, `command_type`, `elapsed_ms`).
- [ ] **ST-04 — Green:** implementar `TenantBehavior` (extrai `tenant_id` do JWT e seta `TenantContext`); `RbacBehavior` (consulta política por atributo do command).
- [ ] **ST-05 — Green:** implementar `IdempotencyBehavior` (store de chaves por `tenant_id + idempotency_key + route`); `ValidationBehavior` (FluentValidation, retorna `ValidationException` mapeável a 422).
- [ ] **ST-06 — Green:** implementar `TransactionBehavior` (abre `IDbContextTransaction`, executa handler, ao final chama `IAuditPublisher` e `IDomainEventDispatcher`, faz commit; em exceção faz rollback).
- [ ] **ST-07 — Encerramento:** testes de RBAC e idempotência verdes; sem PII em logs; commit `feat(opportunity-pipeline): application pipeline behaviors` + push.

#### Critérios de Aceite

- [ ] Ordem de behaviors: Logging → Tenant → Rbac → Idempotency → Validation → Transaction (verificada por teste de integração de behaviors).
- [ ] `RbacBehavior` retorna 403 antes de qualquer acesso a domínio ou banco.
- [ ] `IdempotencyBehavior` não chama o handler na segunda requisição com mesma chave.
- [ ] `TransactionBehavior` garante rollback total em falha (sem registro parcial no banco).
- [ ] `LoggingBehavior` não loga nenhum campo PII (verificado por inspeção de output de teste).

---

### TASK-09 — Commands de criação/edição/movimentação — CreateOpportunity, UpdateOpportunity, MoveStage

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/opportunity-pipeline/09-application-create-update-move` |
| **Worktree** | `git worktree add ../worktrees/opportunity-pipeline/09-application-create-update-move -b feat/opportunity-pipeline/09-application-create-update-move` |
| **Status** | [ ] |
| **Depende de** | TASK-08 |
| **Entregável** | `CreateOpportunityCommand`/Handler, `UpdateOpportunityCommand`/Handler, `MoveStageCommand`/Handler, validators FluentValidation, portas de leitura mockadas em testes. |
| **Mapeia** | Req 1 (criar), Req 2 (owner, OP-ERR-002), Req 3 (número gerado), Req 4 (canal, OP-ERR-003/004), Req 5 (transição), Req 7 (probabilidade), Req 8 (TCV), RNF 2 (latência ≤ 500 ms), design §5.1, §5.3 |
| **Camada principal** | Application |

#### Objetivo

Handlers orquestram: (1) resolver tenant/RBAC (via behaviors); (2) carregar agregado (para update/move); (3) validar referências externas pelas portas de leitura; (4) invocar método de domínio; (5) persistir via `IOpportunityRepository`; (6) retornar DTO. `CreateOpportunityHandler` chama `IOpportunityNumberGenerator.NextAsync` antes de `Opportunity.Create`. Validators FluentValidation cobrem validação sintática (campos obrigatórios, intervalos). Testes usam portas mockadas.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de `CreateOpportunityHandler`: campos obrigatórios ausentes → `ValidationException` com `OP-ERR-001`; owner inválido → `OP-ERR-002`; canal Parceiro sem parceiro → `OP-ERR-004`; criação bem-sucedida → `OpportunityCreated` emitido + number formatado `AZ-NNNN`.
- [ ] **ST-02 — Red:** escrever testes de `MoveStageHandler`: transição inválida → `InvalidStageTransitionException` mapeada a 422; estágio ≥ Proposta Enviada sem data → `OP-ERR-005`; movimentação válida → `OpportunityStageChanged` emitido.
- [ ] **ST-03 — Green:** implementar `CreateOpportunityCommand` + validator + handler (chama `IOpportunityNumberGenerator.NextAsync`, valida referências externas, chama `Opportunity.Create`, persiste, retorna `OpportunityResponse`).
- [ ] **ST-04 — Green:** implementar `UpdateOpportunityCommand` + validator + handler (carrega agregado, valida owner, chama `UpdateContractValue`/`OverrideProbability`, persiste).
- [ ] **ST-05 — Green:** implementar `MoveStageCommand` + validator + handler (carrega agregado, valida estágio via `IOrganizationReadPort`, chama `MoveStage`, persiste).
- [ ] **ST-06 — Encerramento:** testes verdes com portas mockadas; commit `feat(opportunity-pipeline): application create update move-stage commands` + push.

#### Critérios de Aceite

- [ ] `CreateOpportunityHandler`: `opportunity_number` gerado no formato `AZ-NNNN` pelo gerador injetado.
- [ ] `CreateOpportunityHandler`: ausência de `owner_id` retorna erro `OPPORTUNITY_OWNER_REQUIRED` (OP-ERR-002).
- [ ] `MoveStageHandler`: transição inválida não registra `OpportunityStageTransition` nem emite evento (verificado em mock do repositório).
- [ ] `UpdateOpportunityHandler`: alterar `valor_mensal > 0` sem `duracao_meses` retorna `OP-ERR-012`.
- [ ] Validators FluentValidation cobrem todos os campos obrigatórios com mensagem mapeada a `OP-ERR-*`.

---

### TASK-10 — Commands de fechamento — WinOpportunity, LoseOpportunity, ReopenOpportunity

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/opportunity-pipeline/10-application-win-lose-reopen` |
| **Worktree** | `git worktree add ../worktrees/opportunity-pipeline/10-application-win-lose-reopen -b feat/opportunity-pipeline/10-application-win-lose-reopen` |
| **Status** | [ ] |
| **Depende de** | TASK-09 |
| **Entregável** | `WinOpportunityCommand`/Handler transacional, `LoseOpportunityCommand`/Handler, `ReopenOpportunityCommand`/Handler. Atomicidade do ganho verificada (snapshot + transição + closed_at na mesma transação). |
| **Mapeia** | Req 10 (lose, motivo obrigatório, OP-ERR-006), Req 14 (win, snapshot, atomicidade, DD-007/VAL-07), Req 15 (reopen, RBAC, OP-ERR-008), RNF 4 (RBAC), RNF 5.4 (atomicidade), PBT-07 (imutabilidade snapshot), design §5.1 (`WinOpportunityCommand`) |
| **Camada principal** | Application |

#### Objetivo

`WinOpportunityHandler` é o handler mais sensível: abre transação única, carrega agregado, invoca `Opportunity.Win(actor)` (que congela snapshot, registra transição `won`, define `closed_at`), persiste agregado + snapshot + transição + outbox (`OpportunityWon` + `CommissionSnapshotCreated`) + auditoria em commit único. Falha em qualquer passo → rollback total. DD-007 (VAL-07): implementar ponto de extensão via `commission_required_on_win` flag; default é alerta (snapshot zero permitido). `ReopenOpportunityHandler`: valida RBAC (TenantAdmin/GestorBU); verifica que snapshot é preservado após reabertura.

#### Subtasks

- [ ] **ST-01 — Red:** escrever teste de atomicidade do ganho: se a persistência do snapshot falhar (mock de repositório lança exceção após `Win`), nenhum dado deve ser persistido (nem transição, nem evento no outbox).
- [ ] **ST-02 — Red:** escrever teste de RBAC do reopen: Vendedor → `ForbiddenException` (HTTP 403/OP-ERR-008); GestorBU → sucesso; Viewer → 403.
- [ ] **ST-03 — Red:** escrever teste: ganho de oportunidade `lost` ou `won` → `InvalidStageTransitionException` mapeada a OP-ERR-013.
- [ ] **ST-04 — Green:** implementar `LoseOpportunityCommand` + validator (loss_reason_id obrigatório) + handler (carrega agregado, chama `Lose`, persiste, emite `OpportunityLost`).
- [ ] **ST-05 — Green:** implementar `WinOpportunityCommand` + validator + handler transacional (sequência: BEGIN tx → load aggregate → `Win` → persist snapshot+transition+opportunity → enqueue outbox → audit → COMMIT; rollback em falha).
- [ ] **ST-06 — Green:** implementar `ReopenOpportunityCommand` + validator + handler (RBAC via `RbacBehavior`; verifica `stage_category ∈ {won, lost}`; chama `Reopen`; persiste; emite `OpportunityReopened`).
- [ ] **ST-07 — Refactor:** extrair verificação de `commission_required_on_win` como ponto de extensão isolado (DD-007/VAL-07) sem acoplar a lógica ao handler principal.
- [ ] **ST-08 — Encerramento:** testes de atomicidade, RBAC e transição inválida verdes; commit `feat(opportunity-pipeline): application win-lose-reopen commands` + push.

#### Critérios de Aceite

- [ ] `WinOpportunityHandler` é atômico: rollback total em falha de qualquer passo (verificado por mock de falha na persistência).
- [ ] `WinOpportunityHandler` emite `OpportunityWon` + `CommissionSnapshotCreated` via outbox na mesma transação.
- [ ] `ReopenOpportunityHandler` retorna 403 para Vendedor/Viewer (via `RbacBehavior`).
- [ ] `LoseOpportunityHandler` sem `loss_reason_id` retorna OP-ERR-006.
- [ ] Ponto de extensão VAL-07 isolado e configurável sem alterar a lógica principal do handler.

---

### TASK-11 — Commands de comissão e contatos — SetPartnerCommission, LinkContact, UnlinkContact, SaveFilter

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/opportunity-pipeline/11-application-commission-contact-filter` |
| **Worktree** | `git worktree add ../worktrees/opportunity-pipeline/11-application-commission-contact-filter -b feat/opportunity-pipeline/11-application-commission-contact-filter` |
| **Status** | [ ] |
| **Depende de** | TASK-10 |
| **Entregável** | `SetPartnerCommissionCommand`/Handler, `LinkContactCommand`/Handler, `UnlinkContactCommand`/Handler, `SaveFilterCommand`/Handler com testes e validators. |
| **Mapeia** | Req 11 (comissão, OP-ERR-014/015), Req 12 (cálculo automático), Req 16 (contacts, INV-11, OP-ERR-009/016), Req 19.2 (filtros salvos), RNF 6 (auditoria), design §5.1 |
| **Camada principal** | Application |

#### Objetivo

`SetPartnerCommissionHandler`: carrega agregado, consulta `IPartnerReadPort` para defaults, chama `Opportunity.SetPartnerCommission(terms)`. Rejeita se já existe snapshot (`is_snapshot = true`). Rejeita mutual exclusão `valor_fixo` × percentuais (OP-ERR-014). `LinkContactHandler`/`UnlinkContactHandler`: valida contato via `IAccountReadPort`, chama `LinkContact`/`UnlinkContact`; garante INV-11. `SaveFilterHandler`: persiste filtro nomeado em `saved_filters`; sem lógica de domínio.

#### Subtasks

- [ ] **ST-01 — Red:** escrever teste: tentar `SetPartnerCommission` em oportunidade com snapshot existente → retorna HTTP 409 com mensagem de conflito.
- [ ] **ST-02 — Red:** escrever teste: `LinkContact` com `contact_id` de outra conta → OP-ERR-016; `UnlinkContact` do último contato quando é o principal → `PrimaryContactRequiredException`.
- [ ] **ST-03 — Green:** implementar `SetPartnerCommissionCommand` + validator (mutual exclusão `valor_fixo` × pcts, OP-ERR-014; parceiro existente via `IPartnerReadPort`, OP-ERR-015) + handler.
- [ ] **ST-04 — Green:** implementar `LinkContactCommand` + validator + handler (valida via `IAccountReadPort`); `UnlinkContactCommand` + handler (chama `UnlinkContact` e `PromotePrimary` quando necessário).
- [ ] **ST-05 — Green:** implementar `SaveFilterCommand` + handler (persiste em `saved_filters`; validator: nome único por usuário/tenant, OP-ERR padrão).
- [ ] **ST-06 — Encerramento:** testes verdes; commit `feat(opportunity-pipeline): application commission contact filter commands` + push.

#### Critérios de Aceite

- [ ] `SetPartnerCommissionHandler`: retorna HTTP 409 se snapshot existir.
- [ ] `SetPartnerCommissionHandler`: pré-preenche `pct_setup`/`pct_recorrente` com defaults do parceiro via `IPartnerReadPort`.
- [ ] `LinkContactHandler`: rejeita contato que não pertence à conta da oportunidade (OP-ERR-016).
- [ ] `UnlinkContactHandler`: rejeita desvinculação do único contato principal sem promoção alternativa.
- [ ] `SaveFilterHandler`: persiste critério como JSONB; nome duplicado por usuário retorna 422.

---

### TASK-12 — Queries (8 queries) + StagnationDetectionService (PBT-09)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/opportunity-pipeline/12-application-queries-stagnation` |
| **Worktree** | `git worktree add ../worktrees/opportunity-pipeline/12-application-queries-stagnation -b feat/opportunity-pipeline/12-application-queries-stagnation` |
| **Status** | [ ] |
| **Depende de** | TASK-11 |
| **Entregável** | 8 query handlers implementados e testados + `StagnationDetectionService` com idempotência verificada (PBT-09). |
| **Mapeia** | Req 13 (forecast líquido), Req 17 (stale, idempotência), Req 18 (kanban, somas, RNF 1.3), Req 19 (lista, filtros salvos, timeline), RNF 9 (idempotência), PBT-09 (idempotência estagnação), design §5.2, §5.3 |
| **Camada principal** | Application |

#### Objetivo

Implementar os 8 query handlers: `GetOpportunityQuery` (detalhe + flags `vencida`/`stale` derivadas), `ListOpportunitiesQuery` (filtros + paginação), `GetKanbanQuery` (somas SQL por estágio, paginação por coluna), `GetTimelineQuery` (transições append-only), `GetCommissionQuery` (projetada/snapshot + forecast líquido via `NetForecastCalculator`), `GetForecastQuery` (interno), `GetStaleQuery` (interno), `ListSavedFiltersQuery`. `StagnationDetectionService` (Application Service): para cada tenant/BU, consulta `IActivityReadPort`, aplica `StagnationSpecification`, chama `MarkStale`, registra em `stale_detection_runs` (idempotência).

#### Subtasks

- [ ] **ST-01 — Red:** escrever PBT-09: para qualquer conjunto de oportunidades e qualquer número N de reexecuções do `StagnationDetectionService` no mesmo `detection_period`, cada oportunidade estagnada gera no máximo 1 `OpportunityStale` por período.
- [ ] **ST-02 — Red:** escrever testes de `GetKanbanQuery`: resultado inclui `SUM(valor_total)` e `SUM(forecast_ponderado)` por estágio; paginação por coluna limita cards retornados; somente oportunidades do tenant/BU do usuário.
- [ ] **ST-03 — Green:** implementar `GetOpportunityQuery`/Handler (inclui `OverdueSpecification` e `StagnationSpecification` derivadas para flags no DTO).
- [ ] **ST-04 — Green:** implementar `ListOpportunitiesQuery`/Handler (filtros: owner, canal, parceiro, estágio, data, stale; paginação `page`/`page_size`); `ListSavedFiltersQuery`/Handler.
- [ ] **ST-05 — Green:** implementar `GetKanbanQuery`/Handler (SQL agregado via repositório com `SUM(valor_total)`, `SUM(forecast_ponderado)` por `(tenant_id, bu_id, stage_id)`; paginação por coluna com `stage_page_size`).
- [ ] **ST-06 — Green:** implementar `GetTimelineQuery`, `GetCommissionQuery` (inclui `forecast_liquido` via `NetForecastCalculator`), `GetForecastQuery`, `GetStaleQuery`.
- [ ] **ST-07 — Green:** implementar `StagnationDetectionService` (itera oportunidades abertas via repositório, aplica `StagnationSpecification`, chama `MarkStale`, persiste `stale_detection_runs` com PK `(tenant_id, opportunity_id, detection_period)` para idempotência).
- [ ] **ST-08 — Encerramento:** PBT-09 verde; testes de kanban e queries verdes; commit `feat(opportunity-pipeline): application queries stagnation-service PBT-09` + push.

#### Critérios de Aceite

- [ ] PBT-09 verde: `StagnationDetectionService` idempotente por `(opportunity_id, detection_period)`.
- [ ] `GetKanbanQuery` retorna `SUM(valor_total)` e `SUM(forecast_ponderado)` corretos por estágio.
- [ ] `GetKanbanQuery` não retorna oportunidades de outro tenant ou BU sem membership (verificado em teste com mock de tenant context diferente).
- [ ] `GetCommissionQuery` inclui `forecast_liquido` calculado via `NetForecastCalculator`.
- [ ] `GetOpportunityQuery` inclui flags `is_overdue` e `is_stale` derivadas (não persistidas).

---

### TASK-13 — DbContext + EF mapeamentos + Global Query Filter + migrations (7 tabelas) + RLS + REVOKE

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/opportunity-pipeline/13-infra-dbcontext-migrations-rls` |
| **Worktree** | `git worktree add ../worktrees/opportunity-pipeline/13-infra-dbcontext-migrations-rls -b feat/opportunity-pipeline/13-infra-dbcontext-migrations-rls` |
| **Status** | [ ] |
| **Depende de** | TASK-12 |
| **Entregável** | `OpportunityDbContext` com mapeamentos EF Core `snake_case`, Global Query Filter por `tenant_id`, migrations EF para todas as 7 tabelas (+ `outbox_events`, `audit_logs` se não existirem), RLS habilitada e REVOKE UPDATE/DELETE em `opportunity_stage_transitions`. |
| **Mapeia** | RNF 3 (isolamento tenant), RNF 7 (transitions append-only), RNF 8 (retenção), design §6.1, §7.1..7.5, ADR-0001 (RLS), DD-006 (RLS interceptor) |
| **Camada principal** | Infrastructure |

#### Objetivo

Criar `OpportunityDbContext` com mapeamentos das 7 tabelas próprias do módulo (`opportunities`, `opportunity_stage_transitions`, `opportunity_partner_commissions`, `opportunity_contacts`, `opportunity_number_sequences`, `stale_detection_runs`, `saved_filters`) usando nomes físicos `snake_case` conforme data-model §3. Configurar `Global Query Filter` por `tenant_id` em todas as entidades (ADR-0001, camada 2). Criar migrations EF com SQL manual para: RLS em todas as tabelas, REVOKE UPDATE/DELETE/TRUNCATE em `opportunity_stage_transitions` para role `app`, constraint `chk_*` e índices críticos (`idx_opportunities_tenant_bu_stage`, etc.). Colunas geradas `valor_total` e `forecast_ponderado` como `GENERATED ALWAYS AS ... STORED`.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de integração (TestContainers ou banco em memória): inserir `Opportunity` sem `tenant_id` → exceção; `Global Query Filter` filtra por tenant corrente; `opportunity_stage_transitions` rejeita UPDATE/DELETE.
- [ ] **ST-02 — Green:** implementar `OpportunityDbContext` com `ModelBuilder` configurando mapeamentos `snake_case` (ToTable, HasColumnName), `Global Query Filter` (`HasQueryFilter(e => e.TenantId == _tenantContext.TenantId)`), chaves primárias UUID, constraints `UNIQUE`, `CHECK` e índices.
- [ ] **ST-03 — Green:** criar migration `Initial_Opportunities_Schema` com DDL das 7 tabelas, colunas geradas `STORED`, todos os índices listados em §7.1..7.4 do design.
- [ ] **ST-04 — Green:** criar migration `RLS_And_Permissions` com SQL manual: `ENABLE ROW LEVEL SECURITY`, `CREATE POLICY tenant_isolation_policy ON <table> USING (tenant_id = current_setting('app.current_tenant')::uuid)` (falha-fechada: `FORCE ROW LEVEL SECURITY`); `REVOKE UPDATE, DELETE ON opportunity_stage_transitions FROM app`; `REVOKE DELETE ON opportunity_partner_commissions FROM app`.
- [ ] **ST-05 — Refactor:** garantir que as migrations são idempotentes (uso de `IF NOT EXISTS` em índices); verificar que `dotnet ef database update` aplica sem erros em banco limpo.
- [ ] **ST-06 — Encerramento:** testes de integração com banco real (TestContainers) verdes; RLS habilitada e verificada; commit `feat(opportunity-pipeline): infra dbcontext migrations rls` + push.

#### Critérios de Aceite

- [ ] `opportunity_stage_transitions` rejeita UPDATE e DELETE para role `app` (verificado com query direta no banco de testes).
- [ ] `Global Query Filter` filtra corretamente por `tenant_id`; consulta sem contexto de tenant retorna zero resultados (ou lança dependendo da implementação de RLS).
- [ ] Todas as 7 tabelas com RLS habilitada e política falha-fechada.
- [ ] Colunas geradas `valor_total` e `forecast_ponderado` calculadas corretamente pelo banco (teste de inserção e verificação).
- [ ] `idx_opportunities_tenant_bu_stage` existente e utilizado por EXPLAIN ANALYZE no Kanban.

---

### TASK-14 — Trigger trg_block_snapshot_mutation + constraint uq_active_snapshot + imutabilidade no banco (PBT-07 infra)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/opportunity-pipeline/14-infra-snapshot-trigger` |
| **Worktree** | `git worktree add ../worktrees/opportunity-pipeline/14-infra-snapshot-trigger -b feat/opportunity-pipeline/14-infra-snapshot-trigger` |
| **Status** | [ ] |
| **Depende de** | TASK-13 |
| **Entregável** | Trigger `trg_block_snapshot_mutation` implementado em migration EF manual; constraint `uq_partner_commission_active_snapshot`; testes de integração com banco real verificando imutabilidade (PBT-07 infra). |
| **Mapeia** | RNF 5 (imutabilidade snapshot no banco), Req 14.2 (sem UPDATE/DELETE após snapshot), Req 14.6 (no máximo 1 snapshot por oportunidade), PBT-07, PBT-11, DD-002, ADR-0002 (a formalizar) |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar a garantia de imutabilidade do snapshot **no banco** (não apenas na aplicação). A função `block_snapshot_mutation()` é criada em migration EF com SQL raw e o trigger `trg_block_snapshot_mutation` é disparado BEFORE UPDATE OR DELETE em `opportunity_partner_commissions`; quando `OLD.is_snapshot = TRUE`, lança `RAISE EXCEPTION 'commission snapshot is immutable (RN-007/RN-022)'`. A constraint `UNIQUE(opportunity_id) WHERE is_snapshot` (índice parcial único) garante no máximo 1 snapshot por oportunidade. Testes de integração (TestContainers) executam UPDATE e DELETE contra snapshot e verificam a exceção Postgres.

#### Subtasks

- [ ] **ST-01 — Red (integração):** escrever testes de integração com banco real: (a) tentar UPDATE em `opportunity_partner_commissions` com `is_snapshot = TRUE` → deve lançar exceção Postgres; (b) tentar DELETE → mesma exceção; (c) tentar inserir segundo snapshot para mesma `opportunity_id` → viola constraint unique parcial.
- [ ] **ST-02 — Green:** criar migration `Commission_Snapshot_Immutability` com SQL: `CREATE FUNCTION block_snapshot_mutation() RETURNS trigger ... RAISE EXCEPTION ...`; `CREATE TRIGGER trg_block_snapshot_mutation BEFORE UPDATE OR DELETE ON opportunity_partner_commissions ...`; `CREATE UNIQUE INDEX uq_partner_commission_active_snapshot ON opportunity_partner_commissions (opportunity_id) WHERE is_snapshot`.
- [ ] **ST-03 — Green:** escrever teste de regressão PBT-11 infra: criar oportunidade com parceiro, ganhar (snapshot criado), alterar percentuais do parceiro no `partner-management` (mock), verificar que os valores do snapshot no banco permanecem inalterados.
- [ ] **ST-04 — Refactor:** verificar que a função Postgres é idempotente (CREATE OR REPLACE); garantir que migrations são reaplicáveis em ambiente de CI.
- [ ] **ST-05 — Encerramento:** testes de integração com banco real verdes; commit `feat(opportunity-pipeline): infra snapshot trigger immutability PBT-07` + push.

#### Critérios de Aceite

- [ ] UPDATE em registro `is_snapshot = TRUE` lança exceção Postgres (testado com `Npgsql` direto no banco de testes).
- [ ] DELETE em registro `is_snapshot = TRUE` lança exceção Postgres.
- [ ] Tentativa de inserir 2 snapshots para mesma `opportunity_id` viola `uq_partner_commission_active_snapshot`.
- [ ] Snapshot existente não é alterado após alterar percentuais do parceiro (PBT-11 infra).
- [ ] Trigger não interfere com UPDATE/DELETE em registros `is_snapshot = FALSE`.

---

### TASK-15 — OpportunityNumberGenerator + lock atômico por tenant + teste de concorrência (PBT-01)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `test/opportunity-pipeline/15-infra-opportunity-number-generator` |
| **Worktree** | `git worktree add ../worktrees/opportunity-pipeline/15-infra-opportunity-number-generator -b test/opportunity-pipeline/15-infra-opportunity-number-generator` |
| **Status** | [ ] |
| **Depende de** | TASK-13 |
| **Entregável** | `OpportunityNumberGenerator` implementado com `INSERT ... ON CONFLICT DO UPDATE ... RETURNING`; teste de concorrência PBT-01 com N threads simultâneas verifica unicidade. |
| **Mapeia** | Req 3 (numeração AZ-NNNN atômica, imutável, única por tenant), RNF (unicidade sob concorrência), PBT-01 (atomicidade + anti-colisão), DD-001, ADR-0003 (a formalizar) |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar `OpportunityNumberGenerator` que executa dentro da transação de criação o comando atômico em `opportunity_number_sequences`: `INSERT INTO opportunity_number_sequences (tenant_id, next_value) VALUES (@t, 2) ON CONFLICT (tenant_id) DO UPDATE SET next_value = next_value + 1 RETURNING next_value - 1`. O número retornado é formatado como `AZ-{n:0000}`. PBT-01: teste de concorrência com 20 tasks simultâneas criando oportunidades no mesmo tenant — os 20 números gerados devem ser distintos dois a dois e a constraint `uq_opportunities_tenant_number` nunca deve ser violada.

#### Subtasks

- [ ] **ST-01 — Red (PBT-01):** escrever teste de concorrência: instanciar N tasks simultâneas (N = 20) chamando `OpportunityNumberGenerator.NextAsync` no mesmo `tenant_id`; verificar que os N números retornados são todos distintos (`Distinct().Count() == N`); verificar que nenhuma exceção de violação de constraint é lançada.
- [ ] **ST-02 — Red:** escrever testes unitários: formato `AZ-0001` para sequência 1, `AZ-0010` para 10, `AZ-9999` para 9999, `AZ-10000` para 10000 (número com mais de 4 dígitos é válido, regex aceita `\d{4,}`).
- [ ] **ST-03 — Green:** implementar `OpportunityNumberGenerator` (executa SQL com `ExecuteSqlRawAsync` + `RETURNING`; formata como `AZ-{n:D4}`; precisa de `IDbContextTransaction` já aberta pelo `TransactionBehavior`).
- [ ] **ST-04 — Refactor:** garantir que o gerador é compatível com `using` em testes de integração sem transação explícita; verificar que rollback não reutiliza o número (counter só avança).
- [ ] **ST-05 — Encerramento:** PBT-01 verde com N = 20 tarefas simultâneas; commit `test(opportunity-pipeline): infra opportunity number generator PBT-01` + push.

#### Critérios de Aceite

- [ ] PBT-01 verde: 20 tarefas concorrentes no mesmo tenant produzem 20 números distintos.
- [ ] Números de tenants diferentes não colidem (isolamento por `tenant_id` PK na tabela `opportunity_number_sequences`).
- [ ] Rollback de transação não reutiliza número (contador já avançou; o próximo número gerado será o subsequente).
- [ ] Formato `AZ-NNNN`: mínimo 4 dígitos, aceita mais para sequências > 9999.
- [ ] Constraint `uq_opportunities_tenant_number` nunca violada em teste de concorrência.

---

### TASK-16 — RlsConnectionInterceptor + OutboxPublisher + AuditPublisher + PiiMasker

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/opportunity-pipeline/16-infra-rls-outbox-audit` |
| **Worktree** | `git worktree add ../worktrees/opportunity-pipeline/16-infra-rls-outbox-audit -b feat/opportunity-pipeline/16-infra-rls-outbox-audit` |
| **Status** | [ ] |
| **Depende de** | TASK-13 |
| **Entregável** | `RlsConnectionInterceptor`, `OutboxPublisher` (background service), `AuditPublisher`, `PiiMasker` implementados e testados. |
| **Mapeia** | RNF 3 (RLS falha-fechada), RNF 6 (AuditLog imutável), Req 20.4 (eventos consistentes com transação), RNF 10.4 (sem PII em logs), DD-006 (RLS interceptor), ADR-0001 (camada 3), ADR-0004 (Outbox) |
| **Camada principal** | Infrastructure |

#### Objetivo

`RlsConnectionInterceptor`: implementa `IDbConnectionInterceptor` do EF Core; executa `SET app.current_tenant = @tenant_id` ao alugar a conexão do pool, garantindo que a RLS Postgres filtre corretamente. Sem `TenantContext` → lança exceção antes de qualquer comando. `OutboxPublisher`: `BackgroundService` que lê `outbox_events` com `status = 'pending'`, publica no Cloud Pub/Sub (ou mock em testes) em lote, marca `published`; alerta se `pending > 100` por > 5 min. `AuditPublisher`: implementa `IAuditPublisher`; grava em `audit_logs` (append-only) na mesma transação, com delta JSON mascarado por `PiiMasker`. `PiiMasker`: substitui campos PII (emails, CPF, telefone) no delta JSON antes da gravação.

#### Subtasks

- [ ] **ST-01 — Red:** escrever teste de integração: sem `TenantContext` configurado, qualquer query ao banco lança exceção (RLS falha-fechada).
- [ ] **ST-02 — Red:** escrever teste: `PiiMasker.Mask(deltaJson)` remove/mascara campos `email`, `phone`, `cpf`, `name` do contato no JSON; outros campos permanecem.
- [ ] **ST-03 — Green:** implementar `RlsConnectionInterceptor` (sobrescreve `ConnectionOpenedAsync` para executar `SET app.current_tenant`).
- [ ] **ST-04 — Green:** implementar `AuditPublisher` + `PiiMasker` (insere em `audit_logs` na transação corrente; sem IO externo no caminho síncrono).
- [ ] **ST-05 — Green:** implementar `OutboxPublisher` (BackgroundService com loop periódico: SELECT pending → publish Pub/Sub → UPDATE published; lote de até 100 eventos por iteração).
- [ ] **ST-06 — Encerramento:** testes de RLS falha-fechada e PiiMasker verdes; commit `feat(opportunity-pipeline): infra rls-interceptor outbox audit pii-masker` + push.

#### Critérios de Aceite

- [ ] Sem `TenantContext` → qualquer query ao banco lança exceção antes de retornar dados.
- [ ] `PiiMasker` mascara campos PII no delta JSON sem alterar campos não-PII.
- [ ] `AuditPublisher` grava exatamente 1 registro em `audit_logs` por operação de negócio.
- [ ] `OutboxPublisher` publica eventos pendentes e marca como `published`; falha no Pub/Sub não perde o evento (retry).
- [ ] `audit_logs` não pode ser apagado pelo role `app` (verificado por REVOKE já na migration).

---

### TASK-17 — Adaptadores de portas (Organization, Account, Partner, Activity) + Polly + cache de configuração

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/opportunity-pipeline/17-infra-read-port-adapters` |
| **Worktree** | `git worktree add ../worktrees/opportunity-pipeline/17-infra-read-port-adapters -b feat/opportunity-pipeline/17-infra-read-port-adapters` |
| **Status** | [ ] |
| **Depende de** | TASK-16 |
| **Entregável** | 4 adaptadores de portas de leitura (`OrganizationReadAdapter`, `AccountReadAdapter`, `PartnerReadAdapter`, `ActivityReadAdapter`) com Polly (timeout 2 s, 3 retries backoff, circuit breaker) e cache de configuração por tenant/BU (TTL 60 s). |
| **Mapeia** | Req 1 (validar account), Req 2 (owner membership), Req 4 (canal, parceiro), Req 17 (last_activity_at), RNF (resiliência), design §6.4, P9 (Conformist) |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar os 4 adaptadores das portas de leitura via gRPC/HTTP interno com mTLS. Cada adaptador tem: timeout 2 s, 3 retries com backoff exponencial, circuit breaker (Polly). `OrganizationReadAdapter`: valida `bu_id`, `stage_id`, `owner_id`, `origin_channel_id`, `loss_reason_id`; fornece `StageRef` (com `default_probability` e `order`). Cache de `StageRef`/`OriginChannelRef`/`LossReasonRef` por tenant/BU com TTL 60 s (Redis ou in-memory). `ActivityReadAdapter`: fornece `last_activity_at`; degradação graciosa se indisponível (adia o scan). `AccountReadAdapter`: sem cache de PII. `PartnerReadAdapter`: fornece `CommissionDefaults`.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes com mock HTTP: adaptador retorna `StageRef` corretamente; timeout → lança `UpstreamUnavailableException`; 3 falhas consecutivas → circuit breaker aberto → falha rápida.
- [ ] **ST-02 — Red:** escrever teste de cache: segunda chamada para mesma `(tenant_id, bu_id)` dentro do TTL não faz requisição HTTP (verificado por contagem de chamadas ao mock).
- [ ] **ST-03 — Green:** implementar `OrganizationReadAdapter` (com `IHttpClientFactory`, Polly policies, cache `IMemoryCache` por `(tenant_id, stage_id)`).
- [ ] **ST-04 — Green:** implementar `AccountReadAdapter` (sem cache; sem PII retornado, apenas `contact_id` list); `PartnerReadAdapter` (fornece `CommissionDefaults`); `ActivityReadAdapter` (retorna `last_activity_at`; degrada graciosamente).
- [ ] **ST-05 — Encerramento:** testes de resiliência e cache verdes; commit `feat(opportunity-pipeline): infra read-port adapters polly cache` + push.

#### Critérios de Aceite

- [ ] Timeout de 2 s por chamada a upstream; 3 retries com backoff exponencial.
- [ ] Circuit breaker abre após N falhas consecutivas; falha rápida enquanto aberto.
- [ ] Cache de `StageRef`/`OriginChannelRef` por tenant/BU com TTL 60 s.
- [ ] `AccountReadAdapter` não armazena em cache dados PII.
- [ ] `ActivityReadAdapter` degrada graciosamente (retorna `null` para `last_activity_at`) se upstream indisponível.

---

### TASK-18 — StaleScanEndpointHandler + stale_detection_runs + idempotência (PBT-09 infra)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/opportunity-pipeline/18-infra-stale-scan-handler` |
| **Worktree** | `git worktree add ../worktrees/opportunity-pipeline/18-infra-stale-scan-handler -b feat/opportunity-pipeline/18-infra-stale-scan-handler` |
| **Status** | [ ] |
| **Depende de** | TASK-17 |
| **Entregável** | `StaleScanEndpointHandler` (endpoint interno `POST /internal/stale-scan` acionado por Cloud Scheduler) com idempotência por `stale_detection_runs` verificada com banco real. |
| **Mapeia** | Req 17 (detecção de estagnação, idempotência), RNF 9 (idempotência + alerta de não execução), PBT-09 (idempotência), DD-005 (scheduler), design §5.3 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar o handler do endpoint `POST /internal/stale-scan` que delega ao `StagnationDetectionService` (Application). O handler: (1) autentica via identidade de serviço/mTLS; (2) itera tenants; (3) para cada oportunidade `open` candidata, verifica `stale_detection_runs` pela chave `(tenant_id, opportunity_id, detection_period)` — se já presente, pula (idempotência); (4) chama `MarkStale` e persiste `stale_detection_runs`. Atualiza métrica `stale_scan_last_run_timestamp`. Alerta se não executar no intervalo esperado.

#### Subtasks

- [ ] **ST-01 — Red (integração):** escrever teste de integração com banco: executar `StaleScanEndpointHandler` duas vezes no mesmo `detection_period` para as mesmas oportunidades → `OpportunityStale` é emitido apenas na primeira execução; `stale_detection_runs` contém exatamente N registros (N = oportunidades estagnadas), sem duplicatas.
- [ ] **ST-02 — Green:** implementar `StaleScanEndpointHandler` com iteração por tenant, consulta de candidatos (`stage_category = 'open' AND last_activity_at < now() - interval '14 days'`), verificação de `stale_detection_runs`, chamada ao `StagnationDetectionService`, persistência.
- [ ] **ST-03 — Green:** implementar atualização da métrica `stale_scan_last_run_timestamp` após cada execução bem-sucedida.
- [ ] **ST-04 — Encerramento:** PBT-09 infra verde; commit `feat(opportunity-pipeline): infra stale-scan handler idempotency` + push.

#### Critérios de Aceite

- [ ] Segunda execução no mesmo `detection_period` não duplica `OpportunityStale` (verificado por contagem de eventos no outbox).
- [ ] `stale_detection_runs` tem chave PK `(tenant_id, opportunity_id, detection_period)` que impede duplicatas no banco.
- [ ] Métrica `stale_scan_last_run_timestamp` atualizada após cada execução.
- [ ] Oportunidades `won` e `lost` não são candidatas à detecção (filtro por `stage_category = 'open'`).

---

### TASK-19 — Contratos públicos — DTOs, requests/responses, envelopes .v1, catálogo OP-ERR-001..017

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `feat/opportunity-pipeline/19-contracts-dtos-events-errors` |
| **Worktree** | `git worktree add ../worktrees/opportunity-pipeline/19-contracts-dtos-events-errors -b feat/opportunity-pipeline/19-contracts-dtos-events-errors` |
| **Status** | [ ] |
| **Depende de** | TASK-18 |
| **Entregável** | Projeto `OpportunityPipeline.Contracts` com DTOs de request/response, 8 envelopes de evento `.v1`, catálogo `OP-ERR-001..017` como constantes tipadas. Money trafega em centavos inteiros. |
| **Mapeia** | Req 1..20 (contratos de toda operação), Req 20.2 (versionamento `.v1`), Req 20.3 (sem PII nos eventos), RNF 11.1 (money em centavos nos contratos), design §8 (tabela de endpoints), §9 (eventos) |
| **Camada principal** | Contracts |

#### Objetivo

Criar os contratos públicos no projeto `OpportunityPipeline.Contracts` (sem lógica de negócio): `CreateOpportunityRequest`, `UpdateOpportunityRequest`, `MoveStageRequest`, `WinOpportunityRequest`, `LoseOpportunityRequest`, `ReopenOpportunityRequest`, `SetPartnerCommissionRequest`, `LinkContactRequest`; responses: `OpportunityResponse`, `KanbanResponse`, `TimelineResponse`, `CommissionResponse`; 8 envelopes de evento `.v1` (sem PII); `ErrorCodes` como classe de constantes `OP-ERR-001..017`. Testes de contrato validam que campos Money são `long` (centavos), nunca `decimal` nem `string`.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de contrato: `CreateOpportunityRequest` sem `owner_id` → resposta com `error_code = "OP-ERR-002"`; `OpportunityResponse.valor_total` é `long` (centavos); envelopes de evento `.v1` não possuem campo `contact_name` nem `email`.
- [ ] **ST-02 — Green:** implementar DTOs de request (campos obrigatórios com anotações de validação e `[JsonPropertyName]` snake_case); DTOs de response (`OpportunityResponse` com `valor_total: long`, `forecast_ponderado: long`, `forecast_liquido: long?`, flags `is_stale`, `is_overdue`).
- [ ] **ST-03 — Green:** implementar envelopes de evento `.v1` como `record` imutável: `OpportunityCreatedV1`, `OpportunityStageChangedV1`, `OpportunityWonV1`, `OpportunityLostV1`, `OpportunityStaleV1`, `OpportunityReopenedV1`, `CommissionCalculatedV1`, `CommissionSnapshotCreatedV1` — todos sem PII.
- [ ] **ST-04 — Green:** implementar `ErrorCodes` com constantes `OP-ERR-001` a `OP-ERR-017` e classe `OpProblemDetails` (com `error_code`, `correlation_id`, `message`).
- [ ] **ST-05 — Encerramento:** testes de contrato verdes (sem PII nos eventos, money em centavos); commit `feat(opportunity-pipeline): contracts dtos events error-codes` + push.

#### Critérios de Aceite

- [ ] Campos monetários nos DTOs são `long` (centavos); nenhum campo `decimal` nos responses de money.
- [ ] Envelopes `.v1` não contêm campos PII de contato (verificado por inspeção de propriedades via reflection em teste).
- [ ] Catálogo `OP-ERR-001..017` disponível como constantes estáticas tipadas.
- [ ] `OpportunityResponse` inclui `forecast_liquido` quando comissão presente.
- [ ] `KanbanResponse` inclui `total_valor` e `total_forecast` por coluna (em centavos).

---

### TASK-20 — Controllers (OpportunitiesController, CommissionsController, KanbanController) + RBAC + ProblemDetails + OpenAPI

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `feat/opportunity-pipeline/20-api-controllers-rbac-openapi` |
| **Worktree** | `git worktree add ../worktrees/opportunity-pipeline/20-api-controllers-rbac-openapi -b feat/opportunity-pipeline/20-api-controllers-rbac-openapi` |
| **Status** | [ ] |
| **Depende de** | TASK-19 |
| **Entregável** | 3 controllers implementados com todos os endpoints de §8 do design, políticas RBAC, `ProblemDetails` com `OP-ERR-*`, OpenAPI spec gerada, testes de contrato HTTP com mapeamento de erros. |
| **Mapeia** | Req 1 (POST /opportunities), Req 5 (PATCH /stage), Req 10 (POST /lose), Req 14 (POST /win), Req 15 (POST /reopen), Req 18 (GET /kanban), Req 19 (GET /timeline), RNF 4 (RBAC em todo endpoint), design §8, §10 |
| **Camada principal** | Api |

#### Objetivo

Implementar `OpportunitiesController` (9 rotas: POST, GET list, GET detail, PATCH, PATCH /stage, PUT /partner-commission, POST /win, POST /lose, POST /reopen), `CommissionsController` (GET /commissions), `KanbanController` (GET /kanban com `bu_id`). Todas as rotas com: `[Authorize]` por política RBAC (usando `RbacBehavior` via MediatR); erros mapeados a `ProblemDetails` com `OP-ERR-*`; `Idempotency-Key` exigido nos endpoints de escrita; paginação (`page`, `page_size`). `GET /opportunities/{id}/timeline` e `GET /opportunities/{id}/commissions` no mesmo controller. OpenAPI gerado automaticamente com Swashbuckle.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de contrato HTTP (WebApplicationFactory): POST `/opportunities` sem `owner_id` → 422 com `error_code = OP-ERR-002`; POST `/opportunities/{id}/win` como Vendedor → 200; POST `/opportunities/{id}/reopen` como Vendedor → 403.
- [ ] **ST-02 — Red:** escrever testes: GET `/opportunities/kanban?bu_id=X` como Viewer → 200; sem `bu_id` → 422; com `bu_id` de outra BU sem membership → 403 ou resultado vazio.
- [ ] **ST-03 — Green:** implementar `OpportunitiesController` com todas as 9 rotas + mapeamento `OP-ERR-*` → `ProblemDetails` via `ExceptionHandlingMiddleware`.
- [ ] **ST-04 — Green:** implementar `CommissionsController` (GET + PUT /partner-commission + POST /contacts + DELETE /contacts/{id}) e `KanbanController` (GET /kanban com paginação por coluna).
- [ ] **ST-05 — Green:** configurar Swashbuckle para gerar OpenAPI `/api/v1`; adicionar `IOperationFilter` para `Idempotency-Key` header; registrar todos os `OP-ERR-*` como exemplos de response.
- [ ] **ST-06 — Encerramento:** testes de contrato HTTP verdes; OpenAPI spec gerada sem erros; commit `feat(opportunity-pipeline): api controllers rbac openapi` + push.

#### Critérios de Aceite

- [ ] Todas as 9 rotas de `/opportunities` implementadas e testadas (200/422/403/404 verificados).
- [ ] POST `/opportunities/{id}/reopen` como Vendedor → 403 (OP-ERR-008).
- [ ] Erros de domínio mapeados a `ProblemDetails` com `error_code` `OP-ERR-*` e `correlationId`.
- [ ] OpenAPI spec gerada no endpoint `/swagger` (desenvolvimento) e arquivo estático em CI.
- [ ] `Idempotency-Key` header documentado no OpenAPI e verificado pelo `IdempotencyBehavior`.

---

### TASK-21 — InternalPipelineController + mTLS + testes RBAC 200/403 por papel × capacidade

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `feat/opportunity-pipeline/21-api-internal-rbac-tests` |
| **Worktree** | `git worktree add ../worktrees/opportunity-pipeline/21-api-internal-rbac-tests -b feat/opportunity-pipeline/21-api-internal-rbac-tests` |
| **Status** | [ ] |
| **Depende de** | TASK-20 |
| **Entregável** | `InternalPipelineController` com 3 endpoints internos protegidos por mTLS/service identity; matriz completa de testes RBAC (todas combinações papel × capacidade com HTTP 200/403 verificado). |
| **Mapeia** | Req 17 (POST /internal/stale-scan), Req 18 (GET /internal/pipeline/stale), Req 13 (GET /internal/pipeline/forecast), RNF 4.3 (todas as combinações RBAC testadas), design §8 (endpoints internos), §10 (matriz RBAC) |
| **Camada principal** | Api |

#### Objetivo

Implementar `InternalPipelineController` com: `POST /internal/stale-scan` (dispara `StagnationDetectionService`, retorna 202), `GET /internal/pipeline/stale` (oportunidades estagnadas para o digest), `GET /internal/pipeline/forecast` (forecast por período para goal-forecast). Endpoints protegidos por autenticação de identidade de serviço + mTLS (não JWT de usuário). Implementar a suíte de testes RBAC cobrindo a matriz do design §10: para cada combinação de papel (Vendedor, GestorBU, TenantAdmin, Viewer) e capacidade (criar, editar, mover, ganhar, perder, reabrir, ver kanban), verificar código HTTP esperado (200 ou 403).

#### Subtasks

- [ ] **ST-01 — Red:** escrever suíte de testes RBAC paramétricos (xUnit `[Theory]`): para cada `(papel, endpoint, método)`, verificar que o resultado é `200` ou `403` conforme matriz do design §10; cobertura total de 20 combinações papel × capacidade.
- [ ] **ST-02 — Red:** escrever testes: `POST /internal/stale-scan` sem identidade de serviço → 401; com identidade de serviço válida → 202.
- [ ] **ST-03 — Green:** implementar `InternalPipelineController` com os 3 endpoints; política de autenticação de serviço separada da política JWT de usuário.
- [ ] **ST-04 — Green:** parametrizar autenticação de identidade de serviço (via header ou certificado) usando `WebApplicationFactory` nos testes.
- [ ] **ST-05 — Encerramento:** suíte RBAC completa verde (todas as combinações papel × capacidade); commit `feat(opportunity-pipeline): api internal endpoints rbac matrix tests` + push.

#### Critérios de Aceite

- [ ] 100% das combinações da matriz RBAC (design §10) cobertas por testes automatizados com HTTP 200/403 verificado (RNF 4.3).
- [ ] `POST /internal/stale-scan` sem autenticação de serviço → 401.
- [ ] `GET /internal/pipeline/stale` retorna lista de oportunidades estagnadas com `tenant_id` no payload.
- [ ] Nenhuma rota de negócio do módulo sem verificação de RBAC (verificado por análise dos atributos nos controllers via teste de reflexão).

---

### TASK-22 — Testes de isolamento cross-tenant + gate CI KPI-06 (PBT-10, RNF 3)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `test/opportunity-pipeline/22-hardening-cross-tenant-isolation` |
| **Worktree** | `git worktree add ../worktrees/opportunity-pipeline/22-hardening-cross-tenant-isolation -b test/opportunity-pipeline/22-hardening-cross-tenant-isolation` |
| **Status** | [ ] |
| **Depende de** | TASK-21 |
| **Entregável** | Suíte de testes de isolamento cross-tenant (API + SQL) com banco real; gate de CI configurado que bloqueia merge se qualquer isolamento falhar (KPI-06). |
| **Mapeia** | RNF 3 (isolamento multi-tenant, vazamento = sev-1), PBT-10 (anti-enumeração / isolamento), ADR-0001, design §14 |
| **Camada principal** | Tests |

#### Objetivo

Implementar PBT-10 completo: para qualquer par de tenants A e B, qualquer consulta executada no contexto de A não retorna nem afeta registros de B. Cenários: (1) consulta de oportunidades de A no contexto de B → resultado vazio; (2) operação de escrita de A não altera oportunidade de B; (3) acesso sem `app.current_tenant` configurado → RLS nega tudo (falha-fechada); (4) bypass via API com token de tenant A tentando acessar `id` de oportunidade de tenant B → 404 (não 403, para não revelar existência). Gate de CI (KPI-06): execução destes testes é obrigatória antes de merge na branch principal.

#### Subtasks

- [ ] **ST-01 — Red:** escrever PBT-10 (FsCheck + TestContainers): gerar aleatoriamente N oportunidades em tenant A e M em tenant B; para qualquer query executada com contexto de tenant A, verificar que nenhum `opportunity_id` de tenant B aparece no resultado.
- [ ] **ST-02 — Red:** escrever teste: `GET /opportunities/{id_de_B}` com token de tenant A → 404 (não 403, não 200).
- [ ] **ST-03 — Red:** escrever teste SQL direto (sem EF, sem RLS desabilitado): `SET app.current_tenant = 'tenant-a-id'; SELECT * FROM opportunities WHERE tenant_id = 'tenant-b-id'` → zero registros (RLS filtra).
- [ ] **ST-04 — Green:** adicionar job de CI `security-isolation-gate` que executa apenas os testes desta suíte; falha bloqueia o merge (status check obrigatório no branch protection).
- [ ] **ST-05 — Encerramento:** PBT-10 verde; gate CI KPI-06 configurado; commit `test(opportunity-pipeline): hardening cross-tenant isolation PBT-10 gate-CI` + push.

#### Critérios de Aceite

- [ ] PBT-10 verde: consulta de tenant A não retorna registros de tenant B (FsCheck com ≥ 100 amostras).
- [ ] `GET /opportunities/{id_de_B}` com contexto de tenant A → 404.
- [ ] RLS sem `app.current_tenant` → zero registros (falha-fechada verificada).
- [ ] Gate CI KPI-06 bloqueia merge se qualquer teste de isolamento falhar.
- [ ] Alerta de monitoramento configurado para tentativas de violação de RLS em produção (RNF 3.3).

---

### TASK-23 — Teste de carga Kanban 500–2.000 oportunidades p95 ≤ 2.000 ms + índices + paginação (RNF 1)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `test/opportunity-pipeline/23-hardening-kanban-load-test` |
| **Worktree** | `git worktree add ../worktrees/opportunity-pipeline/23-hardening-kanban-load-test -b test/opportunity-pipeline/23-hardening-kanban-load-test` |
| **Status** | [ ] |
| **Depende de** | TASK-22 |
| **Entregável** | Suíte de teste de carga do endpoint Kanban com 500 e 2.000 oportunidades por tenant; resultado p95 ≤ 2.000 ms documentado; paginação por coluna (`stage_page_size`) verificada. |
| **Mapeia** | RNF 1 (Kanban p95 ≤ 2.000 ms com 500–2.000 opp), RNF 1.2 (índice composto), RNF 1.3 (paginação incremental), RNF 1.4 (alerta p95), Req 18 (Kanban), design §15 |
| **Camada principal** | Tests |

#### Objetivo

Criar dataset de 500 e 2.000 oportunidades em banco de testes (TestContainers) distribuídas entre estágios. Executar `GET /opportunities/kanban?bu_id=X` e medir latência p95. Verificar que `EXPLAIN ANALYZE` usa `idx_opportunities_tenant_bu_stage`. Verificar que paginação por coluna (`stage_page_size=20`) retorna subset com `next_cursor`. Verificar que somas `SUM(valor_total)` e `SUM(forecast_ponderado)` por estágio são corretas mesmo com paginação. Configurar alerta se p95 > 2.000 ms (RNF 1.4).

#### Subtasks

- [ ] **ST-01 — Red:** escrever teste de carga: popular banco com 500 oportunidades em 5 estágios; executar 50 requisições Kanban concorrentes; verificar p95 ≤ 2.000 ms.
- [ ] **ST-02 — Red:** escrever teste de paginação: `stage_page_size=10` com 100 oportunidades em um estágio → `has_more = true`; segunda página com `cursor` → próximos 10; somas totais do estágio são corretas independente da página.
- [ ] **ST-03 — Green:** popular dataset de 2.000 oportunidades no banco de teste; executar teste de carga extendido; documentar resultado p95 medido.
- [ ] **ST-04 — Green:** verificar via `EXPLAIN (ANALYZE, BUFFERS)` que `idx_opportunities_tenant_bu_stage` é usado na query do Kanban; se não, ajustar query ou índice.
- [ ] **ST-05 — Encerramento:** resultado p95 ≤ 2.000 ms com 2.000 oportunidades documentado em arquivo de resultado; commit `test(opportunity-pipeline): hardening kanban load test RNF-1` + push.

#### Critérios de Aceite

- [ ] p95 Kanban ≤ 2.000 ms com 500 oportunidades no banco de testes.
- [ ] p95 Kanban ≤ 2.000 ms com 2.000 oportunidades (alvo estendido, RNF 1.1).
- [ ] `EXPLAIN ANALYZE` confirma uso de `idx_opportunities_tenant_bu_stage`.
- [ ] Paginação por coluna (`stage_page_size`) funciona corretamente; somas de `valor_total`/`forecast_ponderado` por estágio são corretas independente da paginação.
- [ ] Alerta de monitoramento configurado para p95 > 2.000 ms por > 5 min (RNF 1.4).

---

### TASK-24 — Observabilidade completa (logs, métricas, traces, health checks, alertas) + scan PII + DoD final

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `feat/opportunity-pipeline/24-hardening-observability-dod` |
| **Worktree** | `git worktree add ../worktrees/opportunity-pipeline/24-hardening-observability-dod -b feat/opportunity-pipeline/24-hardening-observability-dod` |
| **Status** | [ ] |
| **Depende de** | TASK-23 |
| **Entregável** | Observabilidade completa (logs estruturados, 6 métricas, traces com `correlation_id`, health checks); scan automatizado de PII em logs/eventos; verificação de SLO de escrita p95 ≤ 500 ms; DoD assinado. |
| **Mapeia** | RNF 2 (latência escrita ≤ 500 ms), RNF 10 (observabilidade, sem PII), RNF 12 (disponibilidade Tier 1), Req 20 (eventos observáveis), design §11, DoD §19 |
| **Camada principal** | Infrastructure / DevOps |

#### Objetivo

Garantir que toda a infraestrutura de observabilidade está em produção: (1) logs estruturados com `correlation_id`, `tenant_id`, `bu_id`, `opportunity_id`, `action` em todas as operações — sem PII; (2) métricas `opportunities_created_total`, `opportunities_won_total`, `opportunities_lost_total`, `opportunities_stale_total`, `commission_snapshot_created_total`, `kanban_request_duration_ms` (histograma) e `outbox_pending_events` (gauge); (3) trace em `WinOpportunityHandler` cobrindo Win + Freeze + persistência + outbox; (4) health checks liveness/readiness (DB, Pub/Sub, portas críticas); (5) scan automatizado de PII em logs de teste (detecta campos proibidos); (6) verificação SLO de escrita p95 ≤ 500 ms a 50 RPS.

#### Subtasks

- [ ] **ST-01 — Red:** escrever teste de scan de PII: executar todas as operações de criação, movimentação e ganho em modo de teste com log capturado; verificar que nenhum campo `email`, `cpf`, `phone`, `contact_name` aparece nos logs/traces.
- [ ] **ST-02 — Red:** escrever teste de SLO de escrita: 50 RPS de criação/movimentação; p95 ≤ 500 ms incluindo gravação do AuditLog.
- [ ] **ST-03 — Green:** configurar `Serilog` (ou equivalente) com enrichers para `correlation_id`, `tenant_id`, `bu_id`, `opportunity_id`; adicionar `PiiSanitizingEnricher` que remove campos proibidos antes de logar.
- [ ] **ST-04 — Green:** implementar as 6 métricas com `System.Diagnostics.Metrics` (ou Prometheus); registrar histograma de duração do Kanban; gauge do outbox pendente.
- [ ] **ST-05 — Green:** implementar trace span em `WinOpportunityHandler` com `ActivitySource`; propagar `correlation_id` às portas externas via headers.
- [ ] **ST-06 — Green:** implementar health checks ASP.NET Core: liveness (processo vivo), readiness (DB conectado, Pub/Sub acessível, pelo menos 1 porta upstream acessível); endpoint `/health`.
- [ ] **ST-07 — Docs:** atualizar README do módulo com status "DoD — Onda 6 concluída"; atualizar matriz de rastreabilidade com status `[X]` em todas as entradas.
- [ ] **ST-08 — Encerramento:** scan PII verde (sem PII em logs); SLO de escrita p95 ≤ 500 ms a 50 RPS verificado; DoD §19 do design revisado item a item; commit `feat(opportunity-pipeline): hardening observability dod` + push.

#### Critérios de Aceite

- [ ] Scan automatizado de PII em logs/traces verde (nenhum campo proibido detectado).
- [ ] SLO de escrita p95 ≤ 500 ms a 50 RPS (incluindo AuditLog na mesma transação).
- [ ] 6 métricas instrumentadas e verificadas em teste de smoke.
- [ ] Health check `/health/ready` retorna 200 com DB e Pub/Sub disponíveis; 503 se DB indisponível.
- [ ] Trace de `WinOpportunityHandler` visível com spans de domínio, persistência e outbox.
- [ ] Todos os itens do DoD §19 do design.md verificados e marcados (incluindo VAL-07 resolvida como pré-condição).

---

## 5. Matriz de Rastreabilidade

| Origem | Descrição curta | TASKs | Status |
|--------|----------------|-------|--------|
| Req 1 | Criar oportunidade (completa e rápida) | TASK-06, TASK-09, TASK-20 | [ ] |
| Req 2 | Owner obrigatório (`OPPORTUNITY_OWNER_REQUIRED`) | TASK-06, TASK-09, TASK-20 | [ ] |
| Req 3 | Numeração AZ-NNNN atômica e imutável | TASK-03, TASK-06, TASK-15 | [ ] |
| Req 4 | Canal obrigatório; canal Parceiro exige parceiro | TASK-03, TASK-06, TASK-09 | [ ] |
| Req 5 | Estágio obrigatório; transição na timeline | TASK-05, TASK-06, TASK-09, TASK-13 | [ ] |
| Req 6 | Máquina de estados por categoria (open/won/lost) | TASK-06, TASK-10 | [ ] |
| Req 7 | Probabilidade herdada do estágio, editável | TASK-03, TASK-06, TASK-09 | [ ] |
| Req 8 | valor_total e forecast_ponderado em centavos | TASK-02, TASK-04, TASK-13 | [ ] |
| Req 9 | Data de fechamento obrigatória a partir de Proposta Enviada | TASK-04, TASK-06, TASK-09 | [ ] |
| Req 10 | Perder com motivo obrigatório | TASK-07, TASK-10, TASK-20 | [ ] |
| Req 11 | Vínculo de comissão por componente | TASK-03, TASK-05, TASK-07, TASK-11 | [ ] |
| Req 12 | Cálculo de comissão por componente | TASK-04, TASK-07, TASK-11 | [ ] |
| Req 13 | Forecast líquido (forecast ponderado − comissão ponderada) | TASK-04, TASK-12, TASK-19 | [ ] |
| Req 14 | Ganhar com snapshot imutável de comissão | TASK-05, TASK-07, TASK-10, TASK-14, TASK-16 | [ ] |
| Req 15 | Reabertura restrita a gestão com re-auditoria | TASK-07, TASK-10, TASK-21 | [ ] |
| Req 16 | Contatos 1..N com exatamente um principal | TASK-05, TASK-07, TASK-11 | [ ] |
| Req 17 | Detecção de estagnação (≥ 14 dias, idempotente) | TASK-04, TASK-07, TASK-12, TASK-18 | [ ] |
| Req 18 | Kanban por BU com somas e desempenho fluido | TASK-12, TASK-13, TASK-20, TASK-23 | [X] |
| Req 19 | Lista com filtros salvos + linha do tempo | TASK-11, TASK-12, TASK-20 | [X] |
| Req 20 | Publicação de eventos de domínio via Outbox | TASK-05, TASK-16, TASK-19 | [X] |
| RNF 1 | Desempenho Kanban p95 ≤ 2.000 ms (500–2.000 opp) | TASK-12, TASK-13, TASK-23 | [X] |
| RNF 2 | Latência de escrita p95 ≤ 500 ms a 50 RPS | TASK-08, TASK-16, TASK-24 | [X] |
| RNF 3 | Isolamento multi-tenant em defesa em profundidade | TASK-13, TASK-16, TASK-22 | [X] |
| RNF 4 | RBAC em todo endpoint do módulo | TASK-08, TASK-20, TASK-21 | [X] |
| RNF 5 | Imutabilidade do snapshot no banco | TASK-07, TASK-14 | [X] |
| RNF 6 | AuditLog imutável para toda escrita | TASK-05, TASK-16, TASK-24 | [X] |
| RNF 7 | Transições append-only (sem UPDATE/DELETE) | TASK-05, TASK-13 | [X] |
| RNF 8 | Retenção indefinida de snapshot e transições | TASK-13 | [X] |
| RNF 9 | Idempotência e resiliência da detecção de estagnação | TASK-12, TASK-18 | [X] |
| RNF 10 | Observabilidade sem PII | TASK-08, TASK-16, TASK-24 | [X] |
| RNF 11 | Integridade monetária em centavos (NBR 5891) | TASK-02, TASK-04, TASK-19 | [X] |
| RNF 12 | Disponibilidade Tier 1 | TASK-24 | [X] |
| PBT-01 | Unicidade e atomicidade de opportunity_number | TASK-15 | [X] |
| PBT-02 | Imutabilidade do opportunity_number | TASK-03, TASK-06 | [X] |
| PBT-03 | Invariante do valor_total (TCV) | TASK-02, TASK-04 | [X] |
| PBT-04 | Invariante do forecast_ponderado | TASK-02, TASK-04 | [X] |
| PBT-05 | Invariante da comissão por componente | TASK-03, TASK-04 | [X] |
| PBT-06 | Invariante do forecast líquido | TASK-04 | [X] |
| PBT-07 | Imutabilidade do snapshot de comissão | TASK-07, TASK-14 | [X] |
| PBT-08 | Máquina de estados de estágio | TASK-06 | [X] |
| PBT-09 | Idempotência da detecção de estagnação | TASK-12, TASK-18 | [X] |
| PBT-10 | Isolamento por tenant (anti-enumeração) | TASK-22 | [X] |
| PBT-11 | Snapshot preservado após alterar pct do parceiro | TASK-07, TASK-14 | [X] |
| DD-001 | Geração atômica de opportunity_number por tenant | TASK-15 | [X] |
| DD-002 | Mecânica do snapshot imutável de comissão | TASK-05, TASK-07, TASK-14 | [X] |
| DD-003 | Comissão como entidade; termos e cálculo como VOs | TASK-03, TASK-04, TASK-05 | [X] |
| DD-004 | Money em centavos e fidelidade ao data-model | TASK-02, TASK-13, TASK-19 | [X] |
| DD-005 | Detecção de estagnação por scheduler com idempotência | TASK-12, TASK-18 | [X] |
| DD-006 | RLS falha-fechada e interceptor de conexão | TASK-13, TASK-16 | [X] |
| DD-007 | VAL-07: ganho com comissão em branco (pendência bloqueante) | TASK-10 | [X] |
| ADR-0001 | Isolamento multi-tenant em defesa em profundidade | TASK-13, TASK-16, TASK-22 | [X] |
| ADR-0002 | Snapshot imutável (a formalizar) | TASK-14 | [X] |
| ADR-0003 | Unicidade opportunity_number por tenant (a formalizar) | TASK-15 | [X] |
| ADR-0004 | Outbox + idempotência de consumers (a formalizar) | TASK-16 | [X] |

---

## 6. Coverage Gates

| Camada | Gate mínimo | Tipo de teste principal | Notas |
|--------|------------|------------------------|-------|
| Domain | ≥ 95% | Unitários + PBT (FsCheck) para invariantes INV-1..13, calculators, state machine | PBT-01..11 incluídos |
| Application | ≥ 85% | Unitários de handlers (escritas + queries), behaviors (RBAC, idempotência, transaction), StagnationDetectionService | Portas mockadas |
| Infrastructure | ≥ 70% | Integração com banco real (TestContainers): numeração atômica, trigger snapshot, RLS, Outbox, RlsConnectionInterceptor | PBT-01, PBT-07 infra aqui |
| Api | ≥ 80% | Testes de contrato HTTP (WebApplicationFactory), RBAC 200/403 por papel × capacidade, ProblemDetails com OP-ERR-* | Cobertura de todos os endpoints de §8 |
| Architecture | 100% das regras | Testes de dependência entre as 5 camadas (ArchUnitNET) | Sem exceções |
| Security | Cobertura por cenário | Isolamento cross-tenant (PBT-10), RBAC por rota, anti-PII em logs, REVOKE no banco | Gate CI KPI-06 obrigatório |
| Observability | Cobertura por fluxo crítico | Logs estruturados sem PII, métricas (6), trace em Win, health checks | Scan automático de PII |

Coverage gates são pré-condições para encerramento de onda. Gate de CI bloqueia merge se `Domain < 95%` ou `Api < 80%` ou qualquer teste de isolamento falhar (KPI-06).

---

## 7. Critérios de Encerramento

### 7.1 Encerramento de TASK

Uma TASK só pode ser marcada `[X]` quando:

- todas as subtasks concluídas e marcadas `[X]`
- testes locais verdes (sem falha, sem skip injustificado)
- coverage gate da camada atendido ou justificativa registrada
- lint/format executado sem erros
- nenhum warning novo relevante introduzido
- commit em Conventional Commits realizado
- push da branch realizado
- documentação atualizada quando aplicável

### 7.2 Encerramento de Onda

| Onda | Critérios adicionais |
|------|---------------------|
| Onda 1 | Architecture.Tests 100% verde; build sem warnings |
| Onda 2 | Domain.Tests ≥ 95%; PBT-02..08 verdes (FsCheck); invariantes INV-1..13 com teste explícito |
| Onda 3 | Application.Tests ≥ 85%; PBT-09 verde; StagnationDetectionService idempotente verificado |
| Onda 4 | Infrastructure.Tests ≥ 70%; PBT-01, PBT-07, PBT-11 verdes com banco real; RLS habilitada em 7 tabelas; trigger snapshot funcionando |
| Onda 5 | Api.Tests ≥ 80%; 100% das combinações RBAC (papel × capacidade) testadas; OpenAPI spec gerada sem erros |
| Onda 6 | PBT-10 verde; gate CI KPI-06 configurado; p95 Kanban ≤ 2.000 ms com 2.000 oportunidades documentado; scan PII verde; DoD §19 do design.md assinado; **VAL-07 resolvida por Produto** |

### 7.3 Encerramento do Módulo

O módulo `opportunity-pipeline` é considerado pronto para produção quando:

- todas as 6 ondas encerradas com CI verde
- PBT-01..11 todos verdes
- matriz de rastreabilidade completa (nenhuma origem sem TASK)
- `requirements.md`, `design.md` e `tasks.md` consistentes e na mesma versão
- isolamento cross-tenant verificado como gate de CI (KPI-06)
- snapshot imutável garantido no banco por trigger + constraint (verificado com banco real)
- catálogo de erros OP-ERR-001..017 coberto por testes de contrato
- eventos `.v1` publicados via Outbox com consistência transacional verificada
- SLO Kanban p95 ≤ 2.000 ms e SLO escrita p95 ≤ 500 ms documentados
- observabilidade: logs sem PII, 6 métricas, traces, health checks em produção
- **VAL-07 (DD-007) RESOLVIDA por Produto (11/06/2026): alertar com confirmação** — `commission_required_on_win = false`; Onda 3 liberada

---

## 8. Riscos de Execução

| Código | Risco | Impacto | Prob. | Mitigação na execução |
|--------|-------|---------|-------|-----------------------|
| RISK-EXEC-01 | VAL-07 não decidida antes do início da Onda 3 | TASK-10 (`WinOpportunityHandler`) fica bloqueada; implementação especulativa pode precisar de retrabalho | Alta | Escalar imediatamente para Produto; implementar ponto de extensão (`commission_required_on_win`) em TASK-10 como mitigação de custo de troca |
| RISK-EXEC-02 | TestContainers aumenta tempo de CI (imagem Postgres por suite) | CI lento atrasa feedback loop | Média | Compartilhar instância Postgres entre suítes Infrastructure e Security em CI; usar imagem Postgres slim |
| RISK-EXEC-03 | PBT-01 (concorrência de números) difícil de reproduzir de forma confiável em ambiente de CI | Gate flaky intermitente | Média | Usar `SemaphoreSlim` ou `Barrier` para sincronizar tasks no teste; executar N = 20 com timeout fixo; aceitar 3 retries no CI |
| RISK-EXEC-04 | Portas de leitura (organization, partner) indisponíveis em ambiente de testes | TASK-17 com dependências externas não testáveis em CI | Média | Todos os testes de Application usam mocks das portas; testes de Infrastructure usam stubs HTTP; sem chamadas reais a upstream em CI |
| RISK-EXEC-05 | RLS Postgres com `FORCE ROW LEVEL SECURITY` pode bloquear operações de migration do role `app` | Migration falha ao adicionar/remover dados em tabelas com RLS habilitada | Baixa | Migrations executadas com role privilegiado (não `app`); role `app` não executa DDL; documentar na TASK-13 |
| RISK-EXEC-06 | ADR-0002, ADR-0003, ADR-0004 não formalizadas até a Onda 4 | Decisões de design sem ADR aprovado podem ser contestadas em revisão | Média | Criar os 3 ADRs em paralelo às TASKs de Onda 2/3; não bloquear execução, mas priorizar formalização antes da Onda 4 |

---

## 9. Referências

| Origem | Referência |
|--------|------------|
| Requirements | `docs/product/modules/opportunity-pipeline/requirements.md` v0.1.0 (Req 1..20, RNF 1..12, PBT-01..11) |
| Design | `docs/product/modules/opportunity-pipeline/design.md` v0.1.0 (DD-001..007, INV-1..13, §3..19) |
| README do módulo | `docs/product/modules/opportunity-pipeline/README.md` |
| ADR | `docs/product/adr/0001-isolamento-multi-tenant-defesa-em-profundidade.md` (Aceito); ADR-0002/0003/0004 a formalizar |
| TRD | `docs/product/trd/trd.md` §7.2, §9.3, §9.5, §10.2, §16, §17 |
| Data Model | `docs/product/data-model/data-model.md` §3 |
| Glossário | `docs/product/glossary/ubiquitous-language.md` § BC-01 |
| Rules | `.forge/rules/architecture/{clean-architecture,ddd,api-and-contracts,observability,security-and-compliance,jwt-authentication,mtls-internal-services}.md`; `.forge/rules/domain/{money-as-cents,nbr-5891-rounding,audit-immutability}.md`; `.forge/rules/conventions/{database-naming,language-policy,document-versioning}.md` |
| Decisões | DEC-001, DEC-002, DEC-006, DEC-009, DEC-010, DEC-011, DEC-012; DD-001..007 (design §17) |
| Pendências bloqueantes | **VAL-07** (comissão em branco no ganho — DD-007): pré-condição para promoção a "Aprovado para desenvolvimento" e para início de Onda 3 |
