# PROGRESS-TRACKING — authentication

> Tracker do `/forge:coding-loop authentication` (modo autônomo).
> Legenda: `[ ]` pendente · `[-]` em progresso · `[X]` concluído · `[!]` falhou (HALT).

- Módulo: `authentication` (BC-12, Generic — Sprint 2/3/5)
- tasks.md base: v0.1.1 (Aprovado para desenvolvimento)
- Raiz da solution: `services/authentication/`
- Estratégia: branch contínua `feat/authentication/all-waves`, PR único ao fim, merge autônomo após verificação verde (incl. `git status` do worktree limpo).

## Ondas

| Onda | Foco | TASKs | Status |
|------|------|-------|--------|
| 1 | Bootstrap | TASK-01..02 | [X] (`9e38bb5`,`388abdd`) — 14 testes Architecture |
| 2 | Domain | TASK-03..04 | [X] (`f51024f`,`eb36bf5`) — 72 Domain + PBT-01 |
| 3 | Application | TASK-05..09 | [X] (5 commits) — 33 App + PBT-02/03/04/05 |
| 4 | Infrastructure (Firebase adapter) | TASK-10..14 | [X] (5 commits) — 30 Infra (Testcontainers Redis+PG) |
| 5 | API + Contracts | TASK-15..20 | [X] (6 commits) — 54 Api + PBT-03/04/05 API |
| 6 | Hardening | TASK-21..25 | [X] (5 commits) — health/Serilog/métricas/audit/DoD |

> **🎯 Módulo completo — 6/6 ondas, 25 TASKs, 235 testes verdes** (Domain 72 + Application 39 + Api 59 + Infrastructure 51 c/ Testcontainers + Architecture 14). 5 PBTs. PR #6.
> Decisão audit-log: `IAuditEventEmitter` mantido abstrato (fail-open) — sem dependência circular.
> Pendências go-live (DoD): VAL-AUTH-02 (TTL sessão/refresh), VAL-09 (M2M), DD-010 (custo evento por req), teste de carga k6 em staging.

## Detalhe por onda

_(preenchido conforme execução)_

## Última falha

_(nenhuma)_
