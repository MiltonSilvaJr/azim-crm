# Azim — Especificação de Produto

> **Azim é o CRM corporativo da Vellus que dá direção à gestão de clientes, oportunidades e crescimento.**
> *Tagline: **Direção para vender melhor.***
>
> **Versão:** 1.0 · **Data:** 10/06/2026 · **Status:** especificação aprovada para fundação
> **Documentos de apoio:** [análise do vídeo de referência](../research/analise-video-saas-multitenant.md) · [análise da planilha atual](../research/analise-planilha-pipeline-vellus.md) · [deep research](../research/deep-research-crm-multitenant.md)

---

# Parte I — Produto e marca

## 1. Visão

O Azim é o CRM SaaS **multi-tenant** da Vellus para empresas B2B que precisam enxergar sua carteira, conduzir seus times comerciais e tomar decisões com precisão. Em vez de tratar relacionamento com clientes como um conjunto disperso de contatos, tarefas e oportunidades, o Azim organiza a jornada comercial como um **sistema de navegação**: mostra onde a empresa está, para onde deve ir, quais contas exigem atenção, quais oportunidades estão em movimento e quais decisões precisam ser tomadas.

O nome nasce de **azimute** — a referência usada para definir uma direção. No contexto comercial, Azim representa exatamente isso: o ponto de orientação que ajuda equipes, gestores e empresas a conduzirem relacionamentos com mais clareza, previsibilidade e controle.

### Manifesto

> Toda empresa precisa vender.
> Mas empresas fortes não vendem apenas por esforço. Elas vendem com **direção**.
>
> Direção para entender melhor seus clientes. Direção para priorizar oportunidades. Direção para acompanhar negociações. Direção para transformar relacionamento em crescimento real.
>
> O Azim nasce para ser esse ponto de orientação. Um CRM corporativo criado pela Vellus para empresas que precisam enxergar sua carteira, conduzir seus times comerciais e tomar decisões com mais precisão.
>
> Porque relacionamento sem direção vira ruído. Pipeline sem clareza vira previsão frágil. E crescimento sem orientação vira acaso.
>
> **Azim. Direção para relacionamentos que movem negócios.**

### Tom de marca

Corporativo, objetivo, confiável, tecnológico e estratégico. O Azim não deve parecer apenas uma ferramenta de vendas — deve parecer uma **plataforma de direção comercial**. Esse tom orienta UI, e-mails (digest), documentação e mensagens de erro.

## 2. O problema (evidência real)

A operação comercial da própria Vellus — primeiro tenant do Azim — roda hoje numa planilha Excel compartilhada ([análise completa](../research/analise-planilha-pipeline-vellus.md)). Retrato em 10/06/2026: **108 oportunidades** em 3 BUs, forecast ponderado de **R$ 9,01 mi**, e:

- **59% das oportunidades sem responsável** — ninguém é cobrado, nada anda;
- **6 de 108** com data esperada de fechamento; várias vencidas sem atualização;
- follow-ups em texto livre, vínculo entre ação e oportunidade por número digitado;
- parceiro citado por nome, **sem estrutura de comissão**;
- **nenhuma meta cadastrada** para comparar com o pipeline;
- sem trilha de alteração, sem controle de acesso, dados pessoais de contatos expostos.

O Azim substitui esse modelo por um sistema com donos, prazos, automação de cobrança (digest diário) e comparação contínua com metas.

## 3. Personas

| Persona | Quem é | O que o Azim entrega |
|---|---|---|
| **Vendedor / Executivo de contas** (ex.: Eduardo, Milton) | Conduz oportunidades em uma ou mais BUs | Kanban claro do seu funil; to-do diário por e-mail às 7h; histórico da conta num lugar só |
| **Gestor de BU** | Responde pelo resultado de uma unidade de negócio | Pipeline e forecast da BU; metas vs realizado; oportunidades estagnadas; carga por vendedor |
| **Executivo do tenant** (ex.: sócios da Vellus) | Visão consolidada da empresa | Resumo de segunda-feira: pipeline global, por BU, vs metas; tendências |
| **Administrador do tenant** | Configura a empresa no Azim | Branding (logo, favicon, cores), usuários e papéis, BUs, estágios, parceiros, metas |
| **Operador da plataforma (Vellus)** | Opera o Azim como produto | Provisionamento de tenants, monitoramento, suporte — sem acesso a dados comerciais dos tenants por padrão |

*Parceiro comissionado não tem login no MVP — é entidade gerida pelo tenant. Portal do parceiro é evolução futura (§17).*

