# Discovery Notes — Azim CRM

**Produto:** Azim CRM
**Tipo de iniciativa:** Brownfield documental — produto novo construído sobre operação real existente (planilha)
**Data:** 2026-06-10
**Status:** Rascunho para revisão

> Todas as decisões marcadas neste documento foram aprovadas pelo Milton em 10/06/2026 e devem ser tratadas como fixas pelo PRD Generator.
> Valores monetários no modelo de dados são armazenados em **centavos inteiros** (integer cents). Valores contextuais neste documento usam R$ para legibilidade.

---

## 0. Workspace Scan

### 0.1 Diretórios e Arquivos Relevantes

| Caminho | Tipo | Contexto Oferecido |
|---|---|---|
| `docs/product/azim-product-spec.md` | Especificação mestre | Visão, marca, domínio, 13 módulos funcionais (RF-01..13), RBAC, NFRs, arquitetura GCP/.NET/React, roadmap em fases, migração da planilha, decisões registradas — fonte de verdade principal |
| `docs/research/analise-planilha-pipeline-vellus.md` | Análise de evidência primária | Mapeamento completo da planilha Excel atual da Vellus: 108 oportunidades, 3 BUs, dicionário de campos, dores mensuradas, implicações diretas para o modelo de domínio |
| `docs/research/analise-video-saas-multitenant.md` | Análise de referência técnica | Lições do vídeo TeamForge (SaaS multi-tenant com React Flow): arquitetura validada, riscos mitigados, diferenças de abordagem do Azim |
| `docs/research/deep-research-crm-multitenant.md` | Research adversarial | 5 frentes de pesquisa, 115 alegações, 25 verificadas adversarialmente (22 confirmadas, 3 refutadas): isolamento multi-tenant GCP, modelo PRM/comissão (Salesforce/Zoho), IA LangGraph/Langfuse, e-mail transacional, mercado CRM 2026 |

### 0.2 Resumo do Contexto Existente

O workspace contém uma especificação de produto aprovada (`azim-product-spec.md` v1.0, status "aprovada para fundação") e três documentos de research que a fundamentam. A especificação cobre integralmente produto, marca, modelo de domínio, requisitos funcionais (RF-01..13), RBAC, NFRs, arquitetura técnica GCP, roadmap em quatro fases e plano de migração da planilha.

A evidência primária do problema vem da análise da planilha `Pipeline Vellus.xlsx`, o sistema atual da Vellus (primeiro tenant): 108 oportunidades, três BUs, forecast ponderado de R$ 9,01 mi, com dores quantificadas (59% sem responsável, 6 de 108 com data de fechamento, vínculos manuais quebráveis, nenhuma meta cadastrada, sem controle de acesso ou trilha de auditoria).

A research adversarial confirmou o gap de mercado central do Azim — comissão de parceiro nativa na oportunidade não é entregue por Salesforce nem Zoho out-of-the-box — e validou as escolhas arquiteturais (pool + RLS variável de runtime, Identity Platform multi-tenant, LangGraph em produção em vendas).

### 0.3 Lacunas Identificadas

- Modelo de monetização do Azim como produto SaaS (pricing, planos, billing) não está definido; está explicitamente fora do escopo do MVP.
- Percentuais de comissão dos 11 parceiros da Vellus são inexistentes na planilha; serão preenchidos manualmente pela Vellus após o import.
- 64 oportunidades (59%) da planilha não têm responsável definido — exigem triagem manual obrigatória antes da conclusão do import.
- Portal do parceiro (login externo) não faz parte do MVP; é evolução futura.
- Multimoeda não está contemplada no escopo atual.
- App mobile não comprometida; está no horizonte do roadmap.

---

## 1. Visão

### 1.1 Problema

A operação comercial da Vellus — primeiro tenant do Azim — roda hoje numa planilha Excel compartilhada no OneDrive. O retrato em 10/06/2026 é o seguinte:

**Evidências quantificadas (fonte: `Pipeline Vellus.xlsx`):**

| Indicador | Valor | Dor que representa |
|---|---|---|
| Oportunidades ativas | 108 (3 BUs) | Volume real de gestão impossível sem sistema |
| Forecast ponderado total | R$ 9.013.250 | Valor em risco sem rastreabilidade |
| Oportunidades sem responsável | 64 de 108 (59%) | Ninguém é cobrado, nada anda; digest diário impossível |
| Oportunidades com data de fechamento | 6 de 108 (6%) | Forecast frágil; datas vencidas sem alerta |
| Parceiros citados sem estrutura de comissão | 24 oportunidades, 11 parceiros | Comissão vive fora do processo; sem cálculo nem rastreabilidade |
| Metas cadastradas | Nenhuma | Impossível comparar pipeline com objetivo |
| Trilha de auditoria | Inexistente | "Nada profissional e confiável" (descrição do usuário) |
| Controle de acesso | Inexistente | Dados pessoais de contatos (LGPD) expostos a todos |

