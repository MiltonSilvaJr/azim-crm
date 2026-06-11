# DIG — Digest

**Requisitos Funcionais e Não-Funcionais**

- Versão: 0.1.0
- Data: 2026-06-11
- Status: Rascunho para revisão
- Referência pai: docs/product/frd-nfrd/frd.md § RF-09 (FRD-digest-01..04)

## Histórico de Versões

| Versão | Data | Status | Descrição da alteração |
|--------|------|--------|------------------------|
| 0.1.0 | 2026-06-11 | Rascunho para revisão | Criação inicial do documento de requisitos do módulo digest |

## 1. Visão Geral

O módulo **digest** (Bounded Context BC-06, subdomínio de suporte — *Supporting Subdomain*, deployable independente `azim-digest-worker`) é responsável pela seleção de destinatários, composição e envio do **digest diário por e-mail** do Azim CRM. É o diferencial competitivo central do produto — "o CRM que vai ao vendedor": em vez de exigir que o usuário abra o sistema, o digest leva as pendências comerciais e as ações de um clique diretamente ao e-mail do usuário no início do dia.

O digest é um worker assíncrono separado da API de negócio (`azim-api`), para garantir que falha ou pico de carga no processamento do digest não afete a operação transacional. O worker é acionado de hora em hora em UTC pelo Cloud Scheduler e, a cada disparo, seleciona os *tenants* cujo horário local (no fuso *IANA* configurado) corresponde ao `horario_digest` (padrão 07:00) e cujo dia local é dia útil (segunda a sexta).

O conteúdo é personalizado por usuário:

- **Terça a sexta:** pendências do usuário — atividades vencidas (*overdue*), atividades de hoje, oportunidades estagnadas (*stale*, há mais de 14 dias corridos sem atividade) e datas de fechamento esperadas já vencidas. Cada atividade traz um link autenticado (*link_autenticado*) de um clique para concluir ou reagendar.
- **Segunda-feira:** adicionalmente, o **azimute da semana** para Gestor de BU e Executivo do Tenant — pipeline ponderado por estágio e *Business Unit* (BU), variação vs. semana anterior, status global, ganhos, fechamentos esperados na semana e, quando houver meta cadastrada, o bloco de realizado vs. meta (com degradação graciosa quando não houver meta).

O envio é feito via módulo **notification-delivery** (interface `IEmailSender`); a idempotência por usuário e data é garantida pelo `EmailDigestLog`, de modo que retentativas do Cloud Scheduler não geram e-mails duplicados.

Este documento define **o quê** o módulo deve fazer. Decisões de **como** (esquema de tabelas, índices, *frameworks*, topologia de assinaturas Pub/Sub, estrutura do token, *template* de e-mail, algoritmo de seleção) pertencem ao `design.md`, ao `tasks.md` e aos *Architecture Decision Records* (ADRs) referenciados. Em particular, o padrão de acesso do worker aos dados de itens estagnados/vencidos (assinatura Pub/Sub vs. consulta direta ao banco) está **em aberto** (VAL-TRD-13) e deve ser resolvido no `design.md` — ver seção 9 e seção 10.

## 2. Escopo

### 2.1 Incluído

- Recebimento do *trigger* horário do Cloud Scheduler em endpoint interno protegido, com execução assíncrona.
- Seleção de *tenants* elegíveis a cada disparo, por fuso *IANA* do *tenant*: hora local = `horario_digest` e dia local útil (segunda a sexta), neutralizando horário de verão (*Daylight Saving Time*, DST).
- Seleção de destinatários por papel e por presença de pendências (*recipient selection*).
- Composição do conteúdo personalizado por usuário (terça a sexta): atividades vencidas, atividades de hoje, oportunidades estagnadas e datas de fechamento vencidas.
- Composição do bloco "azimute da semana" na segunda-feira para Gestor de BU e Executivo do Tenant.
- Omissão graciosa do bloco de metas quando não houver meta cadastrada para o período (*graceful degradation*).
- Consumo de dados de oportunidades, atividades e metas via *read model* / API dos módulos donos — **sem recalcular** regras de outros contextos.
- Geração de tokens de ação de um clique (`digest_action_tokens`) para conclusão/reagendamento de atividades pendentes.
- Envio do e-mail via notification-delivery (`IEmailSender`), com *branding* do *tenant*.
- Idempotência por `EmailDigestLog` (`tenant_id`, `user_id`, `digest_date`): retentativas do Scheduler não duplicam envio.
- Registro do resultado de cada envio em `email_digest_logs` e atualização de status de entrega (entregue, aberto, falho) a partir de eventos do *provider*.
- *Opt-out* individual do digest, exceto o resumo de segunda para papéis de gestão.
- Publicação do evento de domínio `DigestEmailSent` para a trilha de auditoria após envio bem-sucedido.
- Isolamento de dados por *tenant* e ausência de informação pessoal (*Personally Identifiable Information*, PII) em logs.

### 2.2 Excluído

- Gestão de oportunidades, atividades, contas ou metas (pertence aos respectivos módulos donos — BC-01, BC-02, BC-04, BC-05).
- Cálculo das regras de negócio de outros contextos (estagnação RN-028, forecast ponderado RN-006, comissão): o digest **consome** os resultados já calculados, nunca os recalcula.
- Validação e processamento do token de um clique no consumo (pertence ao activity-management — BC-04). O digest apenas **emite** o token.
- Entrega física do e-mail e integração com o *provider* (Postmark/SendGrid): pertence à notification-delivery (BC-14), exposta via `IEmailSender`.
- Composição de relatórios analíticos sob demanda (pertence ao reporting — BC-09).
- Notificações *push* ou *in-app* (pertence ao workflow-automation — Fase 2).
- Configuração do `horario_digest` e do fuso *IANA* do *tenant* (pertence à tenant-administration — BC-13).
- Provisionamento e agendamento dos *jobs* no Cloud Scheduler (responsabilidade operacional / *Infrastructure as Code*).

