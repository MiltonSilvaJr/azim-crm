# TRD Validation Report — Azim CRM

**Produto:** Azim CRM
**Versão do Relatório:** v1.0
**Data:** 2026-06-11
**Status:** Rascunho para revisão
**Documento Validado:** docs/product/trd/trd.md v1.1 (ajustado durante esta validação)

---

## Controle de Versão

| Versão | Data | Descrição |
|---|---|---|
| v1.0 | 2026-06-11 | Criação inicial do relatório de validação do TRD |

---

## Sumário Executivo

### Parecer Final

**Aprovado com Ressalvas**

### Síntese

O TRD do Azim CRM é tecnicamente sólido, implementável e bem alinhado aos documentos de entrada. A arquitetura monolítica modular com workers especializados está coerente com o DDD, os módulos e os deployables estão mapeados corretamente, e os controles de segurança (RLS em três camadas, Secret Manager, Workload Identity Federation, Cloud Armor, PII mascarada em logs) cobrem os requisitos críticos do NFRD.

Foram encontrados e corrigidos diretamente 5 tipos de ajuste: (1) adição dos eventos `commission.calculated.v1` e `commission.snapshot_created.v1` ao catálogo de eventos e à seção de deployable; (2) endpoint LGPD de anonimização de contato ausente do catálogo de APIs; (3) RF-10 (Notificações In-App, Fase 2) ausente da matriz de rastreabilidade; (4) registro de novo ponto a validar (VAL-TRD-13) sobre ambiguidade arquitetural do digest worker; (5) atualização da versão do TRD.

Os achados não corrigidos são controláveis: o principal risco remanescente é a divergência entre o modelo de acesso do digest worker (Pub/Sub vs. DB polling) descrita em VAL-TRD-13, que requer decisão de engenharia antes da implementação do azim-digest-worker. A constraint `UNIQUE` sem escopo de tenant na coluna `opportunity_number` do data-model.md contradiz ADR-0003 e deve ser corrigida no data model (fora do escopo do TRD Validator).

Todos os 8 ADRs a formalizar estão identificados em §22. Nenhum ADR formal existe no repositório (`docs/product/adr/` ausente), o que não bloqueia o TRD mas deve ser endereçado antes do início da Fase 0.

A divergência de stack de observabilidade (PTV-08 / VAL-TRD-07) está corretamente documentada no TRD e no NFRD, com encaminhamento via ADR-0007.

### Ajustes Aplicados Diretamente no TRD

- ADJ-TRD-001: Adicionados `commission.calculated.v1` e `commission.snapshot_created.v1` à tabela de Eventos Publicados do deployable `azim-api` (§7.2)
- ADJ-TRD-002: Adicionados `commission.calculated.v1` e `commission.snapshot_created.v1` ao Event Catalog (§9.3) com produtor, consumidores, tópico, retenção e criticidade
- ADJ-TRD-003: Adicionado endpoint `DELETE /api/v1/accounts/{accountId}/contacts/{id}` ao catálogo de APIs (§8.4) para cumprimento do direito ao esquecimento (LGPD art. 18 / VAL-TRD-10)
- ADJ-TRD-004: Adicionado RF-10 (Notificações In-App, Fase 2) à Matriz de Rastreabilidade (§20)
- ADJ-TRD-005: Adicionado VAL-TRD-13 à seção de Pontos a Validar (§23) sobre ambiguidade do padrão de acesso do digest worker (Pub/Sub vs. DB polling)
- ADJ-TRD-006: Atualizada tabela de Controle de Versão do TRD (v1.1)

### Principais Riscos Remanescentes

- RISK-TRD remanescente: `opportunity_number UNIQUE` sem escopo de tenant em data-model.md contradiz ADR-0003 (fix em data-model.md, fora do escopo do TRD Validator)
- VAL-TRD-13: Ambiguidade no padrão de acesso do digest worker (Pub/Sub vs. DB polling) — bloqueador de implementação do azim-digest-worker
- VAL-TRD-01/06: Provider de e-mail e pool de conexões não confirmados — impactam go-live
- Nenhum ADR formal criado ainda; 8 ADRs prioritários identificados em §22

### Principais Recomendações

- Formalizar ADR-0001 (Multi-tenancy pooled + RLS) e ADR-0007 (stack de observabilidade) antes do início da Fase 0
- Decidir VAL-TRD-13 (digest worker: Pub/Sub vs. DB poll) antes de iniciar implementação do azim-digest-worker
- Corrigir `opportunity_number UNIQUE` para `UNIQUE(tenant_id, opportunity_number)` em data-model.md
- Resolver VAL-TRD-03 (retenção PII) com jurídico antes do schema freeze
- Executar spike de provider de e-mail (VAL-TRD-01 / ADR-0005) antes de concluir a Fase 0

---

## 1. Documentos Avaliados

| Documento | Caminho | Status |
|---|---|---|
| TRD | docs/product/trd/trd.md | Encontrado |
| PRD | docs/product/prd/prd.md | Encontrado |
| FRD | docs/product/frd-nfrd/frd.md | Encontrado |
| NFRD | docs/product/frd-nfrd/nfrd.md | Encontrado |
| ADRs | docs/product/adr/ | Não encontrado (diretório ausente; 8 ADRs a formalizar identificados no TRD §22) |
| DDD Segmentation | docs/product/ddd/ddd-segmentation.md | Encontrado |
| Context Map | docs/product/ddd/context-map/ | Encontrado |
| Modules README | docs/product/modules/README.md | Encontrado |
| Data Model | docs/product/data-model/data-model.md | Encontrado |
| Glossário | docs/product/glossary/ | Encontrado |
| Diagramas de módulos | docs/product/modules/diagrams/ | Encontrado |
| C4 Containers | docs/product/ddd/diagrams/c4-level-2-containers.md | Encontrado |

---

## 2. Baseline Técnico Esperado

