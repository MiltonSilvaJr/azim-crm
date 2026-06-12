# DDD Segmentation — Azim CRM

**Status:** Rascunho para revisão
**Versão:** v0.1
**Data:** 2026-06-11
**Owner:** Milton Antonio da Silva Jr

---

## §1. Espaço do Problema

### §1.1 Consolidação do Domínio

O Azim CRM é uma plataforma de gestão comercial SaaS multi-tenant voltada para empresas B2B.
O primeiro tenant e operador é a **Vellus** (3 BUs: Vellus, Axis, Vellus Tech), que opera 108 oportunidades em planilha Excel sem controle de acesso, auditoria, owners obrigatórios ou metas cadastradas.

**Problema central:** pipeline comercial sem rastreabilidade, responsabilidade ou forecast estruturado.

**Diferencial estratégico confirmado:** comissão de parceiro nativa por componente (setup + recorrente) com snapshot imutável ao fechar negócio — gap verificado adversarialmente em Salesforce e Zoho.

**Capacidades nucleares entregues:**
1. Pipeline estruturado com owner obrigatório e Kanban por BU
2. Comissão de parceiro nativa com snapshot imutável
3. Digest diário acionável às 07:00 BRT (CRM que vai ao vendedor)
4. Metas mensais com graceful degradation e painel comparativo
5. Multi-tenancy verificada por RLS com branding por tenant

**Eventos de domínio centrais (da spec aprovada):**
`opportunity.created`, `opportunity.stage_changed`, `opportunity.won`, `opportunity.lost`,
`opportunity.stale`, `activity.created`, `activity.completed`, `activity.overdue`,
`goal.updated`, `tenant.branding_changed`

---

### §1.2 Classificação de Subdomínios

| Código | Subdomínio | Classificação | Justificativa |
|---|---|---|---|
| SD-01 | Opportunity Pipeline | **Core Domain** | Diferencial competitivo confirmado: owner obrigatório, comissão nativa, forecast ponderado Setup+Recorrente, snapshot imutável. Regras complexas e proprietárias não replicáveis por commodity. |
| SD-02 | Account Management | Supporting Subdomain | Contas e contatos são necessários ao pipeline mas não diferenciam o produto; dedupe e visão 360° são de suporte ao Core. |
| SD-03 | Partner Management | Supporting Subdomain | Parceiros e percentuais de comissão default alimentam o Core; a gestão do parceiro em si (CRUD, visão de comissão projetada) é suporte ao processo comercial. |
| SD-04 | Activity Management | Supporting Subdomain | Atividades e follow-ups alimentam o Digest e a detecção de estagnação; têm regras próprias mas não são o diferencial. |
| SD-05 | Goal & Forecast | Supporting Subdomain | Metas mensais e painel comparativo são importantes para gestores mas derivam de dados do Core; graceful degradation indica suporte, não independência. |
| SD-06 | Digest | Supporting Subdomain | Seleção de destinatários por fuso IANA, composição do azimute e idempotência têm lógica de domínio específica; o envio em si é Generic. |
| SD-07 | Reporting | Supporting Subdomain | Relatórios e rankings são read models derivados de dados do Core e de outros contextos; sem modelo próprio complexo. |
| SD-08 | Organization Management | Supporting Subdomain | BUs, usuários, papéis e RBAC viabilizam o pipeline mas são suporte à operação multi-tenant; sem diferencial competitivo. |
| SD-09 | Workflow Automation | Supporting Subdomain | Editor visual de automações por BU (Fase 2); reduz trabalho manual mas não é o diferencial central. |
| SD-10 | AI Intelligence | Supporting Subdomain | Scoring, resumo 360° e copilot (Fase 3); diferenciais futuros mas dependentes de validação das fases anteriores. |
| SD-11 | Data Migration | Supporting Subdomain | Import da planilha Pipeline Vellus.xlsx; capacidade temporária (Fase 1) com lógica própria de dry-run e triagem. |
| SD-12 | Identity & Access | Generic Subdomain | Autenticação via GCP Identity Platform; commodity substituível; integrado mas não proprietário. |
| SD-13 | Tenancy & Branding | Generic Subdomain | Provisionamento de tenant, slug, fuso, white-label; infraestrutura multi-tenant; commodity de plataforma. |
| SD-14 | Notification Delivery | Generic Subdomain | IEmailSender / Postmark / SendGrid; envio de e-mail transacional; commodity explicitamente abstraída (DEC-008). |
| SD-15 | Audit Log | Generic Subdomain | AuditLog imutável cross-cutting; padrão de append-only sem lógica de domínio própria; commodity de compliance. |

