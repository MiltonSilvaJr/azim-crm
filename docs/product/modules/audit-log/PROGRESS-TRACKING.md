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
| 2 | Domínio | TASK-03..06 | `feat/audit-log/wave-02` | [ ] |
| 3 | Application | TASK-07..10 | `feat/audit-log/wave-03` | [ ] |
| 4 | Infrastructure | TASK-11..14 | `feat/audit-log/wave-04` | [ ] |
| 5 | API + Contracts | TASK-15..17 | `feat/audit-log/wave-05` | [ ] |
| 6 | Hardening | TASK-18..20 | `feat/audit-log/wave-06` | [ ] |

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

## Última falha

_(nenhuma)_
