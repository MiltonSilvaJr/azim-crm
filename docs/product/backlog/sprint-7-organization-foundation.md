# Sprint 7 — organization-foundation

- **Objetivo:** Ao final desta sprint, um TAdmin consegue criar, renomear e desativar Business Units, convidar usuários com papéis (Vendedor, GestorBU, Viewer) e desativá-los, com RBAC operacional via JWT e a API REST do módulo organization (ondas 1 a 3) disponível, viabilizando o onboarding da equipe de vendas no CRM.
- **Período:** 2026-09-07 a 2026-09-20
- **Story Points totais:** 24
- **Status:** Planejada
- **Dependências:** Sprint 5 (authentication API operacional), Sprint 6 (audit-log IAuditWriter operacional)

---

## 1. Backlog da Sprint

### US-024 — Criar e gerenciar Business Units

- **Épico:** organization (EP-005)
- **RF rastreado:** RF org-BU
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `POST /api/v1/bus` cria BU com nome único por tenant; duplicata retorna 409
- [ ] `PATCH /api/v1/bus/{id}` renomeia; `DELETE /api/v1/bus/{id}` desativa (soft delete); BU com membros ativos não pode ser desativada
- [ ] Eventos `BUCreated` publicados via Outbox; audit-log registra toda escrita

#### Tasks técnicas
- organization/TASK-01: Bootstrap solution + Architecture.Tests — TO DO — pendente Jira
- organization/TASK-02: Architecture.Tests e CI mínimo — TO DO — pendente Jira
- organization/TASK-03: Objetos de valor do domínio — TO DO — pendente Jira
- organization/TASK-04: Aggregate BusinessUnit + pipeline entities + policies + seeds — TO DO — pendente Jira
- organization/TASK-07: Pipeline behaviors — TO DO — pendente Jira
- organization/TASK-08: Handlers de BusinessUnit (Create, Rename, Deactivate) — TO DO — pendente Jira
- organization/TASK-14: Queries + MembershipCacheProjector + GetRbacContextQuery — TO DO — pendente Jira

#### Definition of Done
- [ ] Domain.Tests ≥ 95%; Application.Tests ≥ 85%; CI verde; Code review; PR mergeado

---

### US-025 — Convidar usuários com papéis para BUs

- **Épico:** organization (EP-005)
- **RF rastreado:** RF org-invite
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `POST /api/v1/bus/{buId}/invites` envia convite por e-mail via `IEmailSender`; link de ativação idempotente por e-mail+BU
- [ ] Convite aceito via authentication/TASK-19 cria membership com papel correto
- [ ] `LastTenantAdminPolicy` (PBT-02): impossível desativar o último TAdmin do tenant

#### Tasks técnicas
- organization/TASK-05: Aggregate User + UserMembership + eventos de domínio — TO DO — pendente Jira
- organization/TASK-06: Aggregate UserInvitation + state machine (PBT-04) — TO DO — pendente Jira
- organization/TASK-09: Handlers de convite (Invite, Revoke, Accept) + PBT-03 parcial — TO DO — pendente Jira
- organization/TASK-10: Handlers de membership + LastTenantAdminPolicy (PBT-02) — TO DO — pendente Jira

#### Definition of Done
- [ ] Domain.Tests ≥ 95%; PBTs 02, 03, 04 verdes; CI verde; Code review; PR mergeado

---

### US-026 — Desativar usuários preservando histórico

- **Épico:** organization (EP-005)
- **RF rastreado:** RF org-deactivate
- **Story Points:** 3
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `DELETE /api/v1/users/{id}` desativa o usuário (soft delete); atividades e oportunidades históricas preservadas
- [ ] `FutureActivitiesSpec` (TASK-12): atividades futuras do usuário devem ser reatribuídas ou canceladas antes da desativação
- [ ] Evento `UserDeactivated` publicado via Outbox; invalida cache Redis de memberships

#### Tasks técnicas
- organization/TASK-11: Handlers de configuração de pipeline (Stage, Channel, LossReason) — TO DO — pendente Jira
- organization/TASK-12: DeactivateUserCommand + FutureActivitiesSpec — TO DO — pendente Jira
- organization/TASK-13: ProvisionInitialOrganizationCommand + IdempotencyBehavior (PBT-03) — TO DO — pendente Jira

#### Definition of Done
- [ ] Application.Tests ≥ 85%; PBT-03 verde (idempotência); CI verde; Code review; PR mergeado

---

## 2. Bugs Acompanhados

| ID | Severidade | Descrição | Status | Issue |
|---|---|---|---|---|
| — | — | Nenhum | — | — |

---

## 3. Riscos da Sprint

| Risco | Mitigação |
|---|---|
| `IBuScopeResolver` no audit-log precisa de read model do organization | Implementar `BuScopeReadAdapter` no organization apontando para `IBuScopeResolver` (Sprint 7 entrega o adapter que o audit-log consumirá) |
| Testcontainers PostgreSQL para PBT-01 (isolamento por tenant) em organizartion | Reutilizar fixture da Sprint 4 (audit-log); extrair para package compartilhado `.forge/` |

---

## 4. Encerramento

- [ ] Todas as stories em DONE; todos os PRs mergeados
- [ ] Sprint encerrada no Jira; retrospectiva realizada
- [ ] `progress-tracking.md` atualizado
