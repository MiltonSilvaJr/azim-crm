# DDD Validation Report — Azim CRM

**Produto:** Azim CRM
**Versão do Relatório:** v1.0
**Data:** 2026-06-11
**Status:** Rascunho para revisão
**Artefatos Validados:** Subdomínios, Bounded Contexts, Context Map, Event Storming, Módulos, Data Ownership, Deployables, Linguagem Ubíqua, Diagramas C4

---

## Controle de Versão

| Versão | Data | Descrição |
|---|---|---|
| v1.0 | 2026-06-11 | Criação inicial do relatório de validação DDD |

---

## Sumário Executivo

### Parecer Final

**Aprovado com Ressalvas**

### Síntese

A modelagem DDD do Azim CRM está estruturalmente sólida. O Core Domain (Opportunity Pipeline) está corretamente identificado com evidência estratégica verificável (gap Salesforce/Zoho, 108 oportunidades da Vellus, comissão nativa com snapshot imutável). Os 14 subdomínios Supporting/Generic são adequados, os 15 bounded contexts têm linguagem própria e justificativa rastreável, e o context map utiliza os padrões DDD corretos (ACL, Customer/Supplier, Conformist, Published Language, Read Model).

Foram encontrados 2 achados altos (inconsistência de padrão DDD no diagrama + conflito de ownership de tabelas de configuração de BU), 3 achados médios e 2 achados baixos — todos corrigidos diretamente ou registrados como pontos a validar. Não há achados críticos. A modelagem pode avançar para TRD, módulos e backlog com cautela sobre os pontos a validar já documentados.

### Principais Riscos

- Ownership de dados ambíguo para `stages`, `origin_channels`, `loss_reasons` (corrigido nesta revisão)
- Activity Management como BC formal pode ser complexidade desnecessária se for absorvido como módulo do Opportunity Pipeline (DDD-VAL-01)
- Reporting sem ownership de dados pode não justificar BC formal (DDD-VAL-02)
- Sobrecarga de Conformist: Organization Management é upstream de quase todos os BCs — monitorar se isso não cria gargalo de deployment na Fase 2

### Principais Recomendações

- Resolver DDD-VAL-01 antes de definir o módulo de Activity Management no TRD
- Resolver DDD-VAL-02 antes de criar worker separado para Reporting
- Decidir DDD-VAL-04 (consolidação Tenancy+Organization) antes da Fase 2 para evitar retrabalho
- Adicionar evento de domínio explícito `commission_snapshot.created` ou documentar formalmente sua omissão (VAL-DDD-06)

---

## 1. Documentos Avaliados

| Documento | Caminho | Status |
|---|---|---|
| PRD | `docs/product/prd/prd.md` | Encontrado |
| FRD | `docs/product/frd-nfrd/frd.md` | Encontrado |
| NFRD | `docs/product/frd-nfrd/nfrd.md` | Não verificado nesta revisão |
| TRD | `docs/product/trd/trd.md` | Não verificado nesta revisão |
| ADRs | `docs/product/adr/` | Não verificado nesta revisão |
| Segmentation | `docs/product/ddd/ddd-segmentation.md` | Encontrado |
| Subdomínios | `docs/product/ddd/subdomains/` | Encontrado (15 READMEs) |
| Bounded Contexts | `docs/product/ddd/bounded-contexts/` | Encontrado (15 READMEs) |
| Context Map | `docs/product/ddd/context-map/README.md` | Encontrado |
| Context Map relations.md | `docs/product/ddd/context-map/relations.md` | Encontrado |
| Context Map patterns.md | `docs/product/ddd/context-map/patterns.md` | Encontrado |
| Context Map diagram.md | `docs/product/ddd/context-map/diagram.md` | Encontrado |
| Diagramas C4 | `docs/product/ddd/diagrams/` | Encontrado (3 níveis + index.html) |

---

## 2. Validação Problema x Solução

| Item | Tipo Declarado | Tipo Correto | Status | Observação |
|---|---|---|---|---|
| Opportunity Pipeline | Core Domain + Bounded Context | Correto | OK | Subdomínio SD-01 mapeado em BC-01 sem confusão |
| Activity Management | Supporting Subdomain + Bounded Context | Correto (mas questionável) | Ponto a Validar | DDD-VAL-01: pode ser módulo do Pipeline; ciclo de vida próprio existe mas é fraco |
| Reporting | Supporting Subdomain + Bounded Context | Módulo técnico mais provável | Ponto a Validar | DDD-VAL-02: sem ownership de dados, sem escrita, fronteira questionável como BC formal |
| `azim-api` | Deployable (monólito modular) | Correto | OK | Monólito modular com fronteiras internas — não confunde BC com microsserviço |
| `azim-digest-worker` | Deployable separado | Correto | OK | Justificativa operacional clara (ciclo de release e carga independente) |
| `azim-reporting-worker` | Deployable separado | Correto | OK | Read models assíncronos; não afeta latência transacional |
| `azim-ai-service` | Deployable separado | Correto | OK | Stack diferente (Python/FastAPI/LangGraph); isolamento de custo por tenant |
| Data Migration | BC temporário Fase 1 | Correto | OK | DDD-VAL-03 já documentado; ciclo de vida limitado explícito |
| Tenancy & Branding | Generic Subdomain + BC | Correto | OK | Commodity de plataforma; não é Core |
| Audit Log | Generic Subdomain + BC | Correto | OK | Append-only cross-cutting; sem lógica de domínio própria |

