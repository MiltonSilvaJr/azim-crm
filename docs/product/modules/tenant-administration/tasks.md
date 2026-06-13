# Tasks — TA — Tenant Administration

- Versão: 0.1.1
- Data: 2026-06-13
- Status: Aprovado para desenvolvimento
- Referência base requirements: docs/product/modules/tenant-administration/requirements.md v0.1.0
- Referência base design: docs/product/modules/tenant-administration/design.md v0.1.0
- ADRs aplicáveis: ADR-0001 (Pooled DB + RLS), ADR-0008 (Scheduling digest por fuso IANA), ADR-0009 (correlationId/tenantId — a criar)
- Rules aplicáveis: `.forge/rules/domain/audit-immutability.md`, `.forge/rules/architecture/observability.md`, `.forge/rules/conventions/*`

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

PBT é obrigatório para as invariantes mapeadas em requirements.md §7 (PBT-01..08). Ferramenta: FsCheck ou CsCheck. Cada PBT mapeia explicitamente para o ID canônico do requirements.

| PBT | Propriedade | TASK que cobre |
|-----|-------------|----------------|
| PBT-01 | Imutabilidade e unicidade global do slug | TASK-02 |
| PBT-02 | Normalização e formato do slug | TASK-02 |
| PBT-03 | White-label estrito — conjunto de campos persistidos | TASK-04 |
| PBT-04 | Monotonicidade da validação de contraste WCAG AA | TASK-03 |
| PBT-05 | Determinismo dos tons derivados | TASK-03 |
| PBT-06 | Isolamento estrito por tenant (anti-cross-tenant) — gate de CI | TASK-16 |
| PBT-07 | Atomicidade do provisionamento (ambos-ou-nenhum) | TASK-13 |
| PBT-08 | Transições válidas do estado do tenant | TASK-05 |

### 1.3 Bite-sized Tasks

Cada subtask deve ser estimada para menos de 2 horas. Cada TASK deve ser pequena o suficiente para revisão objetiva e grande o suficiente para entregar um incremento verificável.

### 1.4 Branch Model

```text
<tipo>/<modulo>/<NN>-<slug>
```

Exemplos:

```text
feat/tenant-administration/01-bootstrap-solution
test/tenant-administration/02-domain-slug-vo
feat/tenant-administration/13-provisioning-saga
```

### 1.5 Git Worktree

```sh
git worktree add ../worktrees/tenant-administration/<NN>-<slug> -b <branch>
```

### 1.6 Encerramento de TASK

Cada TASK deve encerrar com: testes locais verdes, coverage gate atendido ou justificativa, lint/format executado, documentação atualizada quando aplicável, commit em Conventional Commits, push da branch.

### 1.7 Encerramento de Onda

Todas as TASKs da onda concluídas, CI verde, conflitos resolvidos, PR aberto ou atualizado, checklist de revisão preenchido.

### 1.8 Early Exit

Se uma subtask falhar: marcar `[-]`, registrar ponto de falha, comando executado e erro principal. Não mascarar falha com implementação especulativa.

### 1.9 Convenção de Status

- `[ ]` Não iniciado
- `[-]` Em progresso
- `[X]` Concluído
- `[!]` Falhou — exige intervenção humana

### 1.10 Convenção canônica de IDs

```
TASK-NN — <título>         ← unidade atômica de invocação do task-coder
  ST-MM — <subtask>        ← etapas internas (Red/Green/Refactor/Docs/Encerramento)
```

A numeração ST-MM reinicia a cada nova TASK.

---

## 2. Status Geral

| TASK | Título | Onda | Branch | Status |
|------|--------|------|--------|--------|
| TASK-01 | Bootstrap solution e testes de arquitetura | Onda 1 | `feat/tenant-administration/01-bootstrap-solution` | [X] |
| TASK-02 | Objetos de valor Slug, TimezoneIana e DigestTime | Onda 2 | `feat/tenant-administration/02-domain-slug-vos` | [X] |
| TASK-03 | WcagContrastPolicy, ToneDerivationService e ColorPair | Onda 2 | `feat/tenant-administration/03-domain-wcag-tones` | [X] |
| TASK-04 | BrandingTheme e AssetValidationPolicy | Onda 2 | `feat/tenant-administration/04-domain-branding-theme` | [X] |
| TASK-05 | Agregado Tenant, TenantBranding e state machine | Onda 2 | `feat/tenant-administration/05-domain-tenant-aggregate` | [X] |
| TASK-06 | Domain events e enfileiramento pelo agregado | Onda 2 | `feat/tenant-administration/06-domain-events` | [X] |
| TASK-07 | Pipeline behaviors (Correlation, Logging, Validation, Auth, Idempotency, Transaction) | Onda 3 | `feat/tenant-administration/07-app-behaviors` | [X] |
| TASK-08 | ProvisionTenantCommand e handler | Onda 3 | `feat/tenant-administration/08-app-provision` | [X] |
| TASK-09 | SuspendTenantCommand e ReactivateTenantCommand | Onda 3 | `feat/tenant-administration/09-app-state-commands` | [X] |
| TASK-10 | UpdateBrandingCommand e handler | Onda 3 | `feat/tenant-administration/10-app-branding-command` | [X] |
| TASK-11 | UpdateDigestConfigCommand, handler e queries | Onda 3 | `feat/tenant-administration/11-app-digest-queries` | [X] |
| TASK-12 | EF Core, migrations, RLS e global query filter | Onda 4 | `feat/tenant-administration/12-infra-efcore-rls` | [X] |
| TASK-13 | TenantProvisioningSaga e adapter IIdentityTenantProvisioner | Onda 4 | `feat/tenant-administration/13-infra-saga-idp` | [X] |
| TASK-14 | IBrandingAssetStorage (GCS), ICdnInvalidator e cache slug | Onda 4 | `feat/tenant-administration/14-infra-gcs-cdn` | [X] |
| TASK-15 | Outbox, IEventOutbox e publicador Pub/Sub | Onda 4 | `feat/tenant-administration/15-infra-outbox-pubsub` | [X] |
| TASK-16 | Testes de isolamento RLS + PBT-06 gate de CI | Onda 4 | `test/tenant-administration/16-infra-rls-pbt06` | [X] |
| TASK-17 | Endpoints de plataforma (provision, suspend, reactivate) | Onda 5 | `feat/tenant-administration/17-api-platform` | [X] |
| TASK-18 | Endpoints de tenant (GET/PATCH tenant, GET/PUT branding) | Onda 5 | `feat/tenant-administration/18-api-tenant` | [X] |
| TASK-19 | Endpoint público brand.json + CDN cache + anti-enumeração | Onda 5 | `feat/tenant-administration/19-api-brand-json` | [X] |
| TASK-20 | Contract tests de eventos + OpenAPI | Onda 5 | `test/tenant-administration/20-contract-events-openapi` | [X] |
| TASK-21 | Segurança: bloqueio PlatOp, sanitização SVG, auditoria | Onda 6 | `feat/tenant-administration/21-hardening-security` | [X] |
| TASK-22 | Observabilidade: métricas, alertas, health checks e DoD | Onda 6 | `feat/tenant-administration/22-hardening-observability` | [X] |

---

## 3. Ondas de Implementação

| Onda | Foco | TASKs | Critério de fechamento |
|------|------|-------|------------------------|
| Onda 1 | Bootstrap: solution, projetos, dependências, testes de arquitetura | TASK-01 | Architecture.Tests verde; solução compila; CI mínimo verde |
| Onda 2 | Domain: objetos de valor, aggregate, state machine, events, policies, PBTs 01–05, 08 | TASK-02..TASK-06 | Coverage domain ≥ 95%; todos PBTs da onda verdes |
| Onda 3 | Application: commands, handlers, behaviors, queries, validações | TASK-07..TASK-11 | Coverage application ≥ 85%; handlers cobertos por testes unitários |
| Onda 4 | Infrastructure: EF Core, RLS, GCS, IdP, Outbox, Pub/Sub; PBT-06 gate CI; PBT-07 | TASK-12..TASK-16 | Coverage infra ≥ 70%; PBT-06 obrigatório verde em CI; saga testada |
| Onda 5 | API + Contracts: endpoints REST, brand.json, catálogo de erros, contract tests | TASK-17..TASK-20 | Coverage API ≥ 80%; todos erros TA-ERR cobertos; contract tests verdes |
| Onda 6 | Hardening: segurança, auditoria, observabilidade, DoD final | TASK-21..TASK-22 | DoD completo; alerta de falha de provisionamento ativo; README sincronizado |

---

## 4. Tarefas

### TASK-01 — Bootstrap solution e testes de arquitetura

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 1 — Bootstrap |
| **Branch** | `feat/tenant-administration/01-bootstrap-solution` |
| **Worktree** | `git worktree add ../worktrees/tenant-administration/01-bootstrap-solution -b feat/tenant-administration/01-bootstrap-solution` |
| **Status** | [ ] |
| **Depende de** | Não aplicável |
| **Entregável** | Solution compilável com 5 projetos de produção + 5 de teste; Architecture.Tests validando regras de dependência entre camadas; CI mínimo configurado |
| **Mapeia** | design.md §2 (princípios), §3 (estrutura da solução) |
| **Camada principal** | DevOps / Tests |

#### Objetivo

