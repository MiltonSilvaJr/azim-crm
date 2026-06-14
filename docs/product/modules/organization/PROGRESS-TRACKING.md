# PROGRESS-TRACKING — organization

> Tracker do `/forge:coding-loop organization` (modo autônomo).
> Legenda: `[ ]` pendente · `[-]` em progresso · `[X]` concluído · `[!]` falhou (HALT).

- Módulo: `organization` (ORG, Supporting foundation — Sprint 7/8/9)
- tasks.md base: v0.1.1 (Aprovado para desenvolvimento)
- Raiz da solution: `services/organization/`
- Estratégia: branch contínua `feat/organization/all-waves`, PR único ao fim, merge autônomo após verificação verde (incl. `git status` worktree limpo + sanidade pós-merge).

## Ondas

| Onda | Foco | TASKs | Status |
|------|------|-------|--------|
| 1 | Bootstrap | TASK-01..02 | [X] (`90a8338`,`b72992c`) — 10 Architecture; EF 9.0.6 |
| 2 | Domain (PBT-04/06/07) | TASK-03..06 | [X] (4 commits) — 139 Domain + PBT-04/06/07 |
| 3 | Application (PBT-02/03) | TASK-07..14 | [X] (8 commits) — 95 App + PBT-02/03; GetRbacContextQuery + MembershipCacheProjector |
| 4 | Infrastructure (RLS, Outbox/Inbox) | TASK-15..19 | [X] (5 commits) — 35 Infra (RLS Testcontainers); EF 9.0.6 |
| 5 | API + Contracts | TASK-20..24 | [X] (5 commits) — 90 Api; RBAC/anti-enum; DI compose |
| 6 | Hardening (PBT-01/05) | TASK-25..28 | [X] (4 commits) — observ/segurança/PBT-01/05/DoD |

> **🎯 Módulo completo — 6/6 ondas, 28 TASKs, 398 testes verdes** (Domain 139 + Application 99 + Api 90 + Infrastructure 60 c/ Testcontainers RLS + Architecture 10). 7 PBTs. EF 9.0.6. PR #8.
> **Provê:** `GetRbacContextQuery` (consumido pelo audit-log IBuScopeResolver / RISK-AUDIT-05) e `MembershipCacheProjector` (consumido pelo authentication MembershipCache). Inbox consome `TenantProvisioned` do tenant-administration.

## Detalhe por onda

_(preenchido conforme execução)_

## Última falha

_(nenhuma)_
