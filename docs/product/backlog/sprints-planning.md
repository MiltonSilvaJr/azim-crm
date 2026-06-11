# Sprints Planning — Azim CRM Fase 1

- **Versão:** 0.1.0
- **Data:** 2026-06-11
- **Sprint length:** 2 semanas
- **Total de sprints:** 15
- **Período estimado:** 2026-06-15 a 2027-01-10 (30 semanas)
- **Baseline HITL #1:** aprovado 2026-06-11 — 321 TASKs de 13 módulos aprovadas

---

## 1. Visão Executiva

O backlog da Fase 1 do Azim CRM cobre 13 módulos organizados em três camadas de dependência:

**Camada Generic (sprints 1–6):** audit-log, notification-delivery, authentication e tenant-administration formam a fundação. Nenhum módulo de negócio pode ser implementado sem autenticação e trail de auditoria operacionais. A Sprint 1 estabelece os bootstraps de Clean Architecture e os contratos públicos. Sprints 2–4 consolidam domínio, aplicação e infraestrutura dos módulos genéricos. Sprints 5–6 entregam APIs REST completas, hardening e o adapter de notificação com provider Resend (ADR-0005).

**Camada Supporting (sprints 7–13):** organization é o primeiro porque define BUs, papéis e pipeline config que todos os demais dependem. Em seguida account-management e partner-management operam em paralelo (sprints 8–10). activity-management e goal-forecast formam o núcleo de produtividade do vendedor (sprints 11–12). digest e reporting constroem sobre todos os módulos anteriores (sprints 12–13). data-migration é executado em sprint dedicada (13) por ser módulo temporário.

**Camada Core Domain (sprints 14–15):** opportunity-pipeline depende de todos os Supporting modules. As sprints 14–15 implementam o domínio completo do pipeline, incluindo os deltas do HITL #1 (multimoeda ADR-0008, campos B2B CNPJ/razão social em account-management) e o hardening de go-live (SPF/DKIM/DMARC, LGPD base legal/retenção, ADRs formalizados).

**Marco de Go-Live:** ao término da Sprint 15, o sistema deve estar apto para a primeira demo interna com dados reais migrados (data-migration Sprint 13) e o Kanban de oportunidades operacional para a equipe de vendas.

---

## 2. Mapa Módulo → Sprints

| Módulo | Subdomínio | Sprints envolvidas | Marco principal |
|---|---|---|---|
| audit-log | Generic | Sprint 1, 4, 6 | Sprint 6: trilha append-only operacional, RBAC de consulta, PII mascarado |
| notification-delivery | Generic | Sprint 1, 5, 6 | Sprint 6: IEmailSender com Resend + Postmark, PBTs verdes, CircuitBreaker ativo |
| authentication | Generic | Sprint 2, 3, 4, 5 | Sprint 5: login, logout, convite, reset-senha operacionais em produção |
| tenant-administration | Generic | Sprint 2, 3, 4, 5 | Sprint 5: provisioning saga, branding WCAG, brand.json público, digest config |
| organization | Supporting | Sprint 7, 8, 9 | Sprint 9: BUs, RBAC, pipeline-config e provisioning inicial operacionais |
| account-management | Supporting | Sprint 8, 10 | Sprint 10: accounts + contacts PII + 360° + CNPJ/razão social operacionais |
| partner-management | Supporting | Sprint 8, 9, 10 | Sprint 10: CRUD parceiros + comissões default + elegibilidade operacionais |
| activity-management | Supporting | Sprint 11, 12 | Sprint 12: CRUD atividades + digest actions + scan estagnação operacionais |
| goal-forecast | Supporting | Sprint 11, 12, 14 | Sprint 12: metas + painel realizado/forecast; Sprint 14: multimoeda |
| data-migration | Supporting | Sprint 13 | Sprint 13: upload → dry-run → triagem → import tudo-ou-nada operacional |
| digest | Supporting | Sprint 12, 13 | Sprint 13: digest 07:00 por fuso, links 1 clique, purge 90 dias operacionais |
| reporting | Supporting | Sprint 14 | Sprint 14: 5 relatórios + export CSV operacionais via views PostgreSQL |
| opportunity-pipeline | Core Domain | Sprint 14, 15 | Sprint 15: Kanban, Win/Lose, comissão snapshot, eventos, multimoeda operacionais |