### 2.3 Fora do escopo do MVP

Consolidado na seção 9.

## 3. Personas / Atores

| Persona | Origem | Descrição |
|---|---|---|
| Vendedor / Executivo de Contas | FRD § Matriz RBAC (ACT-01); ubiquitous-language § BC-08 | Destinatário primário do digest; recebe o recorte das **próprias** pendências de terça a sexta e age via link autenticado de um clique. |
| Gestor de BU | FRD § Matriz RBAC (ACT-02) | Destinatário do azimute da semana na segunda, com escopo da sua *Business Unit*; recebe também as próprias pendências quando houver. |
| Executivo do Tenant / Tenant Admin | FRD § Matriz RBAC (ACT-03) | Destinatário do azimute da semana na segunda, com escopo consolidado do *tenant*. |
| Cloud Scheduler (sistema) | README § 11; TRD § 9.6 (ACT-07) | Dispara o *trigger* HTTP horário em UTC que aciona o worker de digest. |
| notification-delivery / IEmailSender (sistema) | README § 13; TRD § 7.3 (ACT-08) | Serviço que entrega o e-mail e retorna eventos de entrega, abertura e *bounce*. |
| organization (sistema) | README § 13; data-model § BC-08 | Fonte de usuários, papéis (*memberships*), *Business Units* e fuso do *tenant*. |
| opportunity-pipeline (sistema) | README § 13; data-model § StagnationView/ForecastView | Fonte de oportunidades estagnadas, fechamentos esperados e pipeline ponderado. |
| activity-management (sistema) | README § 13; data-model § BC-04 | Fonte de atividades vencidas e do dia; consumidor do token de ação emitido pelo digest. |
| goal-forecast (sistema) | README § 13; data-model § BC-05 | Fonte do bloco de metas (realizado vs. meta) do azimute. |
| audit-log (sistema) | README § 10; TRD § 9.3 | Consumidor de `DigestEmailSent` para a trilha de auditoria. |

## 4. Listas Canônicas

### 4.1 Lista canônica de papéis de destinatário

| Identificador (inglês) | Rótulo (pt-BR) | Recebe digest de pendências (ter–sex) | Recebe azimute (segunda) | Escopo do azimute |
|---|---|---|---|---|
| `Vendedor` | Vendedor | Sim, se tiver pendências próprias | Não | — |
| `GestorBU` | Gestor de BU | Sim, se tiver pendências próprias | Sim | Sua *Business Unit* |
| `TAdmin` | Executivo do Tenant / Tenant Admin | Sim, se tiver pendências próprias | Sim | *Tenant* consolidado |
| `Viewer` | Viewer | Não | Não | — |

> Papéis derivam de `user_memberships.papel` (data-model § BC-08). "Executivo do Tenant" (ACT-03) corresponde, no modelo de papéis, ao papel `TAdmin`. A representação técnica final dos papéis pertence ao organization-management e ao `design.md`.

### 4.2 Lista canônica de status de envio (`EmailDigestLog.status`)

| Identificador (inglês) | Rótulo (pt-BR) | Terminal | Descrição |
|---|---|---|---|
| `scheduled` | Agendado | Não | Envio registrado antes da chamada ao `IEmailSender` (reserva de idempotência). |
| `sent` | Enviado | Não | `IEmailSender` aceitou a mensagem; `message_id` do *provider* registrado. |
| `delivered` | Entregue | Não | *Provider* confirmou a entrega na caixa do destinatário (alimenta KPI-03). |
| `opened` | Aberto | Não | *Provider* sinalizou abertura do e-mail (alimenta KPI-04). |
| `bounced` | Rejeitado | Sim | *Provider* reportou *bounce*; conta para a taxa de falha. |
| `failed` | Falho | Sim | Falha definitiva no envio após esgotar as retentativas. |

> Os identificadores em inglês são referência conceitual de domínio. A enumeração técnica final pertence ao `design.md`. A coluna `status` do data-model (§ BC-06) é a fonte do conjunto base.

### 4.3 Lista canônica de blocos de conteúdo do digest

| Identificador (inglês) | Rótulo (pt-BR) | Dias | Condição de inclusão |
|---|---|---|---|
| `overdue_activities` | Atividades vencidas | Ter–Sex (e Seg, se houver) | Usuário tem atividades vencidas. |
| `today_activities` | Atividades de hoje | Ter–Sex (e Seg, se houver) | Usuário tem atividades com vencimento hoje. |
| `stale_opportunities` | Oportunidades estagnadas | Ter–Sex (e Seg, se houver) | Usuário possui oportunidade aberta estagnada (> 14 dias). |
| `overdue_closings` | Fechamentos vencidos | Ter–Sex (e Seg, se houver) | Usuário possui oportunidade com `data_fechamento_esperada` vencida. |
| `azimute_pipeline` | Azimute — pipeline e variação | Seg | Destinatário tem papel `GestorBU` ou `TAdmin`. |
| `azimute_metas` | Azimute — realizado vs. meta | Seg | Há meta cadastrada para o período (RN-018); omitido sem erro caso contrário. |

## 5. Requisitos Funcionais

### Req 1 — Receber o trigger horário do Cloud Scheduler

**Como** Cloud Scheduler **quero** acionar o worker de digest de hora em hora em UTC **para** que o processamento do digest ocorra no horário local correto de cada *tenant*, sem manter um agendamento por *tenant*.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-digest-01 (RF-09); RN-009; TRD § 9.6 (`digest-trigger`); DEC-009 |
| **Módulo** | digest |

**Critérios de Aceite:**