## 4. Posicionamento competitivo

Pesquisa de mercado ([deep research, frentes 2 e 5](../research/deep-research-crm-multitenant.md)) mostra que kanban fluido e modelo de dados bem desenhado são **higiene** de mercado (Pipedrive, Attio), não diferencial. Os diferenciais defensáveis do Azim:

1. **Comissão de parceiro nativa na oportunidade** — verificado: nem o modelo padrão da Salesforce (`OpportunityPartner` não tem campo de comissão) nem o Zoho (extensão, e só para vendedores internos) entregam isso out-of-the-box. O Azim trata parceiro→comissão→oportunidade como cidadão de primeira classe.
2. **Digest diário acionável** — o CRM vai até o usuário (e-mail seg–sex com o dia planejado), em vez de esperar o usuário abrir o CRM. Na segunda, o gestor recebe o azimute da semana: pipeline vs meta.
3. **Modelo de valor Setup + Recorrente** — contratos B2B de implantação + mensalidade (como os da Vellus) são nativos: forecast pondera os dois componentes, sem gambiarra de campo customizado.
4. **(Fase 3) IA com guardrails** — copilot e scoring sobre LangGraph/Langfuse, num mid-market onde os comparativos de 2026 ainda não apontam IA como diferencial entregue.

---

# Parte II — Especificação funcional

## 5. Modelo de domínio

Primitivas inspiradas no padrão consolidado de mercado (objetos, registros, propriedades, associações — HubSpot [✓]) com as decisões aprovadas pela Vellus.

```
Plataforma (Vellus)
└── Tenant (empresa cliente)  ── branding: logo, favicon, slug, cor primária, cor secundária
    ├── Business Unit (1..N)  ── ex.: Vellus, Axis, Vellus Tech
    │   ├── Pipeline (estágios configuráveis por BU)
    │   ├── Metas da BU (mensal)
    │   └── Oportunidades
    ├── Usuários (papéis; membership por BU)
    ├── Contas (únicas no tenant, compartilhadas entre BUs)
    │   └── Contatos (pessoas)
    ├── Parceiros (comissionados)
    ├── Metas por responsável
    ├── Atividades (follow-ups, reuniões, tarefas)
    ├── Automações (fase 2)
    └── Trilha de auditoria
```

### 5.1 Entidades principais

**Tenant** — `id`, `nome`, `slug` (único global, imutável após criação, minúsculas/hífens), `logo_url`, `favicon_url`, `cor_primaria`, `cor_secundaria`, `fuso_horario` (IANA, default `America/Sao_Paulo`), `horario_digest` (default 07:00), `status`.

**BusinessUnit** — `tenant_id`, `nome`, `ativa`. Toda oportunidade pertence a exatamente uma BU. *(Evidência: a planilha da Vellus já opera 3 BUs — Vellus 71, Axis 20, Vellus Tech 17 oportunidades.)*

**User** — `tenant_id`, `nome`, `email`, `identity_uid` (Identity Platform), `papel` (§7), `bus[]` (memberships). Usuário pertence a um tenant; pode atuar em várias BUs.

**Account (Conta)** — `tenant_id`, `nome` (único normalizado no tenant), `cnpj?`, `segmento?`, `site?`. **Única por tenant e compartilhada entre BUs** — evidência da planilha: Pag.ai e Ethoca aparecem hoje duplicadas em Vellus e Axis; Autopass tem 4 oportunidades. A conta consolida o relacionamento; a oportunidade carrega a BU.

**Contact (Contato)** — `tenant_id`, `account_id`, `nome`, `email?`, `telefone?`, `cargo?`. N contatos por conta; oportunidade referencia 1..N contatos (com um principal). Dado pessoal sob LGPD (§13).

**Partner (Parceiro)** — `tenant_id`, `nome`, `tipo_papel_default` (Indicador, Revendedor, Distribuidor, Integrador — papel tipado, padrão Salesforce `Role` [✓]), `comissao_default_setup_pct?`, `comissao_default_recorrente_pct?`, contato do parceiro.

**PipelineStage** — `bu_id`, `nome`, `ordem`, `probabilidade_default`, `categoria` (`aberta` | `ganha` | `perdida`). **Configurável por BU** (decisão aprovada). Seed de fábrica = processo atual da Vellus:

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

**Opportunity (Oportunidade)** — núcleo do produto:

| Campo | Regra |
|---|---|
| `numero` | Sequencial por tenant, gerado pelo sistema, imutável (ex.: `AZ-0095`) — substitui a fórmula `LARGE()+1` da planilha |
| `titulo`, `descricao` | |
| `bu_id`, `account_id` | Obrigatórios |
| `owner_id` | **Obrigatório** — ataca os 59% sem responsável |
| `stage_id` | Obrigatório; mudança de estágio gera evento `opportunity.stage_changed` |
| `probabilidade` | Preenchida pelo default do estágio; editável por oportunidade (decisão aprovada) |
| `valor_setup` | R$ one-time (implantação) |
| `valor_mensal`, `duracao_meses` | Componente recorrente |
| `valor_total` | Calculado: `setup + mensal × meses` (TCV) |
| `forecast_ponderado` | Calculado: `valor_total × probabilidade` — fórmula idêntica à da planilha, agora automática |
| `canal_origem` | Obrigatório (§5.2) |
| `partner_id?` + comissão | §5.3 |
| `data_fechamento_esperada` | **Obrigatória a partir de "Proposta Enviada"**; vencida ⇒ destaque visual + digest |
| `contatos[]` | 1..N, um principal |
| `motivo_perda?` | Obrigatório ao mover para Perdido (lista configurável) |
| `ultima_atividade_em` | Automático; base do alerta de estagnação (default: 14 dias sem movimento) |

**Activity (Atividade)** — `tenant_id`, `tipo` (Reunião, Follow-up, Ligação, E-mail, Tarefa), `titulo`, `descricao?`, `data_prevista`, `owner_id`, `prioridade` (Baixa/Média/Alta), `status` (Pendente, Em andamento, Concluída, Cancelada), `concluida_em?`, vínculos: `opportunity_id?` e/ou `account_id?` (FK reais — substitui o vínculo manual "Relacionada ao Pipeline (#)" da aba Ações Comerciais). **É o combustível do digest diário.**

**Goal (Meta)** — decisão aprovada: `tenant_id`, `escopo` (`bu` | `usuario`), `bu_id?` ou `user_id?`, `mes_referencia`, `valor_meta` (R$ de valor total ganho no mês), agregações trimestral/anual derivadas. Padrão validado: meta como objeto dedicado por dono e período (Salesforce `ForecastingQuota` [✓]).

**AuditLog** — `tenant_id`, `entidade`, `entidade_id`, `user_id`, `acao`, `delta` (antes/depois), `timestamp`. Toda escrita relevante audita — resposta direta ao "nada confiável" da planilha.

### 5.2 Canais de origem

Toda oportunidade nasce com canal de origem (lista configurável por tenant; seed):
`Parceiro` (exige `partner_id`) · `Indicação` · `Prospecção ativa (outbound)` · `Inbound (site/conteúdo)` · `Evento` · `Base/Cliente existente` · `Outro`.
Relatórios e digest de segunda segmentam por canal — atribuição multi-canal é requisito de primeira classe.

### 5.3 Comissão de parceiro (diferencial)

Decisão aprovada: **% por componente, com snapshot na oportunidade.**

- Vínculo `OpportunityPartnerCommission`: `opportunity_id`, `partner_id`, `papel` (tipado), `pct_setup`, `pct_recorrente`, `valor_fixo?` (alternativa a percentuais), `meses_comissionados` (default = `duracao_meses`).
- Valores calculados e exibidos na oportunidade: `comissao_setup = valor_setup × pct_setup`, `comissao_recorrente = valor_mensal × meses_comissionados × pct_recorrente`, `comissao_total`.
- O **forecast líquido** (forecast ponderado − comissão ponderada) fica disponível em relatórios e na visão do gestor.
- Ao mover para **Ganho**, o cálculo é **congelado (snapshot imutável)** com os valores vigentes — padrão de disparo em Closed Won validado pelo modelo Zoho [✓]; estrutura de rateio inspirada em `OpportunitySplit` da Salesforce [✓]. Reabrir a oportunidade exige permissão de gestor e re-audita.
- MVP: 1 parceiro por oportunidade (cobre 100% dos casos da planilha — 24 oportunidades, todas com parceiro único). O modelo N:N tipado já fica preparado no schema para multi-parceiro futuro.

## 6. Módulos funcionais

### RF-01 — Autenticação e acesso
Login por e-mail/senha e Google via **Identity Platform multi-tenant** (um tenant de identidade por tenant Azim [✓]); convite por e-mail com ativação; recuperação de senha; sessão por tenant resolvida pelo slug. *(Lição do vídeo: convite depende de e-mail — provedor transacional é dependência de fundação, não item tardio.)*