Criar a estrutura de solução .NET 10 conforme design.md §3: projetos `TenantAdministration.Domain`, `.Application`, `.Infrastructure`, `.Api`, `.Contracts` e os cinco projetos de teste correspondentes. Configurar os testes de arquitetura (NetArchTest ou ArchUnitNET) que validam as regras de dependência (`Domain` sem referências externas; `Api` não acessa `Domain` diretamente — apenas via `Application`). Sem esses gates, qualquer TASK futura poderia introduzir acoplamento silencioso.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de arquitetura que falham verificando: (a) `Domain` não referencia EF Core, GCP SDK ou framework web; (b) `Api` não acessa `Domain` sem passar por `Application`; (c) `Contracts` não depende de nenhum outro projeto da solução
- [ ] **ST-02 — Green:** criar a solution com `dotnet new` e os 10 projetos; adicionar referências de projeto conforme grafo do design.md §3; instalar NetArchTest/ArchUnitNET nos testes de arquitetura
- [ ] **ST-03 — Refactor:** garantir nomes de namespace canônicos; adicionar `.editorconfig` e `global.json` (pinado ao .NET 10); limpar projetos gerados por template
- [ ] **ST-04 — Docs:** registrar estrutura de projetos no README do módulo
- [ ] **ST-05 — Encerramento:** `dotnet build` verde; Architecture.Tests verdes; commit `chore(tenant-administration): bootstrap solution and architecture tests` + push

#### Critérios de Aceite

- [ ] Todos os 10 projetos compilam sem warning novo
- [ ] Architecture.Tests verdes para as 3 regras de dependência
- [ ] CI executa `dotnet build` e `dotnet test` sem falha
- [ ] Nenhum projeto de produção referencia `xunit`, `FsCheck` ou `Testcontainers`

---

### TASK-02 — Objetos de valor Slug, TimezoneIana e DigestTime

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `feat/tenant-administration/02-domain-slug-vos` |
| **Worktree** | `git worktree add ../worktrees/tenant-administration/02-domain-slug-vos -b feat/tenant-administration/02-domain-slug-vos` |
| **Status** | [ ] |
| **Depende de** | TASK-01 |
| **Entregável** | Objetos de valor `Slug`, `TimezoneIana` e `DigestTime` imutáveis, com fábrica `Create`, validação na construção e PBT-01 + PBT-02 verdes |
| **Mapeia** | Req 2, Req 3; PBT-01, PBT-02; design.md §4.3 |
| **Camada principal** | Domain |

#### Objetivo

Implementar os três primeiros objetos de valor do domínio. `Slug` é o elemento mais crítico: deve rejeitar qualquer caractere fora de `[a-z-]`, impedir `--` consecutivos e bordas com `-`, comprimento 3..40, e nunca ter setter público. `TimezoneIana` valida contra a base IANA do runtime. `DigestTime` valida formato `HH:mm`. Os PBTs PBT-01 e PBT-02 cobrem imutabilidade, unicidade e formato do slug.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes unitários para `Slug.Create`: casos válidos, caracteres inválidos, bordas com `-`, `--` consecutivos, comprimento fora de 3..40, trim/lowercase tolerados; escrever PBT-01 (sequências de modificação não alteram slug) e PBT-02 (gerador de strings aceitas ⟺ apenas `[a-z-]` normalizável)
- [ ] **ST-02 — Green:** implementar `Slug` com `Create(string raw)` retornando `Result<Slug>`; implementar `TimezoneIana` com validação via `TimeZoneInfo`; implementar `DigestTime` com regex `HH:mm` e padrão `07:00`
- [ ] **ST-03 — Refactor:** extrair constantes (`MinLength`, `MaxLength`, regex); garantir `record` ou `readonly struct` sem setter; revisar mensagens de erro alinhadas ao catálogo (`TA-ERR-005`, `TA-ERR-006`)
- [ ] **ST-04 — Encerramento:** cobertura domain ≥ 95% nos novos arquivos; commit `feat(tenant-administration): domain value objects Slug TimezoneIana DigestTime` + push

#### Critérios de Aceite

- [ ] PBT-01 e PBT-02 verdes (FsCheck/CsCheck)
- [ ] Sem setter público em `Slug`; sem método de alteração
- [ ] `Slug.Create("América")` retorna `Failure` com `TA-ERR-005`
- [ ] `TimezoneIana.Create("America/Sao_Paulo")` retorna `Success`; valor inválido retorna `TA-ERR-006`
- [ ] Coverage ≥ 95% nos novos tipos

---

### TASK-03 — WcagContrastPolicy, ToneDerivationService e ColorPair

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `feat/tenant-administration/03-domain-wcag-tones` |
| **Worktree** | `git worktree add ../worktrees/tenant-administration/03-domain-wcag-tones -b feat/tenant-administration/03-domain-wcag-tones` |
| **Status** | [ ] |
| **Depende de** | TASK-02 |
| **Entregável** | `ColorPair` com validação `#RRGGBB`; `WcagContrastPolicy` determinística com limítrofes corretos; `ToneDerivationService` puro; PBT-04 e PBT-05 verdes |
| **Mapeia** | Req 5, Req 6, Req 8, RNF 2; PBT-04, PBT-05; design.md §4.3, §4.6, DD-003 |
| **Camada principal** | Domain |

#### Objetivo

Implementar `ColorPair` (par `#RRGGBB`, normalizado para maiúsculas) com cálculo de luminância relativa. Implementar `WcagContrastPolicy` usando `round(R, 2) >= 4.50` (DD-003) para evitar instabilidade de ponto flutuante nos limítrofes. Implementar `ToneDerivationService` como função pura (sem I/O, sem clock) que deriva variações HSL determinísticas. PBT-04 valida que aprovação ⟺ `round(R,2) ≥ 4.50` para qualquer par gerado. PBT-05 valida idempotência de `ToneDerivationService`.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de `ColorPair`: aceita `#1A73E8`, rejeita `1A73E8`, `#GGG`, `#RRGGBBAA`; escrever testes de `WcagContrastPolicy` com casos 4.4 (reprova), 4.5 (aprova), 4.6 (aprova) e PBT-04 (gerador de pares com razão R; aprovação ⟺ `round(R,2) >= 4.50`); escrever PBT-05 (`derive(c) == derive(c)` em múltiplas chamadas)
- [ ] **ST-02 — Green:** implementar `ColorPair` com regex `^#[0-9A-Fa-f]{6}$` e normalização uppercase; implementar luminância relativa sRGB; implementar `WcagContrastPolicy.Check(ColorPair)` retornando `(bool approved, decimal ratio)`; implementar `ToneDerivationService.Derive(ColorPair)` retornando `DerivedTones`
- [ ] **ST-03 — Refactor:** extrair fórmula de luminância para função privada estática; garantir que `ToneDerivationService` é `static` ou sem estado; validar que `DerivedTones` é `record` imutável com todos os tons nomeados conforme design.md §4.3
- [ ] **ST-04 — Encerramento:** PBT-04 e PBT-05 verdes; coverage ≥ 95%; commit `feat(tenant-administration): WcagContrastPolicy ToneDerivationService ColorPair` + push

#### Critérios de Aceite

- [ ] PBT-04 verde: limítrofes 4.4/4.5/4.6 determinísticos
- [ ] PBT-05 verde: `Derive` idempotente para qualquer `ColorPair` gerado
- [ ] `ColorPair.Create("#rrggbb")` normaliza para `#RRGGBB`
- [ ] `WcagContrastPolicy` não usa `DateTime`, I/O ou estado mutável
- [ ] Coverage ≥ 95% nos novos tipos

---

### TASK-04 — BrandingTheme e AssetValidationPolicy

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `feat/tenant-administration/04-domain-branding-theme` |
| **Worktree** | `git worktree add ../worktrees/tenant-administration/04-domain-branding-theme -b feat/tenant-administration/04-domain-branding-theme` |
| **Status** | [ ] |
| **Depende de** | TASK-03 |
| **Entregável** | `BrandingTheme` com exatamente quatro campos e sem setter para elementos fora do conjunto; `AssetValidationPolicy` validando formato e tamanho; PBT-03 verde |
| **Mapeia** | Req 5, Req 7; PBT-03; design.md §4.3, §4.6, DD-004, DD-007 |
| **Camada principal** | Domain |

#### Objetivo

Implementar `BrandingTheme` como objeto de valor que encapsula exatamente `{logoUrl, faviconUrl, ColorPair colors}`. O tipo não deve ter propriedade para CSS, fonte ou layout — a ausência de propriedade é a garantia de white-label estrito (PBT-03). `AssetValidationPolicy` valida formato real (magic bytes, não só extensão) e tamanho ≤ 1 MB; inclui sanitização de SVG (remoção de `<script>` e handlers `on*`) conforme DD-007.

#### Subtasks

- [ ] **ST-01 — Red:** escrever PBT-03 (gerador de payloads com campos extras; persistido ⊆ `{logo, favicon, primary, secondary}`; qualquer campo fora é rejeitado pelo compilador/tipo); escrever testes de `AssetValidationPolicy`: PNG válido aceito, SVG válido aceito, JPEG rejeitado, arquivo > 1 MB rejeitado, SVG com `<script>` rejeitado
- [ ] **ST-02 — Green:** implementar `BrandingTheme` como `record` com apenas `logoUrl`, `faviconUrl`, `ColorPair`; implementar `AssetValidationPolicy.Validate(Stream content, string mediaType, long sizeBytes)` retornando `Result`; implementar sanitização SVG básica (strip `<script>`, `on*`, `xlink:href` externos)
- [ ] **ST-03 — Refactor:** garantir que magic bytes (primeiros bytes do stream) são verificados antes da extensão; criar constantes para limites (`MaxSizeBytesLogo = 1_048_576`); manter sanitização SVG testável de forma isolada
- [ ] **ST-04 — Encerramento:** PBT-03 verde; coverage ≥ 95%; commit `feat(tenant-administration): BrandingTheme AssetValidationPolicy SVG sanitization` + push

#### Critérios de Aceite

