# PROGRESS-TRACKING — audit-log

> Tracker de execução do `/forge:coding-loop audit-log`. Fonte de verdade do estado de codificação.
> Legenda: `[ ]` pendente · `[-]` em progresso · `[X]` concluído (build + testes OK) · `[!]` falhou (HALT).

- Módulo: `audit-log` (Generic Subdomain — Sprint 1)
- tasks.md base: v0.1.1 (Aprovado para desenvolvimento)
- Raiz da solution: `services/audit-log/`
- Início da execução: 2026-06-12

## Ondas

| Onda | Foco | TASKs | Branch | Status |
|------|------|-------|--------|--------|
| 1 | Bootstrap | TASK-01, TASK-02 | `feat/audit-log/wave-01` | [X] |
| 2 | Domínio | TASK-03..06 | `feat/audit-log/wave-02` | [X] |
| 3 | Application | TASK-07..10 | `feat/audit-log/wave-03` | [X] |
| 4 | Infrastructure | TASK-11..14 | `feat/audit-log/waves-4-6` | [X] |
| 5 | API + Contracts | TASK-15..17 | `feat/audit-log/waves-4-6` | [X] |
| 6 | Hardening | TASK-18..20 | `feat/audit-log/waves-4-6` | [X] |

> **Estratégia (ajuste 12/06):** Waves 4-6 executadas numa branch contínua `feat/audit-log/waves-4-6`, com **um único PR ao fim da fase** (decisão de Milton). Docker confirmado rodando (Testcontainers Wave 4).

## Wave 1 — Bootstrap (concluída ✅)

🌿 Worktree: `.forge/worktrees/audit-log/wave-01` — branch `feat/audit-log/wave-01`
🧪 33 testes verdes (Application 21 + Architecture 12) · build limpo (0 warn / 0 err) · NetArchTest valida regra de dependência.

| TASK | Título | Specialist | Status | Commit |
|------|--------|-----------|--------|--------|
| TASK-01 | Bootstrap solution e 5 projetos Clean Architecture | backend-engineer-dotnet | [X] | `9544bd9` |
| TASK-02 | Contratos públicos em AuditLog.Contracts | backend-engineer-dotnet | [X] | `9d5ac58` |

**Notas de implementação (divergências aceitas):**
- Solution gerada como `AuditLog.slnx` (formato sucessor nativo do SDK 10; aceito por `dotnet build/test/sln`).
- `AuditAction` sem `[JsonConverter]` no tipo: serialização lowercase (`create`/`update`/`delete`) garantida via `JsonStringEnumConverter` + `JsonNamingPolicy.CamelCase` **a configurar no host da Api (Wave 5)**. Coberto por teste documental.
- EF Core 9.0.6 (LTS, compatível com net10.0; EF Core 10 sem release estável no momento).

## Wave 2 — Domínio (concluída ✅)

🌿 Worktree: `.forge/worktrees/audit-log/wave-02` — branch `feat/audit-log/wave-02`
🧪 127 testes verdes (Domain 94 + Application 21 + Architecture 12) · build limpo · NetArchTest intacto · PBT-03 e PBT-04 com 200 amostras (FsCheck 2.16.6).

| TASK | Título | Specialist | Status | Commit |
|------|--------|-----------|--------|--------|
| TASK-03 | Objetos de valor do domínio (6 VOs) | backend-engineer-dotnet | [X] | `f11d0e1` |
| TASK-04 | Aggregate root AuditLog + IAuditLogRepository | backend-engineer-dotnet | [X] | `20a1787` |
| TASK-05 | PiiMasker + PiiFieldPolicy + PBT-04 | backend-engineer-dotnet | [X] | `3becfe4` |
| TASK-06 | PBT-03 Round-trip do delta | backend-engineer-dotnet | [X] | `654fe6f` |

**Notas:**
- Coverage de linha Domain ≥ 95% (critério atendido); branch coverage 78.7% — diferença em branches de null-check geradas pelo compilador para `private init` do record `AuditDelta`.
- ✅ **Dívida resolvida** (`ba0dc65`): bypass `AuditDelta.ForMaskedUpdate` (internal) removido. Substituído por `AuditDelta.TransformChanges(Func<...>)` — transformação pós-construção que aplica o mascaramento sobre um delta já validado, sem re-executar o guard `before != after`. O guard de "mudança real" (REQ-003.4) passa a ser avaliado sobre os valores originais, antes do mascaramento. Teste explícito adicionado para o caso PII-distintos→mesmo-marcador. 128 testes verdes.

## Wave 3 — Application (concluída ✅)

🌿 Worktree: `.forge/worktrees/audit-log/wave-03` — branch `feat/audit-log/wave-03`
🧪 217 testes verdes (Domain 95 + Application 110 + Architecture 12) · build limpo · NetArchTest intacto · PBT-02 com 200 amostras.

| TASK | Título | Specialist | Status | Commit |
|------|--------|-----------|--------|--------|
| TASK-07 | RecordAuditEntryCommand + AuditService handler + IAuditWriter impl | backend-engineer-dotnet | [X] | `7ed1e1d` |
| TASK-08 | Queries de consulta + handlers + validators | backend-engineer-dotnet | [X] | `06c02fb` |
| TASK-09 | Pipeline behaviors (Validation→Tenant→Authorization→Logging) | backend-engineer-dotnet | [X] | `10c284c` |
| TASK-10 | PBT-02 Conservação (Application.Tests) | backend-engineer-dotnet | [X] | `0d648ef` |

