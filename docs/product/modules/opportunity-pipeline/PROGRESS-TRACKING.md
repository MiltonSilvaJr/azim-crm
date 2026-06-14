# PROGRESS-TRACKING — opportunity-pipeline

> Tracker do `/forge:coding-loop opportunity-pipeline` (modo autônomo).
> Legenda: `[ ]` pendente · `[-]` em progresso · `[X]` concluído · `[!]` falhou (HALT).

- Módulo: `opportunity-pipeline` (OP, **Core** — Sprint 14/15)
- tasks.md base: v0.1.1 (Aprovado para desenvolvimento)
- Raiz da solution: `services/opportunity-pipeline/`
- Domínio sensível: **comissões** (money-as-cents `long`, NBR-5891), snapshot imutável, numeração atômica, VAL-07 (alerta não-bloqueante no Win).
- Estratégia: branch contínua `feat/opportunity-pipeline/all-waves`, PR único ao fim, merge **isolado** + sanidade + cleanup separado.

## Ondas

| Onda | Foco | TASKs | Status |
|------|------|-------|--------|
| 1 | Bootstrap | TASK-01 | [X] (`ec77a18`) — 10 Architecture; EF 9.0.6 |
| 2 | Domain (PBT-02..08) | TASK-02..07 | [-] |
| 3 | Application (PBT-09) | TASK-08..12 | [ ] |
| 4 | Infrastructure (RLS, numeração atômica, outbox; PBT-01/07/11) | TASK-13..18 | [ ] |
| 5 | API + Contracts | TASK-19..21 | [ ] |
| 6 | Hardening (PBT-10, carga Kanban) | TASK-22..24 | [ ] |

## Detalhe por onda

_(preenchido conforme execução)_

## Última falha

_(nenhuma)_
