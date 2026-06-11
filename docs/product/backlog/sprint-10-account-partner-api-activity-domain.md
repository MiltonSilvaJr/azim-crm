# Sprint 10 — account-partner-api-activity-domain

- **Objetivo:** Ao final desta sprint, um Vendedor consegue ver a visão 360° de uma conta (atividades, oportunidades, contatos), exercer direito de esquecimento de contato PII (LGPD), contas têm CNPJ/razão social/nome fantasia (delta HITL#1), e o módulo partner-management está com API REST e hardening completos com PBT-04 de isolamento por tenant como gate de CI.
- **Período:** 2026-10-19 a 2026-11-01
- **Story Points totais:** 30
- **Status:** Planejada
- **Dependências:** Sprint 8 (account/partner domain), Sprint 9 (partner infra)

---

## 1. Backlog da Sprint

### US-031 — Vincular contatos PII com CNPJ/razão social

- **Épico:** account-management (EP-006)
- **RF rastreado:** RF acc-contact
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `POST /api/v1/accounts/{id}/contacts` cria contato com nome, e-mail, telefone, CNPJ (opcional), razão social, nome fantasia
- [ ] Campos PII armazenados com criptografia em repouso; nunca em logs em texto claro
- [ ] PII scan gate de CI verde: nenhum campo PII em logs de teste

#### Tasks técnicas
- account-management/TASK-06: Commands + Handlers de Contact + ListContactsQuery + Validators — TO DO — pendente Jira

#### Definition of Done
- [ ] Application.Tests ≥ 85%; CI verde; PII scan verde; PR mergeado

---

### US-032 — Visão 360° de conta com degradação graciosa

- **Épico:** account-management (EP-006)
- **RF rastreado:** RF acc-360
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `GET /api/v1/accounts/{id}/360` agrega atividades recentes, oportunidades em aberto e contatos
- [ ] PBT-05 verde: degradação graciosa quando módulo de atividades ou oportunidades indisponível (retorna dados parciais com campo `availability`)
- [ ] Resposta em < 500ms p95 para conta com até 100 atividades/oportunidades

#### Tasks técnicas
- account-management/TASK-07: GetAccount360Query + Handler + degradação parcial + PBT-05 — TO DO — pendente Jira
- account-management/TASK-08: DbContext + mappings EF Core + migrations + índice normalizado + filtro global — TO DO — pendente Jira
- account-management/TASK-09: Migrations audit_logs (trigger) + outbox_messages + idempotency_keys — TO DO — pendente Jira
- account-management/TASK-10: IAccountRepository + TenantContext + Testcontainers + PBT-04 gate CI — TO DO — pendente Jira
- account-management/TASK-11: PiiMasker + AuditPublisher + Outbox relay + Pub/Sub — TO DO — pendente Jira
- account-management/TASK-12: ReadPorts adapters (Opportunity, Activity) + resiliência + IdempotencyKey — TO DO — pendente Jira

#### Definition of Done
- [ ] Infrastructure.Tests ≥ 70%; PBT-04 (cross-tenant gate CI) e PBT-05 verdes; CI verde; PR mergeado

---

### US-033 — Direito de esquecimento de contato (LGPD)

- **Épico:** account-management (EP-006)
- **RF rastreado:** RF acc-forget
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `DELETE /api/v1/accounts/{accountId}/contacts/{contactId}/pii` apaga irreversivelmente campos PII do contato
- [ ] Registro de auditoria imutável documenta a operação de esquecimento com ator e timestamp (sem o valor deletado)
- [ ] Resposta uniforme independente de o contato existir ou não (anti-enumeração)

#### Tasks técnicas
- account-management/TASK-13: AccountsController + DTOs + middleware + erros — TO DO — pendente Jira
- account-management/TASK-14: ContactsController + DTOs + RBAC + ForgetContact + erros (PBT-03) — TO DO — pendente Jira
- account-management/TASK-15: Endpoint 360° + contratos de evento + OpenAPI (PBT-05) — TO DO — pendente Jira
- account-management/TASK-16: Observabilidade — métricas + logs + traces + alertas — TO DO — pendente Jira
- account-management/TASK-17: Scan anti-PII em logs + isolamento tenant gate CI — TO DO — pendente Jira
- account-management/TASK-18: RBAC final + anti-enumeração + DoD + residência de dados — TO DO — pendente Jira

#### Definition of Done
- [ ] Api.Tests ≥ 80%; DoD completo; PII scan verde; CI verde; PR mergeado

---

### US-034 — Campos CNPJ, razão social e nome fantasia em contas (HITL#1)

- **Épico:** account-management (EP-006)
- **RF rastreado:** RF acc-cnpj (delta HITL#1)
- **Story Points:** 3
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] Campos `cnpj` (14 dígitos, validado por algoritmo), `razao_social`, `nome_fantasia` opcionais em `Account`
- [ ] CNPJ armazenado como PII (criptografia em repouso); exibido apenas para papéis autorizados (TAdmin, GestorBU)
- [ ] Migration idempotente adicionando colunas sem breaking change

#### Definition of Done
- [ ] Incluso em TASK-08 e TASK-13 acima; CI verde; PR mergeado

---

### partner-management (API + hardening)

- **Épico:** partner-management (EP-007)
- **Story Points:** 8
- **Status atual:** TO DO

#### Critérios de aceite
- [ ] `PartnersController`: CRUD completo + endpoints deactivate/reactivate/eligibility/commissions com RBAC e catálogo de erros PRTN-ERR
- [ ] Testes de contrato Pact, RBAC e catálogo de erros verdes
- [ ] Observabilidade: logs sem PII de parceiro, métricas, traces e alertas
- [ ] `PartnerPiiMasker` aplicado e teste anti-PII verde (gate CI)
- [ ] Resiliência do `IPartnerCommissionReadPort` (Polly) e DoD final

#### Tasks técnicas
- partner-management/TASK-22: DTOs de contratos (request/response/eventos) — TO DO — pendente Jira
- partner-management/TASK-23: PartnersController CRUD e middleware — TO DO — pendente Jira
- partner-management/TASK-24: Endpoints deactivate/reactivate/eligibility/commissions — TO DO — pendente Jira
- partner-management/TASK-25: Testes de contrato Pact, RBAC e catálogo de erros — TO DO — pendente Jira
- partner-management/TASK-26: Observabilidade — TO DO — pendente Jira
- partner-management/TASK-27: PartnerPiiMasker aplicado e teste anti-PII — TO DO — pendente Jira
- partner-management/TASK-28: Resiliência do IPartnerCommissionReadPort — TO DO — pendente Jira
- partner-management/TASK-29: Documentação OpenAPI, DoD e fechamento — TO DO — pendente Jira

#### Definition of Done
- [ ] Api.Tests ≥ 80%; PBT-04 verde; DoD completo; CI verde; PR mergeado

---

## 2. Bugs Acompanhados

| ID | Severidade | Descrição | Status | Issue |
|---|---|---|---|---|
| — | — | Nenhum | — | — |

---

## 3. Riscos da Sprint

| Risco | Mitigação |
|---|---|
| `GetAccount360Query` depende de read ports para activity e opportunity não completamente implementados | Usar stubs/mocks com dados dummy; adaptar quando os módulos reais estiverem disponíveis (Sprint 11-14) |
| Validação de CNPJ (algoritmo de dígitos verificadores) tem edge cases | Extrair para VO `Cnpj` com testes exaustivos; usar biblioteca de validação se disponível no .NET |
| Sprint 10 pesada (30 pts) com dois módulos grandes | Priorizar account-management API; partner hardening pode ser movido para início da Sprint 11 se necessário |

---

## 4. Encerramento

- [ ] Todas as stories em DONE; todos os PRs mergeados
- [ ] Sprint encerrada no Jira; `progress-tracking.md` atualizado
- [ ] **Marco:** account-management e partner-management com DoD de hardening atendido
