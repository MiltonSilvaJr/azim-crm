# ACT — Activity Management
**Requisitos Funcionais e Não-Funcionais**

- Versão: 0.1.0
- Data: 2026-06-11
- Status: Rascunho para revisão
- Referência pai: docs/product/frd-nfrd/frd.md § RF-07 (FRD-activity-01..04)

## Histórico de Versões

| Versão | Data | Status | Descrição da alteração |
|--------|------|--------|------------------------|
| 0.1.0 | 2026-06-11 | Rascunho para revisão | Criação inicial do documento de requisitos do módulo activity-management |

## 1. Visão Geral

O módulo **activity-management** (Bounded Context BC-04, subdomínio de suporte — *Supporting Subdomain*, deployable `azim-api`) é responsável pelo ciclo de vida das atividades comerciais (*activities*) do Azim CRM: criação, atualização, conclusão, reagendamento e cancelamento.

Uma atividade (*Activity*) é uma ação comercial registrada com tipo, título, data de vencimento, responsável, prioridade e status. Atividades podem ser vinculadas, de forma opcional, a uma oportunidade (*opportunity*) e/ou a uma conta (*account*).

O módulo entrega valor a três consumidores principais:

- **Vendedor:** visão "Meu dia / Minha semana" das próprias atividades (vencidas, de hoje e próximas) e conclusão em um clique.
- **digest (BC-06):** lista de atividades vencidas e do dia por usuário, além da conclusão via link autenticado de um clique (*link_autenticado*) baseado em token emitido pelo próprio módulo digest.
- **opportunity-pipeline (BC-01):** consulta da última atividade concluída por oportunidade, base para a detecção de estagnação (*stagnation*, RN-028: oportunidade aberta sem atividade há mais de 14 dias corridos).

Este documento define **o quê** o módulo deve fazer. Decisões de **como** (esquema de tabelas, índices, frameworks, endpoints detalhados, estrutura de token) pertencem ao `design.md`, ao `tasks.md` e aos *Architecture Decision Records* (ADRs) referenciados.

## 2. Escopo

### 2.1 Incluído

- CRUD de atividades comerciais: criar, visualizar, atualizar e excluir.
- Atributos de atividade: tipo, título, descrição (opcional), data prevista de vencimento, responsável, prioridade e status.
- Vínculos opcionais a oportunidade (`opportunity_id`) e/ou a conta (`account_id`) por chave estrangeira real.
- Máquina de estados de status da atividade, com transições válidas e inválidas.
- Visão "Meu dia / Minha semana" (*to_do*): atividades vencidas, de hoje e próximas do usuário.
- Conclusão em um clique a partir da lista da interface web.
- Conclusão em um clique a partir do digest, via link autenticado com token emitido pelo módulo digest.
- Reagendamento de atividade (alteração da data prevista).
- Sugestão de próxima atividade ao concluir uma atividade vinculada a oportunidade aberta.
- Métrica de saúde do funil: manter sempre ao menos um *follow-up* futuro por oportunidade aberta.
- Detecção de atividade vencida (*overdue*) para alimentar o digest (evento `ActivityOverdue`).
- Exposição da última atividade concluída por oportunidade, como insumo da detecção de estagnação.
- Publicação dos eventos de domínio `ActivityCreated`, `ActivityCompleted` e `ActivityOverdue`.
- Registro de auditoria imutável para toda escrita.
- Isolamento de dados por *tenant*.

### 2.2 Excluído

- Emissão e ciclo de vida do token de ação do digest (`digest_action_tokens`): a emissão do token pertence ao módulo digest (BC-06). O activity-management apenas **valida e processa** o token recebido.
- Composição e envio do e-mail do digest (pertence ao digest).
- A regra de negócio de estagnação na oportunidade em si (pertence ao opportunity-pipeline — RN-028). O activity-management apenas fornece o dado de última atividade.
- Autenticação de usuário e resolução de *tenant* (pertence à camada de plataforma / organization-management).

### 2.3 Fora do escopo do MVP

Consolidado na seção 9.

## 3. Personas / Atores

| Persona | Origem | Descrição |
|---|---|---|
| Vendedor | FRD § Matriz RBAC; ubiquitous-language BC-08 | Cria e gerencia as próprias atividades; usa a visão "Meu dia / Minha semana"; conclui atividades pela lista e pelo digest. |
| Gestor de BU | FRD § Matriz RBAC | Cria e gerencia atividades da sua *Business Unit* (BU); visualiza atividades dos vendedores da BU. |
| Viewer | FRD § Matriz RBAC | Visualiza atividades conforme escopo permitido; não cria nem edita. |
| digest (sistema) | README § 13; TRD § eventos de domínio | Sistema consumidor: consulta atividades vencidas e do dia; processa conclusão via link autenticado de um clique. |
| opportunity-pipeline (sistema) | README § 13; data-model § StagnationView | Sistema consumidor: consulta a última atividade concluída por oportunidade para detecção de estagnação. |
| Job de detecção de vencidas (sistema) | TRD § scheduler `activity-overdue` | Processo agendado que detecta atividades vencidas e dispara o evento `ActivityOverdue`. |

