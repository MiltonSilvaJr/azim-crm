# PROGRESS-TRACKING — activity-management

> Tracker do `/forge:coding-loop activity-management` (modo autônomo).
> Legenda: `[ ]` pendente · `[-]` em progresso · `[X]` concluído · `[!]` falhou (HALT).

- Módulo: `activity-management` (ACT, Supporting — Sprint 10/11/12)
- tasks.md base: v0.1.1 (Aprovado para desenvolvimento)
- Raiz da solution: `services/activity-management/`
- Estratégia: branch contínua `feat/activity-management/all-waves`, PR único ao fim, merge **isolado** + sanidade + cleanup separado.

## Ondas

| Onda | Foco | TASKs | Status |
|------|------|-------|--------|
| 1 | Bootstrap | TASK-01 | [X] (`73c1c4f`) — 10 Architecture; EF 9.0.6 |
| 2 | Domínio (PBT-01/04/05) | TASK-02..05 | [X] (4 commits) — 141 Domain + PBT-01/04/05 (dívida: Reschedule usa UtcNow, corrigir Onda 3) |
| 3 | Application (PBT-02/03) | TASK-06..12 | [X] (7 commits) — 70 App + PBT-02/03; Reschedule corrigido (now via IClock); 7 ports |
| 4 | Infrastructure (RLS) | TASK-13..17 | [X] (6 commits) — 55 Infra (RLS falha-fechada Testcontainers, coverage 85%); PiiMasker/token testados; EF 9.0.6 |
| 5 | API + Contratos (Pact) | TASK-18..21 | [-] |
| 6 | Hardening | TASK-22..24 | [ ] |

## Detalhe por onda

_(preenchido conforme execução)_

## Última falha

_(nenhuma)_
