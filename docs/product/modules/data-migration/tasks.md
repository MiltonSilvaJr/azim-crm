# Tasks — BC-09 — Data Migration (Migração de Planilha)

- Versão: 0.1.0
- Data: 2026-06-11
- Status: Rascunho para revisão
- Referência base requirements: docs/product/modules/data-migration/requirements.md v0.1.0
- Referência base design: docs/product/modules/data-migration/design.md v0.1.0
- ADRs aplicáveis: ADR-0001 (isolamento multi-tenant em defesa em profundidade), ADR-0003 (unicidade de opportunity_number por tenant)
- Rules aplicáveis: `.forge/rules/architecture/clean-architecture.md`, `.forge/rules/architecture/api-and-contracts.md`, `.forge/rules/architecture/ddd.md`, `.forge/rules/architecture/observability.md`, `.forge/rules/architecture/security-and-compliance.md`, `.forge/rules/conventions/database-naming.md`, `.forge/rules/domain/money-as-cents.md`, `.forge/rules/domain/nbr-5891-rounding.md`, `.forge/rules/domain/audit-immutability.md`

## Histórico de Versões

| Versão | Data | Status | Descrição da alteração |
|--------|------|--------|------------------------|
| 0.1.0 | 2026-06-11 | Rascunho para revisão | Criação inicial do plano derivado de requirements.md v0.1.0 e design.md v0.1.0 |

## 1. Convenções de Implementação

### 1.1 TDD-first

Toda implementação com lógica verificável segue o ciclo:

1. Red — escrever teste que falha
2. Green — implementar o mínimo para passar
3. Refactor — melhorar sem alterar comportamento

Nenhuma regra de domínio, handler, endpoint, persistência, contrato ou integração é concluída sem teste correspondente.

### 1.2 Property-Based Testing

PBT é obrigatório para:

- invariantes matemáticas (forecast recalculado em centavos)
- idempotência (reexecução do import após rolled_back)
- round-trip (serial Excel ↔ ISO, epoch 1899-12-30)
- anti-enumeração/unicidade (números AZ-NNNN por tenant)
- atomicidade (rollback total)
- state machines (MigrationJob)
- conservação de contagem (oportunidades = linhas válidas, contas = nomes distintos)

Cada PBT mapeia explicitamente para `PBT-NN` do `requirements.md` ou invariante descrita no `design.md`. Biblioteca: FsCheck integrado com xUnit.

### 1.3 Bite-sized Tasks

Cada subtask deve ter menos de 2 horas. Cada TASK deve ter preferencialmente até 1 dia; no máximo 2 dias quando o escopo e o entregável forem claros e verificáveis.

### 1.4 Branch Model

```
<tipo>/data-migration/<NN>-<slug>
```

Exemplos: `feat/data-migration/01-bootstrap-clean-architecture`, `test/data-migration/03-migration-job-aggregate`.

### 1.5 Git Worktree

```sh
git worktree add ../worktrees/data-migration/<NN>-<slug> -b <branch>
```

### 1.6 Encerramento de TASK

- testes locais verdes
- coverage gate da camada atendido ou justificativa registrada
- `dotnet format` executado
- documentação atualizada quando aplicável
- commit em Conventional Commits
- push da branch

### 1.7 Encerramento de Onda

- todas as TASKs da onda em `[X]`
- CI verde
- PR da onda aberto ou atualizado
- riscos da onda tratados ou registrados
- README do módulo sincronizado quando aplicável

### 1.8 Early Exit

Se uma subtask falhar: marcar `[-]`, registrar ponto de falha, comando executado e erro principal; não mascarar com implementação especulativa; deixar contexto suficiente para retomada.

### 1.9 Convenção de Status

- `[ ]` Não iniciado
- `[-]` Em progresso
- `[X]` Concluído
- `[!]` Falhou — exige intervenção humana (interrompe a onda no `task-coder`)

### 1.10 Convenção canônica de IDs

```
TASK-NN — <título>
  ST-MM — <subtask>
```

- TASK é a unidade que o `task-coder` invoca contra um specialist.
- ST-MM são etapas internas TDD executadas em sequência; numeração reinicia a cada nova TASK.
- Onda é atributo de campo no header da TASK e seção de agrupamento; nunca entra no ID.

## 2. Status Geral

| TASK | Título | Onda | Branch | Status |
|------|--------|------|--------|--------|
| TASK-01 | Criar solution e 5 projetos Clean Architecture | Onda 1 | `feat/data-migration/01-bootstrap-clean-architecture` | [ ] |
| TASK-02 | Testes de arquitetura (NetArchTest) | Onda 1 | `test/data-migration/02-architecture-tests` | [ ] |
| TASK-03 | Agregado MigrationJob + máquina de estados + PBT-07 | Onda 2 | `feat/data-migration/03-migration-job-aggregate` | [ ] |
| TASK-04 | Objeto de valor ExcelSerialDate + PBT-03 | Onda 2 | `feat/data-migration/04-excel-serial-date` | [ ] |
| TASK-05 | Objeto de valor ForecastDivergence + PBT-06 | Onda 2 | `feat/data-migration/05-forecast-divergence` | [ ] |
| TASK-06 | Objeto de valor OpportunityNumber + PBT-05 | Onda 2 | `feat/data-migration/06-opportunity-number` | [ ] |
| TASK-07 | Objetos de valor auxiliares + Policies/Specifications | Onda 2 | `feat/data-migration/07-value-objects-policies` | [ ] |
| TASK-08 | UploadSpreadsheetCommand + Handler | Onda 3 | `feat/data-migration/08-upload-handler` | [ ] |
| TASK-09 | RunDryRunCommand + Handler + TriageReport + PBT-04 | Onda 3 | `feat/data-migration/09-dry-run-handler` | [ ] |
| TASK-10 | Commands de triagem assistida (owner, estágio, parceiro, dedupe, salvar, ready) | Onda 3 | `feat/data-migration/10-triage-commands` | [ ] |
| TASK-11 | ExecuteImportCommand + Handler (tudo-ou-nada) + PBT-01 + PBT-02 | Onda 3 | `feat/data-migration/11-execute-import-handler` | [ ] |
| TASK-12 | Pipeline Behaviors (TenantContext, Authorization, FeatureFlag, Validation, PiiSafe, Idempotency) | Onda 3 | `feat/data-migration/12-pipeline-behaviors` | [ ] |
| TASK-13 | Queries (GetMigrationJobStatus, GetTriageReport, GetImportReport) | Onda 3 | `feat/data-migration/13-queries` | [ ] |
| TASK-14 | SpreadsheetParser (ClosedXML, DD-002) + CanonicalRowMapper | Onda 4 | `feat/data-migration/14-spreadsheet-parser` | [ ] |
| TASK-15 | EF Core + migrations (migration_jobs, migration_logs) + RLS (DD-008) + índices | Onda 4 | `feat/data-migration/15-ef-migrations-rls` | [ ] |
| TASK-16 | IAccountImportPort + IPartnerImportPort + AccountDedupePolicy (in-process) | Onda 4 | `feat/data-migration/16-account-partner-ports` | [ ] |
| TASK-17 | IOpportunityImportPort + IOpportunityNumberPort (in-process, ADR-0003) | Onda 4 | `feat/data-migration/17-opportunity-ports` | [ ] |
| TASK-18 | IActivityImportPort + IOrganizationReadPort + UnitOfWork compartilhado (DD-001) | Onda 4 | `feat/data-migration/18-activity-org-uow` | [ ] |
| TASK-19 | Idempotência por import_key + upsert idempotente nos adaptadores (DD-003) | Onda 4 | `feat/data-migration/19-idempotency-import-key` | [ ] |
| TASK-20 | Outbox (ImportCompleted) + eventos de domínio (DryRunCompleted, ImportRolledBack) | Onda 4 | `feat/data-migration/20-outbox-events` | [ ] |
| TASK-21 | MigrationController — upload + dry-run + status (MIG-ERR-001..004) | Onda 5 | `feat/data-migration/21-api-upload-dryrun` | [ ] |
| TASK-22 | MigrationController — endpoints de triagem (owners, stages, partners, dedupe, ready) | Onda 5 | `feat/data-migration/22-api-triage` | [ ] |
| TASK-23 | MigrationController — execute + confirmação explícita + catálogo MIG-ERR-005..010 | Onda 5 | `feat/data-migration/23-api-execute` | [ ] |
| TASK-24 | PiiSafeLogger + PiiSafeLoggingBehavior (RNF 3, LGPD) | Onda 6 | `feat/data-migration/24-pii-safe-logging` | [ ] |
| TASK-25 | Métricas + logs estruturados + traces (RNF 6) | Onda 6 | `feat/data-migration/25-observability` | [ ] |
| TASK-26 | Teste de staging — dry-run e rollback com 108 e 500 linhas (RNF 1.3, RNF 5.2) | Onda 6 | `test/data-migration/26-staging-volume-tests` | [ ] |
| TASK-27 | Feature flag migration.import_enabled + plano de remoção pós-Fase 1 (DD-009, Req 14) | Onda 6 | `feat/data-migration/27-feature-flag-lifecycle` | [ ] |
| TASK-28 | DoD final — README corrigido (DD-001), rastreabilidade sincronizada, VAL-MIGR-01 registrado | Onda 6 | `docs/data-migration/28-dod-final` | [ ] |

## 3. Ondas de Implementação

| Onda | Foco | TASKs | Risco principal |
|------|------|-------|-----------------|
| Onda 1 | Bootstrap | TASK-01..TASK-02 | Solution sem template de referência; risk baixo |
| Onda 2 | Domain | TASK-03..TASK-07 | PBTs de state machine e round-trip de datas exigem generators precisos |
| Onda 3 | Application | TASK-08..TASK-13 | ExecuteImportHandler com transação única e rollback-only do dry-run são os pontos mais complexos |
| Onda 4 | Infrastructure | TASK-14..TASK-20 | Portas in-process devem contratos de upsert idempotente; RLS falha-fechada (RISK-MIGR-04) |
| Onda 5 | API + Contratos | TASK-21..TASK-23 | Confirmação explícita e feature flag devem estar alinhadas ao catálogo de erros |
| Onda 6 | Hardening | TASK-24..TASK-28 | Teste de staging com volume real (108/500) é gate de go-live (RNF 5.2) |

## 4. Tarefas

---

### TASK-01 — Criar solution e 5 projetos Clean Architecture

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 1 — Bootstrap |
| **Branch** | `feat/data-migration/01-bootstrap-clean-architecture` |
| **Worktree** | `git worktree add ../worktrees/data-migration/01-bootstrap-clean-architecture -b feat/data-migration/01-bootstrap-clean-architecture` |
| **Status** | [ ] |
| **Depende de** | Não aplicável |
| **Entregável** | `DataMigration.sln` com 5 projetos de produção e 5 de teste compilando; CI verde |
| **Mapeia** | design §3 (estrutura da solução) |
| **Camada principal** | DevOps |

#### Objetivo