### RF-02 — Administração do tenant (white-label)
Escopo **estrito** de customização (requisito): **logo** (PNG/SVG ≤ 1 MB), **favicon**, **cor primária**, **cor secundária**, sobre o **slug** do tenant (`app.azim.com.br/{slug}`). Aplicação via design tokens (§10.3). Validação de contraste WCAG AA no upload (tons derivados automaticamente). Nada além disso é customizável (sem CSS/fonte/layout por tenant) — decisão de produto que mantém o custo de manutenção linear.

### RF-03 — BUs, usuários e papéis
CRUD de BUs; convite de usuários com papel e memberships por BU; desativação preserva histórico (FKs intactas).

### RF-04 — Contas e contatos
CRUD com busca/dedupe por nome normalizado (alerta "conta similar existe" — evita o caso Pag.ai/Ethoca duplicadas); visão 360° da conta: oportunidades (todas as BUs que o usuário enxerga), contatos, atividades, histórico.

### RF-05 — Parceiros
CRUD de parceiros com papel tipado e percentuais default; visão do parceiro: oportunidades originadas, comissão projetada (pipeline) e consolidada (ganhas), por período.

### RF-06 — Pipeline e oportunidades
- **Kanban por BU** com drag-and-drop entre estágios (higiene de mercado [coletada]); colunas mostram soma de valor total e forecast ponderado.
- Visão lista com filtros salvos (por owner, canal, parceiro, estágio, data de fechamento, estagnadas).
- Criação rápida (conta nova inline) e formulário completo.
- Regras: §5.1 (obrigatoriedades, motivo de perda, numeração, recálculo de forecast em tempo real).
- Linha do tempo da oportunidade: estágios, atividades, notas, alterações (audit).

### RF-07 — Atividades e follow-ups
CRUD; visão "Meu dia / Minha semana" (vencidas, hoje, próximas); conclusão em 1 clique a partir da lista ou do digest (link autenticado); ao concluir, sugestão de próxima atividade (manter sempre um follow-up futuro por oportunidade aberta — métrica de saúde do funil).

### RF-08 — Metas e forecast
Cadastro de metas mensais por BU e/ou responsável (decisão aprovada); painel "Direção": realizado (ganho no mês), pipeline ponderado, gap vs meta, projeção por data de fechamento; agregação trimestral/anual; sem meta cadastrada, painel mostra apenas realizado vs pipeline (graceful degradation — espelha a regra do digest de segunda).

### RF-09 — Digest diário por e-mail (requisito de primeira classe)

**Agendamento:** Cloud Scheduler dispara **de hora em hora (UTC)** um endpoint autenticado (OIDC + `roles/run.invoker` [coletada — docs GCP]); o serviço seleciona tenants cuja hora local (IANA por tenant) = `horario_digest` **e** o dia é útil (seg–sex) no fuso do tenant — neutraliza DST (recomendação do Google de operar em UTC [coletada]) e evita um job por tenant.

**Conteúdo — terça a sexta (por usuário com pendências):**
1. Saudação no tom da marca ("Seu azimute de hoje").
2. **Atividades vencidas** (dias de atraso, vínculo, 1-clique concluir/reagendar).
3. **Atividades de hoje**.
4. **Oportunidades estagnadas** do usuário (>14 dias sem atividade) e **datas de fechamento vencidas**.
5. Link direto para `…/{slug}` com o branding do tenant aplicado ao template.

**Conteúdo adicional — segunda-feira ("o azimute da semana"):**
6. **Resumo do pipeline**: nº de oportunidades e forecast ponderado por estágio e por BU; variação vs semana anterior.
7. **Status global**: ganho no mês/trimestre, top oportunidades por forecast, fechamentos esperados na semana, segmentação por canal de origem.
8. **Se houver meta cadastrada** (BU e/ou responsável): realizado vs meta do mês (% atingido), gap, e pipeline ponderado disponível para cobrir o gap. Sem meta ⇒ bloco omitido.
   - Vendedor recebe o recorte dele; gestor de BU recebe a BU consolidada; executivo/admin recebe o tenant consolidado.

**Regras de envio:** idempotência por `EmailDigestLog` (usuário+data) — retries do Scheduler não duplicam; usuário sem pendências e sem papel de gestão não recebe (sem spam); opt-out individual configurável, exceto resumo de segunda para gestores (default on); template HTML responsivo com tokens do tenant; remetente com SPF/DKIM/DMARC do domínio do Azim.