---

## 3. Tabela de Sprints

| Sprint | Slug | Objetivo (1 frase) | Início | Fim | Stories | Story Points |
|---|---|---|---|---|---|---|
| 1 | foundation | Ao final desta sprint, o time tem os bootstraps de Clean Architecture de todos os módulos genéricos compilando com gates de arquitetura verdes, contratos públicos IAuditWriter e IEmailSender definidos, e os ADRs 0001-0008 formalizados, viabilizando o início do desenvolvimento sobre uma base arquitetural validada. | 2026-06-15 | 2026-06-28 | US-001, US-007, US-079 + tarefas TASK-01..02 (audit-log) + TASK-01..02 (notification-delivery) | 16 |
| 2 | auth-tenant-domain | Ao final desta sprint, um PlatformOperator consegue provisionar um tenant com slug único e um usuário consegue iniciar login com Google SSO, viabilizando o primeiro tenant Azim criado em ambiente de desenvolvimento. | 2026-06-29 | 2026-07-12 | US-012, US-019 + TASK-01..04 (auth) + TASK-01..06 (tenant-adm) | 21 |
| 3 | auth-tenant-application | Ao final desta sprint, um TAdmin consegue configurar branding WCAG do tenant, definir fuso do digest e suspender/reativar o tenant, e a máquina de estados de sessão JWT está completa com PBTs de isolamento cross-tenant verdes. | 2026-07-13 | 2026-07-26 | US-016, US-020, US-021, US-022 + TASK-05..09 (auth) + TASK-07..11 (tenant-adm) | 24 |
| 4 | generic-infra-audit-app | Ao final desta sprint, a trilha de auditoria está com append-only enforçado no banco (trigger + REVOKE + RLS), o cache Redis de memberships está operacional e o mascaramento de PII no delta está com PBTs verdes. | 2026-07-27 | 2026-08-09 | US-003, US-004, US-006, US-017 + TASK-10..14 (auth) + TASK-12..16 (tenant-adm) + TASK-03..10 (audit-log) | 26 |
| 5 | auth-tenant-api-notif-app | Ao final desta sprint, um usuário consegue fazer login, logout, convite e reset-senha pela API REST completa, brand.json público está disponível via CDN, e o IEmailSender com Resend (ADR-0005) está integrado com branding e retry. | 2026-08-10 | 2026-08-23 | US-008, US-009, US-011, US-013, US-014, US-015, US-018, US-023, US-081 + TASK-15..25 (auth) + TASK-17..22 (tenant-adm) + TASK-07..16 (notif-delivery) | 38 |
| 6 | audit-api-notif-hardening | Ao final desta sprint, a API de consulta da trilha de auditoria está operacional com RBAC, isolamento por BU e catálogo de erros completo, e o módulo notification-delivery tem PBTs-01..05 verdes e observabilidade ativa. | 2026-08-24 | 2026-09-06 | US-002, US-005, US-010 + TASK-11..20 (audit-log) + TASK-17..21 (notif-delivery) | 24 |
| 7 | organization-foundation | Ao final desta sprint, um TAdmin consegue criar, renomear e desativar Business Units, convidar usuários com papéis e desativá-los, com RBAC operacional e API REST completa para o módulo organization (ondas 1–3). | 2026-09-07 | 2026-09-20 | US-024, US-025, US-026 + TASK-01..14 (organization) | 24 |
| 8 | org-app-account-partner-domain | Ao final desta sprint, um TAdmin consegue configurar estágios, canais e motivos de perda via API, parceiros são cadastráveis com comissões default, e contas são criáveis com dedupe automático. | 2026-09-21 | 2026-10-04 | US-027, US-028, US-030, US-035 + TASK-15..19 (organization) + TASK-01..03 (account) + TASK-01..08 (partner) | 26 |
| 9 | org-api-account-partner-app | Ao final desta sprint, o provisioning inicial de organização por evento TenantProvisioned está operacional, parceiros têm lifecycle (desativar/reativar) e elegibilidade consultável, e a porta IPartnerCommissionReadPort está estável. | 2026-10-05 | 2026-10-18 | US-029, US-036, US-037, US-038 + TASK-20..28 (organization) + TASK-04..07 (account) + TASK-09..14 (partner) | 24 |
| 10 | account-partner-api-activity-domain | Ao final desta sprint, um Vendedor consegue ver a visão 360° de uma conta, exercer direito de esquecimento de contato (LGPD), contas têm CNPJ/razão social, e o módulo partner-management está com API REST e hardening completos. | 2026-10-19 | 2026-11-01 | US-031, US-032, US-033, US-034 + TASK-08..18 (account) + TASK-15..29 (partner) | 30 |
| 11 | activity-app-goal-domain | Ao final desta sprint, um Vendedor consegue criar, completar e reagendar atividades pelo CRM, a visão Meu Dia/Minha Semana está disponível, e metas mensais por BU são criáveis e consultáveis com painel realizado/forecast. | 2026-11-02 | 2026-11-15 | US-039, US-040, US-044, US-045 + TASK-01..12 (activity) + TASK-01..14 (goal-forecast) | 26 |
| 12 | activity-api-goal-api-digest-domain | Ao final desta sprint, links de ação de 1 clique do digest (concluir/reagendar atividade) estão operacionais, a detecção de estagnação publica OpportunityStale, o painel de forecast individual do vendedor está disponível, e o digest tem conteúdo composto (atividades + metas). | 2026-11-16 | 2026-11-29 | US-041, US-042, US-043, US-046, US-047, US-048, US-055 + TASK-13..24 (activity) + TASK-15..27 (goal-forecast) + TASK-01..13 (digest) | 38 |
| 13 | migration-digest-api-reporting | Ao final desta sprint, um TAdmin consegue importar a planilha legada com dry-run e rollback garantido, o digest 07:00 está operacional via Cloud Scheduler com links de ação e purge de 90 dias, e o reporting tem views PostgreSQL e API REST com 5 relatórios disponíveis. | 2026-11-30 | 2026-12-13 | US-050, US-051, US-052, US-053, US-054, US-056, US-057, US-058, US-059 + TASK-01..28 (data-migration) + TASK-14..27 (digest) | 44 |
| 14 | reporting-api-pipeline-domain | Ao final desta sprint, os 5 relatórios com export CSV estão disponíveis via API, o domínio do opportunity-pipeline (calculators, aggregate, eventos) está com PBTs financeiros verdes, e metas suportam multimoeda BRL/USD/EUR. | 2026-12-14 | 2026-12-27 | US-049, US-060, US-061, US-062, US-063, US-064, US-065, US-073 + TASK-20..25 (reporting) + TASK-01..12 (opportunity-pipeline) | 44 |
| 15 | pipeline-api-hitl-golive | Ao final desta sprint, um Vendedor consegue usar o Kanban completo (criar, mover, ganhar, perder, reabrir oportunidades, vincular parceiro com comissão snapshot imutável e multimoeda), go-live readiness está atendido (SPF/DKIM/DMARC, LGPD base legal/retenção) e o sistema está pronto para a primeira demo interna com dados migrados. | 2026-12-28 | 2027-01-10 | US-066..US-082 + TASK-13..24 (opportunity-pipeline) | 88 |

