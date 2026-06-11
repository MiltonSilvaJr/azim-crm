# Sprint 5 — auth-tenant-api-notif-app

- **Objetivo:** Ao final desta sprint, um usuário consegue fazer login, logout, convite e reset-senha pela API REST completa do módulo authentication, o brand.json público está disponível via CDN, e o IEmailSender com Resend (ADR-0005) está integrado com branding do tenant, retry e circuit breaker, viabilizando o primeiro fluxo E2E de autenticação e o envio de e-mail transacional.
- **Período:** 2026-08-10 a 2026-08-23
- **Story Points totais:** 38
- **Status:** Planejada
- **Dependências:** Sprint 3 (application), Sprint 4 (infrastructure)

---

## 1. Backlog da Sprint

### US-013 — Convite de usuário com link de ativação de 72h

- **Épico:** authentication (EP-003)
- **RF rastreado:** Req 7
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `POST /v1/auth/invites` requer papel TAdmin; retorna 202; e-mail de convite enviado via `IEmailSender`
- [ ] `POST /v1/auth/invites/activate` com token expirado retorna 410 `AUTH-ERR-033`; token consumido retorna 410
- [ ] E-mail duplicado retorna 409 `AUTH-ERR-030` sem vazar dados do usuário existente
- [ ] Falha de envio de e-mail retorna 502 `AUTH-ERR-032` sem reverter convite criado no IdP

#### Tasks técnicas
- authentication/TASK-15: CorrelationMiddleware + TenantResolutionMiddleware — TO DO — pendente Jira
- authentication/TASK-16: AuthenticationMiddleware + RateLimitingMiddleware — TO DO — pendente Jira
- authentication/TASK-17: Contracts, DTOs e catálogo de erros (AUTH-ERR-001..090) — TO DO — pendente Jira
- authentication/TASK-18: Endpoints GET /v1/auth/me e POST /v1/auth/logout — TO DO — pendente Jira
- authentication/TASK-19: Endpoints POST /v1/auth/invites e /invites/activate — TO DO — pendente Jira

#### Definition of Done
- [ ] Api.Tests ≥ 80%; PBT-03 API e PBT-04 API verdes; catálogo de erros completo
- [ ] CI verde; Code review; PR mergeado

---

### US-014 — Reset de senha anti-enumeração

- **Épico:** authentication (EP-003)
- **RF rastreado:** Req 8, Req 10
- **Story Points:** 3
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `POST /v1/auth/password-reset` retorna 202 para e-mail existente ou inexistente com mensagem idêntica
- [ ] PBT-03 API verde: corpo, código HTTP e categoria de mensagem idênticos; timing dentro da banda configurada
- [ ] Usuário com método Google bloqueado (EmailMethodSpec)

#### Tasks técnicas
- authentication/TASK-20: Endpoint POST /v1/auth/password-reset + PBT-03 API — TO DO — pendente Jira

#### Definition of Done
- [ ] PBT-03 API verde; API.Tests ≥ 80%; CI verde; Code review; PR mergeado

---

### US-015 — Logout que revoga sessão de forma idempotente

- **Épico:** authentication (EP-003)
- **RF rastreado:** Req 9
- **Story Points:** 3
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `POST /v1/auth/logout` sem JWT retorna 401; com JWT válido revoga e retorna 204
- [ ] Segunda chamada com mesma sessão já revogada retorna 204 sem erro
- [ ] `GET /v1/auth/me` retorna perfil do usuário autenticado com papéis e BUs

#### Definition of Done
- [ ] PBT-04 API verde; CI verde; Code review; PR mergeado

---

### US-018 — Rate limiting por IP e tenant (anti-bruteforce)

- **Épico:** authentication (EP-003)
- **RF rastreado:** Req 10, RNF 8
- **Story Points:** 3
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] Excesso de tentativas por IP → 429 `AUTH-ERR-040` sem revelar existência de conta
- [ ] Health checks de IdP e Redis expostos em /health (readiness + liveness)
- [ ] Logs estruturados sem PII (e-mail hash via Serilog policy); métricas de autenticação ativas
- [ ] Eventos de autenticação (login, logout, invite) publicados no audit-log via `IAuditWriter`
- [ ] `gitleaks` verde; p95 ≤ 1s no teste de carga do módulo