---

## §2. Event Storming Analítico

### Fluxo 1 — Gestão do Pipeline (Core)

| Ordem | Ator/Sistema | Comando | Política/Regra | Evento Resultante | Entidade/Agregado |
|---|---|---|---|---|---|
| 1 | Vendedor | CreateOpportunity | RN-002: owner obrigatório; RN-001: numeração sequencial | OpportunityCreated | Opportunity |
| 2 | Vendedor | MoveOpportunityStage | RN-003: data fechamento a partir de Proposta Enviada | OpportunityStageChanged | Opportunity |
| 3 | Vendedor | MoveOpportunityToLost | RN-004: motivo de perda obrigatório | OpportunityLost | Opportunity |
| 4 | Vendedor/Gestor | SetPartnerCommission | RN-026: fórmula de cálculo | CommissionCalculated | OpportunityPartnerCommission |
| 5 | Vendedor/Gestor | CloseOpportunityAsWon | RN-007: snapshot imutável | OpportunityWon | Opportunity |
| 6 | Sistema (scheduler) | DetectStaleOpportunity | RN-028: 14 dias sem atividade | OpportunityStale | Opportunity |
| 7 | Gestor/TAdmin | ReopenOpportunity | RN-016: somente Tenant Admin e Gestor de BU | OpportunityReopened | Opportunity |

### Fluxo 2 — Digest Diário

| Ordem | Ator/Sistema | Comando | Política/Regra | Evento Resultante | Entidade/Agregado |
|---|---|---|---|---|---|
| 1 | Cloud Scheduler | TriggerDigestJob | Horário UTC convertido por fuso IANA do tenant | DigestJobTriggered | — |
| 2 | Digest Service | SelectDigestRecipients | RN-009, RN-011: fuso + pendências + papel | RecipientsSelected | EmailDigestLog |
| 3 | Digest Service | ComposeDigestContent | RN-029: azimute apenas na segunda; RN-018: omite bloco sem meta | DigestComposed | EmailDigestLog |
| 4 | Digest Service | SendDigestEmail | RN-010: idempotência por user+date | DigestEmailSent | EmailDigestLog |
| 5 | Vendedor | CompleteActivityViaDigest | Idempotência por EmailDigestLog | ActivityCompleted | Activity |

### Fluxo 3 — Comissão de Parceiro

| Ordem | Ator/Sistema | Comando | Política/Regra | Evento Resultante | Entidade/Agregado |
|---|---|---|---|---|---|
| 1 | TAdmin/Gestor | CreatePartner | RN-021: sem login no MVP | PartnerCreated | Partner |
| 2 | Vendedor | LinkPartnerToOpportunity | RN-008: canal Parceiro exige partner_id | PartnerLinked | Opportunity |
| 3 | Vendedor/Gestor | SetCommissionPercentages | RN-026: pct_setup × valor_setup + pct_recorrente × valor_mensal × meses | CommissionCalculated | OpportunityPartnerCommission |
| 4 | Sistema | SnapshotCommissionOnWin | RN-007 + RN-022: imutável após criação | CommissionSnapshotCreated | OpportunityPartnerCommission |

---

## §3. Business Capability Map

