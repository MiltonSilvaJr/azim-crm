# Tasks — BC-12 — Authentication

- Versão: 0.1.1
- Data: 2026-06-13
- Status: Aprovado para desenvolvimento
- Referência base requirements: docs/product/modules/authentication/requirements.md v0.1.0
- Referência base design: docs/product/modules/authentication/design.md v0.1.0
- ADRs aplicáveis: ADR-0005 (IEmailSender, a formalizar), ADR-0007 (stack GCP, a formalizar)
- Rules aplicáveis: `.forge/rules/architecture/clean-architecture.md`, `.forge/rules/architecture/ddd.md`, `.forge/rules/architecture/jwt-authentication.md`, `.forge/rules/architecture/jwt-permissions.md`, `.forge/rules/architecture/api-and-contracts.md`, `.forge/rules/architecture/security-and-compliance.md`, `.forge/rules/architecture/security-and-secrets.md`, `.forge/rules/architecture/observability.md`, `.forge/rules/domain/audit-immutability.md`, `.forge/rules/conventions/language-policy.md`, `.forge/rules/conventions/naming.md`, `.forge/rules/testing/tdd.md`, `.forge/rules/testing/quality-gates.md`

## Histórico de Versões

| Versão | Data | Status | Descrição da alteração |
|--------|------|--------|------------------------|
| 0.1.0 | 2026-06-11 | Rascunho para revisão | Criação inicial do plano de tasks a partir de requirements.md v0.1.0 e design.md v0.1.0; 25 TASKs em 6 ondas, 5 PBTs cobertos. |
| 0.1.1 | 2026-06-13 | Aprovado para desenvolvimento | Aprovação humana (HITL #1); execução via `/forge:coding-loop` autônomo (6 ondas). |

---

## 1. Convenções de Implementação

### 1.1 TDD-first

Toda implementação com lógica verificável segue o ciclo:

1. Red — escrever teste que falha
2. Green — implementar o mínimo para passar
3. Refactor — melhorar sem alterar comportamento

Nenhuma implementação de VO, specification, handler, middleware, adapter, endpoint ou contrato é considerada concluída sem teste correspondente.

### 1.2 Property-Based Testing

PBT é obrigatório para os seguintes casos do módulo:

| PBT | Propriedade | Camada |
|-----|-------------|--------|
| PBT-01 | Sessão sempre escopada a um único `tenant_id`: para qualquer (usuário, método, slug), `AuthContext.tenant_id` == tenant resolvido | Domain.Tests |
| PBT-02 | Isolamento cross-tenant: token válido de tenant A nunca produz `AuthContext` em contexto de tenant B (HTTP 401) | Application.Tests + Infrastructure.Tests |
| PBT-03 | Resposta indistinguível: corpo, código HTTP e categoria de mensagem do `/password-reset` são idênticos para e-mail existente e inexistente; timing dentro da banda configurada | Api.Tests |
| PBT-04 | Idempotência do logout: N ≥ 1 chamadas consecutivas de `POST /logout` produzem estado final único (sessão inválida) sem erro adicional | Application.Tests + Api.Tests |
| PBT-05 | Link expirado/consumido nunca concede acesso: nenhuma sequência de operações reabilita link em estado `expired` ou `consumed` | Domain.Tests + Infrastructure.Tests |

Geradores usam FsCheck (xUnit) com `Arbitrary` para tokens arbitrários, pares de tenant, sequências de logout e estados de link.

### 1.3 Bite-sized Tasks

Cada subtask estimada para menos de 2 horas. Cada TASK completa em até 1 dia; excepcionalmente 2 dias com escopo claro e entregável verificável.

### 1.4 Branch Model

```text
<tipo>/authentication/<NN>-<slug>
```

Exemplos:

```text
feat/authentication/01-bootstrap-solution
test/authentication/02-architecture-tests
feat/authentication/10-firebase-adapter
```

### 1.5 Git Worktree

```sh
git worktree add .forge/worktrees/authentication/<NN>-<slug> -b <branch>
```

### 1.6 Encerramento de TASK

Cada TASK encerra com:

- testes locais verdes
- coverage gate da camada atendido ou justificativa registrada
- lint/format executado
- documentação atualizada quando aplicável
- commit em Conventional Commits (`feat:`, `test:`, `fix:`, `chore:`)
- push da branch

### 1.7 Encerramento de Onda

- todas as TASKs da onda com status `[X]`
- CI verde
- conflitos resolvidos
- PR aberto ou atualizado (1 PR por onda)
- checklist de revisão preenchido

### 1.8 Early Exit

Se uma subtask falhar:

- marcar status como `[-]`
- registrar ponto de falha, comando executado e erro principal
- não mascarar falha com implementação especulativa
- deixar contexto suficiente para retomada

### 1.9 Convenção de Status

- `[ ]` Não iniciado
- `[-]` Em progresso
- `[X]` Concluído
- `[!]` Falhou — exige intervenção humana

### 1.10 IDs canônicos

```
TASK-NN — <título>          ← unidade atômica
  ST-MM — <subtask>         ← etapas TDD internas (numeração reinicia por TASK)
```

Onda é atributo de agrupamento visual, nunca entra no ID da TASK.

### 1.11 Convenções específicas do módulo

- `identity_uid` confinado exclusivamente a `Authentication.Infrastructure`; nunca aparece em Domain, Application, Contracts, Api, logs, rastreamentos ou mensagens de erro.
- `AuthContext` é imutável após composição; nunca mutado depois de injetado na requisição.
- Senhas nunca armazenadas, processadas nem logadas; autenticação integralmente delegada ao Identity Platform (RNF 1).
- Toda mensagem de erro ao consumidor referencia apenas os códigos do catálogo (design.md § 12); exceções do SDK do Firebase nunca propagam ao chamador.
- `tenant_id` é sempre derivado da resolução do slug; nunca aceito do chamador.
- Fluxos de anti-enumeração (password-reset, login com falha, invite-activate) aplicam delay constante para equalizar timing antes de retornar (PBT-03, Req 10.2).
- Cache de memberships usa chave `auth:membership:{tenant_id}:{user_id}` (nenhuma entrada compartilhada entre tenants).

---

## 2. Status Geral

| TASK | Título | Onda | Branch | Status |
|------|--------|------|--------|--------|
| TASK-01 | Bootstrap solution e 10 projetos Clean Architecture | Onda 1 | `feat/authentication/01-bootstrap-solution` | [ ] |
| TASK-02 | Architecture.Tests: regra de dependência e confinamento do ACL | Onda 1 | `test/authentication/02-architecture-tests` | [ ] |
| TASK-03 | Objetos de valor imutáveis do domínio | Onda 2 | `feat/authentication/03-domain-value-objects` | [ ] |
| TASK-04 | Máquina de estados de sessão e Specifications + PBT-01 | Onda 2 | `feat/authentication/04-state-machine-specs` | [ ] |
| TASK-05 | Portas de aplicação (interfaces de saída) | Onda 3 | `feat/authentication/05-application-ports` | [ ] |
| TASK-06 | SessionTokenValidator + AuthContextComposer | Onda 3 | `feat/authentication/06-token-validator-composer` | [ ] |
| TASK-07 | SessionRevocationService + PBT-04 (idempotência de logout) | Onda 3 | `feat/authentication/07-session-revocation` | [ ] |
| TASK-08 | InviteActivationService + PBT-05 unitário | Onda 3 | `feat/authentication/08-invite-activation` | [ ] |
| TASK-09 | PasswordResetService + PBT-03 unitário (anti-enumeração) | Onda 3 | `feat/authentication/09-password-reset-service` | [ ] |
| TASK-10 | FirebaseIdentityProvider (adapter ACL + circuit breaker) | Onda 4 | `feat/authentication/10-firebase-adapter` | [ ] |
| TASK-11 | JwksTokenVerifier + cache de chaves públicas | Onda 4 | `feat/authentication/11-jwks-verifier` | [ ] |
| TASK-12 | MembershipCacheRepository (Redis + invalidação por evento) | Onda 4 | `feat/authentication/12-membership-cache` | [ ] |
| TASK-13 | SecretManagerProvider + gate gitleaks no CI | Onda 4 | `feat/authentication/13-secret-manager` | [ ] |
| TASK-14 | OrganizationUserDirectory + TenantDirectory | Onda 4 | `feat/authentication/14-directory-adapters` | [ ] |
| TASK-15 | CorrelationMiddleware + TenantResolutionMiddleware | Onda 5 | `feat/authentication/15-tenant-resolution-middleware` | [ ] |
| TASK-16 | AuthenticationMiddleware + RateLimitingMiddleware | Onda 5 | `feat/authentication/16-auth-rate-limit-middleware` | [ ] |
| TASK-17 | Contracts, DTOs e catálogo de erros (AUTH-ERR-001..090) | Onda 5 | `feat/authentication/17-contracts-error-catalog` | [ ] |
| TASK-18 | Endpoints GET /v1/auth/me e POST /v1/auth/logout | Onda 5 | `feat/authentication/18-auth-controller` | [ ] |
| TASK-19 | Endpoints POST /v1/auth/invites e /invites/activate | Onda 5 | `feat/authentication/19-invite-controller` | [ ] |
| TASK-20 | Endpoint POST /v1/auth/password-reset + PBT-03 API | Onda 5 | `feat/authentication/20-password-reset-controller` | [ ] |
| TASK-21 | Health checks e readiness probes (IdP + Redis) | Onda 6 | `feat/authentication/21-health-checks` | [ ] |
| TASK-22 | Logs estruturados + mascaramento de PII (Serilog) | Onda 6 | `feat/authentication/22-structured-logging` | [ ] |
| TASK-23 | Métricas e alertas de autenticação | Onda 6 | `feat/authentication/23-metrics-alerts` | [ ] |
| TASK-24 | Auditoria de eventos de autenticação (append-only) | Onda 6 | `feat/authentication/24-audit-events` | [ ] |
| TASK-25 | Gates de segurança, isolamento cross-tenant (CI) e teste de performance | Onda 6 | `feat/authentication/25-security-perf-gates` | [ ] |

---

## 3. Ondas de Implementação

| Onda | Foco | TASKs | Critério de fechamento |
|------|------|-------|------------------------|
| Onda 1 | Bootstrap | TASK-01, TASK-02 | Solution buildável; Architecture.Tests verdes; regra de dependência enforçada; CI mínimo verde |
| Onda 2 | Domain | TASK-03, TASK-04 | Domain.Tests ≥ 95%; PBT-01 verde; VOs imutáveis; Specifications cobrindo todas as regras de acesso |
| Onda 3 | Application | TASK-05..TASK-09 | Application.Tests ≥ 85%; PBT-03, PBT-04, PBT-05 unitários verdes; portas estáveis sem referência ao SDK do Firebase |
| Onda 4 | Infrastructure | TASK-10..TASK-14 | Infrastructure.Tests ≥ 70%; adapter Firebase contra emulador/WireMock; cache; secret manager; PBT-02 integração verde |
| Onda 5 | API + Contracts | TASK-15..TASK-20 | Api.Tests ≥ 80%; catálogo de erros completo; PBT-03 API e PBT-04 API verdes; ordem de middlewares validada |
| Onda 6 | Hardening | TASK-21..TASK-25 | Métricas ativas; logs sem PII; gitleaks verde; p95 ≤ 1 s no teste de carga; DoD do design.md § 19 completo |

**Risco principal por onda:**
- Onda 2: invariante de `IdentityRef` não cruzar para Domain (requer Architecture.Tests prévio — TASK-02)
- Onda 3: contratos de porta estáveis antes da implementação concreta (risco de retrabalho)
- Onda 4: configuração do emulador Firebase e WireMock para JWKS (RISK-AUTH-01 em testes)
- Onda 5: timing uniforme do `/password-reset` sem comprometer p95 (RISK-AUTH-05)
- Onda 6: VAL-AUTH-02 e VAL-09 não confirmados antes do go-live (RISK-AUTH-06, DD-008, DD-009)

---

## 4. Tarefas

### TASK-01 — Bootstrap solution e 10 projetos Clean Architecture

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 1 — Bootstrap |
| **Branch** | `feat/authentication/01-bootstrap-solution` |
| **Worktree** | `git worktree add .forge/worktrees/authentication/01-bootstrap-solution -b feat/authentication/01-bootstrap-solution` |
| **Status** | [ ] |
| **Depende de** | Não aplicável |
| **Entregável** | `Authentication.sln` com 5 projetos de produção + 5 de teste compilando; referências entre projetos corretas; pacotes base instalados |
| **Mapeia** | design.md § 3 (estrutura da solução); DD-001; `.forge/rules/architecture/clean-architecture.md` |
| **Camada principal** | DevOps / Architecture |

#### Objetivo

Criar a estrutura de solução com os projetos `Authentication.Contracts`, `Authentication.Domain`, `Authentication.Application`, `Authentication.Infrastructure` e `Authentication.Api` mais os respectivos projetos de teste (`.Domain.Tests`, `.Application.Tests`, `.Infrastructure.Tests`, `.Api.Tests`, `.Architecture.Tests`). Configurar referências entre projetos conforme o grafo de dependência do design (seção 3). Instalar pacotes base: xUnit, FsCheck.xUnit, FluentAssertions, NetArchTest, pacotes ASP.NET Core mínimos.

#### Subtasks

- [ ] **ST-01 — Red:** em `Authentication.Architecture.Tests`, escrever um teste NetArchTest que falha afirmando que `Authentication.Domain` não referencia `Authentication.Infrastructure` (a solução ainda não existe).
- [ ] **ST-02 — Green:** criar `Authentication.sln` e os 10 projetos; configurar `<ProjectReference>` conforme grafo da seção 3 do design; instalar pacotes base; o teste NetArchTest passa.
- [ ] **ST-03 — Refactor:** validar `Directory.Build.props`; garantir que `global.json` define a versão de SDK; remover referências desnecessárias.
- [ ] **ST-04 — Encerramento:** `dotnet build Authentication.sln` sem erros; `dotnet test` nos Architecture.Tests verde; `chore(authentication): bootstrap solution com 10 projetos Clean Architecture`; push.

#### Critérios de Aceite

- [ ] `dotnet build Authentication.sln` sem erros ou warnings relevantes
- [ ] Grafo de referências conforme design.md § 3 (Domain → Contracts apenas; Application → Domain + Contracts; Infrastructure → Application + Domain + Contracts; Api → Application + Contracts)
- [ ] `Authentication.Architecture.Tests` compila e executa (mesmo que com 1 teste inicial)
- [ ] Nenhuma referência circular

---

### TASK-02 — Architecture.Tests: regra de dependência e confinamento do ACL

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 1 — Bootstrap |
| **Branch** | `test/authentication/02-architecture-tests` |
| **Worktree** | `git worktree add .forge/worktrees/authentication/02-architecture-tests -b test/authentication/02-architecture-tests` |
| **Status** | [ ] |
| **Depende de** | TASK-01 |
| **Entregável** | Suite completa de `Architecture.Tests` garantindo: (a) regra de dependência entre camadas; (b) SDK do Firebase Admin ausente fora de `Infrastructure`; (c) símbolo `identity_uid` ausente fora de `Infrastructure` |
| **Mapeia** | Req 6.2, Req 6.3; DD-001; RISK-AUTH-03; design.md § 3, § 13 |
| **Camada principal** | Tests / Architecture |

#### Objetivo

Implementar a suite de arquitetura que será gate obrigatório do CI durante todo o desenvolvimento. Esses testes falham imediatamente se qualquer tipo do Firebase Admin SDK ou o símbolo `identity_uid` vazar para fora de `Authentication.Infrastructure`. São o mecanismo de detecção precoce de violação do ACL (DD-001).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes falhando para: (1) `Domain` não referencia `Infrastructure`; (2) `Application` não referencia `Infrastructure`; (3) nenhum namespace `Firebase` aparece em `Domain`/`Application`/`Contracts`/`Api`; (4) nenhuma propriedade/campo com nome `identity_uid` ou `IdentityUid` fora de `Infrastructure`.
- [ ] **ST-02 — Green:** completar a implementação dos testes usando NetArchTest; validar que passam com a estrutura vazia dos projetos (nenhuma violação presente ainda).
- [ ] **ST-03 — Refactor:** extrair predicados de arquitetura em classe `ArchitectureRules` reutilizável; documentar cada regra com referência ao requisito.
- [ ] **ST-04 — Encerramento:** todos os testes de arquitetura verdes; adicionados ao gate de CI como step obrigatório antes de qualquer merge; `test(arch): enforce ACL boundaries e clean architecture rules`; push.

#### Critérios de Aceite

- [ ] Testes de regra de dependência verdes (4 regras mínimas)
- [ ] Teste de confinamento do Firebase SDK verde
- [ ] Teste de confinamento de `identity_uid` verde
- [ ] Gate registrado na configuração de CI do projeto
- [ ] Nenhum warning novo introduzido

---

### TASK-03 — Objetos de valor imutáveis do domínio

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `feat/authentication/03-domain-value-objects` |
| **Worktree** | `git worktree add .forge/worktrees/authentication/03-domain-value-objects -b feat/authentication/03-domain-value-objects` |
| **Status** | [ ] |
| **Depende de** | TASK-01, TASK-02 |
| **Entregável** | `AuthContext`, `Session`, `TenantSlug`, `MembershipSet` em `Authentication.Domain`; `IdentityRef` em `Authentication.Infrastructure` (não em Domain); testes de imutabilidade e igualdade por valor |
| **Mapeia** | Req 5.2, Req 5.3, Req 6.2, Req 6.3; design.md § 4.3; PBT-01 |
| **Camada principal** | Domain |

#### Objetivo

Implementar os cinco objetos de valor definidos no design (seção 4.3). `IdentityRef` é o único que carrega `identity_uid`; fica em `Authentication.Infrastructure` e nunca cruza para `AuthContext`. Os demais pertencem a `Authentication.Domain`. Todos são imutáveis (campos `readonly` ou `init`), com igualdade por valor (record ou `IEquatable`).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de unidade falhando para imutabilidade (tentativa de mutação após criação deve falhar em compile-time ou lançar exceção), igualdade por valor (dois `AuthContext` com mesmo conteúdo são iguais) e invariantes (ex.: `AuthContext` sem `user_id` lança `ArgumentException`).
- [ ] **ST-02 — Green:** implementar `AuthContext` (user_id, tenant_id, email, roles, memberships), `Session` (state, tenant_id, expires_at, issued_at, revocation_checked), `TenantSlug` (value normalizado, não vazio), `MembershipSet` (entries BU→role, cached_at para TTL) em `Authentication.Domain`; `IdentityRef` (identity_uid, firebase_tenant, email, sign_in_provider) em `Authentication.Infrastructure`.
- [ ] **ST-03 — Refactor:** extrair validações comuns de string/UUID para método auxiliar; garantir que `Architecture.Tests` (TASK-02) continua verde (IdentityRef não vaza para Domain).
- [ ] **ST-04 — Encerramento:** Domain.Tests para VOs verdes; `feat(domain): add domain value objects AuthContext Session TenantSlug MembershipSet`; push.

#### Critérios de Aceite

- [ ] Todos os VOs imutáveis por construção (record ou init-only)
- [ ] Igualdade por valor implementada e testada
- [ ] Invariantes validadas no construtor e testadas
- [ ] `IdentityRef` presente apenas em `Authentication.Infrastructure`
- [ ] Architecture.Tests (TASK-02) continuam verdes
- [ ] Domain.Tests coverage ≥ 95% para os VOs implementados

---

### TASK-04 — Máquina de estados de sessão e Specifications + PBT-01

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 2 — Domain |
| **Branch** | `feat/authentication/04-state-machine-specs` |
| **Worktree** | `git worktree add .forge/worktrees/authentication/04-state-machine-specs -b feat/authentication/04-state-machine-specs` |
| **Status** | [ ] |
| **Depende de** | TASK-03 |
| **Entregável** | `SessionState` enum + regras de transição; `TokenValiditySpec`, `TenantMatchSpec`, `ActiveUserSpec`, `InviteUsableSpec`, `EmailMethodSpec`; PBT-01 verde |
| **Mapeia** | Req 4.2, Req 5.4, Req 7.4, Req 7.5, Req 8.4, Req 9.4; design.md § 4.5, § 4.6; PBT-01, PBT-05 |
| **Camada principal** | Domain |

#### Objetivo

Implementar a máquina de estados de sessão (`anonymous → authenticated → expired/revoked`) e as Specifications que governam o acesso. Confirmar que a invariante PBT-01 ("sessão sempre escopada a um tenant") é verificável por teste de propriedade gerado no domínio: para qualquer `AuthContext` gerado, `tenant_id` é único e não nulo.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de transição de estados (ex.: `authenticated → expired` quando `now ≥ expires_at`; tentativa de ir diretamente de `expired` para acesso autorizado deve falhar); escrever PBT-01 com FsCheck gerando `AuthContext` arbitrário e verificando `tenant_id != null && exatamente um tenant_id`.
- [ ] **ST-02 — Green:** implementar `SessionState` (anonymous, authenticated, expired, revoked) + método `Session.IsAccessible()` seguindo invariante de seção 4.5; implementar as cinco Specifications como pure functions/predicados.
- [ ] **ST-03 — Refactor:** garantir que `InviteUsableSpec` cobre o diagrama de estados do link (issued → consumed | expired) da seção 16.5; nenhuma Specification tem efeito colateral.
- [ ] **ST-04 — Encerramento:** PBT-01 verde (mínimo 100 casos gerados pelo FsCheck); todos os testes de transição verdes; `feat(domain): state machine, specifications e PBT-01`; push.

#### Critérios de Aceite

- [ ] Todas as transições válidas e inválidas de `SessionState` testadas
- [ ] Cinco Specifications implementadas e testadas unitariamente
- [ ] PBT-01 verde com FsCheck (≥ 100 casos)
- [ ] Máquina de estados do link (issued/consumed/expired) coberta por testes
- [ ] Domain.Tests coverage ≥ 95% acumulado (VOs + estado + specs)

---

### TASK-05 — Portas de aplicação (interfaces de saída)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/authentication/05-application-ports` |
| **Worktree** | `git worktree add .forge/worktrees/authentication/05-application-ports -b feat/authentication/05-application-ports` |
| **Status** | [ ] |
| **Depende de** | TASK-03 |
| **Entregável** | Interfaces `IIdentityProvider`, `IUserDirectory`, `ITenantDirectory`, `IEmailSender`, `IRateLimiter`, `ITokenVerifier`, `ISecretProvider` definidas em `Authentication.Application` |
| **Mapeia** | Req 6.2, Req 6.4; design.md § 6.4 (IIdentityProvider contract); DD-001 |
| **Camada principal** | Application |

#### Objetivo

Definir as portas estáveis que desacoplam a aplicação das implementações concretas. `IIdentityProvider` é o contrato mais crítico: toda substituição de IdP deve impactar apenas o adapter em `Infrastructure`. As interfaces não referenciam nenhum tipo do Firebase Admin SDK nem `identity_uid`.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de contrato (interfaces) verificando que nenhuma porta declara tipo específico do Firebase SDK ou símbolo `identity_uid` (pode usar reflexão + assertion).
- [ ] **ST-02 — Green:** declarar as sete interfaces em `Authentication.Application`; `IIdentityProvider` com os métodos `VerifyToken`, `RevokeRefreshTokens`, `GenerateInviteActivation`, `GeneratePasswordResetLink`, `HealthCheck` conforme design.md § 6.4; `IUserDirectory` e `ITenantDirectory` retornando apenas tipos de `Authentication.Domain`/`Authentication.Contracts`.
- [ ] **ST-03 — Refactor:** garantir que `ActivationLink` e `ResetLink` retornados pelas portas não carregam `identity_uid`.
- [ ] **ST-04 — Encerramento:** testes de contrato de porta verdes; Architecture.Tests continuam verdes; `feat(application): define application ports IIdentityProvider IUserDirectory ITenantDirectory`; push.

#### Critérios de Aceite

- [ ] Sete interfaces declaradas sem dependência do Firebase SDK
- [ ] `IIdentityProvider` assina todos os 5 métodos do design.md § 6.4
- [ ] Testes de contrato de porta verdes
- [ ] Architecture.Tests (TASK-02) continuam verdes após adição das interfaces

---

### TASK-06 — SessionTokenValidator + AuthContextComposer

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/authentication/06-token-validator-composer` |
| **Worktree** | `git worktree add .forge/worktrees/authentication/06-token-validator-composer -b feat/authentication/06-token-validator-composer` |
| **Status** | [ ] |
| **Depende de** | TASK-04, TASK-05 |
| **Entregável** | `SessionTokenValidator` + `AuthContextComposer` com mocks; cache hit/miss; 403 para usuário inativo; PBT-02 unitário verde |
| **Mapeia** | Req 4, Req 5, Req 5.4; RNF 2; design.md § 5.3; PBT-01, PBT-02 |
| **Camada principal** | Application |

#### Objetivo

Implementar os dois serviços centrais do caminho quente. `SessionTokenValidator` recebe o JWT bruto e o tenant esperado, invoca `IIdentityProvider.VerifyToken` e aplica `TenantMatchSpec`. `AuthContextComposer` consome `IdentityRef`, resolve `user_id`/memberships via `IUserDirectory` com cache, aplica `ActiveUserSpec` e produz `AuthContext`. Usuário sem `user_id` ativo → HTTP 403 (Req 5.4). PBT-02 unitário verifica que token de tenant A é rejeitado em contexto de tenant B.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes com mocks para: token válido produz `AuthContext` com `tenant_id` correto; token de tenant B rejeitado em contexto A (→ 401); `identity_uid` válido sem `user_id` ativo → 403; cache hit evita chamada ao `IUserDirectory`; escrever PBT-02 unitário com FsCheck gerando pares (tenant A ≠ tenant B) e token de A, verificando 401 em B.
- [ ] **ST-02 — Green:** implementar `SessionTokenValidator` + `AuthContextComposer`; lógica de cache read-through com chave `auth:membership:{tenant_id}:{user_id}`; mapear falhas do `IIdentityProvider` para `IdentityProviderException`.
- [ ] **ST-03 — Refactor:** garantir que `identity_uid` não escapa para o `AuthContext` retornado; extrair lógica de cache em método privado.
- [ ] **ST-04 — Encerramento:** PBT-02 unitário verde (≥ 100 casos); Application.Tests cobrindo cache hit/miss, usuário inativo e token cross-tenant; `feat(application): SessionTokenValidator e AuthContextComposer com PBT-02`; push.

#### Critérios de Aceite

- [ ] PBT-02 unitário verde (token de tenant A rejeitado em B, ≥ 100 casos)
- [ ] Usuário inativo → 403 sem vazar `identity_uid`
- [ ] Cache hit verificado (mock de `IUserDirectory` não chamado)
- [ ] `AuthContext` nunca contém `identity_uid`
- [ ] Application.Tests coverage ≥ 85% acumulado

---

### TASK-07 — SessionRevocationService + PBT-04 (idempotência de logout)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/authentication/07-session-revocation` |
| **Worktree** | `git worktree add .forge/worktrees/authentication/07-session-revocation -b feat/authentication/07-session-revocation` |
| **Status** | [ ] |
| **Depende de** | TASK-05 |
| **Entregável** | `SessionRevocationService` (handler de `LogoutCommand`) idempotente; PBT-04 verde |
| **Mapeia** | Req 9, Req 9.5; design.md § 5.1, § 5.3, § 6.5; PBT-04 |
| **Camada principal** | Application |

#### Objetivo

Implementar o serviço de revogação global. `RevokeRefreshTokens` no `IIdentityProvider` é idempotente por natureza: revogar sessão já revogada deve produzir o mesmo estado final sem erro. `SessionRevocationService` captura qualquer resposta de "já revogado" do IdP como sucesso.

#### Subtasks

- [ ] **ST-01 — Red:** escrever PBT-04 com FsCheck gerando N ≥ 1 chamadas consecutivas de `LogoutCommand` para a mesma sessão; verificar que todas retornam sucesso e que o estado final é idêntico após qualquer N.
- [ ] **ST-02 — Green:** implementar `LogoutCommand` + `SessionRevocationService`; tratar "já revogado" do `IIdentityProvider` como sucesso; emitir evento auditável `session_revoked` apenas na primeira revogação bem-sucedida.
- [ ] **ST-03 — Refactor:** extrair lógica de "já revogado como sucesso" em método auxiliar testável.
- [ ] **ST-04 — Encerramento:** PBT-04 verde (≥ 100 casos, N ≥ 1 por caso); `feat(application): SessionRevocationService idempotente com PBT-04`; push.

#### Critérios de Aceite

- [ ] PBT-04 verde com FsCheck (≥ 100 casos)
- [ ] Nenhuma exceção na segunda chamada de logout para sessão já revogada
- [ ] Evento auditável emitido na primeira revogação (verificado por mock)

---

### TASK-08 — InviteActivationService + PBT-05 unitário

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/authentication/08-invite-activation` |
| **Worktree** | `git worktree add .forge/worktrees/authentication/08-invite-activation -b feat/authentication/08-invite-activation` |
| **Status** | [ ] |
| **Depende de** | TASK-04, TASK-05 |
| **Entregável** | `InviteActivationService` (Create + Activate) com `InviteUsableSpec`; uso único + expiração enforçados; PBT-05 unitário verde |
| **Mapeia** | Req 7, Req 7.4, Req 7.5, Req 7.6; design.md § 5.1, § 5.3; PBT-05; DD-009 |
| **Camada principal** | Application |

#### Objetivo

Implementar os dois casos de uso de convite. `Create` gera token de ativação via `IIdentityProvider.GenerateInviteActivation` (TTL 72h, configurável — DD-009) e dispara e-mail via `IEmailSender`; falha de envio → `AUTH-ERR-032` sem abortar criação. `Activate` aplica `InviteUsableSpec`: link expirado ou consumido → 410 `AUTH-ERR-033`. PBT-05 verifica que nenhuma sequência reabilita link em estado terminal.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes falhando para link expirado não ativa conta; link consumido não ativa conta; e-mail duplicado → 409; falha de e-mail não bloqueia criação além do esperado; escrever PBT-05 com FsCheck gerando links em estado `expired`/`consumed` e verificando que `Activate` sempre falha.
- [ ] **ST-02 — Green:** implementar `CreateInviteCommand` + `ActivateInviteCommand` + `InviteActivationService`; aplicar `InviteUsableSpec`; coordenar com `IUserDirectory` para verificar e-mail duplicado.
- [ ] **ST-03 — Refactor:** garantir que o serviço não persiste estado de convite (stateless — estado vive no IdP/organization).
- [ ] **ST-04 — Encerramento:** PBT-05 unitário verde; `feat(application): InviteActivationService com PBT-05`; push.

#### Critérios de Aceite

- [ ] PBT-05 unitário verde (estados `expired` e `consumed` nunca ativam conta)
- [ ] E-mail já ativo → 409 `AUTH-ERR-030` sem vazar dados do usuário existente
- [ ] Falha de e-mail → 502 `AUTH-ERR-032` sem reverter convite criado no IdP
- [ ] Evento auditável `invite_activated` emitido apenas em ativação bem-sucedida

---

### TASK-09 — PasswordResetService + PBT-03 unitário (anti-enumeração)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 3 — Application |
| **Branch** | `feat/authentication/09-password-reset-service` |
| **Worktree** | `git worktree add .forge/worktrees/authentication/09-password-reset-service -b feat/authentication/09-password-reset-service` |
| **Status** | [ ] |
| **Depende de** | TASK-04, TASK-05 |
| **Entregável** | `PasswordResetService` com resposta uniforme anti-enumeração; `EmailMethodSpec` aplicada; delay constante configurável; PBT-03 unitário verde |
| **Mapeia** | Req 8, Req 8.3, Req 8.4, Req 10, Req 10.2; design.md § 5.1, § 5.3; PBT-03 |
| **Camada principal** | Application |

#### Objetivo

O serviço delega geração do link ao `IIdentityProvider.GeneratePasswordResetLink`. Para e-mail inexistente ou de método Google, nenhuma ação observável é executada, mas o retorno é idêntico (202 `accepted`). Delay constante configurável equaliza o tempo de resposta para impedir oráculo de timing (RISK-AUTH-05, Req 10.2).

#### Subtasks

- [ ] **ST-01 — Red:** escrever PBT-03 unitário com FsCheck gerando e-mails (existente/inexistente) e verificando que o tipo de resposta do serviço é idêntico; escrever teste para `EmailMethodSpec` bloqueando usuário Google.
- [ ] **ST-02 — Green:** implementar `RequestPasswordResetCommand` + `PasswordResetService`; ramificação interna silenciosa; interface pública sempre retorna `accepted`; delay constante via `Task.Delay` configurável.
- [ ] **ST-03 — Refactor:** garantir que a ramificação interna não vaza log diferenciado (mesma categoria de log para ambos os casos).
- [ ] **ST-04 — Encerramento:** PBT-03 unitário verde; Application.Tests ≥ 85% acumulado; `feat(application): PasswordResetService anti-enumeração com PBT-03`; push.

#### Critérios de Aceite

- [ ] PBT-03 unitário verde (tipo de resposta idêntico para e-mail existente e inexistente)
- [ ] `EmailMethodSpec` bloqueia geração de link para usuário Google
- [ ] Serviço não vaza motivo de noop em logs (mesma categoria de log)
- [ ] Delay constante configurável implementado

---

### TASK-10 — FirebaseIdentityProvider (adapter ACL + circuit breaker)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/authentication/10-firebase-adapter` |
| **Worktree** | `git worktree add .forge/worktrees/authentication/10-firebase-adapter -b feat/authentication/10-firebase-adapter` |
| **Status** | [ ] |
| **Depende de** | TASK-05, TASK-13 |
| **Entregável** | `FirebaseIdentityProvider` implementando `IIdentityProvider`; exceções do Firebase SDK mapeadas para `IdentityProviderException`; circuit breaker (Polly); timeout; testado contra emulador Firebase ou WireMock |
| **Mapeia** | Req 6, Req 6.5; RNF 9; design.md § 6.4; DD-001, DD-005; RISK-AUTH-01 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar o único ponto de acoplamento ao GCP Identity Platform. Toda exceção do Firebase Admin SDK é capturada e traduzida para `IdentityProviderException` com código do catálogo antes de sair do adapter (Req 6.5). Circuit breaker evita esgotamento de recursos durante indisponibilidade prolongada (RNF 9.2). Timeout explícito por chamada.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de integração (emulador Firebase ou WireMock) falhando para: token válido retorna `IdentityRef`; exceção do SDK mapeia para `IdentityProviderException`; após N falhas, circuit breaker abre e retorna HTTP 503; recuperação fecha o circuit breaker.
- [ ] **ST-02 — Green:** implementar `FirebaseIdentityProvider` com `FirebaseAuth.TenantManager`; circuit breaker e timeout via Polly; mapear `FirebaseAuthException` para `IdentityProviderException`.
- [ ] **ST-03 — Refactor:** extrair mapeamento de exceção em `FirebaseExceptionMapper`; validar que nenhum tipo Firebase escapa (Architecture.Tests continuam verdes).
- [ ] **ST-04 — Encerramento:** Infrastructure.Tests do adapter verdes; Architecture.Tests verdes; `feat(infrastructure): FirebaseIdentityProvider com circuit breaker e timeout`; push.

#### Critérios de Aceite

- [ ] Todas as exceções do Firebase Admin SDK mapeadas (nenhum tipo Firebase propaga ao chamador)
- [ ] Circuit breaker abre após N falhas e fecha após recuperação
- [ ] HTTP 503 retornado durante circuit breaker aberto
- [ ] Architecture.Tests (TASK-02) continuam verdes
- [ ] Infrastructure.Tests coverage ≥ 70%

---

### TASK-11 — JwksTokenVerifier + cache de chaves públicas

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/authentication/11-jwks-verifier` |
| **Worktree** | `git worktree add .forge/worktrees/authentication/11-jwks-verifier -b feat/authentication/11-jwks-verifier` |
| **Status** | [ ] |
| **Depende de** | TASK-05 |
| **Entregável** | `JwksTokenVerifier` implementando `ITokenVerifier`; validação local de assinatura JWT sem round-trip ao IdP por requisição; cache de JWKS respeitando `Cache-Control`; TLS enforçado |
| **Mapeia** | Req 4.5; RNF 2, RNF 6.2; design.md § 6.2, § 6.4; DD-002 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar a verificação local de assinatura do JWT usando as chaves públicas (JWKS) do Identity Platform. O cache de chaves evita chamada ao IdP a cada requisição (RNF 2). Rotação de chaves gerenciada pelo IdP: o verifier respeita `Cache-Control` do endpoint JWKS e busca novas chaves quando o token apresenta `kid` desconhecido.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes com WireMock para: token com assinatura válida passa; assinatura inválida falha; `kid` desconhecido dispara refresh de JWKS; cache reduz chamadas ao endpoint de JWKS.
- [ ] **ST-02 — Green:** implementar `JwksTokenVerifier` usando `System.IdentityModel.Tokens.Jwt`; cache em memória com TTL do `Cache-Control`; busca de novas chaves em `kid` desconhecido; validação de issuer, audience, lifetime e `ClockSkew` mínimo (DD-002).
- [ ] **ST-03 — Refactor:** extrair parâmetros de validação em `JwtValidationOptions` configurável; garantir que a chave privada nunca existe no `azim-api`.
- [ ] **ST-04 — Encerramento:** testes de JWKS verdes; `feat(infrastructure): JwksTokenVerifier com cache de chaves públicas`; push.

#### Critérios de Aceite

- [ ] Token com assinatura inválida rejeitado (401 `AUTH-ERR-002`)
- [ ] Cache de JWKS reduz chamadas ao IdP (verificado por mock de chamadas HTTP)
- [ ] Rotação de chaves tratada automaticamente por refresh de JWKS
- [ ] Nenhuma chave privada no código ou configuração

---

### TASK-12 — MembershipCacheRepository (Redis + invalidação por evento)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/authentication/12-membership-cache` |
| **Worktree** | `git worktree add .forge/worktrees/authentication/12-membership-cache -b feat/authentication/12-membership-cache` |
| **Status** | [ ] |
| **Depende de** | TASK-03, TASK-05 |
| **Entregável** | `MembershipCacheRepository` (Redis TLS); chave `auth:membership:{tenant_id}:{user_id}`; TTL configurável (~5 min); consumer de `user.role_changed` / `user.deactivated` para invalidação explícita |
| **Mapeia** | Req 5.5; RNF 2.3, RNF 6; design.md § 6.2, § 6.3; DD-007 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar cache de `MembershipSet` com Redis/Memorystore via TLS (RNF 6). A chave inclui `tenant_id` para garantir isolamento entre tenants. Invalidação explícita via consumer dos eventos `user.role_changed` e `user.deactivated` do `organization` (DD-007). Falha de invalidação degrada para o TTL curto sem comprometer isolamento.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de integração (Redis in-memory ou Testcontainers) para: set + get com TTL; chave de tenant A não retornada em contexto de tenant B; `DEL` ao receber `user.role_changed`; consumer idempotente (duplo evento não causa erro).
- [ ] **ST-02 — Green:** implementar `MembershipCacheRepository` com StackExchange.Redis; consumer de eventos do organization para invalidação; serialização JSON do `MembershipSet`.
- [ ] **ST-03 — Refactor:** garantir que conexão Redis usa TLS; extrair chave de cache em método `CacheKey(tenantId, userId)`.
- [ ] **ST-04 — Encerramento:** testes de cache verdes; `feat(infrastructure): MembershipCacheRepository com Redis e invalidação por evento`; push.

#### Critérios de Aceite

- [ ] Chave de cache inclui `tenant_id` (isolamento verificado por teste)
- [ ] TTL configurável aplicado em toda escrita
- [ ] Invalidação por evento testada e idempotente
- [ ] Conexão Redis via TLS (verificado por configuração de teste)

---

### TASK-13 — SecretManagerProvider + gate gitleaks no CI

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/authentication/13-secret-manager` |
| **Worktree** | `git worktree add .forge/worktrees/authentication/13-secret-manager -b feat/authentication/13-secret-manager` |
| **Status** | [ ] |
| **Depende de** | TASK-05 |
| **Entregável** | `SecretManagerProvider` implementando `ISecretProvider`; service account key do Firebase Admin carregada em runtime via GCP Secret Manager; gate `gitleaks` adicionado ao pipeline de CI |
| **Mapeia** | RNF 7; design.md § 6.6, § 6.8; DD-005 |
| **Camada principal** | Infrastructure |

#### Objetivo

Garantir que nenhum segredo do Identity Platform esteja em repositório, imagem Docker ou variável de ambiente em texto claro (RNF 7.1). `SecretManagerProvider` lê a service account key em runtime via GCP Secret Manager. Gate `gitleaks` no CI bloqueia o build ao detectar segredo versionado (RNF 7.3).

#### Subtasks

- [ ] **ST-01 — Red:** escrever teste de contrato verificando que `SecretManagerProvider` implementa `ISecretProvider`; escrever teste que simula falha de acesso ao Secret Manager e verifica que o bootstrap do módulo falha de forma controlada.
- [ ] **ST-02 — Green:** implementar `SecretManagerProvider` usando Google.Cloud.SecretManager.V1; configurar injeção de dependência; adicionar step `gitleaks` no pipeline de CI do módulo.
- [ ] **ST-03 — Refactor:** garantir que a chave nunca é logada; rotação sem redeploy verificada em documentação de operação.
- [ ] **ST-04 — Encerramento:** `gitleaks` verde no CI; `feat(infrastructure): SecretManagerProvider via GCP Secret Manager`; push.

#### Critérios de Aceite

- [ ] Nenhum segredo em código, configuração ou variável de ambiente em texto claro
- [ ] `gitleaks` no CI configurado e verde (gate obrigatório)
- [ ] Falha de acesso ao Secret Manager tratada de forma controlada (não expõe detalhe interno)
- [ ] Rotação de chave sem redeploy documentada

---

### TASK-14 — OrganizationUserDirectory + TenantDirectory

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 4 — Infrastructure |
| **Branch** | `feat/authentication/14-directory-adapters` |
| **Worktree** | `git worktree add .forge/worktrees/authentication/14-directory-adapters -b feat/authentication/14-directory-adapters` |
| **Status** | [ ] |
| **Depende de** | TASK-05 |
| **Entregável** | `OrganizationUserDirectory` (implementa `IUserDirectory`, consulta `users` e `user_memberships`) e `TenantDirectory` (implementa `ITenantDirectory`, consulta `tenants`) em `Authentication.Infrastructure` |
| **Mapeia** | Req 1.3, Req 5.1; design.md § 6.1, § 7; DD-001 |
| **Camada principal** | Infrastructure |

#### Objetivo

Implementar os adapters de leitura que consultam tabelas de outros módulos. `TenantDirectory` resolve `slug → tenant_id` e o `identity_tenant_id` correspondente (leitura cross-tenant, em conexão de catálogo). `OrganizationUserDirectory` resolve `identity_uid → user_id`, papéis e memberships (leitura escopada ao `tenant_id` corrente via `SET app.current_tenant`). Nenhuma das consultas escreve dados.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de integração (Testcontainers com PostgreSQL) falhando para: slug ativo retorna `tenant_id` correto; slug inexistente retorna nulo/NotFound; `identity_uid` válido retorna `user_id` e memberships; `users` não tem coluna de senha (assertion por schema).
- [ ] **ST-02 — Green:** implementar `TenantDirectory` com consulta SQL/EF à tabela `tenants` (sem `SET app.current_tenant`); `OrganizationUserDirectory` com consulta escopada ao tenant corrente.
- [ ] **ST-03 — Refactor:** garantir que `OrganizationUserDirectory` nunca retorna `identity_uid` ao chamador (apenas `user_id`).
- [ ] **ST-04 — Encerramento:** testes de diretório verdes; assertion de schema sem coluna de senha verde; `feat(infrastructure): OrganizationUserDirectory e TenantDirectory`; push.

#### Critérios de Aceite

- [ ] Consulta de slug retorna `tenant_id` e `identity_tenant_id` corretos
- [ ] Slug inexistente/inativo retorna resultado que leva a 404 `AUTH-ERR-010`
- [ ] `OrganizationUserDirectory` não expõe `identity_uid` ao chamador
- [ ] Assertion de schema: nenhuma coluna de senha em `users` (RNF 1.1)

---

### TASK-15 — CorrelationMiddleware + TenantResolutionMiddleware

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `feat/authentication/15-tenant-resolution-middleware` |
| **Worktree** | `git worktree add .forge/worktrees/authentication/15-tenant-resolution-middleware -b feat/authentication/15-tenant-resolution-middleware` |
| **Status** | [ ] |
| **Depende de** | TASK-14 |
| **Entregável** | `CorrelationMiddleware` (garante `correlationId` por requisição) e `TenantResolutionMiddleware` (resolve slug → `tenant_id`, executa `SET app.current_tenant`, retorna 404 `AUTH-ERR-010` para slug inexistente); testados como middlewares |
| **Mapeia** | Req 1, Req 1.2, Req 1.5; RNF 4.1; design.md § 5.4; DEC-006 |
| **Camada principal** | Api |

#### Objetivo

Implementar os dois primeiros middlewares do pipeline (ordem obrigatória: Correlation → TenantResolution → Authentication). `CorrelationMiddleware` garante que 100% dos logs contenham `correlationId`. `TenantResolutionMiddleware` resolve o slug do path ou do header `X-Tenant-Slug` via `ITenantDirectory`, executa `SET app.current_tenant = '<tenant_id>'` na conexão e injeta o `tenant_id` no contexto da requisição. Slug inexistente/inativo → 404.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de integração para: slug válido resolve `tenant_id` e prossegue para o próximo middleware; slug inexistente retorna 404 `AUTH-ERR-010` sem detalhe interno; `correlationId` presente no header de resposta e no contexto de log.
- [ ] **ST-02 — Green:** implementar `CorrelationMiddleware` (lê ou gera `X-Correlation-ID`; adiciona ao `ILogger` scope) e `TenantResolutionMiddleware` (lê slug do path/header; chama `ITenantDirectory`; executa `SET`; continua ou retorna 404).
- [ ] **ST-03 — Refactor:** garantir que `TenantResolutionMiddleware` usa a conexão de catálogo (sem `app.current_tenant` ainda definido na consulta de slug).
- [ ] **ST-04 — Encerramento:** testes de middleware verdes; `feat(api): CorrelationMiddleware e TenantResolutionMiddleware`; push.

#### Critérios de Aceite

- [ ] `correlationId` presente em 100% dos logs após o middleware (verificado em teste)
- [ ] Slug inexistente retorna 404 `AUTH-ERR-010` sem expor dados internos
- [ ] `SET app.current_tenant` executado antes do middleware de autenticação
- [ ] Slug presente no path **ou** no header `X-Tenant-Slug` aceito

---

### TASK-16 — AuthenticationMiddleware + RateLimitingMiddleware

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `feat/authentication/16-auth-rate-limit-middleware` |
| **Worktree** | `git worktree add .forge/worktrees/authentication/16-auth-rate-limit-middleware -b feat/authentication/16-auth-rate-limit-middleware` |
| **Status** | [ ] |
| **Depende de** | TASK-06, TASK-15 |
| **Entregável** | `AuthenticationMiddleware` (invoca `SessionTokenValidator` + `AuthContextComposer`, injeta `AuthContext`; falha → 401/403) e `RateLimitingMiddleware` (por IP e tenant; excesso → 429 `AUTH-ERR-040` sem revelar existência de conta) |
| **Mapeia** | Req 4, Req 10.5; RNF 8; design.md § 5.4; PBT-02 |
| **Camada principal** | Api |

#### Objetivo

Implementar os middlewares de autenticação e rate limiting. `AuthenticationMiddleware` é o ponto de entrada do pipeline quente: valida o token, compõe o `AuthContext` e o injeta na requisição; token inválido/expirado/revogado → 401; usuário inativo → 403. `RateLimitingMiddleware` aplica limitação por IP e por tenant usando `IRateLimiter`; excesso → 429 com mensagem genérica (RNF 8.2).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de integração para: token válido → `AuthContext` injetado e requisição prossegue; token ausente → 401 `AUTH-ERR-001`; token expirado → 401 `AUTH-ERR-003`; token de tenant B em contexto A → 401 `AUTH-ERR-004`; excesso de tentativas → 429 `AUTH-ERR-040`; mensagem do 429 não revela existência de conta.
- [ ] **ST-02 — Green:** implementar `AuthenticationMiddleware` integrando `SessionTokenValidator` e `AuthContextComposer`; implementar `RateLimitingMiddleware` usando `IRateLimiter` com janelas por IP e tenant.
- [ ] **ST-03 — Refactor:** garantir que a ordem dos middlewares está correta no `Program.cs`: Correlation → TenantResolution → RateLimiting → Authentication → business.
- [ ] **ST-04 — Encerramento:** testes de middleware verdes; `feat(api): AuthenticationMiddleware e RateLimitingMiddleware`; push.

#### Critérios de Aceite

- [ ] Token ausente → 401 `AUTH-ERR-001` sem detalhe interno
- [ ] Token cross-tenant → 401 `AUTH-ERR-004`
- [ ] Excesso de tentativas → 429 `AUTH-ERR-040` sem revelar existência de conta
- [ ] `AuthContext` injetado na requisição para endpoints protegidos
- [ ] Ordem dos middlewares validada por teste de integração

---

### TASK-17 — Contracts, DTOs e catálogo de erros (AUTH-ERR-001..090)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `feat/authentication/17-contracts-error-catalog` |
| **Worktree** | `git worktree add .forge/worktrees/authentication/17-contracts-error-catalog -b feat/authentication/17-contracts-error-catalog` |
| **Status** | [ ] |
| **Depende de** | TASK-05 |
| **Entregável** | Todos os DTOs de request/response em `Authentication.Contracts`; catálogo de erros completo (`AUTH-ERR-001..090`) com formato `{ code, message }` sem PII; OpenAPI configurado |
| **Mapeia** | Req 10.4; design.md § 8, § 12 (catálogo); Req 11, Req 7, Req 8, Req 9 |
| **Camada principal** | Contracts |

#### Objetivo

Definir todos os contratos públicos do módulo em `Authentication.Contracts`. DTOs de API (MeResponse, LogoutResponse, InviteRequest, ActivateInviteRequest, PasswordResetRequest e respostas correspondentes). Catálogo de erros completo com os 16 códigos definidos no design.md § 12, garantindo que nenhuma mensagem expõe PII, `identity_uid` ou detalhe de implementação.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de serialização para DTOs (sem campos de `identity_uid`; sem nulos inesperados); escrever teste de catálogo verificando que todos os 16 códigos `AUTH-ERR-*` existem e têm mensagens sem PII.
- [ ] **ST-02 — Green:** implementar DTOs em `Authentication.Contracts`; implementar `ErrorCatalog` com os 16 códigos; configurar `ErrorResponse` com campos `{ code, message }`; adicionar OpenAPI/Swagger com anotações de resposta.
- [ ] **ST-03 — Refactor:** garantir que nenhum DTO expõe `identity_uid` ou campo interno do Firebase; remover campos opcionais desnecessários.
- [ ] **ST-04 — Encerramento:** testes de contrato verdes; `feat(contracts): DTOs e catálogo de erros AUTH-ERR-001..090`; push.

#### Critérios de Aceite

- [ ] Todos os 16 códigos `AUTH-ERR-*` implementados e testados
- [ ] Nenhum DTO expõe `identity_uid` ou tipo interno do Firebase
- [ ] Formato de erro `{ code, message }` consistente em todos os endpoints
- [ ] OpenAPI configurado com descrição dos status codes de cada endpoint

---

### TASK-18 — Endpoints GET /v1/auth/me e POST /v1/auth/logout

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `feat/authentication/18-auth-controller` |
| **Worktree** | `git worktree add .forge/worktrees/authentication/18-auth-controller -b feat/authentication/18-auth-controller` |
| **Status** | [ ] |
| **Depende de** | TASK-07, TASK-16, TASK-17 |
| **Entregável** | `AuthController` com `GET /v1/auth/me` (projeção do `AuthContext`) e `POST /v1/auth/logout` (revogação idempotente); PBT-04 API verde |
| **Mapeia** | Req 9, Req 11; design.md § 8.1, § 8.2; PBT-04 |
| **Camada principal** | Api |

#### Objetivo

Implementar os dois endpoints gerenciados pelo `AuthController`. `/me` projeta o `AuthContext` injetado pelo middleware, nunca expondo `identity_uid`. `/logout` invoca `SessionRevocationService` e é idempotente: repetição retorna o mesmo resultado. PBT-04 API verifica N chamadas consecutivas de logout.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de integração para: GET /me retorna `user_id`, `email`, `roles`, `memberships`, `tenant_id`; GET /me sem token → 401; GET /me nunca retorna `identity_uid`; POST /logout com token válido → 200 `{ status: "revoked" }`; segunda chamada de POST /logout → 200 mesmo estado (PBT-04 API, N ≥ 3).
- [ ] **ST-02 — Green:** implementar `AuthController`; mapear `AuthContext` para `MeResponse` (excluindo `identity_uid`); invocar `SessionRevocationService` no logout.
- [ ] **ST-03 — Refactor:** garantir que `MeResponse` retorna apenas dados do usuário corrente e do tenant da sessão (Req 11.2).
- [ ] **ST-04 — Encerramento:** testes do controller verdes; PBT-04 API verde; `feat(api): AuthController com /me e /logout`; push.

#### Critérios de Aceite

- [ ] `GET /me` retorna `{ user_id, email, roles, memberships, tenant_id }` sem `identity_uid`
- [ ] `GET /me` sem token → 401 `AUTH-ERR-001`
- [ ] `POST /logout` idempotente (N ≥ 3 chamadas, mesmo resultado — PBT-04 API)
- [ ] Api.Tests coverage ≥ 80% acumulado

---

### TASK-19 — Endpoints POST /v1/auth/invites e /invites/activate

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `feat/authentication/19-invite-controller` |
| **Worktree** | `git worktree add .forge/worktrees/authentication/19-invite-controller -b feat/authentication/19-invite-controller` |
| **Status** | [ ] |
| **Depende de** | TASK-08, TASK-16, TASK-17 |
| **Entregável** | `InviteController` com `POST /v1/auth/invites` (Tenant Admin) e `POST /v1/auth/invites/activate` (pública); PBT-05 API verde |
| **Mapeia** | Req 7; design.md § 8.3, § 8.4; PBT-05 |
| **Camada principal** | Api |

#### Objetivo

Implementar o fluxo de convite via API. `POST /invites` exige papel Tenant Admin (authorization behavior); retorna 202 `{ status: "invited" }` em sucesso ou erros do catálogo (409, 422, 502, 429). `POST /invites/activate` é pública (sem Bearer) e aplica rate limiting; link expirado/consumido → 410 `AUTH-ERR-033`. PBT-05 API verifica que link em estado terminal sempre retorna 410.

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de integração para: convite criado retorna 202; não-admin recebe 403; e-mail já cadastrado → 409 `AUTH-ERR-030`; ativação de link válido → 200 `{ status: "activated" }`; ativação de link expirado → 410 `AUTH-ERR-033` (PBT-05 API).
- [ ] **ST-02 — Green:** implementar `InviteController`; adicionar `AuthorizationBehavior` verificando papel Tenant Admin para `CreateInviteCommand`; aplicar rate limiting no `activate`.
- [ ] **ST-03 — Refactor:** garantir que `AUTH-ERR-033` não revela detalhes do link nem do usuário.
- [ ] **ST-04 — Encerramento:** testes do controller verdes; PBT-05 API verde; `feat(api): InviteController com /invites e /invites/activate`; push.

#### Critérios de Aceite

- [ ] `POST /invites` restrito a Tenant Admin (403 para outros papéis)
- [ ] `POST /invites/activate` pública com rate limiting
- [ ] Link expirado/consumido → 410 `AUTH-ERR-033` sem expor dados (PBT-05 API)
- [ ] Mensagem de erro 409 não expõe dados do usuário existente além do necessário para orientar o admin

---

### TASK-20 — Endpoint POST /v1/auth/password-reset + PBT-03 API

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 5 — API + Contracts |
| **Branch** | `feat/authentication/20-password-reset-controller` |
| **Worktree** | `git worktree add .forge/worktrees/authentication/20-password-reset-controller -b feat/authentication/20-password-reset-controller` |
| **Status** | [ ] |
| **Depende de** | TASK-09, TASK-16, TASK-17 |
| **Entregável** | `PasswordResetController` com `POST /v1/auth/password-reset`; resposta 202 uniforme para e-mail existente e inexistente; PBT-03 API verde (corpo, código e timing indistinguíveis) |
| **Mapeia** | Req 8, Req 10.2; design.md § 8.5; PBT-03; RISK-AUTH-05 |
| **Camada principal** | Api |

#### Objetivo

Implementar o endpoint público de recuperação de senha. O corpo, o código HTTP e a categoria de mensagem devem ser idênticos para e-mail existente e inexistente. O timing deve estar dentro de uma banda configurável (sem diferença estatisticamente significativa). PBT-03 API é o teste mais rigoroso do módulo: gera pares (existente, inexistente) e verifica indistinguibilidade.

#### Subtasks

- [ ] **ST-01 — Red:** escrever PBT-03 API com FsCheck: para qualquer e-mail gerado, verificar que corpo de resposta, código HTTP e categoria de mensagem são idênticos; medir timing de N pares e verificar que está dentro da banda configurada (ex.: ±200 ms).
- [ ] **ST-02 — Green:** implementar `PasswordResetController` invocando `PasswordResetService`; aplicar delay constante configurável antes de retornar; retornar sempre 202 `{ status: "accepted" }`.
- [ ] **ST-03 — Refactor:** ajustar banda de timing se necessário; garantir que rate limiting retorna 429 `AUTH-ERR-040` com mensagem genérica (não revela existência de conta, RNF 8.2).
- [ ] **ST-04 — Encerramento:** PBT-03 API verde (corpo, código e timing); Api.Tests ≥ 80% acumulado; `feat(api): PasswordResetController com PBT-03 anti-enumeração`; push.

#### Critérios de Aceite

- [ ] PBT-03 API verde (corpo, código e timing indistinguíveis para e-mail existente e inexistente)
- [ ] Resposta sempre 202 `{ status: "accepted" }` para qualquer e-mail
- [ ] Rate limit retorna 429 sem revelar existência de conta
- [ ] RISK-AUTH-05 mitigado (oráculo de timing eliminado)

---

### TASK-21 — Health checks e readiness probes (IdP + Redis)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `feat/authentication/21-health-checks` |
| **Worktree** | `git worktree add .forge/worktrees/authentication/21-health-checks -b feat/authentication/21-health-checks` |
| **Status** | [ ] |
| **Depende de** | TASK-10, TASK-12 |
| **Entregável** | `IdentityProviderHealthCheck` e `RedisHealthCheck` registrados; `GET /health/ready` verifica IdP e Redis; `GET /health/live` independe do IdP |
| **Mapeia** | RNF 3.2; design.md § 11; RISK-AUTH-01 |
| **Camada principal** | Infrastructure / Api |

#### Objetivo

Implementar os health checks que permitem ao orquestrador (Cloud Run) detectar falha de dependência externa. `GET /health/ready` falha quando IdP ou Redis estão indisponíveis, impedindo que o pod receba tráfego. `GET /health/live` é independente do IdP para não derrubar o pod em falha transitória do Identity Platform (RNF 3.2, design.md § 11).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de integração falhando para: `/health/ready` retorna unhealthy quando IdP falha; `/health/live` retorna healthy mesmo quando IdP falha; `/health/ready` retorna unhealthy quando Redis falha.
- [ ] **ST-02 — Green:** implementar `IdentityProviderHealthCheck` usando `IIdentityProvider.HealthCheck()`; `RedisHealthCheck` via ping no Redis; registrar ambos em `/health/ready`; registrar apenas check interno em `/health/live`.
- [ ] **ST-03 — Refactor:** garantir que o health check não expõe detalhe de exceção interna na resposta HTTP.
- [ ] **ST-04 — Encerramento:** testes de health check verdes; `feat(infrastructure): health checks para IdP e Redis`; push.

#### Critérios de Aceite

- [ ] `/health/ready` unhealthy quando IdP ou Redis indisponíveis
- [ ] `/health/live` healthy mesmo com IdP indisponível
- [ ] Resposta de health check não expõe stack trace ou detalhe interno

---

### TASK-22 — Logs estruturados + mascaramento de PII (Serilog)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `feat/authentication/22-structured-logging` |
| **Worktree** | `git worktree add .forge/worktrees/authentication/22-structured-logging -b feat/authentication/22-structured-logging` |
| **Status** | [ ] |
| **Depende de** | TASK-15 |
| **Entregável** | Serilog configurado com destructuring policy mascarando PII (e-mail, nome); `correlationId` e `tenantId` em 100% dos logs de contexto autenticado; nenhum log emite `identity_uid` ou token completo |
| **Mapeia** | RNF 4; design.md § 11; DD-006; NFR-PRIV-01 |
| **Camada principal** | Infrastructure / Api |

#### Objetivo

Configurar Serilog com Cloud Logging sink (JSON estruturado). Destructuring policy mascara automaticamente campos PII (email, nome) por construção. `identity_uid` nunca sai do adapter (Architecture.Tests garantem isso). Em contexto autenticado, 100% dos logs carregam `tenantId` e `correlationId` (RNF 4.4, 4.1).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de scan de saída de log verificando: nenhum log emite e-mail em texto claro; nenhum log emite `identity_uid`; em contexto autenticado, `tenantId` e `correlationId` presentes.
- [ ] **ST-02 — Green:** configurar Serilog com destructuring policy `MaskEmailPolicy` e `ExcludeIdentityUidPolicy`; adicionar enricher de `tenantId` e `correlationId` ao LogContext nos middlewares.
- [ ] **ST-03 — Refactor:** garantir que a política de mascaramento é aplicada globalmente (não apenas por ponto de log individual).
- [ ] **ST-04 — Encerramento:** testes de scan de saída de log verdes; `feat(infrastructure): Serilog estruturado com mascaramento de PII`; push.

#### Critérios de Aceite

- [ ] Nenhum log emite e-mail em texto claro (verificado por scan de saída)
- [ ] Nenhum log emite `identity_uid` (verificado por Architecture.Tests + scan)
- [ ] `tenantId` e `correlationId` presentes em 100% dos logs de contexto autenticado
- [ ] Logs em formato JSON com todos os campos obrigatórios do RNF 4.1

---

### TASK-23 — Métricas e alertas de autenticação

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `feat/authentication/23-metrics-alerts` |
| **Worktree** | `git worktree add .forge/worktrees/authentication/23-metrics-alerts -b feat/authentication/23-metrics-alerts` |
| **Status** | [ ] |
| **Depende de** | TASK-16 |
| **Entregável** | Métricas `auth_token_validation_success_total`, `auth_token_validation_failure_total` (rotuladas por `tenant_id` e causa), `auth_rate_limit_block_total` e `auth_latency_ms`; alertas configurados no Cloud Monitoring |
| **Mapeia** | RNF 5, RNF 2.1, RNF 8.3; design.md § 11 |
| **Camada principal** | Infrastructure / Api |

#### Objetivo

Emitir as métricas definidas no design.md § 11 e configurar os alertas operacionais. Falha de validação acima do baseline dispara alerta de possível ataque (RNF 5.2). Taxa de HTTP 401 em massa dispara alerta de investigação do IdP (RNF 3.3). Rate limit em massa dispara alerta de possível ataque (RNF 8.3).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de unidade verificando que `auth_token_validation_failure_total` é incrementado para cada rejeição de token; `auth_token_validation_success_total` incrementado para cada validação bem-sucedida; rótulo `causa` distingue `expired`, `invalid_signature`, `tenant_mismatch`.
- [ ] **ST-02 — Green:** implementar emissão de métricas via OpenTelemetry/Prometheus no `AuthenticationMiddleware` e no `RateLimitingMiddleware`; configurar alertas no Cloud Monitoring (YAML ou Terraform, conforme padrão do projeto).
- [ ] **ST-03 — Refactor:** garantir que métricas não emitem `tenant_id` como label se isso gerar alta cardinalidade (consultar padrão do projeto); usar hash ou bucket se necessário.
- [ ] **ST-04 — Encerramento:** métricas emitidas e verificadas em teste; alertas configurados documentados; `feat(infrastructure): métricas e alertas de autenticação`; push.

#### Critérios de Aceite

- [ ] Quatro métricas emitidas (success, failure, rate_limit, latency)
- [ ] Rótulo `causa` distingue os três tipos de falha de validação (RNF 5.3)
- [ ] Alertas de falha em massa e de ataque documentados e configurados
- [ ] Latência p95 ≤ 1 s verificável via métrica (baseline para o teste de performance — TASK-25)

---

### TASK-24 — Auditoria de eventos de autenticação (append-only)

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `feat/authentication/24-audit-events` |
| **Worktree** | `git worktree add .forge/worktrees/authentication/24-audit-events -b feat/authentication/24-audit-events` |
| **Status** | [ ] |
| **Depende de** | TASK-07, TASK-08, TASK-09 |
| **Entregável** | `AuditBehavior` emitindo eventos `user_authenticated`, `session_revoked`, `invite_activated`, `password_reset_requested`, `password_reset_completed` para o módulo `audit-log`; nunca contendo `identity_uid`, senha ou token |
| **Mapeia** | RNF 10; design.md § 4.4, § 9.1, § 11; `.forge/rules/domain/audit-immutability.md` |
| **Camada principal** | Application |

#### Objetivo

Implementar o pipeline behavior `AuditBehavior` que intercepta os comandos de coordenação e emite eventos auditáveis append-only para o módulo `audit-log`. Cada evento carrega `user_id`, `tenant_id`, `event_type`, `occurred_at` e `correlation_id`; nunca `identity_uid`, senha ou token completo (RNF 10.2). Logout/revogação e ativação de convite são tipos distinguíveis (RNF 10.3).

#### Subtasks

- [ ] **ST-01 — Red:** escrever testes de unidade verificando: `LogoutCommand` emite `session_revoked` com `user_id` e `tenant_id`; `ActivateInviteCommand` emite `invite_activated`; nenhum evento contém `identity_uid` ou token; eventos são distintos por `event_type`.
- [ ] **ST-02 — Green:** implementar `AuditBehavior` (pipeline MediatR ou equivalente); integrar com `IAuditWriter` do módulo `audit-log`; emitir os cinco tipos de evento.
- [ ] **ST-03 — Refactor:** garantir que falha de emissão de evento de auditoria não bloqueia o fluxo principal (fail-open para auditoria, fail-close para autenticação).
- [ ] **ST-04 — Encerramento:** testes de auditoria verdes; `feat(application): AuditBehavior com 5 eventos auditáveis`; push.

#### Critérios de Aceite

- [ ] Cinco tipos de evento implementados e distintos por `event_type`
- [ ] Nenhum evento contém `identity_uid`, senha ou token (verificado por teste)
- [ ] Falha de auditoria não bloqueia fluxo principal (verificado por teste de falha de `IAuditWriter`)
- [ ] `correlation_id` presente em todos os eventos

---

### TASK-25 — Gates de segurança, isolamento cross-tenant (CI) e teste de performance

| Campo | Valor |
|-------|-------|
| **Onda** | Onda 6 — Hardening |
| **Branch** | `feat/authentication/25-security-perf-gates` |
| **Worktree** | `git worktree add .forge/worktrees/authentication/25-security-perf-gates -b feat/authentication/25-security-perf-gates` |
| **Status** | [ ] |
| **Depende de** | TASK-10, TASK-13, TASK-22, TASK-23 |
| **Entregável** | Gate `gitleaks` verde no CI; teste de isolamento cross-tenant (PBT-02 de integração) como gate de CI; teste de carga confirmando p95 ≤ 1 s; DoD do design.md § 19 verificado e documentado |
| **Mapeia** | RNF 1.4, RNF 2.1, RNF 7.3; PBT-02; design.md § 13, § 19; RISK-AUTH-06 |
| **Camada principal** | Tests / DevOps |

#### Objetivo

Fechar o DoD do módulo. PBT-02 de integração testa que token do tenant A não autentica em contexto do tenant B com banco real (SQL/RLS). Teste de carga (k6 ou NBomber) verifica p95 ≤ 1 s excluindo latência do IdP (RNF 2.1). Registrar explicitamente status de VAL-AUTH-02 (DD-008) e VAL-09 (DD-009) como itens abertos a confirmar antes do go-live.

#### Subtasks

- [ ] **ST-01 — Red:** escrever PBT-02 de integração (Testcontainers + emulador Firebase) com dois tenants reais; verificar que token de tenant A retorna 401 em contexto de tenant B e que nenhum `AuthContext` é produzido; escrever script de carga inicial que falha no p95.
- [ ] **ST-02 — Green:** ajustar configurações de cache e timeout para atingir p95 ≤ 1 s; confirmar PBT-02 de integração verde; adicionar gates ao pipeline de CI (gitleaks + PBT-02 integração + cobertura).
- [ ] **ST-03 — Refactor:** documentar VAL-AUTH-02 e VAL-09 como pendências de confirmação com produto/segurança antes do go-live (RISK-AUTH-06); sincronizar checklist do DoD (design.md § 19).
- [ ] **ST-04 — Encerramento:** PBT-02 integração verde no CI; p95 ≤ 1 s documentado; gitleaks verde; DoD § 19 completo; `chore(authentication): DoD final e gates de segurança`; push.

#### Critérios de Aceite

- [ ] PBT-02 de integração verde no CI (token de tenant A não autentica em B)
- [ ] p95 ≤ 1 s confirmado em teste de carga (excluindo latência do IdP)
- [ ] `gitleaks` verde (nenhum segredo versionado)
- [ ] VAL-AUTH-02 e VAL-09 registrados como pendências de confirmação antes do go-live
- [ ] Todos os itens do DoD do design.md § 19 marcados ou justificados

---

## 5. Matriz de Rastreabilidade

| Origem | Descrição | TASKs | Status |
|--------|-----------|-------|--------|
| Req 1 | Resolução de tenant pelo slug | TASK-14, TASK-15 | [ ] |
| Req 2 | Login por e-mail e senha (validação no backend) | TASK-06, TASK-16 | [ ] |
| Req 3 | Login por Google OIDC | TASK-06, TASK-16 | [ ] |
| Req 4 | Validação de session token em toda requisição | TASK-06, TASK-11, TASK-16 | [ ] |
| Req 5 | Composição do AuthContext via tradução de `identity_uid` | TASK-03, TASK-06, TASK-12 | [ ] |
| Req 6 | Isolamento e reversibilidade do ACL (IIdentityProvider) | TASK-02, TASK-05, TASK-10 | [ ] |
| Req 7 | Convite por e-mail com ativação | TASK-08, TASK-19 | [ ] |
| Req 8 | Recuperação de senha por e-mail | TASK-09, TASK-20 | [ ] |
| Req 9 | Encerramento, expiração e revogação de sessão | TASK-07, TASK-18 | [ ] |
| Req 10 | Anti-enumeração de e-mail nos fluxos públicos | TASK-09, TASK-16, TASK-20 | [ ] |
| Req 11 | Perfil do usuário autenticado (GET /me) | TASK-18 | [ ] |
| RNF 1 | Zero senhas no Azim | TASK-10, TASK-14, TASK-22 | [ ] |
| RNF 2 | Latência de autenticação (p95 ≤ 1 s) | TASK-11, TASK-12, TASK-23, TASK-25 | [ ] |
| RNF 3 | Disponibilidade Tier 1 | TASK-21 | [ ] |
| RNF 4 | Observabilidade sem PII | TASK-15, TASK-22 | [ ] |
| RNF 5 | Métricas e alerta de falha de validação | TASK-23 | [ ] |
| RNF 6 | TLS em todo tráfego com IdP e Redis | TASK-10, TASK-12 | [ ] |
| RNF 7 | Segredos via Secret Manager | TASK-13, TASK-25 | [ ] |
| RNF 8 | Rate limiting e proteção contra abuso | TASK-16, TASK-19, TASK-20 | [ ] |
| RNF 9 | Resiliência à indisponibilidade do IdP | TASK-10, TASK-21 | [ ] |
| RNF 10 | Auditoria de eventos de autenticação | TASK-24 | [ ] |
| PBT-01 | Sessão sempre escopada a um único tenant | TASK-04, TASK-06 | [ ] |
| PBT-02 | Isolamento cross-tenant do token | TASK-06, TASK-25 | [ ] |
| PBT-03 | Resposta indistinguível (anti-enumeração) | TASK-09, TASK-20 | [ ] |
| PBT-04 | Idempotência do logout | TASK-07, TASK-18 | [ ] |
| PBT-05 | Link expirado/consumido nunca concede acesso | TASK-04, TASK-08, TASK-19 | [ ] |
| DD-001 | ACL stateless sem agregado de domínio | TASK-02, TASK-03, TASK-05 | [ ] |
| DD-002 | Conformidade com jwt-authentication.md por delegação | TASK-11 | [ ] |
| DD-003 | Caminho quente por middleware; coordenação por serviços | TASK-06, TASK-15, TASK-16 | [ ] |
| DD-004 | Login client-side, sem endpoint de login no backend | TASK-17 | [ ] |
| DD-005 | Service account via Secret Manager | TASK-13 | [ ] |
| DD-006 | PII e identity_uid fora de toda telemetria | TASK-02, TASK-22 | [ ] |
| DD-007 | Invalidação de cache por evento + TTL curto | TASK-12 | [ ] |
| DD-008 | Duração e renovação de sessão (VAL-AUTH-02) | TASK-25 | [ ] |
| DD-009 | Prazo de expiração do link de convite (VAL-09) | TASK-08, TASK-25 | [ ] |

---

## 6. Coverage Gates

| Camada | Gate | Tipo de teste esperado |
|--------|------|------------------------|
| Domain (`Authentication.Domain`) | ≥ 95% | Unitários de VOs, transições de `SessionState`, Specifications + PBTs de invariantes (PBT-01, PBT-05) |
| Application (`Authentication.Application`) | ≥ 85% | Unitários de `SessionTokenValidator`, `AuthContextComposer`, `SessionRevocationService`, `InviteActivationService`, `PasswordResetService`; PBTs unitários (PBT-02, PBT-03, PBT-04, PBT-05) |
| Infrastructure (`Authentication.Infrastructure`) | ≥ 70% | Integração com emulador Firebase/WireMock; Testcontainers Redis; SecretManager mock; JWKS mock |
| Api (`Authentication.Api`) | ≥ 80% | Integração de middlewares; endpoints; PBTs de API (PBT-03, PBT-04); testes de anti-enumeração de corpo e timing |
| Architecture (`Authentication.Architecture.Tests`) | 100% das regras críticas | Regra de dependência; confinamento do Firebase SDK e `identity_uid` a `Infrastructure` |
| Security | Cobertura por cenário crítico | 401 para token inválido/expirado/revogado; 403 para usuário inativo; anti-enumeração; sem PII em logs; sem segredo versionado (gitleaks) |
| Observability | Cobertura por fluxo crítico | Métricas emitidas; `correlationId` e `tenantId` em logs; health checks respondendo corretamente |

Regras:
- Coverage gate não substitui qualidade: PBTs são obrigatórios para as 5 propriedades mapeadas.
- Architecture.Tests são gate de CI obrigatório antes de qualquer merge (TASK-02).
- Teste de isolamento cross-tenant (PBT-02 integração) é gate de CI da Onda 6 (TASK-25).

---

## 7. Critérios de Encerramento

### Encerramento de TASK

Uma TASK é `[X]` quando:

- todas as subtasks concluídas
- testes da camada verdes
- coverage gate da camada atendido ou justificativa registrada
- lint/format executado
- nenhum warning novo relevante
- commit em Conventional Commits realizado
- push realizado
- documentação atualizada quando aplicável

### Encerramento de Onda

Uma onda é concluída quando:

- todas as TASKs da onda `[X]`
- CI verde (Architecture.Tests inclusos)
- PR da onda aberto, aprovado ou mergeado
- riscos da onda tratados ou registrados

### Encerramento do Módulo

O módulo authentication está pronto quando:

- todos os itens do DoD do design.md § 19 marcados ou justificados
- PBT-01..05 todos verdes (unitários e de integração conforme tabela da seção 1.2)
- Architecture.Tests verde em CI (ACL enforçado)
- Token cross-tenant rejeitado em teste de integração (PBT-02 CI)
- Anti-enumeração verificada por PBT-03 API (corpo, código e timing)
- Logout idempotente verificado por PBT-04 API
- `gitleaks` verde (RNF 7.3)
- p95 ≤ 1 s em teste de carga (RNF 2.1)
- Logs sem PII e sem `identity_uid` (RNF 4)
- Métricas e alertas ativos (RNF 5)
- Health checks respondendo corretamente (RNF 3.2)
- Auditoria de 5 tipos de evento implementada (RNF 10)
- VAL-AUTH-02 e VAL-09 confirmados com produto/segurança ou registrados como bloqueio explícito (RISK-AUTH-06)
- `requirements.md`, `design.md` e `tasks.md` consistentes
- README do módulo sincronizado

---

## 8. Riscos de Execução

| Risco | Impacto | Onda | Mitigação |
|-------|---------|------|-----------|
| RISK-AUTH-01 — Indisponibilidade do Identity Platform nos testes | Testes de integração da Onda 4 flaky | Onda 4 | Usar emulador Firebase + WireMock para JWKS; isolar testes de integração reais |
| RISK-AUTH-02 — Cache de membership desatualizado | Operação com papel antigo | Onda 4 | TTL curto + invalidação por evento (DD-007); testado em TASK-12 |
| RISK-AUTH-03 — Vazamento de `identity_uid` no modelo | Acoplamento ao IdP; viola ACL | Todas | Architecture.Tests como gate de CI desde a Onda 1 (TASK-02) |
| RISK-AUTH-04 — Conflito com `jwt-authentication.md` | Leitura de não conformidade | Onda 1 | DD-002 documenta conformidade por delegação; nova ADR recomendada |
| RISK-AUTH-05 — Oráculo de timing no password-reset | Enumeração de usuários | Onda 5 | Delay constante configurável + PBT-03 API (TASK-20) |
| RISK-AUTH-06 — VAL-AUTH-02 / VAL-09 não confirmados | TTL de sessão/convite inadequado antes do go-live | Onda 6 | DD-008/DD-009 provisórios; gate de confirmação no DoD (TASK-25) |

---

## 9. Referências

| Referência | Caminho |
|------------|---------|
| requirements.md v0.1.0 | docs/product/modules/authentication/requirements.md |
| design.md v0.1.0 | docs/product/modules/authentication/design.md |
| README do módulo | docs/product/modules/authentication/README.md |
| TRD (FLOW-05, DEC-005, DEC-006, RLS) | docs/product/trd/trd.md |
| `.forge/rules/architecture/clean-architecture.md` | rules transversal |
| `.forge/rules/architecture/ddd.md` | rules transversal |
| `.forge/rules/architecture/jwt-authentication.md` | rules transversal (DD-002) |
| `.forge/rules/architecture/security-and-compliance.md` | rules transversal |
| `.forge/rules/architecture/security-and-secrets.md` | rules transversal |
| `.forge/rules/architecture/observability.md` | rules transversal |
| `.forge/rules/domain/audit-immutability.md` | rules transversal |
| Módulo `organization` | docs/product/modules/organization/ |
| Módulo `tenant-administration` | docs/product/modules/tenant-administration/ |
| Módulo `notification-delivery` | docs/product/modules/notification-delivery/ |
| Módulo `audit-log` | docs/product/modules/audit-log/ |

---

> **Observação sobre README do módulo:** `docs/product/modules/authentication/README.md` existe e deve ser atualizado para incluir status `Rascunho para revisão`, versão `0.1.0`, lista das 6 ondas, 25 TASKs planejadas e status dos três artefatos (requirements.md v0.1.0 Rascunho para revisão; design.md v0.1.0 Rascunho para revisão; tasks.md v0.1.0 Rascunho para revisão). Recomenda-se incluir essa atualização no encerramento da Onda 1 (TASK-01) ou em task dedicada de documentação.