#### Tasks técnicas
- authentication/TASK-21: Health checks e readiness probes (IdP + Redis) — TO DO — pendente Jira
- authentication/TASK-22: Logs estruturados + mascaramento de PII (Serilog) — TO DO — pendente Jira
- authentication/TASK-23: Métricas e alertas de autenticação — TO DO — pendente Jira
- authentication/TASK-24: Auditoria de eventos de autenticação (append-only) — TO DO — pendente Jira
- authentication/TASK-25: Gates de segurança, isolamento cross-tenant (CI) e teste de performance — TO DO — pendente Jira

#### Definition of Done
- [ ] Métricas ativas; logs sem PII; gitleaks verde; p95 ≤ 1s; DoD design.md §19 completo
- [ ] CI verde; Code review; PR mergeado

---

### US-023 — brand.json público por slug via CDN

- **Épico:** tenant-administration (EP-004)
- **RF rastreado:** Req 9
- **Story Points:** 3
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `GET /api/v1/tenants/{slug}/brand.json` público (sem auth) retorna payload sem adminEmail
- [ ] Payload inclui `DerivedTones`, `wcagContrastOk`, `version` (timestamp de updatedAt)
- [ ] Slug inexistente retorna 404 com mensagem genérica (anti-enumeração)
- [ ] Headers de cache CDN configurados; blob GCS não público direto
- [ ] PBT-06 de isolamento RLS green como gate de CI

#### Tasks técnicas
- tenant-administration/TASK-12: EF Core + migrations + RLS + global query filter — TO DO — pendente Jira
- tenant-administration/TASK-13: TenantProvisioningSaga + GcpIdentityPlatformAdapter (PBT-07) — TO DO — pendente Jira
- tenant-administration/TASK-14: IBrandingAssetStorage (GCS) + ICdnInvalidator + cache slug — TO DO — pendente Jira
- tenant-administration/TASK-15: Outbox + IEventOutbox + publicador Pub/Sub — TO DO — pendente Jira
- tenant-administration/TASK-16: Testes de isolamento RLS + PBT-06 gate de CI — TO DO — pendente Jira
- tenant-administration/TASK-17: Endpoints de plataforma (provision, suspend, reactivate) — TO DO — pendente Jira
- tenant-administration/TASK-18: Endpoints de tenant (GET/PATCH tenant, GET/PUT branding) — TO DO — pendente Jira
- tenant-administration/TASK-19: Endpoint público brand.json + CDN cache + anti-enumeração — TO DO — pendente Jira
- tenant-administration/TASK-20: Contract tests de eventos + OpenAPI — TO DO — pendente Jira

#### Definition of Done
- [ ] Api.Tests ≥ 80%; PBT-06 verde (gate CI); Outbox operacional
- [ ] CI verde; Code review; PR mergeado

---

### US-081 — Provider de e-mail Resend conforme ADR-0005

- **Épico:** notification-delivery (EP-002) / transversal HITL#1
- **RF rastreado:** Req 4, ADR-0005
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `ResendEmailSender` (ou `PostmarkEmailSender` se Resend não tiver SDK .NET) implementando `IEmailProviderClient`; seleção via `IConfiguration` sem alterar código consumidor
- [ ] `IEmailProviderClient` (porta em Application) com `EmailMessageValidator` retornando `PermanentFailure` antes de chamar provedor para entrada inválida
- [ ] `EmailTemplateRenderer` determinístico (mesma entrada → mesma saída); HTML responsivo com `<meta name="viewport">`; links de 1 clique preservados
- [ ] `BrandingEmailDecorator` aplica branding estrito; tema padrão se `BrandingConfig` ausente
- [ ] `ResilientEmailSender` com Polly: timeout 10s, até 5 retries backoff+jitter, circuit breaker após 5 falhas; nunca lança ao chamador
- [ ] `EmailHasher` SHA-256 truncado; Serilog `DestructuringPolicy` bloqueando campo de e-mail em logs