## 4. Lista canônica de status e tipos

### 4.1 Status da atividade

| Identificador (inglês) | Rótulo (pt-BR) | Terminal | Descrição |
|---|---|---|---|
| `pending` | Pendente | Não | Atividade registrada e ainda não iniciada. |
| `in_progress` | Em andamento | Não | Atividade iniciada e ainda não concluída. |
| `completed` | Concluída | Sim | Atividade concluída; `completed_at` preenchido. |
| `cancelled` | Cancelada | Sim | Atividade descartada sem conclusão. |

Transições válidas:

| De | Para |
|---|---|
| `pending` | `in_progress`, `completed`, `cancelled` |
| `in_progress` | `pending`, `completed`, `cancelled` |
| `completed` | — (terminal) |
| `cancelled` | — (terminal) |

Qualquer transição não listada é inválida e deve ser rejeitada. A reabertura de atividade `completed` ou `cancelled` está fora do escopo do MVP (seção 9).

### 4.2 Tipos de atividade (`activity_type`)

| Identificador (inglês) | Rótulo (pt-BR) |
|---|---|
| `meeting` | Reunião |
| `follow_up` | Follow-up |
| `call` | Ligação |
| `email` | E-mail |
| `task` | Tarefa |

### 4.3 Prioridade

| Identificador (inglês) | Rótulo (pt-BR) |
|---|---|
| `low` | Baixa |
| `medium` | Média |
| `high` | Alta |

> Os identificadores em inglês são referência conceitual de domínio. A representação técnica final (coluna, *enum*, restrição) pertence ao `design.md`. O glossário canônico (ubiquitous-language § BC-04) lista os tipos como call, reunião, e-mail e tarefa; o tipo `follow_up` é acrescentado por este módulo como tipo explícito de acompanhamento, com origem em RF-07 (sugestão de próxima atividade / follow-up).

## 5. Requisitos Funcionais

### Req 1 — Criar atividade

**Como** Vendedor **quero** registrar uma atividade comercial **para** organizar e acompanhar minhas próximas ações junto a oportunidades e contas.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-activity-01 (RF-07); data-model § BC-04 |
| **Módulo** | activity-management |

**Critérios de Aceite:**

- 1.1 É possível criar uma atividade informando obrigatoriamente: tipo (`activity_type`), título, data prevista de vencimento (`due_at`) e responsável (`owner_id`).
- 1.2 É possível informar opcionalmente: descrição, prioridade e vínculo com oportunidade e/ou conta.
- 1.3 Quando a prioridade não é informada, a atividade assume prioridade `medium` (Média).
- 1.4 Toda atividade criada nasce com status `pending` (Pendente).
- 1.5 Título é obrigatório e não pode ser vazio ou conter apenas espaços.
- 1.6 O tipo informado deve pertencer à lista canônica da seção 4.2; valores fora da lista são rejeitados.
- 1.7 A atividade é criada vinculada ao `tenant_id` e à `bu_id` do contexto autenticado.
- 1.8 Atividade criada com `due_at` no dia corrente ou anterior aparece na visão "Meu dia" do responsável (Req 5).

**Cross-ref:** Req 3, Req 14, RNF 1, RNF 2; RN-028 (opportunity-pipeline)

### Req 2 — Atualizar e excluir atividade

**Como** Vendedor **quero** editar ou excluir uma atividade **para** corrigir informações ou remover registros criados por engano.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-activity-01 (RF-07) |
| **Módulo** | activity-management |

**Critérios de Aceite:**

- 2.1 É possível atualizar título, descrição, tipo, prioridade, data prevista, responsável e vínculos de uma atividade não terminal.
- 2.2 A atualização de status só é aceita se respeitar a máquina de estados da seção 4.1 (ver Req 4).
- 2.3 É possível excluir uma atividade; a exclusão é registrada na trilha de auditoria (RNF 2).
- 2.4 Uma atualização ou exclusão só é permitida por persona com papel autorizado sobre a atividade (Vendedor sobre as próprias; Gestor de BU sobre as da sua BU).
- 2.5 Tentativa de atualizar ou excluir atividade de outro *tenant* resulta em falha indistinguível de "não encontrado" (RNF 1).

**Cross-ref:** Req 4, RNF 1, RNF 2