---

## 3. Validação de Subdomínios

| Subdomínio | Classificação Atual | Classificação Recomendada | Status | Justificativa |
|---|---|---|---|---|
| SD-01 Opportunity Pipeline | Core Domain | Core Domain | OK | Diferencial estratégico verificado adversarialmente; comissão nativa inexistente em Salesforce/Zoho; regras proprietárias RN-001..028; R$ 9M em risco operacional |
| SD-02 Account Management | Supporting | Supporting | OK | Necessário ao pipeline; sem diferencial competitivo; dedupe é commodity |
| SD-03 Partner Management | Supporting | Supporting | OK | Alimenta o Core; percentuais e visualização de comissão são suporte, não diferencial |
| SD-04 Activity Management | Supporting | Supporting | OK | Lógica de estagnação (RN-028) tem valor real; porém DDD-VAL-01 questiona se é BC ou módulo |
| SD-05 Goal & Forecast | Supporting | Supporting | OK | Metas derivam de dados do Core; graceful degradation indica suporte |
| SD-06 Digest | Supporting | Supporting | OK | Lógica complexa de seleção por fuso IANA, composição e idempotência — não é mero envio de e-mail |
| SD-07 Reporting | Supporting | Supporting | OK | Read models derivados; sem modelo próprio; DDD-VAL-02 questiona BC formal |
| SD-08 Organization Management | Supporting | Supporting | OK | RBAC e BUs habilitam o pipeline; não diferenciam o produto |
| SD-09 Workflow Automation | Supporting | Supporting | OK | Editor visual reduz trabalho manual; diferencial futuro, não central |
| SD-10 AI Intelligence | Supporting | Supporting | OK | Diferencial futuro validado; dependente de fases anteriores |
| SD-11 Data Migration | Supporting | Supporting | OK | Capacidade temporária Fase 1; dry-run e rollback têm lógica própria |
| SD-12 Identity & Access | Generic | Generic | OK | Commodity GCP Identity Platform; substituível |
| SD-13 Tenancy & Branding | Generic | Generic | OK | Multi-tenancy infrastructure; commodity de plataforma |
| SD-14 Notification Delivery | Generic | Generic | OK | IEmailSender abstrai Postmark/SendGrid; commodity explicitamente |
| SD-15 Audit Log | Generic | Generic | OK | Append-only; sem lógica de domínio; compliance commodity |

---

## 4. Validação do Event Storming

| Fluxo | Item | Tipo | Problema | Status | Recomendação |
|---|---|---|---|---|---|
| Fluxo 1 | CreateOpportunity | Comando | — | OK | Imperativo correto |
| Fluxo 1 | OpportunityCreated | Evento | — | OK | Passado correto |
| Fluxo 1 | MoveOpportunityStage | Comando | — | OK | Imperativo correto |
| Fluxo 1 | OpportunityStageChanged | Evento | — | OK | Passado correto |
| Fluxo 1 | SetPartnerCommission → CommissionSnapshotCreated | Evento | Snapshot prematuro: snapshot imutável só ocorre ao ganhar (Ordem 5); Ordem 4 deveria gerar CommissionCalculated | Corrigido (ADJ-DDD-006) | Evento corrigido para CommissionCalculated |
| Fluxo 1 | CloseOpportunityAsWon → OpportunityWon | Evento | — | OK | Passado correto; snapshot implícito no evento |
| Fluxo 1 | DetectStaleOpportunity → OpportunityStale | Comando/Evento | — | OK | Emissor sistema (scheduler) claro; evento no passado |
| Fluxo 2 | TriggerDigestJob → DigestJobTriggered | Comando/Evento | — | OK | Cloud Scheduler como ator; evento no passado |
| Fluxo 2 | DigestComposed, DigestEmailSent | Evento | — | OK | Passado correto |
| Fluxo 2 | CompleteActivityViaDigest → ActivityCompleted | Comando/Evento | — | OK | Idempotência referenciada; passado correto |
| Fluxo 3 | CreatePartner → PartnerCreated | Evento | — | OK | Passado correto |
| Fluxo 3 | SetCommissionPercentages → CommissionCalculated | Evento | — | OK | Correto: cálculo, não snapshot |
| Fluxo 3 | SnapshotCommissionOnWin → CommissionSnapshotCreated | Evento | — | OK | Snapshot ao ganhar; passado correto |
| Todos | Produtor canônico | — | Produtores explícitos em cada BC canvas | OK | — |
| BC-01 | Eventos de domínio seção 9 | — | CommissionSnapshotCreated ausente como evento explícito; está implícito em opportunity.won | Ponto a Validar (VAL-DDD-06) | Definir se snapshot de comissão é evento explícito ou implícito |