| Código | Item Esperado | Fonte | Deve Aparecer no TRD? | Status |
|---|---|---|---|---|
| BASE-TRD-001 | Monólito modular .NET 10 (azim-api) com módulos alinhados aos BCs | DDD §7, DEC-006 | Sim | Coberto §6, §7.2 |
| BASE-TRD-002 | Multi-tenancy pooled + RLS em 3 camadas | DEC-006, NFR-SEG-01 | Sim | Coberto §12.4 |
| BASE-TRD-003 | GCP southamerica-east1, 100% Terraform | PRD §5.1 | Sim | Coberto §16.1 |
| BASE-TRD-004 | Digest worker separado + Cloud Scheduler + Pub/Sub | DEC-009, NFR-RES-01 | Sim | Coberto §7.3, §9.6 |
| BASE-TRD-005 | Snapshot imutável de comissão ao fechar Ganho | DEC-002, RN-007 | Sim | Coberto §7.2, §10.2 |
| BASE-TRD-006 | Numeração sequencial atômica por tenant (AZ-NNNN) | DEC-001, ADR-0003 | Sim | Coberto §4.5 (ADR-0003) |
| BASE-TRD-007 | IEmailSender como ACL de e-mail | DEC-008 | Sim | Coberto §7.3, §11.3 |
| BASE-TRD-008 | CI/CD via GitHub Actions + Workload Identity Federation | PRD §5.1 | Sim | Coberto §17.1 |
| BASE-TRD-009 | Observabilidade Cloud Logging/Monitoring/Trace com tenant_id | NFR-OBS-01..05 | Sim | Coberto §14 |
| BASE-TRD-010 | Outbox pattern + idempotência de consumers | NFR-RES-01/02 | Sim | Coberto §9.1, §9.5 |
| BASE-TRD-011 | Migrations EF Core como job pré-tráfego | NFR-DISP-04 | Sim | Coberto §16.3 |
| BASE-TRD-012 | Serviço AI (azim-ai-service) Python/FastAPI/LangGraph — Fase 3 | PRD RF-13 | Sim | Coberto §7.7 |
| BASE-TRD-013 | Valores monetários como centavos inteiros (BIGINT) | DEC-011 | Sim | Coberto §10.1, schemas SQL |
| BASE-TRD-014 | GCP Secret Manager como única fonte de segredos | DEC-005, NFR-SEG-05 | Sim | Coberto §12.6 |
| BASE-TRD-015 | LGPD: PII mascarada em logs, retenção controlada, direito ao esquecimento | LGPD, NFR-PRIV-01 | Sim | Coberto §13 (FIND-TRD-001: endpoint LGPD ausente, corrigido por ADJ-TRD-003) |
| BASE-TRD-016 | Cloud Armor (WAF) + rate limit antes do go-live | NFR-SEG-06 | Sim | Coberto §12.7, §16.2 |
| BASE-TRD-017 | 15 bounded contexts mapeados em módulos e deployables | DDD §2 | Sim | Coberto §4.6, §7 |
| BASE-TRD-018 | Catálogo de eventos de domínio versionados | DDD Event Storming, Modules README | Sim | Coberto §9.3 (ADJ-TRD-001/002: eventos de comissão adicionados) |

---

## 3. Validação da Estrutura do TRD

| Seção | Presente? | Status | Ação Aplicada |
|---|---|---|---|
| Introdução (§1) | Sim | OK | — |
| Objetivo do Documento (§2) | Sim | OK | — |
| Referências (§3) | Sim | OK | — |
| Consolidação Técnica dos Insumos (§4) | Sim | OK | — |
| Visão Técnica da Solução (§5) | Sim | OK | — |
| Estilo Arquitetural (§6) | Sim | OK | — |
| Módulos e Deployables (§7) | Sim | OK | ADJ-TRD-001: commission events adicionados à lista de eventos publicados do azim-api |
| Arquitetura de APIs (§8) | Sim | Corrigido | ADJ-TRD-003: endpoint LGPD de anonimização de contato adicionado |
| Arquitetura de Eventos e Mensageria (§9) | Sim | Corrigido | ADJ-TRD-002: commission.calculated.v1 e commission.snapshot_created.v1 adicionados ao Event Catalog |
| Arquitetura de Dados (§10) | Sim | OK | — |
| Arquitetura de Integração (§11) | Sim | OK | — |
| Segurança Técnica (§12) | Sim | OK | — |
| Compliance e Privacidade (§13) | Sim | OK | — |
| Observabilidade (§14) | Sim | OK | Divergência PTV-08 corretamente documentada em VAL-TRD-07 |
| Resiliência, Performance e Escalabilidade (§15) | Sim | OK | — |
| Ambientes, Deploy e Configuração (§16) | Sim | OK | — |
| CI/CD e Qualidade Técnica (§17) | Sim | OK | — |
| Operação e Suporte (§18) | Sim | OK | — |
| Diagramas Técnicos (§19) | Sim | Parcial | FIND-TRD-003: Deployment Diagram e Data Flow Diagram ausentes do §19; cobertos parcialmente por referências externas |
| Matriz de Rastreabilidade (§20) | Sim | Corrigido | ADJ-TRD-004: RF-10 adicionado |
| Riscos Técnicos (§21) | Sim | OK | — |
| ADRs a Formalizar (§22) | Sim | OK | — |
| Pontos a Validar (§23) | Sim | Corrigido | ADJ-TRD-005: VAL-TRD-13 adicionado |

---

## 4. Cobertura PRD → TRD

| Item PRD | Descrição | Seção TRD | Status |
|---|---|---|---|
| OBJ-01 | Eliminar planilha; migrar 108 oportunidades | §7.2 (data-migration), §8.4 (API migração) | Coberto |
| OBJ-02 | Comissão de parceiro rastreável e auditável | §7.2, §9.3 (commission events), §10.2 | Coberto |
| OBJ-03 | Digest diário às 07:00 BRT automatizado | §7.3, §9.3/9.6, §15 | Coberto |
| OBJ-04 | Visibilidade de forecast e metas | §7.2 (goal-forecast), §8.4 (API forecast) | Coberto |
| OBJ-05 | Multi-tenant verificado em CI | §12.4 (RLS), §17.1 (CI gate KPI-06) | Coberto |
| DEC-001 | Estágios configuráveis por BU | §10.2 (tabela stages), §8.4 | Coberto |
| DEC-002 | Snapshot imutável de comissão | §7.2, §10.2, §9.3 (commission.snapshot_created.v1) | Coberto |
| DEC-003 | Metas mensais; agregações derivadas | §7.2 (goal-forecast), §8.4 | Coberto |
| DEC-004 | White-label estrito | §7.2 (tenant-administration), §13.1 | Coberto |
| DEC-005 | GCP Identity Platform; Secret Manager | §12.2, §12.6, §16.4 | Coberto |
| DEC-006 | Multi-tenancy pooled + RLS | §6, §12.4, §10.1 | Coberto |
| DEC-007 | Lead como estágio, não entidade separada | §4.5 (DEC-007: sem tabela leads) | Coberto |
| DEC-008 | IEmailSender / Postmark primário | §7.3, §11.1 | Coberto |
| DEC-009 | Cloud Scheduler UTC + fuso IANA | §9.6, §7.3 | Coberto |
| DEC-010 | Migração: dry-run → triagem → import | §7.2 (data-migration), §8.4 | Coberto |
| DEC-011 | Valores em centavos inteiros (BIGINT) | §10.1, schemas SQL | Coberto |
| DEC-012 | Parceiro sem login no MVP | §4.5 (DEC-012), §12.2 | Coberto |
| DEC-013 | Billing SaaS fora do escopo | §4.5 (DEC-013) | Coberto |