Criar a estrutura canônica Clean Architecture do módulo data-migration com os 5 projetos físicos (.NET 10): `DataMigration.Domain`, `DataMigration.Application`, `DataMigration.Infrastructure`, `DataMigration.Api`, `DataMigration.Contracts`, mais seus respectivos projetos de teste. Configurar referências entre projetos respeitando a direção DA (Domain sem deps externas).

#### Subtasks

- [ ] **ST-01 — Red:** escrever teste de sanidade que verifica que `DataMigration.Domain` compila sem referência a EF Core ou ClosedXML.
- [ ] **ST-02 — Green:** criar `DataMigration.sln`; adicionar os 5 projetos de produção e 5 de teste; configurar referências (Application → Domain; Infrastructure → Application + Domain; Api → Application + Infrastructure + Contracts).
- [ ] **ST-03 — Refactor:** ajustar `.csproj` (nullable enabled, implicit usings, TFM net10.0, TreatWarningsAsErrors).
- [ ] **ST-04 — Docs:** atualizar campo de status e stack no README do módulo.
- [ ] **ST-05 — Encerramento:** `dotnet build` verde; commit `chore(data-migration): bootstrap solution clean-architecture`; push.

#### Critérios de Aceite

- [ ] `dotnet build` verde para todos os 10 projetos.
- [ ] Dependências seguem a regra CA: Domain sem referência a EF Core, ClosedXML ou qualquer SDK de infraestrutura.
- [ ] CI executa build e testes mínimos com sucesso.
- [ ] Nenhum warning novo introduzido.

---

### TASK-02 — Testes de arquitetura (NetArchTest)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 1 — Bootstrap |
| **Branch** | `test/data-migration/02-architecture-tests` |
| **Worktree** | `git worktree add ../worktrees/data-migration/02-architecture-tests -b test/data-migration/02-architecture-tests` |
| **Status** | [ ] |
| **Depende de** | TASK-01 |
| **Entregável** | `DataMigration.Architecture.Tests` com regras de dependência entre camadas validadas em CI |
| **Mapeia** | design §3 (Clean Architecture, proibição Domain-Infrastructure) |
| **Camada principal** | Tests |

#### Objetivo

Implementar testes de arquitetura com NetArchTest para garantir que as regras de dependência do Clean Architecture sejam verificadas automaticamente no CI, impedindo regressões estruturais.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes que falham para: (a) Domain referenciando infraestrutura; (b) Application referenciando Infrastructure; (c) Contracts referenciando qualquer outro projeto do módulo.
- [ ] **ST-02 — Green:** configurar `DataMigration.Architecture.Tests` com NetArchTest; implementar as regras de camada; confirmar que todos os testes passam na estrutura criada na TASK-01.
- [ ] **ST-03 — Refactor:** adicionar regra explícita proibindo referência a `ClosedXML` fora de Infrastructure.
- [ ] **ST-04 — Docs:** nenhum documento adicional necessário neste ponto.
- [ ] **ST-05 — Encerramento:** testes verdes no CI; commit `test(data-migration): architecture dependency rules`; push.

#### Critérios de Aceite

- [ ] Todas as regras NetArchTest passam em CI.
- [ ] Qualquer violação de camada quebra o build.
- [ ] Teste explícito confirma que Domain não depende de EF Core, ClosedXML ou qualquer SDK externo.
- [ ] Coverage gate de Architecture: 100% das regras críticas cobertas.

---

### TASK-03 — Agregado MigrationJob + máquina de estados + PBT-07

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `feat/data-migration/03-migration-job-aggregate` |
| **Worktree** | `git worktree add ../worktrees/data-migration/03-migration-job-aggregate -b feat/data-migration/03-migration-job-aggregate` |
| **Status** | [ ] |
| **Depende de** | TASK-02 |
| **Entregável** | `MigrationJob` (Aggregate Root) com 7 estados, transições guardadas e `MigrationLogEntry`; PBT-07 verde |
| **Mapeia** | Req 4, Req 6, Req 12; PBT-07; design §4.1, §4.5; MIG-ERR-005, MIG-ERR-006 |
| **Camada principal** | Domain |

#### Objetivo

Implementar o agregado `MigrationJob` com a máquina de estados linear (`created → dry_run_completed → triage_in_progress → ready_to_import → importing → completed | rolled_back | failed`). Proteger as invariantes: transições inválidas lançam `InvalidMigrationStateTransition`; `CanTransitionToImporting()` exige zero oportunidades sem owner (PBT-07). Incluir a entidade filha `MigrationLogEntry`.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes unitários para cada transição válida e para todas as transições inválidas; escrever PBT-07 (FsCheck) — propriedade: para qualquer estado de triagem, transição para `importing` é rejeitada se existir oportunidade sem owner.
- [ ] **ST-02 — Green:** implementar `MigrationJob`, `MigrationJobStatus` (enum), `MigrationLogEntry`; implementar `CanTransitionToImporting()` e `Transition(newState)`; proteger invariantes com exceções de domínio.
- [ ] **ST-03 — Refactor:** extrair `OwnerRequiredSpecification` como especificação independente; garantir imutabilidade dos metadados do arquivo após `created`.
- [ ] **ST-04 — Docs:** nenhuma atualização de documentação externa necessária neste ponto.
- [ ] **ST-05 — Encerramento:** Domain.Tests verdes; coverage Domain ≥ 95%; commit `feat(data-migration): migration-job aggregate state machine`; push.

#### Critérios de Aceite

- [ ] Todas as transições válidas testadas e passando.
- [ ] Todas as transições inválidas lançam `InvalidMigrationStateTransition` (MIG-ERR-005).
- [ ] PBT-07 verde: transição para `importing` bloqueada quando existe oportunidade sem owner.
- [ ] `completed` e `failed` são estados terminais (rejeitam qualquer transição).
- [ ] `rolled_back` permite transição de volta para `triage_in_progress`.
- [ ] Coverage Domain ≥ 95%.

---

### TASK-04 — Objeto de valor ExcelSerialDate + PBT-03

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `feat/data-migration/04-excel-serial-date` |
| **Worktree** | `git worktree add ../worktrees/data-migration/04-excel-serial-date -b feat/data-migration/04-excel-serial-date` |
| **Status** | [ ] |
| **Depende de** | TASK-02 |
| **Entregável** | `ExcelSerialDate` (objeto de valor) com conversão serial ↔ ISO e PBT-03 verde |
| **Mapeia** | Req 9; PBT-03; design §4.3, DD-006 |
| **Camada principal** | Domain |

#### Objetivo

Implementar o objeto de valor `ExcelSerialDate` que converte serial Excel (inteiro) para `DateOnly` ISO usando o epoch correto de **1899-12-30** (acomodando o bug histórico do ano-1900 do Excel). Célula vazia retorna `null` sem erro. Datas no passado são marcadas com flag "vencida".

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes unitários para casos conhecidos (ex: serial 45000 → data ISO correspondente); escrever PBT-03 (FsCheck) — propriedade round-trip: `serial → ISO → serial` reproduz o serial original; `vazio → null` sem exceção.
- [ ] **ST-02 — Green:** implementar `ExcelSerialDate.FromSerial(int? serial)` e `ToSerial()` com epoch 1899-12-30; implementar flag `IsOverdue` para datas passadas.
- [ ] **ST-03 — Refactor:** garantir igualdade por valor; documentar epoch no código com referência ao DD-006.
- [ ] **ST-04 — Docs:** sem atualização externa.
- [ ] **ST-05 — Encerramento:** PBT-03 verde; coverage Domain ≥ 95% acumulado; commit `feat(data-migration): excel-serial-date value object pbt-03`; push.

#### Critérios de Aceite

- [ ] PBT-03 verde: round-trip `serial → ISO → serial` determinístico para qualquer serial válido.
- [ ] `FromSerial(null)` retorna data nula sem exceção.
- [ ] Datas passadas recebem `IsOverdue = true`.
- [ ] Domain não referencia nenhuma biblioteca de infraestrutura.
- [ ] Coverage Domain ≥ 95% acumulado.

---

### TASK-05 — Objeto de valor ForecastDivergence + PBT-06

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `feat/data-migration/05-forecast-divergence` |
| **Worktree** | `git worktree add ../worktrees/data-migration/05-forecast-divergence -b feat/data-migration/05-forecast-divergence` |
| **Status** | [ ] |
| **Depende de** | TASK-02 |
| **Entregável** | `ForecastDivergence` com cálculo `round_half_even(valor_total × probabilidade / 100)` e PBT-06 verde |
| **Mapeia** | Req 2.3; PBT-06; design §4.3, §4.6, DD-005; `.forge/rules/domain/nbr-5891-rounding.md` |
| **Camada principal** | Domain |

#### Objetivo

Implementar o objeto de valor `ForecastDivergence` que encapsula o recálculo do forecast ponderado em centavos inteiros com arredondamento bancário NBR 5891 (`round_half_even`). Divergência só é listada quando `|forecast_calculado - forecast_planilha| > 1` (centavo).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes unitários para casos de divergência (> 1 centavo) e não-divergência (≤ 1 centavo); escrever PBT-06 (FsCheck) — propriedade: para qualquer `valor_total` e `probabilidade`, o resultado é `round_half_even(valor_total × probabilidade / 100)` em centavos; divergência listada se e somente se `|Δ| > 1`.
- [ ] **ST-02 — Green:** implementar `ForecastDivergence.Calculate(long valorTotal, int probabilidade, long forecastPlanilha)` retornando `(long forecastCalculado, bool hasDivergence, long delta)`; usar aritmética inteira em centavos; proibir `float`/`double`.
- [ ] **ST-03 — Refactor:** garantir igualdade por valor; extrair constante `DivergenceThresholdCents = 1L`.
- [ ] **ST-04 — Docs:** sem atualização externa.
- [ ] **ST-05 — Encerramento:** PBT-06 verde; commit `feat(data-migration): forecast-divergence value object pbt-06`; push.

#### Critérios de Aceite

- [ ] PBT-06 verde: `round_half_even` aplicado; apenas divergências > 1 centavo listadas.
- [ ] Nenhum uso de `float`, `double` ou `decimal` no cálculo de domínio.
- [ ] Casos de arredondamento half-even testados explicitamente (ex: 0,5 centavo arredonda para par).
- [ ] Coverage Domain ≥ 95% acumulado.

---

### TASK-06 — Objeto de valor OpportunityNumber + PBT-05

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `feat/data-migration/06-opportunity-number` |
| **Worktree** | `git worktree add ../worktrees/data-migration/06-opportunity-number -b feat/data-migration/06-opportunity-number` |
| **Status** | [ ] |
| **Depende de** | TASK-02 |
| **Entregável** | `OpportunityNumber` (AZ-NNNN) imutável; `OpportunityNumberAllocator` com PBT-05 verde |
| **Mapeia** | Req 8; PBT-05; design §4.3, DD-004; ADR-0003; RNF 2.3 |
| **Camada principal** | Domain |

#### Objetivo