### RF-10 — Notificações in-app
Sino com eventos relevantes (oportunidade atribuída, estágio alterado por outro usuário, atividade atribuída, meta atingida). Base: eventos de domínio (§10.5).

### RF-11 — Relatórios (MVP enxuto)
Funil por estágio; forecast por BU/mês; ranking por responsável; oportunidades por canal; comissões por parceiro (projetado × consolidado); export CSV. Dashboards avançados ficam na Fase 2.

### RF-12 — Automações visuais com React Flow (Fase 2)
Editor de workflows por BU — modelo validado pelo vídeo de referência (TeamForge):
- Nós: **Gatilho** (eventos de domínio: `opportunity.created`, `opportunity.stage_changed`, `opportunity.stale`, `activity.overdue`…), **Condição** (campo/operador/valor), **Ações** (criar atividade, atualizar campo, notificar in-app, enviar e-mail, webhook), **fim**.
- Validação de consistência do grafo na edição (gatilho exige contexto que as ações consomem — lição direta do vídeo).
- Execução **sempre assíncrona** (Pub/Sub → worker; jamais no request do usuário — lição do vídeo) com **log de execução por nó** e botão "Executar teste".
- Templates prontos: "ao ganhar, criar atividade de onboarding"; "estagnada há 14 dias, notificar gestor"; "proposta enviada sem follow-up em 5 dias, criar follow-up".

### RF-13 — Agentes de IA (Fase 3)
Sobre stack comprovado em produção (LangGraph: Klarna, Uber, J.P. Morgan… [✓]; padrão "copilot de domínio" [✓]; casos de vendas 11x/Unify [✓]):
- **Lead/opportunity scoring** — priorização da carteira (batch noturno via Pub/Sub).
- **Resumo de conta** — briefing 360° antes da reunião (sob demanda).
- **Próxima melhor ação** — sugestão por oportunidade com justificativa.
- **Copilot de vendas** — conversacional sobre os dados do tenant (REST síncrono).
- Observabilidade total com **Langfuse** (prompt, resposta, tokens, latência, passos [✓]); honestidade assistente×agente e guardrails (tendência Gartner [coletada]); **rate-limit de IA por tenant** em Redis (lição do vídeo); dados jamais cruzam tenants.

## 7. RBAC — matriz de permissões (MVP)

| Capacidade | Platform Op (Vellus) | Tenant Admin | Gestor de BU | Vendedor | Viewer |
|---|---|---|---|---|---|
| Provisionar/suspender tenants | ✅ | — | — | — | — |
| Branding, usuários, BUs, estágios | — | ✅ | — | — | — |
| Parceiros e % default | — | ✅ | ✅ (sua BU) | — | — |
| Metas | — | ✅ | ✅ (sua BU) | ver as suas | — |
| Oportunidades — ver | — | todas | da(s) sua(s) BU(s) | das suas BUs | escopo designado |
| Oportunidades — criar/editar | — | ✅ | ✅ (sua BU) | as suas (owner) | — |
| Reabrir ganha/perdida; editar comissão pós-ganho | — | ✅ | ✅ (sua BU) | — | — |
| Atividades | — | ✅ | ✅ (sua BU) | as suas | ver |
| Relatórios | — | tenant | BU | os seus | escopo |
| Auditoria | — | ✅ | sua BU | — | — |
| Dados comerciais dos tenants | 🚫 (apenas suporte autorizado e audidado) | | | | |

*Lição do vídeo: papéis "aparentemente funcionando" são o risco nº 1 — esta matriz vira suíte de testes automatizados por papel desde o primeiro sprint (§12).*

## 8. Requisitos não-funcionais

| Tema | Requisito |
|---|---|
| Idioma | PT-BR nativo (textos, datas, R$); arquitetura i18n-ready |
| Performance | API p95 < 300 ms; kanban fluido com 500+ oportunidades por BU |
| Disponibilidade | SLO 99,9%; janela de manutenção comunicada |
| Isolamento | Vazamento entre tenants = incidente sev-1; testes de isolamento em CI (§12) |
| LGPD | §13 |
| Acessibilidade | WCAG AA (inclui contraste das cores custom do tenant) |
| Auditoria | Toda escrita em entidades de negócio registrada com autor e delta |
| Backup/DR | PITR no Cloud SQL; RPO ≤ 5 min; RTO ≤ 4 h; restore testado |

---

# Parte III — Arquitetura técnica

## 9. Princípios

