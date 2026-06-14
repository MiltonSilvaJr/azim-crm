# PROGRESS-TRACKING — data-migration

> Tracker do `/forge:coding-loop data-migration` (modo autônomo).
> Legenda: `[ ]` pendente · `[-]` em progresso · `[X]` concluído · `[!]` falhou (HALT).

- Módulo: `data-migration` (BC-09, Supporting — Sprint 13)
- tasks.md base: v0.1.1 (Aprovado para desenvolvimento)
- Raiz da solution: `services/data-migration/`
- Domínio sensível: importação de planilha; money-as-cents `long`, NBR-5891.
- Estratégia: branch contínua `feat/data-migration/all-waves`, PR único ao fim, merge **isolado** + sanidade + cleanup separado.

## Ondas

| Onda | Foco | TASKs | Status |
|------|------|-------|--------|
| 1 | Bootstrap | TASK-01..02 | [X] (`bf43e21`,`cec503e`) — 13 Architecture; EF 9.0.6 |
| 2 | Domain | TASK-03..07 | [X] (5 commits) — 179 Domain + PBT-03/05/06/07; money-as-cents |
| 3 | Application | TASK-08..13 | [X] (6 commits) — 40 App + PBT-01/02/04; import tudo-ou-nada |
| 4 | Infrastructure (RLS) | TASK-14..20 | [X] (7 commits) — 90 Infra (RLS Testcontainers, parser ClosedXML, PBT-02 idempotência); import adapters in-process; EF 9.0.6 |
| 5 | API + Contratos | TASK-21..23 | [-] |
| 6 | Hardening | TASK-24..28 | [ ] |

## Detalhe por onda

_(preenchido conforme execução)_

## Última falha

_(nenhuma)_
