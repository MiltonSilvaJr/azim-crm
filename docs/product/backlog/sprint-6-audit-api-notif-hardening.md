# Sprint 6 — audit-api-notif-hardening

- **Objetivo:** Ao final desta sprint, a API de consulta da trilha de auditoria está operacional com RBAC por papel e escopo de BU, o catálogo de erros AUD-ERR-001..008 está completo, e o módulo notification-delivery tem os PBTs-01..05 verdes e observabilidade ativa — todos os módulos genéricos da Fase 1 estão com DoD de hardening atendido.
- **Período:** 2026-08-24 a 2026-09-06
- **Story Points totais:** 24
- **Status:** Planejada
- **Dependências:** Sprint 4 (audit-log infra), Sprint 5 (notification-delivery app/infra)

---

## 1. Backlog da Sprint

### US-002 — Consulta da trilha de auditoria com filtros e paginação

- **Épico:** audit-log (EP-001)
- **RF rastreado:** REQ-007
- **Story Points:** 3
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `GET /api/v1/audit-logs` retorna 200 com envelope paginado `{items, page, pageSize, total}`; `pageSize` default 50, máx 200
- [ ] `GET /api/v1/audit-logs/{entityType}/{entityId}` retorna histórico da entidade
- [ ] Filtros por `entity_type`, `entity_id`, `user_id`, `from`, `to` funcionando; `from > to` retorna AUD-ERR-006
- [ ] `POST/PUT/PATCH/DELETE` retornam 405 (AUD-ERR-007); JWT ausente retorna 401 (AUD-ERR-003)
- [ ] `createdAt` em UTC ISO-8601 na resposta

#### Tasks técnicas
- audit-log/TASK-15: AuditLogQueryController (endpoints GET) — TO DO — pendente Jira
- audit-log/TASK-16: Catálogo de erros AUD-ERR-001..008 + OpenAPI — TO DO — pendente Jira

#### Definition of Done
- [ ] Api.Tests ≥ 80%; catálogo de erros completo; OpenAPI gerada e validável
- [ ] CI verde; Code review; PR mergeado

---

### US-005 — Isolamento de consulta por BU para GestorBU

- **Épico:** audit-log (EP-001)
- **RF rastreado:** REQ-005, REQ-008
- **Story Points:** 3
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] TAdmin: acesso irrestrito dentro do tenant (REQ-008.2)
- [ ] GestorBU: acesso restrito às suas BUs (REQ-008.3, filtro via `IBuScopeResolver`)
- [ ] Vendedor/Viewer/PlatOp por padrão: 403 (REQ-008.4)
- [ ] Acesso autorizado de PlatOp gera registro na trilha (REQ-008.5)
- [ ] Testes de contrato de `IAuditWriter` verdes

#### Tasks técnicas
- audit-log/TASK-17: RBAC + escopo de BU (DD-008) + testes de contrato — TO DO — pendente Jira

#### Definition of Done
- [ ] Api.Tests ≥ 80%; RBAC e escopo de BU validados; CI verde; Code review; PR mergeado

---

### US-010 — E-mail do destinatário nunca em logs (LGPD)

- **Épico:** notification-delivery (EP-002)
- **RF rastreado:** Req 3, RNF 4
- **Story Points:** 3
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] PBT-03 verde: nenhum valor de `RecipientEmail` aparece em qualquer linha de log gerada (≥ 500 amostras)
- [ ] Serilog `DestructuringPolicy` intercept campos `RecipientEmail`/`Email`/`recipient` e substitui pelo hash SHA-256 truncado
- [ ] PBT-01 verde: reversibilidade do adapter (round-trip SendResult para entrada equivalente, ≥ 500 amostras)
- [ ] PBT-04 verde: mapeamento total do `ProviderResponseMapper` (nenhuma resposta resulta em estado indefinido, ≥ 500 amostras)

#### Tasks técnicas
- notification-delivery/TASK-17: PBT-01 (reversibilidade adapter) + PBT-04 (totalidade ACL) — TO DO — pendente Jira
- notification-delivery/TASK-19: PBT-03 (anti-PII) + Serilog destructuring policy integrada — TO DO — pendente Jira