### Req 3 — Vincular atividade a oportunidade e/ou conta

**Como** Vendedor **quero** vincular a atividade a uma oportunidade e/ou a uma conta **para** manter o histórico comercial consolidado e alimentar a detecção de estagnação.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-activity-01 (RF-07); data-model § BC-04 (opportunity_id, account_id nullable) |
| **Módulo** | activity-management |

**Critérios de Aceite:**

- 3.1 Os vínculos `opportunity_id` e `account_id` são opcionais e independentes: a atividade pode ter ambos, apenas um, ou nenhum.
- 3.2 Quando informado, `opportunity_id` deve referenciar uma oportunidade existente do mesmo *tenant* (chave estrangeira real).
- 3.3 Quando informado, `account_id` deve referenciar uma conta existente do mesmo *tenant* (chave estrangeira real).
- 3.4 Uma atividade vinculada a uma oportunidade aparece na linha do tempo dessa oportunidade.
- 3.5 A criação de uma atividade vinculada a uma oportunidade interrompe o contador de estagnação dessa oportunidade no momento do registro (insumo para RN-028).

**Cross-ref:** Req 1, Req 12; RN-028 (opportunity-pipeline); FRD-account-03 (visão 360°)

### Req 4 — Máquina de estados de status

**Como** Engenheiro do sistema **quero** que o status da atividade siga transições válidas e bem definidas **para** garantir integridade do ciclo de vida e previsibilidade dos eventos emitidos.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | Decisão de modelagem do módulo; FRD-activity-03 (conclusão); ubiquitous-language § BC-04 |
| **Módulo** | activity-management |

**Critérios de Aceite:**

- 4.1 Os status válidos são exatamente: `pending`, `in_progress`, `completed`, `cancelled` (seção 4.1).
- 4.2 As únicas transições aceitas são as listadas na seção 4.1; qualquer outra transição é rejeitada com erro de negócio.
- 4.3 `completed` e `cancelled` são estados terminais: nenhuma transição de saída é aceita no MVP.
- 4.4 A transição para `completed` preenche `completed_at` com o instante da conclusão.
- 4.5 A transição para `cancelled` não preenche `completed_at`.
- 4.6 Toda transição de status gera registro na trilha de auditoria (RNF 2).

**Cross-ref:** Req 6, Req 7, RNF 2, PBT-01

### Req 5 — Visão "Meu dia / Minha semana"

**Como** Vendedor **quero** ver minhas atividades organizadas em vencidas, de hoje e próximas **para** priorizar minhas ações do dia e da semana.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-activity-02 (RF-07); ubiquitous-language § BC-04 (to_do) |
| **Módulo** | activity-management |

**Critérios de Aceite:**

- 5.1 A visão agrupa as atividades não terminais do usuário em três faixas: **vencidas** (`due_at` anterior ao dia corrente e status não concluído), **hoje** (`due_at` no dia corrente) e **próximas** (`due_at` posterior ao dia corrente).
- 5.2 As faixas de data são calculadas no fuso horário (IANA) configurado no *tenant*.
- 5.3 Atividades com status `completed` ou `cancelled` não aparecem em nenhuma das faixas.
- 5.4 As atividades vencidas são sinalizadas para destaque visual.
- 5.5 A visão exibe apenas atividades visíveis à persona conforme escopo de BU e papel.
- 5.6 A mesma visão é consultável pelo módulo digest, restrita a um usuário (atividades vencidas e do dia).

**Cross-ref:** Req 11, RNF 1; FRD-digest-02

### Req 6 — Concluir atividade em um clique pela lista

**Como** Vendedor **quero** concluir uma atividade com um clique a partir da lista **para** registrar o avanço sem abrir o formulário completo.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-activity-03 (RF-07) |
| **Módulo** | activity-management |

**Critérios de Aceite:**

- 6.1 A conclusão muda o status para `completed` e preenche `completed_at` com o instante atual.
- 6.2 A atividade concluída deixa de aparecer nas faixas vencidas/hoje/próximas da visão "Meu dia" (Req 5).
- 6.3 A conclusão só é aceita se o status atual permitir transição para `completed` (Req 4).
- 6.4 Concluir uma atividade já concluída não altera `completed_at` e não gera novo evento `ActivityCompleted` (idempotência — ver PBT-02).
- 6.5 Após a conclusão, o sistema oferece a sugestão de próxima atividade (Req 9), quando aplicável.
- 6.6 A conclusão publica o evento `ActivityCompleted` (Req 14) e gera registro de auditoria (RNF 2).

**Cross-ref:** Req 4, Req 7, Req 9, Req 14, RNF 2, RNF 3, PBT-02