---

## 5. Validação de Bounded Contexts

| Bounded Context | Linguagem Própria | Regras Próprias | Ciclo de Vida | Ownership | Integrações | Decisão |
|---|---|---|---|---|---|---|
| Opportunity Pipeline | Sim | Sim (RN-001..028) | Sim | Sim (corrigido) | Sim | OK |
| Account Management | Sim | Sim (RN-014) | Sim | Sim | Sim | OK |
| Partner Management | Sim | Sim (RN-026, RN-008) | Sim | Sim | Sim | OK |
| Activity Management | Sim | Sim (RN-028) | Parcial | Sim | Sim | Ponto a Validar (DDD-VAL-01) |
| Goal & Forecast | Sim | Sim (RN-017, RN-027) | Sim | Sim | Sim | OK |
| Digest | Sim | Sim (RN-009..011, 018, 029) | Sim | Sim | Sim | OK |
| Reporting | Parcial | Não (read-only) | Não | Não (read models) | Sim | Ponto a Validar (DDD-VAL-02) |
| Organization Management | Sim | Sim (RN-013, MSG-017) | Sim | Sim | Sim | OK |
| Data Migration | Sim | Sim (RN-023) | Fase 1 | Temporário | Sim | OK (temporário) |
| Workflow Automation | Sim | Sim (execução assíncrona) | Sim (Fase 2) | Sim | Sim | OK (Fase 2) |
| AI Intelligence | Sim | Sim (rate-limit, isolamento por tenant) | Sim (Fase 3) | Sim | Sim | OK (Fase 3) |
| Identity & Access | Parcial (GCP) | GCP padrão | Sim | Não (GCP IdP) | Sim | OK |
| Tenancy & Branding | Sim | Sim (RN-019, RN-020) | Sim | Sim | Sim | OK |
| Notification Delivery | Não (abstração) | Não (provider) | Não | Não | Sim | OK (generic/adapter) |
| Audit Log | Parcial | Imutabilidade | Sim | Sim | Sim | OK |

---

## 6. Validação do Context Map

| Origem | Destino | Padrão Atual | Padrão Recomendado | Status | Justificativa |
|---|---|---|---|---|---|
| Opportunity Pipeline | Account Management | Customer/Supplier | Customer/Supplier | OK | Pipeline consome account_id; Account é upstream estável |
| Opportunity Pipeline | Partner Management | Customer/Supplier | Customer/Supplier | OK | Pipeline consome partner_id e percentuais |
| Opportunity Pipeline | Activity Management | Customer/Supplier | Customer/Supplier | OK | Pipeline consulta last_activity_at para detecção de stale |
| Opportunity Pipeline | Audit Log | Published Language | Published Language | OK | Formato AuditEvent canônico consumido por todos |
| Digest | Notification Delivery | Anti-Corruption Layer | Anti-Corruption Layer | OK | ACL via IEmailSender protege de troca de provider |
| Organization Management | Identity & Access | Anti-Corruption Layer | Anti-Corruption Layer | OK | ACL protege modelo interno do contrato GCP IdP |
| Reporting | Opportunity Pipeline | Read Model | Read Model | OK | Sem joins diretos; view ou materialized view |
| Tenancy & Branding | Opportunity Pipeline | Published Language (diagrama) | Conformist | Corrigido (ADJ-DDD-001) | Todos os BCs aceitam tenant_id sem tradução; Conformist correto |
| Tenancy & Branding | Todos os BCs | Conformist | Conformist | OK | Consistente em relations.md e patterns.md |
| Goal & Forecast | Opportunity Pipeline | Customer/Supplier | Customer/Supplier | OK | Goal lê realizado e pipeline do Pipeline |
| Data Migration | Opportunity Pipeline | Customer/Supplier | Customer/Supplier | OK | Escrita bulk após import; Pipeline é upstream do modelo |
| Workflow Automation | Opportunity Pipeline | Customer/Supplier via Pub/Sub | Customer/Supplier | OK (Fase 2) | DDD-VAL-05 pendente: confirmar contrato de evento antes da Fase 2 |
| AI Intelligence | Opportunity Pipeline | Customer/Supplier (read-only) | Customer/Supplier | OK (Fase 3) | Sem escrita; apenas leitura para scoring |
| Todos os BCs | Organization Management | Conformist | Conformist | OK | BUs, user_id, papéis são infraestrutura aceita sem negociação |

---

## 7. Validação da Linguagem Ubíqua