1. **GCP-first, 100% Terraform** — se não está no código, não existe (requisito; antítese do setup manual do vídeo de referência).
2. **Pooled multi-tenancy com defesa em profundidade** — filtro na aplicação + RLS no Postgres.
3. **Postgres é a fonte de verdade; Redis é a memória rápida; eventos movem o resto** (síntese do vídeo + research).
4. **Assíncrono por padrão** para automação, digest e IA — nada disso roda no request do usuário.
5. **Reversibilidade** — provedores externos (e-mail, LLM) atrás de abstrações.

## 10. Topologia GCP (região primária: `southamerica-east1`)

```
                        Cloud DNS + Global HTTPS LB + Cloud Armor + Managed Certs
                                              │
            ┌─────────────────────────────────┼──────────────────────────────┐
            │                                 │                              │
   GCS + Cloud CDN                    Cloud Run: azim-api              Cloud Run: azim-workers
   (SPA React + assets                (.NET 10, REST)                  (.NET; consumidores Pub/Sub:
    de branding por tenant)                   │                         digest, automações F2)
                                              │                              │
                            ┌─────────────────┼─────────────┬────────────────┤
                            │                 │             │                │
                     Cloud SQL Postgres   Memorystore     Pub/Sub      Cloud Run: azim-ai (F3)
                     (pooled + RLS,       (Redis: cache   (eventos      (Python/FastAPI,
                      PITR, réplica)       sessão/perm,    de domínio)   LangGraph + Langfuse)
                                           rate-limit)
                                              │
   Cloud Scheduler ──OIDC──▶ azim-api /jobs/digest-tick (hora em hora, UTC)
   Identity Platform (multi-tenant: 1 tenant de identidade por tenant Azim)
   Secret Manager · Artifact Registry · Cloud Logging/Monitoring/Trace · Cloud Tasks (retries pontuais)
```

### 10.1 Multi-tenancy (dados)

- Toda tabela de negócio carrega `tenant_id` (e `bu_id` onde aplicável).
- **RLS por variável de runtime** — recomendação verificada [✓ AWS]: middleware .NET resolve o tenant (slug → id, cache Redis) e o interceptor de conexão executa `SET app.current_tenant = @tenant_id`; políticas RLS comparam `tenant_id = current_setting('app.current_tenant')::uuid`. Um usuário de banco para a aplicação (não um por tenant).
- Filtro primário no EF Core (global query filter por `tenant_id`); RLS é a segunda linha — a verificação adversarial refutou "RLS obrigatória", e o Azim a adota **deliberadamente** como defesa em profundidade.

### 10.2 Resolução de tenant e white-label

- URL canônica: `app.azim.com.br/{slug}/…` (requisito: slug como prefixo). O slug resolve tenant, tema e tenant de identidade.
- Branding servido por endpoint público cacheado (`/t/{slug}/brand.json` + assets em GCS/CDN): logo, favicon, `--azim-primary`, `--azim-secondary` e tons derivados.
- SPA aplica design tokens como CSS variables em runtime; favicon e título por tenant; e-mails usam os mesmos tokens.

### 10.3 Backend .NET 10

ASP.NET Core (REST + OpenAPI), EF Core/Npgsql, FluentValidation, background services para consumo Pub/Sub (workers separados do processo da API), publicação de **eventos de domínio** (outbox pattern → Pub/Sub): `opportunity.created|stage_changed|won|lost|stale`, `activity.created|completed|overdue`, `goal.updated`, `tenant.branding_changed`.

### 10.4 Funções Python / IA (Fase 3)

Serviço `azim-ai` (FastAPI em Cloud Run) — padrão validado de integração poliglota: **REST interno síncrono** para copilot/resumo; **Pub/Sub assíncrono** para scoring batch [coletada — Panenco]. LangGraph para orquestração de agentes; **Langfuse** para tracing/evals [✓]; chaves no Secret Manager; rate-limit por tenant via Redis.

### 10.5 E-mail transacional

- Abstração `IEmailSender`; provedor inicial: **Postmark** (melhor entregabilidade medida em teste independente de 2026 — 93,8% [coletada]) com **SendGrid** como alternativa avaliada no spike do MVP (critério: entregabilidade > templates > custo; volume do digest é baixo, falhar em inbox é falhar o produto).
- Domínio de envio dedicado (`mail.azim.com.br`) com SPF/DKIM/DMARC; supressão e bounce handling registrados; webhooks de evento (entregue/aberto) alimentam métricas do digest.