---

## 4. Dependências Críticas

| Sprint | Depende de | Motivo |
|---|---|---|
| Sprint 2 | Sprint 1 | Bootstrap de solução e contracts (IAuditWriter, IEmailSender) devem compilar antes do domínio |
| Sprint 3 | Sprint 2 | Portas de aplicação (auth) e aggregate Tenant (tenant-adm) dependem dos domínios criados na Sprint 2 |
| Sprint 4 | Sprint 2, Sprint 3 | Infrastructure de auth (Firebase adapter, Redis) depende de portas estáveis da Sprint 3; audit-log infra depende de domain da Sprint 1 |
| Sprint 5 | Sprint 3, Sprint 4 | API REST de auth/tenant depende da infraestrutura da Sprint 4; notification-delivery app depende de contracts da Sprint 1 |
| Sprint 6 | Sprint 4, Sprint 5 | Audit Log API depende de infra (Sprint 4); notification-delivery hardening depende de infra (Sprint 5) |
| Sprint 7 | Sprint 5, Sprint 6 | Organization depende de authentication (JWT/middleware) e audit-log (IAuditWriter) operacionais |
| Sprint 8 | Sprint 7 | account-management e partner-management dependem de organization (BUs, pipeline config) para contexto de tenant |
| Sprint 9 | Sprint 7, Sprint 8 | Provisioning inicial de organização depende do evento TenantProvisioned (tenant-adm Sprint 5) e dos handlers (Sprint 8) |
| Sprint 10 | Sprint 8, Sprint 9 | 360° de conta depende de activity-management e opportunity-pipeline read ports (Sprint 11+); partner API finalizada aqui |
| Sprint 11 | Sprint 9, Sprint 10 | activity-management depende de account-management (leitura de contas) e organization (RBAC); goal-forecast depende de organization (BU membership) |
| Sprint 12 | Sprint 11 | digest domain depende de activity (ActivityOverdue) e goal-forecast (digest block); links de ação dependem de activity handlers |
| Sprint 13 | Sprint 11, Sprint 12 | data-migration depende de account, partner, opportunity (ports); digest API depende de domain da Sprint 12 |
| Sprint 14 | Sprint 13 | reporting depende de oportunidades, metas e comissões (read via views PostgreSQL); opportunity-pipeline domain depende de todos os supporting modules |
| Sprint 15 | Sprint 14 | opportunity-pipeline infra/API depende do domínio da Sprint 14; go-live hardening depende de todos os módulos completos |