### Req 7 — Concluir atividade via link autenticado do digest

**Como** Vendedor **quero** concluir uma atividade clicando no link do e-mail do digest **para** registrar o avanço sem precisar acessar o portal.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-activity-03 (RF-07); README § 18.2; data-model § digest_action_tokens |
| **Módulo** | activity-management |

**Critérios de Aceite:**

- 7.1 O módulo recebe um token de ação do digest (`digest_action_token`) e processa a conclusão da atividade associada.
- 7.2 O token é validado antes de qualquer escrita: deve existir, não estar expirado (`expires_at` no futuro) e não ter sido usado (`used_at` vazio).
- 7.3 Token válido conclui a atividade (mesma semântica do Req 6) e marca o token como usado (`used_at` preenchido).
- 7.4 Token expirado é rejeitado e o usuário é orientado a acessar o portal (mensagem MSG-030).
- 7.5 Token já utilizado retorna confirmação de sucesso sem alterar novamente o estado (mensagem MSG-029) — comportamento idempotente.
- 7.6 Token inexistente ou malformado é rejeitado sem revelar a existência ou não de atividades (anti-enumeração — RNF 1, RNF 5).
- 7.7 A emissão do token é responsabilidade do módulo digest; este módulo apenas valida e processa.
- 7.8 A conclusão via token publica `ActivityCompleted` (Req 14) e gera registro de auditoria correlacionando token e atividade (RNF 2, RNF 6).

**Cross-ref:** Req 6, Req 8, RNF 1, RNF 3, RNF 5, PBT-02, PBT-03; NFR-RES-02

### Req 8 — Reagendar atividade

**Como** Vendedor **quero** reagendar uma atividade **para** mover a ação para uma nova data sem perder o registro.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Should |
| **Origem** | FRD-activity-03 (fluxo de exceção / reschedule); README § 9 (reschedule) |
| **Módulo** | activity-management |

**Critérios de Aceite:**

- 8.1 É possível alterar a `due_at` de uma atividade não terminal para uma nova data.
- 8.2 O reagendamento não altera o status, exceto manter a atividade fora dos estados terminais.
- 8.3 A atividade reagendada é recalculada nas faixas da visão "Meu dia" conforme a nova `due_at` (Req 5).
- 8.4 O reagendamento gera registro de auditoria (RNF 2).
- 8.5 O reagendamento também pode ser disparado via link autenticado do digest, respeitando as mesmas validações de token do Req 7.

**Cross-ref:** Req 5, Req 7, RNF 2

### Req 9 — Sugerir próxima atividade ao concluir

**Como** Vendedor **quero** que o sistema sugira a próxima atividade ao concluir uma **para** manter o acompanhamento contínuo da oportunidade.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-activity-04 (RF-07) |
| **Módulo** | activity-management |

**Critérios de Aceite:**

- 9.1 Ao concluir uma atividade vinculada a uma oportunidade aberta, o sistema oferece a sugestão de criação de uma próxima atividade.
- 9.2 A sugestão pré-preenche o vínculo com a mesma oportunidade (e conta, quando houver).
- 9.3 O usuário pode ignorar a sugestão sem qualquer impacto no estado da atividade concluída.
- 9.4 A sugestão não cria atividade automaticamente: depende de confirmação do usuário.
- 9.5 Para atividade sem vínculo de oportunidade, ou vinculada a oportunidade já fechada (ganha ou perdida), a sugestão é opcional e pode ser omitida.

**Cross-ref:** Req 6, Req 7, Req 10

### Req 10 — Saúde do funil: ao menos um follow-up futuro por oportunidade aberta

**Como** Gestor de BU **quero** acompanhar se cada oportunidade aberta tem ao menos um follow-up futuro agendado **para** medir a saúde do funil e evitar oportunidades sem próximo passo.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Should |
| **Origem** | FRD-activity-04 + RN-028 (interpretação de saúde do funil); README § 3 |
| **Módulo** | activity-management |

**Critérios de Aceite:**

- 10.1 O módulo expõe, por oportunidade aberta, se existe ao menos uma atividade não terminal com `due_at` no futuro (próximo follow-up).
- 10.2 Uma oportunidade aberta sem nenhuma atividade futura não terminal é sinalizável como "sem próximo passo".
- 10.3 A sugestão de próxima atividade (Req 9) tem como objetivo manter o invariante de ao menos um follow-up futuro por oportunidade aberta.
- 10.4 Esta métrica é de saúde do funil e não bloqueia a conclusão de atividades (a ausência de follow-up futuro é sinalização, não impedimento).

**Cross-ref:** Req 9, Req 12; RN-028 (opportunity-pipeline); PBT-05