---

## 5. Cobertura FRD → TRD

| Item FRD | Descrição | Tratamento no TRD | Status |
|---|---|---|---|
| RF-01 | Autenticação GCP IdP | §7.2 (authentication), §8.4 (auth endpoints), §12.2 | Coberto |
| RF-02 | White-label estrito | §7.2 (tenant-administration), §8.4, §13.1 | Coberto |
| RF-03 | BUs, usuários, RBAC por BU | §7.2 (organization), §8.4, §12.3 | Coberto |
| RF-04 | Contas e contatos (dedupe, 360°) | §7.2 (account-management), §8.4, §13 | Coberto |
| RF-05 | Parceiros e comissão | §7.2 (partner-management), §8.4 | Coberto |
| RF-06 | Pipeline Kanban; comissão nativa; owner obrigatório | §7.2 (opportunity-pipeline), §8.4, §10.2 | Coberto |
| RF-07 | Atividades e follow-ups | §7.2 (activity-management), §8.4, §9.3 | Coberto |
| RF-08 | Metas e forecast; graceful degradation | §7.2 (goal-forecast), §8.4, §15.4 | Coberto |
| RF-09 | Digest diário por e-mail | §7.3, §9.3/9.6, §14.3, §15.5 | Coberto |
| RF-10 | Notificações In-App (Fase 2) | §7.6 (azim-workflow-worker); VAL-MOD-01 | Coberto (Fase 2) — adicionado à matriz por ADJ-TRD-004 |
| RF-11 | Relatórios enxutos com export CSV | §7.4 (reporting-worker), §8.4, §10.4 | Coberto |
| RF-12 | Automações visuais (Fase 2) | §7.6 (workflow-worker), §9.3 | Coberto (Fase 2) |
| RF-13 | Agentes de IA (Fase 3) | §7.7 (azim-ai-service), §8.4 (AI APIs) | Coberto (Fase 3) |
| FRD-pipeline-06 | Fechar como Ganho: snapshot imutável | §7.2, §10.2, §9.3 (commission.snapshot_created.v1) | Coberto |
| FRD-activity-04 | Sugerir próxima atividade | §8.4 (PATCH /activities/{id}/complete) | Parcial (endpoint cobre conclusão; sugestão implícita no payload de resposta — sem endpoint separado) |

---

## 6. Cobertura NFRD → TRD

| Item NFRD | Categoria | Tratamento no TRD | Status |
|---|---|---|---|
| NFR-PERF-01 | Performance — API geral p95 ≤ 300 ms | §15.1, §14.3 (métrica http_request_duration_seconds) | Coberto |
| NFR-PERF-02 | Performance — Kanban p95 ≤ 2 s | §15.1, §10.1 (índice composto), §14.3 | Coberto |
| NFR-PERF-05 | Performance — Digest ≤ 5 min | §7.3, §15.1, §14.3 (métrica digest_processing_duration_seconds) | Coberto |
| NFR-PERF-06 | Performance — Import planilha ≤ 5 min | §7.2 (data-migration), §8.4 (dry-run/import) | Coberto |
| NFR-PERF-07 | Performance — Relatórios p95 ≤ 3 s | §15.1, §10.4 (read models) | Coberto |
| NFR-DISP-01 | Disponibilidade — Tier 1 ≥ 99,5% | §15.3, §14.5 (health checks), §14.6 (alertas) | Coberto |
| NFR-DISP-04 | Janela de manutenção fora de 06:30–09:00 BRT | §16.3 (strategy), §17.1 (pipeline) | Coberto |
| NFR-ESC-01 | Escalabilidade — 50 tenants sem retrabalho | §15.2, §12.4, VAL-TRD-06 | Coberto (pool de conexões pendente — VAL-TRD-06) |
| NFR-SEG-01 | Segurança — zero vazamento entre tenants | §12.4, §17.1 (CI gate KPI-06) | Coberto |
| NFR-SEG-05 | Segredos apenas via Secret Manager | §12.6, §16.4 | Coberto |
| NFR-SEG-06 | Cloud Armor + rate limit | §12.7, §16.2 | Coberto (threshold pendente — VAL-TRD-04) |
| NFR-RES-01 | Execução assíncrona obrigatória | §7.3, §9.1, §15.4 | Coberto |
| NFR-RES-02 | Idempotência de eventos | §9.5 (outbox), §15.5 | Coberto |
| NFR-OBS-01..05 | Observabilidade Cloud Logging/Monitoring/Trace | §14 | Coberto; divergência PTV-08 documentada em VAL-TRD-07 |
| NFR-PRIV-01 | PII ausente de logs | §13.2, §13.4, §14.2 | Coberto |
| NFR-COMP-01 | LGPD — PII de contatos | §13, §12.3 (RBAC+RLS) | Coberto (endpoint anonimização adicionado por ADJ-TRD-003; retenção pendente VAL-TRD-03) |
| NFR-AUD-01..03 | Auditoria imutável (AuditLog) | §7.2 (audit-log), §10.2, §13.4, §18.3 | Coberto |
| NFR-MAN-01 | Cobertura de testes por camada | §17.2 (gates de qualidade) | Coberto |
| NFR-OPS-01..04 | Operabilidade — runbooks, alertas | §18.1, §14.6 | Coberto |
| NFR-USA-01..04 | Usabilidade / WCAG 2.1 AA | §13.1 (WCAG mencionado), §12.7 (CSP frontend) | Parcial — FIND-TRD-006: TRD menciona WCAG apenas para validação de cores de branding; requisitos de acessibilidade do componente frontend não detalhados |
| NFR-POR-01..02 | Portabilidade — containers multi-arch | §17.3 (Docker multi-arch, Alpine) | Coberto |

---

## 7. Validação ADR → TRD