**Dores qualitativas confirmadas:**
- Probabilidade de forecast e "última atualização" digitadas à mão — propensas a inconsistências e abandonos.
- Ações comerciais vinculadas ao pipeline por número digitado ("Relacionada ao Pipeline #43") — renumerou, perdeu o vínculo.
- Contato da negociação preso inline na linha da oportunidade — sem histórico por conta, sem reaproveitamento entre oportunidades.
- Contas duplicadas entre BUs (Pag.ai, Ethoca aparecem em Vellus e Axis como linhas independentes).
- Typos em campos livres: "Miilton", "Consulroria", "Ondoarding", "Prróximo"; BUs com espaço extra ("Sertão ").
- Arquivo único compartilhado — sem controle de versão, sem quem alterou o quê.

O Azim substitui esse modelo por um sistema com donos obrigatórios, prazos, automação de cobrança (digest diário) e comparação contínua com metas.

### 1.2 Usuário Principal

Cinco personas, com hierarquia de impacto:

| Persona | Descrição | O que o Azim entrega |
|---|---|---|
| **Vendedor / Executivo de contas** (ex.: Eduardo, Milton) | Conduz oportunidades em uma ou mais BUs; hoje gerencia sua carteira na planilha, sem to-do estruturado | Kanban claro do seu funil; to-do diário por e-mail às 7h (seg–sex); histórico da conta num só lugar |
| **Gestor de BU** | Responde pelo resultado de uma unidade de negócio; hoje não tem visão consolidada nem comparação com metas | Pipeline e forecast da BU; metas vs realizado; oportunidades estagnadas; carga por vendedor |
| **Executivo do tenant** (ex.: sócios da Vellus) | Visão consolidada da empresa; hoje depende de exportar e consolidar manualmente | Resumo de segunda: pipeline global por BU vs metas; tendências; top oportunidades |
| **Administrador do tenant** | Configura a empresa no Azim | Branding, usuários, papéis, BUs, estágios, parceiros, metas |
| **Operador da plataforma (Vellus)** | Opera o Azim como produto SaaS | Provisionamento de tenants, monitoramento, suporte — sem acesso a dados comerciais dos tenants por padrão |

O parceiro comissionado não tem login no MVP — é entidade gerida pelo tenant. Portal do parceiro é evolução futura.

### 1.3 Referências

| Referência | O que foi absorvido | O que o Azim faz diferente |
|---|---|---|
| **Pipeline Vellus.xlsx** | Modelo de valor (Setup + Mensal × Meses), estágios do funil, numeração sequencial de propostas, estrutura de canais e parceiros | Substitui completamente — a planilha congela após a migração |
| **TeamForge** (vídeo SaaS multi-tenant com React Flow, Helio Arreche, 2026) | Arquitetura pool + RLS validada na prática; Redis com três papéis (cache/rate-limit/fila); automação visual React Flow; migrations automáticas no deploy; testes E2E de isolamento em CI | GCP 100% Terraform (sem VPS manual); backend .NET 10 (não Next.js); digest diário como requisito de primeira classe; BUs como hierarquia; comissão de parceiro nativa |
| **HubSpot** | Primitivas de domínio (objetos, registros, propriedades, associações); pipeline como mecanismo de estágios genérico; Lead como objeto distinto (registrado para evolução futura) | Lead como estágio do funil no v1 (espelha operação atual da Vellus) |
| **Salesforce** | OpportunityPartner (relação de primeira classe tipada por papel); OpportunitySplit (split filho da oportunidade: beneficiário + % + valor calculado); ForecastingQuota (meta como objeto por escopo e período) | Comissão nativa no vínculo (gap: Salesforce não oferece isso out-of-the-box) |
| **Zoho CRM** | Disparo do snapshot de comissão em Closed Won (Ganho) | Comissão calculada para parceiros externos (Zoho só faz para vendedores internos) |
| **Pipedrive** | Kanban fluido como higiene de mercado | Diferencial não é o kanban — é comissão nativa + digest diário + IA (Fase 3) |

### 1.4 Pitch