- 1.1 O worker expõe um endpoint interno de *trigger* acionado a cada hora cheia em UTC pelo Cloud Scheduler.
- 1.2 O endpoint de *trigger* só aceita chamadas autenticadas (identidade de serviço); chamadas não autenticadas são rejeitadas (ver RNF 7).
- 1.3 O *trigger* é processado de forma assíncrona: a resposta ao Cloud Scheduler não depende da conclusão do envio de todos os e-mails (ver RNF 8).
- 1.4 Um único disparo horário é suficiente para atender todos os *tenants* cujo horário local corresponde àquela hora UTC, sem necessidade de um agendamento por *tenant*.
- 1.5 O disparo carrega ou permite derivar a hora de referência UTC, base para o cálculo de elegibilidade da seção Req 2.

**Cross-ref:** Req 2, RNF 7, RNF 8; VAL-DIGEST-02 (job global vs. por *tenant*); ADR-0008

### Req 2 — Selecionar tenants elegíveis por fuso IANA e dia útil

**Como** worker de digest **quero** selecionar, a cada disparo horário UTC, os *tenants* cuja hora local corresponde ao `horario_digest` e cujo dia local é útil **para** enviar o digest no horário correto independentemente de fuso e de horário de verão.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-digest-01 (RF-09); RN-009; DEC-009; ubiquitous-language § BC-06 (`horario_digest`) |
| **Módulo** | digest |

**Critérios de Aceite:**

- 2.1 Para cada *tenant*, a hora UTC do disparo é convertida para a hora local usando o fuso *IANA* (`tenants.iana_timezone`); o *tenant* é elegível quando a hora local resultante é igual ao `horario_digest` (padrão 07:00).
- 2.2 O *tenant* só é elegível quando o **dia local** (no fuso do *tenant*) é dia útil — segunda a sexta-feira.
- 2.3 A conversão usa a base de fusos *IANA*, de modo que a transição de horário de verão (DST) não causa envio antecipado nem atrasado.
- 2.4 Um *tenant* no fuso `America/Sao_Paulo` (UTC−3) é elegível no disparo das 10:00 UTC (= 07:00 local).
- 2.5 *Tenants* inativos não são elegíveis.
- 2.6 Sábado e domingo no fuso do *tenant* não geram envio (calendário de feriados está fora do escopo do MVP — seção 9).

**Cross-ref:** Req 1, Req 3, PBT-01; RN-009; ADR-0008; VAL-06 (calendário de feriados, Fase 2)

### Req 3 — Selecionar destinatários por papel e por pendências

**Como** worker de digest **quero** selecionar, dentro de cada *tenant* elegível, os usuários que devem receber o digest **para** evitar e-mails irrelevantes e garantir que gestores recebam o azimute na segunda.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-digest-01/03 (RF-09); RN-011; RN-029; ubiquitous-language § BC-06 (`pendencia`) |
| **Módulo** | digest |

**Critérios de Aceite:**

- 3.1 Um usuário é elegível ao digest de pendências quando possui ao menos uma pendência própria: atividade vencida, atividade de hoje, oportunidade estagnada ou `data_fechamento_esperada` vencida.
- 3.2 Usuário **sem** pendências e **sem** papel de gestão (`GestorBU` ou `TAdmin`) **não** recebe o digest (RN-011).
- 3.3 Na segunda-feira, usuário com papel `GestorBU` ou `TAdmin` recebe o azimute da semana mesmo sem pendências próprias (RN-029).
- 3.4 O recorte de pendências do Vendedor contém apenas itens dos quais ele é responsável (`owner_id`).
- 3.5 O usuário deve estar ativo no *tenant* para ser destinatário.
- 3.6 A elegibilidade respeita o *opt-out* individual (Req 10), exceto o resumo de segunda para papéis de gestão.
- 3.7 Usuários com papel `Viewer` não são destinatários do digest.

**Cross-ref:** Req 2, Req 4, Req 5, Req 10, PBT-03; RN-011; RN-029

### Req 4 — Compor o digest de pendências (terça a sexta)

**Como** Vendedor **quero** receber, de terça a sexta, um e-mail com minhas pendências e ações de um clique **para** agir no início do dia sem precisar abrir o sistema.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-digest-02 (RF-09); RN-028; ubiquitous-language § BC-06 |
| **Módulo** | digest |

**Critérios de Aceite:**

- 4.1 O conteúdo do usuário inclui, quando existentes: atividades vencidas, atividades de hoje, oportunidades estagnadas (mais de 14 dias corridos sem atividade) e datas de fechamento esperadas já vencidas.
- 4.2 Cada atividade listada acompanha um link autenticado de um clique para concluir ou reagendar (Req 7).
- 4.3 O e-mail é renderizado com o *branding* do *tenant* (logo e cores), conforme configuração da tenant-administration.
- 4.4 O conteúdo reflete os blocos canônicos da seção 4.3 aplicáveis a dia de terça a sexta.
- 4.5 A oportunidade estagnada é apresentada conforme o critério já calculado pelo opportunity-pipeline (RN-028); o digest não recalcula o critério.
- 4.6 Quando o usuário foi selecionado por ter pendências, ao menos um bloco de pendências está presente no e-mail.

**Cross-ref:** Req 3, Req 6, Req 7; RN-028; FRD-digest-02

### Req 5 — Compor o azimute da semana (segunda-feira)

**Como** Gestor de BU ou Executivo do Tenant **quero** receber na segunda-feira um resumo executivo da semana **para** orientar a operação comercial com base em pipeline, variação e metas.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-digest-03 (RF-09); RN-018; RN-029; ubiquitous-language § BC-06 (`azimute_da_semana`) |
| **Módulo** | digest |

**Critérios de Aceite:**