**Abstrações introduzidas (Application):** `ITenantContext`, `IUserContext`, `IBuScopeResolver`, `IAuditMetrics`, `IEntityContextRequest`, `IAuditQuery` — implementações concretas virão nas Waves 4 (Infra) / 5 (Api).

**Risco documentado — RISK-AUDIT-05:** `ListAuditLogsHandler` para `GestorBU` resolve escopo de BU iterando entidades (N+1) — aceito como MVP; Wave 4+ deve adicionar `ListAsync(entityIds)` em lote no repositório.

## Wave 4 — Infrastructure (concluída ✅)

🧪 253 testes verdes (Domain 95 + Application 110 + Architecture 12 + **Infrastructure 36 com Testcontainers PostgreSQL real**) · 0 ignorados.

| TASK | Título | Status | Commit |
|------|--------|--------|--------|
| TASK-11 | AuditLogDbContext + AuditLogRepository + mapeamento EF Core | [X] | `10d5416` |
| TASK-12 | Migration append-only (trigger + REVOKE + RLS + 3 índices) | [X] | `0b5c178` |
| TASK-13 | PBT-01 imutabilidade + PBT-05 isolamento (Testcontainers, ≥50) | [X] | `12f97b9` |
| TASK-14 | Integração fail-closed DD-001 + PBT-06 idempotência (≥100) | [X] | `3d528e0` |
| — | cleanup: remove interceptor inativo | [X] | `9cf433d` |

**Dívida técnica:** o aggregate `AuditLog` ganhou backing fields públicos `EntityTypePersisted`/`EntityIdPersisted` (mapeáveis pelo EF Core), com `EntityReference` virando propriedade computada — vazamento de concern de persistência no domínio (alternativa evitada: `InternalsVisibleTo`). Revisitar na fase de hardening/refino.

## Wave 5 — API + Contracts (concluída ✅)

🧪 296 testes verdes (Domain 95 + Application 110 + Architecture 12 + Infrastructure 36 + **Api 43**) · Api coverage ~83%.

| TASK | Título | Status | Commit |
|------|--------|--------|--------|
| TASK-15 | AuditLogQueryController (GET) + DTOs + JWT + 405 em escrita | [X] | `4530547` |
| TASK-16 | Catálogo AUD-ERR-001..008 + IExceptionHandler + anti-enumeração + OpenAPI | [X] | `dcda02c` |
| TASK-17 | RBAC (TAdmin/GestorBU/403) + contrato IAuditWriter | [X] | `b9bbfd4` |

**⚠️ RISCO DE SEGURANÇA EM ABERTO — RISK-AUDIT-05:** `StubBuScopeResolver` retorna escopo irrestrito (null). Enquanto ativo, **`GestorBU` enxerga todos os registros do tenant**, não apenas das suas BUs (REQ-008.3 parcialmente atendido: estrutura RBAC presente, filtro de BU efetivo pendente). O filtro real depende do read model do `organization` (planejado Sprint 7 no backlog). **NÃO promover a produção sem implementar `IBuScopeResolver` real.** Estrutura/contrato prontos; só a resolução efetiva está stubbed.

## Wave 6 — Hardening (concluída ✅)

🧪 Verificação final: **314 testes verdes** (Domain 95 + Application 117 + Api 51 + Infrastructure 39 + Architecture 12) · 0 ignorados · PII scan self-test OK.

| TASK | Título | Status | Commit |
|------|--------|--------|--------|
| TASK-18 | 5 métricas OTel + health check (3 estados) + traces + 3 alertas | [X] | `3ae5487` |
| TASK-19 | PII scan no CI (gate) + testes de segurança | [X] | `96425b6` |
| TASK-20 | DoD §19 + matriz de rastreabilidade + README | [X] | `3878245` |

---

## 🎯 Módulo audit-log — 6/6 ondas concluídas

Todas as 20 TASKs implementadas com TDD; 6 PBTs verdes; build limpo; 314 testes.
**Código nas Waves 4-6 está no PR #4** (https://github.com/MiltonSilvaJr/azim-crm/pull/4) — aguardando code-evaluator (CI) + merge. Waves 1-3 já em `develop` (PRs #1, #2, #3 mergeados).

**Riscos/dívidas que acompanham o módulo para as próximas fases:**
- **RISK-AUDIT-05 (segurança):** `StubBuScopeResolver` irrestrito — `GestorBU` vê todo o tenant. Bloqueia produção; depende do read model do `organization` (Sprint 7).
- Aggregate com backing fields públicos para EF Core (vazamento de persistência no domínio).
- VAL-AUDIT-01 (retenção LGPD / ADR-0003) e VAL-AUDIT-03 (SOX comissão) diferidos com justificativa.
- Jira AZIM-96..AZIM-115 ainda não sincronizados (MCP atlassian `Pending approval`).

## Última falha

_(nenhuma)_