O Azim é o CRM corporativo multi-tenant da Vellus — um sistema de navegação comercial que transforma pipeline em direção. Substitui a planilha Excel pela qual a Vellus gerencia 108 oportunidades e R$ 9 mi em forecast, entregando pipeline estruturado com dono obrigatório, comissão de parceiro nativa na oportunidade e digest diário que leva o CRM até o vendedor às 7h, em vez de esperar o vendedor abrir o sistema. Projetado desde o primeiro dia para múltiplos tenants com white-label e para crescer em automação e IA.

> Tagline oficial: **"Direção para vender melhor."**

---

## 2. Funcionalidades

### 2.1 Core Features

O escopo é organizado em quatro fases incrementais; cada fase termina com critérios verificáveis.

#### Fase 0 — Fundação (pré-MVP)

- Terraform completo dos três ambientes (dev/stg/prd) no GCP.
- Identity Platform multi-tenant provisionado via Terraform.
- Esqueleto .NET 10 + React SPA com resolução de slug e theming por tenant.
- RLS ativa com testes de isolamento em CI (tenant A não lê dados do tenant B via API e via SQL sem contexto).
- E-mail transacional configurado (SPF/DKIM/DMARC validados no ambiente prd).

**Critério de conclusão:** dois tenants de teste com brandings distintos logam isolados em prd; teste de CI prova isolamento.

#### Fase 1 — MVP comercial (substitui a planilha)

Módulos incluídos: RF-01 a RF-07, RF-09 (digest completo incluindo segunda-feira), RF-11 (relatórios enxutos), RF-08 (metas básicas sem projeções), auditoria e migração da planilha.

**RF-01 — Autenticação e acesso:**
Login por e-mail/senha e Google via Identity Platform multi-tenant; convite por e-mail com ativação; recuperação de senha; sessão por tenant resolvida pelo slug.

**RF-02 — Administração do tenant (white-label estrito):**
Logo (PNG/SVG ≤ 1 MB), favicon, cor primária, cor secundária, sobre o slug do tenant (`app.azim.com.br/{slug}`). Validação de contraste WCAG AA no upload. Nada além disso é customizável (sem CSS, fonte ou layout por tenant).

**RF-03 — BUs, usuários e papéis:**
CRUD de BUs; convite de usuários com papel e memberships por BU; desativação preserva histórico (FKs intactas).

**RF-04 — Contas e contatos:**
CRUD com busca e dedupe por nome normalizado (alerta "conta similar existe"); visão 360° da conta: oportunidades de todas as BUs visíveis, contatos, atividades, histórico.

**RF-05 — Parceiros:**
CRUD de parceiros com papel tipado e percentuais default; visão do parceiro: oportunidades originadas, comissão projetada (pipeline) e consolidada (ganhas) por período.

**RF-06 — Pipeline e oportunidades:**
- Kanban por BU com drag-and-drop entre estágios; colunas mostram soma de valor total e forecast ponderado.
- Visão lista com filtros salvos (por owner, canal, parceiro, estágio, data de fechamento, estagnadas).
- Criação rápida (conta nova inline) e formulário completo.
- Modelo de valor: `valor_total = valor_setup + valor_mensal × duracao_meses`; `forecast_ponderado = valor_total × probabilidade`. Todos os campos de valor armazenados como centavos inteiros.
- Comissão de parceiro nativa (`OpportunityPartnerCommission`): `pct_setup`, `pct_recorrente`, `valor_fixo` opcional, `meses_comissionados`; snapshot imutável ao mover para Ganho.
- Numeração automática e imutável por tenant (ex.: `AZ-0095`), substitui a fórmula `LARGE()+1` da planilha.
- Linha do tempo da oportunidade: estágios, atividades, notas, alterações (audit).
- `owner_id` obrigatório (ataca os 59% sem responsável).
- `data_fechamento_esperada` obrigatória a partir do estágio "Proposta Enviada".
- `motivo_perda` obrigatório ao mover para Perdido (lista configurável).

**Estágios seed (configuráveis por BU):**

| Ordem | Estágio | Prob. default | Categoria |
|---|---|---|---|
| 1 | Lead | 10% | aberta |
| 2 | Prospecção | 25% | aberta |
| 3 | Diagnóstico | 50% | aberta |
| 4 | Proposta Enviada | 60% | aberta |
| 5 | Negociação | 75% | aberta |
| 6 | Fechamento Provável | 90% | aberta |
| 7 | Ganho | 100% | ganha |
| 8 | Perdido | 0% | perdida |