| Termo | Contexto | Problema | Status | Recomendação |
|---|---|---|---|---|
| "VO" (Value Object) | Todos | Não encontrado em nenhum artefato | OK | Uso correto de "objeto de valor" em pt-BR |
| `OpportunityPartnerCommission` | Opportunity Pipeline | Nome em inglês técnico para entidade — aceitável pois é nome de classe de código | OK | Preservado como nome técnico canônico |
| `stale` | Opportunity Pipeline | Inglês técnico para "estagnado" — mas definido no glossário ubíquo do BC | OK | Definição clara no canvas |
| `azimute_da_semana` | Digest | Linguagem de negócio específica e bem definida | OK | Termo de domínio correto |
| `EmailDigestLog` | Digest | Nome técnico mas com definição de negócio no canvas | OK | Aceitável como nome de entidade |
| `BU` (Business Unit) | Organization Management | Sigla usada sem expansão em alguns contextos | OK | Expandida na primeira ocorrência do canvas |
| `forecast_ponderado` | Opportunity Pipeline | Mistura pt-BR + inglês — mas é o termo do domínio | OK | Coerente com discovery e operação da Vellus |
| `digest` | Digest | Inglês adotado como termo do domínio | OK | Definição clara; uso consistente |

---

## 8. Validação de Ownership de Dados

| Dado | Dono Atual (pré-revisão) | Problema | Status | Recomendação |
|---|---|---|---|---|
| `stages` | Organization Management E Opportunity Pipeline | Duplicidade de ownership — mesma tabela listada nos dois BCs | Corrigido (ADJ-DDD-003, ADJ-DDD-004, ADJ-DDD-005) | Dono único: Organization Management; Pipeline consome por referência (stage_id) |
| `origin_channels` | Organization Management E Opportunity Pipeline | Idem | Corrigido (ADJ-DDD-003) | Idem |
| `loss_reasons` | Organization Management E Opportunity Pipeline | Idem | Corrigido (ADJ-DDD-003) | Idem |
| `opportunities` | Opportunity Pipeline | — | OK | Dono único e claro |
| `opportunity_partner_commissions` | Opportunity Pipeline | — | OK | Snapshot imutável; dono único |
| `activities` | Activity Management | — | OK | Dono único |
| `email_digest_logs` | Digest | — | OK | Dono único |
| `goals` | Goal & Forecast | — | OK | Dono único |
| `business_units`, `users` | Organization Management | — | OK | Dono único |
| `tenants`, `tenant_brandings` | Tenancy & Branding | — | OK | Dono único |
| `audit_logs` | Audit Log | — | OK | Append-only; sem modificação |
| Read models de Reporting | Nenhum (derivados) | Sem ownership de dados — leitura de views/materialized views | OK (mas valida DDD-VAL-02) | Sem risco se não houver escrita |
| `accounts`, `contacts` | Account Management | — | OK | Dono único; compartilhado entre BUs por API, não por tabela |
| `partners` | Partner Management | — | OK | Dono único |

---

## 9. Validação de Módulos e Deployables

| Item | Tipo Atual | Problema | Status | Recomendação |
|---|---|---|---|---|
| MOD-01 Autenticação | Módulo (Identity & Access + Org Mgmt) | Módulo cross-cutting com dois BCs — aceitável em monólito modular | OK | Fronteira clara: Identity resolve sessão; Org valida no tenant |
| MOD-09 Digest Diário | Módulo (Digest) → `azim-digest-worker` | Deployable separado justificado | OK | Ciclo de processamento independente do API |
| MOD-10 Notificações In-App | Módulo listado como Workflow Automation | Notificações in-app (RF-10) atribuídas ao Workflow Automation mas podem ser necessárias antes da Fase 2 | Ponto a Validar | Definir se notificação in-app simples pode ser MOD próprio na Fase 1 |
| MOD-11 Relatórios | Módulo (Reporting) → `azim-reporting-worker` | Worker separado depende de BC formal (DDD-VAL-02) | Ponto a Validar | Se DDD-VAL-02 decidir por módulo técnico, o worker pode ser simplificado |
| `azim-api` | Deployable (monólito modular) | Contém 9 BCs — adequado para Fase 1 com 1 tenant | OK | Fronteiras de módulo internas garantem separação futura |
| `azim-digest-worker` | Deployable (worker) | — | OK | Justificativa operacional clara |
| `azim-reporting-worker` | Deployable (worker) | Depende de decisão sobre DDD-VAL-02 | Ponto a Validar | Pode ser simplificado |
| `azim-ai-service` | Deployable (Python/FastAPI) | — | OK | Stack diferente justifica deployable próprio |
| `azim-workflow-worker` | Deployable (Fase 2) | — | OK | Execução assíncrona obrigatória; adequado |
| `azim-web` | Frontend SPA | — | OK | Interface única com theming por tenant via CSS variables |

---

## 10. Validação DDD Tático

