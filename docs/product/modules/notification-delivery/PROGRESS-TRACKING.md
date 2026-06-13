# PROGRESS-TRACKING — notification-delivery

> Tracker de execução do `/forge:coding-loop notification-delivery` (modo autônomo). Fonte de verdade do estado de codificação.
> Legenda: `[ ]` pendente · `[-]` em progresso · `[X]` concluído (build + testes OK) · `[!]` falhou (HALT).

- Módulo: `notification-delivery` (Generic Subdomain — Sprint 1)
- tasks.md base: v0.1.1 (Aprovado para desenvolvimento)
- Raiz da solution: `services/notification-delivery/`
- Início da execução: 2026-06-13
- Estratégia: branch contínua `feat/notification-delivery/all-waves`, **PR único ao fim do módulo**, merge autônomo após verificação verde.

## Ondas

| Onda | Foco | TASKs | Status |
|------|------|-------|--------|
| 1 | Bootstrap | TASK-01..02 | [X] |
| 2 | Contracts | TASK-03..06 | [-] |
| 3 | Application | TASK-07..10 | [ ] |
| 4 | Infrastructure | TASK-11..16 | [ ] |
| 5 | PBTs + Hardening | TASK-17..21 | [ ] |

## Decisão de provedor (DD-001)

O `tasks.md` (TASK-12/13) menciona Postmark/SendGrid, mas **DD-001 do design é a decisão canônica aprovada (HITL #1, ADR-0005): provedor primário = Resend** (`ResendEmailSender`). Aplicação na Onda 4: TASK-12 → `ResendEmailSender` (primário); TASK-13 → `SendGridEmailSender` (implementação alternativa, prova portabilidade da ACL / RNF 1). Nenhum SDK de provedor pode vazar para `Contracts`/`Application` (NetArchTest enforça).

## Detalhe por onda

### Onda 1 — Bootstrap ✅
🧪 5 testes Architecture verdes · build limpo (0 warn). Solution `NotificationDelivery.slnx`, 3 prod + 4 teste (DD-003, sem Domain/Api). FluentAssertions fixado em 7.0.0 (MIT — 8.x é licença comercial).
- TASK-01 `535491c` · TASK-02 `1122b90`

## Última falha

_(nenhuma)_