**Canais de origem** (lista configurável; seed): Parceiro (exige `partner_id`), Indicação, Prospecção ativa (outbound), Inbound (site/conteúdo), Evento, Base/Cliente existente, Outro.

**RF-07 — Atividades e follow-ups:**
CRUD; visão "Meu dia / Minha semana" (vencidas, hoje, próximas); conclusão em 1 clique a partir da lista ou do digest (link autenticado); ao concluir, sugestão de próxima atividade.

**RF-08 — Metas e forecast (básico no MVP):**
Cadastro de metas mensais por BU e/ou responsável; sem meta cadastrada, painel mostra apenas realizado vs pipeline (graceful degradation). Projeções avançadas ficam na Fase 2.

**RF-09 — Digest diário por e-mail (requisito de primeira classe):**
- Cloud Scheduler dispara a cada hora (UTC) endpoint autenticado (OIDC); o serviço seleciona tenants cuja hora local (IANA por tenant) = `horario_digest` (default 07:00) e o dia é útil (seg–sex) no fuso do tenant.
- **Terça a sexta (por usuário com pendências):** saudação no tom da marca; atividades vencidas (1-clique concluir/reagendar); atividades de hoje; oportunidades estagnadas (>14 dias sem atividade) e datas de fechamento vencidas; link direto com branding do tenant.
- **Segunda-feira ("o azimute da semana"):** resumo do pipeline por estágio e BU; variação vs semana anterior; status global (ganho no mês/trimestre, top oportunidades, fechamentos esperados na semana, segmentação por canal); bloco de metas (realizado vs meta, gap, pipeline disponível para cobrir o gap) — bloco omitido se não houver meta cadastrada.
- Idempotência por `EmailDigestLog` (usuário + data); usuário sem pendências e sem papel de gestão não recebe.

**RF-11 — Relatórios (MVP enxuto):**
Funil por estágio; forecast por BU/mês; ranking por responsável; oportunidades por canal; comissões por parceiro (projetado × consolidado); export CSV.

**Migração da planilha:**
Upload do `Pipeline Vellus.xlsx` → dry-run com relatório (contagens, dedupes, flags, divergências de forecast) → triagem assistida (owners e estágios faltantes) → import transacional → relatório final auditado → planilha congelada (read-only no OneDrive) com banner apontando para o Azim.

**Critério de conclusão da Fase 1:** a Vellus opera as 3 BUs no Azim por 2 semanas sem voltar à planilha; digest diário chegando às 07:00 BRT seg–sex; 100% das oportunidades migradas com owner definido.

#### Fase 2 — Direção (automação e gestão)

**RF-12 — Automações visuais com React Flow:**
Editor de workflows por BU — nós: Gatilho (eventos de domínio), Condição, Ações (criar atividade, atualizar campo, notificar in-app, enviar e-mail, webhook), Fim. Execução sempre assíncrona (Pub/Sub → worker) com log de execução por nó e botão "Executar teste". Templates prontos incluídos.

RF-08 completo (projeções, painel Direção), RF-10 (notificações in-app), dashboards avançados, webhooks de saída, calendário de feriados no digest.

**Critério:** os 3 templates de automação rodam em produção com log por nó e zero execução síncrona.

#### Fase 3 — Inteligência (IA)

**RF-13 — Agentes de IA (`azim-ai` em Python/FastAPI):**
- Lead/opportunity scoring (batch noturno via Pub/Sub).
- Resumo de conta: briefing 360° antes da reunião (sob demanda).
- Próxima melhor ação: sugestão por oportunidade com justificativa.
- Copilot de vendas: conversacional sobre os dados do tenant (REST síncrono).
- Observabilidade total com Langfuse (prompt, resposta, tokens, latência, passos intermediários).
- Rate-limit de IA por tenant via Redis; dados jamais cruzam tenants.

**Critério:** copilot responde sobre dados do tenant com tracing completo no Langfuse e custo por tenant visível.

#### Horizonte (não comprometido)

Portal do parceiro com deal registration; múltiplos parceiros por oportunidade; Lead como entidade separada (padrão HubSpot); integração WhatsApp; multimoeda; app mobile.

### 2.2 Integrações