### 10.6 Terraform (100% IaC)

```
infra/
├── modules/        # network, cloudsql, redis, run-service, pubsub, scheduler,
│                   # identity-platform, lb-cdn, gcs-branding, secrets, monitoring
├── envs/
│   ├── dev/        # state isolado (GCS backend + locking), projeto GCP próprio
│   ├── stg/
│   └── prd/
└── global/         # DNS, Artifact Registry, billing alerts
```

- Backend de estado em GCS com versionamento; um projeto GCP por ambiente; CI executa `plan` em PR e `apply` com aprovação manual em `prd`.
- **Inclui** recursos frequentemente esquecidos: jobs do Cloud Scheduler (a doc oficial traz exemplos Terraform com `attempt_deadline`/`retry_count` [coletada]), tenants do Identity Platform, tópicos/subscriptions Pub/Sub, políticas de alerta, dashboards, segredos (valores fora do state via Secret Manager), e o **provisionamento de novo tenant** como rotina da aplicação (não Terraform — tenant é dado, não infraestrutura).
- Deploy: GitHub Actions + Workload Identity Federation (sem chave de SA em repositório); migrations EF Core executadas como job de release antes do tráfego (lição do vídeo: migrations automatizadas no deploy, nunca manuais).

### 10.7 Observabilidade e segurança

Cloud Logging/Monitoring/Trace com `tenant_id` como label estruturado (sem PII nos logs); alertas: erro 5xx, latência, falha de digest, fila Pub/Sub crescendo, falha de RLS (qualquer query sem contexto de tenant loga erro crítico). Cloud Armor (WAF + rate limit de borda); CSP estrita; segredos exclusivamente no Secret Manager; dependabot/renovate + `npm audit`/`dotnet list package --vulnerable` em CI.

---

# Parte IV — Roadmap

> Fases são incrementos de produto utilizáveis; cada uma termina com critérios verificáveis.

### Fase 0 — Fundação (infra + esqueleto)
Terraform completo dos 3 ambientes; Identity Platform multi-tenant; esqueleto .NET + React com resolução de slug e theming; RLS ativa; CI/CD com testes de isolamento; e-mail transacional configurado (SPF/DKIM/DMARC validados).
**Pronto quando:** dois tenants de teste com brandings distintos logam isolados em `prd`, e o teste de CI prova que tenant A não lê dados do tenant B (via API e via SQL sem contexto).

### Fase 1 — MVP comercial (substitui a planilha)
RF-01..07 + RF-09 (digest completo, incl. segunda-feira) + RF-11 (relatórios enxutos) + metas básicas (RF-08 sem projeções) + auditoria + **migração da planilha (Parte V)**.
**Pronto quando:** a Vellus opera as 3 BUs no Azim por 2 semanas sem voltar à planilha; digest diário chegando 07:00 BRT seg–sex; 100% das oportunidades migradas com owner definido.

### Fase 2 — Direção (automação e gestão)
RF-12 (automações React Flow com templates), RF-08 completo (projeções, painel Direção), RF-10 (notificações), dashboards, webhooks de saída, calendário de feriados no digest.
**Pronto quando:** os 3 templates de automação rodam em produção com log por nó e zero execução síncrona.

### Fase 3 — Inteligência (IA)
RF-13 sobre `azim-ai` (LangGraph + Langfuse): scoring, resumo de conta, próxima melhor ação, copilot; rate-limit de IA por plano/tenant.
**Pronto quando:** copilot responde sobre dados do tenant com tracing completo no Langfuse e custo por tenant visível.

### Horizonte (não comprometido)
Portal do parceiro (deal registration — padrão PRM dentro do CRM core [✓]); múltiplos parceiros por oportunidade; lead como entidade separada (padrão HubSpot [✓]); integração WhatsApp (tendência folk [coletada]); multimoeda; app mobile.

---

# Parte V — Migração da planilha

**Fonte:** `Pipeline Vellus.xlsx` (2 abas — [mapeamento completo](../research/analise-planilha-pipeline-vellus.md)).