| Código | Capacidade | Descrição | Comandos | Eventos | Evidências |
|---|---|---|---|---|---|
| CAP-01 | Gestão de Pipeline | Ciclo de vida de oportunidades do funil | CreateOpportunity, MoveStage, CloseWon, CloseLost | OpportunityCreated, StageChanged, Won, Lost | PRD RF-06; evidência 108 opps Vellus |
| CAP-02 | Comissão Nativa de Parceiro | Cálculo e snapshot imutável de comissão | SetCommission, SnapshotCommission | CommissionCalculated, CommissionSnapshotCreated | PRD RF-06; DEC-002; gap Salesforce/Zoho |
| CAP-03 | Forecast Ponderado | Cálculo valor_total e forecast_ponderado | UpdateValue | ForecastUpdated | PRD RF-06; RN-005, RN-006 |
| CAP-04 | Gestão de Contas e Contatos | CRUD de contas com dedupe; visão 360° | CreateAccount, LinkContact | AccountCreated, ContactLinked | PRD RF-04 |
| CAP-05 | Gestão de Parceiros | CRUD de parceiros; visão de comissão projetada/consolidada | CreatePartner, SetDefaultPcts | PartnerCreated | PRD RF-05 |
| CAP-06 | Atividades e Follow-ups | CRUD de atividades; detecção de estagnação | CreateActivity, CompleteActivity | ActivityCreated, ActivityCompleted, OpportunityStale | PRD RF-07 |
| CAP-07 | Metas e Forecast Comparativo | Cadastro de metas mensais; painel realizado vs meta | SetGoal | GoalUpdated | PRD RF-08; DEC-003 |
| CAP-08 | Digest Diário | Seleção, composição e envio de e-mail diário | TriggerDigest, SendDigest | DigestSent | PRD RF-09; DEC-009 |
| CAP-09 | Relatórios | Funil, forecast, ranking, canais, comissões, CSV | GenerateReport | ReportGenerated | PRD RF-11 |
| CAP-10 | Gestão de Organização | BUs, usuários, papéis, RBAC | CreateBU, InviteUser | BUCreated, UserInvited | PRD RF-03 |
| CAP-11 | Automações Visuais | Editor React Flow; execução assíncrona (Fase 2) | CreateWorkflow, TriggerWorkflow | WorkflowExecuted | PRD RF-12 |
| CAP-12 | Inteligência Artificial | Scoring, resumo, próxima ação, copilot (Fase 3) | RequestScoring, RequestSummary | ScoringCompleted | PRD RF-13 |
| CAP-13 | Migração de Planilha | Dry-run, triagem, import transacional | UploadSpreadsheet, ExecuteImport | ImportCompleted | PRD Fase 1; DEC-010 |
| CAP-14 | Autenticação e Identidade | Login, sessão por tenant, convite | Login, Logout | UserAuthenticated | PRD RF-01 |
| CAP-15 | Tenancy e Branding | Provisionamento, slug, white-label | ProvisionTenant, SetBranding | TenantProvisioned, BrandingChanged | PRD RF-02 |
| CAP-16 | Entrega de Notificações | Envio de e-mail transacional via IEmailSender | SendEmail | EmailSent | PRD RF-09; DEC-008 |
| CAP-17 | Trilha de Auditoria | Registro imutável de toda escrita em entidade de negócio | — (transversal) | AuditLogWritten | PRD seção 8.6; RN-024 |

---

## §4. Espaço da Solução

### §4.1 Bounded Context Candidates