### Req 11 — Detectar atividade vencida

**Como** digest (sistema) **quero** receber as atividades vencidas de cada usuário **para** compor a lista de pendências do e-mail diário.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-activity-02 (RF-07); TRD § scheduler `activity-overdue`; README § 10 (ActivityOverdue) |
| **Módulo** | activity-management |

**Critérios de Aceite:**

- 11.1 Uma atividade é considerada vencida (*overdue*) quando `due_at` é anterior ao instante atual e o status não é terminal (`completed`/`cancelled`).
- 11.2 O processo agendado de detecção identifica as atividades vencidas e publica o evento `ActivityOverdue` (Req 14) para o digest.
- 11.3 O módulo expõe a consulta de atividades vencidas por usuário (`owner_id`), respeitando o isolamento por *tenant*.
- 11.4 O módulo expõe a consulta de atividades do dia por usuário (`due_at` no dia corrente, status não terminal).
- 11.5 Atividades concluídas ou canceladas nunca são reportadas como vencidas.

**Cross-ref:** Req 5, Req 14, RNF 1, PBT-04; FRD-digest-02

### Req 12 — Expor última atividade concluída por oportunidade

**Como** opportunity-pipeline (sistema) **quero** consultar a última atividade concluída de uma oportunidade **para** detectar estagnação (RN-028).

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | README § 9, § 13; data-model § StagnationView; RN-028 |
| **Módulo** | activity-management |

**Critérios de Aceite:**

- 12.1 O módulo expõe, por `opportunity_id`, a data/hora da última atividade concluída (`completed_at` mais recente).
- 12.2 A consulta retorna ausência de atividade quando a oportunidade nunca teve atividade concluída.
- 12.3 A consulta respeita o isolamento por *tenant*.
- 12.4 O critério de estagnação (mais de 14 dias corridos sem atividade) é avaliado pelo opportunity-pipeline; este módulo fornece apenas o dado base.

**Cross-ref:** Req 3, Req 10, RNF 1, RNF 4; RN-028 (opportunity-pipeline)

### Req 13 — Visibilidade conforme papel e BU

**Como** Gestor de BU **quero** ver as atividades da minha BU e o Vendedor apenas as próprias **para** respeitar a matriz de permissões do CRM.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § Matriz RBAC; NFR-SEG-03 |
| **Módulo** | activity-management |

**Critérios de Aceite:**

- 13.1 Vendedor visualiza e gerencia apenas as atividades das quais é responsável.
- 13.2 Gestor de BU visualiza e gerencia as atividades da sua BU.
- 13.3 Viewer visualiza atividades conforme o escopo concedido, sem permissão de criação ou edição.
- 13.4 Toda leitura e escrita é verificada quanto ao papel da persona no endpoint correspondente.
- 13.5 Nenhuma persona acessa atividades de outro *tenant* (RNF 1).

**Cross-ref:** Req 2, Req 5, RNF 1; NFR-SEG-03

### Req 14 — Publicar eventos de domínio

**Como** Arquiteto do sistema **quero** que o módulo publique eventos de domínio de atividade **para** alimentar digest, audit-log, reporting e opportunity-pipeline de forma desacoplada.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | README § 10; TRD § eventos (`activity.created.v1`, `activity.completed.v1`, `activity.overdue.v1`) |
| **Módulo** | activity-management |

**Critérios de Aceite:**

- 14.1 Após a criação de uma atividade, o módulo publica `ActivityCreated`.
- 14.2 Após a conclusão de uma atividade, o módulo publica `ActivityCompleted` exatamente uma vez por conclusão efetiva (consumido por audit-log, opportunity-pipeline e reporting).
- 14.3 Ao detectar uma atividade vencida, o processo agendado publica `ActivityOverdue` (consumido pelo digest).
- 14.4 A publicação de eventos é idempotente em retentativas: uma mesma conclusão não gera eventos `ActivityCompleted` duplicados processados (NFR-RES-02).
- 14.5 Todo evento carrega `tenant_id` e identificador de correlação para rastreabilidade (RNF 6).

**Cross-ref:** Req 6, Req 7, Req 11, RNF 3, RNF 6; NFR-RES-02

## 6. Requisitos Não-Funcionais

### RNF 1 — Isolamento por tenant

| Campo | Valor |
|-------|-------|
| **Categoria** | Segurança |
| **Prioridade** | Must |
| **Origem** | NFR-SEG-01; DEC-006; RISCO-T01 |
| **Módulo** | activity-management |

**Descrição:**

Toda leitura e escrita de atividades e tokens de ação é restrita ao `tenant_id` do contexto. Vazamento de dados entre *tenants* é incidente severidade 1. O isolamento deve ser garantido por defesa em profundidade, não dependendo de filtragem manual em consulta isolada.

