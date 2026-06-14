# PROGRESS-TRACKING — partner-management

> Tracker do `/forge:coding-loop partner-management` (modo autônomo).
> Legenda: `[ ]` pendente · `[-]` em progresso · `[X]` concluído · `[!]` falhou (HALT).

- Módulo: `partner-management` (PM, Supporting — Sprint 9/10)
- tasks.md base: v0.1.1 (Aprovado para desenvolvimento)
- Raiz da solution: `services/partner-management/`
- Domínio sensível: **comissões** (money-as-cents `long`, arredondamento NBR-5891).
- Estratégia: branch contínua `feat/partner-management/all-waves`, PR único ao fim, merge **isolado** + sanidade + cleanup separado.

## Ondas

| Onda | Foco | TASKs | Status |
|------|------|-------|--------|
| 1 | Bootstrap | TASK-01..02 | [X] (`02bd902`,`e5b7702`) — 10 Architecture; EF 9.0.6 |
| 2 | Domain (PBTs) | TASK-03..08 | [X] (6 commits) — 128 Domain + PBT-02/03; Percentage decimal NBR-5891 |
| 3 | Application (PBTs) | TASK-09..14 | [X] (6 commits) — 61 App + PBT-01/05; IPartnerCommissionReadPort |
| 4 | Infrastructure (RLS, outbox) | TASK-15..21 | [X] (7 commits) — 60 Infra (RLS/PBT-04 Testcontainers, PartnerPiiMasker); EF 9.0.6 |
| 5 | API + Contratos (Pact) | TASK-22..25 | [-] |
| 6 | Hardening | TASK-26..29 | [ ] |

## Detalhe por onda

_(preenchido conforme execução)_

## Última falha

_(nenhuma)_