| Código | Bounded Context | Subdomínio | Tipo | Justificativa | Decisão |
|---|---|---|---|---|---|
| BC-01 | Opportunity Pipeline | SD-01 | Core Domain | Linguagem própria (opportunity, stage, stale, forecast_ponderado, valor_total, snapshot); regras proprietárias (RN-001..RN-008, RN-016, RN-022, RN-028); owner imutável do modelo de comissão; ciclo de vida central; integrações específicas (todos os outros BCs lêem daqui). | Confirmar |
| BC-02 | Account Management | SD-02 | Supporting Subdomain | Linguagem própria (account, contact, dedupe, 360° view); regras de normalização (RN-014); ciclo de vida independente (conta existe sem oportunidade); compartilhada entre BUs sem duplicação. | Confirmar |
| BC-03 | Partner Management | SD-03 | Supporting Subdomain | Linguagem própria (partner, pct_setup, pct_recorrente, comissão projetada vs consolidada); percentuais default por componente; ciclo de vida de parceiro independente do fechamento da oportunidade. | Confirmar |
| BC-04 | Activity Management | SD-04 | Supporting Subdomain | Linguagem própria (activity, follow-up, overdue, stagnation); regras de detecção de estagnação (RN-028); alimenta o Digest; ciclo de vida independente (atividade existe vinculada a oportunidade mas tem estado próprio). | Confirmar |
| BC-05 | Goal & Forecast | SD-05 | Supporting Subdomain | Linguagem própria (goal, meta mensal, graceful degradation, realizado vs meta vs pipeline); regras de agregação derivada (RN-027); ciclo de vida independente (meta existe sem atividade de pipeline). | Confirmar |
| BC-06 | Digest | SD-06 | Supporting Subdomain | Linguagem própria (digest, azimute da semana, EmailDigestLog, recipient selection, horario_digest); regras de seleção por fuso IANA (RN-009), idempotência (RN-010), azimute (RN-029); integra Cloud Scheduler, Notification Delivery e Pipeline. | Confirmar |
| BC-07 | Reporting | SD-07 | Supporting Subdomain | Read models derivados de múltiplos contextos; sem escrita própria em entidades de negócio; linguagem própria (funil, ranking, canal, export CSV); separado para evitar acoplamento de consultas com Pipeline. | Confirmar |
| BC-08 | Organization Management | SD-08 | Supporting Subdomain | Linguagem própria (BU, papel, RBAC, membership, convite, slug-dentro-do-tenant); regras de desativação (RN-013), mínimo de um Tenant Admin (MSG-017); ciclo de vida de usuário independente do pipeline. | Confirmar |
| BC-09 | Data Migration | SD-11 | Supporting Subdomain | Linguagem própria (dry-run, triagem assistida, import transacional, congelamento); regras próprias (RN-023: rollback total); capacidade temporária Fase 1; não deve poluir o modelo do Pipeline. | Confirmar |
| BC-10 | Workflow Automation | SD-09 | Supporting Subdomain | Fase 2; linguagem própria (workflow, trigger, condition, action node, log de execução); execução assíncrona via Pub/Sub; separado do Core para permitir evolução independente. | Confirmar |
| BC-11 | AI Intelligence | SD-10 | Supporting Subdomain | Fase 3; Python/FastAPI/LangGraph; linguagem própria (scoring, briefing, next best action, copilot, Langfuse); isolamento por tenant para rate-limit e custo; stack completamente diferente. | Confirmar |
| BC-12 | Identity & Access | SD-12 | Generic Subdomain | GCP Identity Platform multi-tenant; commodity; Anti-Corruption Layer para proteção do modelo interno. | Confirmar |
| BC-13 | Tenancy & Branding | SD-13 | Generic Subdomain | Provisionamento de tenant (PlatOp), slug imutável (RN-019), white-label estrito (DEC-004); ciclo de vida de plataforma separado de operação comercial. | Confirmar |
| BC-14 | Notification Delivery | SD-14 | Generic Subdomain | Abstração IEmailSender com Postmark/SendGrid (DEC-008); commodity; Anti-Corruption Layer para proteger o modelo de negócio da instabilidade de providers. | Confirmar |
| BC-15 | Audit Log | SD-15 | Generic Subdomain | AuditLog imutável cross-cutting (RN-024); append-only; sem lógica de domínio própria; consumido por todos os contextos de escrita. | Confirmar |

---

## §5. Boundary Validation Matrix

| Bounded Context | Linguagem Própria | Regras Próprias | Ciclo de Vida Próprio | Ownership de Dados | NFRs Específicos | Decisão |
|---|---|---|---|---|---|---|
| Opportunity Pipeline | Sim | Sim (RN-001..008, 016, 022, 028) | Sim | Sim | Performance Kanban (NFR-PERF-02/03); snapshot imutável | Confirmar |
| Account Management | Sim | Sim (RN-014) | Sim | Sim | LGPD (RN-025) | Confirmar |
| Partner Management | Sim | Sim (RN-026, RN-008) | Sim | Sim | Relatório de comissão | Confirmar |
| Activity Management | Sim | Sim (RN-028) | Sim | Sim | Detecção de estagnação em tempo real | Confirmar |
| Goal & Forecast | Sim | Sim (RN-017, RN-027) | Sim | Sim | Graceful degradation | Confirmar |
| Digest | Sim | Sim (RN-009..011, 018, 029) | Sim | Sim | Performance (NFR-PERF-05); entrega 07:00 BRT | Confirmar |
| Reporting | Parcial | Não (read-only) | Não (depende de outros) | Não (read models) | Export CSV | Confirmar |
| Organization Management | Sim | Sim (RN-013, MSG-017) | Sim | Sim | RBAC, segurança | Confirmar |
| Data Migration | Sim | Sim (RN-023) | Fase 1 apenas | Temporário | Import em ≤ 5 min (NFR-PERF-06) | Confirmar |
| Workflow Automation | Sim | Sim (execução assíncrona) | Sim (Fase 2) | Sim | Execução assíncrona obrigatória | Confirmar |
| AI Intelligence | Sim | Sim (rate-limit, isolamento) | Sim (Fase 3) | Sim | Rate-limit Redis; custo por tenant | Confirmar |
| Identity & Access | Parcial (GCP) | GCP padrão | Sim | Não (GCP IdP) | Segurança (NFR-SEG) | Confirmar |
| Tenancy & Branding | Sim | Sim (RN-019, RN-020) | Sim | Sim | WCAG AA | Confirmar |
| Notification Delivery | Não (abstração) | Não (provider) | Não | Não | Entregabilidade ≥ 98% | Confirmar |
| Audit Log | Parcial | Imutabilidade | Sim | Sim | Append-only; LGPD | Confirmar |

