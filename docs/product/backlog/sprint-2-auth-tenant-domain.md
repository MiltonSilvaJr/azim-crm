# Sprint 2 — auth-tenant-domain

- **Objetivo:** Ao final desta sprint, um PlatformOperator consegue provisionar o primeiro tenant com slug único e fuso configurado, e um usuário consegue iniciar o fluxo de login com Google SSO pelo GCP Identity Platform com sessão escopada ao tenant correto, viabilizando o primeiro tenant Azim criado em ambiente de desenvolvimento.
- **Período:** 2026-06-29 a 2026-07-12
- **Story Points totais:** 21
- **Status:** Planejada
- **Dependências:** Sprint 1 (bootstraps e contracts compilando)

---

## 1. Backlog da Sprint

### US-012 — Login com Google SSO via GCP Identity Platform escopado ao tenant

- **Épico:** authentication (EP-003)
- **RF rastreado:** Req 1, Req 4, Req 5
- **Story Points:** 5
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `Authentication.sln` com 10 projetos compilando; Architecture.Tests verdes (Firebase SDK confinado a Infrastructure)
- [ ] `AuthContext` (user_id, tenant_id, email, roles, memberships), `Session` e `TenantSlug` imutáveis e com igualdade por valor
- [ ] `SessionState` enum com transitions válidas/inválidas testadas
- [ ] PBT-01 verde: para qualquer `AuthContext` gerado, `tenant_id` é único e não nulo (≥ 100 amostras FsCheck)
- [ ] `IdentityRef` com `identity_uid` presente apenas em `Authentication.Infrastructure` (Architecture.Tests verde)

#### Tasks técnicas
- authentication/TASK-01: Bootstrap solution e 10 projetos — TO DO — pendente Jira
- authentication/TASK-02: Architecture.Tests regra de dependência + confinamento Firebase — TO DO — pendente Jira
- authentication/TASK-03: Objetos de valor imutáveis do domínio (AuthContext, Session, TenantSlug) — TO DO — pendente Jira
- authentication/TASK-04: Máquina de estados de sessão + Specifications + PBT-01 — TO DO — pendente Jira

#### Definition of Done
- [ ] Domain.Tests ≥ 95% coverage
- [ ] PBT-01 verde (≥ 100 amostras)
- [ ] Architecture.Tests verde
- [ ] CI verde; Code review aprovado; PR mergeado

---

### US-019 — Provisionamento de tenant com slug único via saga compensatória

- **Épico:** tenant-administration (EP-004)
- **RF rastreado:** Req 1, Req 2
- **Story Points:** 8
- **Status atual:** TO DO
- **Issue Jira:** pendente

#### Critérios de aceite
- [ ] `TenantAdministration.sln` com 10 projetos compilando; Architecture.Tests verdes
- [ ] `Slug` rejeita caracteres fora de `[a-z-]`, `--` consecutivos, bordas com `-`, tamanho 3..40; PBT-01 e PBT-02 verdes
- [ ] `TimezoneIana` valida contra base IANA do runtime; `DigestTime` valida `HH:mm`
- [ ] `WcagContrastPolicy` determinística com limítrofes corretos (4.4 reprova, 4.5 aprova); PBT-04 e PBT-05 verdes
- [ ] `BrandingTheme` com apenas 4 campos (logoUrl, faviconUrl, ColorPair); PBT-03 verde
- [ ] Aggregate `Tenant` com state machine `provisioned/suspended`; PBT-08 verde
- [ ] Domain events tipados enfileirados pelo aggregate

#### Tasks técnicas
- tenant-administration/TASK-01: Bootstrap solution e Architecture.Tests — TO DO — pendente Jira
- tenant-administration/TASK-02: VOs Slug, TimezoneIana, DigestTime (PBT-01, PBT-02) — TO DO — pendente Jira
- tenant-administration/TASK-03: WcagContrastPolicy, ToneDerivationService, ColorPair (PBT-04, PBT-05) — TO DO — pendente Jira
- tenant-administration/TASK-04: BrandingTheme, AssetValidationPolicy (PBT-03) — TO DO — pendente Jira
- tenant-administration/TASK-05: Aggregate Tenant + TenantBranding + state machine (PBT-08) — TO DO — pendente Jira
- tenant-administration/TASK-06: Domain events e enfileiramento pelo aggregate — TO DO — pendente Jira

#### Definition of Done
- [ ] Domain.Tests ≥ 95% coverage
- [ ] PBTs 01-05, 08 verdes
- [ ] Architecture.Tests verde
- [ ] CI verde; Code review aprovado; PR mergeado

---

## 2. Bugs Acompanhados

| ID | Severidade | Descrição | Status | Issue |
|---|---|---|---|---|
| — | — | Nenhum | — | — |

---

## 3. Riscos da Sprint

| Risco | Mitigação |
|---|---|
| FsCheck gerando casos de borda para `Slug` pode exigir Arbitrary customizado | Implementar `Arbitrary<Slug>` no início da TASK-02 antes dos PBTs |
| `WcagContrastPolicy` com `round(R,2)` pode ter instabilidade em bordas — DD-003 | Confirmar `Math.Round(ratio, 2, MidpointRounding.AwayFromZero)` e testar exaustivamente 4.44/4.45/4.46/4.54/4.55 |

---

## 4. Encerramento

- [ ] Todas as stories em DONE
- [ ] Todos os PRs mergeados
- [ ] Sprint encerrada no Jira (quando sincronizado)
- [ ] `progress-tracking.md` atualizado