| Sistema | Papel | Fase |
|---|---|---|
| **GCP Identity Platform** (multi-tenant) | Autenticação e gestão de identidade — um tenant de identidade por tenant Azim | Fase 0 |
| **Cloud SQL / Postgres** (pooled + RLS) | Banco de dados principal, fonte de verdade | Fase 0 |
| **Memorystore / Redis** | Cache de sessão/membership (<1 ms), rate-limit por tenant, sessões | Fase 0 |
| **Cloud Pub/Sub** | Barramento de eventos de domínio; execução assíncrona de digest, automações e IA | Fase 0 |
| **Cloud Scheduler** | Trigger horário UTC para o digest diário | Fase 0 |
| **Cloud Storage + CDN** | Hosting da SPA React e assets de branding por tenant | Fase 0 |
| **Cloud Armor** | WAF + rate limit de borda | Fase 0 |
| **Secret Manager** | Gestão de segredos — nunca em repositório ou variável de ambiente não gerenciada | Fase 0 |
| **Postmark** (candidato primário; `IEmailSender` abstraction) | E-mail transacional para convites, digest e notificações | Fase 1 |
| **Terraform** (100% IaC) | Provisionamento de toda a infraestrutura GCP | Fase 0 |
| **GitHub Actions + Workload Identity Federation** | CI/CD sem chave de service account em repositório | Fase 0 |
| **LangGraph + Langfuse** | Orquestração de agentes e observabilidade de IA | Fase 3 |

---

## 3. Monetização

### 3.1 Modelo

Billing do Azim como produto SaaS está explicitamente fora do escopo do MVP. O primeiro tenant (Vellus) utiliza o produto como operador da plataforma — a relação comercial interna é definida fora do escopo desta especificação.

Referência de mercado coletada (para posicionamento futuro): CRMs do mid-market em 2026 operam entre US$ 11 (folk) e US$ 29/usuário/mês (Attio); HubSpot a partir de US$ 20 com free tier. Benchmark a revalidar antes de decisões de pricing.

### 3.2 Planos

Não aplicável ao MVP. A estrutura técnica multi-tenant e o rate-limit de IA por tenant (Fase 3 via Redis) estão desenhados para suportar diferenciação de planos futura sem retrabalho de arquitetura.

---

## 4. Técnico

### 4.1 Stack

Stack aprovada (fixe; não alterar sem decisão explícita):

| Camada | Tecnologia | Observações |
|---|---|---|
| **Cloud** | GCP 100% Terraform | Região primária: `southamerica-east1`; um projeto GCP por ambiente (dev/stg/prd) |
| **Banco de dados** | Cloud SQL / Postgres (pooled + RLS) | PITR ativo; RPO ≤ 5 min; RTO ≤ 4 h; restore testado |
| **Cache / rate-limit** | Memorystore / Redis | Cache de sessão e memberships; rate-limit por tenant; fila leve |
| **Mensageria** | Cloud Pub/Sub | Barramento de eventos de domínio; execução assíncrona |
| **Scheduler** | Cloud Scheduler | Job horário UTC + hora local IANA por tenant para o digest |
| **Backend API** | .NET 10 / ASP.NET Core (REST + OpenAPI) | EF Core / Npgsql; FluentValidation; outbox pattern → Pub/Sub |
| **Workers** | .NET 10 (Cloud Run separado) | Consumidores Pub/Sub: digest, automações (Fase 2) |
| **Frontend** | React + TypeScript + React Flow (SPA) | Theming via CSS variables; deploy em GCS + CDN |
| **IA** | Python / FastAPI + LangGraph + Langfuse | Cloud Run; REST síncrono (copilot) + Pub/Sub assíncrono (scoring batch); Fase 3 |
| **Auth** | GCP Identity Platform (multi-tenant) | Um tenant de identidade por tenant Azim |
| **E-mail** | Postmark (candidato primário) atrás de `IEmailSender` | Domínio `mail.azim.com.br` com SPF/DKIM/DMARC; SendGrid como alternativa avaliada no spike |
| **IaC** | Terraform | Backend de estado em GCS; CI executa `plan` em PR; `apply` com aprovação manual em prd |
| **CI/CD** | GitHub Actions + Workload Identity Federation | Sem chave de SA em repositório; migrations EF Core como job de release antes do tráfego |
| **Observabilidade** | Cloud Logging / Monitoring / Trace | `tenant_id` como label estruturado; sem PII nos logs; alertas de 5xx, latência, falha de digest, fila crescendo, falha de RLS |
| **Segurança de borda** | Cloud Armor | WAF + rate limit; CSP estrita |

**Multi-tenancy — defesa em profundidade:**
1. Filtro primário: EF Core global query filter por `tenant_id`.
2. Segunda linha: RLS no Postgres via variável de runtime (`SET app.current_tenant = @tenant_id`; middleware .NET resolve tenant por slug → id, com cache Redis).
3. Vazamento entre tenants = incidente sev-1; testes de isolamento em CI como gate obrigatório.