- 5.1 Na segunda-feira, o azimute inclui: pipeline ponderado por estágio e *Business Unit*, variação vs. semana anterior, status global, ganhos do período e fechamentos esperados na semana.
- 5.2 O Gestor de BU recebe o azimute com o escopo da sua *Business Unit*; o Executivo do Tenant recebe o escopo consolidado do *tenant*.
- 5.3 Quando há meta cadastrada para o período, o azimute inclui o bloco de realizado vs. meta (e *gap*).
- 5.4 Quando **não** há meta cadastrada, o bloco de metas é **completamente omitido**, sem erro, sem espaço vazio e sem texto de marcador (*placeholder*) — degradação graciosa (RN-018).
- 5.5 Valores monetários no azimute (pipeline, variação, realizado, meta) são tratados em centavos inteiros; nenhuma comparação ou diferença usa ponto flutuante.
- 5.6 Se o destinatário do azimute também tiver pendências próprias, o digest contém ambos: o azimute e o bloco de pendências (RN-029).
- 5.7 O azimute é incluído **apenas** no digest de segunda-feira; de terça a sexta o conteúdo contém somente pendências (RN-029).

**Cross-ref:** Req 3, Req 6, PBT-04, PBT-06; RN-018; RN-029; FRD-digest-03; RF-08 (goal-forecast)

### Req 6 — Consumir dados via read model dos módulos donos

**Como** worker de digest **quero** obter oportunidades, atividades e metas a partir dos módulos donos **para** compor o conteúdo sem duplicar regras nem escrever em contextos alheios.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | data-model § 2 (ownership), § 6 (read models); README § 13/14; TRD § 7.3 |
| **Módulo** | digest |

**Critérios de Aceite:**

- 6.1 O digest obtém atividades vencidas e do dia via activity-management; oportunidades estagnadas, fechamentos e pipeline via opportunity-pipeline; metas via goal-forecast; usuários, papéis e fuso via organization.
- 6.2 O digest **não escreve** em tabelas de outros contextos (`opportunities`, `activities`, `goals`, `users`).
- 6.3 O digest **não recalcula** regras dos módulos donos (estagnação, forecast ponderado, comissão): consome o resultado já calculado.
- 6.4 Toda consulta é restrita ao `tenant_id` em processamento (ver RNF 1).
- 6.5 O padrão concreto de acesso a itens estagnados/vencidos (assinatura Pub/Sub de `opportunity.stale.v1` e `activity.overdue.v1` vs. consulta direta ao *read model*) está em aberto e será definido no `design.md` (VAL-TRD-13); qualquer das opções deve preservar os critérios desta seção.

**Cross-ref:** Req 4, Req 5, RNF 1; VAL-TRD-13; data-model § 6 (StagnationView, ForecastView)

### Req 7 — Gerar token de ação de um clique

**Como** worker de digest **quero** emitir um token autenticado por atividade pendente **para** permitir que o usuário conclua ou reagende a atividade em um clique a partir do e-mail.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-digest-02 (RF-09); README § 4/7; data-model § BC-06 (`digest_action_tokens`); ADR-0006 |
| **Módulo** | digest |

**Critérios de Aceite:**

- 7.1 Para cada atividade incluída no digest, é gerado um token de ação com a ação correspondente (`complete` ou `reschedule`), vinculado ao `tenant_id`, `user_id` e `activity_id`.
- 7.2 Cada token tem prazo de expiração (`expires_at`); o prazo exato é ponto a validar (sugestão 24h–48h — VAL-DIGEST-03 / VAL-TRD-05).
- 7.3 O link incluído no e-mail permite a ação sem login (*link_autenticado*).
- 7.4 A **validação e o processamento** do token no consumo pertencem ao activity-management; o digest apenas emite o token.
- 7.5 O token é gerado de forma que não seja previsível nem enumerável (ver RNF 7).

**Cross-ref:** Req 4, RNF 7, PBT-05; activity-management Req 7/RNF 5; ADR-0006; VAL-DIGEST-03; VAL-TRD-05

### Req 8 — Enviar o digest via notification-delivery e registrar o envio

**Como** worker de digest **quero** enviar o e-mail composto via `IEmailSender` e registrar o resultado **para** entregar o digest ao usuário e manter trilha do envio.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-digest-02 (RF-09); DEC-008; README § 13; data-model § BC-06 |
| **Módulo** | digest |

**Critérios de Aceite:**

- 8.1 O envio é feito exclusivamente via a interface `IEmailSender` do módulo notification-delivery; o digest não integra diretamente com o *provider* de e-mail.
- 8.2 Após o envio aceito pelo *provider*, o `EmailDigestLog` registra `tenant_id`, `user_id`, `digest_date` (no fuso do *tenant*), `status` e o `message_id` retornado.
- 8.3 O `digest_date` é a data local do *tenant*, não a data UTC.
- 8.4 Falha definitiva de envio registra `status` `failed`; *bounce* reportado pelo *provider* registra `bounced`.
- 8.5 O envio respeita o resultado da seleção (Req 3): usuários não elegíveis não recebem e-mail.

**Cross-ref:** Req 3, Req 9, Req 11, RNF 5; DEC-008; FRD-digest-02

### Req 9 — Garantir idempotência do digest por usuário e data

**Como** Vendedor **quero** receber no máximo um digest por dia **para** não ser incomodado por e-mails duplicados em retentativas do sistema.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-digest-04 (RF-09); RN-010; NFR-RES-02; README § 4 (idempotência) |
| **Módulo** | digest |

**Critérios de Aceite:**

- 9.1 Existe no máximo um envio efetivo por (`tenant_id`, `user_id`, `digest_date`); a unicidade dessa chave é a base da idempotência (RN-010).
- 9.2 Se já houver `EmailDigestLog` para (`tenant_id`, `user_id`, `digest_date`) com status `sent`, `delivered` ou `opened`, o worker não reenvia.
- 9.3 Um segundo disparo do Cloud Scheduler no mesmo dia, para o mesmo usuário, não gera segundo e-mail.
- 9.4 Uma retentativa por falha temporária do *provider* não gera segundo e-mail ao usuário.
- 9.5 A verificação de idempotência ocorre antes de invocar o `IEmailSender`.