#### Tasks técnicas
- notification-delivery/TASK-07: IEmailProviderClient (porta) + validações de borda — TO DO — pendente Jira
- notification-delivery/TASK-08: EmailTemplateRenderer — TO DO — pendente Jira
- notification-delivery/TASK-09: BrandingEmailDecorator — TO DO — pendente Jira
- notification-delivery/TASK-10: ResilientEmailSender (Polly) — TO DO — pendente Jira
- notification-delivery/TASK-11: ProviderResponseMapper (ACL, mapeamento total) — TO DO — pendente Jira
- notification-delivery/TASK-12: PostmarkEmailSender (ou ResendEmailSender) — TO DO — pendente Jira
- notification-delivery/TASK-13: SendGridEmailSender (fallback) — TO DO — pendente Jira
- notification-delivery/TASK-14: SecretManagerProvider + cache TTL — TO DO — pendente Jira
- notification-delivery/TASK-15: EmailHasher + Serilog DestructuringPolicy — TO DO — pendente Jira
- notification-delivery/TASK-16: EmailProviderHealthCheck + DI extensions — TO DO — pendente Jira

#### Definition of Done
- [ ] Application.Tests ≥ 85%; Infrastructure.Tests ≥ 70%
- [ ] `EmailSenderContractTestBase` verde contra todos os senders
- [ ] Architecture.Tests verde (nenhum SDK provedor em Contracts/Application)
- [ ] CI verde; Code review; PR mergeado

---

### US-008 — Branding do tenant aplicado em e-mails transacionais

- **Épico:** notification-delivery (EP-002)
- **RF rastreado:** Req 5, Req 6
- **Story Points:** 3
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] E-mail de convite enviado pelo authentication usa BrandingConfig do tenant via `IEmailSender`
- [ ] `BrandingEmailDecorator` injeta LogoUrl, PrimaryColor, SecondaryColor; tema padrão quando ausente
- [ ] HTML contém `<meta name="viewport">`; determinístico; links preservados

#### Definition of Done (incluso nos tasks de TASK-09 e TASK-10 acima)

---

### US-009 — Retry automático com circuit breaker no envio de e-mail

- **Épico:** notification-delivery (EP-002)
- **RF rastreado:** Req 8, Req 9
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] N retries configurável (padrão 5); backoff exponencial + jitter; `Bounced`/`Suppressed` não incrementam breaker
- [ ] Timeout por tentativa ≤ 10s; nenhuma exceção propagada ao chamador
- [ ] Renderização executada exatamente 1 vez por chamada `SendAsync` (PBT-05 estrutural)

#### Definition of Done (incluso em TASK-10 acima)

---

### US-011 — Health check do provedor de e-mail

- **Épico:** notification-delivery (EP-002)
- **RF rastreado:** Req 11
- **Story Points:** 2
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `EmailProviderHealthCheck`: ping leve sem enviar e-mail real; `Healthy`/`Unhealthy` sem PII e sem credencial
- [ ] Extensão `AddNotificationDelivery(IServiceCollection, IConfiguration)` registra toda a cadeia no DI

#### Definition of Done (incluso em TASK-16 acima)

---

## 2. Bugs Acompanhados

| ID | Severidade | Descrição | Status | Issue |
|---|---|---|---|---|
| — | — | Nenhum | — | — |

---

## 3. Riscos da Sprint

| Risco | Mitigação |
|---|---|
| Resend não tem SDK .NET oficial — HTTP direto com `HttpClientFactory` | Implementar adapter HTTP direto; `ProviderResponseMapper` abstrai diferenças de resposta |
| TenantProvisioningSaga (PBT-07): state parcial difícil de simular | Usar double fake de IdP + DB com injeção de falha em cada step |
| Sprint 5 é pesada (38 pts) — risco de não completar | Priorizar authentication API e notification-delivery infra; mover tenant-adm infra para Sprint 5 mesmo se precisar de extra |

---

## 4. Encerramento

- [ ] Todas as stories em DONE; todos os PRs mergeados
- [ ] Sprint encerrada no Jira (quando sincronizado)
- [ ] `progress-tracking.md` atualizado
