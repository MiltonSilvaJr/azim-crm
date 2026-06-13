# Module — Authentication

**Status:** Implementado
**Fase:** Fase 1 MVP
**Versão:** 1.0.0
**Implementação:** 6 ondas, 25 TASKs, 5 PBTs — 235 testes verdes

---

## 1. Visão Geral

Módulo responsável pela autenticação de usuários e gerenciamento de sessão por tenant. Atua como Anti-Corruption Layer (ACL) entre o modelo interno do Azim CRM e o GCP Identity Platform (Firebase Auth multi-tenant). Garante que mudanças no contrato do IdP externo não impactem o modelo de domínio interno.

Localização da solução: `services/authentication/Authentication.slnx`

---

## 2. Classificação

| Item | Valor |
|---|---|
| Tipo de Módulo | Application Module (ACL) |
| Deployable Candidato | azim-api |
| Bounded Context Relacionado | Identity & Access (BC-12) |
| Subdomínio DDD | Generic Subdomain |
| Tier / Criticidade | Tier 1 — sem autenticação, nenhuma funcionalidade do CRM é acessível |
| Status | Implementado |

---

## 3. Objetivo

Prover autenticação segura por tenant via GCP Identity Platform. Validar tokens de sessão (ID tokens JWT), carregar o contexto de usuário e tenant para a requisição, e proteger o modelo interno do CRM de qualquer mudança no contrato do GCP IdP.

---

## 4. Responsabilidades

- Validar ID tokens JWT emitidos pelo GCP Identity Platform por tenant.
- Extrair `user_id` e `tenant_id` do token e injetar no contexto da requisição (`AuthContext`).
- Implementar middleware de autenticação aplicado a todos os endpoints protegidos.
- Coordenar com o módulo organization o carregamento de `UserMembership` e papéis após autenticação.
- Implementar ACL: isolar o modelo interno do contrato do GCP Identity Platform.
- Emitir eventos auditáveis (`session_revoked`, `invite_activated`, `password_reset_requested`) via `IAuditEventEmitter`.
- Expor health checks em `/health/ready` (IdP + Redis) e `/health/live`.

---

## 5. Fora de Escopo

- Cadastro de usuários (pertence ao módulo organization via convite).
- Gestão de papéis e permissões (pertence ao módulo organization / RBAC).
- Provisionamento de tenant de identidade no GCP (pertence ao módulo tenant-administration).
- Autorização por recurso (RBAC aplicado nos próprios módulos de negócio).
- Autenticação de máquina a máquina entre workers (VAL-AUTH-01 — a confirmar).
- Login: realizado diretamente no GCP Identity Platform pelo frontend (Firebase SDK).

---

## 6. Capacidades Atendidas

| Código | Capability | Descrição |
|---|---|---|
| CAP-14 | Autenticação e Identidade | Login, sessão por tenant, validação de token, convite |

---

## 7. Bounded Context e Linguagem Ubíqua

| Termo | Definição |
|---|---|
| `identity_uid` | Identificador único do usuário no GCP Identity Platform (não o `user_id` interno); confinado exclusivamente em `Authentication.Infrastructure` |
| `session_token` | ID token JWT emitido pelo GCP Identity Platform; válido por tenant |
| `AuthContext` | Objeto interno com `user_id` (UUID interno), `tenant_id`, `email` — isolado do modelo do GCP |
| ACL (Anti-Corruption Layer) | Camada que traduz o modelo do GCP (`identity_uid`) para o modelo interno (`user_id`) |
| `TenantSlug` | Identificador legível do tenant (ex.: `acme`), normalizado para lowercase |
| `MembershipSet` | Conjunto de papéis do usuário por Business Unit; cacheado em Redis com TTL |

---

## 8. Estrutura da Solução

```text
services/authentication/
├── Authentication.slnx
├── Directory.Build.props
├── global.json          (.NET 10, C# 13)
├── observability/
│   ├── alerts.yaml      (4 alertas Cloud Monitoring — TASK-23)
│   └── performance.md   (SLO p95 ≤ 1 s — TASK-25)
├── src/
│   ├── Authentication.Api/          (Minimal API, endpoints, middlewares)
│   ├── Authentication.Application/  (Use cases, portas, serviços)
│   ├── Authentication.Contracts/    (DTOs, catálogo de erros AUTH-ERR-*)
│   ├── Authentication.Domain/       (VOs, máquina de estados, specifications)
│   └── Authentication.Infrastructure/  (Adapters: Firebase, Postgres, Redis)
└── tests/
    ├── Authentication.Api.Tests/            (59 testes — integração de endpoints)
    ├── Authentication.Application.Tests/    (39 testes — serviços de aplicação)
    ├── Authentication.Architecture.Tests/   (14 testes — regras NetArchTest)
    ├── Authentication.Domain.Tests/         (72 testes — VOs, states, specs, PBTs)
    └── Authentication.Infrastructure.Tests/ (51 testes — adapters, health checks, métricas)
```