Implementar o objeto de valor `OpportunityNumber` (`AZ-NNNN`, imutável após criação) e o serviço de domínio `OpportunityNumberAllocator`. A alocação preserva números existentes da planilha e gera sequenciais a partir do próximo livre do tenant (≥ 95), sem colisão entre preservados e gerados dentro do mesmo job (PBT-05).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes para: (a) parsing `AZ-0043` válido; (b) formato inválido rejeitado; (c) alocação sequencial a partir de 95 sem colisão; escrever PBT-05 (FsCheck) — propriedade: preservados permanecem inalterados; gerados iniciam no próximo livre (≥95); conjunto final único por tenant, sem colisão.
- [ ] **ST-02 — Green:** implementar `OpportunityNumber.Parse(string?)` e `OpportunityNumber.IsValid(string)`; implementar `OpportunityNumberAllocator.Allocate(IEnumerable<OpportunityNumber> preserved, int nextFreeSequence)` retornando conjunto completo único.
- [ ] **ST-03 — Refactor:** garantir imutabilidade; documentar invariante de unicidade por tenant com referência ao ADR-0003.
- [ ] **ST-04 — Docs:** sem atualização externa.
- [ ] **ST-05 — Encerramento:** PBT-05 verde; commit `feat(data-migration): opportunity-number value object pbt-05`; push.

#### Critérios de Aceite

- [ ] PBT-05 verde: preservados inalterados; gerados ≥ 95; sem colisão no conjunto final.
- [ ] `OpportunityNumber` é imutável após criação.
- [ ] Sequência respeita o próximo livre do tenant (não global).
- [ ] Coverage Domain ≥ 95% acumulado.

---

### TASK-07 — Objetos de valor auxiliares + Policies/Specifications

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `feat/data-migration/07-value-objects-policies` |
| **Worktree** | `git worktree add ../worktrees/data-migration/07-value-objects-policies -b feat/data-migration/07-value-objects-policies` |
| **Status** | [ ] |
| **Depende de** | TASK-03 |
| **Entregável** | `SourceRow`, `ColumnMapping`, `NormalizedName`, `Money`, `TriageFlag` implementados + 5 Policies testadas |
| **Mapeia** | Req 3, Req 5, Req 7; design §4.3, §4.6; `.forge/rules/domain/money-as-cents.md` |
| **Camada principal** | Domain |

#### Objetivo

Implementar os objetos de valor auxiliares do módulo e as Policies/Specifications que encapsulam as regras de mapeamento, dedupe e flags de triagem. Todos imutáveis, com igualdade por valor.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes unitários para: `SourceRow` (igualdade, imutabilidade); `NormalizedName` (trim, comparação insensível a case, ex: "Pag.ai" == "pag.ai"); `Money` (centavos inteiros, rejeição de float); `TriageFlag` (tipo + severidade + referência por índice); `AccountDedupePolicy` (detecta pares com mesmo `NormalizedName`); `StageFallbackPolicy` (etapa vazia → "Lead" com flag não bloqueante); `OwnerTypoMappingPolicy` (corrige "Miilton" → usuário Milton).
- [ ] **ST-02 — Green:** implementar cada objeto de valor e cada policy; `ColumnMapping` como mapa imutável coluna→campo com transformação associada; `PartnerPctPendingPolicy` e `ForecastDivergencePolicy` integradas ao pipeline de dry-run.
- [ ] **ST-03 — Refactor:** garantir que nenhum objeto de valor aceita `null` sem tratamento explícito; extrair constantes de colunas esperadas para `ColumnNames`.
- [ ] **ST-04 — Docs:** sem atualização externa.
- [ ] **ST-05 — Encerramento:** Domain.Tests verdes; coverage Domain ≥ 95% acumulado; commit `feat(data-migration): auxiliary value objects and domain policies`; push.

#### Critérios de Aceite

- [ ] `Money` rejeita `float` e `double`; armazena centavos como `long`.
- [ ] `NormalizedName` usa comparação insensível a case e trim.
- [ ] `AccountDedupePolicy` detecta corretamente pares duplicados.
- [ ] `StageFallbackPolicy` nunca bloqueia o import; parceiros sem percentual também não bloqueiam.
- [ ] `OwnerTypoMappingPolicy` aplica mapa configurável; "Miilton" → Milton corrigido.
- [ ] Coverage Domain ≥ 95% acumulado.

---

### TASK-08 — UploadSpreadsheetCommand + Handler

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/data-migration/08-upload-handler` |
| **Worktree** | `git worktree add ../worktrees/data-migration/08-upload-handler -b feat/data-migration/08-upload-handler` |
| **Status** | [ ] |
| **Depende de** | TASK-07 |
| **Entregável** | `UploadSpreadsheetCommand`/`Handler` com validação de extensão/estrutura e criação de `MigrationJob` em `created` |
| **Mapeia** | Req 1; design §5.1, §5.3; MIG-ERR-001, MIG-ERR-002, MIG-ERR-003 |
| **Camada principal** | Application |

#### Objetivo

Implementar o caso de uso de upload: validar extensão `.xlsx` e estrutura de colunas esperadas sem efeitos colaterais em caso de rejeição; criar `MigrationJob` em estado `created` com metadados do arquivo (nome, tamanho, hash sha256, contagem de linhas detectadas). Não persiste conteúdo de domínio (Req 1.4).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes para: (a) arquivo não-`.xlsx` retorna MIG-ERR-001, sem escrita; (b) colunas ausentes retornam MIG-ERR-002 com lista esperada, sem escrita; (c) arquivo acima do limite retorna MIG-ERR-003; (d) upload válido cria job em `created` com metadados corretos.
- [ ] **ST-02 — Green:** implementar `UploadSpreadsheetCommand` (stream + metadados) e `UploadSpreadsheetHandler`; usar `ISpreadsheetParser` em modo estrutura-only (detecção de colunas); persistir `MigrationJob` via `IMigrationJobRepository`.
- [ ] **ST-03 — Refactor:** extrair `SpreadsheetStructureValidator` para isolar validação de colunas; garantir ausência de efeito colateral em erro.
- [ ] **ST-04 — Docs:** sem atualização externa.
- [ ] **ST-05 — Encerramento:** Application.Tests verdes; coverage Application ≥ 85% acumulado; commit `feat(data-migration): upload-spreadsheet command handler`; push.

#### Critérios de Aceite

- [ ] Arquivo não-`.xlsx` rejeitado com MIG-ERR-001; zero escritas no banco.
- [ ] Colunas ausentes rejeitadas com MIG-ERR-002; lista de colunas esperadas incluída na resposta.
- [ ] Upload válido cria exatamente 1 `MigrationJob` em estado `created`.
- [ ] Metadados (nome, tamanho, hash sha256, contagem de linhas) persistidos corretamente.
- [ ] Conteúdo de domínio (oportunidades, contas) não é persistido no upload.
- [ ] Coverage Application ≥ 85% acumulado.

---

### TASK-09 — RunDryRunCommand + Handler + TriageReport + PBT-04

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/data-migration/09-dry-run-handler` |
| **Worktree** | `git worktree add ../worktrees/data-migration/09-dry-run-handler -b feat/data-migration/09-dry-run-handler` |
| **Status** | [ ] |
| **Depende de** | TASK-08 |
| **Entregável** | `RunDryRunCommand`/`Handler` com simulação rollback-only; `TriageReport` completo; PBT-04 verde |
| **Mapeia** | Req 2; PBT-04; design §5.1, §5.3 (RunDryRunHandler); DD-001 |
| **Camada principal** | Application |

#### Objetivo

Implementar o dry-run como simulação completa do import em transação marcada para rollback incondicional. O handler roda o pipeline de mapeamento + policies + simulação via portas em modo *probe*, coleta contagens, flags, candidatos a dedupe e divergências de forecast, descarta a transação (zero escritas persistem) e grava o `TriageReport` no `MigrationJob`. Emite `DryRunCompleted`.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes para: (a) estado do banco idêntico antes e depois (zero writes); (b) `TriageReport` contém contagens corretas, flags e divergências; (c) PBT-04 — nº de oportunidades no relatório = nº de linhas válidas; nº de contas = nº de `NormalizedName` distintos pós-dedupe; (d) job transita para `dry_run_completed`.
- [ ] **ST-02 — Green:** implementar `RunDryRunCommand`/`Handler`; usar `IUnitOfWork.BeginRollbackOnly()` para a transação de simulação; executar `CanonicalRowMapper` → policies → chamadas probe nas portas; coletar `TriageReport`; persistir relatório no job; transitar estado.
- [ ] **ST-03 — Refactor:** extrair `DryRunSimulator` como serviço de aplicação; garantir que nenhum dado de domínio persiste.
- [ ] **ST-04 — Docs:** sem atualização externa.
- [ ] **ST-05 — Encerramento:** Application.Tests verdes; PBT-04 verde; coverage Application ≥ 85%; commit `feat(data-migration): dry-run handler triage-report pbt-04`; push.

#### Critérios de Aceite

- [ ] Zero linhas escritas em qualquer tabela de domínio durante o dry-run.
- [ ] `TriageReport` inclui: total de oportunidades, distribuição por BU, sem owner (64), sem data, parceiro sem percentual, pares de dedupe, typos, divergências de forecast.
- [ ] PBT-04 verde: conservação de contagem.
- [ ] Job transita para `dry_run_completed` ao final.
- [ ] Coverage Application ≥ 85% acumulado.

---

### TASK-10 — Commands de triagem assistida

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/data-migration/10-triage-commands` |
| **Worktree** | `git worktree add ../worktrees/data-migration/10-triage-commands -b feat/data-migration/10-triage-commands` |
| **Status** | [ ] |
| **Depende de** | TASK-09 |
| **Entregável** | 7 commands de triagem implementados e testados; snapshot JSONB de `TriageResolution` persistido |
| **Mapeia** | Req 4, Req 5, Req 7.2; PBT-07; design §5.1; DD-007 |
| **Camada principal** | Application |

#### Objetivo

Implementar os commands de triagem assistida: `AssignOwnerCommand`, `BulkAssignOwnerCommand`, `ResolveStagePendingCommand`, `ResolvePartnerPctCommand`, `ResolveDedupeCommand`, `SaveTriageProgressCommand` e `MarkReadyToImportCommand`. O progresso é persistido como snapshot JSONB no `MigrationJob` (DD-007), sem PII.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes para: (a) atribuição individual e em massa por BU; (b) `MarkReadyToImportCommand` rejeita com MIG-ERR-006 quando owner faltante; (c) `SaveTriageProgressCommand` persiste snapshot sem PII; (d) triagem retomada a partir do snapshot.
- [ ] **ST-02 — Green:** implementar os 7 handlers; `MarkReadyToImportHandler` aplica `OwnerRequiredSpecification` antes de transitar para `ready_to_import`; `SaveTriageProgressHandler` serializa `TriageResolution` em JSONB (sem PII).
- [ ] **ST-03 — Refactor:** garantir que owners são a única pendência bloqueante; parceiros sem percentual e estágios faltantes são flags não bloqueantes.
- [ ] **ST-04 — Docs:** sem atualização externa.
- [ ] **ST-05 — Encerramento:** Application.Tests verdes; coverage Application ≥ 85%; commit `feat(data-migration): triage-commands handlers`; push.

#### Critérios de Aceite

- [ ] `MarkReadyToImportCommand` rejeita com MIG-ERR-006 enquanto existir owner faltante.
- [ ] Progresso salvo e retomado sem perda de dados entre sessões.
- [ ] `TriageResolution` JSONB não contém PII.
- [ ] Atribuição em massa por BU cobre todas as oportunidades da BU.
- [ ] Import não bloqueado por estágio faltante ou parceiro sem percentual.
- [ ] Coverage Application ≥ 85% acumulado.

---

### TASK-11 — ExecuteImportCommand + Handler (tudo-ou-nada) + PBT-01 + PBT-02

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/data-migration/11-execute-import-handler` |
| **Worktree** | `git worktree add ../worktrees/data-migration/11-execute-import-handler -b feat/data-migration/11-execute-import-handler` |
| **Status** | [ ] |
| **Depende de** | TASK-10 |
| **Entregável** | `ExecuteImportCommand`/`Handler` com import atômico; PBT-01 (atomicidade) e PBT-02 (idempotência) verdes |
| **Mapeia** | Req 6, Req 12; PBT-01, PBT-02; design §5.3 (ExecuteImportHandler); DD-001; RNF 5; MIG-ERR-006, MIG-ERR-007, MIG-ERR-008 |
| **Camada principal** | Application |