| Origem (planilha) | Destino (Azim) | Transformação |
|---|---|---|
| `BU` | `BusinessUnit` | 3 BUs (trim de espaços: "Sertão " → "Sertão") |
| `Empresa / Cliente` | `Account` | Dedupe por nome normalizado (Pag.ai, Ethoca, Autopass… viram 1 conta com N oportunidades) |
| `#` | `Opportunity.numero` | Preservado quando existir (`AZ-0043`…); sem número ⇒ próximo da sequência (≥ 95) |
| `Oportunidade / Descrição` | `titulo` | Vazio ⇒ "Oportunidade — {conta}" + flag de triagem |
| `Parceiro` | `Partner` + vínculo | 11 parceiros distintos cadastrados; "-" ⇒ sem parceiro; **percentuais de comissão preenchidos pela Vellus pós-import** (inexistentes na origem) |
| `Responsável` | `owner_id` | Mapa nome→usuário (3 usuários); **64 linhas sem responsável entram em fila de atribuição obrigatória** — o import não conclui sem owner |
| `Etapa` | `stage_id` | Mapa 1:1 com seed; 13 linhas vazias ⇒ "Lead" + flag de triagem |
| `Nome`/`E-mail`/`Celular` | `Contact` ligado à conta | Criado quando houver; vínculo como contato principal |
| `Valor Setup`/`Mensal`/`Meses` | campos de valor | Numéricos; vazio ⇒ 0 |
| `Probabilidade (%)` | `probabilidade` | Preservada (sobrepõe o default do estágio) |
| `Forecast (R$)` | — | **Recalculado** pelo sistema; divergência > R$ 0,01 vs planilha listada no relatório de import |
| `Data Esperada Fechamento` | `data_fechamento_esperada` | Serial Excel → ISO (epoch 1899-12-30); datas no passado mantidas e flagadas "vencida" |
| `Status / Próximos Passos` | nota na oportunidade **+ Activity** | Texto preservado como nota; quando acionável, vira atividade pendente na triagem |
| `Última Atualização` | nota histórica | `ultima_atividade_em` passa a ser automático |
| Aba "Ações Comerciais" | `Activity` | Tipo/“Data Planejada”/Status/Prioridade mapeados; vínculo `Relacionada ao Pipeline (#)` → FK pela numeração preservada; typo "Miilton" → usuário Milton |

**Processo:** upload do .xlsx → **dry-run** com relatório (contagens, dedupes, flags, divergências de forecast) → triagem assistida (owners e estágios faltantes) → import transacional → relatório final auditado → **planilha congelada** (read-only no OneDrive) com banner apontando para o Azim.

---

# Anexo A — Rastreabilidade dos requisitos

| # | Requisito do usuário | Onde está |
|---|---|---|
| 1 | CRM multi-tenant "Azim", solução Vellus | Parte I; §10.1 |
| 2 | White-label: logo, favicon, slug, 2 cores — apenas | RF-02; §10.2 |
| 3 | GCP + 100% Terraform | §9; §10; §10.6 |
| 4 | React+TS+React Flow; .NET 10; Python p/ IA (LangChain/LangGraph/Langfuse) | §10.3; §10.4; RF-12; RF-13 |
| 5 | Tenant com múltiplas BUs | §5 (BusinessUnit); RF-03; evidência na Parte I §2 |
| 6 | Oportunidade multi-canal, incl. parceiro comissionado com comissão na oportunidade | §5.2; §5.3; RF-05 |
| 7 | E-mail diário seg–sex (to-do) + segunda com pipeline vs metas | RF-09; RF-08; §10.5 |
| 8 | Narrativa de marca (azimute/direção) | Parte I (integral); tom aplicado em RF-09 |

# Anexo B — Decisões registradas

| Decisão | Escolha | Racional |
|---|---|---|
| Estágios de pipeline | Configuráveis por BU, seed da planilha Vellus | Aprovada pelo usuário; padrão de pipelines genéricos validado (HubSpot [✓]) |
| Comissão de parceiro | % por componente (setup/recorrente) + valor fixo opcional; snapshot no ganho | Aprovada pelo usuário; estrutura inspirada em OpportunitySplit [✓]; disparo em Closed Won (Zoho [✓]) |
| Metas | BU + responsável, mensal (agregações trim/anual) | Aprovada pelo usuário; padrão ForecastingQuota [✓] |
| Lead | Estágio do funil (não entidade separada) no v1 | Espelha a operação atual da Vellus; padrão HubSpot registrado para evolução |
| E-mail | Postmark candidato primário atrás de `IEmailSender` | Entregabilidade medida [coletada]; decisão reversível |
| Digest × fuso | Job horário UTC + hora local IANA por tenant | Neutraliza DST (recomendação GCP [coletada]) |
| RLS | Adotada como defesa em profundidade (não como única barreira) | Verificação adversarial da research (claim "obrigatória" refutada; método runtime var confirmado [✓]) |