| Item | Tipo | Problema | Status | Recomendação |
|---|---|---|---|---|
| `Opportunity` | Agregado | Raiz bem definida; invariantes protegidas (owner, snapshot, estágio) | OK | — |
| `OpportunityPartnerCommission` | Entidade | Dentro do agregado Opportunity; correto — snapshot imutável pertence ao mesmo contexto transacional | OK | — |
| `OpportunityStageTransition` | Entidade | Dentro do agregado Opportunity; correto — transição é parte do ciclo de vida da oportunidade | OK | — |
| `OpportunityNumber` | Objeto de valor | Imutável, sem setter, igualdade por valor | OK | — |
| `Money` | Objeto de valor | Imutável, centavos inteiros, sem setter | OK | Transversal entre contextos; bem documentado |
| `CommissionCalculation` | Objeto de valor | Resultado de cálculo — imutável por definição | OK | — |
| `Activity` | Agregado | Raiz única; estado protegido (status, data) | OK | — |
| `BusinessUnit` | Agregado | Raiz com estágios, canais, motivos de perda como entidades filhas — tamanho adequado | OK | — |
| `User` | Agregado | Raiz com UserMembership como entidade filha — correto | OK | — |
| `Goal` | Agregado | Raiz simples; `GoalPeriod` como objeto de valor — correto | OK | — |
| `GoalPeriod` | Objeto de valor | Imutável (ano, mês), sem setter | OK | — |
| `DigestJob` | Agregado | Raiz de orquestração; `EmailDigestLog` como entidade interna | OK | — |
| `Stage` (Organization Management) | Entidade | Dono: Organization Management; consumido por Pipeline via referência — após correção ADJ-DDD-005 está correto | Corrigido | — |
| Repositórios para entidades internas | — | Não encontrados; padrão aparentemente correto | OK | Validar na implementação |
| Eventos de domínio | — | Todos no passado (opportunity.created, opportunity.won, etc.) | OK | — |
| `CommissionSnapshotCreated` | Evento | Ausente como evento explícito no BC Canvas do Pipeline; presente no Event Storming Fluxo 3 | Ponto a Validar (VAL-DDD-06) | Decidir: evento explícito ou implícito em opportunity.won |
| "VO" | Linguagem | Não encontrado em nenhum artefato | OK | Uso correto de "objeto de valor" |

---

## 11. Validação de Diagramas C4

| Diagrama | Problema | Status | Recomendação |
|---|---|---|---|
| C4 Level 1 (System Context) | Não verificado em detalhe nesta revisão | A verificar | Validar que exibe sistema, atores e sistemas externos (GCP, Postmark, Cloud Scheduler) |
| C4 Level 2 (Containers) | Não verificado em detalhe nesta revisão | A verificar | Validar que exibe os 5 deployables candidatos + GCP services |
| C4 Level 3 (Components) | Não verificado em detalhe nesta revisão | A verificar | Validar que detalha módulos críticos (Pipeline, Digest) |
| Context Map Diagram (Mermaid) | Aresta TB → OP com label incorreto (Published Language) | Corrigido (ADJ-DDD-001) | Diagrama Mermaid válido após correção |

---

## 12. Validação de Rastreabilidade

| Decisão DDD | Evidência | Status | Observação |
|---|---|---|---|
| Core Domain = Opportunity Pipeline | PRD seção 1.2, RF-06; gap Salesforce/Zoho verificado adversarialmente | OK | Evidência múltipla e quantificada (R$ 9M, 108 opps) |
| Comissão nativa com snapshot imutável | PRD RF-06; DEC-002; discovery notes; 24 opps com parceiro sem % definido | OK | Diferencial confirmado |
| Digest como deployable separado | PRD RF-09; DEC-009; NFR-PERF-05 | OK | Justificativa operacional rastreável |
| ACL para GCP Identity Platform | DEC-006; patterns.md; TRD referenciado | OK | Decisão documentada |
| ACL para Notification Delivery | DEC-008; LAC-03 pendente | OK | Decisão documentada; spike pendente |
| Monólito modular na Fase 1 | PRD Fase 1 (1 tenant em produção); §10 do segmentation | OK | Justificativa explícita |
| Supporting = Activity Management | PRD RF-07; FRD ESC-07 | OK | Evidência clara nos requisitos |
| Supporting = Reporting | PRD RF-11; FRD ESC-10 | OK | Evidência clara; DDD-VAL-02 pendente sobre BC formal |
| Generic = Audit Log | PRD seção 8.6; RN-024; LGPD | OK | Cross-cutting imutável justificado |
| Data Migration como BC temporário | PRD Jornada J-04; DEC-010; ESC-11 | OK | DDD-VAL-03 documenta limpeza pós-Fase 1 |
| Valores monetários em centavos inteiros | DEC-011; PRD seção 1.1 | OK | Consistente em todos os objetos de valor |

---

## 13. Completude Documental

### Tabela de Completude Documental