---

## 5. Riscos de Cronograma

| Risco | Sprint impactada | Mitigação |
|---|---|---|
| Configuração do Firebase Emulator / WireMock para testes de authentication | Sprint 4 | Spike de 1 dia no início da Sprint 3; documentar setup em `.forge/` |
| TenantProvisioningSaga (saga compensatória com GCP IdP) pode ter edge cases | Sprint 4 | PBT-07 cobre atomicidade; fixture de teste com IdP fake |
| Complexidade do OpportunityNumberGenerator com lock atômico por tenant | Sprint 15 | PBT-01 e teste de concorrência em Sprint 14 (TASK-15) |
| Testcontainers com PostgreSQL real pode ser lento em CI | Sprint 4, 7, 8 | Paralelizar com `-parallel` no xUnit; usar pool de containers |
| Provider Resend (ADR-0005) sem SDK .NET oficial — HTTP direto | Sprint 5 | Spike de 1 dia no início da Sprint 5; ProviderResponseMapper cobre fallback |
| Relatórios com views PostgreSQL e `security_invoker` podem ter latência alta | Sprint 14 | Índices críticos em TASK-17 (reporting); baseline p95 em TASK-25 |
| data-migration com planilha de 500+ linhas pode exceder timeout HTTP | Sprint 13 | Processamento assíncrono com MigrationJob + polling de status (TASK-13) |
| Multimoeda (ADR-0008) impacta opportunity-pipeline, goal-forecast e comissões | Sprint 14, 15 | Objeto de valor Money com currency implementado em Sprint 14 antes da API |
| LGPD base legal/retenção não definida juridicamente antes do go-live | Sprint 15 | US-082 em Sprint 15; DPO deve aprovar antes do encerramento da sprint |