**Total: 235 testes, 0 falhas, 0 warnings de build.**

---

## 9. APIs Expostas

| Método | Endpoint | Autenticação | Finalidade |
|---|---|---|---|
| GET | `/v1/auth/me` | Obrigatória | Retorna `AuthContext` do usuário autenticado |
| POST | `/v1/auth/logout` | Obrigatória | Revoga refresh tokens (idempotente — PBT-04) |
| POST | `/v1/invites` | Obrigatória (admin) | Cria convite de ativação para novo usuário |
| POST | `/v1/invites/activate` | Pública | Ativa convite com link de ativação do IdP |
| POST | `/v1/auth/password-reset` | Pública | Solicita reset (resposta uniforme — PBT-03) |
| GET | `/health/ready` | Nenhuma | Readiness probe: verifica IdP + Redis |
| GET | `/health/live` | Nenhuma | Liveness probe: sempre healthy se processo está vivo |

---

## 10. Eventos Auditáveis Emitidos

Via porta `IAuditEventEmitter` (fail-open — falha não bloqueia o fluxo principal):

| Evento | Quando | Emitido por |
|---|---|---|
| `session_revoked` | Logout bem-sucedido (nova revogação) | `SessionRevocationService` |
| `invite_activated` | Ativação de convite bem-sucedida | `InviteActivationService` |
| `password_reset_requested` | Reset solicitado para usuário com método senha | `PasswordResetService` |
| `user_authenticated` | Pendente — evento client-side (DD-010 a registrar) | — |
| `password_reset_completed` | Pendente — ocorre no IdP client-side (DD-004) | — |

Nenhum evento carrega `identity_uid`, senha ou token (verificado por teste — TASK-24).

---

## 11. Observabilidade

### 11.1 Métricas (`services/authentication/observability/alerts.yaml`)

| Métrica | Tipo | Labels | SLO |
|---|---|---|---|
| `auth_token_validation_success_total` | Counter | `tenant_id` | — |
| `auth_token_validation_failure_total` | Counter | `tenant_id`, `causa` | — |
| `auth_rate_limit_block_total` | Counter | `tenant_id` | — |
| `auth_latency_ms` | Histogram | — | p95 ≤ 1000 ms |

### 11.2 Alertas Cloud Monitoring

| Nome | Severidade | Condição |
|---|---|---|
| `auth_token_validation_failure_high_rate` | WARNING | > 10 falhas/s por 5 min |
| `auth_401_mass_alert` | CRITICAL | > 50 falhas expired/invalid_signature por 2 min |
| `auth_rate_limit_mass_block` | WARNING | > 100 bloqueios/s por 2 min |
| `auth_latency_p95_slo_breach` | WARNING | p95 > 1000 ms por 5 min |

### 11.3 Logs Estruturados

- Serilog com política de destructuring mascarando PII (email, nome → `***`).
- `correlationId` e `tenantId` presentes em 100% dos logs de contexto autenticado.
- `identity_uid` confinado exclusivamente em `Authentication.Infrastructure` (Architecture.Tests gate).

---

## 12. Health Checks

| Endpoint | Inclui | Comportamento esperado |
|---|---|---|
| `/health/ready` | IdP + Redis | Unhealthy quando IdP ou Redis indisponível |
| `/health/live` | Apenas processo | Always healthy — não depende do IdP |

---

## 13. Segurança

- `identity_uid` jamais sai de `Authentication.Infrastructure` (gate Architecture.Tests — DD-001).
- Nenhum segredo no repositório (gate `gitleaks` no CI — RNF 7.3, DD-005).
- Service account Firebase Admin SDK via GCP Secret Manager (DD-005).
- Anti-enumeração em `/v1/auth/password-reset` (corpo, código e timing uniformes — PBT-03, RISK-AUTH-05).
- Isolamento cross-tenant: token de tenant A rejeitado em contexto de tenant B (PBT-02, RNF 1.4).
- Rate limiting por IP e tenant com resposta genérica (RNF 8, AUTH-ERR-040).

---

## 14. Decisões de Design