| Tipo | Esperado (matriz §1.2 / §4.1) | Encontrado (filesystem) | Faltando | Excedente |
|---|---|---|---|---|
| Subdomínios Core | 1 | 1 | `[]` | `[]` |
| Subdomínios Supporting | 10 | 10 | `[]` | `[]` |
| Subdomínios Generic | 4 | 4 | `[]` | `[]` |
| Bounded Contexts (Confirmar) | 15 | 15 | `[]` | `[]` |

### Estrutura e Visualização

| Artefato | Esperado | Presente? |
|---|---|---|
| `subdomains/core/` (dir) | Sim | Sim |
| `subdomains/supporting/` (dir) | Sim | Sim |
| `subdomains/generic/` (dir) | Sim | Sim |
| `bounded-contexts/` (dir) | Sim | Sim |
| `context-map/README.md` | Sim | Sim |
| `context-map/relations.md` | Sim | Sim |
| `context-map/patterns.md` | Sim | Sim |
| `context-map/diagram.md` | Sim | Sim |
| `diagrams/c4-level-1-system-context.md` | Sim | Sim |
| `diagrams/c4-level-2-containers.md` | Sim | Sim |
| `diagrams/c4-level-3-components.md` | Sim | Sim |
| `diagrams/index.html` | Sim | Sim |

Completude documental: **100% — sem faltando, sem excedente, sem órfãos**.

---

## 14. Matriz de Cobertura RF → Subdomínio

| RF | Descrição | Subdomínio | Bounded Context | Deployable | Status |
|---|---|---|---|---|---|
| RF-01 | Autenticação e Acesso | SD-12 Identity & Access | BC-12 | `azim-api` | OK |
| RF-02 | Administração do Tenant | SD-13 Tenancy & Branding | BC-13 | `azim-api` | OK |
| RF-03 | BUs, Usuários e Papéis | SD-08 Organization Management | BC-08 | `azim-api` | OK |
| RF-04 | Contas e Contatos | SD-02 Account Management | BC-02 | `azim-api` | OK |
| RF-05 | Parceiros | SD-03 Partner Management | BC-03 | `azim-api` | OK |
| RF-06 | Pipeline e Oportunidades | SD-01 Opportunity Pipeline | BC-01 | `azim-api` | OK |
| RF-07 | Atividades e Follow-ups | SD-04 Activity Management | BC-04 | `azim-api` | OK (DDD-VAL-01 pendente) |
| RF-08 | Metas e Forecast | SD-05 Goal & Forecast | BC-05 | `azim-api` | OK |
| RF-09 | Digest Diário por E-mail | SD-06 Digest + SD-14 Notification Delivery | BC-06 + BC-14 | `azim-digest-worker` | OK |
| RF-10 | Notificações In-App | SD-09 Workflow Automation | BC-10 | `azim-workflow-worker` | Ponto a Validar (Fase 2) |
| RF-11 | Relatórios | SD-07 Reporting | BC-07 | `azim-reporting-worker` | OK (DDD-VAL-02 pendente) |
| RF-12 | Automações Visuais | SD-09 Workflow Automation | BC-10 | `azim-workflow-worker` | OK (Fase 2) |
| RF-13 | Agentes de IA | SD-10 AI Intelligence | BC-11 | `azim-ai-service` | OK (Fase 3) |
| Migração | Migração da Planilha | SD-11 Data Migration | BC-09 | `azim-api` | OK (DDD-VAL-03 pendente) |
| Cross-cutting | Trilha de Auditoria | SD-15 Audit Log | BC-15 | `azim-api` | OK |
| Cross-cutting | Envio de E-mail | SD-14 Notification Delivery | BC-14 | `azim-digest-worker` | OK |

**Cobertura: 13/13 RFs cobertos (RF-10 como Fase 2, RF-12 como Fase 2, RF-13 como Fase 3 — conforme PRD).**

---

## 15. Achados de Validação

