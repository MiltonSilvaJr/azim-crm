# Sprint 4 — generic-infra-audit-app

- **Objetivo:** Ao final desta sprint, a trilha de auditoria está com append-only enforçado no banco PostgreSQL (trigger + REVOKE + RLS), o cache Redis de memberships está operacional com invalidação por evento, o mascaramento de PII no delta está com PBTs verdes, e o módulo audit-log tem sua camada de Application completa (handlers, queries, behaviors), viabilizando a integração completa dos módulos genéricos de infraestrutura.
- **Período:** 2026-07-27 a 2026-08-09
- **Story Points totais:** 26
- **Status:** Planejada
- **Dependências:** Sprint 2 (domínios), Sprint 3 (application layers)

---

## 1. Backlog da Sprint

### US-003 — PII mascarado no delta de auditoria (LGPD)

- **Épico:** audit-log (EP-001)
- **RF rastreado:** REQ-004, RNF-002
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `PiiMasker` (serviço de domínio puro sem deps de infra) aplica máscara `[MASKED]` para campos de `Contact` (name, email, phone) com chave preservada
- [ ] PBT-04 verde: nenhum valor PII original em texto claro no `delta_json` (≥ 100 amostras FsCheck)
- [ ] PBT-03 verde: round-trip do delta para create/update/delete com ≥ 100 amostras cada
- [ ] `AuditDelta` sem campos `float`/`double` (DD-005, money em `long` centavos)
- [ ] `RecordAuditEntryCommand` aplica mascaramento antes de qualquer persistência

#### Tasks técnicas
- audit-log/TASK-03: Objetos de valor do domínio (6 VOs) — TO DO — pendente Jira
- audit-log/TASK-04: Aggregate AuditLog + IAuditLogRepository — TO DO — pendente Jira
- audit-log/TASK-05: PiiMasker + PiiFieldPolicy + PBT-04 — TO DO — pendente Jira
- audit-log/TASK-06: PBT-03 Round-trip do delta — TO DO — pendente Jira
- audit-log/TASK-07: RecordAuditEntryCommand + AuditService handler — TO DO — pendente Jira
- audit-log/TASK-08: Queries de consulta + handlers + validators — TO DO — pendente Jira
- audit-log/TASK-09: Pipeline behaviors (4 behaviors MediatR) — TO DO — pendente Jira
- audit-log/TASK-10: PBT-02 Conservação (Application.Tests) — TO DO — pendente Jira

#### Definition of Done
- [ ] Domain.Tests ≥ 95%; Application.Tests ≥ 85%
- [ ] PBTs 02, 03, 04 verdes (≥ 100 amostras cada)
- [ ] CI verde; Code review; PR mergeado

---

### US-004 — Append-only enforçado no banco via trigger e REVOKE

- **Épico:** audit-log (EP-001)
- **RF rastreado:** REQ-002, REQ-005, RNF-001
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] Migration `create_immutable_audit_logs.sql` aplicada: trigger `trg_audit_logs_immutable`, REVOKE de UPDATE/DELETE/TRUNCATE do role `app`, RLS com policy `rls_audit_logs_tenant`, 3 índices compostos criados
- [ ] PBT-01 verde: toda tentativa de UPDATE/DELETE/TRUNCATE pelo role `app` lança exceção e o registro permanece idêntico (Testcontainers PostgreSQL real, ≥ 50 amostras)
- [ ] PBT-05 verde: nenhum registro de tenant Y retorna em consulta de tenant X (≥ 50 amostras)
- [ ] `AuditLogRepository` sem métodos `Update`/`Remove`; filtro global de `tenant_id` em todas as queries

#### Tasks técnicas
- audit-log/TASK-11: EF Core DbContext + AuditLogRepository — TO DO — pendente Jira
- audit-log/TASK-12: Migration append-only (trigger + REVOKE + RLS + índices) — TO DO — pendente Jira
- audit-log/TASK-13: PBT-01 Imutabilidade + PBT-05 Isolamento (Testcontainers) — TO DO — pendente Jira