### 4.2 Plataforma

O Azim é uma aplicação web responsiva (SPA). A URL canônica usa slug como prefixo de path: `app.azim.com.br/{slug}/…`.

App mobile não faz parte do MVP nem de nenhuma fase comprometida — está no horizonte do roadmap. A decisão de investir em app nativo ou PWA será tomada após a Fase 1 validada em produção.

---

## 5. Contexto

### 5.1 Referências Visuais

Nenhuma referência visual (wireframe, Figma, protótipo) foi fornecida. As referências conceituais de UX são:

- **Kanban com drag-and-drop:** padrão Pipedrive (higiene de mercado em 2026).
- **Automação visual:** React Flow, conforme demonstrado no vídeo TeamForge.
- **Tom visual da marca:** corporativo, objetivo, confiável, tecnológico e estratégico. O Azim deve parecer uma plataforma de direção comercial, não apenas uma ferramenta de vendas. Aplicado em UI, e-mails (digest), documentação e mensagens de erro.
- **White-label:** o tenant aplica logo, favicon, cor primária e cor secundária — design tokens como CSS variables; tons derivados automaticamente; validação de contraste WCAG AA no upload.

### 5.2 Notas Adicionais

**Tenant nº 1 — Vellus (3 BUs):**

| BU | Oportunidades atuais | Observações |
|---|---|---|
| Vellus | 71 | BU principal |
| Axis | 20 | Compartilha contas com Vellus (ex.: Pag.ai, Ethoca) |
| Vellus Tech | 17 | — |

A Vellus é simultaneamente o primeiro tenant e o operador da plataforma (Vellus como empresa de tecnologia). Essa dupla natureza define dois contextos de uso: a Vellus usa o Azim como CRM da sua operação comercial (Fase 1), e a Vellus opera o Azim como produto SaaS para outros tenants (Fases 2+).

**Conformidade e regulação:**
- LGPD: dados pessoais de contatos (nome, e-mail, celular) são dados pessoais sob a LGPD. Requer consentimento, minimização e direito ao esquecimento. Detalhamento na fase de requisitos (NFRD).
- Auditoria: toda escrita em entidades de negócio registrada com autor, delta e timestamp em `AuditLog`.
- Acessibilidade: WCAG AA, incluindo contraste das cores customizadas do tenant.

**RBAC — matriz de permissões (MVP):**

| Capacidade | Platform Op | Tenant Admin | Gestor de BU | Vendedor | Viewer |
|---|---|---|---|---|---|
| Provisionar/suspender tenants | Sim | — | — | — | — |
| Branding, usuários, BUs, estágios | — | Sim | — | — | — |
| Parceiros e % default | — | Sim | Sim (sua BU) | — | — |
| Metas | — | Sim | Sim (sua BU) | ver as suas | — |
| Oportunidades — ver | — | todas | da(s) sua(s) BU(s) | das suas BUs | escopo designado |
| Oportunidades — criar/editar | — | Sim | Sim (sua BU) | as suas (owner) | — |
| Reabrir ganha/perdida; editar comissão pós-ganho | — | Sim | Sim (sua BU) | — | — |
| Atividades | — | Sim | Sim (sua BU) | as suas | ver |
| Relatórios | — | tenant | BU | os seus | escopo |
| Auditoria | — | Sim | sua BU | — | — |
| Dados comerciais dos tenants | Bloqueado (apenas suporte autorizado e auditado) | — | — | — | — |

Papéis "aparentemente funcionando" são o risco nº 1 (lição do vídeo TeamForge). Esta matriz vira suíte de testes automatizados por papel desde o primeiro sprint.

**Numeração de propostas:** a planilha usa fórmula `LARGE(B4:B500,1)+1`; próximo número: 95. O Azim adota numeração sequencial automática e imutável por tenant (`AZ-0095`).

**Formato de e-mail do digest:** `IEmailSender` com Postmark como candidato primário. Domínio de envio dedicado `mail.azim.com.br`. Supressão e bounce handling registrados. Webhooks de evento (entregue/aberto) alimentam métricas do digest.

---

## 6. Decisões Registradas