| ID | Severidade | Artefato | Problema | Impacto | Recomendação |
|---|---|---|---|---|---|
| FIND-DDD-001 | Alta | `context-map/diagram.md` | Aresta Tenancy & Branding → Opportunity Pipeline rotulada como "Published Language" mas o padrão correto é "Conformist" (consistente com relations.md e patterns.md). Published Language implica contrato publicado explícito; tenant_id é apenas UUID aceito sem tradução. | Inconsistência de padrão DDD entre artefatos; pode gerar decisões de implementação erradas | Corrigido (ADJ-DDD-001) |
| FIND-DDD-002 | Alta | `bounded-contexts/opportunity-pipeline/README.md` | Tabelas `stages`, `origin_channels`, `loss_reasons` listadas como dados próprios do Opportunity Pipeline E do Organization Management simultaneamente. Duplicidade de ownership viola o princípio de dono único de escrita. | Risco de escrita cruzada na implementação; confusion sobre qual contexto faz CRUD dessas tabelas | Corrigido (ADJ-DDD-003) — owner único: Organization Management |
| FIND-DDD-003 | Alta | `bounded-contexts/tenancy-branding/README.md` | Padrão DDD "Published Language" declarado para relação Tenancy → Todos os BCs na seção de Integrações, contradizendo relations.md e patterns.md (Conformist). | Inconsistência entre artefatos do mesmo contexto | Corrigido (ADJ-DDD-002) |
| FIND-DDD-004 | Média | `bounded-contexts/organization-management/README.md` | Coluna "Dono" da entidade Stage continha "Organization Management (configuração) / Opportunity Pipeline (consumo)" — misturando ownership com relação de consumo em uma única célula de tabela. | Ambiguidade de ownership na leitura dos artefatos | Corrigido (ADJ-DDD-005) |
| FIND-DDD-005 | Média | `ddd-segmentation.md` §2 Fluxo 1 | Evento Storming Fluxo 1, Ordem 4: `SetPartnerCommission` gerava `CommissionSnapshotCreated` prematuramente. O snapshot imutável só ocorre ao mover para "Ganho" (Ordem 5 / CloseOpportunityAsWon). Ordem 4 deveria gerar `CommissionCalculated`. | Confusão entre cálculo provisório e snapshot imutável; pode levar a implementação incorreta do momento de geração do snapshot | Corrigido (ADJ-DDD-006) |
| FIND-DDD-006 | Média | `bounded-contexts/opportunity-pipeline/README.md` | Evento `commission_snapshot.created` (ou equivalente) ausente na seção 9 "Eventos de Domínio" do BC. O snapshot de comissão é um evento de domínio relevante (Audit Log e Reporting precisam dele para rastreabilidade). Está implícito em `opportunity.won`. | Consumidores de eventos podem não reagir corretamente ao snapshot; rastreabilidade de comissão depende deste evento | Ponto a Validar (VAL-DDD-06) |
| FIND-DDD-007 | Baixa | `ddd-segmentation.md` §8 Relações | Relação Data Migration → Opportunity Pipeline descrita como "Customer/Supplier" com "bulk write" mas Data Migration na verdade usa os comandos do Pipeline (CreateOpportunity, CreateAccount) — não escreve diretamente nas tabelas do Pipeline. O padrão Customer/Supplier está correto mas o campo "Contrato" deveria explicitar isso. | Risco de interpretação como escrita direta no banco do Pipeline | Registrado; sem correção forçada (semântica aceitável) |
| FIND-DDD-008 | Baixa | `context-map/diagram.md` | Nota 3 do diagrama dizia "representado como Published Language cross-cutting" — inconsistente com a aresta corrigida para Conformist | Consistência documental | Corrigido (ADJ-DDD-001, nota atualizada) |

---

## 16. Ajustes Aplicados

| ID | Artefato | Ajuste | Fonte da Correção |
|---|---|---|---|
| ADJ-DDD-001 | `context-map/diagram.md` | Aresta `TB → OP`: label alterada de "Published Language\ntenant_id cross-cutting" para "Conformist\ntenant_id cross-cutting" | relations.md e patterns.md — ambos declaram Conformist |
| ADJ-DDD-002 | `context-map/diagram.md` | Nota 3: texto atualizado de "Published Language cross-cutting" para "Conformist cross-cutting" com justificativa | Coerência com ADJ-DDD-001 |
| ADJ-DDD-003 | `bounded-contexts/opportunity-pipeline/README.md` | Seção 12 Dados Próprios: removidas tabelas `stages`, `origin_channels`, `loss_reasons`; adicionada nota explicando que são owned by Organization Management e consumidas por referência | Organization Management BC canvas seção 3 (responsabilidades) e seção 12 (dados próprios) |
| ADJ-DDD-004 | `bounded-contexts/tenancy-branding/README.md` | Seção 5 Integrações: padrão "Published Language" → "Conformist" para relação Tenancy → Todos os BCs | relations.md linha "Opportunity Pipeline \| Tenancy & Branding \| Conformist" |
| ADJ-DDD-005 | `bounded-contexts/organization-management/README.md` | Seção 7 Agregados: coluna "Dono" de Stage corrigida de "Organization Management (configuração) / Opportunity Pipeline (consumo)" para "Organization Management" com nota inline | Princípio de dono único de escrita |
| ADJ-DDD-006 | `ddd-segmentation.md` | Fluxo 1, Ordem 4: evento corrigido de `CommissionSnapshotCreated` para `CommissionCalculated` | Fluxo 3 Ordem 3 (cálculo) vs Ordem 4 (snapshot ao ganhar); RN-007 |

---

## 17. Conflitos Arquiteturais

Nenhum conflito arquitetural identificado. Todos os pontos de tensão já estavam documentados como pontos a validar nos próprios artefatos (DDD-VAL-01 a DDD-VAL-05).

---

## 18. Pontos a Validar

