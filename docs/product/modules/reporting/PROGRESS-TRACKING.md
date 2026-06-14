# PROGRESS-TRACKING — reporting

> Tracker do `/forge:coding-loop reporting` (modo autônomo).
> Legenda: `[ ]` pendente · `[-]` em progresso · `[X]` concluído · `[!]` falhou (HALT).

- Módulo: `reporting` (REPORT, Supporting — Sprint 13/14)
- tasks.md base: v0.1.1 (Aprovado para desenvolvimento)
- Raiz da solution: `services/reporting/`
- Estratégia: branch contínua `feat/reporting/all-waves`, PR único ao fim, merge **isolado** + sanidade + cleanup separado.

## Ondas

| Onda | Foco | TASKs | Status |
|------|------|-------|--------|
| 1 | Bootstrap | TASK-01..02 | [X] (`f1c75e1`,`6c10c3c`) — 13 Architecture (read-side purity); EF 9.0.6 |
| 2 | Domain | TASK-03..04 | [X] (2 commits) — 79 Domain + PBT-02/04; money 100% coverage |
| 3 | Application | TASK-05..12 | [X] (8 commits) — 79 App + PBT-01/02/04/05; 5 relatórios + export CSV |
| 4 | Infrastructure (views security_invoker, RLS, GCS) | TASK-13..19 | [X] (7 commits) — 28 Infra (views security_invoker, RLS PBT-03 Testcontainers, Dapper); EF 9.0.6 |
| 5 | API + Contracts | TASK-20..22 | [-] |
| 6 | Hardening | TASK-23..25 | [ ] |

## Detalhe por onda

_(preenchido conforme execução)_

## Última falha

_(nenhuma)_