**Critérios de Aceite:**

- RNF-1.1 Consulta ou escrita sem contexto de *tenant* não retorna nem altera atividades de nenhum *tenant*.
- RNF-1.2 Tentativa de acessar atividade de outro *tenant* é indistinguível de "não encontrado" (sem vazar existência).
- RNF-1.3 Existe teste automatizado em CI que verifica que o *tenant* A não acessa atividades do *tenant* B, via API e via acesso direto a dados.
- RNF-1.4 A falha de qualquer camada isolada de isolamento não resulta em vazamento.

**Cross-ref:** NFR-SEG-01; ADR-0001 (`correlationId-e-tenant-id-propagation`); Req 13

### RNF 2 — Auditoria imutável de toda escrita

| Campo | Valor |
|-------|-------|
| **Categoria** | Auditoria |
| **Prioridade** | Must |
| **Origem** | NFR-AUD-01; RN-024; `.forge/rules/domain/audit-immutability.md` |
| **Módulo** | activity-management |

**Descrição:**

Toda operação de escrita em atividade (criação, atualização, conclusão, cancelamento, reagendamento, exclusão) gera um registro imutável e *append-only* na trilha de auditoria, contendo autor, tipo de entidade, identificador, ação, *delta* e instante.

**Critérios de Aceite:**

- RNF-2.1 Cada escrita em atividade gera exatamente um registro de auditoria.
- RNF-2.2 Registros de auditoria não podem ser alterados nem excluídos pela aplicação (*append-only*).
- RNF-2.3 O registro inclui `user_id` (autor), `entity_type`, `entity_id`, `action`, *delta* e `tenant_id`.
- RNF-2.4 A conclusão via link autenticado do digest registra a correlação entre o token usado e a atividade.

**Cross-ref:** NFR-AUD-01; RN-024; ADR-0003; Req 4, Req 6, Req 7

### RNF 3 — Idempotência da conclusão via token

| Campo | Valor |
|-------|-------|
| **Categoria** | Resiliência |
| **Prioridade** | Must |
| **Origem** | NFR-RES-02; README § 15; MSG-029 |
| **Módulo** | activity-management |

**Descrição:**

A conclusão de atividade via link autenticado do digest deve ser idempotente: múltiplos cliques no mesmo link produzem o mesmo estado final, sem duplicar a conclusão nem o evento publicado.

**Critérios de Aceite:**

- RNF-3.1 Clicar no mesmo link do digest três vezes resulta em exatamente uma atividade concluída.
- RNF-3.2 O segundo e os demais cliques retornam confirmação de sucesso sem alterar `completed_at`.
- RNF-3.3 Uma conclusão efetiva publica `ActivityCompleted` apenas uma vez; retentativas não geram eventos duplicados processados.

**Cross-ref:** NFR-RES-02; ADR-0004 (`outbox-e-idempotencia-eventos-dominio`); Req 7, Req 14, PBT-02

### RNF 4 — Performance da consulta de última atividade

| Campo | Valor |
|-------|-------|
| **Categoria** | Performance |
| **Prioridade** | Must |
| **Origem** | README § 12, § 15, RISK-ACT-02; data-model § 422 (índice obrigatório) |
| **Módulo** | activity-management |

**Descrição:**

A consulta da última atividade concluída por oportunidade (insumo da detecção de estagnação) deve ser eficiente o suficiente para não comprometer o tempo de composição do digest, cujo envio tem alvo de 07:00 no fuso do *tenant*.

**Critérios de Aceite:**

- RNF-4.1 A consulta de última atividade por oportunidade é servida por acesso indexado por (`tenant_id`, `opportunity_id`, `completed_at`).
- RNF-4.2 A composição do digest não excede o SLA de envio por causa da consulta de estagnação.
- RNF-4.3 A consulta em lote de últimas atividades de múltiplas oportunidades do mesmo *tenant* é suportada sem varredura completa de tabela.

**Cross-ref:** RISK-ACT-02; Req 12; data-model § índices obrigatórios

### RNF 5 — Token de ação de uso único e expirável

| Campo | Valor |
|-------|-------|
| **Categoria** | Segurança |
| **Prioridade** | Must |
| **Origem** | README § 14.3, RISK-ACT-01; data-model § digest_action_tokens; VAL-TRD-05 |
| **Módulo** | activity-management |

**Descrição:**

O processamento do token de ação do digest deve aceitar apenas tokens não expirados e ainda não utilizados, e impedir enumeração. A emissão do token pertence ao digest; o activity-management é responsável pela validação no consumo.

**Critérios de Aceite:**

