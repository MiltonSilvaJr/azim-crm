# Deep Research — Fundamentos para o Azim CRM (multi-tenant, GCP, IA)

> **Data:** 10/06/2026
> **Metodologia:** pipeline de pesquisa multiagente (105 agentes): decomposição em 5 frentes → buscas paralelas → 23 fontes lidas → 115 alegações falsificáveis extraídas → as 25 mais relevantes submetidas a verificação adversarial (3 votos independentes cada) → **22 confirmadas, 3 refutadas**. A síntese final deste documento foi feita pelo agente principal a partir das alegações verificadas (o passo automático de síntese atingiu limite de sessão).
> **Como ler:** alegações marcadas **[✓ n-m]** passaram pela verificação adversarial (n votos a favor, m contra). Alegações marcadas **[coletada]** vêm de fontes lidas pela pipeline, com citação textual, mas não passaram pelo funil de verificação — tratar como insumo de menor confiança.

---

## Frente 1 — Arquitetura multi-tenant na GCP

### Isolamento de dados: pool + RLS com variável de runtime

- **[✓ 3-0]** Entre os dois métodos de filtragem com Row-Level Security (comparar a coluna ao usuário PostgreSQL logado vs. compará-la a uma **variável de contexto definida pela aplicação em runtime**), a orientação prescritiva da AWS para SaaS multi-tenant em PostgreSQL recomenda o método de variável de runtime — criar um usuário de banco por tenant não escala. A aplicação define o contexto do tenant a cada consulta (ex.: `SET app.current_tenant = '...'`).
  — [AWS Prescriptive Guidance: Multi-tenant SaaS on managed PostgreSQL](https://docs.aws.amazon.com/prescriptive-guidance/latest/saas-multitenant-managed-postgresql/rls.html)
- **[✗ refutada 0-3]** "Em modelo pooled, RLS é *obrigatória* para isolamento." — Os verificadores refutaram a obrigatoriedade: RLS é uma **camada adicional de defesa** (defense in depth) sobre a filtragem na aplicação, não um requisito absoluto do modelo pooled. **Decisão para o Azim:** adotar RLS mesmo assim, como segunda linha de defesa — com clareza de que o filtro primário é o da aplicação (interceptor no EF Core) e a RLS protege contra bugs de query.

### Identidade e tenants

- **[✓ 3-0]** O **GCP Identity Platform** suporta multi-tenancy nativa: "silos isolados de usuários e configurações dentro de um único projeto Identity Platform" — um único projeto GCP atende todos os tenants do Azim, sem projeto por cliente.
  — [Google Cloud: Identity Platform multi-tenancy](https://docs.cloud.google.com/identity-platform/docs/multi-tenancy)
- **[✓ 3-0]** A documentação do Google indica multi-tenancy do Identity Platform como padrão para **aplicações B2B**, e os silos podem representar "clientes, unidades de negócio ou subsidiárias" — aderente à hierarquia tenant → BUs do Azim.
  — [mesma fonte]

### Implicações de arquitetura (frente 1)

1. **Modelo pooled** (banco e schema compartilhados, `tenant_id` em toda tabela) + **RLS por variável de runtime** + filtro na aplicação. Validado também na prática pelo vídeo analisado (`analise-video-saas-multitenant.md`).
2. **Identity Platform com um tenant de identidade por tenant Azim** (login isolado por slug), num único projeto GCP.
3. Resolução de tenant por **slug no path** + carregamento de branding (logo, favicon, cores) no bootstrap — white-label via design tokens (CSS variables). *(Padrão de implementação; não dependeu de verificação externa.)*

---

## Frente 2 — Modelo de domínio CRM B2B e comissões de parceiro (PRM)

### Como os líderes modelam parceiro ↔ oportunidade

- **[✓ 3-0]** Na Salesforce, o envolvimento de parceiro num negócio é um **objeto de junção de primeira classe** (`OpportunityPartner`) ligando uma Account parceira a uma Opportunity — não um campo de texto na oportunidade.
  — [Salesforce Object Reference: OpportunityPartner](https://developer.salesforce.com/docs/atlas.en-us.object_reference.meta/object_reference/sforce_api_objects_opportunitypartner.htm)
- **[✓ 3-0]** Esse vínculo é **tipado por um papel** (picklist `Role`: ex. Reseller, Manufacturer).
- **[✓ 3-0]** **Gap de mercado confirmado:** `OpportunityPartner` expõe apenas 5 campos (AccountToId, IsPrimary, OpportunityId, ReversePartnerId, Role) — **nenhum representa comissão**. Comissionamento de parceiro vinculado à oportunidade não faz parte do modelo padrão da Salesforce; exige customização ou produtos adicionais.
- **[✓ 3-0]** Para rateio de valor, a Salesforce usa `OpportunitySplit` — objeto **filho da oportunidade** que credita a membros do time uma porção do valor. É a referência estrutural para o snapshot de comissão do Azim (oportunidade + beneficiário + percentual + valor).
  — [Salesforce Object Reference: OpportunitySplit](https://developer.salesforce.com/docs/atlas.en-us.object_reference.meta/object_reference/sforce_api_objects_opportunitysplit.htm)
- **[✓ 3-0]** Os splits suportam dois regimes de validação: somar exatamente 100% (rateio de receita) ou livre de 0–1.000% (crédito overlay) — útil para o Azim distinguir "comissão sobre o valor" de "crédito de influência".
- **[✓ 3-0 / 2-1]** No **Zoho CRM**, comissão **não é módulo nativo**: é extensão de marketplace, **restrita ao módulo Deals** (ancorada na oportunidade), disparada quando o negócio é **ganho (Closed Won)**, com planos de comissão por usuário — e calcula comissão de **vendedores internos, não de parceiros externos**.
  — [Zoho: Commission Management extension](https://help.zoho.com/portal/en/kb/crm/extensions/sales/articles/commission-management-for-zoho-crm)
- **[✓ 3-0]** No posicionamento de PRM da Salesforce, parceria vive **dentro do CRM core** (deal registration e roteamento no Sales Cloud, rastreio no Pipeline Inspection) — PRM não é sistema apartado.
  — [Salesforce: Partner Relationship Management](https://www.salesforce.com/sales/partner-relationship-management/)
- **[✗ refutada 1-2]** A alegação de que a Salesforce posiciona incentivos de canal exclusivamente num produto separado ("Channel Revenue Management") não se sustentou na fonte — não usar.

### Primitivas de modelo e metas

- **[✓ 3-0]** O modelo da **HubSpot** assenta em 4 primitivas: **objetos, registros, propriedades e associações** — referência para o modelo de domínio do Azim.
  — [HubSpot Developers: Understanding the CRM](https://developers.hubspot.com/docs/guides/crm/understanding-the-crm)
- **[✓ 3-0]** Na HubSpot, **Deals** são rastreados por **estágios de pipeline**, e pipeline é mecanismo genérico de estágios — fundamenta estágios configuráveis por BU no Azim.
- **[✓ 3-0]** A HubSpot modela **Lead como objeto distinto** de Contact e Deal. *(O Azim v1 decide diferente — lead como estágio inicial do funil, como a Vellus já opera na planilha — mas registra o padrão para evolução futura.)*
- **[✓ 3-0]** A Salesforce modela **meta como objeto dedicado** (`ForecastingQuota`): quota de um usuário **ou território** para um **período**, acoplada ao módulo de forecast — valida o desenho de metas do Azim (registro por BU/usuário por mês).
  — [Salesforce Object Reference: ForecastingQuota](https://developer.salesforce.com/docs/atlas.en-us.object_reference.meta/object_reference/sforce_api_objects_forecastingquota.htm)

### Implicações (frente 2) — onde o Azim se diferencia

1. **Parceiro↔oportunidade como relação de primeira classe com papel tipado** (padrão Salesforce) **+ comissão nativa no vínculo** (que nem Salesforce nem Zoho oferecem out-of-the-box) = **diferencial competitivo direto do Azim**, e exatamente o requisito da Vellus.
2. Snapshot de comissão estruturado como "split" (beneficiário, base, %, valor calculado) ancorado na oportunidade; consolidação no momento do ganho (padrão Zoho: dispara em Closed Won).
3. Metas como entidade própria por escopo (BU | usuário) e período, integrada ao forecast.

---

## Frente 3 — Agentes de IA em CRM (LangChain/LangGraph/Langfuse)

- **[✓ 3-0]** A documentação da LangChain mantém lista pública de empresas com **LangGraph em produção**: Klarna, Uber, LinkedIn, J.P. Morgan, GitLab, Cisco, Replit, Elastic, entre outras — evidência de maturidade do framework escolhido para o Azim.
  — [LangChain Docs: LangGraph case studies](https://docs.langchain.com/oss/python/langgraph/case-studies)
- **[✓ 3-0]** O padrão mais recorrente entre adotantes é **"copilot para tarefa de domínio específico"** (Klarna, AppFolio, J.P. Morgan, Elastic) — valida o desenho "copilot de vendas" do Azim.
- **[✓ 3-0]** Há casos **diretamente ligados a vendas/CRM**: 11x (SDR/vendas GenAI-native, "research & outreach") e Unify (go-to-market) usam LangGraph em produção.
- **[✓ 3-0]** O **Langfuse** captura por requisição: prompt exato, resposta, tokens, latência e passos intermediários de ferramentas/retrieval — a telemetria necessária para depurar lead scoring e copilot no CRM.
  — [Langfuse: Observability overview](https://langfuse.com/docs/observability/overview)
- **[✓ 3-0]** Langfuse tem integrações nativas com OpenAI, **LangChain** e LlamaIndex — instrumenta o stack Python do Azim sem cola adicional.
- **[✓ 1-1, 1 abstenção — usar com cautela]** Survey da LangChain (State of Agent Engineering): casos de uso mais comuns de agentes em produção são atendimento ao cliente (26,5%), pesquisa/análise de dados (24,4%) e automação de workflows internos (18%) — a fonte sustenta a viabilidade geral de agentes, **não** especificamente os casos de vendas.
  — [LangChain: State of Agent Engineering](https://www.langchain.com/state-of-agent-engineering)
- **[✗ refutada 0-3]** O número "57,3% das organizações já têm agentes em produção" não foi confirmado na fonte — **não usar** estatísticas de adoção dessa pesquisa sem retestar.
- **[coletada]** Padrão de integração poliglota: camada de IA em LangChain (Python) desacoplada de backend em outra linguagem (ex.: **C#/.NET**) encapsulando-a como serviço/função serverless, invocada **sincronamente via REST** ou **assincronamente via fila/pub-sub** — exatamente o desenho .NET 10 + workers Python do Azim. — [Panenco: Building AI microservices]
- **[coletada]** Gartner (press release, 2025): **40% das aplicações empresariais terão agentes de IA task-specific integrados até o fim de 2026** (de <5% em 2025); até 2027, 1/3 das implementações agênticas combinará múltiplos agentes com habilidades distintas; distinção assistente×agente ("agentwashing"). — [Gartner Newsroom]

### Implicações (frente 3)

1. LangGraph + Langfuse é stack comprovado em produção, inclusive em vendas — risco tecnológico baixo para a Fase de IA do Azim.
2. Integração .NET↔Python: REST interno para casos interativos (copilot), Pub/Sub para batch (scoring noturno, resumos).
3. Posicionar IA com honestidade (assistente vs agente) e guardrails — tendência de mercado cobra isso.

---

## Frente 4 — E-mail transacional e digests agendados *(alegações coletadas; não passaram pelo funil adversarial)*

### Agendamento (fontes primárias GCP)

- Cloud Scheduler aciona serviços **Cloud Run por HTTP com autenticação OIDC** (service account dedicada com `roles/run.invoker`, audience = URL do serviço); "Require authentication, don't allow public access". — [GCP: Triggering Cloud Run with Scheduler](https://docs.cloud.google.com/run/docs/triggering/using-scheduler)
- A documentação traz **exemplos Terraform completos** (serviço, SA, IAM binding, job com `attempt_deadline`/`retry_count`) — compatível com o requisito 100% IaC do Azim.
- Cron unix de 5 campos suporta **dias úteis nativamente**: `0 8 * * 1-5` (ou `MON-FRI`) — o digest seg–sex do Azim não precisa de lógica de calendário para o básico. — [GCP: Cron job schedules](https://docs.cloud.google.com/scheduler/docs/configuring/cron-job-schedules)
- **Fuso horário:** default é UTC; o campo aceita identificadores IANA (tz database); o Google **alerta que DST pode causar execuções inesperadas e recomenda UTC**. → Decisão Azim: job único de hora em hora em UTC + comparação com hora local (IANA) configurada por tenant — neutraliza DST e evita um job por tenant.

### Provedores (fontes secundárias, maio/2026)

| Provedor | Preço observado | Observações das fontes |
|---|---|---|
| Amazon SES | US$ 0,10/1.000 | Mais barato em escala; setup "by far most complicated"; exige construir templates/supressão/analytics próprios; é AWS (foge do GCP-first) |
| SendGrid | US$ 19,95/mês (50k) | Editor de templates hospedado com versionamento; **82% de entregabilidade** no teste independente (GlockApps, 4 rodadas) |
| Mailgun | US$ 15/mês (10k), US$ 35 (50k) | Developer-oriented; recomendado para produtos eng-led 50k–500k/mês; não testado em entregabilidade |
| Postmark | — | **Melhor entregabilidade medida: 93,8%** (SMTP2GO 95,5% foi o topo geral); especializado em transacional |
| Resend | — | Não avaliado no teste de entregabilidade citado; DX moderna (React Email) |

**Leitura para o Azim:** o produto manda **um e-mail por usuário por dia útil** — volume baixo, mas **entregabilidade é o requisito nº 1** (digest na caixa de spam = produto morto). Critério: entregabilidade > DX de templates > custo. Recomendação da spec: abstração `IEmailSender` + **Postmark como candidato primário** e SendGrid como alternativa, com spike de validação (SPF/DKIM/DMARC próprios) no MVP.

---

## Frente 5 — Mercado de CRM 2025/2026 *(alegações coletadas; não passaram pelo funil adversarial)*

- O critério de compra de CRM em 2026 migra de checklist de features para **"fit de ecossistema"**: workflows de engajamento + camada unificada de dados do cliente + assistentes/agentes de IA **com guardrails claros** + integração que mantém dados sincronizados. — [CX Today: CRM Trends 2026]
- **Pipeline Kanban com drag-and-drop é feature básica esperada** — é o diferencial central histórico do Pipedrive ("by salespeople for salespeople", a partir de US$ 14/usuário/mês). — [comparativo folk vs HubSpot vs Pipedrive vs Attio, abr/2026]
- **Attio** se diferencia por modelo de dados flexível (objetos/campos custom, API-first); **folk** por captura multicanal (WhatsApp nativo, enriquecimento LinkedIn) a US$ 11/usuário; HubSpot entra a US$ 20 com free tier; Attio a US$ 29 sem free tier. Notável: o comparativo de 2026 **não cita IA como diferencial em nenhum dos quatro** — espaço aberto para CRM IA-nativo bem executado.
- Gartner: 40% dos apps enterprise com agentes task-specific até fim de 2026 (<5% em 2025); cenário otimista de IA agêntica ≈ 30% da receita de software enterprise até 2035 (>US$ 450 bi). — [Gartner Newsroom, 2025]

### Implicações (frente 5)

1. O Azim compete num mercado onde kanban fluido e modelo de dados bem desenhado são **higiene**, não diferencial. Os diferenciais defensáveis do Azim: **comissão de parceiro nativa na oportunidade** (gap dos líderes — frente 2), **digest diário acionável** (operacionaliza o CRM por e-mail) e, na fase 3, **IA com guardrails** num mercado que ainda não a entrega bem no mid-market.
2. Benchmark de preço de entrada no mid-market: US$ 11–29/usuário/mês — referência futura para o posicionamento comercial do Azim (fora de escopo desta spec).

---

## Alegações refutadas (registro)

| Alegação | Voto | Lição |
|---|---|---|
| "RLS é obrigatória em modelo pooled" | 0-3 | RLS é defense-in-depth recomendada, não obrigação técnica; o filtro primário é da aplicação |
| "57,3% das organizações têm agentes em produção" (survey LangChain) | 0-3 | Números de adoção dessa survey não conferem com a fonte; não citar |
| "Salesforce posiciona incentivo de canal só em produto separado (Channel Revenue Management)" | 1-2 | Parcial demais; manter apenas o que a página de PRM confirma |

## Fontes (23)

**Primárias:** AWS Prescriptive Guidance (RLS multi-tenant) · GCP Identity Platform (multi-tenancy) · GCP Cloud Scheduler (cron schedules) · GCP Cloud Run (trigger via Scheduler) · Salesforce Object Reference (OpportunityPartner, OpportunitySplit, ForecastingQuota) · Salesforce PRM (página de produto) · HubSpot Developers (Understanding the CRM) · Zoho (Commission Management extension) · LangChain Docs (LangGraph case studies) · Langfuse Docs (Observability) · LangChain (State of Agent Engineering) · Gartner Newsroom (agentes task-specific 2026).
**Secundárias/blogs:** OneUptime (multi-tenancy GCP) · Nile (multi-tenant RLS) · Analytics Vidhya (LangGraph para vendas) · Panenco (AI microservices) · VantagePoint (HubSpot vs Salesforce IA 2026) · SuprSend (SendGrid vs Mailgun vs SES) · EmailToolTester (teste de entregabilidade 2026) · Automaiva (folk vs HubSpot vs Pipedrive vs Attio) · CX Today (CRM Trends 2026).

> **Limitações:** (a) a verificação adversarial cobriu as 25 alegações mais relevantes de 115 — as frentes 4 e 5 ficaram majoritariamente fora do funil e estão marcadas como [coletada]; (b) preços e percentuais de entregabilidade são fotografias de 2026 de fontes secundárias — revalidar antes de decisões contratuais; (c) dois sub-agentes falharam por limite de sessão (1 voto de verificação e a síntese automática), sem perda de dados confirmados.