#### Objetivo

Implementar o `ExecuteImportHandler` como núcleo da atomicidade (RN-023). Verifica estado `ready_to_import` e `confirmation: true`, aplica `OwnerRequiredSpecification`, abre **uma única transação Postgres** via `IUnitOfWork` e executa em ordem determinística: (1) contas + contatos, (2) parceiros, (3) oportunidades, (4) atividades. Sucesso: `COMMIT` + transição `completed` + `ImportReport` + `ImportCompleted` no Outbox. Falha: `ROLLBACK` total + `rolled_back` + MIG-ERR-007.

#### Subtasks

- [ ] **ST-01 — Red:** escrever PBT-01 (FsCheck) — propriedade: para qualquer conjunto com ≥1 linha que falha, após o import o banco está idêntico ao estado anterior (zero registros de domínio); escrever PBT-02 — propriedade: import bem-sucedido + reexecução do mesmo conjunto mantém contagens por entidade; escrever testes para `confirmation: false` retorna MIG-ERR-008; `execute` fora de `ready_to_import` retorna MIG-ERR-005.
- [ ] **ST-02 — Green:** implementar `ExecuteImportCommand`/`Handler`; ordem determinística de escrita; cada linha gera `MigrationLogEntry`; atividades sem oportunidade correspondente geram `aviso` sem abortar (Req 10.4); em falha: `ROLLBACK` total, `rolled_back`, emite `ImportRolledBack`.
- [ ] **ST-03 — Refactor:** extrair `ImportTransactionService` como serviço de aplicação isolado; garantir que `ImportReport` é gerado apenas em sucesso; congelamento registrado no `ImportReport` (DD-010).
- [ ] **ST-04 — Docs:** sem atualização externa.
- [ ] **ST-05 — Encerramento:** Application.Tests verdes; PBT-01 e PBT-02 verdes; coverage Application ≥ 85%; commit `feat(data-migration): execute-import handler atomic transaction pbt-01 pbt-02`; push.

#### Critérios de Aceite

- [ ] PBT-01 verde: rollback total em qualquer falha; zero registros de domínio persistidos.
- [ ] PBT-02 verde: reexecução do mesmo conjunto triado não duplica registros.
- [ ] `confirmation: false` retorna MIG-ERR-008 sem executar nada.
- [ ] Job fora de `ready_to_import` retorna MIG-ERR-005.
- [ ] Ordem de escrita: accounts → partners → opportunities → activities (preserva FKs).
- [ ] `ImportReport` inclui: contagens por entidade, flags resolvidos, divergências, instrução de congelamento.
- [ ] Coverage Application ≥ 85% acumulado.

---

### TASK-12 — Pipeline Behaviors

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/data-migration/12-pipeline-behaviors` |
| **Worktree** | `git worktree add ../worktrees/data-migration/12-pipeline-behaviors -b feat/data-migration/12-pipeline-behaviors` |
| **Status** | [ ] |
| **Depende de** | TASK-11 |
| **Entregável** | 6 behaviors implementados e testados (TenantContext, Authorization, FeatureFlag, Validation, PiiSafeLogging, Idempotency) |
| **Mapeia** | RNF 2, RNF 3; design §5.4; DD-009; ADR-0001 |
| **Camada principal** | Application |

#### Objetivo

Implementar os pipeline behaviors que envolvem todos os commands/queries: `TenantContextBehavior` (garante `app.current_tenant` setado, falha-fechada); `AuthorizationBehavior` (Platform Operator para upload/dry-run/execute; Tenant Admin para triagem); `FeatureFlagBehavior` (bloqueia `ExecuteImportCommand` quando flag desabilitada); `ValidationBehavior` (FluentValidation); `PiiSafeLoggingBehavior` (proíbe PII em logs do pipeline); `IdempotencyBehavior` (aplica `import_key` em reexecuções).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes para: (a) `TenantContextBehavior` bloqueia quando tenant ausente; (b) `AuthorizationBehavior` rejeita papel errado; (c) `FeatureFlagBehavior` retorna MIG-ERR-009 com flag false; (d) `PiiSafeLoggingBehavior` impede PII nos logs.
- [ ] **ST-02 — Green:** implementar os 6 behaviors; `FeatureFlagBehavior` lê `IFeatureFlags.IsEnabled("migration.import_enabled")`; `IdempotencyBehavior` delega ao `import_key` já presente no command.
- [ ] **ST-03 — Refactor:** garantir que `TenantContextBehavior` executa antes de qualquer outro behavior.
- [ ] **ST-04 — Docs:** sem atualização externa.
- [ ] **ST-05 — Encerramento:** Application.Tests verdes; coverage Application ≥ 85%; commit `feat(data-migration): pipeline behaviors tenant auth feature-flag pii`; push.

#### Critérios de Aceite

- [ ] `TenantContextBehavior` bloqueia e registra log quando `app.current_tenant` ausente.
- [ ] `FeatureFlagBehavior` retorna MIG-ERR-009 quando `migration.import_enabled` = false.
- [ ] `AuthorizationBehavior` garante papel mínimo por tipo de command.
- [ ] `PiiSafeLoggingBehavior` verificado por teste que tenta logar nome/e-mail.
- [ ] Coverage Application ≥ 85% acumulado.

---

### TASK-13 — Queries (GetMigrationJobStatus, GetTriageReport, GetImportReport)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/data-migration/13-queries` |
| **Worktree** | `git worktree add ../worktrees/data-migration/13-queries -b feat/data-migration/13-queries` |
| **Status** | [ ] |
| **Depende de** | TASK-11 |
| **Entregável** | 3 queries implementadas e testadas com DTOs de resposta sem PII |
| **Mapeia** | Req 2, Req 11; design §5.2; RNF 3 |
| **Camada principal** | Application |

#### Objetivo

Implementar as queries de leitura: `GetMigrationJobStatusQuery` (estado, contagens, progresso); `GetTriageReportQuery` (relatório de dry-run com flags e divergências); `GetImportReportQuery` (relatório final para download após `completed`). Todos os DTOs sem PII.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes para: (a) `GetMigrationJobStatus` retorna estado correto e contagens; (b) `GetTriageReport` retorna flags, dedupe, divergências; (c) `GetImportReport` disponível apenas em estado `completed`; (d) nenhum DTO expõe PII.
- [ ] **ST-02 — Green:** implementar os 3 handlers de query; paginação de pendências por tipo de flag em `GetTriageReport` (`?flag=owner_missing&page=1&pageSize=50`).
- [ ] **ST-03 — Refactor:** garantir que `GetImportReport` retorna 404 quando job não está em `completed`.
- [ ] **ST-04 — Docs:** sem atualização externa.
- [ ] **ST-05 — Encerramento:** Application.Tests verdes; coverage Application ≥ 85%; commit `feat(data-migration): migration queries status triage import-report`; push.

#### Critérios de Aceite

- [ ] `GetImportReport` disponível apenas após `completed`.
- [ ] Paginação de pendências funcional em `GetTriageReport`.
- [ ] Zero PII em qualquer DTO de resposta.
- [ ] Coverage Application ≥ 85% acumulado.

---

### TASK-14 — SpreadsheetParser (ClosedXML) + CanonicalRowMapper

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/data-migration/14-spreadsheet-parser` |
| **Worktree** | `git worktree add ../worktrees/data-migration/14-spreadsheet-parser -b feat/data-migration/14-spreadsheet-parser` |
| **Status** | [ ] |
| **Depende de** | TASK-07 |
| **Entregável** | `SpreadsheetParser` (ClosedXML, confinado à Infrastructure) + `CanonicalRowMapper` testados |
| **Mapeia** | Req 1, Req 3, Req 9, Req 10; DD-002, DD-006; design §6.4 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar o adaptador `SpreadsheetParser` usando ClosedXML para leitura das abas "Pipeline" e "Ações Comerciais" do arquivo `.xlsx`. Implementar o `CanonicalRowMapper` que transforma cada `SourceRow` nas estruturas de mapeamento canônico: aplica trim de BU, normalização de nome de conta, conversão de `ExcelSerialDate`, money em centavos, "-" em Parceiro como sem parceiro, título vazio como "Oportunidade — {conta}".

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de integração com arquivo `.xlsx` de fixture (subconjunto da planilha real); testar: parsing das duas abas; conversão de serial Excel para `DateOnly`; money em centavos; trim "Sertão " → "Sertão"; mapeamento "-" em Parceiro.
- [ ] **ST-02 — Green:** implementar `SpreadsheetParser` com ClosedXML; implementar `CanonicalRowMapper` com todas as transformações de Req 3; `ISpreadsheetParser` definida em Application; adaptador em Infrastructure.
- [ ] **ST-03 — Refactor:** garantir que o Domain não referencia ClosedXML (Architecture.Tests valida); extrair constantes de nomes de colunas.
- [ ] **ST-04 — Docs:** sem atualização externa.
- [ ] **ST-05 — Encerramento:** Infrastructure.Tests verdes; Architecture.Tests verdes; coverage Infra ≥ 70%; commit `feat(data-migration): spreadsheet-parser closedxml canonical-row-mapper`; push.

#### Critérios de Aceite

- [ ] Parsing das duas abas ("Pipeline", "Ações Comerciais") funcional.
- [ ] Transformações de Req 3 aplicadas: trim BU, normalização nome, serial Excel → ISO, money em centavos, "-" sem parceiro, título vazio → "Oportunidade — {conta}".
- [ ] Domain não referencia ClosedXML (Architecture.Tests verde).
- [ ] Coverage Infra ≥ 70% acumulado.

---

### TASK-15 — EF Core + migrations (migration_jobs, migration_logs) + RLS (DD-008) + índices

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/data-migration/15-ef-migrations-rls` |
| **Worktree** | `git worktree add ../worktrees/data-migration/15-ef-migrations-rls -b feat/data-migration/15-ef-migrations-rls` |
| **Status** | [ ] |
| **Depende de** | TASK-14 |
| **Entregável** | `MigrationDbContext` com mapeamento EF; migrations criadas; RLS habilitada; índices criados |
| **Mapeia** | RNF 2; design §6.1, §7; DD-008; ADR-0001 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar o `MigrationDbContext` com Global Query Filter por `tenant_id` para `MigrationJob` e `MigrationLogEntry`. Criar migrations EF Core para as tabelas `migration_jobs` e `migration_logs` com todos os campos, constraints (`CHECK` de status), índices e **RLS habilitada com política falha-fechada** (DD-008). Interceptor de conexão seta `app.current_tenant` antes de qualquer comando (ADR-0001).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de integração (Testcontainers/Postgres) para: (a) RLS bloqueia leitura de job de outro tenant; (b) Global Query Filter retorna apenas dados do tenant correto; (c) inserção sem `tenant_id` falha na constraint.
- [ ] **ST-02 — Green:** implementar `MigrationDbContext`, mapeamentos Fluent API, interceptor de conexão; criar migration com tabelas, constraints `CHECK`, índices `idx_migration_logs_job`, `idx_migration_jobs_status`, `uq_migration_log_import_key`; adicionar políticas RLS.
- [ ] **ST-03 — Refactor:** garantir que o interceptor executa `SET app.current_tenant` antes de qualquer `SELECT`/`INSERT`/`UPDATE`.
- [ ] **ST-04 — Docs:** sem atualização externa.
- [ ] **ST-05 — Encerramento:** Infrastructure.Tests verdes; teste de isolamento RLS como gate; coverage Infra ≥ 70%; commit `feat(data-migration): ef-core migrations rls migration-jobs migration-logs`; push.