| Código | Decisão | Origem | Impacto |
|---|---|---|---|
| DEC-001 | Estágios de pipeline são configuráveis por BU, com seed derivado do processo atual da Vellus (Lead→Prospecção→Diagnóstico→Proposta Enviada→Negociação→Fechamento Provável→Ganho/Perdido) | Aprovação Milton 10/06/2026 | Orienta modelo de domínio `PipelineStage`, UI de configuração, migração da planilha |
| DEC-002 | Comissão de parceiro em % por componente (pct_setup, pct_recorrente separados), com valor fixo opcional e snapshot imutável ao mover a oportunidade para Ganho | Aprovação Milton 10/06/2026 | Orienta entidade `OpportunityPartnerCommission`, trigger de congelamento, relatório de comissões |
| DEC-003 | Metas por BU e por responsável, granularidade mensal; agregações trimestral e anual são derivadas | Aprovação Milton 10/06/2026 | Orienta entidade `Goal`, painel Direção (RF-08), bloco de metas no digest de segunda |
| DEC-004 | White-label estrito: logo, favicon, slug-prefixo, cor primária e cor secundária — nada além. Sem CSS, fonte ou layout customizável por tenant | Aprovação Milton 10/06/2026 | Mantém custo de manutenção linear; orienta RF-02, design tokens, validação de contraste WCAG AA |
| DEC-005 | Stack GCP 100% Terraform; backend .NET 10; SPA React + TypeScript + React Flow; IA em Python (LangGraph + Langfuse) na Fase 3 | Aprovação Milton 10/06/2026 | Define todas as escolhas tecnológicas; orienta TRD e ADRs |
| DEC-006 | Multi-tenancy pooled com RLS Postgres como defesa em profundidade (segunda linha); filtro primário é EF Core global query filter por tenant_id | Aprovação Milton 10/06/2026 + research adversarial [✓ 3-0] | Orienta arquitetura de dados, middleware .NET, runtime var RLS, testes de isolamento em CI |
| DEC-007 | Lead é estágio do funil (não entidade separada) no v1; padrão HubSpot (Lead como objeto distinto) registrado para evolução futura | Decisão de produto; espelha operação atual da Vellus | Simplifica o modelo de domínio no MVP; deve ser revisitada ao implementar portal do parceiro ou lead inbound |
| DEC-008 | E-mail transacional: Postmark como candidato primário atrás da abstração IEmailSender; SendGrid como alternativa avaliada no spike do MVP. Critério: entregabilidade > templates > custo | Research [coletada]: Postmark 93,8% de entregabilidade medida | Orienta RF-09, Fase 0 (SPF/DKIM/DMARC), spike de validação |
| DEC-009 | Digest diário: Cloud Scheduler dispara job horário em UTC; o serviço compara hora local IANA configurada por tenant para decidir o envio — neutraliza DST, evita um job por tenant | Research [coletada]: recomendação GCP de operar em UTC | Orienta arquitetura do agendador, configuração do Terraform para o Scheduler, campo `fuso_horario` no tenant |
| DEC-010 | Tenant nº 1 é a Vellus com 3 BUs (Vellus, Axis, Vellus Tech); o import da planilha Pipeline Vellus.xlsx é requisito obrigatório da Fase 1 | Fonte primária: planilha analisada em 10/06/2026 | Define a migração como requisito de produto, não item opcional |
| DEC-011 | Valores monetários armazenados como centavos inteiros (integer cents) em todas as entidades do modelo de dados | Convenção do projeto (CLAUDE.md / AGENTS.md) | Orienta schema do banco, serialização de API, cálculos de comissão e forecast |
| DEC-012 | O parceiro comissionado não tem login no MVP — é entidade gerida pelo tenant. Portal do parceiro (deal registration) é evolução futura no horizonte do roadmap | Decisão de escopo MVP | Simplifica Fase 1; parceiro tratado como dado, não como usuário |
| DEC-013 | Billing do Azim como produto SaaS está fora do escopo do MVP | Decisão de escopo | Não há módulo de cobrança, Stripe ou faturamento no MVP |

---

## 7. Pontos a Validar