**Cross-ref:** Req 8, RNF 2, PBT-02; RN-010; NFR-RES-02; FRD-digest-04

### Req 10 — Permitir opt-out individual do digest

**Como** usuário **quero** poder desativar o recebimento do digest diário **para** controlar quando recebo e-mails do CRM.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Should |
| **Origem** | RF-09 (digest acionável / opt-out); README § 4; LGPD by design (NFR-PRIV) |
| **Módulo** | digest |

**Critérios de Aceite:**

- 10.1 Um usuário com *opt-out* ativo não recebe o digest de pendências de terça a sexta.
- 10.2 O *opt-out* **não** suprime o resumo de segunda (azimute) para usuários com papel `GestorBU` ou `TAdmin`.
- 10.3 A preferência de *opt-out* é avaliada por usuário, dentro do *tenant*, durante a seleção de destinatários (Req 3).
- 10.4 A origem e a persistência da preferência de *opt-out* (módulo dono e mecanismo) são ponto a validar no `design.md` (VAL-DIGEST-04).

**Cross-ref:** Req 3; VAL-DIGEST-04; RF-09

### Req 11 — Registrar status de entrega e publicar evento de auditoria

**Como** operação do produto **quero** acompanhar o status de entrega e abertura do digest e ter trilha de auditoria do envio **para** medir entregabilidade (KPI-03/04) e manter rastreabilidade.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Should |
| **Origem** | FRD-digest-02/04 (RF-09); KPI-03/KPI-04; README § 10; TRD § 9.3 (`digest.email_sent.v1`) |
| **Módulo** | digest |

**Critérios de Aceite:**

- 11.1 Eventos de entrega do *provider* atualizam o `EmailDigestLog` para `delivered`, `opened` ou `bounced`, conforme o caso.
- 11.2 A taxa de entrega é mensurável a partir do `EmailDigestLog` (KPI-03, meta ≥ 98%); a taxa de abertura alimenta o KPI-04.
- 11.3 Após envio bem-sucedido por usuário, o digest publica o evento `DigestEmailSent` para a trilha de auditoria.
- 11.4 O evento de auditoria não contém PII em texto claro (ver RNF 3).

**Cross-ref:** Req 8, RNF 3, RNF 6; KPI-03; KPI-04; TRD § 9.3

## 6. Requisitos Não-Funcionais

### RNF 1 — Isolamento por tenant

| Campo | Valor |
|-------|-------|
| **Categoria** | Segurança |
| **Prioridade** | Must |
| **Origem** | NFR-SEG-01; DEC-006; RISCO-T01 |
| **Módulo** | digest |

**Descrição:**

Toda seleção, composição, leitura de *read models* e escrita em `email_digest_logs` e `digest_action_tokens` é restrita ao `tenant_id` em processamento. Um usuário de um *tenant* jamais pode receber conteúdo, links ou tokens de outro *tenant*. Vazamento de dados entre *tenants* é incidente severidade 1.

**Critérios de Aceite:**

- RNF-1.1 O conteúdo do digest de um usuário contém exclusivamente dados do `tenant_id` desse usuário.
- RNF-1.2 Tokens de ação gerados carregam o `tenant_id` do usuário e não podem referenciar atividades de outro *tenant*.
- RNF-1.3 Existe teste automatizado em CI que verifica que o processamento do *tenant* A não acessa nem mistura dados do *tenant* B.
- RNF-1.4 A falha de uma camada isolada de filtragem não resulta em vazamento (defesa em profundidade).

**Cross-ref:** NFR-SEG-01; ADR-0001; Req 6, Req 7

### RNF 2 — Idempotência resiliente a retentativas

| Campo | Valor |
|-------|-------|
| **Categoria** | Resiliência |
| **Prioridade** | Must |
| **Origem** | NFR-RES-02; RN-010; README § 15; TRD § 15.5 |
| **Módulo** | digest |

**Descrição:**

O envio do digest deve ser idempotente em relação a retentativas do Cloud Scheduler, retentativas do *provider* e reprocessamento parcial após falha. A chave de idempotência é (`tenant_id`, `user_id`, `digest_date`) no `EmailDigestLog`.

**Critérios de Aceite:**

- RNF-2.1 Teste de integração com retentativa forçada: o `EmailDigestLog` registra exatamente um envio por (`tenant_id`, `user_id`, `digest_date`).
- RNF-2.2 Falha na metade do processamento de um *tenant*, seguida de reprocessamento, não duplica e-mails de usuários já enviados.
- RNF-2.3 A verificação de idempotência ocorre antes da chamada ao `IEmailSender`, evitando envio antes do registro de reserva.
- RNF-2.4 A idempotência não depende de ordem de processamento dos usuários dentro do *tenant*.

**Cross-ref:** NFR-RES-02; RN-010; ADR-0004; Req 9, PBT-02

### RNF 3 — Ausência de PII em logs

| Campo | Valor |
|-------|-------|
| **Categoria** | Privacidade |
| **Prioridade** | Must |
| **Origem** | NFR-PRIV-01; README § 16/17; LGPD by design; `.forge/rules/architecture/observability.md` |
| **Módulo** | digest |

**Descrição:**

Os logs operacionais do worker não devem conter informação pessoal em texto claro — em especial o endereço de e-mail do destinatário e o conteúdo do digest. A correlação de logs usa identificadores opacos (`tenant_id`, `user_id`, `correlationId`), não PII.

**Critérios de Aceite:**

- RNF-3.1 Nenhum log do worker contém o endereço de e-mail do destinatário em texto claro.
- RNF-3.2 O conteúdo personalizado do e-mail (títulos de atividades, nomes de conta) não é registrado em log.
- RNF-3.3 Mensagens de erro e alertas referenciam o usuário por `user_id`, não por e-mail (ex.: MSG-031).
- RNF-3.4 O evento `DigestEmailSent` e a trilha de auditoria não expõem PII em texto claro.