#### Critérios de Aceite

- [ ] Tabelas `migration_jobs` e `migration_logs` criadas com campos, constraints e índices conforme design §7.
- [ ] RLS habilitada e testada: job de outro tenant é invisível.
- [ ] Global Query Filter por `tenant_id` validado em teste de integração.
- [ ] `uq_migration_log_import_key` (parcial, quando `import_key IS NOT NULL`) criado.
- [ ] Teste de isolamento de tenant como gate de CI (KPI-06).
- [ ] Coverage Infra ≥ 70% acumulado.

---

### TASK-16 — IAccountImportPort + IPartnerImportPort + AccountDedupePolicy (in-process)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/data-migration/16-account-partner-ports` |
| **Worktree** | `git worktree add ../worktrees/data-migration/16-account-partner-ports -b feat/data-migration/16-account-partner-ports` |
| **Status** | [ ] |
| **Depende de** | TASK-15 |
| **Entregável** | Adaptadores in-process de `IAccountImportPort` e `IPartnerImportPort` testados com upsert idempotente |
| **Mapeia** | Req 7; design §6.4; DD-003; RN-014 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar os adaptadores in-process que traduzem as chamadas das portas para os serviços de aplicação de account-management e partner-management, compartilhando o mesmo `IUnitOfWork`. `IAccountImportPort.CreateOrGet(NormalizedName)` faz upsert idempotente por `import_key` e respeita a `AccountDedupePolicy` (RN-014). `IPartnerImportPort.CreateOrGet` não importa percentuais.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de integração para: (a) criação de conta nova; (b) recuperação de conta existente por `NormalizedName` (dedupe); (c) PII de contato não logada; (d) upsert idempotente por `import_key`; (e) criação de parceiro sem percentual.
- [ ] **ST-02 — Green:** implementar `AccountImportAdapter` e `PartnerImportAdapter`; vincular contato principal à conta (Req 7.3); parceiro sem percentual registrado sem bloqueio.
- [ ] **ST-03 — Refactor:** garantir que `AccountImportAdapter` respeita `RN-014` (alerta, não bloqueio no dedupe); contato vinculado como contato principal.
- [ ] **ST-04 — Docs:** sem atualização externa.
- [ ] **ST-05 — Encerramento:** Infrastructure.Tests verdes; coverage Infra ≥ 70%; commit `feat(data-migration): account-partner import adapters dedupe`; push.

#### Critérios de Aceite

- [ ] Dedupe por `NormalizedName`: empresas com mesmo nome normalizado retornam a mesma conta.
- [ ] PII de contato (nome/e-mail/telefone) nunca registrada em `migration_logs`.
- [ ] Parceiro criado sem percentual; sem bloqueio do import.
- [ ] Upsert idempotente por `import_key` validado em reexecução.
- [ ] Coverage Infra ≥ 70% acumulado.

---

### TASK-17 — IOpportunityImportPort + IOpportunityNumberPort (in-process, ADR-0003)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/data-migration/17-opportunity-ports` |
| **Worktree** | `git worktree add ../worktrees/data-migration/17-opportunity-ports -b feat/data-migration/17-opportunity-ports` |
| **Status** | [ ] |
| **Depende de** | TASK-16 |
| **Entregável** | Adaptadores in-process de `IOpportunityImportPort` e `IOpportunityNumberPort` testados |
| **Mapeia** | Req 8, Req 12; design §6.4; DD-004; ADR-0003; RN-001; RN-002 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar os adaptadores in-process para opportunity-pipeline. `IOpportunityNumberPort.AllocateNext(tenantId)` delega ao serviço de numeração atômica do Pipeline (fonte única da verdade, ADR-0003), reservando o próximo livre (≥ 95). `IOpportunityImportPort.Create` cria a oportunidade com owner obrigatório (RN-002), número preservado ou alocado, valores em centavos e data ISO.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de integração para: (a) número preservado da planilha mantido inalterado; (b) número gerado respeita sequência ≥ 95 do tenant; (c) colisão entre preservado e gerado impossível; (d) owner ausente lança exceção de domínio; (e) reexecução não duplica oportunidade (upsert por `import_key`).
- [ ] **ST-02 — Green:** implementar `OpportunityNumberAdapter` e `OpportunityImportAdapter`; `AllocateNext` usa sequência atômica do tenant; `Create` valida `owner != null` (RN-002).
- [ ] **ST-03 — Refactor:** garantir que `uq_opportunity_number_per_tenant` (ADR-0003) é respeitado; documentar dependência do porta no serviço do Pipeline.
- [ ] **ST-04 — Docs:** sem atualização externa.
- [ ] **ST-05 — Encerramento:** Infrastructure.Tests verdes; coverage Infra ≥ 70%; commit `feat(data-migration): opportunity import port number allocation adr-0003`; push.

#### Critérios de Aceite

- [ ] Números preservados mantidos; gerados ≥ 95; sem colisão.
- [ ] Owner ausente rejeitado antes de qualquer escrita.
- [ ] Valores monetários em centavos (`long`); sem `float`/`double`.
- [ ] Upsert idempotente por `import_key` validado.
- [ ] Coverage Infra ≥ 70% acumulado.

---

### TASK-18 — IActivityImportPort + IOrganizationReadPort + UnitOfWork compartilhado (DD-001)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/data-migration/18-activity-org-uow` |
| **Worktree** | `git worktree add ../worktrees/data-migration/18-activity-org-uow -b feat/data-migration/18-activity-org-uow` |
| **Status** | [ ] |
| **Depende de** | TASK-17 |
| **Entregável** | `IActivityImportPort`, `IOrganizationReadPort` e `IUnitOfWork` compartilhado testados; DD-001 validado |
| **Mapeia** | Req 10, Req 6; design §6.4, §6.1; DD-001; RNF 5 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar `IActivityImportPort` (cria atividade vinculada por `opportunity_id` resolvido de `opportunity_number`, corrige typos de owner) e `IOrganizationReadPort` (lê `bu_id` por nome de BU e `stage_id` por etapa — DEP-04). Implementar `IUnitOfWork` compartilhado que garante **uma única transação Postgres** abrangendo todos os adaptadores de porta (DD-001 — import in-process, não HTTP).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de integração para: (a) atividade vinculada por `opportunity_number` resolvido; (b) atividade com número não encontrado gera `aviso` sem abortar; (c) typo "Miilton" → Milton corrigido via `OwnerTypoMappingPolicy`; (d) `IUnitOfWork` abrange todos os adaptadores na mesma transação física (rollback total em falha); (e) `IOrganizationReadPort` retorna MIG-ERR-010 quando BU não configurada.
- [ ] **ST-02 — Green:** implementar `ActivityImportAdapter`, `OrganizationReadAdapter`; implementar `SharedUnitOfWork` que compartilha `DbContext`/transação entre todos os adaptadores; validar DEP-04 antes do import.
- [ ] **ST-03 — Refactor:** garantir que o rollback da transação única abrange também as escritas nos módulos-alvo; documentar DD-001 no código.
- [ ] **ST-04 — Docs:** sem atualização externa.
- [ ] **ST-05 — Encerramento:** Infrastructure.Tests verdes; PBT-01 validado end-to-end (rollback cobre todos os módulos-alvo); coverage Infra ≥ 70%; commit `feat(data-migration): activity org ports shared unit-of-work dd-001`; push.

#### Critérios de Aceite

- [ ] Atividade vinculada por `opportunity_number`; FK real resolvida.
- [ ] Número não encontrado gera `migration_log` com status `aviso`; transação não abortada.
- [ ] `IUnitOfWork` compartilhado: rollback em falha desfaz todas as escritas (contas, parceiros, oportunidades, atividades).
- [ ] `IOrganizationReadPort` retorna MIG-ERR-010 quando BU ausente (DEP-04).
- [ ] Coverage Infra ≥ 70% acumulado.

---

### TASK-19 — Idempotência por import_key + upsert idempotente nos adaptadores (DD-003)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/data-migration/19-idempotency-import-key` |
| **Worktree** | `git worktree add ../worktrees/data-migration/19-idempotency-import-key -b feat/data-migration/19-idempotency-import-key` |
| **Status** | [ ] |
| **Depende de** | TASK-18 |
| **Entregável** | Geração de `import_key` determinística e upsert idempotente validados em reexecução pós-`rolled_back` |
| **Mapeia** | Req 12; PBT-02; design §6.5; DD-003; RNF 5 |
| **Camada principal** | Infrastructure |

#### Objetivo

Garantir que a reexecução de um job em `rolled_back` com o mesmo conjunto triado não duplica dados. A `import_key` é calculada como `hash(tenant_id, source_sheet, source_row_index, normalized_payload)` para cada linha de origem. Os adaptadores fazem upsert idempotente por essa chave. O `IdempotencyBehavior` (TASK-12) delega ao `import_key` presente em cada `MigrationLogEntry`.

#### Subtasks

