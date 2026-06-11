# Sprint 9 — org-api-account-partner-app

- **Objetivo:** Ao final desta sprint, o provisioning inicial de organização por evento TenantProvisioned está operacional com idempotência, a API REST do organization está completa com contract tests de eventos, parceiros têm lifecycle completo (desativar/reativar) e a porta IPartnerCommissionReadPort está estável para consumo pelo opportunity-pipeline.
- **Período:** 2026-10-05 a 2026-10-18
- **Story Points totais:** 24
- **Status:** Planejada
- **Dependências:** Sprint 7 (org handlers), Sprint 8 (org infra, partner domain, account domain)

---

## 1. Backlog da Sprint

### US-029 — Provisioning inicial de organização idempotente por TenantProvisioned

- **Épico:** organization (EP-005)
- **RF rastreado:** RF org-provision
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] Consumer de `tenant.provisioned.v1` do Pub/Sub executa `ProvisionInitialOrganizationCommand` com idempotência (PBT-03 verde)
- [ ] Outbox worker publicando eventos `*.v1` no Pub/Sub `azim-organizations`; Inbox consumer tratando `TenantProvisioned`
- [ ] Testes de contrato de eventos `*.v1` (Pact ou schema validation) verdes

#### Tasks técnicas
- organization/TASK-18: Outbox worker + Pub/Sub publisher + Inbox TenantProvisioned — TO DO — pendente Jira
- organization/TASK-19: Adapters externos (IdentityProvisioner, EmailSender, counters) — TO DO — pendente Jira
- organization/TASK-21: Controllers de usuário e convite + anti-enumeração — TO DO — pendente Jira
- organization/TASK-23: Api.Tests — contrato REST, RBAC papel×operação, isolamento — TO DO — pendente Jira
- organization/TASK-24: Testes de contrato de eventos *.v1 — TO DO — pendente Jira

#### Definition of Done
- [ ] Api.Tests ≥ 80%; PBT-03 verde; contract tests verdes; CI verde; PR mergeado

---

### US-036 — Desativar e reativar parceiros

- **Épico:** partner-management (EP-007)
- **RF rastreado:** RF prtn-lifecycle
- **Story Points:** 3
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `DeactivatePartnerCommand` e `ReactivatePartnerCommand` com handlers e state machine; PBT-01 (isolamento por tenant) e PBT-05 verdes
- [ ] Parceiro desativado não pode ser vinculado a novas oportunidades

#### Tasks técnicas
- partner-management/TASK-10: Commands DeactivatePartner e ReactivatePartner — TO DO — pendente Jira
- partner-management/TASK-13: Pipeline behaviors MediatR — TO DO — pendente Jira
- partner-management/TASK-14: PBT-01 e PBT-05 — Application Tests — TO DO — pendente Jira

#### Definition of Done
- [ ] Application.Tests ≥ 85%; PBTs 01, 05 verdes; CI verde; PR mergeado

---

### US-037 — Consultar elegibilidade de parceiro

- **Épico:** partner-management (EP-007)
- **RF rastreado:** RF prtn-eligibility
- **Story Points:** 2
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `GetPartnerEligibility` query retorna `{eligible: bool, reason?: string}` para uso no opportunity-pipeline
- [ ] Parceiro inativo retorna `eligible: false`

#### Tasks técnicas
- partner-management/TASK-11: Queries ListPartners, GetPartnerById, GetPartnerEligibility — TO DO — pendente Jira

#### Definition of Done
- [ ] Application.Tests ≥ 85%; CI verde; PR mergeado

---

### US-038 — Porta IPartnerCommissionReadPort estável

- **Épico:** partner-management (EP-007)
- **RF rastreado:** RF prtn-commission-port
- **Story Points:** 3
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `IPartnerCommissionReadPort` com `GetDefaultCommissionsAsync(partnerId, tenantId)` em Application
- [ ] `PartnerCommissionReadAdapter` (infra) implementando a porta com Polly (retry + circuit breaker)
- [ ] EF Core + migrations + RLS + Outbox + Audit para partner-management

#### Tasks técnicas
- partner-management/TASK-12: Porta IPartnerCommissionReadPort e queries de comissão — TO DO — pendente Jira
- partner-management/TASK-15: EF Core DbContext + mapeamento + filtro global — TO DO — pendente Jira
- partner-management/TASK-16: Migrations: partners + outbox + idempotency + índices + RLS — TO DO — pendente Jira
- partner-management/TASK-17: PartnerRepository + TenantContext + RLS interceptor — TO DO — pendente Jira
- partner-management/TASK-18: Outbox transacional + AuditPublisher — TO DO — pendente Jira
- partner-management/TASK-19: PartnerCommissionReadAdapter (adaptador do pipeline) — TO DO — pendente Jira
- partner-management/TASK-20: CanonicalRoleProvider + PartnerPiiMasker + idempotência — TO DO — pendente Jira
- partner-management/TASK-21: PBT-04 — Isolamento por tenant (gate CI) — TO DO — pendente Jira

#### Definition of Done
- [ ] Infrastructure.Tests ≥ 70%; PBT-04 verde (gate CI); CI verde; PR mergeado

---

### organization (hardening) — Observabilidade e segurança

- **Épico:** organization (EP-005)
- **Story Points:** 5
- **Status atual:** TO DO

#### Critérios de aceite
- [ ] PBT-01 (isolamento por tenant) e PBT-05 (integridade na desativação) verdes
- [ ] Observabilidade: logs estruturados, métricas (`org_memberships_total`, `org_invites_sent_total`), health checks, alertas
- [ ] Mascaramento de PII (e-mail de usuário hash), token hash de convite, anti-enumeração nos endpoints de usuário
- [ ] DoD final, README sincronizado e matriz de rastreabilidade completa

#### Tasks técnicas
- organization/TASK-25: Observabilidade — TO DO — pendente Jira
- organization/TASK-26: Segurança e PII — TO DO — pendente Jira
- organization/TASK-27: PBT-01 + PBT-05 — TO DO — pendente Jira
- organization/TASK-28: DoD final, README sync — TO DO — pendente Jira

#### Definition of Done
- [ ] PBTs 01, 05 verdes; DoD completo; CI verde; PR mergeado

---

## 2. Bugs Acompanhados

| ID | Severidade | Descrição | Status | Issue |
|---|---|---|---|---|
| — | — | Nenhum | — | — |

---

## 3. Riscos da Sprint

| Risco | Mitigação |
|---|---|
| Pub/Sub fake para testes de Inbox consumer pode ser complexo | Usar in-memory message bus para Application.Tests; Pub/Sub real via Testcontainers emulator para Infrastructure.Tests |

---

## 4. Encerramento

- [ ] Todas as stories em DONE; todos os PRs mergeados
- [ ] Sprint encerrada no Jira; `progress-tracking.md` atualizado
