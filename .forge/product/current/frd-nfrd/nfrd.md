# NFRD — Azim CRM

**Documento:** Non-Functional Requirements Document — Azim CRM
**Produto:** Azim CRM
**Versão:** 0.1
**Data:** 2026-06-10
**Status:** Rascunho para revisão
**Owner:** Milton Antonio da Silva Jr
**PRD de Origem:** `docs/product/prd/prd.md` v0.1

---

## Controle de Versão

| Versão | Data | Autor | Descrição |
|---|---|---|---|
| 0.1 | 2026-06-10 | Milton Antonio da Silva Jr | Versão inicial derivada do PRD v0.1; cobre 14 categorias; 59 NFRs derivados |

---

## Sumário

1. [Introdução](#1-introdução)
2. [Objetivo do Documento](#2-objetivo-do-documento)
3. [Referências](#3-referências)
4. [Visão Geral dos Atributos de Qualidade](#4-visão-geral-dos-atributos-de-qualidade)
5. [Escopo Não Funcional](#5-escopo-não-funcional)
6. [Fora de Escopo](#6-fora-de-escopo)
7. [Decisão por Categoria](#7-decisão-por-categoria)
8. [Detalhamento dos Requisitos Não Funcionais](#8-detalhamento-dos-requisitos-não-funcionais)
9. [Restrições Técnicas Não Funcionais](#9-restrições-técnicas-não-funcionais)
10. [Matriz de Rastreabilidade PRD → NFRD](#10-matriz-de-rastreabilidade-prd--nfrd)
11. [Critérios de Validação Não Funcional](#11-critérios-de-validação-não-funcional)
12. [Dependências](#12-dependências)
13. [Premissas](#13-premissas)
14. [Pontos a Validar](#14-pontos-a-validar)
15. [Anexos](#15-anexos)

---

## 1. Introdução

O **Azim CRM** é uma plataforma SaaS de gestão comercial multi-tenant voltada para empresas B2B. A fundação técnica combina GCP 100% Terraform na região `southamerica-east1`, backend .NET 10 / ASP.NET Core, SPA React + TypeScript, Cloud SQL/Postgres com isolamento por RLS pooled e digest diário automatizado como diferencial competitivo central.

O primeiro tenant e operador da plataforma é a **Vellus** (3 BUs: Vellus 71 oportunidades, Axis 20, Vellus Tech 17), com 108 oportunidades e R$ 9.013.250 em forecast ponderado atualmente geridos em planilha Excel — contexto que dimensiona os volumes iniciais e a criticidade da Fase 1.

Este documento define os requisitos não funcionais (NFRs) verificáveis que a plataforma deve satisfazer para ser considerada pronta para produção em cada fase do roadmap. Os NFRs cobrem: performance, disponibilidade, escalabilidade, resiliência, segurança, privacidade, compliance, observabilidade, auditoria, interoperabilidade, usabilidade, manutenibilidade, portabilidade e operabilidade.

> **Convenção de marcação:** metas numéricas presentes no PRD mas sem evidência de origem (marcadas como pendências P6/P7 na validação do PRD) são indicadas como **"alvo proposto a confirmar"** — jamais tratadas como fato sourced. Onde o NFRD propõe uma meta sem respaldo no PRD, usa-se **"Proposto pelo NFRD — validar com produto"**.

---

## 2. Objetivo do Documento

Este NFRD responde às seguintes questões:

- Quais atributos de qualidade o Azim CRM deve atender e com quais metas mensuráveis?
- Como cada NFR será validado e em qual momento do ciclo de desenvolvimento?
- Como cada NFR se rastreia ao PRD e às decisões arquiteturais aprovadas (DEC-001 a DEC-013)?
- Quais pontos permanecem em aberto e precisam de decisão antes do go-live?

O NFRD é insumo para o TRD (escolhas técnicas) e para os ADRs (decisões arquiteturais formais). Não altera o escopo funcional do PRD nem detalha implementação.

---

## 3. Referências

| Documento | Caminho | Papel |
|---|---|---|
| PRD — Azim CRM v0.1 | `docs/product/prd/prd.md` | Fonte primária de NFRs, KPIs e decisões |
| Discovery Notes | `docs/discovery/discovery-notes.md` | Volumes, stack, DEC-001 a DEC-013 |
| Quality Gates | `.forge/rules/testing/quality-gates.md` | Baseline de coverage e gates de CI |
| Observabilidade e Auditoria | `.forge/rules/architecture/observability.md` | Correlação ID, métricas, logs, traces |
| Segurança e Conformidade | `.forge/rules/architecture/security-and-compliance.md` | Baseline segurança e LGPD |
| Segurança e Segredos | `.forge/rules/architecture/security-and-secrets.md` | Política de segredos |
| Audit Immutability | `.forge/rules/domain/audit-immutability.md` | Padrão de imutabilidade de AuditLog |
| APIs e Contratos | `.forge/rules/architecture/api-and-contracts.md` | Versionamento de API |
| LGPD | Lei nº 13.709/2018 | Base regulatória de privacidade |
| WCAG 2.1 nível AA | W3C Recommendation | Padrão de acessibilidade |

> **Divergência de stack (Ponto a Validar PTV-08):** as regras `.forge/rules/architecture/observability.md` e `.forge/rules/architecture/security-and-secrets.md` referenciam ferramentas Kubernetes/Grafana/Prometheus/Loki/Jaeger/ESO que diferem da stack GCP aprovada no PRD (Cloud Logging/Monitoring/Trace, GCP Secret Manager — DEC-005). Os **princípios** das regras (correlationId, logs sem PII, auditoria imutável, zero segredo em repositório) são adotados integralmente neste NFRD; a **implementação específica** segue a stack GCP de DEC-005. A divergência está registrada em PTV-08 e sugere ADR-0005.

---

## 4. Visão Geral dos Atributos de Qualidade

| Prioridade | Atributo | Justificativa |
|---|---|---|
| 1 | Segurança / Isolamento de Tenant | Vazamento entre tenants = incidente sev-1; dados comerciais e pessoais de clientes distintos no mesmo banco (pool + RLS) |
| 2 | Disponibilidade | Digest diário às 07:00 BRT é diferencial de produto; falha visível ao usuário final toda manhã |
| 3 | Privacidade / LGPD | Dados pessoais de contatos (nome, e-mail, celular) sem controle de acesso na planilha atual — risco imediato |
| 4 | Performance | Kanban fluido com centenas de oportunidades e digest processado antes das 07:00 BRT são requisitos funcionais |
| 5 | Observabilidade | Sistema multi-tenant requer rastreabilidade completa por tenant; auditoria imutável é requisito de produto |
| 6 | Resiliência | Execução assíncrona por padrão; digest não pode bloquear o sistema; idempotência mandatória para retentativas |
| 7 | Escalabilidade | Pool + RLS projetado para crescer de 1 para 50+ tenants sem retrabalho arquitetural |
| 8 | Manutenibilidade | Produto em desenvolvimento ativo com roadmap de 3 fases; cobertura e testes de isolamento em CI são gates obrigatórios |

---

## 5. Escopo Não Funcional

Este documento cobre todos os serviços da plataforma Azim CRM nas Fases 0 e 1 (MVP), com indicação de NFRs que se estendem às Fases 2 e 3:

- **API REST** (.NET 10 / ASP.NET Core): endpoints de negócio, auth, digest, relatórios
- **Worker de Digest** (.NET 10, Cloud Run separado): consumidor Pub/Sub do digest diário
- **SPA Frontend** (React + TypeScript): interface web responsiva
- **Banco de dados** (Cloud SQL / Postgres): multi-tenant pooled + RLS
- **Cache / Rate-limit** (Memorystore/Redis): sessão, memberships, rate-limit por tenant
- **Mensageria** (Cloud Pub/Sub): eventos de domínio assíncronos
- **Scheduler** (Cloud Scheduler): trigger do digest
- **Infraestrutura** (GCP, Terraform): ambientes dev / stg / prd em `southamerica-east1`
- **Serviço de IA** (Python / FastAPI, Fase 3): scoring, resumo 360° e copilot por tenant

---

## 6. Fora de Escopo

- Requisitos funcionais de produto (pertencem ao FRD)
- Arquitetura técnica detalhada, contratos de API e seleção de bibliotecas (pertencem ao TRD)
- Modelo de domínio e regras de negócio (pertencem ao FRD / DDD Architect)
- Billing, cobrança e faturamento do Azim como SaaS (REST-01 — fora do escopo do MVP)
- Portal do parceiro e acesso externo (REST-03 — fora do MVP)
- App mobile nativo ou PWA (horizonte do roadmap; sem NFRs específicos até decisão formal)
- Multimoeda (LAC-05 — pendente; NFRs de conversão de moeda não tratados nesta versão)

---

## 7. Decisão por Categoria

| Categoria | Aplicável? | Justificativa (quando não aplicável) | NFRs |
|---|---|---|---|
| Performance (PERF) | Sim | — | NFR-PERF-01 a NFR-PERF-07 |
| Disponibilidade (DISP) | Sim | — | NFR-DISP-01 a NFR-DISP-04 |
| Escalabilidade (ESC) | Sim | — | NFR-ESC-01 a NFR-ESC-04 |
| Resiliência (RES) | Sim | — | NFR-RES-01 a NFR-RES-03 |
| Segurança (SEG) | Sim | — | NFR-SEG-01 a NFR-SEG-08 |
| Privacidade (PRIV) | Sim | — | NFR-PRIV-01 a NFR-PRIV-05 |
| Compliance (COMP) | Sim | — | NFR-COMP-01 a NFR-COMP-03 |
| Observabilidade (OBS) | Sim | — | NFR-OBS-01 a NFR-OBS-05 |
| Auditoria (AUD) | Sim | — | NFR-AUD-01 a NFR-AUD-03 |
| Interoperabilidade (INT) | Sim | — | NFR-INT-01 a NFR-INT-03 |
| Usabilidade e Acessibilidade (USA) | Sim | — | NFR-USA-01 a NFR-USA-04 |
| Manutenibilidade (MAN) | Sim | — | NFR-MAN-01 a NFR-MAN-04 |
| Portabilidade (POR) | Sim | — | NFR-POR-01 a NFR-POR-02 |
| Operabilidade (OPS) | Sim | — | NFR-OPS-01 a NFR-OPS-04 |

> Todas as 14 categorias são aplicáveis ao Azim CRM. Nenhuma categoria está fora de escopo.

## 8. Detalhamento dos Requisitos Não Funcionais

---

### 8.1 Performance (PERF)

---

### NFR-PERF-01 — Latência de API REST (operações gerais)

| Campo | Conteúdo |
|---|---|
| **Categoria** | Performance |
| **Descrição** | Toda requisição autenticada à API REST do Azim (exceto endpoints com SLO específico definido em NFR-PERF-02 a NFR-PERF-07) deve responder dentro do SLO geral de latência. |
| **Meta** | p95 ≤ 300 ms; p99 ≤ 800 ms — em carga nominal de até 50 RPS |
| **Método de medição** | Histograma `http_request_duration_seconds` no Cloud Monitoring; percentis sobre janela de 5 minutos; alertas automáticos em Cloud Monitoring |
| **Fonte de dados** | Cloud Monitoring / Cloud Trace |
| **Escopo** | Todos os endpoints da API REST .NET 10, exceto os cobertos por NFR-PERF-02 a NFR-PERF-07 |
| **Prioridade** | Alta |
| **Origem** | Proposto pelo NFRD — validar com produto (p95 ≤ 300 ms não consta explicitamente no PRD §8.2 como meta geral; instrução do usuário na geração deste documento) |
| **Critérios de aceite** | Load test pré-release: 50 RPS por 15 min; p95 ≤ 300 ms; p99 ≤ 800 ms sem degradação; alerta Cloud Monitoring configurado para p95 > 300 ms por > 5 min em produção |
| **Dependência arquitetural** | ADR-0001 — `correlationId-e-tenant-id-propagation` (a ser criado via `adr-writer`) |

---

### NFR-PERF-02 — Latência de Carregamento do Kanban

| Campo | Conteúdo |
|---|---|
| **Categoria** | Performance |
| **Descrição** | O carregamento inicial da visão Kanban (RF-06) com todas as oportunidades ativas da BU deve ocorrer dentro do SLO de latência, inclusive em carga com volumes de até 2.000 oportunidades por tenant. |
| **Meta** | p95 ≤ 2.000 ms (2 s) em carga nominal; p95 ≤ 2.000 ms também em load test com 2.000 oportunidades por tenant — alvo proposto a confirmar |
| **Método de medição** | Latência do endpoint GET `/api/v1/kanban` medida via Cloud Trace; histograma no Cloud Monitoring |
| **Fonte de dados** | Cloud Monitoring / Cloud Trace |
| **Escopo** | Endpoint de carregamento do Kanban por BU (RF-06); inclui cálculo de soma de valor total e forecast ponderado por estágio |
| **Prioridade** | Alta |
| **Origem** | PRD §8.2 — alvo proposto a confirmar (PRD §8.2 e KPI-05 citam ≤ 2 s; sem evidência de origem do número; ver PTV-01) |
| **Critérios de aceite** | Load test pré-release com 2.000 oportunidades por tenant a 50 RPS: p95 ≤ 2 s; consistente com KPI-05; alerta em produção se p95 > 2 s por > 5 min (PRD §8.6) |
| **Dependência arquitetural** | Índices compostos `(tenant_id, bu_id, stage_id)` e paginação/lazy loading no Kanban são pré-requisitos (RISCO-T03 do PRD) |

---

### NFR-PERF-03 — Latência de Criação e Edição de Oportunidade

| Campo | Conteúdo |
|---|---|
| **Categoria** | Performance |
| **Descrição** | As operações de criação e edição de oportunidade (RF-06), incluindo validação de regras de estágio e gravação no AuditLog, devem responder dentro do SLO. |
| **Meta** | p95 ≤ 500 ms — alvo proposto a confirmar |
| **Método de medição** | Latência dos endpoints POST e PATCH `/api/v1/opportunities` via Cloud Trace |
| **Fonte de dados** | Cloud Monitoring / Cloud Trace |
| **Escopo** | Endpoints de criação e edição de oportunidade, incluindo drag-and-drop de estágio no Kanban |
| **Prioridade** | Alta |
| **Origem** | PRD §8.2 — alvo proposto a confirmar (citado sem evidência de origem; ver PTV-01) |
| **Critérios de aceite** | Load test pré-release: p95 ≤ 500 ms a 50 RPS; gravação de AuditLog incluída no tempo de resposta |

---

### NFR-PERF-04 — Latência de Autenticação

| Campo | Conteúdo |
|---|---|
| **Categoria** | Performance |
| **Descrição** | As operações de login (e-mail/senha e Google) e renovação de token via GCP Identity Platform devem responder dentro do SLO. |
| **Meta** | p95 ≤ 1.000 ms (1 s) — alvo proposto a confirmar |
| **Método de medição** | Latência dos endpoints de auth medida no backend .NET (excluindo latência do Identity Platform externo, que é medida separadamente) |
| **Fonte de dados** | Cloud Monitoring / Cloud Trace |
| **Escopo** | RF-01 — endpoints de login, renovação de token e logout |
| **Prioridade** | Alta |
| **Origem** | PRD §8.2 — alvo proposto a confirmar (ver PTV-01) |
| **Critérios de aceite** | Teste de carga pré-release: p95 ≤ 1 s; latência do Identity Platform monitorada via Cloud Monitoring como dependência externa |

---

### NFR-PERF-05 — Processamento do Digest Diário

| Campo | Conteúdo |
|---|---|
| **Categoria** | Performance |
| **Descrição** | O serviço de digest deve processar e enfileirar os e-mails de todos os tenants e usuários elegíveis para o horário selecionado dentro da janela de tempo especificada, garantindo entrega às 07:00 BRT. |
| **Meta** | Processamento completo (seleção de usuários + geração + enfileiramento) em ≤ 5 min após o trigger do Cloud Scheduler — alvo proposto a confirmar |
| **Método de medição** | Métrica customizada `digest_processing_duration_seconds` no Cloud Monitoring; tempo entre o trigger do Cloud Scheduler e a última mensagem enfileirada no Pub/Sub |
| **Fonte de dados** | Cloud Monitoring; EmailDigestLog |
| **Escopo** | Worker de Digest (Cloud Run); cobre todos os tenants configurados para o horário 07:00 |
| **Prioridade** | Alta |
| **Origem** | PRD §8.2 — alvo proposto a confirmar; US-01 exige entrega entre 07:00 e 07:05 BRT (ver PTV-01) |
| **Critérios de aceite** | Teste de carga com 1.000 destinatários: processamento ≤ 5 min; alerta Cloud Monitoring se duração > 4 min (janela de segurança) |

---

### NFR-PERF-06 — Import da Planilha

| Campo | Conteúdo |
|---|---|
| **Categoria** | Performance |
| **Descrição** | O import transacional da planilha `Pipeline Vellus.xlsx` com até 500 oportunidades deve completar dentro da janela de tempo especificada, incluindo validação, dry-run e gravação transacional. |
| **Meta** | Import transacional de até 500 oportunidades em ≤ 5 min (excluindo a etapa de triagem assistida manual) — alvo proposto a confirmar |
| **Método de medição** | Tempo total de execução medido no endpoint de import; log estruturado com timestamp de início e término |
| **Fonte de dados** | Cloud Logging; relatório de migração auditado |
| **Escopo** | Módulo de migração da planilha (J-04); inclui dry-run e import transacional |
| **Prioridade** | Média |
| **Origem** | PRD §8.2 — alvo proposto a confirmar (ver PTV-01) |
| **Critérios de aceite** | Teste de import com 108 oportunidades (volume real da Vellus) e com 500 oportunidades em stg: ambos em ≤ 5 min; rollback total em caso de falha testado explicitamente |

---

### NFR-PERF-07 — Relatórios Enxutos

| Campo | Conteúdo |
|---|---|
| **Categoria** | Performance |
| **Descrição** | Os relatórios enxutos (RF-11) — funil, forecast, ranking, canais e comissões — para períodos de até 12 meses devem responder dentro do SLO de latência. |
| **Meta** | p95 ≤ 3.000 ms (3 s) para qualquer relatório com período ≤ 12 meses — alvo proposto a confirmar |
| **Método de medição** | Latência dos endpoints de relatório via Cloud Trace |
| **Fonte de dados** | Cloud Monitoring / Cloud Trace |
| **Escopo** | RF-11 — endpoints de relatório; volume de referência: até 2.000 oportunidades por tenant |
| **Prioridade** | Média |
| **Origem** | PRD §8.2 — alvo proposto a confirmar (ver PTV-01) |
| **Critérios de aceite** | Teste de carga pré-release com 2.000 oportunidades e período de 12 meses: p95 ≤ 3 s; Export CSV incluído na medição |

---

### 8.2 Disponibilidade (DISP)

---

### NFR-DISP-01 — Disponibilidade Tier 1 (Kanban, Digest, Autenticação)

| Campo | Conteúdo |
|---|---|
| **Categoria** | Disponibilidade |
| **Descrição** | Os módulos críticos para a operação comercial diária — Pipeline/Kanban (RF-06), Digest (RF-09) e Autenticação (RF-01) — devem manter disponibilidade mínima mensal. |
| **Meta** | ≥ 99,5% de disponibilidade mensal (downtime tolerado: ≤ 3 h 39 min / mês) — alvo proposto a confirmar |
| **Método de medição** | Uptime medido via Cloud Monitoring Uptime Checks nos endpoints de health dos serviços correspondentes; janela de medição: mês calendário |
| **Fonte de dados** | Cloud Monitoring Uptime Checks; SLO dashboard |
| **Escopo** | RF-01 (auth), RF-06 (Kanban/oportunidades), RF-09 (digest — disponibilidade do agendamento) |
| **Prioridade** | Alta |
| **Origem** | PRD §8.1 — alvo proposto a confirmar (percentual citado sem evidência de origem; ver PTV-02) |
| **Critérios de aceite** | SLO dashboard configurado em Cloud Monitoring; alerta quando error budget < 20%; relatório mensal de disponibilidade |
| **Dependência arquitetural** | ADR-0002 — `slo-por-tier-de-disponibilidade` (a ser criado via `adr-writer`) |

---

### NFR-DISP-02 — Disponibilidade Tier 2

| Campo | Conteúdo |
|---|---|
| **Categoria** | Disponibilidade |
| **Descrição** | Os módulos importantes, mas com degradação tolerável por algumas horas — Contas/Contatos (RF-04), Atividades (RF-07), Relatórios (RF-11), Metas (RF-08), BUs/Usuários (RF-03) — devem manter disponibilidade mínima mensal. |
| **Meta** | ≥ 99,0% de disponibilidade mensal (downtime tolerado: ≤ 7 h 18 min / mês) — alvo proposto a confirmar |
| **Método de medição** | Cloud Monitoring Uptime Checks nos endpoints de health dos serviços correspondentes; janela: mês calendário |
| **Fonte de dados** | Cloud Monitoring Uptime Checks |
| **Escopo** | RF-03, RF-04, RF-07, RF-08, RF-11 |
| **Prioridade** | Alta |
| **Origem** | PRD §8.1 — alvo proposto a confirmar (ver PTV-02) |
| **Critérios de aceite** | SLO dashboard configurado; alerta quando error budget < 20% |
| **Dependência arquitetural** | ADR-0002 — `slo-por-tier-de-disponibilidade` (a ser criado via `adr-writer`) |

---

### NFR-DISP-03 — Disponibilidade Tier 3

| Campo | Conteúdo |
|---|---|
| **Categoria** | Disponibilidade |
| **Descrição** | Módulos de operações administrativas e assíncronas — Backoffice Platform Operator, Migração de planilha, Automações (RF-12, Fase 2), IA (RF-13, Fase 3) — têm tolerância maior a indisponibilidade. |
| **Meta** | ≥ 98,0% de disponibilidade mensal (downtime tolerado: ≤ 14 h 36 min / mês) — alvo proposto a confirmar |
| **Método de medição** | Cloud Monitoring Uptime Checks |
| **Fonte de dados** | Cloud Monitoring Uptime Checks |
| **Escopo** | Backoffice de Platform Operator; módulo de migração; workers de automação (Fase 2); serviço de IA (Fase 3) |
| **Prioridade** | Média |
| **Origem** | PRD §8.1 — alvo proposto a confirmar (ver PTV-02) |
| **Critérios de aceite** | SLO dashboard configurado; incidentes de Tier 3 têm SLA de resposta mais longo (a definir no runbook operacional) |
| **Dependência arquitetural** | ADR-0002 — `slo-por-tier-de-disponibilidade` (a ser criado via `adr-writer`) |

---

### NFR-DISP-04 — Janela de Manutenção Planejada

| Campo | Conteúdo |
|---|---|
| **Categoria** | Disponibilidade |
| **Descrição** | Manutenções planejadas (deploys, migrations, upgrades de infraestrutura) que impliquem downtime devem ser realizadas fora da janela crítica de uso do digest. |
| **Meta** | Manutenções com downtime planejado realizadas fora da janela 06:30–09:00 BRT de segunda a sexta-feira; zero deploys disruptivos sem aviso prévio de 24 h |
| **Método de medição** | Auditoria de histórico de deploys no CI/CD (GitHub Actions); verificação de timestamps dos deploys em produção |
| **Fonte de dados** | GitHub Actions; Cloud Logging (eventos de deploy) |
| **Escopo** | Todos os ambientes de produção (prd) |
| **Prioridade** | Alta |
| **Origem** | Inferência Não Funcional (derivada do requisito de digest às 07:00 BRT — PRD RF-09 / US-01; a janela de restrição não está explicitamente no PRD) |
| **Critérios de aceite** | Runbook operacional documenta a política de janela de manutenção; deploys em prd usam rolling update ou blue-green (sem downtime quando possível); migrations EF Core executadas como job pré-tráfego |

### 8.3 Escalabilidade (ESC)

---

### NFR-ESC-01 — Capacidade de Tenants

| Campo | Conteúdo |
|---|---|
| **Categoria** | Escalabilidade |
| **Descrição** | A arquitetura pool + RLS deve suportar o crescimento de tenants ativos sem retrabalho estrutural no banco de dados ou na camada de aplicação. |
| **Meta** | Suporte a até 50 tenants ativos simultâneos sem mudança arquitetural; onboarding de novo tenant em ≤ 30 min (processo automatizado) |
| **Método de medição** | Teste de carga em stg com múltiplos tenants simultâneos; benchmark de tempo de provisionamento via IaC |
| **Fonte de dados** | Cloud Monitoring (uso de conexões Cloud SQL, Memorystore); GitHub Actions (tempo de deploy de tenant) |
| **Escopo** | Cloud SQL (RLS), Memorystore/Redis (cache de memberships), API (.NET 10) |
| **Prioridade** | Alta |
| **Origem** | PRD §8.7 — pico estimado de 50 tenants; DEC-006 (pool + RLS suporta centenas sem retrabalho) |
| **Critérios de aceite** | Teste de carga em stg com 10 tenants simultâneos e 200 oportunidades cada: sem degradação de SLO de PERF; pool de conexões Cloud SQL dimensionado para 50 tenants |

---

### NFR-ESC-02 — Capacidade de Oportunidades por Tenant

| Campo | Conteúdo |
|---|---|
| **Categoria** | Escalabilidade |
| **Descrição** | O Kanban e os relatórios devem permanecer dentro do SLO de performance mesmo com o volume de oportunidades crescendo até o pico estimado de 2.000 por tenant. |
| **Meta** | Kanban p95 ≤ 2 s com 2.000 oportunidades ativas por tenant (load test obrigatório pré-release de Fase 1); relatórios p95 ≤ 3 s no mesmo volume |
| **Método de medição** | Load test em stg com dataset de 2.000 oportunidades por tenant; latência medida via Cloud Trace |
| **Fonte de dados** | Cloud Monitoring / Cloud Trace |
| **Escopo** | Endpoints do Kanban (RF-06) e de relatórios (RF-11); índices compostos por `(tenant_id, bu_id, stage_id)` |
| **Prioridade** | Alta |
| **Origem** | PRD §8.7 (pico de 2.000 oportunidades/tenant); RISCO-T03; KPI-05 |
| **Critérios de aceite** | Load test documentado e aprovado antes do go-live da Fase 1; índices compostos presentes na migration; paginação e lazy loading no Kanban implementados desde o MVP |

---

### NFR-ESC-03 — Throughput de Pico

| Campo | Conteúdo |
|---|---|
| **Categoria** | Escalabilidade |
| **Descrição** | O sistema deve suportar picos de tráfego de API sem degradação de SLO ou aumento de erros 5xx. |
| **Meta** | 200 RPS de pico sem degradação de SLO (p95 PERF e disponibilidade DISP mantidos); sem escalabilidade manual necessária |
| **Método de medição** | Load test em stg com rampa de 0 a 200 RPS; métricas de latência, erros e conexões Cloud SQL |
| **Fonte de dados** | Cloud Monitoring; Cloud Run autoscaling metrics |
| **Escopo** | API REST (.NET 10, Cloud Run); Cloud SQL (pool de conexões) |
| **Prioridade** | Média |
| **Origem** | PRD §8.7 — pico estimado de 200 RPS; nota "a validar com load test antes do go-live" |
| **Critérios de aceite** | Load test a 200 RPS durante 5 min sem erros 5xx e sem p95 > 300 ms; Cloud Run configurado com autoscaling e mínimo de instâncias definido |

---

### NFR-ESC-04 — Destinatários do Digest

| Campo | Conteúdo |
|---|---|
| **Categoria** | Escalabilidade |
| **Descrição** | O worker de digest deve processar e enfileirar e-mails para até 1.000 destinatários em um único horário sem violação do SLO de pontualidade (NFR-OPS-03). |
| **Meta** | Até 1.000 destinatários processados em ≤ 5 min (NFR-PERF-05 mantido); sem e-mails duplicados |
| **Método de medição** | Teste com dataset de 1.000 usuários em stg; tempo de enfileiramento total no Pub/Sub |
| **Fonte de dados** | Cloud Monitoring; EmailDigestLog; Pub/Sub metrics |
| **Escopo** | Worker de Digest (Cloud Run); Pub/Sub topic de digest; IEmailSender |
| **Prioridade** | Média |
| **Origem** | PRD §8.7 — destinatários digest/dia: pico de 1.000 |
| **Critérios de aceite** | Teste de carga com 1.000 destinatários: tempo total ≤ 5 min; zero duplicatas verificado via EmailDigestLog; worker escalado horizontalmente se necessário |

---

### 8.4 Resiliência (RES)

---

### NFR-RES-01 — Execução Assíncrona Obrigatória

| Campo | Conteúdo |
|---|---|
| **Categoria** | Resiliência |
| **Descrição** | Todas as operações de processamento pesado — digest diário, automações (Fase 2), scoring de IA (Fase 3) — devem ser executadas de forma assíncrona via Pub/Sub, nunca bloqueando a API síncrona de negócio. |
| **Meta** | Zero execuções síncronas de digest ou automações na API de negócio; todas as operações de fundo publicam evento no Pub/Sub e retornam imediatamente (HTTP 202) |
| **Método de medição** | Revisão de código (gate de PR); testes de integração verificam que a API retorna antes de o worker concluir |
| **Fonte de dados** | GitHub Actions CI; Cloud Monitoring (latência do endpoint de trigger) |
| **Escopo** | Worker de Digest; Worker de Automações (RF-12, Fase 2); pipeline de IA (RF-13, Fase 3) |
| **Prioridade** | Alta |
| **Origem** | PRD RF-12 ("execução sempre assíncrona via Pub/Sub → worker; nunca síncrona"); DEC-009 (Cloud Scheduler + Pub/Sub para digest) |
| **Critérios de aceite** | Teste automatizado verifica que o endpoint de trigger do digest retorna HTTP 202 em ≤ 200 ms; consumidor Pub/Sub é independente da API; falha do worker não afeta disponibilidade da API |
| **Dependência arquitetural** | ADR-0004 — `outbox-e-idempotencia-eventos-dominio` (a ser criado via `adr-writer`) |

---

### NFR-RES-02 — Idempotência de Eventos

| Campo | Conteúdo |
|---|---|
| **Categoria** | Resiliência |
| **Descrição** | O sistema deve garantir idempotência em todas as operações que podem ser retentadas: envio de digest, publicação de eventos de domínio (outbox pattern) e ações via link autenticado do digest. |
| **Meta** | Zero e-mails de digest duplicados para o mesmo usuário no mesmo dia; zero eventos de domínio duplicados processados em retentativas; ação via link do digest idempotente (múltiplos cliques no mesmo link produzem o mesmo estado final) |
| **Método de medição** | Teste de integração: simula falha na metade do processamento do digest e verifica que retentativa não gera duplicatas via EmailDigestLog; teste de link duplicado |
| **Fonte de dados** | EmailDigestLog; tabela de outbox; Cloud Logging |
| **Escopo** | Worker de Digest; eventos de domínio publicados ao Pub/Sub; endpoint de ação via link autenticado do digest (RF-09) |
| **Prioridade** | Alta |
| **Origem** | PRD §8.6 (EmailDigestLog como trilha de idempotência); J-02 (idempotência por usuário + data) |
| **Critérios de aceite** | Teste de integração com retry forçado: EmailDigestLog registra exatamente 1 envio por (usuário, data); link autenticado clicado 3 vezes produz exatamente 1 atividade concluída |
| **Dependência arquitetural** | ADR-0004 — `outbox-e-idempotencia-eventos-dominio` (a ser criado via `adr-writer`) |

---

### NFR-RES-03 — Degradação Graciosa

| Campo | Conteúdo |
|---|---|
| **Categoria** | Resiliência |
| **Descrição** | O sistema deve operar de forma degradada mas funcional quando dados opcionais estão ausentes ou quando serviços não críticos falham, sem erros conspícuos ao usuário. |
| **Meta** | Ausência de meta cadastrada: painel exibe realizado e pipeline sem erro; bloco de metas no digest omitido sem falha de entrega; falha transiente do provider de e-mail: worker registra falha e re-tenta com backoff exponencial sem afetar outros tenants |
| **Método de medição** | Testes de integração com banco sem metas cadastradas; simulação de falha do provider de e-mail (mock do IEmailSender retornando erro); verificação de logs de retentativa |
| **Fonte de dados** | Cloud Logging; EmailDigestLog; Cloud Monitoring (taxa de falha de digest) |
| **Escopo** | RF-08 (metas — graceful degradation); RF-09 (digest — falha de provider); RF-13 (IA — rate-limit por tenant não afeta outros tenants) |
| **Prioridade** | Alta |
| **Origem** | PRD RF-08 ("graceful degradation: sem meta cadastrada, painel exibe apenas realizado vs pipeline"); J-02 (falha de e-mail → retentativa); J-03 |
| **Critérios de aceite** | Teste E2E com tenant sem metas: painel renderiza sem erro 5xx e sem mensagem de erro conspícua; teste de falha de provider: worker re-tenta 3x com backoff; alerta operacional disparado sem downtime da API |

---

### 8.5 Segurança (SEG)

---

### NFR-SEG-01 — Multi-Tenancy com Defesa em Profundidade

| Campo | Conteúdo |
|---|---|
| **Categoria** | Segurança |
| **Descrição** | O isolamento de dados entre tenants deve ser garantido por três camadas independentes, de modo que a falha de qualquer camada individual não resulte em vazamento de dados. Vazamento entre tenants é incidente sev-1. |
| **Meta** | Zero falhas de isolamento em testes de CI (KPI-06: 100% de aprovação a cada build); zero alertas de violação de RLS em produção; testes de isolamento como gate obrigatório de merge |
| **Método de medição** | Suite de testes automatizados em CI: tenant A tenta acessar recursos de tenant B via API e diretamente via SQL sem contexto de tenant — ambos devem ser bloqueados; alerta de Cloud Monitoring para violação de RLS |
| **Fonte de dados** | GitHub Actions CI pipeline; Cloud Monitoring (alertas de RLS) |
| **Escopo** | (1) EF Core global query filter por `tenant_id`; (2) RLS Postgres via `SET app.current_tenant = @tenant_id`; (3) testes de isolamento em CI |
| **Prioridade** | Alta |
| **Origem** | DEC-006; PRD §8.3; RISCO-T01; OBJ-05 |
| **Critérios de aceite** | CI bloqueado se qualquer teste de isolamento falhar (KPI-06); alerta Cloud Monitoring configurado para log de violação de RLS; code review obrigatório em mudanças de middleware de tenant |
| **Dependência arquitetural** | ADR-0001 — `correlationId-e-tenant-id-propagation` (a ser criado via `adr-writer`) |

---

### NFR-SEG-02 — Autenticação via GCP Identity Platform

| Campo | Conteúdo |
|---|---|
| **Categoria** | Segurança |
| **Descrição** | Nenhuma senha de usuário deve ser armazenada ou processada pelo sistema Azim. A autenticação é delegada integralmente ao GCP Identity Platform multi-tenant. |
| **Meta** | Zero senhas de usuário armazenadas no Cloud SQL do Azim; 100% dos fluxos de autenticação passam pelo Identity Platform; tokens validados em todo request autenticado |
| **Método de medição** | Inspeção do schema do banco (ausência de coluna `password_hash` em `users`); teste de autenticação com token inválido deve retornar HTTP 401 |
| **Fonte de dados** | Cloud SQL schema; testes de integração de autenticação |
| **Escopo** | RF-01 — login e-mail/senha, Google OAuth, convite, recuperação de senha, renovação de sessão |
| **Prioridade** | Alta |
| **Origem** | DEC-005; PRD §8.3; RF-01 |
| **Critérios de aceite** | Teste de integração: request com token expirado retorna 401; token de tenant A não autentica em contexto de tenant B; expiração de sessão configurada (política a definir no FRD/TRD) |

---

### NFR-SEG-03 — RBAC Verificado em Todo Endpoint

| Campo | Conteúdo |
|---|---|
| **Categoria** | Segurança |
| **Descrição** | Toda rota de API que acessa ou modifica dados de negócio deve verificar explicitamente as permissões RBAC do usuário autenticado. A verificação ocorre na camada de API e é confirmada pela RLS no banco. |
| **Meta** | Zero endpoints de negócio sem verificação de RBAC; 100% das combinações papel × capacidade da matriz RBAC do PRD cobertas por testes automatizados |
| **Método de medição** | Testes de integração: cada papel tenta acessar cada recurso; resposta esperada (200 ou 403) verificada; análise estática via custom Roslyn analyzer (gate de CI) |
| **Fonte de dados** | GitHub Actions CI; testes de integração com Testcontainers |
| **Escopo** | Todos os endpoints de negócio da API REST; matriz RBAC: Platform Operator, Tenant Admin, Gestor de BU, Vendedor, Viewer (PRD §5 / discovery §5.2) |
| **Prioridade** | Alta |
| **Origem** | PRD §8.3; RF-03; discovery §5.2 ("papéis aparentemente funcionando são o risco nº 1") |
| **Critérios de aceite** | Suite de testes RBAC cobrindo toda a matriz de permissões; nenhuma rota sensível sem atributo de autorização; revisão obrigatória de RBAC em mudanças de endpoint |

---

### NFR-SEG-04 — TLS em Todo Tráfego

| Campo | Conteúdo |
|---|---|
| **Categoria** | Segurança |
| **Descrição** | Todo tráfego externo (cliente → API) e interno (serviço → serviço) deve ser cifrado com TLS na versão mínima especificada. |
| **Meta** | TLS ≥ 1.2 obrigatório em todos os endpoints; TLS 1.0 e 1.1 desabilitados; certificados válidos e auto-renovados |
| **Método de medição** | Scan de configuração TLS (ex.: `ssl-enum-ciphers` no Nmap) em stg antes de cada release; configuração GCP Load Balancer verificada via Terraform |
| **Fonte de dados** | Terraform plan (política TLS no load balancer GCP); scan de segurança |
| **Escopo** | GCP Load Balancer → API; API → Cloud SQL; API → Memorystore; API → Pub/Sub; API → Identity Platform; API → provider de e-mail |
| **Prioridade** | Alta |
| **Origem** | PRD §8.3; `.forge/rules/architecture/security-and-compliance.md` |
| **Critérios de aceite** | Terraform configura `ssl_policy` com TLS 1.2+ no load balancer; scan em stg não detecta TLS < 1.2; Cloud SQL configurado com `sslmode=require` |

---

### NFR-SEG-05 — Segredos Exclusivamente via GCP Secret Manager

| Campo | Conteúdo |
|---|---|
| **Categoria** | Segurança |
| **Descrição** | Nenhum segredo (credencial de banco, chave de API, token de serviço) pode estar em código-fonte, repositório Git, imagem Docker, variável de ambiente não gerenciada ou arquivo de configuração versionado. |
| **Meta** | Zero segredos detectados em repositório Git ou imagens Docker; 100% dos segredos em prd/stg/dev injetados via GCP Secret Manager |
| **Método de medição** | Scan automático de segredos no CI (ex.: `gitleaks` ou similar) como gate bloqueante; Trivy scan de imagem verifica ausência de segredos embutidos |
| **Fonte de dados** | GitHub Actions CI (scan de segredos); Secret Manager audit log |
| **Escopo** | Todos os ambientes (dev/stg/prd); todos os serviços (.NET, Python/FastAPI, workers) |
| **Prioridade** | Alta |
| **Origem** | DEC-005; PRD §8.3; `.forge/rules/architecture/security-and-secrets.md` |
| **Critérios de aceite** | CI falha se segredo detectado em commit; `.gitignore` inclui `.env`; Terraform usa Secret Manager data sources para injeção em Cloud Run; rotação de segredos suportada sem downtime |

---

### NFR-SEG-06 — WAF e Rate Limiting de Borda via Cloud Armor

| Campo | Conteúdo |
|---|---|
| **Categoria** | Segurança |
| **Descrição** | Toda requisição externa ao sistema deve passar pelo Cloud Armor (WAF) antes de atingir a API, com regras de rate limiting por IP e por tenant para proteção contra abuso e ataques. |
| **Meta** | Cloud Armor configurado com regras OWASP CRS; rate limit de borda definido (threshold a estabelecer no TRD); requisições bloqueadas logadas no Cloud Logging |
| **Método de medição** | Verificação da configuração Cloud Armor no Terraform; teste de rate limit em stg; logs de bloqueio verificados |
| **Fonte de dados** | Terraform (configuração Cloud Armor); Cloud Logging (eventos de bloqueio) |
| **Escopo** | GCP Load Balancer → Cloud Armor → API REST |
| **Prioridade** | Alta |
| **Origem** | PRD §8.3 ("WAF + rate limit de borda via Cloud Armor") |
| **Critérios de aceite** | Cloud Armor configurado via Terraform antes do go-live; threshold de rate limit documentado no TRD; testes de OWASP ZAP em stg validam eficácia das regras |

---

### NFR-SEG-07 — CSP Estrita no Frontend

| Campo | Conteúdo |
|---|---|
| **Categoria** | Segurança |
| **Descrição** | O frontend React SPA deve servir uma Content Security Policy (CSP) estrita que previna XSS e injeção de scripts de terceiros não autorizados. |
| **Meta** | CSP sem `unsafe-inline` e sem `unsafe-eval` para scripts; nenhuma violação de CSP detectada nos testes E2E |
| **Método de medição** | Inspeção dos cabeçalhos HTTP da SPA em stg; verificação de ausência de violações no console do browser nos testes E2E (Playwright) |
| **Fonte de dados** | Cabeçalhos HTTP (inspeção via curl/Playwright); testes E2E |
| **Escopo** | SPA React servida via Cloud Storage + CDN; inclui integração com Identity Platform (redirect de OAuth deve ser permitido) |
| **Prioridade** | Alta |
| **Origem** | PRD §8.3 ("CSP estrita no frontend") |
| **Critérios de aceite** | Cabeçalho `Content-Security-Policy` presente na resposta da SPA; testes E2E passam sem violações de CSP; política documentada e versionada |

---

### NFR-SEG-08 — Scan de CVE em Imagens Docker

| Campo | Conteúdo |
|---|---|
| **Categoria** | Segurança |
| **Descrição** | Toda imagem Docker construída no CI/CD deve ser verificada por vulnerabilidades conhecidas antes do deploy. CVEs com fix disponível bloqueiam o build. |
| **Meta** | Zero CVEs Critical ou High com fix disponível em imagens que chegam a prd/stg; CVEs sem fix registrados em `.security-waivers.yml` com owner e data de reavaliação |
| **Método de medição** | Trivy scan no pipeline CI após build de imagem (gate bloqueante para Critical/High); relatório de scan arquivado como artefato do CI |
| **Fonte de dados** | GitHub Actions CI (relatório Trivy); `.security-waivers.yml` |
| **Escopo** | Todas as imagens Docker: API .NET, Worker de Digest, SPA (imagem de build), serviço de IA Python (Fase 3) |
| **Prioridade** | Alta |
| **Origem** | `.forge/rules/architecture/security-and-compliance.md` (Trivy obrigatório); `.forge/rules/testing/quality-gates.md` |
| **Critérios de aceite** | CI falha se Trivy detectar CVE Critical/High com fix sem waiver; waivers expirados também falham o CI; scan de OWASP ZAP semanal em stg complementa o scan de imagem |

### 8.6 Privacidade (PRIV)

---

### NFR-PRIV-01 — PII Ausente de Logs

| Campo | Conteúdo |
|---|---|
| **Categoria** | Privacidade |
| **Descrição** | Dados pessoais de contatos (nome, e-mail, celular) jamais devem aparecer em logs estruturados no Cloud Logging, seja em campos de log, mensagens de erro ou traces. |
| **Meta** | Zero ocorrências de PII de contatos (nome, e-mail, celular) detectáveis nos logs do Cloud Logging em produção e staging |
| **Método de medição** | Scan automatizado de logs em CI: queries no Cloud Logging usando padrões regex para formatos de e-mail e telefone brasileiro; revisão em pré-release; teste de integração que verifica ausência de PII nos logs após operações em contatos |
| **Fonte de dados** | Cloud Logging (queries de auditoria de PII) |
| **Escopo** | Todos os serviços (.NET API, Worker de Digest, Python/FastAPI); Cloud Logging; Cloud Trace |
| **Prioridade** | Alta |
| **Origem** | PRD §8.3 e §8.4 ("dados pessoais de contatos jamais presentes em logs"); LGPD Art. 6º (minimização); `.forge/rules/architecture/observability.md` |
| **Critérios de aceite** | Teste de integração: criar/editar contato com e-mail real; verificar que Cloud Logging não contém o e-mail; revisão de código obrigatória em PRs que tocam entidades de contato |

---

### NFR-PRIV-02 — Minimização de Dados Pessoais Coletados

| Campo | Conteúdo |
|---|---|
| **Categoria** | Privacidade |
| **Descrição** | O sistema deve coletar apenas os dados pessoais de contatos estritamente necessários para a operação comercial do tenant, sem campos adicionais de PII não justificados. |
| **Meta** | Campos de PII de contatos limitados ao mínimo operacional: nome, e-mail, celular e cargo — sem dados de identificação pessoal adicional sem base legal documentada |
| **Método de medição** | Revisão do schema do banco (RF-04); análise de novos campos em PRs via checklist de LGPD by design |
| **Fonte de dados** | Cloud SQL schema; documentação do FRD (RF-04) |
| **Escopo** | Entidade `Contact` (RF-04); formulários de cadastro de contato |
| **Prioridade** | Alta |
| **Origem** | PRD §8.4 ("minimização de dados pessoais coletados"); LGPD Art. 6º, I |
| **Critérios de aceite** | Schema `contacts` sem campos de PII além do conjunto mínimo (nome, e-mail, celular, cargo); checklist de LGPD by design incluído no template de PR para features que tocam dados de contato |

---

### NFR-PRIV-03 — Direito ao Esquecimento (Anonimização de Contatos)

| Campo | Conteúdo |
|---|---|
| **Categoria** | Privacidade |
| **Descrição** | O sistema deve fornecer mecanismo para anonimizar ou excluir dados pessoais de contatos sob demanda (direito ao esquecimento da LGPD), preservando a integridade do histórico comercial (oportunidades, atividades) com referência anonimizada. |
| **Meta** | Mecanismo de anonimização de contato executa em ≤ 30 s; após anonimização, zero campos de PII do contato recuperáveis no sistema; histórico comercial (oportunidades) preservado com referência `[contato removido]` |
| **Método de medição** | Teste de integração: anonimizar contato → verificar que nome, e-mail e celular retornam valor substituído (`[anonimizado]` ou similar) em todos os endpoints; verificar que oportunidades vinculadas permanecem acessíveis |
| **Fonte de dados** | Cloud SQL (verificação pós-anonimização); testes de integração |
| **Escopo** | RF-04 (Contas e Contatos); entidade `Contact`; mecanismo acionável pelo Tenant Admin |
| **Prioridade** | Alta |
| **Origem** | PRD §8.4 ("mecanismo de anonimização ou exclusão de dados pessoais de contatos a pedido"); LGPD Art. 18, IV |
| **Critérios de aceite** | Operação de anonimização registrada no AuditLog (quem solicitou, quando, qual contato — sem PII no log da operação); oportunidades vinculadas ao contato permanecem no banco com `contact_id` preservado mas dados pessoais substituídos |

---

### NFR-PRIV-04 — Base Legal Documentada para Dados Pessoais

| Campo | Conteúdo |
|---|---|
| **Categoria** | Privacidade |
| **Descrição** | Cada categoria de dado pessoal coletado pelo Azim deve ter base legal explícita documentada conforme LGPD Art. 7º, revisada pela equipe jurídica do tenant. |
| **Meta** | Documento de registro de atividades de tratamento (RAT) existente e revisado pela equipe jurídica da Vellus antes do go-live em produção; base legal para dados de contatos (nome, e-mail, celular) documentada |
| **Método de medição** | Verificação documental: RAT aprovado pela equipe jurídica da Vellus; gate humano no processo de go-live |
| **Fonte de dados** | Documento jurídico externo (RAT); aprovação registrada em `approvals.yaml` |
| **Escopo** | Dados de contatos de negociação (RF-04); dados de usuários da plataforma (RF-03) |
| **Prioridade** | Alta |
| **Origem** | PRD §8.4 ("base legal documentada — a confirmar com equipe jurídica da Vellus — LAC-07"); LGPD Art. 7º — alvo proposto a confirmar (ver PTV-03) |
| **Critérios de aceite** | RAT aprovado e registrado antes do go-live em prd; política de privacidade acessível aos usuários da plataforma; gate humano de aprovação jurídica em `approvals.yaml` |

---

### NFR-PRIV-05 — Bloqueio de Acesso de Platform Operator a Dados Comerciais

| Campo | Conteúdo |
|---|---|
| **Categoria** | Privacidade |
| **Descrição** | O perfil Platform Operator (P-05) deve ser bloqueado por padrão de acessar dados comerciais dos tenants (oportunidades, contatos, atividades, metas, comissões). Acesso autorizado apenas em situações de suporte explícitas, com auditoria. |
| **Meta** | 100% de requisições de Platform Operator a dados comerciais de qualquer tenant bloqueadas com HTTP 403 por padrão; acesso de suporte autorizado e registrado no AuditLog |
| **Método de medição** | Testes de integração RBAC: Platform Operator tenta acessar oportunidades de qualquer tenant → HTTP 403; verificação que qualquer acesso de suporte gera entrada no AuditLog |
| **Fonte de dados** | Testes de integração; AuditLog |
| **Escopo** | Todos os endpoints de dados comerciais (RF-04, RF-05, RF-06, RF-07, RF-08, RF-11); RBAC do Platform Operator |
| **Prioridade** | Alta |
| **Origem** | PRD §8.3 e §8.4 ("acesso a dados comerciais dos tenants bloqueado para Platform Operator por padrão"); PRD §3 (P-05) |
| **Critérios de aceite** | Teste RBAC explícito para Platform Operator em toda a matriz de permissões; qualquer acesso de suporte requer mecanismo de elevação temporária documentado no runbook |

---

### 8.7 Compliance (COMP)

---

### NFR-COMP-01 — Conformidade LGPD

| Campo | Conteúdo |
|---|---|
| **Categoria** | Compliance |
| **Descrição** | O Azim CRM deve estar em conformidade com a LGPD (Lei nº 13.709/2018) para todos os dados pessoais de contatos processados pelos tenants. A conformidade abrange: base legal, minimização, segurança, transparência e direitos dos titulares. |
| **Meta** | Aprovação de checklist de conformidade LGPD (baseado em NFR-PRIV-01 a NFR-PRIV-05) antes do go-live em produção; zero incidentes de violação de dados pessoais nos primeiros 90 dias |
| **Método de medição** | Checklist de LGPD by design revisado pela equipe jurídica; gate humano em `approvals.yaml`; monitoramento de acessos não autorizados via Cloud Logging |
| **Fonte de dados** | `approvals.yaml`; Cloud Logging; AuditLog |
| **Escopo** | Todo o sistema; especialmente RF-04 (Contatos), RF-01 (autenticação), RF-03 (RBAC) |
| **Prioridade** | Alta |
| **Origem** | PRD §8.4 e §8.8 (LGPD); LAC-07 (base legal — confirmação pendente com jurídico da Vellus) |
| **Critérios de aceite** | RAT aprovado (NFR-PRIV-04); mecanismo de anonimização funcional (NFR-PRIV-03); PII fora de logs verificado (NFR-PRIV-01); política de privacidade publicada; processo de resposta a requisições de titulares documentado |

---

### NFR-COMP-02 — Conformidade WCAG 2.1 Nível AA

| Campo | Conteúdo |
|---|---|
| **Categoria** | Compliance |
| **Descrição** | A interface do Azim CRM deve atender ao nível AA da WCAG 2.1, incluindo as cores customizadas de cada tenant (que variam dinamicamente). |
| **Meta** | Zero violações de contraste WCAG AA detectadas em testes automatizados (jest-axe, axe-core/Playwright); validação de contraste no upload de cores do tenant com rejeição de combinações que não atingem a meta mínima de contraste 4,5:1 |
| **Método de medição** | `jest-axe` em testes unitários de componentes; `@axe-core/playwright` em testes E2E; lógica de validação de contraste (WCAG AA 4,5:1) no endpoint de upload de cores do tenant |
| **Fonte de dados** | GitHub Actions CI (jest-axe, playwright); testes unitários de frontend |
| **Escopo** | SPA React (todos os componentes de UI); endpoint de upload de cores do tenant (RF-02) |
| **Prioridade** | Alta |
| **Origem** | PRD §8.5 e §8.8 ("WCAG 2.1 nível AA"); DEC-004 ("validação de contraste WCAG AA no upload") |
| **Critérios de aceite** | CI falha se `jest-axe` detectar violações de acessibilidade; upload de cor primária/secundária com contraste < 4,5:1 retorna HTTP 422 com mensagem orientativa; testes E2E de acessibilidade em fluxos críticos |

---

### NFR-COMP-03 — CI/CD sem Chave de Service Account (Workload Identity Federation)

| Campo | Conteúdo |
|---|---|
| **Categoria** | Compliance |
| **Descrição** | O pipeline de CI/CD não deve conter chaves de service account do GCP. A autenticação entre GitHub Actions e GCP deve ser feita exclusivamente via Workload Identity Federation (WIF). |
| **Meta** | Zero chaves de service account em repositório Git ou em secrets do GitHub; 100% dos deploys autenticados via WIF; auditoria de acesso ao GCP por pipeline rastreável no Cloud Audit Logs |
| **Método de medição** | Scan de secrets no CI (gitleaks) detecta tentativa de commit de chave de SA; inspeção da configuração GitHub Actions |
| **Fonte de dados** | GitHub Actions (configuração WIF); Cloud Audit Logs (identidade do caller) |
| **Escopo** | Pipeline CI/CD (GitHub Actions); todos os ambientes (dev/stg/prd) |
| **Prioridade** | Alta |
| **Origem** | DEC-005; PRD §5.1 ("GitHub Actions + Workload Identity Federation — sem chave de service account em repositório") |
| **Critérios de aceite** | Terraform configura WIF; GitHub Actions usa `google-github-actions/auth` com WIF; nenhum arquivo `.json` de credencial presente no repositório; deploy em prd requer aprovação manual |

---

### 8.8 Observabilidade (OBS)

---

### NFR-OBS-01 — Logs Estruturados com tenant_id e correlationId

| Campo | Conteúdo |
|---|---|
| **Categoria** | Observabilidade |
| **Descrição** | Todos os eventos de log de todos os serviços do Azim devem ser emitidos em formato JSON estruturado com campos obrigatórios, garantindo rastreabilidade por tenant e por requisição. |
| **Meta** | 100% dos logs em produção e staging em formato JSON; campos obrigatórios: `correlationId` (UUID v4), `tenantId`, `service`, `level`, `timestamp`; zero logs sem `tenantId` em contexto autenticado |
| **Método de medição** | Queries no Cloud Logging para verificar presença dos campos obrigatórios; teste de integração que verifica estrutura dos logs após cada operação de negócio |
| **Fonte de dados** | Cloud Logging (verificação de estrutura via queries) |
| **Escopo** | API REST .NET 10; Worker de Digest; serviço de IA Python/FastAPI (Fase 3); todos os eventos de domínio publicados |
| **Prioridade** | Alta |
| **Origem** | PRD §8.6 ("logs estruturados no Cloud Logging com tenant_id como label obrigatório"); `.forge/rules/architecture/observability.md` (correlationId em todos os logs) |
| **Critérios de aceite** | Teste de integração após login e após criação de oportunidade: log gerado contém `correlationId`, `tenantId`, `service`, `level`, `timestamp`; Cloud Logging filter confirma 100% dos logs com estrutura correta |
| **Dependência arquitetural** | ADR-0001 — `correlationId-e-tenant-id-propagation` (a ser criado via `adr-writer`) |

---

### NFR-OBS-02 — Métricas Técnicas no Cloud Monitoring

| Campo | Conteúdo |
|---|---|
| **Categoria** | Observabilidade |
| **Descrição** | Todos os serviços devem expor métricas técnicas que permitam monitoramento de latência (percentis), taxa de erro e volume de eventos de domínio. |
| **Meta** | Métricas obrigatórias por serviço: `http_requests_total` (por método, rota, status), `http_request_duration_seconds` (histograma com percentis p50/p95/p99), `domain_events_published_total` (por tipo de evento); dashboard provisionado no Cloud Monitoring para cada serviço em produção |
| **Método de medição** | Verificação no Cloud Monitoring que os dashboards existem e exibem dados em menos de 60 s após tráfego; alerta disparado corretamente em teste de injeção de falha |
| **Fonte de dados** | Cloud Monitoring |
| **Escopo** | API REST .NET 10; Worker de Digest; serviço de IA Python (Fase 3) |
| **Prioridade** | Alta |
| **Origem** | PRD §8.6 ("métricas técnicas e de negócio no Cloud Monitoring"); `.forge/rules/architecture/observability.md` (métricas obrigatórias por serviço) |
| **Critérios de aceite** | Nenhum serviço vai a produção sem métricas configuradas; dashboard provisionado via Terraform/IaC; alerta de 5xx e latência p95 configurados (NFR-OBS-04) |

---

### NFR-OBS-03 — Rastreamento Distribuído com tenant_id Propagado

| Campo | Conteúdo |
|---|---|
| **Categoria** | Observabilidade |
| **Descrição** | Toda requisição ao sistema deve gerar um trace distribuído no Cloud Trace com o `tenant_id` e `correlationId` propagados entre todos os serviços participantes. |
| **Meta** | 100% das requisições de negócio com trace completo no Cloud Trace; `tenant_id` como atributo de span em todos os spans; `correlationId` como atributo do span raiz; latência de instrumentação ≤ 1 ms por span |
| **Método de medição** | Verificação no Cloud Trace: trace de requisição de criação de oportunidade mostra spans para API, banco e Pub/Sub com `tenant_id` presente |
| **Fonte de dados** | Cloud Trace |
| **Escopo** | API REST → Cloud SQL; API REST → Pub/Sub; Cloud Scheduler → Worker → IEmailSender |
| **Prioridade** | Alta |
| **Origem** | PRD §8.6 ("rastreamento distribuído via Cloud Trace com tenant_id propagado entre serviços"); `.forge/rules/architecture/observability.md` (OpenTelemetry spans para handlers, chamadas HTTP e banco) |
| **Critérios de aceite** | OpenTelemetry configurado em .NET 10 e Python/FastAPI; `tenant_id` como atributo em todos os spans; busca por `correlationId` no Cloud Trace retorna o trace completo da requisição |
| **Dependência arquitetural** | ADR-0001 — `correlationId-e-tenant-id-propagation` (a ser criado via `adr-writer`) |

---

### NFR-OBS-04 — Alertas Operacionais Configurados

| Campo | Conteúdo |
|---|---|
| **Categoria** | Observabilidade |
| **Descrição** | O sistema deve ter alertas operacionais configurados no Cloud Monitoring para os cenários de falha crítica, com notificação automática para a equipe de operação. |
| **Meta** | Alertas configurados antes do go-live para: (1) taxa de erros 5xx > 1% por mais de 5 min; (2) latência p95 > 2 s por mais de 5 min; (3) falha de digest > 0 ocorrências em 1 h; (4) fila Pub/Sub com crescimento sem consumo por > 10 min; (5) violação de RLS detectada (qualquer ocorrência) |
| **Método de medição** | Verificação de configuração dos alertas no Cloud Monitoring antes do go-live; teste de cada alerta com injeção de condição de falha em stg |
| **Fonte de dados** | Cloud Monitoring (Alerting Policies); Cloud Logging (violação de RLS) |
| **Escopo** | Todos os serviços; especialmente API REST, Worker de Digest e Cloud SQL (RLS) |
| **Prioridade** | Alta |
| **Origem** | PRD §8.6 ("alertas operacionais configurados para: taxa de 5xx > 1%, latência p95 > 2s por mais de 5 minutos, falha de digest, fila Pub/Sub crescendo sem consumo, violação de RLS") |
| **Critérios de aceite** | Todos os 5 alertas testados em stg; notificação de oncall configurada (canal a definir — Slack, PagerDuty ou e-mail); alerta de RLS dispara incidente sev-1 automaticamente |

---

### NFR-OBS-05 — Retenção de Logs Operacionais

| Campo | Conteúdo |
|---|---|
| **Categoria** | Observabilidade |
| **Descrição** | Os logs operacionais do sistema (exceto dados de auditoria, que têm política própria em NFR-AUD-03) devem ser retidos por período suficiente para suporte, debugging e análise de incidentes. |
| **Meta** | Retenção mínima de 90 dias para logs operacionais no Cloud Logging — alvo proposto a confirmar; logs de segurança (acesso, autenticação, RLS) retidos por período superior (a definir, sugestão: 365 dias) |
| **Método de medição** | Verificação da configuração de retenção no Cloud Logging via Terraform; consulta a logs com mais de 90 dias deve retornar resultados |
| **Fonte de dados** | Cloud Logging (configuração de retenção) |
| **Escopo** | Cloud Logging; todos os serviços |
| **Prioridade** | Média |
| **Origem** | PRD §8.6 ("retenção mínima de logs e eventos: política a definir no nfrd.md (referência: 90 dias)") — alvo proposto a confirmar (ver PTV-04) |
| **Critérios de aceite** | Terraform configura retenção de 90 dias (ou valor confirmado) no Cloud Logging antes do go-live; política documentada e aprovada |
| **Dependência arquitetural** | ADR-0003 — `politica-retencao-logs-e-auditoria` (a ser criado via `adr-writer`) |

### 8.9 Auditoria (AUD)

---

### NFR-AUD-01 — AuditLog Imutável para Toda Escrita em Entidade de Negócio

| Campo | Conteúdo |
|---|---|
| **Categoria** | Auditoria |
| **Descrição** | Toda operação de escrita (criação, edição, transição de estágio, encerramento) em entidade de negócio (oportunidade, conta, contato, parceiro, meta, atividade) deve gerar um registro imutável no `AuditLog`, incluindo autor, delta JSON e timestamp. A imutabilidade é enforced no banco via REVOKE + trigger BEFORE UPDATE/DELETE. |
| **Meta** | 100% das escritas em entidades de negócio registradas no AuditLog; zero registros de AuditLog alterados ou excluídos (trigger bloqueia qualquer tentativa); snapshot de comissão imutável gerado ao mover para "Ganho" |
| **Método de medição** | Teste de integração: tentar UPDATE na tabela `audit_log` via conexão com role `app` deve retornar exceção Postgres; verificar que toda operação de negócio gera exatamente 1 registro no AuditLog |
| **Fonte de dados** | Cloud SQL (trigger e REVOKE verificados); testes de integração com Testcontainers |
| **Escopo** | Entidades de negócio: `opportunities`, `accounts`, `contacts`, `partners`, `goals`, `activities`, `opportunity_partner_commissions`; tabela `audit_log` |
| **Prioridade** | Alta |
| **Origem** | PRD §8.3 e §8.6 ("AuditLog imutável — toda escrita em entidade de negócio registra autor, delta JSON, timestamp e tenant_id"); `.forge/rules/domain/audit-immutability.md` |
| **Critérios de aceite** | Migration de criação do `audit_log` inclui trigger `trg_audit_log_immutable` + REVOKE UPDATE/DELETE/TRUNCATE FROM app; teste de integração verifica imutabilidade; código de aplicação não usa DbSet.Remove() em entidades de auditoria |

---

### NFR-AUD-02 — EmailDigestLog como Trilha de Idempotência

| Campo | Conteúdo |
|---|---|
| **Categoria** | Auditoria |
| **Descrição** | O `EmailDigestLog` deve registrar cada tentativa de envio de digest (status: agendado, enviado, entregue, falha), servindo simultaneamente como trilha de idempotência (impede duplicatas) e como evidência de entrega por usuário e data para o KPI-03 e KPI-04. |
| **Meta** | Exatamente 1 registro por (usuário, data) no EmailDigestLog, independentemente do número de retentativas; status atualizado em cada evento do webhook do provider |
| **Método de medição** | Consulta ao EmailDigestLog após re-tentativa simulada: COUNT = 1 por (user_id, digest_date); webhooks do provider de e-mail atualizando status verificados em teste de integração |
| **Fonte de dados** | Cloud SQL (EmailDigestLog); webhooks do provider de e-mail (Postmark/SendGrid) |
| **Escopo** | Worker de Digest; endpoint de webhook de eventos de e-mail; KPI-03 e KPI-04 |
| **Prioridade** | Alta |
| **Origem** | PRD §8.6 ("EmailDigestLog como trilha de idempotência e evidência de entrega por usuário e data"); J-02 |
| **Critérios de aceite** | Teste: disparar digest 2x para o mesmo usuário no mesmo dia → 1 e-mail enviado, 2 tentativas no EmailDigestLog (1 entregue + 1 bloqueada por idempotência); KPI-03 calculável a partir dos dados do EmailDigestLog |

---

### NFR-AUD-03 — Retenção de Dados de Auditoria

| Campo | Conteúdo |
|---|---|
| **Categoria** | Auditoria |
| **Descrição** | Dados de auditoria (AuditLog, EmailDigestLog, snapshot de comissão) não devem ter TTL ou purge automático. Retenção é indefinida exceto por obrigação legal explícita (LGPD — direito ao esquecimento), que exige role administrativo separado e aprovação dupla. |
| **Meta** | Zero registros de AuditLog removidos automaticamente; purge legal somente via role `app_admin` com registro prévio em `audit_purge_log` e aprovação de 2 membros da equipe; compliance com obrigação de retenção mínima de dados de auditoria (período mínimo a confirmar com jurídico — ver PTV-04) |
| **Método de medição** | Verificação que não existe job de purge automático; verificação que role `app` não possui DELETE em tabelas de auditoria; documentação do processo de purge legal |
| **Fonte de dados** | Cloud SQL (verificação de permissões); runbook operacional |
| **Escopo** | Tabelas: `audit_log`, `email_digest_log`, `opportunity_partner_commission` (snapshot) |
| **Prioridade** | Alta |
| **Origem** | `.forge/rules/domain/audit-immutability.md` ("retenção regulatória obrigatória — não usar TTL em tabelas de auditoria"); PRD §8.6; Inferência Não Funcional (período mínimo) |
| **Critérios de aceite** | Nenhum job de purge automático configurado para tabelas de auditoria; processo de purge legal documentado com aprovação dupla; política de retenção aprovada pela equipe jurídica |
| **Dependência arquitetural** | ADR-0003 — `politica-retencao-logs-e-auditoria` (a ser criado via `adr-writer`) |

---

### 8.10 Interoperabilidade (INT)

---

### NFR-INT-01 — Versionamento de API REST

| Campo | Conteúdo |
|---|---|
| **Categoria** | Interoperabilidade |
| **Descrição** | A API REST do Azim deve ser versionada explicitamente via prefixo de path. Breaking changes devem criar nova versão sem modificar a versão existente, garantindo retrocompatibilidade para clientes durante o período de deprecação. |
| **Meta** | 100% dos endpoints com prefixo de versão (`/api/v1/`); zero breaking changes na versão corrente sem nova versão; versões antigas mantidas com aviso de deprecação por no mínimo 90 dias |
| **Método de medição** | Inspeção do OpenAPI gerado; testes de contrato (Pact) verificam que mudanças de contrato não quebram consumidores existentes |
| **Fonte de dados** | OpenAPI spec; GitHub Actions CI (testes de contrato) |
| **Escopo** | Todos os endpoints da API REST .NET 10 |
| **Prioridade** | Média |
| **Origem** | `.forge/rules/architecture/api-and-contracts.md` ("toda API DEVE ser versionada: /api/v1/; breaking changes criam nova versão") |
| **Critérios de aceite** | OpenAPI spec exportado e versionado em `contracts/openapi/`; CI valida que spec não teve breaking changes sem bump de versão; testes de contrato Pact configurados para integração frontend–backend |

---

### NFR-INT-02 — Abstração IEmailSender para Portabilidade de Provider

| Campo | Conteúdo |
|---|---|
| **Categoria** | Interoperabilidade |
| **Descrição** | O sistema deve abstrair o provider de e-mail transacional por trás de uma interface `IEmailSender`, permitindo trocar o provider (Postmark → SendGrid ou outro) sem modificar o código de negócio. |
| **Meta** | Troca de provider de e-mail requer mudança apenas na implementação concreta de `IEmailSender` e na configuração — zero mudanças em código de negócio; tempo de troca estimado: < 1 sprint |
| **Método de medição** | Revisão de código: nenhuma referência direta ao Postmark SDK fora da implementação concreta de `IEmailSender`; teste unitário do Worker de Digest usa mock de `IEmailSender` |
| **Fonte de dados** | Revisão de código; testes unitários |
| **Escopo** | RF-09 (Digest); RF-01 (convites por e-mail); toda notificação por e-mail |
| **Prioridade** | Alta |
| **Origem** | DEC-008 ("Postmark como candidato primário atrás da abstração IEmailSender; SendGrid como alternativa avaliada no spike"); LAC-03 |
| **Critérios de aceite** | Interface `IEmailSender` com método(s) documentados; implementação Postmark em classe separada; testes do Worker usam `IEmailSender` mockado; configuração do provider via Secret Manager (nunca hardcoded) |

---

### NFR-INT-03 — SPF, DKIM e DMARC Validados Antes do Primeiro Envio

| Campo | Conteúdo |
|---|---|
| **Categoria** | Interoperabilidade |
| **Descrição** | O domínio de envio `mail.azim.com.br` deve ter SPF, DKIM e DMARC configurados e validados no ambiente de produção antes de qualquer envio de e-mail, garantindo entregabilidade mínima para o KPI-03 (≥ 98%). |
| **Meta** | SPF, DKIM e DMARC válidos e verificáveis por ferramenta externa (ex.: MXToolbox) antes do go-live em prd; bounce rate < 5% nos primeiros 30 dias (KPI-03 implica entregabilidade ≥ 98%) |
| **Método de medição** | Verificação de DNS via MXToolbox ou similar antes do go-live; monitoramento de bounce rate via webhooks do provider de e-mail |
| **Fonte de dados** | DNS do domínio azim.com.br (verificação externa); webhooks do provider (bounce events) |
| **Escopo** | Domínio `mail.azim.com.br`; configuração DNS; provider de e-mail (Postmark ou SendGrid) |
| **Prioridade** | Alta |
| **Origem** | DEC-008; PRD §5.1 (Fase 0 — "e-mail transacional configurado com SPF/DKIM/DMARC validados no ambiente prd"); PRM-03; RISCO-T02 |
| **Critérios de aceite** | Gate de go-live: verificação de SPF/DKIM/DMARC como critério de conclusão da Fase 0; relatório de entregabilidade nos primeiros 7 dias de produção |

---

### 8.11 Usabilidade e Acessibilidade (USA)

---

### NFR-USA-01 — Interface Responsiva

| Campo | Conteúdo |
|---|---|
| **Categoria** | Usabilidade e Acessibilidade |
| **Descrição** | A interface web do Azim CRM deve ser utilizável em desktop e tablet. O acesso mobile no MVP é coberto pelo digest via e-mail; não há requisito de PWA ou responsividade para smartphones no MVP. |
| **Meta** | Interface funcional e sem sobreposição de elementos em viewport ≥ 1280 px (desktop) e ≥ 768 px (tablet); Kanban drag-and-drop utilizável em mouse e touchpad |
| **Método de medição** | Testes de regressão visual (Playwright snapshots) em viewports de 1280 px e 768 px; verificação de drag-and-drop em touchpad nos testes E2E |
| **Fonte de dados** | GitHub Actions CI (Playwright visual regression); testes E2E |
| **Escopo** | SPA React — todas as telas; especialmente Kanban (RF-06) |
| **Prioridade** | Alta |
| **Origem** | PRD §8.5 ("interface responsiva compatível com desktop >= 1280px e tablet >= 768px") |
| **Critérios de aceite** | Testes de regressão visual passam em ambos os viewports; nenhum elemento visualmente sobreposto; Kanban drag-and-drop funciona em viewport de 768 px |

---

### NFR-USA-02 — Validação de Contraste WCAG AA nas Cores do Tenant

| Campo | Conteúdo |
|---|---|
| **Categoria** | Usabilidade e Acessibilidade |
| **Descrição** | Quando o administrador do tenant faz upload de cores primária e secundária, o sistema deve validar automaticamente se a combinação de cores atinge o contraste mínimo WCAG 2.1 AA (4,5:1 para texto normal, 3:1 para texto grande) antes de aceitar. |
| **Meta** | Upload de cor com razão de contraste < 4,5:1 para texto normal retorna HTTP 422 com mensagem clara; 100% dos tenants em produção com cores dentro do threshold WCAG AA |
| **Método de medição** | Teste unitário do endpoint de upload de cor com valores limítrofes (4,4:1 → rejeitado; 4,5:1 → aceito; 4,6:1 → aceito); `jest-axe` nos componentes de UI com as cores do tenant |
| **Fonte de dados** | Testes unitários; testes de integração do endpoint de configuração de branding |
| **Escopo** | RF-02 (Administração do Tenant — configuração de cor primária e secundária); todos os componentes de UI que usam as cores do tenant |
| **Prioridade** | Alta |
| **Origem** | PRD §8.5 ("contraste WCAG 2.1 nível AA mínimo, inclusive para as cores customizadas de cada tenant — validado no upload"); DEC-004; NFR-COMP-02 |
| **Critérios de aceite** | Endpoint de upload valida contraste e retorna mensagem orientativa; testes com paletas de cores reais (incluindo as da Vellus) verificados antes do go-live |

---

### NFR-USA-03 — Fluxos Críticos em no Máximo 5 Passos

| Campo | Conteúdo |
|---|---|
| **Categoria** | Usabilidade e Acessibilidade |
| **Descrição** | Os fluxos críticos do produto — criar oportunidade, mover estágio, registrar atividade — devem ser executáveis em no máximo 5 passos a partir do Kanban, sem necessidade de navegar para outras telas. |
| **Meta** | Criar oportunidade: ≤ 5 passos a partir do Kanban; mover oportunidade de estágio (drag-and-drop): ≤ 2 passos; registrar atividade concluída: ≤ 3 passos |
| **Método de medição** | Contagem de passos (cliques/ações) nos testes E2E de cada fluxo; revisão de UX contra checklist de passos |
| **Fonte de dados** | Testes E2E (Playwright); revisão de UX |
| **Escopo** | RF-06 (Pipeline — criação e transição de estágio); RF-07 (Atividades — conclusão 1 clique) |
| **Prioridade** | Alta |
| **Origem** | PRD §8.5 ("fluxos críticos executáveis em no máximo 5 passos a partir do Kanban") |
| **Critérios de aceite** | Testes E2E documentam a contagem de passos; nenhum fluxo crítico excede o limite; UX review antes do go-live |

---

### NFR-USA-04 — i18n-Ready (PT-BR Nativo)

| Campo | Conteúdo |
|---|---|
| **Categoria** | Usabilidade e Acessibilidade |
| **Descrição** | A interface deve usar PT-BR como idioma nativo e a arquitetura de strings deve suportar internacionalização futura sem reescrita, mediante externalização de todos os textos de UI em arquivos de strings. |
| **Meta** | 100% das strings de UI externalizadas em arquivos de i18n (ex.: `pt-BR.json`); zero strings hardcoded em componentes React; estrutura suporta adição de novo idioma (ex.: `en-US`) sem mudança de componentes |
| **Método de medição** | ESLint rule de i18n (lint check em CI) verifica ausência de strings hardcoded em componentes; inspeção manual pré-release |
| **Fonte de dados** | GitHub Actions CI (ESLint i18n rule); revisão de código |
| **Escopo** | SPA React — todos os componentes; e-mails de digest (templates externalizados) |
| **Prioridade** | Média |
| **Origem** | Inferência Não Funcional (PRD usa PT-BR; produto é brasileiro; internacionalização futura é esperada para expansão SaaS) |
| **Critérios de aceite** | Estrutura de i18n presente desde o início do projeto; arquivo `pt-BR.json` como única fonte de strings; adição de idioma futuro requer apenas novo arquivo JSON e configuração |

---

### 8.12 Manutenibilidade (MAN)

---

### NFR-MAN-01 — Cobertura de Testes por Camada

| Campo | Conteúdo |
|---|---|
| **Categoria** | Manutenibilidade |
| **Descrição** | Cada camada do sistema deve atingir os thresholds mínimos de cobertura de código, com gates de CI bloqueantes. A cobertura é proxy — o objetivo é cobertura de comportamentos de negócio. |
| **Meta** | Domain: ≥ 95% de linha / ≥ 90% de branch; Application: ≥ 85% / ≥ 80%; Infrastructure: ≥ 70%; Frontend (features/): ≥ 80% / ≥ 75% |
| **Método de medição** | Relatórios de cobertura gerados no CI (Coverlet para .NET, Istanbul para Jest); gate bloqueante se thresholds não atingidos |
| **Fonte de dados** | GitHub Actions CI (Coverlet, Istanbul) |
| **Escopo** | Backend .NET 10 (todas as camadas); Frontend React (features/) |
| **Prioridade** | Alta |
| **Origem** | `.forge/rules/testing/quality-gates.md` (Coverage Thresholds Mínimos por Camada) |
| **Critérios de aceite** | CI falha se thresholds não atingidos em qualquer camada; relatório de cobertura gerado como artefato em todo PR; nenhuma lógica financeira sem teste determinístico |

---

### NFR-MAN-02 — Testes de Isolamento de Tenant como Gate Obrigatório de CI

| Campo | Conteúdo |
|---|---|
| **Categoria** | Manutenibilidade |
| **Descrição** | Existe um conjunto de testes automatizados dedicados a verificar o isolamento de dados entre tenants. Esses testes são um gate obrigatório de build: nenhum merge para main é permitido sem sua aprovação. |
| **Meta** | 100% de aprovação da suite de isolamento em todo build (KPI-06); suite cobre: API sem contexto de tenant → HTTP 401/403; API com tenant A → não retorna dados de tenant B; SQL direto com role `app` sem RLS → dados vazios ou erro |
| **Método de medição** | GitHub Actions CI — job de isolamento como dependency obrigatória do job de merge; Testcontainers com Postgres real |
| **Fonte de dados** | GitHub Actions CI pipeline |
| **Escopo** | Middleware de resolução de tenant; EF Core global query filter; RLS Postgres |
| **Prioridade** | Alta |
| **Origem** | DEC-006; OBJ-05; KPI-06; PRD §8.3 ("testes de isolamento em CI como gate obrigatório — DEC-006") |
| **Critérios de aceite** | CI bloqueado em falha de qualquer teste de isolamento; code review obrigatório em PRs que tocam middleware de tenant; 0 regressões em produção |

---

### NFR-MAN-03 — Property-Based Testing para Operações Monetárias

| Campo | Conteúdo |
|---|---|
| **Categoria** | Manutenibilidade |
| **Descrição** | Toda operação que envolva cálculo monetário (comissão de parceiro, forecast ponderado, agregações de pipeline) deve ter testes property-based que verifiquem propriedades matemáticas invariantes. |
| **Meta** | Operações de comissão (`pct_setup × valor_setup`, `pct_recorrente × valor_mensal × meses`) e de forecast (`valor_total × probabilidade`) cobertas por testes FsCheck com ≥ 1.000 exemplos gerados; propriedades: não-negatividade, corretude para valores limite (0, Long.MaxValue) |
| **Método de medição** | FsCheck (xUnit) no backend .NET; fast-check no frontend (se cálculo for feito no cliente) |
| **Fonte de dados** | GitHub Actions CI (relatório FsCheck) |
| **Escopo** | Domain layer: `OpportunityPartnerCommission`, cálculo de `forecast_ponderado`, cálculo de `valor_total`; conversão de centavos inteiros para exibição |
| **Prioridade** | Alta |
| **Origem** | `.forge/rules/testing/quality-gates.md` (PBT obrigatório para operações monetárias); DEC-011 (valores como centavos inteiros) |
| **Critérios de aceite** | CI inclui testes PBT; falha de propriedade impede merge; nenhuma lógica financeira sem PBT correspondente |

---

### NFR-MAN-04 — Infraestrutura 100% como Código (Terraform)

| Campo | Conteúdo |
|---|---|
| **Categoria** | Manutenibilidade |
| **Descrição** | Toda a infraestrutura GCP deve ser provisionada e gerenciada exclusivamente via Terraform. Nenhuma configuração manual de infraestrutura é permitida em stg ou prd. |
| **Meta** | Zero recursos de infraestrutura em stg/prd não gerenciados pelo Terraform; `terraform plan` em CI detecta drift de configuração; apply em prd requer aprovação manual |
| **Método de medição** | `terraform plan` com saída `0 to add, 0 to change, 0 to destroy` após apply indica zero drift; CI executa plan em todo PR de Terraform |
| **Fonte de dados** | GitHub Actions CI (Terraform plan); GCP Resource Manager |
| **Escopo** | Toda a infraestrutura GCP: Cloud SQL, Memorystore, Cloud Run, Pub/Sub, Cloud Scheduler, Cloud Armor, Secret Manager, Identity Platform, Cloud Storage |
| **Prioridade** | Alta |
| **Origem** | DEC-005 ("GCP 100% Terraform; CI executa plan em PR; apply com aprovação manual em prd") |
| **Critérios de aceite** | Nenhum recurso GCP criado manualmente em stg/prd; CI executa `terraform plan` em todo PR; apply em prd via pipeline com step de aprovação manual |

---

### 8.13 Portabilidade (POR)

---

### NFR-POR-01 — Paridade entre Ambientes (dev / stg / prd)

| Campo | Conteúdo |
|---|---|
| **Categoria** | Portabilidade |
| **Descrição** | Os três ambientes (dev, stg, prd) devem ser provisionados com a mesma configuração de infraestrutura via Terraform, diferindo apenas em capacidade (tamanho de instâncias) e dados. |
| **Meta** | Diff entre `terraform plan` de stg e prd: apenas variáveis de capacidade (tamanho da instância Cloud SQL, número de workers Cloud Run) — zero diferenças estruturais; deploys promovidos de stg → prd sem modificação de código |
| **Método de medição** | Revisão do Terraform: módulos compartilhados entre ambientes com variáveis de ambiente como único diferencial; pipeline de promoção stg → prd sem rebuilds |
| **Fonte de dados** | Repositório Terraform; GitHub Actions pipeline |
| **Escopo** | Toda a infraestrutura GCP nos 3 ambientes |
| **Prioridade** | Alta |
| **Origem** | DEC-005 ("três ambientes dev/stg/prd no GCP"); PRD §5.1 Fase 0 |
| **Critérios de aceite** | Deploy em prd usa a mesma imagem Docker validada em stg; migrations EF Core executadas como job de release antes do tráfego; testes de isolamento de tenant executam em stg antes da promoção |

---

### NFR-POR-02 — Residência de Dados no Brasil

| Campo | Conteúdo |
|---|---|
| **Categoria** | Portabilidade |
| **Descrição** | Todos os dados de negócio e pessoais processados pelo Azim CRM devem residir exclusivamente em região GCP do Brasil, em conformidade com a LGPD e com as expectativas de soberania de dados dos tenants. |
| **Meta** | Cloud SQL, Memorystore, Cloud Storage e todos os serviços de processamento de dados provisionados exclusivamente em `southamerica-east1` (São Paulo); zero replicação de dados pessoais ou comerciais para fora do Brasil sem decisão explícita documentada |
| **Método de medição** | Inspeção do Terraform: todos os recursos com `location = southamerica-east1`; verificação via GCP Console ou `gcloud` |
| **Fonte de dados** | Terraform; GCP Resource Manager |
| **Escopo** | Cloud SQL (dados de negócio e PII); Memorystore (sessão e cache); Cloud Storage (assets de branding); Cloud Logging (logs com possível PII) |
| **Prioridade** | Alta |
| **Origem** | DEC-005 ("região primária: southamerica-east1"); PRD §5.1; LGPD (soberania de dados) |
| **Critérios de aceite** | Terraform review verifica que todos os recursos estão em `southamerica-east1`; qualquer proposta de replicação cross-region requer ADR aprovado explicitamente |

---

### 8.14 Operabilidade (OPS)

---

### NFR-OPS-01 — PITR Cloud SQL (RPO e RTO)

| Campo | Conteúdo |
|---|---|
| **Categoria** | Operabilidade |
| **Descrição** | O banco de dados Cloud SQL deve ter Point-In-Time Recovery (PITR) ativo, garantindo os objetivos de ponto de recuperação (RPO) e tempo de recuperação (RTO) especificados. |
| **Meta** | RPO ≤ 5 min (perda máxima de dados em caso de falha); RTO ≤ 4 h (tempo máximo para restauração completa do serviço) |
| **Método de medição** | Configuração PITR verificada via Terraform; drill de restore trimestral (NFR-OPS-02) mede o RTO real; Cloud SQL backup status monitorado |
| **Fonte de dados** | Terraform (configuração Cloud SQL com PITR); Cloud SQL backup dashboard; drill de restore |
| **Escopo** | Cloud SQL / Postgres (instância de produção) |
| **Prioridade** | Alta |
| **Origem** | PRD §8.1 ("RPO <= 5 min e RTO <= 4 h para Cloud SQL (PITR ativo) como referência aprovada no azim-product-spec.md") — **meta sourced** (referência ao azim-product-spec.md aprovado, diferente dos demais alvos propostos) |
| **Critérios de aceite** | Terraform configura PITR com retenção de backups; Cloud Monitoring alerta para falha de backup; RTO ≤ 4 h verificado no drill trimestral |

---

### NFR-OPS-02 — Drill de Restore Periódico

| Campo | Conteúdo |
|---|---|
| **Categoria** | Operabilidade |
| **Descrição** | O restore a partir do backup PITR deve ser testado periodicamente para garantir que o RTO declarado em NFR-OPS-01 é atingível na prática, e que o processo de recuperação está documentado e praticado. |
| **Meta** | Restore drill executado ao menos 1 vez por trimestre em ambiente de stg; RTO real medido e comparado com a meta de 4 h; resultado documentado no runbook operacional |
| **Método de medição** | Execução do restore drill: restaurar backup PITR em ambiente de stg, verificar integridade dos dados e medir tempo total; resultado registrado em documento de DR |
| **Fonte de dados** | Runbook operacional (histórico de drills); stg (resultado do restore) |
| **Escopo** | Cloud SQL prd → restore em stg isolado |
| **Prioridade** | Média |
| **Origem** | Inferência Não Funcional (PRD §8.1 menciona "restore testado" como parte da referência aprovada; frequência trimestral é inferência do NFRD) |
| **Critérios de aceite** | Primeiro drill executado antes do go-live em prd; resultado documentado; qualquer falha no drill gera incidente de prioridade Média |

---

### NFR-OPS-03 — Pontualidade do Digest Diário

| Campo | Conteúdo |
|---|---|
| **Categoria** | Operabilidade |
| **Descrição** | O e-mail de digest deve ser entregue dentro da janela de pontualidade especificada. Atraso sistêmico além da janela é considerado incidente de Tier 1 (afeta o diferencial competitivo central do produto). |
| **Meta** | E-mail de digest entregue na janela 07:00–07:05 BRT para ≥ 98% dos destinatários elegíveis em dias úteis (segunda a sexta); atraso > 5 min é incidente de operação (alerta automático) |
| **Método de medição** | Timestamp de entrega registrado via webhook do provider (evento `delivered`) vs horário configurado do tenant; métrica customizada `digest_delivery_delay_seconds` no Cloud Monitoring |
| **Fonte de dados** | EmailDigestLog (timestamp de envio vs horário do tenant); webhooks do provider (evento `delivered`) |
| **Escopo** | Worker de Digest; Cloud Scheduler; IEmailSender; provider de e-mail |
| **Prioridade** | Alta |
| **Origem** | PRD RF-09; US-01 ("e-mail enviado entre 07:00 e 07:05 BRT em dias úteis"); OBJ-03 |
| **Critérios de aceite** | Alerta Cloud Monitoring configurado para delay > 5 min; KPI-03 (taxa de entrega ≥ 98%) medido via EmailDigestLog; monitoramento de fila Pub/Sub detecta acúmulo antes do atraso (NFR-OBS-04) |

---

### NFR-OPS-04 — Política de Retenção de Dados de Negócio e Usuário

| Campo | Conteúdo |
|---|---|
| **Categoria** | Operabilidade |
| **Descrição** | O sistema deve ter uma política documentada de retenção de dados de negócio (oportunidades, atividades, contas encerradas) e de dados de usuário (contas desativadas), compatível com a LGPD e com os requisitos de histórico comercial dos tenants. |
| **Meta** | Política de retenção documentada e aprovada antes do go-live em produção; dados de oportunidades encerradas retidos por no mínimo X anos (a confirmar com jurídico — ver PTV-05); conta de usuário desativada preserva histórico com FKs intactas (sem exclusão física) — alvo proposto a confirmar |
| **Método de medição** | Verificação documental: política de retenção aprovada; teste de integração que verifica que desativação de usuário preserva todo o histórico (oportunidades, atividades, AuditLog com `owner_id`) |
| **Fonte de dados** | Documentação jurídica; testes de integração |
| **Escopo** | Todas as entidades de negócio: `opportunities`, `activities`, `accounts`, `contacts`; entidade `users` (desativação sem exclusão física — PRD RF-03) |
| **Prioridade** | Alta |
| **Origem** | PRD §8.4 ("retenção controlada: política de retenção de dados a ser definida no nfrd.md"); LAC-07 — alvo proposto a confirmar (ver PTV-05) |
| **Critérios de aceite** | Política de retenção documentada, aprovada pela equipe jurídica e publicada antes do go-live; teste: desativar usuário → histórico completo acessível via AuditLog e relatórios; nenhuma exclusão física de dados de negócio sem processo aprovado |
| **Dependência arquitetural** | ADR-0003 — `politica-retencao-logs-e-auditoria` (a ser criado via `adr-writer`) |

---

## 9. Restrições Técnicas Não Funcionais

As restrições abaixo são condições obrigatórias, não metas — devem ser atendidas por design, sem possibilidade de waiver sem ADR formal.

| Código | Restrição | Origem |
|---|---|---|
| REST-NF-01 | Dados pessoais de contatos (nome, e-mail, celular) **jamais** presentes em logs no Cloud Logging, sem exceção | PRD §8.3/§8.4; `.forge/rules/architecture/observability.md` |
| REST-NF-02 | Nenhuma senha de usuário armazenada ou processada pelo sistema Azim (delegação integral ao Identity Platform) | DEC-005; PRD §8.3 |
| REST-NF-03 | Nenhum segredo (API key, credencial de banco, token) em código-fonte, histórico Git, imagem Docker ou variável de ambiente não gerenciada | DEC-005; `.forge/rules/architecture/security-and-secrets.md` |
| REST-NF-04 | TLS ≥ 1.2 obrigatório em todo tráfego externo e interno; TLS 1.0 e 1.1 desabilitados | PRD §8.3; `.forge/rules/architecture/security-and-compliance.md` |
| REST-NF-05 | Valores monetários armazenados como centavos inteiros (`bigint`) em todas as entidades do modelo de dados — nunca `float` ou `decimal` | DEC-011; `.forge/rules/domain/money-as-cents.md` |
| REST-NF-06 | Tabelas de auditoria (`audit_log`, `email_digest_log`, `opportunity_partner_commission`) são append-only; REVOKE UPDATE/DELETE/TRUNCATE + trigger BEFORE obrigatório | `.forge/rules/domain/audit-immutability.md` |
| REST-NF-07 | Todos os dados de negócio e PII residem exclusivamente em `southamerica-east1` (GCP); sem replicação cross-region sem ADR | DEC-005; LGPD |
| REST-NF-08 | CI/CD autenticado exclusivamente via Workload Identity Federation — zero chaves de service account em repositório | DEC-005; PRD §5.1 |
| REST-NF-09 | Automações (Fase 2) e digest executam exclusivamente via Pub/Sub assíncrono — nunca síncrono na API de negócio | PRD RF-12; DEC-009 |
| REST-NF-10 | Nenhum recurso de infraestrutura em stg/prd criado manualmente fora do Terraform | DEC-005 |
| REST-NF-11 | Rate-limit de IA (Fase 3) por tenant via Redis; dados de IA de um tenant jamais cruzam para outro tenant | PRD RF-13; DEC-006 |

---

## 10. Matriz de Rastreabilidade PRD → NFRD

| Item do PRD | NFRs relacionados | Cobertura |
|---|---|---|
| OBJ-01 — Eliminar operação em planilha | NFR-PERF-02, NFR-PERF-03, NFR-DISP-01, NFR-SEG-01, NFR-SEG-03, NFR-MAN-02 | Completa |
| OBJ-02 — Comissão de parceiro rastreável | NFR-AUD-01, NFR-AUD-03, NFR-MAN-03, REST-NF-05 | Completa |
| OBJ-03 — Digest diário automatizado | NFR-PERF-05, NFR-DISP-01, NFR-RES-01, NFR-RES-02, NFR-INT-02, NFR-INT-03, NFR-OPS-03, NFR-ESC-04 | Completa |
| OBJ-04 — Visibilidade de forecast e metas | NFR-PERF-07, NFR-RES-03, NFR-DISP-02 | Completa |
| OBJ-05 — Base multi-tenant verificada | NFR-SEG-01, NFR-MAN-02, NFR-POR-02, NFR-ESC-01, NFR-PRIV-05 | Completa |
| PRD §8.1 — Disponibilidade por tier | NFR-DISP-01, NFR-DISP-02, NFR-DISP-03, NFR-DISP-04 | Completa (alvos a confirmar) |
| PRD §8.2 — Performance | NFR-PERF-01 a NFR-PERF-07 | Completa (alvos a confirmar) |
| PRD §8.3 — Segurança | NFR-SEG-01 a NFR-SEG-08, NFR-PRIV-05, REST-NF-01 a REST-NF-08 | Completa |
| PRD §8.4 — Privacidade LGPD | NFR-PRIV-01 a NFR-PRIV-05, NFR-COMP-01, NFR-OPS-04 | Completa (LAC-07 em aberto) |
| PRD §8.5 — Usabilidade e Acessibilidade | NFR-USA-01 a NFR-USA-04, NFR-COMP-02 | Completa |
| PRD §8.6 — Observabilidade | NFR-OBS-01 a NFR-OBS-05, NFR-AUD-01, NFR-AUD-02 | Completa |
| PRD §8.7 — Escalabilidade | NFR-ESC-01 a NFR-ESC-04 | Completa |
| PRD §8.8 — Compliance | NFR-COMP-01, NFR-COMP-02, NFR-COMP-03, NFR-PRIV-01 a NFR-PRIV-05 | Completa |
| DEC-001 — Estágios configuráveis | NFR-AUD-01 (auditoria de transições de estágio) | Parcial (funcional → FRD) |
| DEC-002 — Snapshot de comissão imutável | NFR-AUD-01, NFR-AUD-03, NFR-MAN-03, REST-NF-06 | Completa |
| DEC-004 — White-label estrito | NFR-USA-02, NFR-COMP-02 | Completa |
| DEC-005 — Stack GCP/.NET/React | NFR-POR-01, NFR-POR-02, NFR-MAN-04, NFR-COMP-03, REST-NF-07, REST-NF-08 | Completa |
| DEC-006 — Multi-tenancy pool + RLS | NFR-SEG-01, NFR-MAN-02, NFR-PRIV-05, REST-NF-01 | Completa |
| DEC-008 — IEmailSender / Postmark | NFR-INT-02, NFR-INT-03, NFR-RES-03 | Completa |
| DEC-009 — Digest via Cloud Scheduler + Pub/Sub | NFR-RES-01, NFR-RES-02, NFR-OPS-03, REST-NF-09 | Completa |
| DEC-011 — Centavos inteiros | NFR-MAN-03, REST-NF-05 | Completa |
| KPI-03 — Taxa de entrega do digest ≥ 98% | NFR-OPS-03, NFR-INT-03, NFR-RES-02 | Completa |
| KPI-04 — Taxa de abertura ≥ 30% | NFR-AUD-02 (EmailDigestLog alimenta KPI) | Parcial (meta de produto → OBJ-03) |
| KPI-05 — Latência Kanban ≤ 2 s | NFR-PERF-02, NFR-ESC-02 | Completa |
| KPI-06 — Isolamento 100% em CI | NFR-SEG-01, NFR-MAN-02 | Completa |
| RISCO-T01 — Vazamento de dados entre tenants | NFR-SEG-01, NFR-PRIV-05, NFR-OBS-04 | Completa |
| RISCO-T02 — Falha de entrega do digest | NFR-INT-03, NFR-RES-03, NFR-OBS-04, NFR-OPS-03 | Completa |
| RISCO-T03 — Kanban lento com crescimento | NFR-ESC-02, NFR-PERF-02 | Completa |
| LAC-07 — Base legal LGPD e retenção | NFR-PRIV-04, NFR-COMP-01, NFR-OPS-04 | Em aberto (PTV-03, PTV-05) |
| PRM-03 — SPF/DKIM/DMARC antes do primeiro envio | NFR-INT-03 | Completa |
| RF-01 — Autenticação | NFR-SEG-02, NFR-PERF-04, NFR-DISP-01 | Completa |
| RF-02 — Administração do Tenant | NFR-USA-02, NFR-COMP-02 | Completa |
| RF-03 — BUs, Usuários e Papéis | NFR-SEG-03, NFR-PRIV-05, NFR-DISP-02 | Completa |
| RF-04 — Contas e Contatos | NFR-PRIV-01, NFR-PRIV-02, NFR-PRIV-03, NFR-PRIV-04 | Completa (LAC-07 em aberto) |
| RF-06 — Pipeline e Oportunidades | NFR-PERF-02, NFR-PERF-03, NFR-ESC-02, NFR-USA-03, NFR-AUD-01 | Completa |
| RF-09 — Digest Diário | NFR-PERF-05, NFR-RES-01, NFR-RES-02, NFR-RES-03, NFR-ESC-04, NFR-OPS-03, NFR-INT-02, NFR-INT-03 | Completa |
| RF-11 — Relatórios | NFR-PERF-07, NFR-DISP-02 | Completa |
| RF-12 — Automações (Fase 2) | NFR-RES-01, NFR-REST-NF-09 | Completa |
| RF-13 — IA (Fase 3) | NFR-RES-01, REST-NF-11, NFR-DISP-03 | Parcial (Fase 3 — NFRs específicos de IA serão detalhados antes da Fase 3) |

---

## 11. Critérios de Validação Não Funcional

| Categoria | Método de Validação | Momento | Bloqueante para |
|---|---|---|---|
| PERF | Load test com k6 ou Locust em stg; perfis com 50 RPS nominal e pico de 200 RPS; dataset com 2.000 oportunidades por tenant | Pré-release de cada fase | Go-live em prd |
| DISP | SLO dashboard no Cloud Monitoring; Uptime Checks ativos; relatório mensal de availability | Contínuo pós-go-live | — (monitoramento) |
| ESC | Load test com 10 tenants simultâneos; test com 2.000 oportunidades; test de 1.000 destinatários de digest | Pré-release Fase 1 | Go-live em prd |
| RES | Testes de caos: falha de provider de e-mail, falha de Pub/Sub, falha do banco; verificação de idempotência via retry simulado | Pré-release; revisão semestral | Go-live em prd |
| SEG | Testes de isolamento de tenant em CI (gate); OWASP ZAP DAST semanal em stg (gate de deploy); pentest externo (a agendar antes do go-live) | CI (gate contínuo); DAST semanal; pentest pré-go-live | Go-live em prd |
| PRIV | Teste de PII em logs (scan automático); teste de anonimização de contato; verificação documental do RAT | Pré-release; gate humano de go-live | Go-live em prd |
| COMP | Checklist LGPD + gate humano de aprovação jurídica; `jest-axe` e axe-core/Playwright em CI para WCAG; inspeção de Terraform para WIF | Pré-release; gate humano de go-live | Go-live em prd |
| OBS | Verificação de logs estruturados em stg (queries Cloud Logging); validação de dashboards e alertas via injeção de falha | Pré-release; pós-go-live (trimestral) | Go-live em prd |
| AUD | Testes de integração com Testcontainers: verificar trigger de imutabilidade; verificar que toda operação de negócio gera AuditLog | CI (gate contínuo) | Merge para main |
| INT | Testes de contrato (Pact) para frontend–backend; verificação de SPF/DKIM/DMARC via MXToolbox; review de OpenAPI sem breaking changes | Pré-release; gate de go-live | Go-live em prd |
| USA | `jest-axe` em unitários; axe-core/Playwright em E2E; regressão visual Playwright em 1280 px e 768 px; contagem de passos em fluxos críticos | CI (gate contínuo) | Merge para main (regressão visual) |
| MAN | Relatório de cobertura em CI; gate de thresholds (Coverlet + Istanbul); PBT (FsCheck) em operações monetárias | CI (gate contínuo) | Merge para main |
| POR | Inspeção do Terraform: todos os recursos em southamerica-east1; pipeline de promoção stg → prd sem rebuild de imagem | Pré-release; revisão semestral | Go-live em prd |
| OPS | Drill de restore trimestral; verificação de PITR ativo no Cloud SQL; configuração de alertas de digest e backup | Pré-release (1º drill); trimestral (recorrente) | Go-live em prd |

---

## 12. Dependências

| Dependência | Tipo | NFRs Afetados | Responsável | Status |
|---|---|---|---|---|
| GCP Identity Platform multi-tenant em southamerica-east1 disponível via Terraform | Externa (GCP) | NFR-SEG-02, NFR-DISP-01 | Engenharia | PRM-01 — assumido; verificar no Terraform spike |
| Provider de e-mail confirmado (Postmark vs SendGrid — LAC-03) | Externa (e-mail) | NFR-INT-02, NFR-INT-03, NFR-OPS-03 | Time de Engenharia | Em aberto — spike previsto na Fase 0 |
| Base legal LGPD aprovada pela equipe jurídica da Vellus (LAC-07) | Regulatória / Produto | NFR-PRIV-04, NFR-COMP-01, NFR-OPS-04 | Equipe jurídica da Vellus | Em aberto — gate de go-live |
| SPF/DKIM/DMARC configurados no DNS do domínio azim.com.br | Operacional (DNS) | NFR-INT-03, NFR-OPS-03 | Engenharia / DNS admin | PRM-03 — obrigatório antes do 1º envio em prd |
| Aprovação de SLOs por tier por produto e engenharia | Produto / ADR | NFR-DISP-01, NFR-DISP-02, NFR-DISP-03 | Time de Produto + Engenharia | Em aberto — ADR-0002 sugerido |
| Política de retenção de logs e auditoria aprovada | Jurídica / Produto | NFR-OBS-05, NFR-AUD-03, NFR-OPS-04 | Equipe jurídica + Produto | Em aberto — ADR-0003 sugerido |
| Confirmação de que não há regulação setorial adicional (BACEN, PCI DSS, etc.) | Regulatória | NFR-COMP-01 | Equipe jurídica da Vellus | LAC-07 — em aberto |

## 13. Premissas

| Código | Premissa | Impacto se Falhar |
|---|---|---|
| PRM-NF-01 | Os alvos de performance do PRD §8.2 (latências por endpoint) são tecnicamente atingíveis com a stack GCP/.NET 10/Postgres pooled + RLS | NFRs PERF-02 a PERF-07 precisam ser revistos; pode implicar mudanças de arquitetura (cache agressivo, desnormalização) |
| PRM-NF-02 | Os SLOs de disponibilidade por tier (99,5%/99,0%/98,0%) do PRD §8.1 são consistentes com o modelo de operação sem SRE dedicado na Fase 1 (Vellus operando com equipe reduzida) | Alvos podem ser inatingíveis sem automação de recuperação e runbooks maduros; renegociação necessária |
| PRM-NF-03 | O Cloud SQL com PITR e a configuração de Cloud Run com autoscaling são suficientes para atingir RPO ≤ 5 min e RTO ≤ 4 h sem Multi-Region ou HA adicional | Arquitetura de DR pode exigir Cloud SQL HA (standby replica) com custo adicional |
| PRM-NF-04 | A arquitetura pool + RLS Postgres suporta 50 tenants simultâneos com o tamanho de instância Cloud SQL selecionado no TRD | Pode ser necessário escalar verticalmente o Cloud SQL ou introduzir connection pooler (PgBouncer) antes do pico |
| PRM-NF-05 | O GCP Identity Platform multi-tenant está disponível na região southamerica-east1 sem limitações de API que impeçam o funcionamento da autenticação | Impacta NFR-SEG-02; alternativas (Auth0, Firebase Auth direto) precisariam ser avaliadas via ADR |
| PRM-NF-06 | Não há obrigação regulatória setorial adicional (BACEN, PCI DSS, CVM, ANATEL) além da LGPD para o escopo do MVP da Fase 1 | NFRs de compliance (COMP) precisariam ser revistos; custos e prazos de implementação poderiam aumentar significativamente |

---

## 14. Pontos a Validar

| Código | Ponto a Validar | O que Falta Decidir | Quem Decide | Impacto se Não Decidido | NFRs Afetados |
|---|---|---|---|---|---|
| PTV-01 | Confirmação dos alvos de performance do PRD §8.2 (latências específicas por endpoint) | Origem e justificativa dos números: 2 s para Kanban, 500 ms para oportunidade, 1 s para auth, 5 min para digest, 5 min para import, 3 s para relatórios | Time de Produto + Engenharia | NFRs PERF-02 a PERF-07 ficam como "alvos propostos" sem validação de exequibilidade; risco de go-live com SLOs não atingíveis | NFR-PERF-02 a NFR-PERF-07 |
| PTV-02 | Confirmação dos SLOs de disponibilidade por tier (99,5%/99,0%/98,0%) do PRD §8.1 | Origem dos percentuais; implicação de capacity planning e custo de infraestrutura; viabilidade com equipe reduzida da Fase 1 | Time de Produto + Engenharia (ADR-0002 sugerido) | SLOs não comprometidos formalmente; sem error budget; impossível medir desempenho operacional | NFR-DISP-01, NFR-DISP-02, NFR-DISP-03 |
| PTV-03 | Base legal LGPD e RAT (Registro de Atividades de Tratamento) — LAC-07 | Qual é a base legal para tratamento de dados pessoais de contatos (legítimo interesse, contrato, consentimento)?; Quem é o controlador e o operador? | Equipe jurídica da Vellus | Go-live em prd sem RAT = risco de infração LGPD com multa de até 2% do faturamento (LGPD Art. 52) | NFR-PRIV-04, NFR-COMP-01 |
| PTV-04 | Período de retenção de logs operacionais e dados de auditoria | Quantos dias de logs operacionais manter (90 d é referência do PRD, não aprovado); período mínimo de retenção de AuditLog (obrigação legal vs necessidade de negócio) | Time de Produto + Jurídico (ADR-0003 sugerido) | Configuração de retenção no Cloud Logging sem base; possível custo excessivo ou descumprimento regulatório | NFR-OBS-05, NFR-AUD-03, NFR-OPS-04 |
| PTV-05 | Período mínimo de retenção de dados de negócio (oportunidades, atividades, contatos encerrados) | Quanto tempo manter dados de oportunidades "Perdidas" ou encerradas? Qual o ciclo de vida de contas e contatos desativados? | Equipe jurídica da Vellus + Time de Produto | Política de retenção indefinida = custo crescente de storage e LGPD não cumprida (sem prazo de descarte); política muito curta = perda de histórico comercial para relatórios | NFR-OPS-04 |
| PTV-06 | Confirmação do provider de e-mail (Postmark vs SendGrid) — LAC-03 | Resultado do spike de entregabilidade, SPF/DKIM/DMARC e custo; pode confirmar ou reverter DEC-008 | Time de Engenharia | Provider não confirmado antes do go-live = risco de entregabilidade < 98% (KPI-03) no lançamento | NFR-INT-02, NFR-INT-03, NFR-OPS-03 |
| PTV-07 | Política de janela de manutenção e runbook operacional | Qual a janela exata de manutenção? Como comunicar ao tenant? Como fazer deploys zero-downtime? | Engenharia + Produto | Deploys sem política formal podem interromper o digest às 07:00 BRT | NFR-DISP-04, NFR-OPS-03 |
| PTV-08 | Resolução de divergência entre `.forge/rules/architecture/observability.md` (Prometheus/Loki/Jaeger) e PRD DEC-005 (Cloud Logging/Monitoring/Trace) | Confirmar que a stack GCP nativa substitui a stack da rule para este projeto; atualizar a rule ou criar override documentado | Time de Engenharia (ADR-0005 sugerido) | Risco de times diferentes implementando stacks distintas; falta de dashboards unificados | NFR-OBS-01 a NFR-OBS-05 |
| PTV-09 | Confirmação de ausência de regulação setorial adicional (BACEN, PCI DSS) — LAC-07 | A Vellus ou futuros tenants operam em segmentos regulados (fintech, saúde, seguros)? | Equipe jurídica da Vellus | Se regulação setorial existir, novos NFRs de compliance precisam ser acrescentados antes do go-live | NFR-COMP-01 |
| PTV-10 | Período mínimo de aviso de deprecação de versões de API | 90 dias é suficiente para os clientes da API migrarem? Existe SLA de suporte a versões antigas? | Time de Produto + Engenharia | NFR-INT-01 cita 90 dias como sugestão do NFRD; sem confirmação, breaking changes podem impactar integrações futuras | NFR-INT-01 |

---

## 15. Anexos

### Anexo A — ADRs Sugeridos (delegação a `adr-writer`)

Os NFRs abaixo implicam decisões arquiteturais duráveis que merecem registro formal como ADR. O orquestrador deve invocar o agente `adr-writer` para cada item abaixo.

| ID sugerido | Título proposto | Origem (NFR) | Severidade | Justificativa breve |
|---|---|---|---|---|
| ADR-0001 | `correlationId-e-tenant-id-propagation` | NFR-OBS-01, NFR-OBS-03, NFR-SEG-01 | Alta | Mecanismo transversal a todos os serviços; define como correlationId e tenant_id são gerados, propagados e obrigatórios em logs, traces e eventos — custo de reversão alto após adotado |
| ADR-0002 | `slo-por-tier-de-disponibilidade` | NFR-DISP-01, NFR-DISP-02, NFR-DISP-03 | Alta | SLOs numéricos sem evidência de origem no PRD ditam capacity planning, custo de infraestrutura e on-call policy; formalização evita ambiguidade sobre o que constitui incidente vs degradação aceitável |
| ADR-0003 | `politica-retencao-logs-e-auditoria` | NFR-OBS-05, NFR-AUD-03, NFR-OPS-04 | Média | Política de retenção é binding para múltiplos serviços e tem implicações de custo (Cloud Logging pricing), compliance LGPD (prazo de descarte vs direito ao esquecimento) e obrigação contratual com tenants |
| ADR-0004 | `outbox-e-idempotencia-eventos-dominio` | NFR-RES-01, NFR-RES-02 | Alta | Padrão transversal de outbox pattern + idempotência para todos os eventos publicados ao Pub/Sub; define como evitar duplicação em retentativas; toda feature de eventos herda este padrão |
| ADR-0005 | `stack-observabilidade-gcp-vs-prometheus-loki` | PTV-08, NFR-OBS-01 a NFR-OBS-05 | Alta | Resolve conflito explícito entre `.forge/rules/architecture/observability.md` (Prometheus/Loki/Jaeger) e PRD DEC-005 (Cloud Logging/Monitoring/Trace); sem ADR, times podem implementar stacks divergentes |

### Anexo B — Glossário de Siglas Não Funcionais

| Sigla | Significado |
|---|---|
| NFR | Non-Functional Requirement (Requisito Não Funcional) |
| SLO | Service Level Objective (Objetivo de Nível de Serviço) |
| SLA | Service Level Agreement (Acordo de Nível de Serviço) |
| RPO | Recovery Point Objective (Objetivo de Ponto de Recuperação) |
| RTO | Recovery Time Objective (Objetivo de Tempo de Recuperação) |
| PITR | Point-In-Time Recovery (Recuperação em Ponto no Tempo) |
| PII | Personally Identifiable Information (Informação Pessoal Identificável) |
| RAT | Registro de Atividades de Tratamento (LGPD) |
| WAF | Web Application Firewall |
| WIF | Workload Identity Federation |
| DAST | Dynamic Application Security Testing |
| PBT | Property-Based Testing |
| CSP | Content Security Policy |
| RLS | Row-Level Security |
| RBAC | Role-Based Access Control |
| DR | Disaster Recovery |
| IaC | Infrastructure as Code |
| WCAG | Web Content Accessibility Guidelines |
| LGPD | Lei Geral de Proteção de Dados |
| CVE | Common Vulnerabilities and Exposures |

### Anexo C — Referência de Volumes (PRD §8.7)

| Métrica | Volume Fase 1 | Volume 12 meses | Pico estimado |
|---|---|---|---|
| Tenants ativos | 1 (Vellus) | 5–20 | 50 |
| Oportunidades por tenant | 108 | 500 | 2.000 |
| Usuários por tenant | 5–10 | 20 | 100 |
| Destinatários digest / dia | ~10 | ~200 | 1.000 |
| Requisições por segundo | < 5 | 50 | 200 |

> Volumes marcados como "a validar com load test antes do go-live" no PRD §8.7. Usados como dimensionamento de referência para NFRs de performance e escalabilidade.

### Anexo D — Mapa de NFRs por Fase

| Fase | NFRs obrigatórios antes da Fase | Notas |
|---|---|---|
| Fase 0 (Fundação) | NFR-SEG-01 (isolamento CI), NFR-SEG-02 (Identity Platform), NFR-INT-03 (SPF/DKIM/DMARC), NFR-MAN-04 (IaC), NFR-MAN-02 (testes de isolamento), NFR-POR-02 (residência de dados), NFR-COMP-03 (WIF) | Gate de conclusão da Fase 0 |
| Fase 1 (MVP) | Todos os NFRs de PERF, DISP, SEG, PRIV, OBS, AUD, INT, USA, MAN, POR, OPS; NFR-COMP-01 (RAT aprovado); NFR-PRIV-04 (base legal) | Gate de go-live em produção |
| Fase 2 (Direção) | NFR-RES-01 (automações assíncronas via Pub/Sub — RF-12); NFR-ESC-04 revisado para 1.000 destinatários | Pré-requisitos para Automações Visuais |
| Fase 3 (IA) | NFRs específicos de IA (rate-limit Redis por tenant, isolamento de dados de IA, custo de IA por tenant) — a detalhar antes da Fase 3 | REST-NF-11 como baseline |