- [ ] **ST-01 — Red:** escrever PBT-02 end-to-end — propriedade: import bem-sucedido + reexecução completa do mesmo conjunto triado mantém exatamente as mesmas contagens por entidade; escrever teste para `uq_migration_log_import_key` rejeitar duplicata.
- [ ] **ST-02 — Green:** implementar `ImportKeyCalculator.Calculate(tenantId, sourceSheet, sourceRowIndex, normalizedPayload)` usando SHA-256; integrar geração de `import_key` no pipeline de import; garantir upsert por `import_key` em todos os adaptadores.
- [ ] **ST-03 — Refactor:** garantir que números preservados não são realocados em reexecução (Req 12.3).
- [ ] **ST-04 — Docs:** sem atualização externa.
- [ ] **ST-05 — Encerramento:** PBT-02 end-to-end verde; Infrastructure.Tests verdes; coverage Infra ≥ 70%; commit `feat(data-migration): import-key idempotency dd-003 pbt-02`; push.

#### Critérios de Aceite

- [ ] PBT-02 verde: reexecução não duplica nenhuma entidade.
- [ ] `uq_migration_log_import_key` rejeita duplicata de `import_key` ativo.
- [ ] Números preservados mantidos em reexecução.
- [ ] SHA-256 determinístico: mesma linha + mesmo tenant → mesma chave.
- [ ] Coverage Infra ≥ 70% acumulado.

---