---

## §6. Data Ownership Summary

| Bounded Context | Entidade/Tabela Principal | Dono da Escrita | Consumidores | Forma |
|---|---|---|---|---|
| Opportunity Pipeline | opportunities, opportunity_stages, opportunity_partner_commissions | Opportunity Pipeline | Digest, Reporting, Goal&Forecast | API/Evento |
| Account Management | accounts, contacts | Account Management | Opportunity Pipeline, Reporting | API |
| Partner Management | partners | Partner Management | Opportunity Pipeline, Reporting | API |
| Activity Management | activities | Activity Management | Digest, Reporting | API/Evento |
| Goal & Forecast | goals | Goal & Forecast | Digest, Reporting | API |
| Digest | email_digest_logs | Digest | — | — |
| Reporting | (read models) | — (derivado) | Usuários finais | Read Model |
| Organization Management | business_units, users, user_memberships, invitations | Organization Management | Todos os BCs (auth/RBAC) | API |
| Data Migration | migration_jobs, migration_logs | Data Migration | Opportunity Pipeline (escrita após import) | Transacional |
| Identity & Access | (GCP IdP externo) | GCP Identity Platform | Organization Management | OAuth/OIDC |
| Tenancy & Branding | tenants, tenant_branding | Tenancy & Branding | Todos (tenant_id cross-cutting) | API |
| Audit Log | audit_logs | Audit Log | Nenhum (append-only) | Evento |

---

## §7. Deployables Candidatos

| Deployable | Bounded Contexts Incluídos | Tipo | Justificativa |
|---|---|---|---|
| `azim-api` | Opportunity Pipeline, Account Management, Partner Management, Activity Management, Goal & Forecast, Organization Management, Data Migration, Tenancy & Branding, Audit Log | Microservice (.NET 10) | Monólito modular na Fase 1; separação física possível na Fase 2+ conforme escala |
| `azim-digest-worker` | Digest, Notification Delivery | Worker (.NET 10, Cloud Run) | Ciclo de release e carga independente; deve processar antes das 07:00 BRT sem afetar API principal |
| `azim-reporting-worker` | Reporting | Worker (.NET 10) | Read models assíncronos; separado para não afetar latência da API transacional |
| `azim-web` | (Frontend SPA, consome azim-api) | Frontend SPA (React + TypeScript) | Interface única; theming por tenant via CSS variables |
| `azim-ai-service` | AI Intelligence | Microservice (Python / FastAPI) | Fase 3; stack diferente (LangGraph, Langfuse); isolamento de custo e rate-limit por tenant |
| `azim-workflow-worker` | Workflow Automation | Worker (.NET 10) | Fase 2; execução assíncrona obrigatória via Pub/Sub; separado da API síncrona |

---

## §8. Context Map — Resumo de Relações

| Origem | Destino | Padrão DDD | Observação |
|---|---|---|---|
| Opportunity Pipeline | Account Management | Customer/Supplier | Pipeline consome dados de conta; Account é upstream |
| Opportunity Pipeline | Partner Management | Customer/Supplier | Pipeline consome parceiro e percentuais; Partner é upstream |
| Opportunity Pipeline | Activity Management | Customer/Supplier | Pipeline sinaliza estagnação a partir de atividades; Activity é upstream |
| Opportunity Pipeline | Audit Log | Published Language | Pipeline publica eventos de escrita para o AuditLog |
| Digest | Opportunity Pipeline | Customer/Supplier | Digest lê dados de pipeline (opps estagnadas, fechamentos) |
| Digest | Activity Management | Customer/Supplier | Digest lê atividades vencidas e de hoje |
| Digest | Goal & Forecast | Customer/Supplier | Digest inclui bloco de metas no azimute |
| Digest | Notification Delivery | Anti-Corruption Layer | Digest protege modelo interno da instabilidade de providers de e-mail |
| Reporting | Opportunity Pipeline | Read Model | Relatórios consomem read models derivados do pipeline |
| Reporting | Partner Management | Read Model | Relatório de comissões derivado de Partner + Pipeline |
| All BCs | Organization Management | Conformist | Todos dependem de BU, user_id e papéis definidos por Organization |
| All BCs | Tenancy & Branding | Conformist | tenant_id e configurações de tenant são cross-cutting |
| All BCs | Identity & Access | Anti-Corruption Layer | ACL protege o modelo interno do contrato do GCP Identity Platform |
| Workflow Automation | Opportunity Pipeline | Customer/Supplier | Automações reagem a eventos do pipeline; Pipeline é upstream |
| AI Intelligence | Opportunity Pipeline | Customer/Supplier | IA lê dados de pipeline para scoring/resumo; sem escrita |