- RNF-5.1 Token com `expires_at` no passado é rejeitado, com orientação ao usuário para acessar o portal.
- RNF-5.2 Token com `used_at` preenchido não executa nova escrita (uso único efetivo).
- RNF-5.3 Token inexistente ou malformado é rejeitado sem revelar a existência de atividades ou de outros tokens (anti-enumeração).
- RNF-5.4 O TTL do token é configurável (sugestão de 24h a 48h, a confirmar — VAL-ACT-02 / VAL-TRD-05).

**Cross-ref:** RISK-ACT-01; ADR-0006 (token de link autenticado); Req 7, PBT-03

### RNF 6 — Observabilidade

| Campo | Valor |
|-------|-------|
| **Categoria** | Observabilidade |
| **Prioridade** | Should |
| **Origem** | README § 15, § 17; NFR-OBS (correlação) |
| **Módulo** | activity-management |

**Descrição:**

As operações do módulo devem ser observáveis via log estruturado com correlação, e métricas operacionais devem permitir detectar anomalias de volume de atividades vencidas.

**Critérios de Aceite:**

- RNF-6.1 Logs estruturados incluem `correlation_id`, `tenant_id`, `activity_id`, `opportunity_id` (quando houver) e ação.
- RNF-6.2 São expostas as métricas `activities_created_total`, `activities_completed_total` e `activities_overdue_total`.
- RNF-6.3 Expiração e uso de tokens de ação do digest são registrados em log.
- RNF-6.4 Existe alerta quando `activities_overdue_total` cresce muito acima da linha de base.

**Cross-ref:** README § 17; ADR-0001; Req 7, Req 11, Req 14

### RNF 7 — Privacidade de dados pessoais em texto livre

| Campo | Valor |
|-------|-------|
| **Categoria** | Privacidade |
| **Prioridade** | Should |
| **Origem** | README § 16 (LGPD marginal); NFR-SEG; `.forge/rules/architecture/security-and-compliance.md` |
| **Módulo** | activity-management |

**Descrição:**

Os campos de texto livre da atividade (título e descrição) podem conter referências a dados pessoais de contatos. O módulo deve aplicar LGPD *by design*, evitando armazenamento desnecessário de PII e exposição indevida em logs e mensagens de erro.

**Critérios de Aceite:**

- RNF-7.1 Dados pessoais não são exigidos nem incentivados nos campos de título e descrição.
- RNF-7.2 Conteúdo de título e descrição não é registrado integralmente em logs operacionais; PII é mascarada quando presente.
- RNF-7.3 Mensagens de erro não expõem conteúdo de atividade de outros usuários ou *tenants*.

**Cross-ref:** README § 16; NFR-PRIV; RN-025 (account-management)

## 7. Property-Based Testing

### PBT-01 — Integridade da máquina de estados de status

**Mapeia para:** Req 4
**Tipo:** State machine

**Propriedade:**

> Para qualquer sequência gerada de tentativas de transição de status a partir de um status inicial válido, o status final só pode ser alcançado por uma cadeia de transições permitidas pela seção 4.1; nenhuma transição inválida (incluindo qualquer saída de `completed` ou `cancelled`) altera o status, e `completed_at` está preenchido se, e somente se, o status final é `completed`.

### PBT-02 — Idempotência da conclusão

**Mapeia para:** Req 6, Req 7, RNF 3
**Tipo:** Idempotência

**Propriedade:**

> Para qualquer atividade e qualquer número N ≥ 1 de requisições de conclusão (pela lista ou pelo mesmo link autenticado do digest), o estado final tem exatamente uma atividade concluída, um único `completed_at` (o da primeira conclusão efetiva) e no máximo um evento `ActivityCompleted` processado.

### PBT-03 — Uso único e anti-enumeração do token

**Mapeia para:** Req 7, RNF 5
**Tipo:** Anti-enumeração

**Propriedade:**

> Para qualquer token gerado (válido, expirado, já usado ou inexistente/malformado), o processamento executa a escrita no máximo uma vez (apenas para token válido e não usado), marca o token como usado após a primeira execução efetiva, e a resposta para token inexistente é indistinguível — em forma e em tempo observável — da resposta para token de atividade inacessível, não permitindo inferir a existência de atividades ou tokens.

### PBT-04 — Invariante de atividade vencida

**Mapeia para:** Req 11, Req 5
**Tipo:** Invariante matemática

**Propriedade:**

> Para qualquer atividade gerada com `due_at` e status arbitrários, avaliada em um instante de referência, a atividade é classificada como vencida se, e somente se, `due_at` é anterior ao instante de referência e o status não é terminal (`completed`/`cancelled`); atividades terminais nunca são vencidas.

### PBT-05 — Saúde do funil após sugestão aceita