| ADR | Decisão | TRD Alinhado? | Status |
|---|---|---|---|
| ADR-0001 (a formalizar) | Multi-tenancy pooled + RLS | Sim — §12.4, §10.1, §6 | OK |
| ADR-0002 (a formalizar) | Snapshot imutável de comissão | Sim — §7.2, §10.2 | OK |
| ADR-0003 (a formalizar) | Numeração sequencial por tenant | Sim — §4.5 (DEC-001), §22 (ADR-0003) | OK (ver FIND-TRD-002 sobre data-model.md) |
| ADR-0004 (a formalizar) | Outbox pattern + idempotência | Sim — §9.1, §9.5, §15.5 | OK |
| ADR-0005 (a formalizar) | Provider de e-mail (spike pendente) | Parcial — §11.1 lista Postmark/SendGrid; VAL-TRD-01 registrado | Ponto a Validar |
| ADR-0006 (a formalizar) | Token digest: expiração | Parcial — §10.5 define 48h; VAL-TRD-05 registrado | Ponto a Validar |
| ADR-0007 (a formalizar) | Stack de observabilidade GCP vs Prometheus | Parcial — §14 adota stack GCP; VAL-TRD-07 registrado; divergência PTV-08 documentada | Ponto a Validar |
| ADR-0008 (a formalizar) | Scheduling digest: Cloud Scheduler UTC + fuso IANA | Sim — §9.6, §7.3 | OK |

**Nota:** nenhum ADR formal existe no repositório (`docs/product/adr/` ausente). O diretório e os 8 documentos ADR devem ser criados antes do início da Fase 0 (ADR-0001 e ADR-0007 são críticos).

---

## 8. Validação DDD / Modules → TRD

| Item | Tipo | Fonte | Tratamento no TRD | Status |
|---|---|---|---|---|
| BC-01 Opportunity Pipeline (Core Domain) | Bounded Context | DDD | §7.2, §8.4, §9.3, §10.2 | Coberto |
| BC-02 Account Management | Bounded Context | DDD | §7.2, §8.4, §13 | Coberto |
| BC-03 Partner Management | Bounded Context | DDD | §7.2, §8.4 | Coberto |
| BC-04 Activity Management | Bounded Context | DDD | §7.2, §8.4, §9.3 | Coberto |
| BC-05 Goal & Forecast | Bounded Context | DDD | §7.2, §8.4 | Coberto |
| BC-06 Digest | Bounded Context | DDD | §7.3, §9.3/9.6 | Coberto |
| BC-07 Reporting | Bounded Context | DDD | §7.4, §8.4, §10.4 | Coberto |
| BC-08 Organization Management | Bounded Context | DDD | §7.2 (organization), §8.4 | Coberto |
| BC-09 Data Migration | Bounded Context | DDD | §7.2 (temporário), §8.4 | Coberto |
| BC-10 Workflow Automation | Bounded Context | DDD | §7.6 (Fase 2) | Coberto (Fase 2) |
| BC-11 AI Intelligence | Bounded Context | DDD | §7.7 (Fase 3) | Coberto (Fase 3) |
| BC-12 Identity & Access | Bounded Context | DDD | §7.2 (authentication), §12.2 | Coberto |
| BC-13 Tenancy & Branding | Bounded Context | DDD | §7.2 (tenant-administration), §13 | Coberto |
| BC-14 Notification Delivery | Bounded Context | DDD | §7.3 (IEmailSender ACL), §11.3 | Coberto |
| BC-15 Audit Log | Bounded Context | DDD | §7.2, §10.2, §13.4 | Coberto |
| commission.calculated.v1 | Evento de Domínio | Modules README §7 | Ausente — adicionado por ADJ-TRD-001/002 | Corrigido |
| commission.snapshot_created.v1 | Evento de Domínio | Modules README §7 | Ausente — adicionado por ADJ-TRD-001/002 | Corrigido |
| Digest worker: consumo de eventos vs. DB polling | Padrão de acesso | Modules README §7 vs. TRD §19.3 | Ambiguidade — VAL-TRD-13 registrado | Ponto a Validar |
| notification-delivery deployable split | Módulo compartilhado | Modules README §3 | Corretamente duplicado em §4.6 (azim-digest-worker / azim-api) | OK |
| reporting Fase 1/2 deployment | Deployable opcional | Modules README §9 | §7.1 Fase 1/2; VAL-TRD-02 registrado | Ponto a Validar |

---

## 9. Validação da Arquitetura Técnica

| Critério | Status | Problema | Ação |
|---|---|---|---|
| Coesão | OK | Módulos com responsabilidade clara por bounded context | — |
| Acoplamento | OK | Módulos do monólito se comunicam por interfaces internas; sem HTTP inter-módulo na Fase 1 | — |
| Evolução | OK | Fronteiras de módulo permitem extração futura; Fase 2/3 incrementais | — |
| Resiliência | OK | Outbox pattern, retry exponencial, circuit breaker, DLQ, idempotência cobertos | — |
| Segurança | OK | RLS 3 camadas, WAF, Secret Manager, WIF, TLS everywhere | — |
| Observabilidade | OK | Logs estruturados com tenant_id, métricas, traces, health checks, alertas | Divergência PTV-08 corretamente rastreada |
| Compliance | Parcial | LGPD coberto; endpoint anonimização adicionado; retenção PII pendente VAL-TRD-03 | ADJ-TRD-003 aplicado |
| Deploy | OK | Cloud Run rolling update; migrations pré-tráfego; ambientes dev/stg/prd | — |
| Testabilidade | OK | Tenant isolation tests como gate CI; RBAC matrix tests; contract tests | — |
| Operação | OK | Runbooks iniciais em §18.1; suporte N1/N2/N3 definido; auditoria operacional | — |

---

## 10. Validação de APIs