**Cross-ref:** NFR-PRIV-01; README § 16; MSG-031; Req 11

### RNF 4 — Pontualidade e desempenho do processamento

| Campo | Valor |
|-------|-------|
| **Categoria** | Performance |
| **Prioridade** | Must |
| **Origem** | NFR-PERF-05; NFR-OPS-03; README § 15; US-01 (07:00–07:05) |
| **Módulo** | digest |

**Descrição:**

O processamento e enfileiramento do digest de um *tenant* deve concluir dentro da janela que garante a entrega no início do horário local configurado, sem comprometer o diferencial de produto.

**Critérios de Aceite:**

- RNF-4.1 O digest é processado e enfileirado para até 1.000 destinatários em ≤ 5 min por horário (NFR-PERF-05).
- RNF-4.2 O e-mail é entregue entre o `horario_digest` e até 5 minutos depois, no fuso do *tenant*, em dias úteis.
- RNF-4.3 A consulta a *read models* (estagnação, pipeline) não faz o processamento exceder a janela de pontualidade.
- RNF-4.4 A métrica de duração do processamento é exposta (`digest_processing_duration_seconds`).

**Cross-ref:** NFR-PERF-05; NFR-OPS-03; RISK-DIGEST-01; RNF 6

### RNF 5 — Resiliência de envio e isolamento de falhas

| Campo | Valor |
|-------|-------|
| **Categoria** | Resiliência |
| **Prioridade** | Must |
| **Origem** | NFR-RES-03; TRD § 15.4 (retry/backoff, circuit breaker); README § 19 (RISK-DIGEST-03) |
| **Módulo** | digest |

**Descrição:**

Falha transiente do *provider* de e-mail ou de um *tenant* específico não deve interromper o digest dos demais. O envio retenta com *backoff* exponencial e protege o worker contra indisponibilidade do *provider*.

**Critérios de Aceite:**

- RNF-5.1 Falha transiente do *provider* dispara retentativa com *backoff* exponencial, sem duplicar envio (RNF 2).
- RNF-5.2 Após esgotar as retentativas, o envio é marcado `failed` e um alerta operacional é gerado, sem derrubar o worker.
- RNF-5.3 Falha no processamento de um *tenant* não impede o processamento dos demais *tenants* elegíveis no mesmo horário.
- RNF-5.4 Ausência de dado opcional (ex.: meta) não causa erro de envio — degradação graciosa (RN-018, NFR-RES-03).

**Cross-ref:** NFR-RES-03; RISK-DIGEST-03; ADR-0004; Req 5, Req 8

### RNF 6 — Observabilidade do worker

| Campo | Valor |
|-------|-------|
| **Categoria** | Observabilidade |
| **Prioridade** | Should |
| **Origem** | NFR-OBS-01/02/04; README § 17; TRD § 17 |
| **Módulo** | digest |

**Descrição:**

O worker deve emitir logs estruturados, métricas e alertas que permitam detectar atraso, falha de entrega e queda de elegibilidade, com `tenant_id` e `correlationId` propagados, sem PII (RNF 3).

**Critérios de Aceite:**

- RNF-6.1 Cada *job* de digest registra `correlationId`, `tenant_id`, `digest_date`, contagem de destinatários selecionados e status, sem PII.
- RNF-6.2 São expostas as métricas `digest_jobs_processed_total`, `digest_emails_sent_total`, `digest_emails_failed_total` e `digest_processing_duration_seconds`.
- RNF-6.3 Há alerta quando nenhum *job* é processado após o horário esperado e quando a taxa de falha excede 2% (ou a taxa de entrega cai abaixo de 95% por mais de 1 hora).
- RNF-6.4 O *trace* cobre seleção de destinatários → composição → envio por usuário.

**Cross-ref:** NFR-OBS-01; NFR-OBS-04; README § 17; RISK-DIGEST-01

### RNF 7 — Segurança do trigger e do token de um clique

| Campo | Valor |
|-------|-------|
| **Categoria** | Segurança |
| **Prioridade** | Must |
| **Origem** | NFR-SEG-02; TRD § 12.2 (OIDC/WIF); ADR-0006; data-model § BC-06; VAL-TRD-05 |
| **Módulo** | digest |

**Descrição:**

O endpoint de *trigger* deve aceitar apenas chamadas autenticadas pela identidade do Cloud Scheduler, e o token de um clique deve ser não previsível, de uso único e expirável, sem permitir enumeração.

**Critérios de Aceite:**

- RNF-7.1 O endpoint interno de *trigger* só aceita chamadas com credencial de serviço válida (*token* OIDC via *Workload Identity Federation*); chamadas anônimas são rejeitadas.
- RNF-7.2 O token de ação é gerado com entropia suficiente para não ser previsível nem enumerável.
- RNF-7.3 O token tem `expires_at` definido e deixa de ser válido após o prazo (sugestão 24h–48h — VAL-DIGEST-03 / VAL-TRD-05).
- RNF-7.4 A emissão do token não revela existência de outras atividades ou tokens (anti-enumeração); a validação de uso único é responsabilidade do consumidor (activity-management).

**Cross-ref:** NFR-SEG-02; ADR-0006; Req 1, Req 7, PBT-05; VAL-TRD-05

### RNF 8 — Execução assíncrona obrigatória

| Campo | Valor |
|-------|-------|
| **Categoria** | Resiliência |
| **Prioridade** | Must |
| **Origem** | NFR-RES-01; DEC-009; TRD § 7.3 (worker separado) |
| **Módulo** | digest |

**Descrição:**

O processamento do digest é executado por um worker (`azim-digest-worker`) desacoplado da API de negócio. O *trigger* retorna sem aguardar a conclusão de todos os envios, e a falha do worker não afeta a disponibilidade da `azim-api`.