- [ ] PBT-03 verde: nenhum campo fora de `{logo, favicon, primary, secondary}` existe em `BrandingTheme`
- [ ] `AssetValidationPolicy` rejeita JPEG com `TA-ERR-013`
- [ ] `AssetValidationPolicy` rejeita arquivo > 1 MB com `TA-ERR-012`
- [ ] SVG com `<script>` é sanitizado ou rejeitado antes do armazenamento
- [ ] Coverage ≥ 95%

---

### TASK-05 — Agregado Tenant, TenantBranding e state machine

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `feat/tenant-administration/05-domain-tenant-aggregate` |
| **Worktree** | `git worktree add ../worktrees/tenant-administration/05-domain-tenant-aggregate -b feat/tenant-administration/05-domain-tenant-aggregate` |
| **Status** | [ ] |
| **Depende de** | TASK-04 |
| **Entregável** | Agregado `Tenant` com `TenantBranding` interna, métodos de comportamento (`Provision`, `Suspend`, `Reactivate`, `UpdateBranding`, `UpdateDigestConfig`) e state machine com PBT-08 verde |
| **Mapeia** | Req 1, Req 2, Req 3, Req 4, Req 5; PBT-08; design.md §4.1, §4.2, §4.5, DD-002 |
| **Camada principal** | Domain |

#### Objetivo

Implementar o agregado `Tenant` como raiz da fronteira transacional. `slug` não tem setter e não é parâmetro de nenhum método que não seja `Provision`. A state machine `TenantStatus` (`provisioned`/`suspended`) deve rejeitar transições inválidas com `TA-ERR-007`. PBT-08 gera sequências arbitrárias de suspend/reactivate e verifica que apenas transições canônicas passam.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de `Tenant`: `Provision` com campos válidos cria em `provisioned`; `Suspend` em `provisioned` transita para `suspended`; `Suspend` em `suspended` falha com `TA-ERR-007`; `Reactivate` em `provisioned` falha com `TA-ERR-007`; `UpdateBranding` atualiza `TenantBranding`; `slug` inacessível via setter; escrever PBT-08 (sequências de operações de estado; apenas transições canônicas)
- [ ] **ST-02 — Green:** implementar `Tenant` com campo `_slug` privado imutável; implementar `TenantStatus` enum; implementar todos os métodos de comportamento que enfileiram domain events correspondentes; implementar `TenantBranding` como entidade interna (sem repositório próprio)
- [ ] **ST-03 — Refactor:** garantir que `Tenant` não expõe coleções mutáveis de domain events; usar `IClock` injetado em `Provision` para `provisionedAt`; revisar que `UpdateBranding` invoca `WcagContrastPolicy` antes de aceitar as cores (ou delega ao handler — verificar design.md §5.3 e definir responsabilidade)
- [ ] **ST-04 — Encerramento:** PBT-08 verde; coverage ≥ 95%; commit `feat(tenant-administration): Tenant aggregate TenantBranding state machine` + push

#### Critérios de Aceite

- [ ] PBT-08 verde: sequências inválidas não alteram estado
- [ ] Sem setter público para `slug` em nenhum caminho
- [ ] `Tenant.Suspend()` em tenant suspenso lança `TA-ERR-007`
- [ ] `TenantBranding` só é modificável via `Tenant.UpdateBranding`
- [ ] Coverage ≥ 95%

---

### TASK-06 — Domain events e enfileiramento pelo agregado

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `feat/tenant-administration/06-domain-events` |
| **Worktree** | `git worktree add ../worktrees/tenant-administration/06-domain-events -b feat/tenant-administration/06-domain-events` |
| **Status** | [ ] |
| **Depende de** | TASK-05 |
| **Entregável** | Domain events `TenantProvisioned`, `TenantSuspended`, `TenantReactivated`, `BrandingChanged`, `DigestConfigChanged` tipados e enfileirados pelo agregado; testes de enfileiramento por método |
| **Mapeia** | Req 11; design.md §4.4 |
| **Camada principal** | Domain |

#### Objetivo

Formalizar os domain events como tipos imutáveis com payload mínimo conforme design.md §4.4. O agregado deve enfileirar o evento correto em cada operação de comportamento sem depender de qualquer infraestrutura. Testes verificam que após `Tenant.Provision(...)` exatamente um `TenantProvisioned` está na fila de eventos; após `Tenant.UpdateBranding(...)` exatamente um `BrandingChanged`.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes que verificam: `Tenant.Provision` enfileira `TenantProvisioned` com `tenantId`, `slug`, `displayName`, `timezone`; `Tenant.Suspend()` enfileira `TenantSuspended`; `Tenant.UpdateBranding` enfileira `BrandingChanged` com `wcagContrastOk`; `Tenant.UpdateDigestConfig` enfileira `DigestConfigChanged`; nenhum evento é enfileirado em operação que falha
- [ ] **ST-02 — Green:** criar tipos de domain events como `record` imutáveis no `Domain`; adicionar método `DomainEvents` (readonly) no agregado e limpeza `ClearDomainEvents()` para consumo pelo handler
- [ ] **ST-03 — Refactor:** garantir que eventos têm `OccurredAt` via `IClock` (não `DateTime.UtcNow` diretamente no tipo); garantir que eventos não expõem dados além do payload mínimo do design.md §4.4
- [ ] **ST-04 — Encerramento:** coverage ≥ 95%; commit `feat(tenant-administration): domain events provisioned suspended reactivated branding-changed` + push

#### Critérios de Aceite

- [ ] Evento enfileirado somente quando operação é bem-sucedida
- [ ] Tipos de evento são `record` imutáveis, sem dependências de infraestrutura
- [ ] `ClearDomainEvents()` limpa a fila sem expor a lista mutável
- [ ] Coverage ≥ 95%

---

### TASK-07 — Pipeline behaviors

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/tenant-administration/07-app-behaviors` |
| **Worktree** | `git worktree add ../worktrees/tenant-administration/07-app-behaviors -b feat/tenant-administration/07-app-behaviors` |
| **Status** | [ ] |
| **Depende de** | TASK-06 |
| **Entregável** | Seis behaviors MediatR testados unitariamente: `CorrelationBehavior`, `LoggingBehavior`, `ValidationBehavior`, `AuthorizationBehavior`, `IdempotencyBehavior`, `TransactionBehavior` |
| **Mapeia** | RNF 1, RNF 6, RNF 7; design.md §5.4 |
| **Camada principal** | Application |

#### Objetivo

Implementar os behaviors MediatR conforme ordem do design.md §5.4. `AuthorizationBehavior` deve diferenciar o plano de plataforma (PlatOp) do plano de tenant (TAdmin/Viewer). `IdempotencyBehavior` verifica `Idempotency-Key` do header para `ProvisionTenantCommand`. `TransactionBehavior` aplica `SET app.current_tenant` quando há contexto de tenant. Testes unitários com mocks de `ICurrentUserContext` e `ITenantContext`.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de `AuthorizationBehavior`: comando marcado com `[RequiresPlatformOperator]` é rejeitado para TAdmin; comando marcado com `[RequiresTenantAdmin]` é rejeitado para PlatOp; escrever testes de `ValidationBehavior`: comando inválido retorna `ValidationException` antes de chegar ao handler; escrever testes de `IdempotencyBehavior`: segunda chamada com mesma chave retorna resultado cacheado sem chamar handler
- [ ] **ST-02 — Green:** implementar `CorrelationBehavior` propagando `correlation_id` e `tenant_id` para o contexto de log (Serilog `LogContext`); implementar `LoggingBehavior` com log de entrada/saída; implementar os demais behaviors; registrar behaviors na DI do módulo na ordem correta
- [ ] **ST-03 — Refactor:** garantir que behaviors não têm lógica de domínio; extrair atributos de autorização (`[RequiresPlatformOperator]`, `[RequiresTenantAdmin]`, `[AllowAnonymous]`) para tipos canônicos do `Application`
- [ ] **ST-04 — Encerramento:** coverage application ≥ 85%; commit `feat(tenant-administration): MediatR pipeline behaviors` + push

#### Critérios de Aceite

- [ ] `AuthorizationBehavior` bloqueia PlatOp em rotas de tenant e TAdmin em rotas de plataforma
- [ ] `IdempotencyBehavior` não reexecuta handler para chave já processada
- [ ] `CorrelationBehavior` injeta `correlation_id` e `tenant_id` no `LogContext` antes do handler
- [ ] Coverage ≥ 85% nos behaviors

---

### TASK-08 — ProvisionTenantCommand e handler

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/tenant-administration/08-app-provision` |
| **Worktree** | `git worktree add ../worktrees/tenant-administration/08-app-provision -b feat/tenant-administration/08-app-provision` |
| **Status** | [ ] |
| **Depende de** | TASK-07 |
| **Entregável** | `ProvisionTenantCommand` com todos os campos do design.md §5.1; `ProvisionTenantHandler` delegando à saga; `ProvisionTenantCommandValidator` (FluentValidation) cobrindo `TA-ERR-001/003/005/006` |
| **Mapeia** | Req 1, Req 2, Req 12; design.md §5.1, §5.3, §5.5 |
| **Camada principal** | Application |

#### Objetivo