| API | Produtor | Consumidor | Problema | Status |
|---|---|---|---|---|
| Endpoints de autenticação (/auth/*) | azim-api | azim-web, workers | Completos; idempotência e JWT cobertos | OK |
| Endpoints de tenant-administration | azim-api | azim-web, PlatformOperator | Completos; slug imutável referenciado | OK |
| Endpoints de organization | azim-api | azim-web | Completos; RBAC por endpoint definido | OK |
| Endpoints de account-management | azim-api | azim-web | Endpoint LGPD (DELETE contact) ausente — corrigido | Corrigido |
| Endpoints de opportunity-pipeline | azim-api | azim-web | Completos; owner obrigatório, comissão, reabrir cobertos | OK |
| Endpoints de activity-management | azim-api | azim-web | Completos; idempotência de conclusão coberta | OK |
| Endpoints de goal-forecast | azim-api | azim-web | Completos | OK |
| Endpoints de reporting | azim-api / azim-reporting-worker | azim-web | Completos; signed URL GCS para CSV | OK |
| Endpoints de audit-log | azim-api | azim-web (TenantAdmin) | Completos; somente leitura | OK |
| Endpoints de data-migration | azim-api | azim-web (TenantAdmin) | Completos; dry-run e import transacional | OK |
| Endpoints AI (/ai/*) | azim-ai-service | azim-web, azim-api | Fase 3; completos para o escopo | OK (Fase 3) |
| Digest action endpoint (/digest/actions/{token}) | azim-api | azim-web (link e-mail) | Público com token signed; idempotência coberta | OK |
| Endpoint backoffice (/platform/tenants) | azim-api | PlatformOperator | PlatformOperator isolado; sem acesso a dados de negócio | OK |
| Versionamento (/api/v1/) | azim-api | Todos | Prefixo obrigatório; breaking changes exigem nova versão | OK |
| Padrão de erro (correlation_id) | azim-api | Todos | Envelope padronizado com error_code e correlation_id | OK |

---

## 11. Validação de Eventos e Mensageria

| Evento | Produtor | Consumidores | Problema | Status |
|---|---|---|---|---|
| opportunity.created.v1 | azim-api | audit-log, reporting, workflow (F2) | OK | OK |
| opportunity.stage_changed.v1 | azim-api | audit-log, digest, reporting, workflow (F2) | OK | OK |
| opportunity.won.v1 | azim-api | audit-log, reporting, digest | OK | OK |
| opportunity.lost.v1 | azim-api | audit-log, reporting | OK | OK |
| opportunity.stale.v1 | azim-api | digest, workflow (F2) | Ambiguidade: digest consome via Pub/Sub (§9.3) ou por DB poll (§19.3)? — VAL-TRD-13 | Ponto a Validar |
| opportunity.reopened.v1 | azim-api | audit-log, reporting | OK | OK |
| commission.calculated.v1 | azim-api | reporting | Ausente do catálogo original — adicionado por ADJ-TRD-002 | Corrigido |
| commission.snapshot_created.v1 | azim-api | audit-log, reporting | Ausente do catálogo original — adicionado por ADJ-TRD-002 | Corrigido |
| activity.created.v1 | azim-api | digest, audit-log | OK | OK |
| activity.completed.v1 | azim-api | digest, audit-log, reporting | OK | OK |
| activity.overdue.v1 | azim-api | digest | Mesma ambiguidade de opportunity.stale.v1 — VAL-TRD-13 | Ponto a Validar |
| goal.updated.v1 | azim-api | digest, reporting | OK | OK |
| tenant.branding_changed.v1 | azim-api | azim-web (CDN) | OK | OK |
| digest.email_sent.v1 | azim-digest-worker | KPI dashboard | OK; retenção 90 dias diferenciada | OK |
| Versionamento (.v1) | Todos | Todos | Sufixo .v1 consistente; breaking changes requerem .v2 | OK |
| Outbox pattern | azim-api | Pub/Sub publisher | Tabela outbox_events com status pending/published/failed | OK |
| DLQ | Todos os tópicos | Operação | azim-{topic}-dlq com alerta imediato após 5 tentativas | OK |
| Correlação e tenant_id | Todos | Todos | Envelope padrão com correlation_id e tenant_id obrigatórios | OK |

---

## 12. Validação da Arquitetura de Dados

| Item | Problema | Status |
|---|---|---|
| Multi-tenancy pooled + RLS | Dois mecanismos independentes (EF Core + RLS Postgres); testes em CI como gate | OK |
| Ownership de dados | Cada módulo é único dono de escrita; consumo via API, evento ou read model | OK |
| Joins diretos entre contextos | Proibidos; read models e views projetadas são o padrão | OK |
| Valores monetários (BIGINT) | DEC-011 aplicado; valor_setup, valor_mensal, valor_total, comissao_calculada como BIGINT | OK |
| Percentuais de comissão (NUMERIC) | pct_setup e pct_recorrente como NUMERIC(5,2) — correto para percentuais; não é valor monetário | OK |
| opportunity_number UNIQUE constraint | data-model.md define UNIQUE global em opportunity_number; ADR-0003 exige UNIQUE(tenant_id, opportunity_number) — inconsistência em data-model.md, fora do escopo de correção do TRD Validator | FIND-TRD-002 |
| Snapshot imutável (opportunity_partner_commissions) | is_snapshot=true; constraint por policy/trigger; append-only | OK |
| audit_logs append-only | Sem UPDATE/DELETE; trigger de imutabilidade; delta_json com PII mascarada | OK |
| PII em contacts e users | Mascarada em logs; acesso restrito por RBAC+RLS | OK |
| Retenção de PII | audit_logs e contacts: pendente definição jurídica (VAL-TRD-03) | Ponto a Validar |
| email_digest_logs (idempotência) | Retenção 90 dias; chave (tenant_id, user_id, digest_date) | OK |
| outbox_events | Retenção 7 dias; purge batch diário de published | OK |
| Read models | KanbanView, FunnelReport, CommissionReport, ForecastView, StagnationView, Account360View definidos | OK |
| Cache Redis (memberships) | Sem PII; TTL configurável | OK |
| GCS (branding + CSV) | Buckets por ambiente; signed URLs temporárias | OK |

---

## 13. Validação de Segurança, Privacidade e Compliance

| Área | Problema | Status |
|---|---|---|
| Autenticação (usuário) | JWT Bearer via GCP Identity Platform; nenhuma senha no banco | OK |
| Autenticação (serviço) | OIDC token WIF para Cloud Scheduler → worker e worker → azim-api | OK |
| Autorização (RBAC) | 5 papéis (PlatformOperator, TenantAdmin, GestorBU, Vendedor, Viewer); verificação em 3 camadas (middleware, service, RLS) | OK |
| Isolamento de tenant (RLS) | 3 camadas independentes; testes CI obrigatórios; alerta sev-1 para violação | OK |
| Criptografia em trânsito | TLS 1.2+ em todas as conexões (borda, Cloud Run → Cloud SQL, Cloud Run → Redis) | OK |
| Criptografia em repouso | AES-256 GCP managed em Cloud SQL, Memorystore, Cloud Storage | OK |
| Criptografia em nível de campo (PII) | Sem criptografia de campo no MVP; encryption at rest como nível base — VAL-TRD-09 | Ponto a Validar |
| Gestão de segredos | Apenas GCP Secret Manager; injeção via Terraform; zero segredo em código/imagem | OK |
| WAF e rate limit de borda | Cloud Armor ativo antes do go-live; threshold por IP — VAL-TRD-04 | Parcial |
| PII em logs | Serilog destructuring; mascaramento antes de gravar; scan de logs em CI | OK |
| LGPD — direito ao esquecimento | Endpoint de anonimização/exclusão de contato ausente — corrigido por ADJ-TRD-003 | Corrigido |
| LGPD — base legal | Contrato de tratamento com tenant (LAC-07); aceite no onboarding | OK |
| LGPD — retenção PII | A definir com jurídico (VAL-TRD-03) | Ponto a Validar |
| WCAG 2.1 AA | Validação de contraste no upload de cores; componentes acessíveis (frontend) | Parcial — FIND-TRD-006 |
| PCI DSS | Não aplicável — Azim CRM não processa dados de cartão | OK |
| Scan de CVE (Trivy) | Critical/High com fix bloqueiam build CI | OK |
| Secret scan (Gitleaks) | Gate bloqueante no CI | OK |
| SAST (CodeQL) | Gate no CI | OK |
| CSP frontend | Content-Security-Policy sem unsafe-inline/unsafe-eval | OK |

---

## 14. Validação de Observabilidade e Operação

| Item | Problema | Status |
|---|---|---|
| Logs estruturados JSON | Serilog + Cloud Logging; campos obrigatórios: timestamp, level, service_name, correlation_id, tenant_id | OK |
| PII ausente de logs | Serilog destructuring para mascarar campos PII | OK |
| Métricas | 9 métricas definidas com SLOs associados; inclui tenant_rls_violation_count | OK |
| Tracing | Cloud Trace; correlation_id propagado entre azim-api e workers; Langfuse para IA (Fase 3) | OK |
| Health Checks | Liveness e Readiness definidos para todos os deployables; dependências verificadas | OK |
| Alertas | 7 alertas com condição, severidade e ação esperada; sev-1 para violação de RLS | OK |
| Divergência PTV-08 | Regras .forge referenciam Prometheus/Loki/Jaeger; stack aprovada é GCP — VAL-TRD-07 | Ponto a Validar — ADR-0007 a formalizar |
| Runbooks iniciais | 7 cenários cobertos (digest falho, Pub/Sub acumulando, 401 em massa, alta latência Kanban, violação RLS, deploy com regressão, tenant admin bloqueado) | OK |
| Auditoria operacional | Cloud Logging (30 dias), audit_logs (a definir), GitHub Actions (90 dias), Terraform state | OK |
| Suporte N1/N2/N3 | Definido com responsabilidades claras | OK |

---

## 15. Validação de Resiliência, Performance e Escalabilidade

| Item | Problema | Status |
|---|---|---|
| Performance — API geral | p95 ≤ 300 ms / p99 ≤ 800 ms a 50 RPS | OK |
| Performance — Kanban | p95 ≤ 2 s com 2.000 oportunidades; índice composto obrigatório | OK |
| Performance — Digest | ≤ 5 min para 1.000 destinatários | OK |
| Escalabilidade — tenants | 50 tenants sem retrabalho; pool de conexões a dimensionar (VAL-TRD-06) | Parcial |
| Escalabilidade — pico | 200 RPS sem degradação; Cloud Run autoscaling | OK |
| Disponibilidade por tier | Tier 1 ≥ 99,5%; Tier 2 ≥ 99%; Tier 3 ≥ 98% | OK |
| RPO/RTO | RPO ≤ 5 min (PITR); RTO ≤ 4 h | OK |
| Idempotência | Digest por (tenant_id, user_id, date); events por event_id; API por Idempotency-Key | OK |
| Outbox / at-least-once | Publisher background; consumer verifica event_id; DLQ após 5 tentativas | OK |
| Retries | Backoff exponencial 1s/5s/30s/2min/10min; 5 tentativas máximas | OK |
| Circuit Breaker | Digest worker → Postmark; azim-api → Redis (fallback para DB) | OK |
| Timeouts | Definidos por integração (Cloud SQL 30s, Postmark 10s, Pub/Sub 5s, Redis 2s, Vertex 30s) | OK |
| Graceful degradation | Sem metas: painel exibe realizado sem erro; bloco de metas omitido no digest | OK |
| Deploy sem downtime | Rolling update Cloud Run; migration pré-tráfego; fora de 06:30–09:00 BRT | OK |

---

## 16. Validação dos Diagramas Técnicos

| Diagrama | Presente? | Qualidade | Status |
|---|---|---|---|
| Architecture Overview (C4 L1) — §19.1 | Sim | Boa — Mermaid válido; cobre todos os componentes principais | OK |
| Container Diagram (C4 L2) — §19.2 | Sim | Boa — Mermaid válido; módulos internos do azim-api visíveis | OK |
| Event Flow — Digest Diário — §19.3 | Sim | Boa — Sequence diagram válido; cobre idempotência e envio | OK |
| Security Boundary — §19.4 | Sim | Boa — Mermaid válido; zonas pública/borda/app/dados/segredos | OK |
| Outbox Pattern — §19.5 | Sim | Boa — Sequence diagram válido; cobre transação, publicação e ack | OK |
| Deployment Diagram | Não (§19) | — | FIND-TRD-003: ausente do §19; topologia GCP descrita em texto em §16.2 |
| Data Flow Diagram | Não (§19) | — | FIND-TRD-003: ausente do §19; parcialmente coberto por references externas (modules/diagrams/) |
| Integration Flow Diagram | Não (§19) | Parcial | Referenciado externamente (modules/diagrams/integration-flows.md) — OK para o MVP |
| Compliance Flow Diagram (LGPD) | Não (§19) | Parcial | Referenciado externamente (modules/diagrams/compliance-lgpd.md) — OK para o MVP |
| Component Diagram (módulos críticos) | Não (§19) | Parcial | C4 L3 existe em docs/product/ddd/diagrams/c4-level-3-components.md — referenciável |

---

## 17. Ajustes Aplicados no TRD

| ID | Seção do TRD | Tipo de Ajuste | Descrição do Ajuste | Fonte Utilizada |
|---|---|---|---|---|
| ADJ-TRD-001 | §7.2 — Deployable azim-api, Eventos Publicados | Conteúdo — evento ausente | Adicionados `commission.calculated.v1` (consumer: reporting) e `commission.snapshot_created.v1` (consumers: audit-log, reporting) à tabela de eventos publicados | Modules README §7 (opportunity-pipeline publica CommissionCalculated e CommissionSnapshotCreated) |
| ADJ-TRD-002 | §9.3 — Event Catalog | Conteúdo — evento ausente | Adicionadas duas linhas ao Event Catalog: `commission.calculated.v1` (tópico azim-opportunities, 7 dias, Média) e `commission.snapshot_created.v1` (tópico azim-opportunities, 7 dias, Alta) | Modules README §7; DDD Event Storming §2 (CommissionCalculated, CommissionSnapshotCreated) |
| ADJ-TRD-003 | §8.4 — Catálogo de APIs, account-management | Conteúdo — endpoint ausente | Adicionado `DELETE /api/v1/accounts/{accountId}/contacts/{id}` (TenantAdmin) para anonimização/exclusão de contato por direito ao esquecimento LGPD art. 18 | LGPD art. 18; NFRD NFR-COMP-01; §13.4 (Controles de Compliance); VAL-TRD-10 |
| ADJ-TRD-004 | §20 — Matriz de Rastreabilidade | Rastreabilidade — item ausente | Adicionada linha FRD RF-10 (Notificações In-App, Fase 2) com referência a §7.6 (azim-workflow-worker) e VAL-MOD-01 | FRD §10 (FRD-notif-01, RF-10, Fase 2) |
| ADJ-TRD-005 | §23 — Pontos a Validar | Pendência arquitetural — VAL novo | Adicionado VAL-TRD-13: ambiguidade no padrão de acesso do digest worker (consumo de Pub/Sub per §7.3/§9.3 vs. DB polling per §19.3) | TRD §7.3 vs. §19.3; Modules README §7 (digest consome OpportunityStale, ActivityOverdue) |
| ADJ-TRD-006 | Controle de Versão | Estrutura — versão | Atualizada tabela de versão de v1.0 para v1.1 com resumo dos ajustes | — |

---

## 18. Achados Não Corrigidos

| ID | Severidade | Seção | Problema | Motivo de não correção | Recomendação |
|---|---|---|---|---|---|
| FIND-TRD-001 | Alta | data-model.md (externo ao TRD) | `opportunity_number VARCHAR(10) NOT NULL UNIQUE` em data-model.md é uma constraint global; ADR-0003 (§22) especifica que a unicidade deve ser por tenant `UNIQUE(tenant_id, opportunity_number)`. Se dois tenants gerarem AZ-0001, haverá conflito no banco. | data-model.md está fora do escopo de correção do TRD Validator | Corrigir constraint em data-model.md para `UNIQUE(tenant_id, opportunity_number)` antes do schema freeze |
| FIND-TRD-002 | Média | §7.3, §9.3 vs. §19.3 | O azim-digest-worker aparece como consumer de `opportunity.stale.v1` e `activity.overdue.v1` via Pub/Sub (§7.3 "Eventos Consumidos" e §9.3 Event Catalog), mas o fluxo de sequência (§19.3) mostra o worker fazendo apenas queries no DB sem consumo de Pub/Sub. A ambiguidade impacta a topologia de assinaturas do digest worker. | Requer decisão arquitetural sobre padrão de acesso (Pub/Sub vs. DB polling) | Registrado como VAL-TRD-13; definir antes da implementação do azim-digest-worker |
| FIND-TRD-003 | Média | §19 | Deployment Diagram e Data Flow Diagram ausentes da seção §19. A topologia GCP está descrita em texto em §16.2 e os fluxos de integração/compliance estão em documentos externos (modules/diagrams/). | Criação dos diagramas exige decisões sobre topologia de rede VPC e configuração GCP não totalmente especificadas | Criar Deployment Diagram em §19.6 antes da Fase 0 com base na topologia de §16.2 |
| FIND-TRD-004 | Média | §20 | Matriz de Rastreabilidade cobre seletivamente os NFRs (9 de 59 itens NFRD explicitados). Não cobre NFR-DISP-01..04, NFR-ESC-01..04, NFR-PERF-03..07, NFR-AUD-01..03, NFR-OPS-01..04, NFR-INT-01..03. | Expandir a matriz para 59 NFRs aumentaria o volume sem benefício proporcional; itens cobertos são os críticos | Considerar documento separado trd-traceability-report.md se rastreabilidade NFRD completa for exigida por auditoria |
| FIND-TRD-005 | Média | §22 | Diretório `docs/product/adr/` ausente; nenhum ADR formal criado. 8 ADRs identificados como prioritários, com ADR-0001 e ADR-0007 marcados como críticos (Fase 0). | Criar ADRs é tarefa de produto/arquitetura, não correção derivável do TRD | Criar `docs/product/adr/` e formalizar ADR-0001 e ADR-0007 antes do início da Fase 0 |
| FIND-TRD-006 | Baixa | §13.1 | NFR-USA-01..04 (usabilidade e acessibilidade WCAG 2.1 AA) é apenas parcialmente coberto: TRD menciona WCAG para validação de cores de branding mas não define requisitos de acessibilidade de componentes frontend (biblioteca de componentes acessível, testes axe-core, etc.). | Decisão de biblioteca de componentes frontend não está nos insumos | Definir requisitos de acessibilidade de componentes frontend e incluir gate de acessibilidade no CI (ex.: axe-core) |

---

## 19. Conflitos Arquiteturais

Nenhum conflito arquitetural identificado entre os documentos de entrada. As divergências encontradas (PTV-08 / stack de observabilidade) foram registradas como pontos a validar e encaminhadas via ADR-0007.

---

## 20. Pontos a Validar

| Código | Ponto | Origem | Impacto | Recomendação |
|---|---|---|---|---|
| VAL-TRD-01 | Confirmar provider de e-mail (Postmark vs SendGrid) via spike de entregabilidade | LAC-03, DEC-008 | Contrato INT-02; entregabilidade do digest | Executar spike antes de concluir Fase 0; produzir ADR-0005 |
| VAL-TRD-02 | Reporting: worker assíncrono desde Fase 1 ou módulo síncrono do azim-api | DDD-VAL-02, VAL-MOD-03 | Define deployable azim-reporting-worker | Manter síncrono no azim-api na Fase 1; promover na Fase 2 |
| VAL-TRD-03 | Retenção de audit_logs e contacts (PII) sob LGPD | VAL-08, LAC-07 | Política de expurgo; compliance LGPD | Envolver jurídico da Vellus antes do schema freeze |
| VAL-TRD-04 | Threshold de rate limit no Cloud Armor (req/IP/min) | NFR-SEG-06 | Configuração Terraform; proteção contra abuso | Definir thresholds após load test em stg |
| VAL-TRD-05 | Prazo de expiração do token do digest (24h ou 48h) | J-02, BC-06 | UX do digest; segurança do token | Recomendar 48h; confirmar com produto |
| VAL-TRD-06 | Pool de conexões Cloud SQL: tamanho e estratégia (Cloud SQL Proxy vs pgBouncer) | NFR-ESC-01 | Dimensionamento para 50 tenants | Cloud SQL Proxy como padrão GCP; pgBouncer se pool saturar |
| VAL-TRD-07 | Divergência de stack de observabilidade: GCP vs Prometheus/Loki/Jaeger nas regras .forge | PTV-08, NFRD §3 | ADR-0007; coerência das regras de projeto | Produzir ADR-0007 ratificando stack GCP; atualizar .forge/rules/architecture/observability.md |
| VAL-TRD-08 | Suporte a multimoeda antes do schema freeze | LAC-05 | Campo currency em opportunities e commissions | Decisão urgente; adicionar `currency CHAR(3) DEFAULT 'BRL'` preventivamente |
| VAL-TRD-09 | Criptografia em nível de campo para PII de contatos (nome, e-mail, celular) | NFR-PRIV-01, LGPD | Proteção adicional além da encryption at rest | Avaliar necessidade com jurídico; encryption at rest GCP pode ser suficiente no MVP |
| VAL-TRD-10 | Fluxo de anonimização / exclusão de contato (direito ao esquecimento LGPD art. 18) | VAL-TRD-03, LGPD | Compliance LGPD; processo operacional | Endpoint adicionado (ADJ-TRD-003); definir fluxo operacional e runbook de anonimização |
| VAL-TRD-11 | Remoção do módulo data-migration pós-Fase 1 | DDD-VAL-03, VAL-MOD-04 | Limpeza arquitetural; redução de surface de ataque | Desabilitar via feature flag após migração; remover na Fase 2 |
| VAL-TRD-12 | Mecanismo concreto de feature flags (env vars vs Unleash/LaunchDarkly) | §16.5 | Operabilidade; rollout gradual | Env vars suficientes para Fase 1; avaliar ferramenta dedicada na Fase 2 |
| VAL-TRD-13 | Padrão de acesso do digest worker a itens estagnados/vencidos: Pub/Sub (§7.3, §9.3) vs. DB polling (§19.3) | §7.3 vs. §19.3, Modules README §7 | Topologia de assinaturas Pub/Sub; arquitetura do azim-digest-worker | Decidir antes de implementar o azim-digest-worker; atualizar §7.3, §9.3 e §19.3 conforme decisão |

---

## 21. Métricas da Validação

| Métrica | Quantidade |
|---|---|
| Seções obrigatórias avaliadas | 23 |
| Seções adicionadas ao TRD | 0 (todas as 23 presentes) |
| Ajustes aplicados diretamente | 6 (ADJ-TRD-001 a ADJ-TRD-006) |
| Achados críticos | 0 |
| Achados altos | 1 (FIND-TRD-001 — data-model.md fora do escopo) |
| Achados médios | 4 (FIND-TRD-002 a FIND-TRD-005) |
| Achados baixos | 1 (FIND-TRD-006) |
| Pontos a validar | 13 (VAL-TRD-01 a VAL-TRD-13) |
| Conflitos arquiteturais | 0 |

---

## 22. Parecer Final

### Classificação

**Aprovado com Ressalvas**

### Justificativa

O TRD do Azim CRM está tecnicamente completo, coerente e implementável para a Fase 1. Todos os 23 módulos, deployables, contratos de API, eventos de domínio, padrões de dados, controles de segurança, mecanismos de observabilidade, estratégias de deploy e runbooks operacionais estão presentes e alinhados aos documentos de entrada (PRD, FRD, NFRD, DDD Segmentation, Modules README, Data Model).

Os 6 ajustes aplicados diretamente (eventos de comissão, endpoint LGPD, rastreabilidade RF-10, VAL-TRD-13) corrigem lacunas deriváveis dos insumos sem introduzir escopo novo ou decisões não fundamentadas.

Não há achados críticos nem conflitos arquiteturais. O único achado de alta severidade (FIND-TRD-001) está em data-model.md, fora do escopo do TRD Validator. Os 13 pontos a validar são controláveis e, com exceção de VAL-TRD-13 (digest worker data access pattern), não bloqueiam o início da implementação.

### Condições para Aprovação Total

- Resolver VAL-TRD-13 (digest worker: Pub/Sub vs. DB polling) antes de implementar azim-digest-worker
- Corrigir FIND-TRD-001 (opportunity_number UNIQUE) em data-model.md antes do schema freeze
- Formalizar ADR-0001 e ADR-0007 antes de iniciar a Fase 0
- Resolver VAL-TRD-03 (retenção PII) com equipe jurídica antes do go-live
- Definir threshold de Cloud Armor (VAL-TRD-04) após load test em stg
- Executar spike de provider de e-mail (VAL-TRD-01) antes de concluir a Fase 0

### Próximos Passos Recomendados

1. Revisar os 6 ajustes aplicados no TRD v1.1
2. Criar `docs/product/adr/` e formalizar ADR-0001 (Multi-tenancy pooled + RLS) e ADR-0007 (Stack de observabilidade GCP) como prioridade Fase 0
3. Resolver VAL-TRD-13 em sessão de arquitetura com a equipe de engenharia (digest worker: Pub/Sub subscription vs. DB polling)
4. Corrigir `opportunity_number UNIQUE` para `UNIQUE(tenant_id, opportunity_number)` em data-model.md
5. Envolver equipe jurídica para resolver VAL-TRD-03 (retenção PII) antes do schema freeze
6. Executar spike de entregabilidade de e-mail (VAL-TRD-01 / ADR-0005) antes de encerrar a Fase 0
7. Considerar adicionar Deployment Diagram a §19 do TRD antes da revisão técnica final (FIND-TRD-003)