| Código | Decisão | Referência |
|---|---|---|
| DD-001 | `identity_uid` confinado em Infrastructure; Firebase SDK não vaza | design.md § 3, Architecture.Tests |
| DD-002 | Delegação de validação JWT ao Firebase Admin SDK | design.md § 6.3 |
| DD-003 | Caminho quente por middleware; auditoria injetada nos serviços | `AuditBehavior.cs` |
| DD-004 | `password_reset_completed` é evento client-side (no callback do IdP) | `AuditBehavior.cs` |
| DD-005 | Segredos via Secret Manager; gitleaks no CI | design.md § 13 |
| DD-010 | (a registrar) `user_authenticated` — decisão de custo/benefício Tier 1 | `AuditBehavior.cs` |

---

## 15. Integrações

| Sistema/Módulo | Tipo | Direção | Observações |
|---|---|---|---|
| GCP Identity Platform | Firebase Admin SDK | Saída | Adapter em Infrastructure; valida JWT por tenant |
| organization | PostgreSQL (consulta) | Saída | Resolve `identity_uid` → `user_id`, memberships |
| Memorystore / Redis | Cache | Saída | Cache de memberships (TTL configurável) |
| audit-log | `IAuditEventEmitter` (porta) | Saída | Fail-open; integração em Infrastructure via `IAuditWriter` |
| notification-delivery | `IEmailSender` (porta) | Saída | Envia e-mails de convite e reset de senha |

---

## 16. Configuração e Variáveis de Ambiente

| Variável | Descrição | Fonte |
|---|---|---|
| `Firebase__ProjectId` | Project ID do GCP | Secret Manager |
| `Firebase__ServiceAccountKeyJson` | JSON da service account key (Base64) | Secret Manager |
| `ConnectionStrings__Default` | Connection string do PostgreSQL | Secret Manager |
| `Redis__ConnectionString` | Connection string do Redis | Secret Manager |
| `PasswordReset__ConstantDelayMs` | Delay constante em `/password-reset` (anti-timing) | appsettings |

---

## 17. Como Executar Localmente

```bash
# Pré-requisitos: .NET 10, Docker (para Testcontainers)

cd services/authentication

# Build
dotnet build Authentication.slnx

# Testes (todos — inclui Testcontainers para testes de integração com Postgres)
dotnet test Authentication.slnx

# Testes por projeto
dotnet test tests/Authentication.Domain.Tests/
dotnet test tests/Authentication.Application.Tests/
dotnet test tests/Authentication.Infrastructure.Tests/
dotnet test tests/Authentication.Api.Tests/
dotnet test tests/Authentication.Architecture.Tests/

# Testes de segurança (cross-tenant, PBT-02)
dotnet test Authentication.slnx --filter "FullyQualifiedName~CrossTenantIsolation"

# API local (requer appsettings.Development.json configurado)
dotnet run --project src/Authentication.Api/
```

---

## 18. Ondas de Implementação

| Onda | Escopo | TASKs | Status |
|---|---|---|---|
| 1 — Bootstrap | Solução .NET 10, 10 projetos, Architecture.Tests | TASK-01..02 | Concluído |
| 2 — Domain | VOs, máquina de estados, Specifications, PBT-01, PBT-05 | TASK-03..04 | Concluído |
| 3 — Application | Portas, serviços de aplicação, PBT-04 | TASK-05..09 | Concluído |
| 4 — Infrastructure | Adapters Firebase, Postgres, Redis, PBT-02, PBT-05 | TASK-10..14 | Concluído |
| 5 — API + Contracts | Middlewares, endpoints, DTOs, catálogo de erros, PBT-03, PBT-04 | TASK-15..20 | Concluído |
| 6 — Hardening | Health checks, Serilog, métricas, auditoria, gates | TASK-21..25 | Concluído |

---

## 19. Property-Based Tests

| PBT | Propriedade | Camada | Status |
|---|---|---|---|
| PBT-01 | Sessão sempre escopada a um único `tenant_id` | Domain.Tests | Verde |
| PBT-02 | Isolamento cross-tenant: token de tenant A nunca autentica em B | Infrastructure.Tests | Verde (100 casos FsCheck) |
| PBT-03 | Anti-enumeração: corpo, código e timing indistinguíveis | Api.Tests | Verde |
| PBT-04 | Logout idempotente: N ≥ 1 chamadas → estado final único sem erro | Application.Tests + Api.Tests | Verde |
| PBT-05 | Link expirado/consumido nunca concede acesso | Domain.Tests + Infrastructure.Tests | Verde |

---

## 20. DoD — Itens Verificados (design.md § 19)