| Código | Ponto | Impacto | Recomendação |
|---|---|---|---|
| DDD-VAL-01 | Activity Management como BC formal vs módulo interno do Opportunity Pipeline | Definição de deployable (`azim-api` com módulo interno vs BC separado), ownership de dados de atividade | Decidir antes do TRD de módulos; se BC for absorvido, o `activities` table permanece mas sem BC canvas próprio |
| DDD-VAL-02 | Reporting como BC formal vs camada de queries otimizadas no `azim-api` | Necessidade do `azim-reporting-worker` como deployable separado | Se decidido como módulo técnico, simplificar para queries com índices otimizados; BC canvas pode ser descontinuado |
| DDD-VAL-03 | Data Migration como BC temporário vs feature dentro do Opportunity Pipeline pós-Fase 1 | Limpeza arquitetural após migração completa da planilha | Planejar remoção do BC após conclusão da Fase 1; tabelas `migration_jobs` e `migration_logs` podem ser arquivadas |
| DDD-VAL-04 | Consolidação de Tenancy & Branding com Organization Management | Decisão antes da Fase 2 para evitar retrabalho; sobrecarga de Conformist em dois BCs de infraestrutura | Avaliar se PlatOp (Tenancy) e TAdmin (Organization) são perfis suficientemente distintos para justificar BCs separados |
| DDD-VAL-05 | Contrato de eventos do Pipeline para Workflow Automation (pull vs push; Pub/Sub) | Definição de interface pública de eventos do Pipeline antes da Fase 2 | Formalizar como Published Language antes de implementar Workflow Automation |
| VAL-DDD-06 | `CommissionSnapshotCreated` como evento de domínio explícito vs implícito em `opportunity.won` | Rastreabilidade de comissão; consumidores (Audit Log, Reporting) precisam do snapshot para relatórios corretos | Adicionar `commission_snapshot.created` como evento explícito no BC Canvas do Opportunity Pipeline ou documentar formalmente que está implícito e consumidores devem ouvir `opportunity.won` |
| VAL-DDD-07 | (existente) Comportamento ao fechar oportunidade como Ganho com comissão em branco (alerta vs bloqueio) | UX e integridade de dados do snapshot de comissão | Definir no FRD (LAC-01 referenciado) |

---

## 19. Métricas da Validação

| Métrica | Quantidade |
|---|---|
| Subdomínios avaliados | 15 |
| Bounded contexts avaliados | 15 |
| Relações de context map avaliadas | 20+ |
| Módulos avaliados | 14 |
| Eventos de domínio avaliados | 20+ |
| Achados críticos | 0 |
| Achados altos | 3 |
| Achados médios | 3 |
| Achados baixos | 2 |
| Ajustes aplicados | 6 |
| Pontos a validar (novos) | 1 (VAL-DDD-06) |
| Pontos a validar (total, incluindo DDD-VAL-01..05 preexistentes) | 7 |

---

## 20. Parecer Final

### Classificação

**Aprovado com Ressalvas**

### Justificativa

A modelagem está arquiteturalmente coerente. O Core Domain está corretamente identificado com evidência estratégica múltipla (PRD, discovery, análise adversarial de concorrentes). Os 14 subdomínios Supporting e Generic têm classificação adequada e justificativa rastreável. O context map usa os padrões DDD corretos para cada relação após as correções desta revisão. Os agregados têm tamanho razoável, os objetos de valor estão corretamente identificados, e não há uso de "VO" nos artefatos.

Os 6 ajustes aplicados corrigem inconsistências de padrão DDD (Published Language vs Conformist) e um conflito de ownership de dados de configuração de BU — achados altos mas todos diretamente corrigíveis a partir dos próprios artefatos, sem decisão de produto.

A modelagem pode avançar para TRD, definição de módulos e backlog. As ressalvas são os pontos a validar DDD-VAL-01 (Activity Management) e DDD-VAL-02 (Reporting) que, se não resolvidos antes do TRD, podem gerar retrabalho na definição de deployables.

### Condições para Aprovação Total

1. Resolver DDD-VAL-01 antes do TRD de módulos
2. Resolver DDD-VAL-02 antes de especificar o `azim-reporting-worker`
3. Definir VAL-DDD-06 (evento explícito `commission_snapshot.created` ou documentar omissão formal)
4. Validar C4 Levels 1, 2 e 3 contra os bounded contexts, módulos e deployables definidos nesta revisão

### Próximos Passos

1. Revisar e decidir DDD-VAL-01 (Activity Management como BC vs módulo)
2. Revisar e decidir DDD-VAL-02 (Reporting como BC vs módulo técnico)
3. Decidir DDD-VAL-04 (Tenancy + Organization consolidation) antes da Fase 2
4. Adicionar `commission_snapshot.created` como evento explícito no BC Canvas do Opportunity Pipeline (ou documentar omissão — VAL-DDD-06)
5. Formalizar Published Language de eventos do Pipeline para Workflow Automation antes da Fase 2 (DDD-VAL-05)
6. Verificar C4 Levels 1, 2, 3 contra os deployables candidatos após consolidação dos pontos acima
7. Reexecutar validação após resolução dos pontos a validar altos
