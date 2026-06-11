# Sprint 3 — auth-tenant-application

- **Objetivo:** Ao final desta sprint, um TAdmin consegue configurar branding WCAG do tenant, definir o fuso do digest e suspender/reativar o tenant via handlers da camada de Application, e a máquina de estados de sessão JWT está completa com verificação local de assinatura e PBTs de isolamento cross-tenant verdes, viabilizando a integração das camadas de Application de ambos os módulos genéricos centrais.
- **Período:** 2026-07-13 a 2026-07-26
- **Story Points totais:** 24
- **Status:** Planejada
- **Dependências:** Sprint 2 (domínios de authentication e tenant-administration)

---

## 1. Backlog da Sprint

### US-016 — Verificação local de JWT com cache de chaves JWKS

- **Épico:** authentication (EP-003)
- **RF rastreado:** Req 4, RNF 2, DD-002
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] Portas de aplicação definidas sem dependência do Firebase SDK: `IIdentityProvider`, `IUserDirectory`, `ITenantDirectory`, `IEmailSender`, `IRateLimiter`, `ITokenVerifier`, `ISecretProvider`
- [ ] `SessionTokenValidator` valida token JWT e aplica `TenantMatchSpec`; token de tenant B rejeitado em contexto A (PBT-02 unitário verde, ≥ 100 casos FsCheck)
- [ ] `AuthContextComposer` resolve user_id/memberships com cache read-through; cache hit evita chamada ao `IUserDirectory`
- [ ] Usuário sem `user_id` ativo → 403 (sem `identity_uid` no AuthContext)
- [ ] `SessionRevocationService` idempotente: N chamadas de logout produzem estado final único sem erro adicional (PBT-04 verde)

#### Tasks técnicas
- authentication/TASK-05: Portas de aplicação (interfaces de saída) — TO DO — pendente Jira
- authentication/TASK-06: SessionTokenValidator + AuthContextComposer + PBT-02 unitário — TO DO — pendente Jira
- authentication/TASK-07: SessionRevocationService + PBT-04 (idempotência de logout) — TO DO — pendente Jira
- authentication/TASK-08: InviteActivationService + PBT-05 unitário — TO DO — pendente Jira
- authentication/TASK-09: PasswordResetService + PBT-03 unitário (anti-enumeração) — TO DO — pendente Jira

#### Definition of Done
- [ ] Application.Tests ≥ 85% coverage
- [ ] PBTs 02, 03, 04, 05 unitários verdes (≥ 100 amostras cada)
- [ ] Architecture.Tests continua verde (Firebase SDK confinado)
- [ ] CI verde; Code review aprovado; PR mergeado

---

### US-020 — Suspender e reativar tenants via API

- **Épico:** tenant-administration (EP-004)
- **RF rastreado:** Req 4
- **Story Points:** 3
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `SuspendTenantCommand` e `ReactivateTenantCommand` com handlers; transição inválida retorna `TA-ERR-007`
- [ ] Tenant não encontrado retorna `TA-ERR-008`
- [ ] Domain events enfileirados escritos via Outbox no mesmo `TransactionBehavior`
- [ ] `ProvisionTenantCommand` com validação de slug (confirmação, unicidade via `ITenantRepository`) antes de qualquer I/O

#### Tasks técnicas
- tenant-administration/TASK-07: Pipeline behaviors (6 behaviors MediatR) — TO DO — pendente Jira
- tenant-administration/TASK-08: ProvisionTenantCommand + handler — TO DO — pendente Jira
- tenant-administration/TASK-09: SuspendTenantCommand + ReactivateTenantCommand — TO DO — pendente Jira

#### Definition of Done
- [ ] Application.Tests ≥ 85% coverage
- [ ] CI verde; Code review aprovado; PR mergeado

---

### US-021 — Configurar branding do tenant com validação WCAG AA

- **Épico:** tenant-administration (EP-004)
- **RF rastreado:** Req 5, Req 6, Req 7, Req 8
- **Story Points:** 8
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `UpdateBrandingCommand`: asset inválido retorna `TA-ERR-012/013` sem chamar GCS; cores com contraste < 4.5 retornam `TA-ERR-014` com `contrastRatio` calculado
- [ ] Branding anterior preservado em qualquer cenário de falha antes do commit
- [ ] `DerivedTones` retornados na resposta após atualização bem-sucedida
- [ ] `ICdnInvalidator.InvalidateAsync` chamado após commit bem-sucedido

#### Tasks técnicas
- tenant-administration/TASK-10: UpdateBrandingCommand + handler (WCAG, asset, CDN) — TO DO — pendente Jira

#### Definition of Done
- [ ] Application.Tests ≥ 85%; CI verde; Code review; PR mergeado

---

### US-022 — Configurar fuso e horário do digest diário

- **Épico:** tenant-administration (EP-004)
- **RF rastreado:** Req 3
- **Story Points:** 3
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `UpdateDigestConfigCommand`: fuso IANA inválido retorna `TA-ERR-006`; `TA-ERR-011` se `slug` aparecer no body
- [ ] `GetPublicBrandJsonQuery` resolve tenant por slug, retorna payload sem `adminEmail`/dados sensíveis
- [ ] `GetCurrentTenantQuery` e `GetTenantBrandingQuery` retornam dados corretos escopados por RLS

#### Tasks técnicas
- tenant-administration/TASK-11: UpdateDigestConfigCommand + handler + queries — TO DO — pendente Jira

#### Definition of Done
- [ ] Application.Tests ≥ 85%; CI verde; Code review; PR mergeado

---

## 2. Bugs Acompanhados

| ID | Severidade | Descrição | Status | Issue |
|---|---|---|---|---|
| — | — | Nenhum | — | — |

---

## 3. Riscos da Sprint

| Risco | Mitigação |
|---|---|
| PBT-03 de anti-enumeração: timing uniforme pode comprometer p95 | `Task.Delay` configurável via `IOptions<T>`; monitorar latência em testes unitários |
| `InviteActivationService` stateless depende de contrato estável com IdP — pode gerar retrabalho na Sprint 4 | Definir contrato de `IIdentityProvider.GenerateInviteActivation` como stub bem antes da implementação concreta |

---

## 4. Encerramento

- [ ] Todas as stories em DONE
- [ ] Todos os PRs mergeados
- [ ] Sprint encerrada no Jira (quando sincronizado)
- [ ] `progress-tracking.md` atualizado