Implementar o command e o handler para provisionamento. O handler não executa a saga diretamente nesta TASK — ele invoca `TenantProvisioningSaga` (porta, interface) que será implementada na TASK-13. O foco aqui é: validação de confirmação de slug (`TA-ERR-003`), verificação de unicidade antes da saga via `ITenantRepository`, e delegação à saga com `idempotencyKey`. Testes unitários com saga mockada.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de `ProvisionTenantHandler`: `slug != slugConfirmation` retorna `TA-ERR-003`; slug com formato inválido retorna `TA-ERR-005`; slug já existente retorna `TA-ERR-002` (409); saga retorna sucesso → handler retorna `tenantId` e `slug`; saga falha IdP → handler propaga `TA-ERR-009`; escrever testes de `ProvisionTenantCommandValidator`
- [ ] **ST-02 — Green:** implementar `ProvisionTenantCommand` com campos `slug`, `slugConfirmation`, `displayName`, `timezone`, `digestTime`, `adminEmail`, `idempotencyKey`; implementar `ProvisionTenantCommandValidator`; implementar `ProvisionTenantHandler` com verificação de unicidade e delegação à `ITenantProvisioningSaga`
- [ ] **ST-03 — Refactor:** garantir que `ProvisionTenantHandler` não conhece EF Core nem GCP SDK diretamente; apenas ports (`ITenantRepository`, `ITenantProvisioningSaga`); limpar mensagens de erro para o catálogo §12
- [ ] **ST-04 — Encerramento:** coverage ≥ 85%; commit `feat(tenant-administration): ProvisionTenantCommand handler validator` + push

#### Critérios de Aceite

- [ ] Validação de confirmação de slug antes de qualquer I/O
- [ ] Unicidade de slug verificada via `ITenantRepository.ExistsSlugAsync`
- [ ] Handler não importa EF Core nem GCP SDK
- [ ] Coverage ≥ 85% no handler e validator

---

### TASK-09 — SuspendTenantCommand e ReactivateTenantCommand

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/tenant-administration/09-app-state-commands` |
| **Worktree** | `git worktree add ../worktrees/tenant-administration/09-app-state-commands -b feat/tenant-administration/09-app-state-commands` |
| **Status** | [ ] |
| **Depende de** | TASK-08 |
| **Entregável** | `SuspendTenantCommand` e `ReactivateTenantCommand` com handlers que carregam o agregado, invocam transição e persistem com Outbox |
| **Mapeia** | Req 4; design.md §5.1, §5.3 |
| **Camada principal** | Application |

#### Objetivo

Implementar os dois commands de ciclo de vida para Platform Operator. Handlers carregam o agregado via `ITenantRepository`, invocam `Tenant.Suspend()` ou `Tenant.Reactivate()`, propagam o erro de transição inválida (`TA-ERR-007`) ou `TA-ERR-008` (não encontrado), e persistem o agregado com seus domain events para o Outbox.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de `SuspendTenantHandler`: tenant não encontrado retorna `TA-ERR-008`; tenant já suspenso retorna `TA-ERR-007`; tenant ativo é suspenso e `TenantSuspended` é enfileirado; escrever testes análogos para `ReactivateTenantHandler`
- [ ] **ST-02 — Green:** implementar commands, validators e handlers com ports mockados; garantir que `TransactionBehavior` (via pipeline) escreve Outbox na mesma transação
- [ ] **ST-03 — Refactor:** extrair lógica de carregamento e verificação de existência para método helper interno do módulo de application; garantir que handler não usa `TenantStatus` diretamente (delega ao agregado)
- [ ] **ST-04 — Encerramento:** coverage ≥ 85%; commit `feat(tenant-administration): SuspendTenantCommand ReactivateTenantCommand handlers` + push

#### Critérios de Aceite

- [ ] Transição inválida retorna `TA-ERR-007` sem alterar estado do agregado
- [ ] Tenant não encontrado retorna `TA-ERR-008`
- [ ] Domain events enfileirados são escritos via Outbox no mesmo `TransactionBehavior`
- [ ] Coverage ≥ 85%

---

### TASK-10 — UpdateBrandingCommand e handler

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/tenant-administration/10-app-branding-command` |
| **Worktree** | `git worktree add ../worktrees/tenant-administration/10-app-branding-command -b feat/tenant-administration/10-app-branding-command` |
| **Status** | [ ] |
| **Depende de** | TASK-09 |
| **Entregável** | `UpdateBrandingCommand` e `UpdateBrandingHandler` orquestrando validação de asset, upload GCS, validação WCAG, persistência e invalidação de CDN; rejeições não alteram branding prévio |
| **Mapeia** | Req 5, Req 6, Req 7, Req 8; design.md §5.3 |
| **Camada principal** | Application |

#### Objetivo

Implementar a orquestração de branding conforme o sequence diagram do design.md §16.4: (1) `AssetValidationPolicy`; (2) upload via `IBrandingAssetStorage`; (3) `WcagContrastPolicy`; (4) `Tenant.UpdateBranding`; (5) persistir com Outbox `BrandingChanged`; (6) `ICdnInvalidator`. Qualquer falha antes do commit não altera o branding existente (Req 7.5). Segundo DD-004, cores reprovadas no contraste bloqueiam o salvamento com `TA-ERR-014`.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes: asset inválido retorna `TA-ERR-012/013` sem chamar GCS; cores com contraste < 4.5 retornam `TA-ERR-014` com `contrastRatio` calculado; upload GCS falha → branding anterior preservado; sucesso retorna `wcagContrastOk=true`, `derivedTones` e URLs; `ICdnInvalidator.InvalidateAsync` chamado após commit
- [ ] **ST-02 — Green:** implementar `UpdateBrandingCommand` (`logo` como `Stream`, `favicon` como `Stream`, `primaryColor`, `secondaryColor`) e `UpdateBrandingHandler` com todos os ports mockados; implementar resposta com `DerivedTones` via `ToneDerivationService`
- [ ] **ST-03 — Refactor:** garantir rollback de upload GCS se commit do banco falhar (ou usar URL temporária até commit — definir decisão explícita no código); garantir que `BrandingChanged` é enfileirado apenas após `Tenant.UpdateBranding` bem-sucedido
- [ ] **ST-04 — Encerramento:** coverage ≥ 85%; commit `feat(tenant-administration): UpdateBrandingCommand handler WCAG asset validation CDN` + push

#### Critérios de Aceite

- [ ] `TA-ERR-014` retorna valor de `contrastRatio` calculado ao usuário
- [ ] Upload GCS não é chamado se `AssetValidationPolicy` reprova
- [ ] Branding anterior permanece inalterado em qualquer cenário de falha
- [ ] `ICdnInvalidator` chamado uma vez por atualização bem-sucedida
- [ ] Coverage ≥ 85%

---

### TASK-11 — UpdateDigestConfigCommand, handler e queries

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/tenant-administration/11-app-digest-queries` |
| **Worktree** | `git worktree add ../worktrees/tenant-administration/11-app-digest-queries -b feat/tenant-administration/11-app-digest-queries` |
| **Status** | [ ] |
| **Depende de** | TASK-10 |
| **Entregável** | `UpdateDigestConfigCommand` e handler; queries `GetCurrentTenantQuery`, `GetTenantBrandingQuery` e `GetPublicBrandJsonQuery` implementadas e testadas |
| **Mapeia** | Req 3, Req 9; design.md §5.1, §5.2, §5.3 |
| **Camada principal** | Application |

#### Objetivo

Fechar a camada de Application com o command de digest e as três queries. `GetPublicBrandJsonQuery` resolve tenant por slug, monta o payload `brand.json` sem dados sensíveis (Req 9.2), inclui `DerivedTones` e `wcagContrastOk`. `GetTenantBrandingQuery` é restrita por RLS (contexto de tenant). PATCH de `slug` no corpo deve ser rejeitado com `TA-ERR-011`.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de `UpdateDigestConfigHandler`: fuso IANA inválido retorna `TA-ERR-006`; fuso válido atualiza e enfileira `DigestConfigChanged`; escrever testes de `GetPublicBrandJsonHandler`: slug existente retorna payload sem `adminEmail`/dados sensíveis; slug inexistente retorna `TA-ERR-008`; escrever testes de `UpdateDigestConfigCommandValidator` rejeitando `slug` no payload
- [ ] **ST-02 — Green:** implementar command e handler de digest; implementar handlers das três queries; garantir que `GetPublicBrandJsonQuery` inclui `version` (timestamp de `updatedAt` do branding)
- [ ] **ST-03 — Refactor:** garantir que nenhuma query retorna `adminEmail`, `identity_tenant_id` ou qualquer dado de plano de plataforma ao plano de tenant ou anônimo
- [ ] **ST-04 — Encerramento:** coverage ≥ 85%; commit `feat(tenant-administration): UpdateDigestConfigCommand queries brand-json` + push

#### Critérios de Aceite

- [ ] `GetPublicBrandJsonQuery` não expõe `adminEmail` ou `identityTenantId`
- [ ] Fuso IANA inválido em `UpdateDigestConfigCommand` retorna `TA-ERR-006`
- [ ] `TA-ERR-011` retornado se `slug` aparecer no corpo de PATCH
- [ ] Coverage ≥ 85%

---

### TASK-12 — EF Core, migrations, RLS e global query filter

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/tenant-administration/12-infra-efcore-rls` |
| **Worktree** | `git worktree add ../worktrees/tenant-administration/12-infra-efcore-rls -b feat/tenant-administration/12-infra-efcore-rls` |
| **Status** | [ ] |
| **Depende de** | TASK-11 |
| **Entregável** | `TenantAdministrationDbContext` com mapeamento completo de `Tenant` e `TenantBranding`; migration inicial; global query filter para `tenant_brandings`; trigger `prevent_slug_update`; RLS habilitado; tabela `tenant_provisioning_requests` |
| **Mapeia** | Req 2, Req 10, RNF 1; design.md §6.1, §7.1..7.5, DD-001, DD-002 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar a camada de persistência com todas as constraints do design.md §7. A migration deve incluir: `status` enum + coluna `active` como projeção (DD-002); índice `uq_tenants_slug`; RLS em `tenant_brandings`; tabela `tenant_provisioning_requests` para idempotência. O global query filter EF Core é a Camada 1 de defesa em profundidade (RNF 1). Testes de integração com Testcontainers PostgreSQL.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de integração (Testcontainers): inserir branding de tenant A e verificar que query com contexto de tenant B retorna vazio tanto pelo EF Core global filter quanto pela RLS direta; verificar que `slug` não pode ser atualizado via SQL direto (trigger)
- [ ] **ST-02 — Green:** implementar `TenantAdministrationDbContext` com mapeamentos Fluent API; criar migration inicial com todas as tabelas, índices, checks e RLS; implementar `TenantRepository` com `ITenantRepository`; configurar global query filter `HasQueryFilter(t => t.TenantId == _currentTenant)` em `tenant_brandings`
- [ ] **ST-03 — Refactor:** garantir que migration que toca RLS tem revisão obrigatória anotada (comentário explícito no arquivo de migration — RNF 1.3); separar connection strings de plano de plataforma e plano de tenant (DD-001); validar que `IClock` é injetado, não `DateTime.UtcNow`
- [ ] **ST-04 — Docs:** documentar no README do módulo a necessidade de revisão obrigatória de migrations que tocam RLS
- [ ] **ST-05 — Encerramento:** Testcontainers verdes; coverage infra ≥ 70%; commit `feat(tenant-administration): EF Core migrations RLS global-query-filter` + push

