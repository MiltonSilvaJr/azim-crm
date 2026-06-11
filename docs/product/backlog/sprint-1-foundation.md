# Sprint 1 — foundation

- **Objetivo:** Ao final desta sprint, o time tem os bootstraps de Clean Architecture de todos os módulos genéricos compilando com gates de arquitetura verdes, os contratos públicos `IAuditWriter` e `IEmailSender` definidos e compilando, e os ADRs 0001-0008 formalizados como documentos arquivados, viabilizando o início do desenvolvimento sobre uma base arquitetural validada e governada.
- **Período:** 2026-06-15 a 2026-06-28
- **Story Points totais:** 16
- **Status:** Planejada
- **Dependências:** nenhuma (sprint inicial)

---

## 1. Backlog da Sprint

### US-001 — Registro automático de toda escrita via IAuditWriter

- **Épico:** audit-log (EP-001)
- **RF rastreado:** REQ-001, REQ-006
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `IAuditWriter` com método `RecordAsync(AuditEntryRequest, CancellationToken)` compilando em `AuditLog.Contracts`
- [ ] `AuditEntryRequest` é `sealed record` imutável; `AuditAction` enum com `create/update/delete`
- [ ] `AuditLog.Contracts` não referencia nenhum projeto interno (zero deps)
- [ ] Architecture.Tests (NetArchTest) com regra de dependência verde (`dotnet test` passa)
- [ ] `AuditLog.sln` com 10 projetos compila sem erros (`dotnet build` verde)

#### Tasks técnicas (rastreamento TASK)
- audit-log/TASK-01: Bootstrap solution e 5 projetos Clean Architecture — Status: TO DO — pendente Jira
- audit-log/TASK-02: Contratos públicos em AuditLog.Contracts (IAuditWriter, AuditEntryRequest) — Status: TO DO — pendente Jira

#### Definition of Done
- [ ] Código implementado conforme critérios de aceite
- [ ] Testes unitários passando (Architecture.Tests ≥ 1 regra de dependência validada)
- [ ] CI verde (lint, format, build, test)
- [ ] Code review aprovado
- [ ] Commit seguindo `conventional-commits.md` (`chore/feat`)
- [ ] PR aberto (branch: `feat/audit-log/01-bootstrap-solution`, `feat/audit-log/02-contracts`)

---

### US-007 — IEmailSender portável abstraindo provedor de e-mail

- **Épico:** notification-delivery (EP-002)
- **RF rastreado:** Req 1, Req 4
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `NotificationDelivery.sln` com 3 projetos produção + 4 de teste compilando
- [ ] `IEmailSender` definido em `Contracts` com `SendAsync(EmailMessage, CancellationToken) → Task<SendResult>`
- [ ] `EmailMessage`, `SendResult`, `SendStatus` (5 valores) e `FailureReason` imutáveis e compilando
- [ ] `BrandingConfig` com validação de cores `#RRGGBB`
- [ ] Architecture.Tests verde: SDK de provedor ausente de `Contracts` e `Application`

#### Tasks técnicas (rastreamento TASK)
- notification-delivery/TASK-01: Bootstrap solution e projetos — Status: TO DO — pendente Jira
- notification-delivery/TASK-02: Architecture.Tests regra de dependência e proibição de SDK — Status: TO DO — pendente Jira
- notification-delivery/TASK-03: Contracts SendStatus + FailureReason — Status: TO DO — pendente Jira
- notification-delivery/TASK-04: Contracts EmailMessage imutável com validação de borda — Status: TO DO — pendente Jira
- notification-delivery/TASK-05: Contracts BrandingConfig — Status: TO DO — pendente Jira
- notification-delivery/TASK-06: Contracts SendResult + IEmailSender + EmailSenderContractTestBase — Status: TO DO — pendente Jira

#### Definition of Done
- [ ] Código implementado conforme critérios de aceite
- [ ] Contracts.Tests ≥ 90% coverage
- [ ] Architecture.Tests verde
- [ ] CI verde
- [ ] Code review aprovado
- [ ] Commits em Conventional Commits
- [ ] PR aberto para cada TASK

---

### US-079 — ADRs 0001-0008 formalizados como documentos arquivados

- **Épico:** transversal (HITL #1 delta)
- **RF rastreado:** HITL#1 decisões aprovadas
- **Story Points:** 3
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] ADR-0001 (Pooled DB + RLS) formalizado com contexto, decisão, consequências e status `Aceito`
- [ ] ADR-0002 a ADR-0008 formalizados (multimoeda, retenção LGPD, scheduling digest, stack GCP, Resend, B2B fields) com o mesmo padrão
- [ ] Todos os ADRs em `docs/product/adrs/` ou equivalente, referenciados nos módulos afetados
- [ ] Nenhum módulo referencia `a formalizar` para esses ADRs após a sprint

#### Tasks técnicas
- chore/ADR-0001: Formalizar ADR-0001 Pooled DB + RLS — Status: TO DO
- chore/ADR-0002 a ADR-0008: Formalizar os demais ADRs — Status: TO DO

#### Definition of Done
- [ ] 8 ADRs criados com template padrão (contexto, decisão, consequências, status)
- [ ] Módulos afetados com cross-reference atualizada
- [ ] PR aprovado e mergeado

---

## 2. Bugs Acompanhados

| ID | Severidade | Descrição | Status | Issue |
|---|---|---|---|---|
| — | — | Nenhum bug ativo | — | — |

---

## 3. Riscos da Sprint

| Risco | Mitigação |
|---|---|
| NetArchTest pode não suportar .NET 10 RC — verificar compatibilidade | Verificar version no início da sprint; alternativa: ArchUnitNET |
| ADR template não definido — pode gerar inconsistência | Definir template no primeiro commit da sprint (formato MADR ou equivalente) |

---

## 4. Encerramento

- [ ] Todas as stories em DONE
- [ ] Todos os PRs mergeados
- [ ] Sprint encerrada no Jira (quando sincronizado)
- [ ] Retrospectiva agendada
- [ ] `progress-tracking.md` atualizado