**Mapeia para:** Req 9, Req 10
**Tipo:** Invariante

**Propriedade:**

> Para qualquer oportunidade aberta, se a sugestão de próxima atividade (Req 9) é aceita ao concluir sua última atividade futura, então após a operação existe ao menos uma atividade não terminal com `due_at` no futuro vinculada à oportunidade — preservando o invariante de ao menos um follow-up futuro por oportunidade aberta.

## 8. Glossário local

| Termo | Definição | Origem |
|---|---|---|
| Atividade (*Activity*) | Ação comercial registrada com tipo, título, data prevista, responsável, prioridade e status. | ubiquitous-language § BC-04 |
| Tipo de atividade (`activity_type`) | Classificação da atividade: Reunião, Follow-up, Ligação, E-mail, Tarefa. | seção 4.2 |
| `due_at` | Data e hora previstas de vencimento da atividade. | README § 7; data-model § BC-04 |
| `completed_at` | Instante em que a atividade foi concluída. | data-model § BC-04 |
| Vencida (*overdue*) | Atividade com `due_at` no passado e status não terminal. | ubiquitous-language § BC-04 |
| Estagnação (*stagnation*) | Oportunidade aberta sem atividade registrada há mais de 14 dias corridos (RN-028); detectada pelo opportunity-pipeline a partir do dado fornecido por este módulo. | ubiquitous-language § BC-04; RN-028 |
| Visão "Meu dia / Minha semana" (*to_do*) | Lista das atividades do usuário agrupadas em vencidas, de hoje e próximas. | ubiquitous-language § BC-04 |
| Follow-up | Atividade de acompanhamento futura; um follow-up aberto por oportunidade é métrica de saúde do funil. | RF-07; README § 6 |
| Link autenticado (*link_autenticado*) | URL com token incluída no digest para concluir ou reagendar a atividade em um clique, sem login. | ubiquitous-language § BC-06 |
| Token de ação do digest (`digest_action_token`) | Token de uso único e expirável, emitido pelo digest, que autoriza a conclusão/reagendamento de uma atividade. | README § 7; data-model § BC-06 |
| Saúde do funil | Indicador de que cada oportunidade aberta tem ao menos um follow-up futuro agendado. | Req 10 |
| Objeto de valor | Elemento de domínio sem identidade própria, definido por seus atributos. | `.forge/rules/architecture/ddd.md` |

## 9. Fora do escopo do MVP

- Reabertura de atividade `completed` ou `cancelled` (estados terminais no MVP — seção 4.1).
- Emissão e ciclo de vida do token de ação do digest (`digest_action_tokens`): pertence ao módulo digest (BC-06).
- Composição e envio do e-mail do digest (pertence ao digest).
- Criação automática de atividades por workflow (pertence ao workflow-automation — Fase 2).
- Avaliação da regra de estagnação na oportunidade (pertence ao opportunity-pipeline — RN-028).
- Atividades recorrentes ou séries de atividades.
- Atribuição em massa e redistribuição de atividades entre usuários (parcialmente tratada na desativação de usuário em organization-management — MSG-018).
- Notificações in-app de atividade atribuída (pertence ao módulo de notificações — RF-10).
- Anexos e checklists em atividades.

## 10. Referências cruzadas

| Referência | Relação |
|---|---|
| docs/product/frd-nfrd/frd.md § FRD-activity-01..04 (RF-07) | Origem funcional principal |
| docs/product/frd-nfrd/nfrd.md § NFR-SEG-01, NFR-AUD-01, NFR-RES-02 | Origem não-funcional |
| docs/product/trd/trd.md § activity-management; eventos `activity.created/completed/overdue.v1`; scheduler `activity-overdue` | Eventos e processo agendado |
| docs/product/data-model/data-model.md § BC-04 (`activities`), § BC-06 (`digest_action_tokens`) | Modelo de dados de referência |
| docs/product/glossary/ubiquitous-language.md § BC-04, § BC-06 | Linguagem ubíqua |
| docs/product/modules/activity-management/README.md | Visão de módulo, riscos e pontos a validar |
| RN-024 (AuditLog imutável), RN-025 (PII), RN-028 (estagnação) | Regras de negócio aplicáveis |
| ADR-0001, ADR-0003, ADR-0004, ADR-0006 | Decisões arquiteturais referenciadas (a criar/confirmar) |
| VAL-ACT-01, VAL-ACT-02, VAL-TRD-05 | Pontos a validar (fronteira do BC; TTL do token) |
| `.forge/rules/domain/audit-immutability.md`, `.forge/rules/architecture/security-and-compliance.md`, `.forge/rules/architecture/ddd.md` | Regras transversais |