---

## §9. Módulos Funcionais × Bounded Contexts × Deployables

| Módulo (FRD) | Bounded Context | Deployable |
|---|---|---|
| MOD-01 Autenticação | Identity & Access + Organization Management | azim-api |
| MOD-02 Administração do Tenant | Tenancy & Branding | azim-api |
| MOD-03 BUs, Usuários e Papéis | Organization Management | azim-api |
| MOD-04 Contas e Contatos | Account Management | azim-api |
| MOD-05 Parceiros | Partner Management | azim-api |
| MOD-06 Pipeline e Oportunidades | Opportunity Pipeline | azim-api |
| MOD-07 Atividades e Follow-ups | Activity Management | azim-api |
| MOD-08 Metas e Forecast | Goal & Forecast | azim-api |
| MOD-09 Digest Diário | Digest | azim-digest-worker |
| MOD-10 Notificações In-App | Workflow Automation (Fase 2) | azim-workflow-worker |
| MOD-11 Relatórios | Reporting | azim-reporting-worker |
| MOD-12 Automações Visuais | Workflow Automation | azim-workflow-worker |
| MOD-13 Agentes de IA | AI Intelligence | azim-ai-service |
| MOD-14 Migração de Planilha | Data Migration | azim-api |

---

## §10. Decisões Arquiteturais DDD

| Decisão | Justificativa | Status |
|---|---|---|
| Monólito modular na Fase 1 (azim-api) | Fase 1 tem apenas 1 tenant em produção; fragmentação prematura em microsserviços aumentaria complexidade operacional sem ganho; separação física possível na Fase 2+ via fronteiras de módulo já definidas. | Aprovada |
| Digest como worker separado | Ciclo de processamento fixo (07:00 BRT); carga e criticidade independente da API REST; isolamento garante que falha no digest não afeta operação transacional. | Aprovada |
| Anti-Corruption Layer para Identity Platform | GCP Identity Platform tem contrato externo instável para mudanças; ACL garante que o modelo interno de usuário não vaza o modelo do IdP. | Aprovada |
| Anti-Corruption Layer para Notification Delivery | Provider de e-mail (Postmark/SendGrid) pode mudar (LAC-03 pendente); ACL via IEmailSender protege o Digest do modelo do provider. | Aprovada |
| Reporting como read models assíncronos | Queries de relatório não devem impactar latência da API transacional; read models permitem projeções otimizadas sem joins entre schemas de contextos diferentes. | Aprovada |
| Audit Log como contexto Generic cross-cutting | AuditLog é append-only sem lógica de domínio; todos os contextos de escrita publicam para ele; imutabilidade garantida por design, não por permissão. | Aprovada |

---

## §11. Pontos a Validar

| Código | Ponto | Motivo | Impacto |
|---|---|---|---|
| DDD-VAL-01 | Separação de Activity Management em BC próprio vs módulo de Opportunity Pipeline | Activities têm lifecycle próprio mas estão sempre vinculadas a uma oportunidade ou conta; limiar pode ser módulo, não BC | Fronteira de ownership de dados |
| DDD-VAL-02 | Reporting como BC separado vs módulo técnico dentro do azim-api | Reporting não tem ownership de dados próprio; pode ser apenas uma camada de queries otimizadas sem BC formal | Complexidade desnecessária se mantido como módulo |
| DDD-VAL-03 | Data Migration como BC temporário vs feature dentro do Pipeline | A migração importa diretamente para o modelo do Pipeline; o BC temporário pode ser removido após a Fase 1 | Limpeza arquitetural pós-Fase 1 |
| DDD-VAL-04 | Separação de Tenancy vs Organization Management | Há sobreposição de responsabilidades (slug está em Tenancy, BUs em Organization, mas ambos configurados pelo Tenant Admin) | Pode ser consolidado em um único BC de Tenant Administration |
| DDD-VAL-05 | Workflow Automation consumindo eventos do Pipeline: pull vs push | Se automações reagem a eventos de domínio do Pipeline, o Pipeline deve publicar em Pub/Sub — confirmar antes da Fase 2 | Contrato de evento entre BCs |