**Critérios de Aceite:**

- RNF-8.1 O endpoint de *trigger* aceita o disparo e retorna rapidamente, sem bloquear até o fim dos envios.
- RNF-8.2 O worker é um deployable independente; sua falha não degrada a API de negócio.
- RNF-8.3 Nenhuma etapa de composição ou envio do digest é executada de forma síncrona dentro da API de negócio.

**Cross-ref:** NFR-RES-01; DEC-009; Req 1; TRD § 7.3

### RNF 9 — Retenção e minimização dos logs de digest

| Campo | Valor |
|-------|-------|
| **Categoria** | Privacidade |
| **Prioridade** | Should |
| **Origem** | NFR-OPS-04; LGPD; README § 12/16; data-model § 5 (90 dias); KPI-03/04 |
| **Módulo** | digest |

**Descrição:**

O `EmailDigestLog` é a trilha de entregabilidade e idempotência; deve reter apenas os dados necessários e por prazo definido, e os `digest_action_tokens` devem ser purgados após expiração.

**Critérios de Aceite:**

- RNF-9.1 `email_digest_logs` é retido por 90 dias (KPI-03/04); registros mais antigos são purgados.
- RNF-9.2 `digest_action_tokens` é purgado após `expires_at` (prazo exato em VAL-TRD-05).
- RNF-9.3 O `EmailDigestLog` armazena identificadores e status, não o conteúdo do e-mail.

**Cross-ref:** NFR-OPS-04; data-model § 5; README § 12; VAL-TRD-05

### RNF 10 — Auditoria do envio

| Campo | Valor |
|-------|-------|
| **Categoria** | Auditoria |
| **Prioridade** | Should |
| **Origem** | NFR-AUD-02; README § 10; `.forge/rules/domain/audit-immutability.md` |
| **Módulo** | digest |

**Descrição:**

O envio bem-sucedido do digest gera um evento de auditoria (`DigestEmailSent`) consumido pela trilha de auditoria *append-only*, sem PII em texto claro.

**Critérios de Aceite:**

- RNF-10.1 Cada envio bem-sucedido por usuário gera exatamente um evento `DigestEmailSent`.
- RNF-10.2 O evento referencia `tenant_id`, `user_id` e `digest_date`, sem expor e-mail nem conteúdo.
- RNF-10.3 A retentativa idempotente não gera evento de auditoria duplicado processado.

**Cross-ref:** NFR-AUD-02; `.forge/rules/domain/audit-immutability.md`; Req 9, Req 11

## 7. Property-Based Testing

### PBT-01 — Seleção por fuso IANA neutra a DST

**Mapeia para:** Req 2
**Tipo:** Invariante matemática

**Propriedade:**

> Para qualquer *tenant* com fuso *IANA* e `horario_digest` arbitrários, e qualquer hora de disparo UTC gerada (incluindo datas em transição de horário de verão), o *tenant* é elegível se, e somente se, a conversão da hora UTC para a hora local do fuso resulta exatamente no `horario_digest` **e** o dia local correspondente é dia útil (segunda a sexta) — sem antecipação nem atraso causado por DST.

### PBT-02 — Idempotência do envio sob retentativas

**Mapeia para:** Req 9, RNF 2
**Tipo:** Idempotência

**Propriedade:**

> Para qualquer usuário elegível e qualquer número N ≥ 1 de disparos/retentativas no mesmo `digest_date`, o estado final tem exatamente um envio efetivo registrado em `EmailDigestLog` para (`tenant_id`, `user_id`, `digest_date`) e no máximo um e-mail entregue ao usuário.

### PBT-03 — Regra de inclusão de destinatário

**Mapeia para:** Req 3
**Tipo:** Invariante (máquina de decisão)

**Propriedade:**

> Para qualquer usuário gerado com papel, conjunto de pendências, estado de *opt-out* e dia da semana arbitrários, o usuário recebe o digest se, e somente se: (a) tem ao menos uma pendência própria, sem *opt-out*; ou (b) é segunda-feira e o papel é `GestorBU` ou `TAdmin` (caso em que o *opt-out* não suprime o azimute). Usuário sem pendências e sem papel de gestão nunca recebe; `Viewer` e usuário inativo nunca recebem.

### PBT-04 — Omissão graciosa do bloco de metas

**Mapeia para:** Req 5, RNF 5
**Tipo:** Invariante

**Propriedade:**

> Para qualquer azimute gerado em segunda-feira, o bloco de realizado vs. meta está presente se, e somente se, existe meta cadastrada para o período; quando não existe, o azimute é composto e enviado com sucesso, sem erro, sem espaço vazio e sem texto de marcador para o bloco de metas.

### PBT-05 — Token de um clique: não previsível e expirável

**Mapeia para:** Req 7, RNF 7
**Tipo:** Anti-enumeração

**Propriedade:**

> Para qualquer conjunto gerado de atividades pendentes, os tokens emitidos são distintos, não sequenciais e não deriváveis uns dos outros, cada um vinculado a (`tenant_id`, `user_id`, `activity_id`) e com `expires_at` no futuro no momento da emissão; a emissão não permite inferir a existência de atividades ou tokens de outros usuários ou *tenants*.

### PBT-06 — Azimute apenas na segunda-feira

**Mapeia para:** Req 5
**Tipo:** State machine (calendário)

**Propriedade:**

> Para qualquer destinatário e qualquer dia local gerado, o bloco de azimute está presente no digest se, e somente se, o dia local é segunda-feira e o papel é `GestorBU` ou `TAdmin`; de terça a sexta o conteúdo nunca contém o azimute, apenas pendências.

## 8. Glossário local