| Código | Ponto | Motivo | Impacto |
|---|---|---|---|
| VAL-001 | Percentuais de comissão dos 11 parceiros da Vellus (Montanaro, Salto, JV Korporate, Bus2, Finaya, Paytime, Nelson e outros) | São inexistentes na planilha — a Vellus precisará definir e preencher pós-import | Impacta RF-05, RF-06, relatório de comissões; o import deve deixar campo em branco com flag de triagem |
| VAL-002 | Atribuição de owner para as 64 oportunidades (59%) que estão sem responsável na planilha | O import transacional não pode concluir sem owner definido; exige triagem manual pela Vellus | Impacta o critério de conclusão da Fase 1 ("100% das oportunidades migradas com owner definido") |
| VAL-003 | Provedor de e-mail transacional: Postmark vs SendGrid | A decisão é Postmark como candidato primário, mas o spike de validação (entregabilidade + SPF/DKIM/DMARC) está previsto no MVP e pode confirmar ou reverter a escolha | Impacta RF-09 (digest), RF-01 (convites), todas as notificações por e-mail |
| VAL-004 | Definição de pricing e planos do Azim como produto SaaS | Não definido; a estrutura técnica suporta diferenciação futura (rate-limit por tenant, multi-tenancy) | Impacta roadmap comercial pós-MVP; não bloqueia desenvolvimento |
| VAL-005 | Capacidade de multimoeda | Não contemplada; todos os valores assumem BRL. Se a Vellus fechar negócios em USD ou EUR antes da Fase 2, pode ser necessário antecipar | Impacta modelo de dados (currency field), cálculo de forecast e comissões |
| VAL-006 | Calendário de feriados para o digest (dias úteis além de seg–sex) | A Fase 2 inclui "calendário de feriados no digest", mas a lógica exata de quais feriados e para quais tenants não está especificada | Impacta RF-09 na Fase 2; pode exigir integração com fonte de feriados por país/estado |

---

## 8. Resumo Final do Discovery

### 8.1 Resumo por Blocos

**Visão:** O Azim é um CRM SaaS multi-tenant B2B da Vellus, construído para resolver um problema real e quantificado: a operação comercial da própria Vellus roda numa planilha Excel com 108 oportunidades, R$ 9 mi em forecast, 59% sem dono e zero metas cadastradas. O produto substitui a planilha pela Fase 1 e cresce em automação (Fase 2) e IA (Fase 3). O nome e a marca (azimute, "Direção para vender melhor") refletem o posicionamento de plataforma de orientação comercial — não apenas ferramenta de vendas.

**Funcionalidades:** MVP (Fases 0+1) cobre autenticação, administração do tenant com white-label estrito, BUs/usuários/papéis, contas e contatos com dedupe, parceiros com comissão nativa, pipeline Kanban com estágios configuráveis por BU e modelo de valor Setup+Recorrente, atividades estruturadas, digest diário por e-mail (requisito de primeira classe), metas básicas, relatórios enxutos e migração completa da planilha da Vellus. Automações visuais (React Flow) na Fase 2; agentes de IA (LangGraph/Langfuse) na Fase 3.

**Diferenciais competitivos:** (1) comissão de parceiro nativa na oportunidade — gap confirmado em Salesforce e Zoho; (2) digest diário acionável que leva o CRM ao vendedor em vez do contrário; (3) modelo Setup+Recorrente nativo para contratos B2B de implantação + mensalidade; (4) IA com guardrails na Fase 3, num mercado onde os comparativos de 2026 não citam IA como diferencial entregue.

**Monetização:** fora do escopo do MVP. Referência de mercado: US$ 11–29/usuário/mês no mid-market.

**Técnico:** GCP 100% Terraform (southamerica-east1); .NET 10 REST API + EF Core; React SPA + React Flow; Postgres pooled + RLS; Redis (cache/rate-limit); Pub/Sub (eventos); Cloud Scheduler (digest); Identity Platform multi-tenant; Postmark (e-mail); Python/FastAPI + LangGraph + Langfuse (IA, Fase 3). Plataforma web responsiva; app mobile no horizonte.

**Tenant nº 1:** Vellus com 3 BUs (Vellus 71 opps, Axis 20, Vellus Tech 17). Vellus é simultaneamente o primeiro cliente e o operador da plataforma.

**Decisões fixas:** 13 decisões aprovadas em 10/06/2026 (DEC-001 a DEC-013), cobrindo estágios, comissão, metas, white-label, stack, multi-tenancy, lead como estágio, e-mail, scheduler, migração, convenção de money em centavos inteiros, parceiro sem login no MVP e billing fora do MVP.

**Riscos e lacunas:** 6 pontos a validar (VAL-001 a VAL-006), sendo os mais críticos para a Fase 1: atribuição das 64 oportunidades sem owner (VAL-002) e definição dos percentuais de comissão dos parceiros (VAL-001).

### 8.2 Confirmação do Usuário

Rascunho entregue para revisão do Milton em 10/06/2026. Todas as decisões foram extraídas dos documentos aprovados; nenhuma inferência foi adicionada sem respaldo documental.

### 8.3 Status

Rascunho para revisão