### TASK-20 — Outbox (ImportCompleted) + eventos de domínio

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/data-migration/20-outbox-events` |
| **Worktree** | `git worktree add ../worktrees/data-migration/20-outbox-events -b feat/data-migration/20-outbox-events` |
| **Status** | [ ] |
| **Depende de** | TASK-19 |
| **Entregável** | `ImportCompleted` publicado via Outbox na mesma transação do import; `DryRunCompleted` e `ImportRolledBack` emitidos |
| **Mapeia** | Req 11; RNF 4; design §6.3, §6.6, §9; `.forge/rules/domain/audit-immutability.md` |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar a publicação de `ImportCompleted` via padrão Outbox (tabela `outbox_events` gravada na mesma transação do import → relay → Cloud Pub/Sub → audit-log). Garantir que o registro de auditoria seja append-only (RNF 4.3). Emitir `DryRunCompleted` e `ImportRolledBack` como eventos de domínio/observabilidade. Nenhum payload contém PII.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de integração para: (a) `ImportCompleted` gravado em `outbox_events` na mesma transação do import; (b) rollback do import descarta também o evento do Outbox; (c) payload de `ImportCompleted` sem PII; (d) `DryRunCompleted` emitido após dry-run.
- [ ] **ST-02 — Green:** implementar `OutboxPublisher` que grava em `outbox_events` com `correlation_id`/`causation_id`; integrar emissão no `ExecuteImportHandler` (commit) e no `RunDryRunHandler`; implementar `ImportRolledBack` no caminho de falha.
- [ ] **ST-03 — Refactor:** garantir que `outbox_events` é parte da transação única (DD-001); sem dual-write.
- [ ] **ST-04 — Docs:** sem atualização externa.
- [ ] **ST-05 — Encerramento:** Infrastructure.Tests verdes; coverage Infra ≥ 70%; commit `feat(data-migration): outbox import-completed domain-events audit-append-only`; push.

#### Critérios de Aceite

- [ ] `ImportCompleted` gravado no Outbox na mesma transação; rollback descarta o evento junto.
- [ ] Payload de todos os eventos sem PII; `tenantId` presente.
- [ ] `DryRunCompleted` emitido após dry-run bem-sucedido.
- [ ] `ImportRolledBack` emitido após falha com rollback.
- [ ] Registro de auditoria append-only: sem UPDATE/DELETE pós-emissão.
- [ ] Coverage Infra ≥ 70% acumulado.

---

### TASK-21 — MigrationController — upload + dry-run + status (MIG-ERR-001..004)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contratos |
| **Branch** | `feat/data-migration/21-api-upload-dryrun` |
| **Worktree** | `git worktree add ../worktrees/data-migration/21-api-upload-dryrun -b feat/data-migration/21-api-upload-dryrun` |
| **Status** | [ ] |
| **Depende de** | TASK-13, TASK-15 |
| **Entregável** | 3 endpoints REST com testes de contrato e catálogo de erros MIG-ERR-001..004 |
| **Mapeia** | Req 1, Req 2; design §8; MIG-ERR-001, MIG-ERR-002, MIG-ERR-003, MIG-ERR-004 |
| **Camada principal** | Api |

#### Objetivo

Implementar os endpoints REST de upload, dry-run e status no `MigrationController`: `POST /api/v1/migrations/upload` (multipart, autenticação JWT, PlatOp); `POST /api/v1/migrations/{jobId}/dry-run` (PlatOp); `GET /api/v1/migrations/{jobId}/status` (PlatOp, TenantAdmin). Todos referenciando o catálogo de erros.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de contrato para: (a) upload sem autenticação retorna 401; (b) upload de arquivo inválido retorna 422 + MIG-ERR-001; (c) colunas ausentes retornam 422 + MIG-ERR-002; (d) `jobId` inexistente retorna 404 + MIG-ERR-004; (e) dry-run retorna `TriageReport` correto.
- [ ] **ST-02 — Green:** implementar os 3 endpoints com model binding (multipart para upload), middleware de autenticação JWT, resolução de tenant por slug; delegar para commands/queries via mediator.
- [ ] **ST-03 — Refactor:** garantir que nenhuma mensagem de erro expõe PII; aplicar `ProblemDetails` padrão para erros.
- [ ] **ST-04 — Docs:** sem atualização externa.
- [ ] **ST-05 — Encerramento:** Api.Tests verdes; coverage Api ≥ 80%; commit `feat(data-migration): api upload dry-run status endpoints`; push.

#### Critérios de Aceite

- [ ] Upload aceita apenas `multipart/form-data` com arquivo `.xlsx`.
- [ ] Erros MIG-ERR-001..004 retornam HTTP status correto (422, 422, 413, 404).
- [ ] Tenant resolvido por slug no header JWT.
- [ ] Zero PII em mensagens de erro.
- [ ] Coverage Api ≥ 80% acumulado.

---

### TASK-22 — MigrationController — endpoints de triagem

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contratos |
| **Branch** | `feat/data-migration/22-api-triage` |
| **Worktree** | `git worktree add ../worktrees/data-migration/22-api-triage -b feat/data-migration/22-api-triage` |
| **Status** | [ ] |
| **Depende de** | TASK-21 |
| **Entregável** | 6 endpoints de triagem com autorização TenantAdmin e paginação de pendências |
| **Mapeia** | Req 4, Req 5, Req 7.2; design §8 |
| **Camada principal** | Api |

#### Objetivo

Implementar os endpoints de triagem assistida no `MigrationController`: `GET /triage` (relatório + pendências paginadas por flag); `POST /triage/owners` (atribuição individual/massa); `POST /triage/stages`; `POST /triage/partners`; `POST /triage/dedupe`; `POST /ready`. Todos exigem papel TenantAdmin.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de contrato para: (a) PlatOp sem papel TenantAdmin retorna 403 em `/triage/owners`; (b) paginação `?flag=owner_missing&page=1&pageSize=50` retorna subset correto; (c) `POST /ready` com owner faltante retorna 409 + MIG-ERR-006.
- [ ] **ST-02 — Green:** implementar os 6 endpoints; paginação de pendências via query string; body de atribuição em massa inclui `bu_id` e `ownerId`.
- [ ] **ST-03 — Refactor:** garantir que `/ready` só aceita job em `triage_in_progress` ou `ready_to_import`.
- [ ] **ST-04 — Docs:** sem atualização externa.
- [ ] **ST-05 — Encerramento:** Api.Tests verdes; coverage Api ≥ 80%; commit `feat(data-migration): api triage endpoints owner-assign dedupe ready`; push.

#### Critérios de Aceite

- [ ] Endpoints de triagem exigem papel TenantAdmin; PlatOp sem esse papel recebe 403.
- [ ] Paginação de pendências funcional e testada.
- [ ] `POST /ready` retorna 409 + MIG-ERR-006 quando owner faltante.
- [ ] Coverage Api ≥ 80% acumulado.

---

### TASK-23 — MigrationController — execute + confirmação explícita + catálogo MIG-ERR-005..010

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contratos |
| **Branch** | `feat/data-migration/23-api-execute` |
| **Worktree** | `git worktree add ../worktrees/data-migration/23-api-execute -b feat/data-migration/23-api-execute` |
| **Status** | [ ] |
| **Depende de** | TASK-22 |
| **Entregável** | `POST /execute` com confirmação explícita, feature flag e relatório final; catálogo de erros completo |
| **Mapeia** | Req 6, Req 11; design §8; MIG-ERR-005..010; DD-009 |
| **Camada principal** | Api |

#### Objetivo

Implementar o endpoint `POST /api/v1/migrations/{jobId}/execute` (PlatOp, `confirmation: true`, `idempotencyKey` opcional) e `GET /api/v1/migrations/{jobId}/report` (TenantAdmin). Validar todos os erros MIG-ERR-005..010 com HTTP status corretos. `migration.import_enabled = false` retorna MIG-ERR-009.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de contrato para: (a) `confirmation: false` retorna 400 + MIG-ERR-008; (b) feature flag false retorna 403 + MIG-ERR-009; (c) job fora de `ready_to_import` retorna 409 + MIG-ERR-005; (d) rollback retorna 409 + MIG-ERR-007; (e) sucesso retorna 200 com contagens; (f) `/report` retorna 404 antes de `completed`.
- [ ] **ST-02 — Green:** implementar `/execute` e `/report`; integrar `FeatureFlagBehavior` via behavior pipeline; mapear todos os erros de domínio para os códigos MIG-ERR-005..010.
- [ ] **ST-03 — Refactor:** garantir que a resposta 200 de sucesso inclui `freezeInstruction` e contagens por entidade.
- [ ] **ST-04 — Docs:** sem atualização externa.
- [ ] **ST-05 — Encerramento:** Api.Tests verdes; coverage Api ≥ 80%; commit `feat(data-migration): api execute report error-catalog mig-err-005-010`; push.

#### Critérios de Aceite

- [ ] Todos os 10 erros MIG-ERR-001..010 com HTTP status correto e mensagem sem PII.
- [ ] `confirmation: false` bloqueia sem executar.
- [ ] Feature flag false retorna 403 + MIG-ERR-009.
- [ ] Resposta 200 de sucesso inclui contagens por entidade e instrução de congelamento.
- [ ] `/report` disponível apenas em `completed`; 404 antes disso.
- [ ] Coverage Api ≥ 80% acumulado.

---

### TASK-24 — PiiSafeLogger + PiiSafeLoggingBehavior (RNF 3, LGPD)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `feat/data-migration/24-pii-safe-logging` |
| **Worktree** | `git worktree add ../worktrees/data-migration/24-pii-safe-logging -b feat/data-migration/24-pii-safe-logging` |
| **Status** | [ ] |
| **Depende de** | TASK-23 |
| **Entregável** | `PiiSafeLogger` e `PiiSafeLoggingBehavior` validados; teste automático confirma ausência de PII em logs |
| **Mapeia** | RNF 3; design §5.4, §10; RN-025; RISK-MIGR-03 |
| **Camada principal** | Application |

#### Objetivo

Implementar `PiiSafeLogger` (wrapper de logger estruturado que rejeita campos marcados como PII) e `PiiSafeLoggingBehavior` (behavior de pipeline que inspeciona o log antes da emissão). Garantir que nome, e-mail e telefone de contatos jamais apareçam em `migration_logs`, mensagens de erro ou payloads de evento. Referência sempre por índice de linha.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes para: (a) log que tenta registrar nome de contato é bloqueado/mascarado; (b) `migration_logs` só contém índice, status e mensagem técnica; (c) mensagem MIG-ERR-007 referencia linha por índice, não por nome.
- [ ] **ST-02 — Green:** implementar `PiiSafeLogger` com lista de campos proibidos (`contactName`, `email`, `phone`); implementar `PiiSafeLoggingBehavior` que intercepta logs do pipeline; configurar no `MigrationLogEntry`.
- [ ] **ST-03 — Refactor:** garantir que `triage_report` e `triage_resolution` JSONB não contêm PII.
- [ ] **ST-04 — Docs:** sem atualização externa.
- [ ] **ST-05 — Encerramento:** Application.Tests verdes; commit `feat(data-migration): pii-safe-logger logging-behavior rnf-3 lgpd`; push.

#### Critérios de Aceite

- [ ] Teste automático tenta logar PII; `PiiSafeLogger` bloqueia ou mascara.
- [ ] `migration_logs` validado: zero campos com nome/e-mail/telefone.
- [ ] Payloads de eventos sem PII verificados.
- [ ] Mensagens de erro referenciam linha por índice.

---

### TASK-25 — Métricas + logs estruturados + traces (RNF 6)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `feat/data-migration/25-observability` |
| **Worktree** | `git worktree add ../worktrees/data-migration/25-observability -b feat/data-migration/25-observability` |
| **Status** | [ ] |
| **Depende de** | TASK-24 |
| **Entregável** | Métricas `migration_rows_*` + `migration_duration_seconds` expostas; logs estruturados por linha; spans de trace |
| **Mapeia** | RNF 6; design §11; `.forge/rules/architecture/observability.md` |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar a observabilidade do processo de migração: métricas `migration_rows_processed_total`, `migration_rows_failed_total`, `migration_duration_seconds` (histograma), `migration_forecast_divergences_total` e `migration_jobs_state_total{state}`; logs estruturados por linha (`migration_job_id`, `source_sheet`, `source_row_index`, `status`, `message`, `correlation_id`; sem PII); span de trace por etapa (parse, dry-run, import) com `correlation_id`.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes para: (a) `migration_duration_seconds` registrado com `started_at`/`finished_at` do job; (b) `migration_rows_processed_total` incrementado por linha; (c) `migration_rows_failed_total` incrementado em erro; (d) log por linha contém campos obrigatórios sem PII.
- [ ] **ST-02 — Green:** implementar instrumentação OpenTelemetry (métricas + traces); registrar métricas no `ExecuteImportHandler`; logs estruturados usando `ILogger` no pipeline; notificação ao PlatOp via log consolidado ao final (RNF 6.3).
- [ ] **ST-03 — Refactor:** garantir que `migration_duration_seconds` usa `started_at`/`finished_at` da `migration_jobs` (RNF 1.2).
- [ ] **ST-04 — Docs:** sem atualização externa.
- [ ] **ST-05 — Encerramento:** Infrastructure.Tests verdes; commit `feat(data-migration): observability metrics traces structured-logs rnf-6`; push.

#### Critérios de Aceite

- [ ] `migration_rows_processed_total`, `migration_rows_failed_total`, `migration_duration_seconds` expostos.
- [ ] Log estruturado por linha com campos obrigatórios; sem PII.
- [ ] Span de trace por etapa com `correlation_id`.
- [ ] `migration_duration_seconds` mede `finished_at - started_at` do job.
- [ ] Notificação consolidada ao PlatOp via log ao final da execução.

---

### TASK-26 — Teste de staging — dry-run e rollback com 108 e 500 linhas (RNF 1.3, RNF 5.2)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `test/data-migration/26-staging-volume-tests` |
| **Worktree** | `git worktree add ../worktrees/data-migration/26-staging-volume-tests -b test/data-migration/26-staging-volume-tests` |
| **Status** | [ ] |
| **Depende de** | TASK-25 |
| **Entregável** | Evidência de dry-run e rollback total bem-sucedidos com 108 e 500 linhas em staging; SLO ≤ 5 min validado |
| **Mapeia** | RNF 1, RNF 5; design §13, §15; RISK-MIGR-02 |
| **Camada principal** | Tests |

#### Objetivo

Executar testes de volume em ambiente de staging com os volumes reais da Vellus (108 oportunidades) e com volume estendido (500 linhas de fixture). Validar: (a) dry-run conclui em ≤ 5 min; (b) import transacional conclui em ≤ 5 min; (c) rollback total funciona e desfaz todos os registros de domínio; (d) reexecução após rollback não duplica (PBT-02 em staging).

#### Subtasks

- [ ] **ST-01 — Red:** configurar fixtures de 108 e 500 linhas para ambiente de staging; escrever script de validação de SLO medindo `started_at`/`finished_at`.
- [ ] **ST-02 — Green:** executar dry-run com 108 linhas em staging; medir duração; executar import; verificar contagens; executar rollback forçado e confirmar zero registros persistidos; repetir com 500 linhas.
- [ ] **ST-03 — Refactor:** documentar resultados de tempo em `staging-results.md` (na branch); registrar evidências de rollback.
- [ ] **ST-04 — Docs:** registrar resultados como evidência do DoD (design §19).
- [ ] **ST-05 — Encerramento:** evidências de SLO ≤ 5 min e rollback total documentadas; commit `test(data-migration): staging volume tests 108 500 lines rnf-1-5`; push.

#### Critérios de Aceite

- [ ] Dry-run de 108 linhas conclui em ≤ 5 minutos em staging.
- [ ] Import de 500 linhas conclui em ≤ 5 minutos em staging.
- [ ] Rollback total testado: zero registros de domínio persistidos após falha forçada.
- [ ] Reexecução após rollback: contagens idênticas à primeira execução bem-sucedida (PBT-02 em staging).
- [ ] Evidências documentadas e rastreáveis.

---

### TASK-27 — Feature flag migration.import_enabled + plano de remoção pós-Fase 1 (DD-009, Req 14)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `feat/data-migration/27-feature-flag-lifecycle` |
| **Worktree** | `git worktree add ../worktrees/data-migration/27-feature-flag-lifecycle -b feat/data-migration/27-feature-flag-lifecycle` |
| **Status** | [ ] |
| **Depende de** | TASK-23 |
| **Entregável** | Feature flag `migration.import_enabled` operacional; checklist de remoção pós-Fase 1 registrado |
| **Mapeia** | Req 14; DD-009; VAL-MIGR-01; design §2, §10 |
| **Camada principal** | Application |

#### Objetivo

Garantir que o endpoint de import é protegido pela flag `migration.import_enabled` (via `FeatureFlagBehavior`). Registrar formalmente o plano de remoção do módulo após a Fase 1: desligar a flag, remover migrations de rollback, arquivar as tabelas `migration_jobs`/`migration_logs`, confirmar ausência de dependências reversas nos módulos de domínio (Req 14.2).

#### Subtasks

- [ ] **ST-01 — Red:** escrever teste que confirma `ExecuteImportCommand` bloqueado com MIG-ERR-009 quando flag = false; escrever teste que confirma que módulos de domínio (accounts, opportunities) não referenciam `data-migration` (teste de arquitetura reverso).
- [ ] **ST-02 — Green:** configurar `IFeatureFlags` com `migration.import_enabled`; integrar `FeatureFlagBehavior` na cadeia de pipeline; redigir checklist de remoção pós-Fase 1 em `docs/product/modules/data-migration/lifecycle-removal.md`.
- [ ] **ST-03 — Refactor:** garantir que desligar a flag torna o endpoint inoperante sem remover o código (primeira etapa do ciclo de remoção).
- [ ] **ST-04 — Docs:** redigir `lifecycle-removal.md` com itens: (1) desligar flag; (2) confirmar zero consumidores do módulo; (3) executar migration de drop das tabelas `migration_*`; (4) remover projetos do `DataMigration.*`.
- [ ] **ST-05 — Encerramento:** testes verdes; commit `feat(data-migration): feature-flag import-enabled lifecycle-removal dd-009 req-14`; push.

#### Critérios de Aceite

- [ ] `migration.import_enabled = false` retorna MIG-ERR-009 em qualquer tentativa de execute.
- [ ] Teste de arquitetura confirma zero dependências reversas dos módulos de domínio sobre `data-migration`.
- [ ] Checklist de remoção pós-Fase 1 documentado e rastreável (VAL-MIGR-01).
- [ ] Flag operacional em staging antes do go-live.

---

### TASK-28 — DoD final — README corrigido (DD-001), rastreabilidade sincronizada, VAL-MIGR-01 registrado

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `docs/data-migration/28-dod-final` |
| **Worktree** | `git worktree add ../worktrees/data-migration/28-dod-final -b docs/data-migration/28-dod-final` |
| **Status** | [ ] |
| **Depende de** | TASK-27 |
| **Entregável** | README corrigido (DD-001); matriz de rastreabilidade completa; DoD de design §19 verificado item a item |
| **Mapeia** | Req 14, todos os requisitos; design §19; VAL-MIGR-01; DD-001 |
| **Camada principal** | Docs |

#### Objetivo

Completar o DoD do módulo conforme design §19: verificar cada item, corrigir o README do módulo quanto à fronteira in-process (DD-001 — não HTTP), sincronizar a matriz de rastreabilidade deste `tasks.md`, registrar VAL-MIGR-01 (decisão de remoção pós-Fase 1) e garantir consistência entre `requirements.md`, `design.md` e `tasks.md`.

#### Subtasks

- [ ] **ST-01 — Red:** verificar a lista completa do DoD de design §19; identificar itens pendentes; anotar o que falta.
- [ ] **ST-02 — Green:** corrigir README do módulo (seção de integrações: "API HTTP interna" → "in-process via porta", conforme DD-001); atualizar status do módulo; sincronizar seção 21 do README com ondas e TASKs concluídas.
- [ ] **ST-03 — Refactor:** verificar consistência completa entre `requirements.md`, `design.md` e `tasks.md`; confirmar que todas as Reqs, RNFs e PBTs têm TASK correspondente.
- [ ] **ST-04 — Docs:** registrar VAL-MIGR-01 em `docs/product/modules/data-migration/README.md §20` com data de decisão e critérios de remoção.
- [ ] **ST-05 — Encerramento:** DoD verificado; commit `docs(data-migration): dod-final readme corrected traceability val-migr-01`; push.

#### Critérios de Aceite

- [ ] README do módulo corrigido: fronteira in-process (não HTTP) conforme DD-001.
- [ ] Todos os 14 requisitos funcionais e 6 RNFs têm contraparte TASK na matriz de rastreabilidade.
- [ ] Todos os 7 PBTs têm TASK correspondente e estão verdes em CI.
- [ ] VAL-MIGR-01 registrado com critérios de remoção pós-Fase 1.
- [ ] `requirements.md`, `design.md` e `tasks.md` consistentes e sem contradições.

---

## 5. Matriz de Rastreabilidade

| Origem | Descrição | TASKs | Status |
|--------|-----------|-------|--------|
| Req 1 | Upload da planilha Pipeline Vellus.xlsx | TASK-08, TASK-14, TASK-21 | [ ] |
| Req 2 | Dry-run sem efeitos colaterais com relatório de triagem | TASK-09, TASK-13, TASK-21 | [ ] |
| Req 3 | Mapeamento canônico das colunas da planilha | TASK-07, TASK-14 | [ ] |
| Req 4 | Triagem obrigatória de owners faltantes | TASK-03, TASK-10, TASK-22 | [ ] |
| Req 5 | Triagem de estágios faltantes e parceiros sem percentual | TASK-07, TASK-10, TASK-22 | [ ] |
| Req 6 | Import transacional tudo ou nada com rollback total | TASK-11, TASK-18, TASK-23 | [ ] |
| Req 7 | Criação de contas com dedupe e contatos vinculados | TASK-07, TASK-09, TASK-16 | [ ] |
| Req 8 | Preservação e geração de número AZ-NNNN | TASK-06, TASK-17 | [ ] |
| Req 9 | Transformação de datas serial Excel para ISO | TASK-04, TASK-14 | [ ] |
| Req 10 | Import de atividades da aba Ações Comerciais | TASK-18, TASK-14 | [ ] |
| Req 11 | Relatório final auditado e evento de conclusão | TASK-11, TASK-13, TASK-20, TASK-23 | [ ] |
| Req 12 | Idempotência do import | TASK-11, TASK-19 | [ ] |
| Req 13 | Congelamento da planilha de origem | TASK-11, TASK-23 | [ ] |
| Req 14 | Ciclo de vida temporário do módulo | TASK-01, TASK-27, TASK-28 | [ ] |
| RNF 1 | Desempenho do dry-run e do import (≤ 5 min) | TASK-25, TASK-26 | [ ] |
| RNF 2 | Isolamento por tenant (RLS, tenant_id) | TASK-12, TASK-15 | [ ] |
| RNF 3 | PII ausente dos logs de migração | TASK-24, TASK-28 | [ ] |
| RNF 4 | Auditoria append-only do import | TASK-20, TASK-28 | [ ] |
| RNF 5 | Resiliência e atomicidade da transação | TASK-11, TASK-18, TASK-19, TASK-26 | [ ] |
| RNF 6 | Observabilidade do processo | TASK-25 | [ ] |
| PBT-01 | Atomicidade do import (rollback total) | TASK-11 | [ ] |
| PBT-02 | Idempotência da reexecução | TASK-11, TASK-19 | [ ] |
| PBT-03 | Round-trip de datas serial Excel ↔ ISO | TASK-04 | [ ] |
| PBT-04 | Conservação de contagem no import | TASK-09 | [ ] |
| PBT-05 | Preservação e unicidade do número AZ-NNNN | TASK-06 | [ ] |
| PBT-06 | Detecção de divergência de forecast recalculado | TASK-05 | [ ] |
| PBT-07 | Invariante de owner obrigatório (state machine) | TASK-03, TASK-10 | [ ] |
| DD-001 | Import in-process com transação única, não HTTP | TASK-18, TASK-28 | [ ] |
| DD-002 | Biblioteca de parsing .xlsx: ClosedXML | TASK-14 | [ ] |
| DD-003 | Idempotência por import_key determinística | TASK-19 | [ ] |
| DD-004 | Alocação de número AZ-NNNN via IOpportunityNumberPort | TASK-06, TASK-17 | [ ] |
| DD-005 | Forecast com arredondamento bancário NBR 5891 | TASK-05 | [ ] |
| DD-006 | Conversão de datas com epoch Excel 1899-12-30 | TASK-04, TASK-14 | [ ] |
| DD-007 | Triagem persistida como snapshot JSONB | TASK-10 | [ ] |
| DD-008 | RLS obrigatória nas tabelas de migração | TASK-15 | [ ] |
| DD-009 | Ciclo de vida temporário via feature flag | TASK-12, TASK-27 | [ ] |
| DD-010 | Congelamento como instrução operacional | TASK-11, TASK-23 | [ ] |
| ADR-0001 | Isolamento multi-tenant (RLS, EF Filter) | TASK-12, TASK-15 | [ ] |
| ADR-0003 | Unicidade de opportunity_number por tenant | TASK-06, TASK-17 | [ ] |
| VAL-MIGR-01 | Remoção do módulo após Fase 1 | TASK-27, TASK-28 | [ ] |

## 6. Coverage Gates

| Camada | Gate | Tipo de teste esperado |
|--------|------|------------------------|
| Domain | ≥ 95% | Unitários + PBTs (PBT-03, 05, 06, 07); state machine; objetos de valor |
| Application | ≥ 85% | Unitários de handlers, behaviors, validações, queries; PBT-01, 02, 04 |
| Infrastructure | ≥ 70% | Integração com Postgres (Testcontainers); RLS; SpreadsheetParser; adaptadores de porta; idempotência |
| Api | ≥ 80% | Contratos REST; autorização; catálogo de erros; confirmação explícita |
| Architecture | 100% das regras críticas | NetArchTest: dependências entre camadas; Domain sem infraestrutura; ClosedXML confinado |
| Security | Cobertura por cenário crítico | RLS por tenant (gate de merge, KPI-06); PII ausente de logs; anti-enumeração de jobId |
| Observability | Cobertura por fluxo crítico | Métricas registradas; log estruturado sem PII; span de trace por etapa |

## 7. Critérios de Encerramento

### 7.1 Encerramento de TASK

Uma TASK só pode ser marcada como `[X]` quando:

- todas as subtasks concluídas
- testes aplicáveis verdes
- coverage gate da camada atendido ou justificativa registrada
- `dotnet format` executado sem novos warnings
- commit em Conventional Commits realizado
- push realizado
- documentação atualizada quando aplicável

### 7.2 Encerramento de Onda

Uma onda só pode ser considerada concluída quando:

- todas as TASKs da onda estão em `[X]`
- CI verde (build + testes + architecture tests)
- PR da onda aberto, aprovado ou mergeado conforme regra do projeto
- riscos da onda tratados ou formalmente registrados
- README do módulo sincronizado quando aplicável

### 7.3 Encerramento do Módulo

O módulo só pode ser considerado pronto para go-live quando:

- todas as 6 ondas concluídas (TASK-01 a TASK-28)
- matriz de rastreabilidade completa (todos os 14 Reqs, 6 RNFs e 7 PBTs com TASK)
- todos os 7 PBTs verdes em CI
- teste de isolamento de tenant como gate de merge (KPI-06)
- dry-run e rollback total validados em staging com 108 e 500 linhas (RNF 1.3, RNF 5.2)
- catálogo de erros MIG-ERR-001..010 coberto por testes de contrato
- métricas `migration_rows_*` e `migration_duration_seconds` expostas e validadas
- PII ausente de todos os logs, payloads de evento e respostas de API
- feature flag `migration.import_enabled` operacional em staging
- `ImportCompleted` publicado via Outbox e consumido pelo audit-log como append-only
- README do módulo corrigido (fronteira in-process DD-001)
- `requirements.md`, `design.md` e `tasks.md` consistentes
- VAL-MIGR-01 registrado com critérios de remoção pós-Fase 1
- ciclo de vida temporário (VAL-MOD-04): confirmação de que a remoção do módulo não deixará dependências ativas nos módulos de domínio (Req 14.2); plano de drop das tabelas `migration_*` documentado

## 8. Riscos de Execução

| Risco | Impacto | Mitigação |
|-------|---------|-----------|
| RISK-MIGR-02: rollback falha por timeout no Postgres | Dados parciais | Timeout > SLO; rollback testado em staging com 108 e 500 linhas antes do go-live (TASK-26) |
| RISK-MIGR-04: porta in-process de módulo-alvo não idempotente | Duplicação em reexecução | `import_key` (TASK-19); contrato de porta exige upsert idempotente; PBT-02 (TASK-11, TASK-19) |
| RISK-MIGR-05: BUs/estágios não configurados antes do import (DEP-04) | Import bloqueado | `IOrganizationReadPort` valida pré-requisito; MIG-ERR-010 (TASK-18) |
| RISK-MIGR-06: divergência de epoch/datas | Datas erradas | Epoch 1899-12-30 (TASK-04); PBT-03 round-trip (TASK-04) |
| RISK-MIGR-07: README indica HTTP interna conflitando com atomicidade | Implementação incorreta | DD-001 corrigido em TASK-28; teste de arquitetura sem dependências reversas (TASK-27) |
| DEP-04 ausente em staging | Bloqueio de TASK-18 | Configurar BUs/estágios no tenant Vellus antes de TASK-18; dependência operacional registrada |

## 9. Referências

| Referência | Origem |
|-----------|--------|
| Requirements do módulo | docs/product/modules/data-migration/requirements.md v0.1.0 |
| Design técnico do módulo | docs/product/modules/data-migration/design.md v0.1.0 |
| README do módulo | docs/product/modules/data-migration/README.md |
| ADR-0001 (multi-tenant defesa em profundidade) | docs/product/adr/0001-isolamento-multi-tenant-defesa-em-profundidade.md |
| ADR-0003 (unicidade de opportunity_number por tenant) | docs/product/data-model/data-model.md § uq_opportunity_number_per_tenant |
| Money em centavos / NBR 5891 | `.forge/rules/domain/money-as-cents.md`; `.forge/rules/domain/nbr-5891-rounding.md` |
| Auditoria imutável / observabilidade | `.forge/rules/domain/audit-immutability.md`; `.forge/rules/architecture/observability.md` |
| Clean Architecture / DDD / API | `.forge/rules/architecture/clean-architecture.md`; `.forge/rules/architecture/ddd.md`; `.forge/rules/architecture/api-and-contracts.md` |
| Segurança e compliance | `.forge/rules/architecture/security-and-compliance.md` |
| Dados próprios (migration_jobs, migration_logs) | docs/product/data-model/data-model.md § Data Migration (BC-09) |
| Entidades-alvo (accounts, contacts, partners, opportunities, activities) | docs/product/data-model/data-model.md |
| Mapeamento de colunas da planilha | docs/product/azim-product-spec.md § Parte V |
| FRD migration-01..03 | docs/product/frd-nfrd/frd.md |
| Subdomínio SD-11 | docs/product/ddd/subdomains/supporting/data-migration/README.md |