#### Definition of Done
- [ ] Infrastructure.Tests ≥ 70% (Testcontainers); PBTs 01, 05 verdes
- [ ] Migration aplicável e reversível; CI verde; Code review; PR mergeado

---

### US-006 — Fail-closed: falha de auditoria reverte escrita de negócio

- **Épico:** audit-log (EP-001)
- **RF rastreado:** REQ-001, DD-001
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] Falha no INSERT de `audit_logs` reverte também a escrita de negócio (mesma transação, Testcontainers)
- [ ] PBT-06 verde: N execuções de qualquer query não alteram conjunto, ordem ou conteúdo persistido (≥ 100 amostras)
- [ ] Falhas de auditoria registradas em log separado sem PII; contador `audit_insert_failures_total` incrementado

#### Tasks técnicas
- audit-log/TASK-14: Integração fail-closed (DD-001) + PBT-06 idempotência de leitura — TO DO — pendente Jira

#### Definition of Done
- [ ] Infrastructure.Tests verdes; PBT-06 verde; CI verde; Code review; PR mergeado

---

### US-017 — Cache Redis de memberships com invalidação por evento

- **Épico:** authentication (EP-003)
- **RF rastreado:** Req 5, RNF 2, DD-007
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `FirebaseIdentityProvider` com circuit breaker Polly (N falhas → aberto, HTTP 503), timeout, exceções Firebase mapeadas para `IdentityProviderException`
- [ ] `JwksTokenVerifier`: verificação local de assinatura JWT sem round-trip ao IdP; cache de JWKS com TTL de `Cache-Control`; rotação automática em `kid` desconhecido
- [ ] `MembershipCacheRepository` (Redis TLS): chave `auth:membership:{tenant_id}:{user_id}`, TTL configurável; consumer de `user.role_changed`/`user.deactivated` para invalidação
- [ ] `SecretManagerProvider` via GCP Secret Manager; `gitleaks` verde no CI
- [ ] `OrganizationUserDirectory` e `TenantDirectory` com Testcontainers PostgreSQL; `identity_uid` não exposto ao chamador

#### Tasks técnicas
- authentication/TASK-10: FirebaseIdentityProvider + circuit breaker — TO DO — pendente Jira
- authentication/TASK-11: JwksTokenVerifier + cache de chaves públicas — TO DO — pendente Jira
- authentication/TASK-12: MembershipCacheRepository (Redis + invalidação por evento) — TO DO — pendente Jira
- authentication/TASK-13: SecretManagerProvider + gate gitleaks no CI — TO DO — pendente Jira
- authentication/TASK-14: OrganizationUserDirectory + TenantDirectory — TO DO — pendente Jira

#### Definition of Done
- [ ] Infrastructure.Tests ≥ 70%; Architecture.Tests verde
- [ ] PBT-02 integração verde; gitleaks verde
- [ ] CI verde; Code review; PR mergeado

---

## 2. Bugs Acompanhados

| ID | Severidade | Descrição | Status | Issue |
|---|---|---|---|---|
| — | — | Nenhum | — | — |

---

## 3. Riscos da Sprint

| Risco | Mitigação |
|---|---|
| Firebase Emulator + WireMock JWKS: setup complexo em CI | Spike de 1 dia no início da sprint; documentar em `.forge/scripts/` |
| Testcontainers PostgreSQL para PBT-01 pode ser lento (banco sobe por fixture) | Usar shared container com `IClassFixture` e truncate via superuser entre batchs |
| Trigger PostgreSQL pode não ser portável entre versões (PostgreSQL 15 vs 16) | Fixar versão no Testcontainers; testar com `postgres:15-alpine` e `postgres:16-alpine` |

---

## 4. Encerramento

- [ ] Todas as stories em DONE; todos os PRs mergeados
- [ ] Sprint encerrada no Jira (quando sincronizado)
- [ ] `progress-tracking.md` atualizado