#### Critérios de Aceite

- [ ] Global query filter ativo para `tenant_brandings` em todos os contextos de tenant
- [ ] RLS PostgreSQL habilitado com policy `USING (tenant_id = current_setting('app.current_tenant')::uuid)`
- [ ] Trigger `prevent_slug_update` impede UPDATE da coluna `slug`
- [ ] `tenant_provisioning_requests` criada com PK `idempotency_key`
- [ ] Testes de integração com Testcontainers verdes; coverage ≥ 70%

---

### TASK-13 — TenantProvisioningSaga e adapter IIdentityTenantProvisioner

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/tenant-administration/13-infra-saga-idp` |
| **Worktree** | `git worktree add ../worktrees/tenant-administration/13-infra-saga-idp -b feat/tenant-administration/13-infra-saga-idp` |
| **Status** | [ ] |
| **Depende de** | TASK-12 |
| **Entregável** | `TenantProvisioningSaga` com compensação; `GcpIdentityPlatformAdapter` implementando `IIdentityTenantProvisioner`; idempotência via `tenant_provisioning_requests`; PBT-07 verde |
| **Mapeia** | Req 1, Req 12; PBT-07; design.md §6.4, §6.5 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar a saga conforme o sequence diagram do design.md §6.4: (1) criar no IdP com `idempotencyKey`; (2) se falhar → abortar com `TA-ERR-009`; (3) se sucesso → BEGIN; INSERT tenant; INSERT outbox; COMMIT; (4) se commit falhar → compensar deletando tenant de identidade → `TA-ERR-010`. PBT-07 simula falha em cada passo e verifica que o estado final é sempre ambos-ou-nenhum.

#### Subtasks

- [ ] **ST-01 — Red:** escrever PBT-07 com double fake de IdP e DB: injetar falha aleatória em cada passo (IdP antes de DB, DB após IdP); verificar que estado final é `(idpExists == dbExists)` sempre; escrever testes de idempotência: segunda chamada com mesma `idempotency_key` retorna resultado anterior sem chamar IdP novamente
- [ ] **ST-02 — Green:** implementar `TenantProvisioningSaga`; implementar `GcpIdentityPlatformAdapter` com HTTP client + timeout ~10 s, retry backoff (1 s/5 s/30 s) e circuit breaker (Polly); implementar leitura/escrita em `tenant_provisioning_requests` para idempotência
- [ ] **ST-03 — Refactor:** garantir que step de compensação (delete IdP) também usa retry com backoff; registrar no log estruturado `correlationId` e `slug` em cada passo da saga; extrair constantes de timeout/retry para configuração
- [ ] **ST-04 — Encerramento:** PBT-07 verde; coverage ≥ 70%; commit `feat(tenant-administration): TenantProvisioningSaga GcpIdentityPlatformAdapter idempotency` + push

#### Critérios de Aceite

- [ ] PBT-07 verde: nunca estado parcial após qualquer falha simulada
- [ ] Idempotência: segunda chamada com mesma chave não cria novo tenant no IdP
- [ ] Timeout, retry com backoff e circuit breaker configurados no adapter
- [ ] Log estruturado de cada passo da saga com `correlationId` e `slug`
- [ ] Coverage ≥ 70%

---

### TASK-14 — IBrandingAssetStorage (GCS) e ICdnInvalidator

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/tenant-administration/14-infra-gcs-cdn` |
| **Worktree** | `git worktree add ../worktrees/tenant-administration/14-infra-gcs-cdn -b feat/tenant-administration/14-infra-gcs-cdn` |
| **Status** | [ ] |
| **Depende de** | TASK-13 |
| **Entregável** | `GcsBrandingAssetStorage` implementando `IBrandingAssetStorage`; `CloudCdnInvalidator` implementando `ICdnInvalidator`; cache `slug→tenantId` em Memorystore (Redis) |
| **Mapeia** | Req 7, Req 9, RNF 4; design.md §6.2, §6.4, DD-006, DD-007 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar o storage de assets (logo e favicon) no GCS com bucket não público, URLs de leitura via CDN e upload com Service Account (Workload Identity Federation). Implementar `CloudCdnInvalidator` que dispara invalidação de cache do `brand.json` após `BrandingChanged`. Implementar cache `slug→tenantId` em Memorystore com TTL curto e invalidação em provisionamento (DD-006).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de `GcsBrandingAssetStorage` com fake GCS (ou Testcontainers): upload retorna URL pública CDN; upload de arquivo duplicado não falha (idempotente por path); escrever testes de `CloudCdnInvalidator`: chamada de invalidação é feita com URL correta do `brand.json`
- [ ] **ST-02 — Green:** implementar `GcsBrandingAssetStorage.UploadAsync(tenantId, type, stream)` retornando CDN URL; implementar `CloudCdnInvalidator.InvalidateAsync(slug)`; implementar `SlugTenantIdCache` com `IDistributedCache` (Redis) e TTL configurável
- [ ] **ST-03 — Refactor:** garantir que content-type real é verificado via magic bytes antes do upload (DD-007); garantir que bucket GCS nunca é público; garantir que path inclui `tenants/{slug}/{type}` para segregação por tenant
- [ ] **ST-04 — Encerramento:** coverage ≥ 70%; commit `feat(tenant-administration): GcsBrandingAssetStorage CloudCdnInvalidator slug-cache` + push

#### Critérios de Aceite

- [ ] Upload via GCS com path `tenants/{slug}/logo` e `tenants/{slug}/favicon`
- [ ] CDN URL retornada ao caller; bucket não exposto diretamente
- [ ] `ICdnInvalidator.InvalidateAsync` chamado com slug correto
- [ ] Cache `slug→tenantId` com TTL e invalidação em provisionamento
- [ ] Coverage ≥ 70%

---