#### Definition of Done
- [ ] PBTs 01, 03, 04 verdes (≥ 500 amostras); CI verde; Code review; PR mergeado

---

### US-009 (hardening) — Idempotência e caos de envio de e-mail

- **Épico:** notification-delivery (EP-002)
- **RF rastreado:** Req 8, Req 9, RNF 3
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] PBT-02 verde: idempotência de reenvio — N tentativas para mesma `IdempotencyKey` não duplicam entrega (≥ 500 amostras)
- [ ] PBT-05 verde: backoff não duplica entrega; renderização chamada exatamente 1 vez por chamada `SendAsync` (≥ 500 amostras)
- [ ] Teste de caos (TASK-21): falha aleatória de provedor não propaga exceção ao chamador
- [ ] Gates de go-live: `EmailSenderContractTestBase` verde contra todos os senders; DoD do módulo completo

#### Tasks técnicas
- notification-delivery/TASK-18: PBT-02 (idempotência de reenvio) + PBT-05 (backoff não duplica) — TO DO — pendente Jira
- notification-delivery/TASK-21: Hardening: teste de caos + gates de go-live + DoD final — TO DO — pendente Jira

#### Definition of Done
- [ ] PBTs 02, 05 verdes (≥ 500 amostras); teste de caos verde; DoD completo; CI verde; PR mergeado

---

### US-010 (observabilidade) — Métricas e observabilidade de notification-delivery

- **Épico:** notification-delivery (EP-002)
- **RF rastreado:** RNF 5, RNF 7
- **Story Points:** 3
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] Métricas `email_send_attempts_total`, `email_send_duration_seconds`, `email_circuit_breaker_open` ativas e documentadas
- [ ] Logs estruturados (Serilog JSON): nenhum campo PII em texto claro; `correlationId` e `tenantId` em toda entrada de log
- [ ] Alert rule: taxa de falha > 5% em 5min; circuit breaker aberto

#### Tasks técnicas
- notification-delivery/TASK-20: Observabilidade: métricas email_send_*, logs estruturados, alertas — TO DO — pendente Jira

#### Definition of Done
- [ ] Métricas ativas; DoD do módulo completo; CI verde; Code review; PR mergeado

---

### audit-log (hardening) — Métricas, alertas e DoD final

- **Épico:** audit-log (EP-001)
- **RF rastreado:** RNF-003, RNF-004
- **Story Points:** 3
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] Métricas `audit_insert_total`, `audit_insert_failures_total`, `audit_query_without_tenant_context_total` ativas
- [ ] Health check `/health` retorna status do módulo; PII scan no CI verde
- [ ] DoD do design.md §19 completo; testes de segurança verdes

#### Tasks técnicas
- audit-log/TASK-18: Métricas, alertas e health check — TO DO — pendente Jira
- audit-log/TASK-19: PII scan em CI + testes de segurança — TO DO — pendente Jira
- audit-log/TASK-20: DoD final e sincronização de documentação — TO DO — pendente Jira

#### Definition of Done
- [ ] Métricas ativas; PII scan verde; DoD completo; CI verde; PR mergeado

---

## 2. Bugs Acompanhados

| ID | Severidade | Descrição | Status | Issue |
|---|---|---|---|---|
| — | — | Nenhum | — | — |

---

## 3. Riscos da Sprint

| Risco | Mitigação |
|---|---|
| PBTs com 500 amostras podem ser lentos em CI (PBT-01..05 notification-delivery) | Configurar `MaxTest = 500` com seed fixo para reprodutibilidade; paralelizar runs |
| `IBuScopeResolver` precisa de read model externo (organization) ainda não implementado | Usar stub/mock em Application; adaptar quando organization estiver disponível (Sprint 7) |

---

## 4. Encerramento

- [ ] Todas as stories em DONE; todos os PRs mergeados
- [ ] Sprint encerrada no Jira; retrospectiva realizada
- [ ] **Marco:** todos os 4 módulos genéricos (audit-log, notification-delivery, authentication, tenant-administration) com DoD de hardening atendido
- [ ] `progress-tracking.md` atualizado
