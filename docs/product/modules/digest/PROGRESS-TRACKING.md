# PROGRESS-TRACKING — digest

> Tracker do `/forge:coding-loop digest` (modo autônomo).
> Legenda: `[ ]` pendente · `[-]` em progresso · `[X]` concluído · `[!]` falhou (HALT).

- Módulo: `digest` (DIG, Supporting — Sprint 12/13)
- tasks.md base: v0.1.1 (Aprovado para desenvolvimento)
- Raiz da solution: `services/digest/`
- Estratégia: branch contínua `feat/digest/all-waves`, PR único ao fim, merge **isolado** + sanidade + cleanup separado.

## Ondas

| Onda | Foco | TASKs | Status |
|------|------|-------|--------|
| 1 | Bootstrap | TASK-01..02 | [X] (`fafa6bb`,`137f955`) — 9 Architecture; EF 9.0.6 + NodaTime |
| 2 | Domínio | TASK-03..08 | [X] (6 commits) — 78 Domain + PBT-01/03; money-as-cents + token hash |
| 3 | Application | TASK-09..13 | [X] (5 commits) — 55 App + PBT-02/04/06; idempotência envio |
| 4 | Infrastructure (RLS, outbox, template) | TASK-14..20 | [X] (7 commits) — 59 Infra (RLS Testcontainers, PBT-05, Polly read adapters); EF 9.0.6 |
| 5 | Api + Contratos (OIDC/WIF, Pub/Sub) | TASK-21..23 | [X] (3 commits) — 28 Api; trigger OIDC/WIF; consumer fan-out; evento digest.email_sent.v1 |
| 6 | Hardening (purge, isolamento, observ) | TASK-24..27 | [X] (4 commits) — purge 90d/isolamento gate/observ/DoD |

> **🎯 Módulo completo — 6/6 ondas, 27 TASKs, 245 testes verdes** (Domain 78 + Application 55 + Api 28 + Infrastructure 75 c/ Testcontainers RLS + Architecture 9). 6 PBTs. EF 9.0.6 + NodaTime. PR #14.
> Scheduling por fuso IANA (NodaTime), token de uso único (hash), idempotência de envio, fan-out per-tenant Pub/Sub, purge 90d. Pendências: InProcessFanout/NoOpEmailSender → reais; RISK-DIGEST-07 (opt-out via organization).

## Detalhe por onda

_(preenchido conforme execução)_

## Última falha

_(nenhuma)_