### TASK-15 — Outbox, IEventOutbox e publicador Pub/Sub

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/tenant-administration/15-infra-outbox-pubsub` |
| **Worktree** | `git worktree add ../worktrees/tenant-administration/15-infra-outbox-pubsub -b feat/tenant-administration/15-infra-outbox-pubsub` |
| **Status** | [ ] |
| **Depende de** | TASK-14 |
| **Entregável** | `outbox_events` escrito na mesma transação do agregado; publicador background que lê `pending` e publica no Pub/Sub `azim-tenants`; retry + DLQ; envelope TRD completo |
| **Mapeia** | Req 11, RNF 5; design.md §6.3, §6.6, §9 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar o padrão Outbox: `IEventOutbox.AppendAsync(domainEvent)` escreve em `outbox_events` na mesma transação do estado do agregado (garantido pelo `TransactionBehavior`). O publicador background (`OutboxPublisher`) lê eventos `pending`, serializa para o envelope TRD (com `event_id`, `event_type`, `correlation_id`, `tenant_id`, `aggregate_type`, `aggregate_id`, `payload`), publica no Pub/Sub `azim-tenants`, marca `published`. Após N retries → `failed` + alerta.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de integração com Testcontainers: após `ProvisionTenantHandler` (com saga mockada), `outbox_events` contém `tenant.provisioned.v1` com `correlation_id` não-nulo; `OutboxPublisher` lê e publica (fake Pub/Sub); evento marcado `published` após publicação; após 3 falhas consecutivas → `failed`
- [ ] **ST-02 — Green:** implementar `OutboxRepository` (INSERT + SELECT FOR UPDATE SKIP LOCKED para concorrência); implementar `PubSubEventPublisher` com serialização JSON do envelope TRD; implementar `OutboxPublisher` como `IHostedService` com polling configurável; mapear domain events para tipos de evento de integração (`TenantProvisioned` → `tenant.provisioned.v1`)
- [ ] **ST-03 — Refactor:** garantir que `outbox_events` usa `retry_count` e `last_error`; configurar DLQ `azim-tenants-dlq` no Pub/Sub; garantir que `adminEmail` é mascarado no payload publicado (LGPD — design.md §10)
- [ ] **ST-04 — Encerramento:** testes de integração verdes; coverage ≥ 70%; commit `feat(tenant-administration): Outbox IEventOutbox PubSub publisher envelope` + push

#### Critérios de Aceite

- [ ] `outbox_events` escrito na mesma transação do agregado (Req 11.4)
- [ ] Envelope TRD completo: `event_id`, `event_type`, `correlation_id`, `tenant_id`, `aggregate_type`, `payload`
- [ ] `adminEmail` não aparece no payload do evento publicado
- [ ] Após N falhas → `failed` + métrica `outbox_failed_total` incrementada
- [ ] Coverage ≥ 70%

---

### TASK-16 — Testes de isolamento RLS e PBT-06 gate de CI

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `test/tenant-administration/16-infra-rls-pbt06` |
| **Worktree** | `git worktree add ../worktrees/tenant-administration/16-infra-rls-pbt06 -b test/tenant-administration/16-infra-rls-pbt06` |
| **Status** | [ ] |
| **Depende de** | TASK-15 |
| **Entregável** | Suíte de testes de isolamento anti-cross-tenant; PBT-06 executado por Testcontainers; configurado como gate obrigatório de CI com 100% de aprovação (RNF 1.1) |
| **Mapeia** | Req 10, RNF 1; PBT-06; design.md §14 |
| **Camada principal** | Tests / Infrastructure |

#### Objetivo

PBT-06 é a propriedade de segurança mais crítica do módulo: para qualquer par de tenants A e B distintos, nenhuma query em contexto de A retorna dados de B. Esta TASK cria a suíte completa com gerador de pares de tenants, execução de queries via EF Core e via SQL direto, e verificação de ausência de dados cruzados. O CI deve falhar imediatamente se qualquer asserção deste grupo falhar.

#### Subtasks

- [ ] **ST-01 — Red:** escrever PBT-06 com Testcontainers PostgreSQL: gerador de N tenants com brandings distintos; para cada par (A, B): executar `GetTenantBrandingQuery` em contexto A; verificar que resultado não contém nenhum dado de B; repetir com query EF Core e SQL direto com `SET app.current_tenant`
- [ ] **ST-02 — Green:** resolver falhas de isolamento identificadas nos testes (se houver); garantir que global query filter e RLS ambos rejeitam acesso cruzado; implementar helper `WithTenantContext(tenantId)` para os testes
- [ ] **ST-03 — Refactor:** garantir que PBT-06 usa `[Trait("Category", "SecurityGate")]` para identificação no CI; configurar pipeline CI para falhar a build se qualquer teste `SecurityGate` falhar, mesmo com outros testes passando
- [ ] **ST-04 — Docs:** registrar no README do módulo que PBT-06 é gate obrigatório de CI (RNF 1.1, KPI-06)
- [ ] **ST-05 — Encerramento:** PBT-06 verde; CI configurado com gate `SecurityGate`; commit `test(tenant-administration): PBT-06 anti-cross-tenant RLS isolation gate` + push

#### Critérios de Aceite

- [ ] PBT-06 verde: nenhum par (A, B) retorna dados cruzados em nenhuma query
- [ ] Gate de CI configurado: falha em `[SecurityGate]` interrompe a build
- [ ] Testes cobrem tanto EF Core global filter quanto RLS direto no PostgreSQL
- [ ] Alerta `tenant_rls_violation_count > 0` documentado para fase de hardening

---

### TASK-17 — Endpoints de plataforma (provision, suspend, reactivate)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `feat/tenant-administration/17-api-platform` |
| **Worktree** | `git worktree add ../worktrees/tenant-administration/17-api-platform -b feat/tenant-administration/17-api-platform` |
| **Status** | [ ] |
| **Depende de** | TASK-16 |
| **Entregável** | `POST /api/v1/platform/tenants`, `POST /api/v1/platform/tenants/{tenantId}/suspend`, `POST /api/v1/platform/tenants/{tenantId}/reactivate` com autorização `PlatformOperator`, mapeamento ao catálogo de erros e testes de contrato |
| **Mapeia** | Req 1, Req 4, RNF 7; design.md §8.1 |
| **Camada principal** | Api |

#### Objetivo

Implementar os três endpoints do plano de plataforma. `AuthorizationBehavior` bloqueia qualquer papel que não seja `PlatformOperator`. O controller mapeia commands MediatR, trata `Result` com `application/problem+json` e retorna os HTTP status codes corretos conforme design.md §8.1. Testes de API verificam que TAdmin recebe `403` ao chamar esses endpoints.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de contrato de API (WebApplicationFactory): `POST /platform/tenants` com corpo válido retorna `201` com `tenantId` e `slug`; slug duplicado retorna `409` com código `TA-ERR-002`; `slugConfirmation` divergente retorna `422`; chamada por TAdmin retorna `403`; `POST /platform/tenants/{id}/suspend` em suspenso retorna `409` com `TA-ERR-007`
- [ ] **ST-02 — Green:** implementar `PlatformTenantController` com os três endpoints; mapear results para HTTP usando `IProblemDetailsFactory`; configurar roteamento `/api/v1/platform/tenants`
- [ ] **ST-03 — Refactor:** garantir que `adminEmail` não aparece na resposta de nenhum endpoint; garantir que cabeçalho `Idempotency-Key` é lido e passado ao command; garantir que todos os erros usam `application/problem+json` com campo `errorCode` do catálogo
- [ ] **ST-04 — Encerramento:** coverage API ≥ 80%; commit `feat(tenant-administration): platform tenant API provision suspend reactivate` + push

#### Critérios de Aceite

- [ ] `POST /api/v1/platform/tenants` retorna `201` com `tenantId` e `slug` em sucesso
- [ ] Todos os erros retornam `application/problem+json` com `errorCode` do catálogo §12
- [ ] TAdmin recebe `403` ao chamar qualquer endpoint `/platform/*`
- [ ] `Idempotency-Key` é lido do header e repassado ao command
- [ ] Coverage ≥ 80%

---

### TASK-18 — Endpoints de tenant (GET/PATCH tenant, GET/PUT branding)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `feat/tenant-administration/18-api-tenant` |
| **Worktree** | `git worktree add ../worktrees/tenant-administration/18-api-tenant -b feat/tenant-administration/18-api-tenant` |
| **Status** | [ ] |
| **Depende de** | TASK-17 |
| **Entregável** | `GET /api/v1/tenant`, `PATCH /api/v1/tenant`, `GET /api/v1/tenant/branding`, `PUT /api/v1/tenant/branding` com autorização correta, isolamento por tenant e testes de contrato |
| **Mapeia** | Req 3, Req 5, Req 6, Req 7, Req 9, Req 10, RNF 1; design.md §8.2 |
| **Camada principal** | Api |

#### Objetivo

Implementar os quatro endpoints do plano de tenant. `PATCH /api/v1/tenant` nunca aceita `slug` no corpo (rejeita com `TA-ERR-011`). `PUT /api/v1/tenant/branding` aceita `multipart/form-data`. Testes verificam isolamento: TAdmin do tenant A não recebe dados do tenant B ao chamar `GET /api/v1/tenant/branding`.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de contrato: `GET /tenant` retorna `slug`, `displayName`, `timezone`, `digestTime`, `status` sem dados sensíveis; `PATCH /tenant` com `slug` no body retorna `422` com `TA-ERR-011`; `PUT /tenant/branding` com logo JPEG retorna `415` com `TA-ERR-013`; `PUT /tenant/branding` com cores de contraste insuficiente retorna `422` com `TA-ERR-014` e `contrastRatio`; TAdmin de tenant A não recebe branding de tenant B
- [ ] **ST-02 — Green:** implementar `TenantController` com os quatro endpoints; configurar `multipart/form-data` para `PUT /branding`; mapear todos os results para HTTP
- [ ] **ST-03 — Refactor:** garantir que `GET /tenant` não expõe `identityTenantId`; garantir que resposta de `PUT /branding` inclui `wcagContrastOk`, `contrastRatio`, `logoUrl`, `faviconUrl`, `derivedTones`
- [ ] **ST-04 — Encerramento:** coverage ≥ 80%; commit `feat(tenant-administration): tenant API CRUD branding digest-config` + push

#### Critérios de Aceite

- [ ] `PATCH /api/v1/tenant` com `slug` no corpo retorna `422` com `TA-ERR-011`
- [ ] Resposta de `PUT /branding` inclui `derivedTones` com todos os tons nomeados
- [ ] Isolamento verificado: TAdmin A não recebe dados de B via API
- [ ] Coverage ≥ 80%

---

### TASK-19 — Endpoint público brand.json + CDN cache + anti-enumeração

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `feat/tenant-administration/19-api-brand-json` |
| **Worktree** | `git worktree add ../worktrees/tenant-administration/19-api-brand-json -b feat/tenant-administration/19-api-brand-json` |
| **Status** | [ ] |
| **Depende de** | TASK-18 |
| **Entregável** | `GET /brand/{slug}/brand.json` público com `Cache-Control`, `ETag`, payload completo e anti-enumeração (slug inexistente = suspenso = `404 TA-ERR-008`) |
| **Mapeia** | Req 9, RNF 1 (anti-enumeração), RNF 4; design.md §8.3 |
| **Camada principal** | Api |

#### Objetivo

Implementar o endpoint público sem autenticação que serve o payload `brand.json` cacheável. `Cache-Control: public, max-age=300` e `ETag` baseado em `updatedAt`. Não distinguir entre slug inexistente e tenant suspenso (anti-enumeração, RNF 1). Verificar que após `BrandingChanged` a CDN invalida em < 30 s (testado com fake de CDN). Payload completo conforme design.md §8.3.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes: slug existente retorna `200` com todos os campos do payload (incluindo `derivedTones` e `wcagContrastOk`); slug inexistente retorna `404` com `TA-ERR-008`; slug de tenant suspenso retorna `404` com mesmo `TA-ERR-008` (anti-enumeração); resposta inclui `Cache-Control: public, max-age=300` e `ETag`; chamada com `If-None-Match` igual ao ETag retorna `304`
- [ ] **ST-02 — Green:** implementar `BrandController` com `GET /brand/{slug}/brand.json`; sem `[Authorize]`; retornar `ETag` derivado de hash de `updatedAt`; implementar `304 Not Modified` com `If-None-Match`
- [ ] **ST-03 — Refactor:** garantir que payload não expõe `adminEmail`, `identityTenantId`, `displayName` completo nem qualquer dado do plano de plataforma; verificar que o mesmo `TA-ERR-008` é retornado para inexistente e suspenso
- [ ] **ST-04 — Encerramento:** coverage ≥ 80%; commit `feat(tenant-administration): brand.json public endpoint CDN cache anti-enumeration` + push

#### Critérios de Aceite

- [ ] Slug suspenso e slug inexistente retornam idêntico `404 TA-ERR-008` (anti-enumeração)
- [ ] `Cache-Control: public, max-age=300` e `ETag` presentes
- [ ] `304 Not Modified` retornado corretamente com `If-None-Match`
- [ ] Payload inclui `slug`, `logoUrl`, `faviconUrl`, `colors`, `derivedTones`, `wcagContrastOk`, `version`
- [ ] Coverage ≥ 80%

---

### TASK-20 — Contract tests de eventos e OpenAPI

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `test/tenant-administration/20-contract-events-openapi` |
| **Worktree** | `git worktree add ../worktrees/tenant-administration/20-contract-events-openapi -b test/tenant-administration/20-contract-events-openapi` |
| **Status** | [ ] |
| **Depende de** | TASK-19 |
| **Entregável** | Contract tests dos quatro eventos `tenant.*`; esquema OpenAPI gerado e validado; DTOs em `TenantAdministration.Contracts` documentados |
| **Mapeia** | Req 11, RNF 5; design.md §9 |
| **Camada principal** | Contracts / Tests |

#### Objetivo

Verificar que os eventos publicados no Pub/Sub conformam ao envelope TRD: `event_id`, `event_type`, `event_version`, `occurred_at`, `correlation_id`, `tenant_id`, `aggregate_type`, `aggregate_id`, `producer`, `payload`. Cada evento (`tenant.provisioned.v1`, `tenant.suspended.v1`, `tenant.reactivated.v1`, `tenant.branding_changed.v1`) deve ter schema JSON validado. OpenAPI do módulo gerado sem divergência de contrato.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de schema JSON para cada envelope de evento: deserializar payload capturado de Outbox e verificar que todos os campos obrigatórios do TRD estão presentes; verificar que `adminEmail` não aparece em nenhum payload; escrever snapshot test do OpenAPI gerado
- [ ] **ST-02 — Green:** ajustar serialização dos eventos para garantir conformidade com envelope TRD; adicionar atributos OpenAPI (`[ProducesResponseType]`, `[ApiVersion]`) nos controllers; gerar `openapi.json` em build
- [ ] **ST-03 — Refactor:** garantir que `TenantAdministration.Contracts` exporta todos os DTOs públicos sem referência a EF Core ou GCP SDK; remover qualquer campo `internal`/`set` dos DTOs públicos
- [ ] **ST-04 — Encerramento:** contract tests verdes; snapshot OpenAPI salvo em `docs/`; commit `test(tenant-administration): contract tests events OpenAPI schema` + push

#### Critérios de Aceite

- [ ] Schema dos quatro eventos validado com todos os campos do envelope TRD
- [ ] `adminEmail` ausente em todos os payloads de evento
- [ ] OpenAPI gerado sem quebra de contrato (snapshot test verde)
- [ ] `TenantAdministration.Contracts` sem dependências de infraestrutura

---

### TASK-21 — Segurança: bloqueio PlatOp, sanitização SVG e auditoria

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `feat/tenant-administration/21-hardening-security` |
| **Worktree** | `git worktree add ../worktrees/tenant-administration/21-hardening-security -b feat/tenant-administration/21-hardening-security` |
| **Status** | [X] |
| **Depende de** | TASK-20 |
| **Entregável** | Testes de bloqueio de PlatOp a dados comerciais; sanitização SVG verificada por teste; auditoria append-only de provisionamento e branding validada; grants de banco revisados |
| **Mapeia** | RNF 5, RNF 7; design.md §10, §14, DD-001, DD-007 |
| **Camada principal** | Infrastructure / Tests |

#### Objetivo

Consolidar as verificações de segurança que não foram cobertas em TASKs anteriores: (1) verificar por teste de API que PlatOp recebe `403` ao acessar qualquer rota de dados comerciais de tenant; (2) verificar que auditoria append-only (`audit_logs`) registra `TenantProvisioned` e `BrandingChanged` com `tenant_id` e ator — e que registros não podem ser deletados; (3) verificar sanitização SVG com payload malicioso real.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes: PlatOp autenticado tenta `GET /api/v1/tenant` → `403`; PlatOp tenta `GET /api/v1/tenant/branding` → `403`; SVG com `<script>alert(1)</script>` é rejeitado ou sanitizado antes do upload; `audit_logs` contém entrada após provisionamento com `tenant_id`, `slug` e `actor`; tentativa de DELETE em `audit_logs` é bloqueada pela role de banco
- [ ] **ST-02 — Green:** ajustar `AuthorizationBehavior` se qualquer falha for encontrada; ajustar sanitizador SVG se SVG malicioso passar; verificar que role de banco de tenant não tem `DELETE` em `audit_logs`
- [ ] **ST-03 — Refactor:** revisar grants de banco conforme DD-001 (duas roles); documentar no README do módulo a separação de roles; garantir que acesso de suporte autorizado gera entrada de auditoria (RNF 7.2)
- [ ] **ST-04 — Encerramento:** todos os testes de segurança verdes; commit `fix(tenant-administration): hardening security PlatOp block SVG sanitization audit` + push

#### Critérios de Aceite

- [ ] PlatOp não acessa `GET /api/v1/tenant` nem `GET /api/v1/tenant/branding` (recebe `403`)
- [ ] SVG com `<script>` é bloqueado antes do armazenamento
- [ ] `audit_logs` contém entrada de provisionamento e branding com `tenant_id` e `actor`
- [ ] Role de banco de tenant não tem `DELETE` em `audit_logs`

---

### TASK-22 — Observabilidade: métricas, alertas, health checks e DoD final

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `feat/tenant-administration/22-hardening-observability` |
| **Worktree** | `git worktree add ../worktrees/tenant-administration/22-hardening-observability -b feat/tenant-administration/22-hardening-observability` |
| **Status** | [X] |
| **Depende de** | TASK-21 |
| **Entregável** | Todas as métricas do design.md §11 implementadas; alertas de falha de provisionamento e RLS configurados; health checks ativos; DoD completo verificado; README do módulo sincronizado |
| **Mapeia** | RNF 6; design.md §11 |
| **Camada principal** | Infrastructure / DevOps |

#### Objetivo

Fechar o DoD do design.md §20: implementar as métricas `tenant_provisioned_total`, `tenant_provisioning_failed_total`, `tenant_branding_update_total`, `tenant_wcag_reject_total`, `tenant_provisioning_duration_seconds`, `cdn_invalidation_total`. Configurar alertas para falha de provisionamento (evento crítico — RNF 6.2) e `tenant_rls_violation_count > 0` (sev-1). Implementar `/health/live` e `/health/ready` com check de Cloud SQL, Pub/Sub e Identity Platform.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de integração: após provisionamento com falha, `tenant_provisioning_failed_total` incrementa; `/health/ready` retorna `503` quando Cloud SQL não está disponível (Testcontainers com kill de container); `/health/live` retorna `200` independente de Pub/Sub
- [ ] **ST-02 — Green:** implementar contadores com `System.Diagnostics.Metrics` (OpenTelemetry); implementar `/health/live` e `/health/ready` com checks configuráveis; configurar exportação para Cloud Monitoring
- [ ] **ST-03 — Refactor:** garantir que `correlation_id` e `tenant_id` são atributos em todos os spans OpenTelemetry do módulo (ADR-0009); garantir que `adminEmail` é mascarado em todos os logs de plataforma; verificar DoD design.md §20 item a item
- [ ] **ST-04 — Docs:** sincronizar README do módulo com status final: versão das specs, status das ondas, links para requirements/design/tasks
- [ ] **ST-05 — Encerramento:** todos os health checks ativos; métricas expostas; DoD verificado; commit `feat(tenant-administration): observability metrics alerts health-checks DoD` + push

#### Critérios de Aceite

- [ ] `tenant_provisioning_failed_total` incrementa em falha de provisionamento
- [ ] `/health/ready` retorna `503` quando Cloud SQL indisponível
- [ ] Alerta de `tenant_rls_violation_count > 0` documentado e configurado
- [ ] Todos os itens do DoD design.md §20 verificados e marcados
- [ ] README do módulo sincronizado com status final

---

## 5. Matriz de Rastreabilidade

| Origem | Descrição curta | TASKs | Status |
|--------|-----------------|-------|--------|
| Req 1 | Provisionar novo tenant (PlatOp, slug, displayName, timezone) | TASK-08, TASK-13, TASK-17 | [X] |
| Req 2 | Slug único global e imutável | TASK-02, TASK-05, TASK-12, TASK-17 | [X] |
| Req 3 | Fuso horário IANA e horário do digest | TASK-02, TASK-11, TASK-18 | [X] |
| Req 4 | Suspensão e reativação do tenant | TASK-05, TASK-09, TASK-17 | [X] |
| Req 5 | Branding white-label estrito (logo, favicon, cores) | TASK-04, TASK-10, TASK-18 | [X] |
| Req 6 | Validação de contraste WCAG 2.1 AA | TASK-03, TASK-10, TASK-18 | [X] |
| Req 7 | Formato e tamanho de logo e favicon | TASK-04, TASK-10, TASK-14 | [X] |
| Req 8 | Derivação determinística de tons | TASK-03, TASK-11, TASK-19 | [X] |
| Req 9 | Exposição de branding cacheável (brand.json) | TASK-11, TASK-14, TASK-19 | [X] |
| Req 10 | Isolamento estrito por tenant_id (RLS) | TASK-12, TASK-16, TASK-18 | [X] |
| Req 11 | Publicação de TenantProvisioned e BrandingChanged | TASK-06, TASK-15, TASK-20 | [X] |
| Req 12 | Atomicidade do provisionamento com IdP | TASK-13 | [X] |
| RNF 1 | Isolamento multi-tenant com defesa em profundidade | TASK-12, TASK-16, TASK-21 | [X] |
| RNF 2 | Validação de contraste WCAG 2.1 AA (determinismo) | TASK-03 | [X] |
| RNF 3 | Provisionamento como rotina de aplicação (≤ 30 min) | TASK-13 | [X] |
| RNF 4 | Propagação do branding em < 30 s | TASK-14, TASK-19 | [X] |
| RNF 5 | Auditoria imutável de provisionamento e branding | TASK-15, TASK-21 | [X] |
| RNF 6 | Observabilidade: logs, métricas, alertas, traces | TASK-07, TASK-22 | [X] |
| RNF 7 | Bloqueio de PlatOp a dados comerciais | TASK-07, TASK-12, TASK-21 | [X] |
| PBT-01 | Imutabilidade e unicidade global do slug | TASK-02 | [X] |
| PBT-02 | Normalização e formato do slug | TASK-02 | [X] |
| PBT-03 | White-label estrito — conjunto de campos persistidos | TASK-04 | [X] |
| PBT-04 | Monotonicidade da validação de contraste WCAG AA | TASK-03 | [X] |
| PBT-05 | Determinismo dos tons derivados | TASK-03 | [X] |
| PBT-06 | Isolamento estrito por tenant — gate obrigatório de CI | TASK-16 | [X] |
| PBT-07 | Atomicidade do provisionamento (ambos-ou-nenhum) | TASK-13 | [X] |
| PBT-08 | Transições válidas da state machine do tenant | TASK-05 | [X] |
| DD-001 | Acesso a `tenants` em dois planos (roles segregadas) | TASK-12, TASK-21 | [X] |
| DD-002 | `status` enum + projeção `active` | TASK-05, TASK-12 | [X] |
| DD-003 | Comparação de contraste com arredondamento a 2 casas | TASK-03 | [X] |
| DD-004 | Salvar branding com cores reprovadas bloqueia (fail-safe) | TASK-10, TASK-18 | [X] |
| DD-005 | Separação física `tenants`/`tenant_brandings` | TASK-12 | [X] |
| DD-006 | Cache `slug→tenantId` em Memorystore | TASK-14 | [X] |
| DD-007 | Sanitização SVG e validação de content-type real | TASK-04, TASK-21 | [X] |
| ADR-0001 | Multi-tenancy pooled DB + RLS | TASK-12, TASK-16 | [X] |
| ADR-0008 | Scheduling do digest por fuso IANA | TASK-02, TASK-11 | [X] |
| ADR-0009 | Propagação de `correlation_id` e `tenant_id` | TASK-07, TASK-22 | [X] |
| TA-ERR-001..014 | Catálogo de erros completo | TASK-17, TASK-18, TASK-19 | [X] |

---

## 6. Coverage Gates

| Camada | Projeto | Gate | Tipo de teste esperado |
|--------|---------|------|------------------------|
| Domain | `TenantAdministration.Domain.Tests` | ≥ 95% | Unitários de objetos de valor, agregado, policies, state machine; PBT-01..05, PBT-08 |
| Application | `TenantAdministration.Application.Tests` | ≥ 85% | Unitários de handlers, validators, behaviors, queries; PBT-07 |
| Infrastructure | `TenantAdministration.Infrastructure.Tests` | ≥ 70% | Integração com Testcontainers PostgreSQL, GCS fake, IdP fake; PBT-06 (gate CI) |
| Api | `TenantAdministration.Api.Tests` | ≥ 80% | Contrato REST (WebApplicationFactory), autorização por plano, anti-enumeração, isolamento |
| Architecture | `TenantAdministration.Architecture.Tests` | 100% das 3 regras | Testes de dependência entre camadas (NetArchTest/ArchUnitNET) |
| Security | `[Trait("Category","SecurityGate")]` | 100% de aprovação por build | PBT-06, bloqueio PlatOp, sanitização SVG, auditoria append-only |
| Contract | `TenantAdministration.Api.Tests` (subconjunto) | 100% dos 4 eventos | Schema JSON dos eventos `tenant.*` com envelope TRD |

Regras:

- Coverage gate não substitui qualidade de teste.
- PBT-06 com `[SecurityGate]` interrompe a build se qualquer asserção falhar.
- Migrations que tocam RLS exigem revisão de código obrigatória (RNF 1.3).
- Testes de idempotência são obrigatórios para `ProvisionTenantCommand`.

---

## 7. Critérios de Encerramento

### 7.1 Encerramento de TASK

Uma TASK só pode ser marcada `[X]` quando:

- Todas as subtasks estão `[X]`
- Testes aplicáveis verdes localmente
- Coverage gate da camada atendido ou justificativa explícita registrada
- `dotnet format` / lint executado sem novo warning
- Commit em Conventional Commits realizado
- Push da branch realizado
- Documentação atualizada quando aplicável

### 7.2 Encerramento de Onda

Uma onda só pode ser considerada concluída quando:

- Todas as TASKs da onda estão `[X]`
- CI verde (build + testes + architecture tests + security gate)
- PR da onda aberto, aprovado ou mergeado conforme regra do projeto
- Riscos da onda tratados ou registrados
- README do módulo sincronizado quando aplicável

Critérios específicos por onda:

| Onda | Critério adicional |
|------|--------------------|
| Onda 1 | Architecture.Tests verdes; solution compila |
| Onda 2 | PBT-01..05, PBT-08 verdes; coverage domain ≥ 95% |
| Onda 3 | Todos os handlers cobertos por testes unitários; coverage application ≥ 85% |
| Onda 4 | PBT-06 gate CI ativo; PBT-07 verde; Testcontainers RLS verde; coverage infra ≥ 70% |
| Onda 5 | Contract tests dos 4 eventos verdes; OpenAPI snapshot verde; coverage API ≥ 80% |
| Onda 6 | DoD design.md §20 verificado item a item; alertas configurados; README sincronizado |

### 7.3 Encerramento do Módulo

O módulo só pode ser considerado pronto quando:

- Todas as 6 ondas estão concluídas
- Matriz de rastreabilidade completa (todas as origens com status `[X]`)
- `requirements.md`, `design.md` e `tasks.md` consistentes e aprovados
- PBT-06 como gate obrigatório de CI ativo com 100% de aprovação
- Auditoria append-only de provisionamento e branding verificada
- Bloqueio de PlatOp a dados comerciais verificado
- `brand.json` servido por Cloud CDN com invalidação testada
- Eventos `tenant.*` publicados via Outbox com envelope TRD conforme
- Logs com `correlation_id` e `tenant_id`; alerta de falha de provisionamento ativo
- README do módulo atualizado com status `Aprovado para desenvolvimento` e links para as specs

---

## 8. Riscos de Execução

| Risco | Impacto | Probabilidade | Mitigação por TASK |
|-------|---------|---------------|--------------------|
| PBT-06 revela vazamento de dados entre tenants não detectado antes | Crítico — bloqueia entrega | Baixa | TASK-16 é gate obrigatório antes da Onda 5; PBT-06 deve ser a primeira evidência a ser executada |
| Saga de provisionamento inconsistente com IdP real vs fake | Alto | Média | TASK-13: PBT-07 com doubles; revisar comportamento de retry do GcpIdentityPlatformAdapter com IdP real em ambiente de staging |
| Comparação de contraste WCAG instável em limítrofes (ponto flutuante) | Médio | Média | TASK-03: DD-003 (`round(R,2)`) é obrigatório; testes de limítrofes 4.4/4.5/4.6 devem ser executados com valores exatos |
| SVG malicioso não filtrado servido via CDN | Alto (XSS) | Baixa | TASK-04 + TASK-21: sanitizador testado com payload real; content-type validado por magic bytes |
| Migration tocando RLS sem revisão obrigatória | Alto (isolamento) | Baixa | TASK-12: comentário explícito de revisão obrigatória na migration; RNF 1.3 |
| `brand.json` não invalida em < 30 s após BrandingChanged | Médio | Média | TASK-14 + TASK-19: métrica `cdn_invalidation_total` e teste com fake CDN; revisitar TTL se necessário |
| ADR-0009 (correlationId/tenantId) não criado bloqueando observabilidade | Baixo | Média | TASK-07 e TASK-22 podem avançar com convenção local; ADR-0009 deve ser criado antes da Onda 6 |

---

## 9. Referências

| Documento | Relação |
|-----------|---------|
| `docs/product/modules/tenant-administration/requirements.md` v0.1.0 | Base de requisitos: Req 1..12, RNF 1..7, PBT-01..08 |
| `docs/product/modules/tenant-administration/design.md` v0.1.0 | Base de design: DD-001..007, agregado Tenant, saga, catálogo de erros, DoD |
| `docs/product/modules/tenant-administration/README.md` | Responsabilidades, eventos, riscos, APIs do módulo |
| `docs/product/trd/trd.md` | Stack .NET 10, EF Core, GCP Identity Platform, Pub/Sub, Outbox, envelope de eventos |
| `docs/product/data-model/data-model.md` | Tabelas `tenants`, `tenant_brandings`, `outbox_events` |
| `docs/product/adr/ADR-0001` | Multi-tenancy pooled DB + RLS |
| `docs/product/adr/ADR-0008` | Scheduling do digest por fuso IANA |
| `docs/product/adr/ADR-0009` (a criar) | Propagação de `correlation_id` e `tenant_id` |
| `.forge/rules/domain/audit-immutability.md` | Auditoria append-only |
| `.forge/rules/architecture/observability.md` | `correlation_id` e `tenant_id` em logs e traces |
