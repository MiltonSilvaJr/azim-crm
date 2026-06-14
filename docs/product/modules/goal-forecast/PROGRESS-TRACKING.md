# PROGRESS-TRACKING — goal-forecast

> Tracker do `/forge:coding-loop goal-forecast` (modo autônomo).
> Legenda: `[ ]` pendente · `[-]` em progresso · `[X]` concluído · `[!]` falhou (HALT).

- Módulo: `goal-forecast` (GF, Supporting — Sprint 11/12)
- tasks.md base: v0.1.1 (Aprovado para desenvolvimento)
- Raiz da solution: `services/goal-forecast/`
- Domínio sensível: **metas/forecast** (money-as-cents `long`).
- Estratégia: branch contínua `feat/goal-forecast/all-waves`, PR único ao fim, merge **isolado** + sanidade + cleanup separado.

## Ondas

| Onda | Foco | TASKs | Status |
|------|------|-------|--------|
| 1 | Bootstrap | TASK-01..02 | [X] (`325e093`,`ba01abf`) — 10 Architecture; EF 9.0.6 |
| 2 | Domain | TASK-03..07 | [X] (5 commits) — 108 Domain + PBT-02/05; money-as-cents long |
| 3 | Application | TASK-08..14 | [X] (7 commits) — 56 App + PBT-01/02/03/04; forecast degradação graciosa |
| 4 | Infrastructure (RLS) | TASK-15..20 | [X] (6 commits) — 40 Infra (RLS Testcontainers, PBT-05); circuit breaker pipeline; EF 9.0.6 |
| 5 | API + Contracts | TASK-21..26 | [X] (6 commits) — 71 Api; RBAC/GF-ERR; internal mTLS; forecast total |
| 6 | Hardening | TASK-27..30 | [X] (4 commits) — segurança/observ/perf baseline/DoD |

> **🎯 Módulo completo — 6/6 ondas, 30 TASKs, 321 testes verdes** (Domain 108 + Application 56 + Api 107 + Infrastructure 40 c/ Testcontainers RLS + Architecture 10). 5 PBTs. EF 9.0.6. PR #12.
> Money-as-cents (`long`); forecast com degradação graciosa + circuit breaker; agregação conservativa (PBT-02). Pendências: RISK-GOAL-04 (DD-008 bu_id NOT NULL reconciliar data-model), StubForecastViewSource (substituir por opportunity-pipeline real), coverage Infra/Api justificado abaixo do gate.

## Detalhe por onda

_(preenchido conforme execução)_

## Última falha

_(nenhuma)_