| Termo | Definição | Origem |
|---|---|---|
| Digest | E-mail diário acionável enviado ao usuário com pendências comerciais e ações de um clique. | ubiquitous-language § BC-06 |
| DigestJob | Execução do digest para um *tenant* em uma data específica. | README § 7; data-model § BC-06 |
| EmailDigestLog | Registro de envio por (`tenant_id`, `user_id`, `digest_date`) que garante idempotência (RN-010). | ubiquitous-language § BC-06; data-model § BC-06 |
| DigestActionToken | Token de uso único e expirável emitido pelo digest para conclusão/reagendamento de atividade em um clique. | README § 7; data-model § BC-06 |
| `horario_digest` | Hora local, no fuso do *tenant*, em que o digest é disparado (padrão 07:00). | ubiquitous-language § BC-06; DEC-009 |
| Fuso *IANA* | Identificador de fuso horário (ex.: `America/Sao_Paulo`) usado para converter a hora UTC do disparo em hora local do *tenant*. | data-model § BC-13 (`tenants.iana_timezone`) |
| Azimute da semana | Conteúdo executivo do digest de segunda: pipeline por estágio/BU, variação, status global, ganhos, fechamentos e metas. | ubiquitous-language § BC-06 (`azimute_da_semana`); RN-029 |
| Pendência (*pendencia*) | Atividade vencida, atividade de hoje, oportunidade estagnada ou data de fechamento vencida — critério de inclusão (RN-011). | ubiquitous-language § BC-06 |
| Estagnação (*stale*) | Oportunidade aberta sem atividade registrada há mais de 14 dias corridos (RN-028); calculada pelo opportunity-pipeline. | ubiquitous-language § BC-01; RN-028 |
| Vencida (*overdue*) | Atividade com vencimento no passado e status não concluída; calculada pelo activity-management. | ubiquitous-language § BC-04 |
| Link autenticado (*link_autenticado*) | URL com token incluída no digest para executar a ação em um clique sem login. | ubiquitous-language § BC-06 |
| *Opt-out* | Preferência individual de não receber o digest de pendências; não suprime o azimute de segunda para gestores. | Req 10; RF-09 |
| `IEmailSender` | Interface do módulo notification-delivery usada para enviar o e-mail; abstrai o *provider* (Postmark/SendGrid). | README § 13; DEC-008 |
| *Business Unit* (BU) | Unidade de negócio do *tenant*; escopo do azimute do Gestor de BU. | ubiquitous-language § BC-08 |
| Objeto de valor | Elemento de domínio sem identidade própria, definido por seus atributos. | `.forge/rules/architecture/ddd.md` |

## 9. Fora do escopo do MVP

- Calendário de feriados: de terça a sexta e dias úteis são determinados apenas por dia da semana no fuso do *tenant*; feriados não suspendem o digest no MVP (VAL-06 / LAC-06 — Fase 2).
- Notificações *push* ou *in-app* do digest (pertence ao workflow-automation — Fase 2).
- *Template* visual avançado de e-mail; o MVP usa *template* funcional com *branding* básico do *tenant*.
- Configuração por usuário do horário de envio (o `horario_digest` é por *tenant*, não por usuário).
- Resumo semanal configurável (frequência, blocos selecionáveis) — o azimute é fixo na segunda-feira no MVP.
- Validação e processamento do token de ação no consumo (pertence ao activity-management — BC-04).
- Entrega física do e-mail e *fallback* entre *providers* (pertence à notification-delivery — BC-14).
- Recálculo de regras de outros contextos (estagnação, forecast ponderado, comissão) — o digest apenas consome resultados.
- Multimoeda nos valores do azimute (centavos em BRL no MVP — DEC-011 / LAC-05).

## 10. Referências cruzadas

| Referência | Relação |
|---|---|
| docs/product/frd-nfrd/frd.md § FRD-digest-01..04 (RF-09) | Origem funcional principal |
| docs/product/frd-nfrd/nfrd.md § NFR-PERF-05, NFR-RES-01/02/03, NFR-SEG-01/02, NFR-PRIV-01, NFR-OBS-01/02/04, NFR-OPS-03/04 | Origem não-funcional |
| docs/product/trd/trd.md § 7.3 (`azim-digest-worker`), § 9.3/9.6 (eventos e Scheduler), § 15 (idempotência/resiliência), § 17 (observabilidade) | Arquitetura, eventos e processo agendado |
| docs/product/data-model/data-model.md § BC-06 (`email_digest_logs`, `digest_action_tokens`), § BC-13 (`tenants.iana_timezone`, `digest_time`), § 6 (read models) | Modelo de dados de referência |
| docs/product/glossary/ubiquitous-language.md § BC-06, § BC-01, § BC-04, § BC-08 | Linguagem ubíqua |
| docs/product/modules/digest/README.md | Visão de módulo, riscos e pontos a validar |
| docs/product/modules/activity-management/requirements.md § Req 7, RNF 5 | Consumo/validação do token de ação de um clique |
| docs/product/modules/notification-delivery/requirements.md | `IEmailSender` e entrega do e-mail |
| RN-009 (seleção por fuso), RN-010 (idempotência), RN-011 (inclusão), RN-018 (omissão de metas), RN-028 (estagnação), RN-029 (azimute na segunda) | Regras de negócio aplicáveis |
| ADR-0004 (outbox/idempotência), ADR-0006 (token de link autenticado), ADR-0008 (scheduling por fuso IANA) | Decisões arquiteturais referenciadas (a criar/confirmar) |
| VAL-TRD-13 (Pub/Sub vs. DB polling do worker), VAL-TRD-05 (TTL do token), VAL-DIGEST-01/02/03, VAL-DIGEST-04 (origem do opt-out), VAL-06 (feriados — Fase 2) | Pontos a validar (resolver no `design.md`) |
| `.forge/rules/domain/audit-immutability.md`, `.forge/rules/domain/money-as-cents.md`, `.forge/rules/architecture/security-and-compliance.md`, `.forge/rules/architecture/observability.md` | Regras transversais |