| Item | Status | Observação |
|---|---|---|
| Req 1..11 e RNF 1..10 com contraparte técnica implementada | Implementado | Rastreado em tasks.md § 5 |
| Middlewares na ordem correta; nenhuma rota sem validação | Implementado | Program.cs: Correlation → TenantResolution → RateLimiting → Authentication |
| `IIdentityProvider` único ponto de acoplamento; Architecture.Tests verde | Implementado | 14 testes NetArchTest verdes |
| Token de tenant A não autentica em B (PBT-02) | Implementado | Gate de CI: `authentication-cross-tenant-gate` |
| Anti-enumeração verificada (PBT-03) | Implementado | 202 uniforme para e-mail existente/inexistente |
| Logout idempotente com invalidação global (PBT-04) | Implementado | `SessionRevocationService` — captura "já revogado" como sucesso |
| Link expirado/consumido nunca concede acesso (PBT-05) | Implementado | `InviteUsableSpec` + Domain.Tests |
| Catálogo de erros implementado; sem PII nas mensagens | Implementado | AUTH-ERR-001..090 em `Authentication.Contracts` |
| Logs sem PII/`identity_uid`; `correlationId` e `tenantId` presentes | Implementado | Serilog + `MaskEmailDestructuringPolicy` |
| Métricas e alertas configurados | Implementado | `AuthMetrics` + `observability/alerts.yaml` |
| Segredos via Secret Manager; `gitleaks` no CI | Implementado | `.gitleaks.toml` + CI job `authentication-gitleaks-gate` |
| Circuit breaker/timeout no adapter; health/readiness | Implementado | `IdentityProviderHealthCheck` + `RedisHealthCheck` |
| p95 ≤ 1 s documentado; baseline via `auth_latency_ms` | Documentado | `observability/performance.md`; k6 pendente de infraestrutura |
| VAL-AUTH-02 e VAL-09 registrados como pendências de go-live | Registrado | Ver seção 21 abaixo |
| README sincronizado | Implementado | Este arquivo |

---

## 21. Pontos a Validar (Pendências de Go-Live)

| Código | Ponto | Impacto | Status |
|---|---|---|---|
| VAL-AUTH-01 | Autenticação M2M entre workers (digest-worker → api) | Define mecanismo de autenticação interna | A confirmar com produto |
| VAL-AUTH-02 | Duração da sessão e política de refresh token (DD-008) | Experiência do usuário e segurança | A confirmar com produto/segurança |
| VAL-09 | Autenticação interna — subdomínio Identity & Access SD-12 (DD-009) | Scope de autenticação M2M | A confirmar com segurança |
| — | Teste de carga k6 com infraestrutura real | Validação do SLO p95 ≤ 1 s em staging | A executar antes do go-live |
| DD-010 | `user_authenticated` — custo/benefício de emissão em Tier 1 | Completude de auditoria de autenticação | A registrar com arquitetura |

---

## 22. Riscos

| Código | Risco | Impacto | Mitigação |
|---|---|---|---|
| RISK-AUTH-01 | Indisponibilidade do GCP Identity Platform | Nenhum usuário consegue autenticar | Circuit breaker; `/health/ready` + alerta `auth_401_mass_alert` |
| RISK-AUTH-02 | Cache de memberships desatualizado após mudança de papel | Usuário opera com papel antigo | TTL configurável (5 min); invalidação explícita |
| RISK-AUTH-03 | Vazamento de `identity_uid` para fora de Infrastructure | Acoplamento ao GCP | Architecture.Tests gate (14 regras NetArchTest) |
| RISK-AUTH-05 | Oráculo de timing em `/password-reset` | Enumeração de contas | PBT-03 verde; delay constante configurável |
| RISK-AUTH-06 | VAL-AUTH-02/VAL-09 não confirmados antes do go-live | TTL de sessão inadequado | Gate de confirmação explícito no DoD (seção 21) |

---

## 23. Referências

| Documento | Seção |
|---|---|
| `docs/product/modules/authentication/requirements.md` | Req 1..11, RNF 1..10, PBT-01..05 |
| `docs/product/modules/authentication/design.md` | Arquitetura, DDs, DoD § 19 |
| `docs/product/modules/authentication/tasks.md` | 25 TASKs, 6 ondas, matriz de rastreabilidade |
| `docs/product/adr/0001-isolamento-multi-tenant-defesa-em-profundidade.md` | ADR-0001 — Multi-tenancy RLS |
| `docs/product/adr/0007-stack-observabilidade-gcp.md` | ADR-0007 — Stack de observabilidade |
| `services/authentication/observability/alerts.yaml` | Alertas Cloud Monitoring |
| `services/authentication/observability/performance.md` | SLO de latência e script k6 |
| `.github/workflows/staging.yml` | Gates de CI (gitleaks, Architecture.Tests, PBT-02) |
