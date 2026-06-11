# OP — Opportunity Pipeline (Pipeline de Oportunidades)
**Requisitos Funcionais e Não-Funcionais**

- Versão: 0.1.0
- Data: 2026-06-11
- Status: Rascunho para revisão
- Referência pai: docs/product/frd-nfrd/frd.md § RF-06 (Pipeline e oportunidades) e § Anexo de Regras de Negócio (RN-001..RN-028)

## Histórico de Versões

| Versão | Data | Status | Descrição da alteração |
|--------|------|--------|------------------------|
| 0.1.0 | 2026-06-11 | Rascunho para revisão | Criação inicial do documento a partir do README do módulo, FRD/NFRD, azim-product-spec §5.1/§5.3, TRD e data-model. |

## 1. Visão Geral

O módulo **opportunity-pipeline** é o **Core Domain** do Azim CRM (Bounded Context BC-01, deployable `azim-api`). Ele governa o ciclo de vida completo de uma **oportunidade** (negociação comercial em andamento): criação com responsável (owner) obrigatório, movimentação entre estágios do funil, cálculo automático de forecast ponderado, comissão nativa de parceiro com snapshot imutável ao fechar, detecção de estagnação e encerramento como ganha ou perdida.

Este é o **diferencial competitivo confirmado** do produto: tratar parceiro → comissão → oportunidade como cidadão de primeira classe, gap inexistente out-of-the-box em Salesforce e Zoho. O módulo substitui a planilha Excel da Vellus (primeiro tenant), que opera 108 oportunidades sem controle de acesso, sem auditoria e com 59% das oportunidades sem responsável definido — problema que o owner obrigatório (RN-002) ataca diretamente.

O módulo é dono dos dados `opportunities`, `opportunity_stage_transitions` e `opportunity_partner_commissions`, e consome configurações de outros contextos (`stages`, `origin_channels`, `loss_reasons`, contas, parceiros, atividades) sem escrevê-las.

## 2. Escopo

### 2.1 Incluído

- Criação de oportunidade (formulário completo e criação rápida) com owner, conta, estágio e canal de origem.
- Numeração sequencial imutável por tenant no formato `AZ-NNNN`, gerada atomicamente.
- Movimentação de estágio com registro de transição (ator, timestamp) e publicação de evento.
- Máquina de estados de estágio com categorias `open` (aberta), `won` (ganha) e `lost` (perdida).
- Cálculo automático de `valor_total` (modelo Setup + Recorrente) e `forecast_ponderado`.
- Comissão nativa de parceiro por componente, com cálculo de comissão setup, recorrente e total.
- Forecast líquido (forecast ponderado menos comissão ponderada).
- Encerramento como ganha com snapshot imutável de comissão; encerramento como perdida com motivo obrigatório.
- Reabertura de oportunidade ganha ou perdida restrita a papéis de gestão, com re-auditoria.
- Detecção de estagnação (ausência de atividade por ≥ 14 dias corridos) e publicação de evento.
- Vínculo de contatos da oportunidade (1..N, exatamente um principal).
- Visão Kanban por unidade de negócio (BU) e visão em lista com filtros salvos.
- Linha do tempo (timeline) auditável de transições de estágio.
- Publicação de eventos de domínio (`opportunity.*`, `commission.*`) para audit-log, reporting, digest e goal-forecast.

### 2.2 Excluído

- Gestão de contas e contatos (dono: `account-management`).
- Gestão de parceiros e percentuais default (dono: `partner-management`).
- Gestão de atividades e follow-ups (dono: `activity-management`).
- Configuração de estágios, canais de origem e motivos de perda (dono: `organization`).
- Gestão de metas mensais e painel realizado vs meta (dono: `goal-forecast`).
- Composição e envio do digest (dono: `digest`).
- Relatórios analíticos e dashboards complexos (dono: `reporting`).

### 2.3 Fora do escopo do MVP

- Automação de pipeline por workflow orientado a eventos `opportunity.*` (módulo `workflow-automation` — Fase 2).
- Scoring de oportunidade com IA (módulo `ai-intelligence` — Fase 3).
- Multimoeda: campo `currency` em `opportunities` e `commissions` (LAC-05 / VAL-TRD-08 — decisão pendente antes do schema freeze).
- Múltiplos parceiros por oportunidade (rateio N:N). O MVP suporta **1 parceiro por oportunidade**; o schema é preparado para N:N, mas a regra de negócio do MVP é unitária.
- Portal do parceiro com login próprio (parceiro é entidade de dados sem credencial no MVP — DEC-012, RN-021).

## 3. Personas / Atores

| Persona | Papel RBAC | Relação com o módulo |
|---------|-----------|----------------------|
| Vendedor | Vendedor | Cria e edita oportunidades; move estágios; registra comissão; encerra como ganha ou perdida. Não pode reabrir oportunidade fechada. |
| Gestor de BU | Gestor de BU | Acompanha pipeline e forecast da BU; redistribui owners; reabre oportunidades ganhas/perdidas da própria BU; edita comissão pós-ganho conforme política. |
| Tenant Admin | Tenant Admin | Acesso administrativo ao pipeline do tenant; reabre oportunidades de qualquer BU; supervisiona auditoria. |
| Viewer | Viewer | Visualização de Kanban, lista e linha do tempo sem capacidade de escrita. |
| data-migration | Serviço interno | Importa oportunidades em massa via transação única, respeitando numeração e owner obrigatório. |
| StagnationDetectionService | Serviço interno (agendado) | Detecta estagnação e publica `opportunity.stale`; idempotente por execução. |
| digest, goal-forecast, reporting | Serviços consumidores | Leem read models (estagnadas, forecast, funil, comissões) e eventos publicados. |

## 4. Lista canônica de estágios (categorias de ciclo de vida)

O estágio (`stage`) é configurável por BU (DEC-001), mas toda configuração mapeia-se a exatamente uma das três **categorias canônicas** de ciclo de vida (`stage_category`), que governam a máquina de estados e as regras de encerramento.

| Categoria (`stage_category`) | Significado | Regras associadas |
|------------------------------|-------------|-------------------|
| `open` (aberta) | Oportunidade em negociação | Pode mover-se entre estágios `open`, ganhar ou perder; sujeita a detecção de estagnação (RN-028). |
| `won` (ganha) | Oportunidade fechada com sucesso | Gera snapshot imutável de comissão (RN-007); registra `closed_at`; só reabre via gestor (RN-016). |
| `lost` (perdida) | Oportunidade encerrada sem êxito | Exige `motivo_perda` obrigatório (RN-004); registra `closed_at`; só reabre via gestor (RN-016). |

Estágios-semente derivados da Vellus (configuráveis por BU): `Lead` → `Qualificação` → `Proposta Enviada` → `Negociação` → `Fechamento Provável` → `Ganho` (`won`) / `Perdido` (`lost`). A obrigatoriedade de `data_fechamento_esperada` vigora a partir de `Proposta Enviada` (RN-003).

## 5. Requisitos Funcionais

### Req 1 — Criar oportunidade (completa e rápida)

**Como** Vendedor **quero** criar uma oportunidade por formulário completo ou por criação rápida com conta inline **para** registrar uma negociação no pipeline com o mínimo de atrito.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-pipeline-02 (RF-06); azim-product-spec §5.1 |
| **Módulo** | opportunity-pipeline |

**Critérios de Aceite:**

- 1.1 A criação exige `account_id`, `bu_id`, `stage_id`, `origin_channel_id` e `owner_id`; a ausência de qualquer obrigatório resulta em rejeição com erro de validação (HTTP 422).
- 1.2 A criação rápida permite informar a conta inline (delegando a criação/dedupe ao `account-management`) sem sair do fluxo de criação da oportunidade.
- 1.3 `title` é obrigatório e não deve conter dados pessoais identificáveis (PII) de contato em texto livre (LGPD; PII reside em `account-management`).
- 1.4 Toda criação bem-sucedida gera o evento `opportunity.created` e um registro imutável no AuditLog com autor, delta e timestamp.
- 1.5 A oportunidade é sempre criada na categoria `open`.

**Cross-ref:** Req 2, Req 3, Req 4, RNF 4, RNF 6; FRD-pipeline-02; RN-002, RN-008.

### Req 2 — Owner obrigatório em toda oportunidade

**Como** Gestor de BU **quero** que toda oportunidade tenha um responsável definido **para** eliminar oportunidades órfãs (evidência: 59% / 64 de 108 sem responsável na planilha Vellus).

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | RN-002; DEC-010; FRD-pipeline-02 (RF-06) |
| **Módulo** | opportunity-pipeline |

**Critérios de Aceite:**

- 2.1 `owner_id` é obrigatório na criação e na edição; nenhuma oportunidade pode existir sem `owner_id`, sem exceções.
- 2.2 Na criação, `owner_id` é pré-preenchido com o usuário autenticado, podendo ser alterado para outro usuário ativo da mesma BU.
- 2.3 O `owner_id` deve referenciar um usuário ativo com membership na BU da oportunidade; valor inválido é rejeitado com HTTP 422.
- 2.4 A API retorna HTTP 422 com código de erro `OPPORTUNITY_OWNER_REQUIRED` quando `owner_id` está ausente.
- 2.5 Toda troca de owner gera registro no AuditLog com owner anterior e novo.

**Cross-ref:** Req 1; RNF 6; FRD-pipeline-02; RN-002.

### Req 3 — Numeração sequencial AZ-NNNN atômica, única por tenant e imutável

**Como** Tenant Admin **quero** que cada oportunidade receba um número sequencial humano único e imutável **para** ter um identificador rastreável e à prova de colisão, substituindo a fórmula frágil da planilha.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | RN-001; FRD-pipeline-02; FRD-migration-03; data-model §3 (UNIQUE(tenant_id, opportunity_number)) |
| **Módulo** | opportunity-pipeline |

**Critérios de Aceite:**

- 3.1 O número é gerado pelo sistema no formato `AZ-NNNN` (prefixo `AZ-` seguido de sequência numérica), nunca informado pelo usuário.
- 3.2 O número é **único por tenant** — a unicidade é garantida pela restrição `UNIQUE(tenant_id, opportunity_number)`; dois tenants distintos podem ter o mesmo número.
- 3.3 A geração é **atômica**: duas criações concorrentes no mesmo tenant nunca produzem o mesmo número (sem colisão sob concorrência).
- 3.4 O número é **imutável** após a criação; nenhuma operação de edição, reabertura ou migração altera o `opportunity_number`.
- 3.5 Um número nunca é reutilizado, mesmo após exclusão lógica de uma oportunidade.
- 3.6 Na migração da planilha, números preexistentes são preservados quando válidos; oportunidades sem número recebem o próximo número livre do tenant.

**Cross-ref:** Req 1, PBT-01, PBT-02; RISK-PIPE-03; RN-001; ADR de unicidade por tenant (a criar — referência data-model FIND-TRD-001).

### Req 4 — Canal de origem obrigatório e canal "Parceiro" exige parceiro

**Como** Vendedor **quero** registrar o canal de origem da oportunidade **para** permitir análise de desempenho por canal e disparar a regra de comissão de parceiro quando aplicável.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | RN-008; azim-product-spec §5.2; FRD-pipeline-02 |
| **Módulo** | opportunity-pipeline |

**Critérios de Aceite:**

- 4.1 `origin_channel_id` é obrigatório em toda oportunidade; ausência é rejeitada com HTTP 422.
- 4.2 Quando o canal de origem é "Parceiro", `partner_id` torna-se obrigatório; ausência é rejeitada com HTTP 422.
- 4.3 Para canais que não sejam "Parceiro" (ex.: Indicação, Prospecção ativa, Inbound), `partner_id` é opcional.
- 4.4 O canal de origem referencia a lista configurável por BU mantida pelo `organization`; o módulo não escreve nessa lista.

**Cross-ref:** Req 11; RN-008; RN-021; FRD-pipeline-02.

### Req 5 — Estágio obrigatório e transição registrada na linha do tempo

**Como** Vendedor **quero** mover a oportunidade entre estágios do funil **para** refletir o avanço da negociação, com cada transição registrada de forma auditável.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | RN-001 (estágios configuráveis); FRD-pipeline-04; azim-product-spec §5.1 |
| **Módulo** | opportunity-pipeline |

**Critérios de Aceite:**

- 5.1 `stage_id` é obrigatório em toda oportunidade; ausência é rejeitada com HTTP 422.
- 5.2 Cada mudança de estágio publica o evento `opportunity.stage_changed` e insere um registro em `opportunity_stage_transitions` com estágio de origem, estágio de destino, ator e timestamp.
- 5.3 Os registros de `opportunity_stage_transitions` formam uma linha do tempo (timeline) consultável e **imutável** (append-only): sem UPDATE nem DELETE.
- 5.4 A movimentação respeita as validações de transição da máquina de estados (Req 6) e as obrigatoriedades de campo do estágio de destino (Req 9, Req 10).
- 5.5 Toda transição gera registro no AuditLog.

**Cross-ref:** Req 6, Req 9, Req 10, RNF 7; FRD-pipeline-04; RN-001.

### Req 6 — Máquina de estados de estágio por categoria

**Como** Arquiteto de produto **quero** uma máquina de estados que governe transições válidas entre categorias de ciclo de vida **para** garantir consistência do encerramento e da reabertura.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | azim-product-spec §5.1; DEC-001; data-model §3 (stage_category) |
| **Módulo** | opportunity-pipeline |

**Critérios de Aceite:**

- 6.1 Toda oportunidade está em exatamente uma categoria: `open`, `won` ou `lost`.
- 6.2 A partir de `open` são válidas: mover para outro estágio `open`, encerrar como `won` (Req 14) ou encerrar como `lost` (Req 10).
- 6.3 Transições a partir de `won` ou `lost` só são permitidas via reabertura (Req 15), restrita a papéis de gestão.
- 6.4 Encerrar como `won` exige snapshot de comissão (Req 14); encerrar como `lost` exige `motivo_perda` (Req 10).
- 6.5 Transição inválida é rejeitada sem efeito colateral (sem registro de transição, sem evento, sem alteração de estado).

**Cross-ref:** Req 5, Req 10, Req 14, Req 15, PBT-08; RN-016.

### Req 7 — Probabilidade herdada do estágio, editável por oportunidade

**Como** Vendedor **quero** que a probabilidade de fechamento seja preenchida automaticamente pelo estágio, mas ajustável caso a caso **para** refletir a realidade da negociação no forecast.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Should |
| **Origem** | azim-product-spec §5.1; RN-006; ubiquitous-language (probabilidade) |
| **Módulo** | opportunity-pipeline |

**Critérios de Aceite:**

- 7.1 Ao criar a oportunidade ou mover de estágio, `probabilidade` recebe por padrão o valor default configurado no estágio (0–100).
- 7.2 O usuário pode sobrescrever a `probabilidade` da oportunidade individualmente, dentro do intervalo válido [0, 100].
- 7.3 A `probabilidade` é insumo direto do cálculo de `forecast_ponderado` (Req 8); qualquer alteração recalcula o forecast.
- 7.4 Valor fora de [0, 100] é rejeitado com HTTP 422.

**Cross-ref:** Req 8, PBT-04; RN-006.

### Req 8 — Cálculo de valor_total e forecast_ponderado em centavos

**Como** Gestor de BU **quero** que valor total do contrato e forecast ponderado sejam calculados automaticamente **para** ter previsão confiável sem digitação manual sujeita a erro.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | RN-005; RN-006; RN-015; DEC-011; azim-product-spec §5.1 |
| **Módulo** | opportunity-pipeline |

**Critérios de Aceite:**

- 8.1 `valor_setup`, `valor_mensal` e `valor_total` são representados em **centavos inteiros** de BRL; `duracao_meses` é inteiro de meses.
- 8.2 `valor_total = valor_setup + valor_mensal × duracao_meses` (Total Contract Value, TCV); nunca é digitado diretamente — é sempre derivado.
- 8.3 `forecast_ponderado = valor_total × probabilidade / 100`, em centavos inteiros.
- 8.4 `duracao_meses` é obrigatório quando `valor_mensal > 0`.
- 8.5 O recálculo de `valor_total` e `forecast_ponderado` é automático a cada alteração de `valor_setup`, `valor_mensal`, `duracao_meses` ou `probabilidade`.
- 8.6 Arredondamento de qualquer divisão segue NBR 5891 (ToEven / arredondamento bancário); nunca usar tipo de ponto flutuante para cálculo monetário.

**Cross-ref:** Req 7, Req 13, PBT-03, PBT-04, RNF 11; RN-005, RN-006, RN-015.

### Req 9 — Data de fechamento esperada obrigatória a partir de "Proposta Enviada"

**Como** Gestor de BU **quero** que a data de fechamento esperada seja obrigatória a partir do estágio "Proposta Enviada" **para** acompanhar previsibilidade e acionar cobrança quando a data vencer.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | RN-003; FRD-pipeline-04; J-01 |
| **Módulo** | opportunity-pipeline |

**Critérios de Aceite:**

- 9.1 A transição para "Proposta Enviada" ou estágios posteriores (Negociação, Fechamento Provável) é bloqueada se `data_fechamento_esperada` estiver ausente (HTTP 422).
- 9.2 Em estágios anteriores a "Proposta Enviada", `data_fechamento_esperada` é opcional.
- 9.3 Uma oportunidade aberta cuja `data_fechamento_esperada` esteja no passado é marcada como **vencida** e recebe destaque visual no Kanban e na lista.
- 9.4 Oportunidades com data de fechamento vencida são expostas ao `digest` como pendência (critério de inclusão no digest diário).

**Cross-ref:** Req 5, Req 18, Req 19; FRD-pipeline-04; RN-003; RN-011 (digest).

### Req 10 — Encerrar como perdida com motivo obrigatório

**Como** Gestor de BU **quero** que toda perda exija um motivo selecionado **para** analisar causas de perda por canal e BU.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | RN-004; FRD-pipeline-07 |
| **Módulo** | opportunity-pipeline |

**Critérios de Aceite:**

- 10.1 Mover a oportunidade para um estágio de categoria `lost` exige `loss_reason_id` selecionado da lista configurável da BU.
- 10.2 A ausência de motivo de perda é **bloqueante**, sem possibilidade de contornar (HTTP 422).
- 10.3 O encerramento como perdida registra `closed_at`, publica o evento `opportunity.lost` e gera registro no AuditLog.
- 10.4 Após encerramento como perdida, a oportunidade só volta a `open` via reabertura (Req 15).

**Cross-ref:** Req 6, Req 15; FRD-pipeline-07; RN-004.

### Req 11 — Vínculo de comissão de parceiro por componente

**Como** Vendedor **quero** registrar a comissão do parceiro por componente (setup e recorrente) ou por valor fixo **para** tornar a remuneração do parceiro rastreável já na oportunidade.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | azim-product-spec §5.3; RN-008; FRD-pipeline-05; DEC-002 |
| **Módulo** | opportunity-pipeline |

**Critérios de Aceite:**

- 11.1 O vínculo de comissão (`OpportunityPartnerCommission`) registra `partner_id`, `papel` (tipado: Indicador, Revendedor, Distribuidor, Integrador), `pct_setup`, `pct_recorrente`, `valor_fixo` (alternativa a percentuais) e `meses_comissionados`.
- 11.2 Ao vincular o parceiro, `pct_setup` e `pct_recorrente` são pré-preenchidos com os percentuais default do parceiro (consultados no `partner-management`), podendo ser ajustados na oportunidade.
- 11.3 `meses_comissionados` tem como default `duracao_meses`, podendo ser ajustado.
- 11.4 `valor_fixo` é uma alternativa mutuamente excludente aos percentuais: quando informado, prevalece sobre `pct_setup`/`pct_recorrente` no cálculo (Req 12).
- 11.5 No MVP, no máximo **1 parceiro por oportunidade**; o schema é preparado para N:N, mas a regra do MVP é unitária.
- 11.6 Toda criação ou edição do vínculo de comissão (antes do snapshot) gera registro no AuditLog e publica `commission.calculated`.

**Cross-ref:** Req 4, Req 12, Req 14; RN-008; RN-026; DEC-002; azim-product-spec §5.3.

### Req 12 — Cálculo de comissão por componente

**Como** Gestor de BU **quero** que a comissão de setup, recorrente e total seja calculada automaticamente **para** ter visibilidade da comissão projetada sem cálculo manual.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | RN-026; azim-product-spec §5.3; FRD-pipeline-05 |
| **Módulo** | opportunity-pipeline |

**Critérios de Aceite:**

- 12.1 `comissao_setup = valor_setup × pct_setup`, em centavos inteiros.
- 12.2 `comissao_recorrente = valor_mensal × meses_comissionados × pct_recorrente`, em centavos inteiros.
- 12.3 `comissao_total = comissao_setup + comissao_recorrente`; quando `valor_fixo` é usado, `comissao_total = valor_fixo`.
- 12.4 Todos os valores de comissão são em centavos inteiros; o arredondamento segue NBR 5891 (ToEven); proibido ponto flutuante no cálculo.
- 12.5 A comissão é recalculada automaticamente a cada alteração de valores da oportunidade ou de percentuais, **enquanto a oportunidade não for ganha** (antes do snapshot).
- 12.6 A comissão projetada de uma oportunidade aberta é exposta ao `reporting` e ao `partner-management` (comissão projetada).

**Cross-ref:** Req 11, Req 13, Req 14, PBT-05, RNF 11; RN-026; DEC-002.

### Req 13 — Forecast líquido (forecast ponderado menos comissão ponderada)

**Como** Gestor de BU **quero** ver o forecast líquido da oportunidade **para** avaliar o resultado previsto já descontada a comissão do parceiro.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Should |
| **Origem** | azim-product-spec §5.3 ("forecast líquido = forecast ponderado − comissão ponderada") |
| **Módulo** | opportunity-pipeline |

**Critérios de Aceite:**

- 13.1 `comissao_ponderada = comissao_total × probabilidade / 100`, em centavos inteiros.
- 13.2 `forecast_liquido = forecast_ponderado − comissao_ponderada`, em centavos inteiros.
- 13.3 O forecast líquido é disponibilizado em relatórios e na visão do gestor (read model), não substituindo o `forecast_ponderado`.
- 13.4 Quando não há comissão vinculada, `forecast_liquido = forecast_ponderado`.

**Cross-ref:** Req 8, Req 12, PBT-06, RNF 11; azim-product-spec §5.3.

### Req 14 — Encerrar como ganha com snapshot imutável de comissão

**Como** Gestor de BU **quero** que ao ganhar a oportunidade a comissão seja congelada em um snapshot imutável **para** ter base confiável e auditável para pagamento do parceiro.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | RN-007; RN-022; DEC-002; FRD-pipeline-06; azim-product-spec §5.3; TOBJ-05 |
| **Módulo** | opportunity-pipeline |

**Critérios de Aceite:**

- 14.1 Mover a oportunidade para um estágio de categoria `won` cria um registro `OpportunityPartnerCommission` com `is_snapshot = TRUE`, `snapshot_at` e os percentuais, `meses_comissionados` e `comissao_calculada` **vigentes no momento do fechamento**.
- 14.2 Após `is_snapshot = TRUE`, o registro **não pode ser editado nem excluído por nenhum papel**; a imutabilidade é garantida no banco por policy/trigger (sem UPDATE/DELETE).
- 14.3 Alterações posteriores nos percentuais default do parceiro **não** alteram o snapshot já criado (RN-022).
- 14.4 A operação é atômica: criar snapshot, registrar transição `won` e atualizar `stage_category = won` + `closed_at` ocorrem na mesma transação; falha em qualquer passo provoca rollback total.
- 14.5 O encerramento publica `opportunity.won` e `commission.snapshot_created`, e gera registros no AuditLog para ambos.
- 14.6 Existe no máximo um snapshot ativo por oportunidade (`UNIQUE(opportunity_id) WHERE is_snapshot = TRUE`), prevenindo duplicação.

> **Pendência VAL-07 — Ganho com comissão em branco (alerta × bloqueio):** ainda não está decidido se ganhar uma oportunidade de canal "Parceiro" **sem** comissão preenchida deve gerar apenas um **alerta** (permitindo o ganho com snapshot de comissão zero) ou um **bloqueio** (impedindo o ganho até preencher a comissão). Esta decisão impacta diretamente os critérios 14.1 e a relação com RN-008. **Decisão pendente — escalar para Produto antes de Aprovado para desenvolvimento.**

**Cross-ref:** Req 6, Req 11, Req 12, Req 15, PBT-07, PBT-11, RNF 5, RNF 6; RN-007, RN-022; DEC-002; ADR de snapshot imutável (a criar); RISK-PIPE-01; **VAL-07 (pendente)**.

### Req 15 — Reabertura de oportunidade fechada restrita a gestão com re-auditoria

**Como** Gestor de BU **quero** reabrir uma oportunidade ganha ou perdida da minha BU **para** corrigir um encerramento equivocado, preservando o histórico original.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | RN-016; FRD-pipeline-08; azim-product-spec §5.3 |
| **Módulo** | opportunity-pipeline |

**Critérios de Aceite:**

- 15.1 Apenas Tenant Admin e Gestor de BU (na BU da oportunidade) podem reabrir; Vendedor e Viewer não têm essa permissão (HTTP 403).
- 15.2 A reabertura move a oportunidade de `won`/`lost` de volta para `open`, registra transição e publica `opportunity.reopened`.
- 15.3 O snapshot de comissão original criado no ganho é **preservado** (não é apagado nem alterado) — a imutabilidade do snapshot persiste mesmo após reabertura.
- 15.4 A reabertura gera registro no AuditLog identificando o ator, o estado anterior e a justificativa, re-auditando a oportunidade.
- 15.5 Uma oportunidade reaberta volta a ser elegível à detecção de estagnação (Req 17).

**Cross-ref:** Req 6, Req 14, RNF 4, RNF 6; RN-016; RN-007.

### Req 16 — Contatos da oportunidade (1..N com exatamente um principal)

**Como** Vendedor **quero** vincular um ou mais contatos à oportunidade, com um deles marcado como principal **para** saber com quem conduzir a negociação.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Should |
| **Origem** | FRD RF-06 (obrigatoriedades); azim-product-spec §5.1; README §7 |
| **Módulo** | opportunity-pipeline |

**Critérios de Aceite:**

- 16.1 A oportunidade pode vincular de 1 a N contatos, referenciando contatos da conta mantidos pelo `account-management` (o módulo não escreve dados de contato).
- 16.2 Exatamente um contato vinculado é marcado como **principal**; nunca zero nem mais de um.
- 16.3 Ao remover o contato principal, é obrigatório promover outro contato a principal enquanto houver ao menos um vínculo.
- 16.4 Dados pessoais (PII) do contato não são duplicados em texto livre na oportunidade nem expostos em logs.

**Cross-ref:** Req 1, RNF 3; RN-025; account-management.

### Req 17 — Última atividade automática e detecção de estagnação

**Como** Gestor de BU **quero** que a oportunidade seja marcada como estagnada após 14 dias sem atividade **para** acionar a equipe antes que a negociação esfrie.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | RN-028; FRD-pipeline-01; FRD-digest-02; azim-product-spec §5.1 |
| **Módulo** | opportunity-pipeline |

**Critérios de Aceite:**

- 17.1 `ultima_atividade_em` é atualizado automaticamente a partir da última atividade concluída vinculada à oportunidade (dado consultado no `activity-management`).
- 17.2 Uma oportunidade de categoria `open` sem atividade concluída há ≥ 14 dias corridos é considerada **estagnada** (`stale`).
- 17.3 A condição de estagnação é avaliada para sinalização visual no Kanban e na lista, e é exposta ao `digest`.
- 17.4 O serviço de detecção publica o evento `opportunity.stale` para as oportunidades estagnadas.
- 17.5 A detecção é **idempotente**: reexecução no mesmo período não duplica o evento `opportunity.stale` para a mesma oportunidade.
- 17.6 Oportunidades de categoria `won` ou `lost` não são candidatas a estagnação.

**Cross-ref:** Req 6, Req 18, PBT-09, RNF 9; RN-028; activity-management; digest.

### Req 18 — Kanban por BU com somas e desempenho fluido

**Como** Vendedor **quero** um Kanban por BU com colunas de estágio e arrastar-e-soltar **para** operar o pipeline como cockpit diário com fluidez.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-pipeline-01; RF-06; NFR-PERF-02; azim-product-spec §6 (RF-06) |
| **Módulo** | opportunity-pipeline |

**Critérios de Aceite:**

- 18.1 O Kanban exibe colunas por estágio da BU, cada card representando uma oportunidade aberta.
- 18.2 Cada coluna exibe a soma de `valor_total` e a soma de `forecast_ponderado` das oportunidades naquele estágio.
- 18.3 Arrastar-e-soltar um card entre colunas dispara a movimentação de estágio (Req 5), aplicando todas as validações de transição.
- 18.4 Cards de oportunidades estagnadas (Req 17) e com data de fechamento vencida (Req 9) recebem destaque visual.
- 18.5 O Kanban é restrito por isolamento de tenant e por BU do usuário; não exibe oportunidades de outro tenant nem de BU sem membership.
- 18.6 O carregamento atende ao SLO de desempenho mesmo com volumes elevados (ver RNF 1).

**Cross-ref:** Req 5, Req 9, Req 17, RNF 1, RNF 3; FRD-pipeline-01; NFR-PERF-02.

### Req 19 — Lista de oportunidades com filtros salvos e linha do tempo

**Como** Vendedor **quero** uma visão em lista com filtros salvos e a linha do tempo de cada oportunidade **para** trabalhar recortes recorrentes e auditar o histórico.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Should |
| **Origem** | FRD-pipeline-09; RF-06; FRD-pipeline-04 (linha do tempo) |
| **Módulo** | opportunity-pipeline |

**Critérios de Aceite:**

- 19.1 A lista permite filtrar por owner, canal de origem, parceiro, estágio, data de fechamento e oportunidades estagnadas.
- 19.2 O usuário pode salvar combinações de filtros e reutilizá-las.
- 19.3 A visão de detalhe da oportunidade exibe a linha do tempo (timeline) com todas as transições de estágio, ator e timestamp (a partir de `opportunity_stage_transitions`).
- 19.4 A lista respeita isolamento de tenant e visibilidade por BU do usuário.

**Cross-ref:** Req 5, Req 18, RNF 3; FRD-pipeline-09.

### Req 20 — Publicação de eventos de domínio

**Como** Arquiteto de produto **quero** que toda mudança relevante publique eventos de domínio **para** alimentar auditoria, relatórios, digest, forecast e automações futuras.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | TRD §7.2/§9.3; README §10; azim-product-spec §11 |
| **Módulo** | opportunity-pipeline |

**Critérios de Aceite:**

- 20.1 O módulo publica os eventos: `opportunity.created`, `opportunity.stage_changed`, `opportunity.won`, `opportunity.lost`, `opportunity.stale`, `opportunity.reopened`, `commission.calculated`, `commission.snapshot_created`.
- 20.2 Os nomes de evento seguem o padrão versionado no passado (ex.: `opportunity.won.v1`), conforme convenção do TRD.
- 20.3 Cada evento carrega `tenant_id`, identificador da oportunidade, ator e timestamp, sem expor PII de contato.
- 20.4 A publicação dos eventos que acompanham uma escrita é consistente com a transação que a originou (sem evento sem persistência correspondente).

**Cross-ref:** Req 1, Req 5, Req 10, Req 14, Req 15, Req 17, RNF 6, RNF 10; TRD §9.3.

## 6. Requisitos Não-Funcionais

### RNF 1 — Desempenho do Kanban com alto volume

| Campo | Valor |
|-------|-------|
| **Categoria** | Performance |
| **Prioridade** | Must |
| **Origem** | NFR-PERF-02; RISCO-T03; KPI-05 |
| **Módulo** | opportunity-pipeline |

**Descrição:**

O carregamento da visão Kanban por BU deve permanecer fluido mesmo com centenas a milhares de oportunidades por tenant, incluindo o cálculo das somas de `valor_total` e `forecast_ponderado` por estágio.

**Critérios de Aceite:**

- RNF-1.1 Carregamento do endpoint de Kanban com p95 ≤ 2.000 ms em carga nominal e também em teste de carga com 500+ oportunidades por tenant (alvo de teste estendido até 2.000 oportunidades).
- RNF-1.2 Índice composto `(tenant_id, bu_id, stage_id)` disponível como pré-requisito de consulta.
- RNF-1.3 Paginação ou carregamento incremental no Kanban para evitar materializar todo o conjunto de uma vez.
- RNF-1.4 Alerta de monitoramento se p95 do Kanban exceder 2.000 ms por mais de 5 minutos em produção.

**Cross-ref:** Req 18; NFR-PERF-02.

### RNF 2 — Latência de criação, edição e movimentação de estágio

| Campo | Valor |
|-------|-------|
| **Categoria** | Performance |
| **Prioridade** | Should |
| **Origem** | NFR-PERF-03 |
| **Módulo** | opportunity-pipeline |

**Descrição:**

As operações de criação, edição e arrastar-e-soltar de estágio devem responder dentro do SLO definido, incluindo a gravação do AuditLog no tempo de resposta.

**Critérios de Aceite:**

- RNF-2.1 Endpoints de criação e edição de oportunidade e de movimentação de estágio com p95 ≤ 500 ms a 50 RPS.
- RNF-2.2 A gravação do AuditLog está incluída no tempo medido.

**Cross-ref:** Req 1, Req 5; NFR-PERF-03.

### RNF 3 — Isolamento multi-tenant com defesa em profundidade

| Campo | Valor |
|-------|-------|
| **Categoria** | Segurança |
| **Prioridade** | Must |
| **Origem** | NFR-SEG-01; DEC-006; ADR-0001 |
| **Módulo** | opportunity-pipeline |

**Descrição:**

Todo acesso a `opportunities`, `opportunity_stage_transitions` e `opportunity_partner_commissions` deve ser isolado por `tenant_id` em múltiplas camadas independentes, de modo que a falha de uma camada não cause vazamento. Vazamento entre tenants é incidente sev-1.

**Critérios de Aceite:**

- RNF-3.1 Nenhuma consulta ou operação do módulo retorna ou afeta dados de outro tenant; toda tabela própria é filtrada por `tenant_id` (RLS Postgres + filtro de aplicação).
- RNF-3.2 Testes de isolamento em CI verificam que tenant A não acessa recursos de tenant B via API nem via SQL sem contexto de tenant; falha bloqueia o merge (KPI-06).
- RNF-3.3 Tentativa de violação de RLS gera alerta de monitoramento em produção.

**Cross-ref:** Req 18, Req 19, PBT-10; NFR-SEG-01; ADR-0001.

### RNF 4 — RBAC verificado em todo endpoint do módulo

| Campo | Valor |
|-------|-------|
| **Categoria** | Segurança |
| **Prioridade** | Must |
| **Origem** | NFR-SEG-03; RN-016; azim-product-spec §matriz RBAC |
| **Módulo** | opportunity-pipeline |

**Descrição:**

Toda rota do módulo que lê ou modifica oportunidades, transições ou comissões deve verificar explicitamente o papel RBAC do usuário, confirmado pela RLS no banco. Operações sensíveis (reabertura, edição de comissão pós-ganho) têm restrição de papel específica.

**Critérios de Aceite:**

- RNF-4.1 Nenhuma rota de negócio do módulo sem verificação de RBAC.
- RNF-4.2 Reabertura (Req 15) e edição de comissão pós-ganho restritas a Tenant Admin e Gestor de BU; Vendedor e Viewer recebem HTTP 403.
- RNF-4.3 Todas as combinações papel × capacidade do módulo cobertas por testes automatizados (200/403 esperado verificado).

**Cross-ref:** Req 14, Req 15; NFR-SEG-03; RN-016.

### RNF 5 — Imutabilidade do snapshot de comissão

| Campo | Valor |
|-------|-------|
| **Categoria** | Integridade |
| **Prioridade** | Must |
| **Origem** | RN-007; RN-022; DEC-002; NFR-AUD-01; RISK-PIPE-01 |
| **Módulo** | opportunity-pipeline |

**Descrição:**

Após `is_snapshot = TRUE`, um registro de `opportunity_partner_commissions` não pode ser alterado nem excluído por nenhum papel; a imutabilidade é garantida no banco, não apenas na aplicação.

**Critérios de Aceite:**

- RNF-5.1 Policy/trigger no banco bloqueia UPDATE e DELETE em registros com `is_snapshot = TRUE`; tentativa retorna exceção do Postgres.
- RNF-5.2 Restrição `UNIQUE(opportunity_id) WHERE is_snapshot = TRUE` impede mais de um snapshot por oportunidade.
- RNF-5.3 Teste de regressão verifica que alteração de percentuais do parceiro após o ganho não altera o snapshot.
- RNF-5.4 O snapshot é criado dentro da transação de fechamento como ganho (atomicidade), com rollback total em falha.

**Cross-ref:** Req 14, PBT-07, PBT-11; RN-007, RN-022; DEC-002.

### RNF 6 — AuditLog imutável para toda escrita do módulo

| Campo | Valor |
|-------|-------|
| **Categoria** | Auditoria |
| **Prioridade** | Must |
| **Origem** | RN-024; NFR-AUD-01; `.forge/rules/domain/audit-immutability.md` |
| **Módulo** | opportunity-pipeline |

**Descrição:**

Toda operação de escrita em oportunidade, transição de estágio, comissão e encerramento gera um registro imutável (append-only) no AuditLog, com autor, `entity_type`, `entity_id`, `action`, delta JSON, timestamp e `tenant_id`.

**Critérios de Aceite:**

- RNF-6.1 100% das escritas de negócio do módulo registradas no AuditLog (exatamente 1 registro por operação).
- RNF-6.2 Imutabilidade do AuditLog garantida no banco via REVOKE UPDATE/DELETE/TRUNCATE + trigger; tentativa de alteração retorna exceção.
- RNF-6.3 PII de contato é mascarada ou omitida no delta JSON antes da gravação.

**Cross-ref:** Req 1, Req 2, Req 5, Req 10, Req 14, Req 15; NFR-AUD-01; RN-024, RN-025.

### RNF 7 — Histórico de transições append-only

| Campo | Valor |
|-------|-------|
| **Categoria** | Auditoria |
| **Prioridade** | Must |
| **Origem** | data-model §3; README §15/§17 |
| **Módulo** | opportunity-pipeline |

**Descrição:**

`opportunity_stage_transitions` é um histórico imutável: cada transição é inserida e nunca atualizada ou removida, formando a linha do tempo auditável da oportunidade.

**Critérios de Aceite:**

- RNF-7.1 Operações sobre `opportunity_stage_transitions` são exclusivamente INSERT; sem UPDATE/DELETE.
- RNF-7.2 Cada registro contém estágio de origem, estágio de destino, ator e timestamp.

**Cross-ref:** Req 5, Req 19; RN-024.

### RNF 8 — Retenção indefinida de dados de auditoria e snapshot

| Campo | Valor |
|-------|-------|
| **Categoria** | Auditoria |
| **Prioridade** | Should |
| **Origem** | NFR-AUD-03; `.forge/rules/domain/audit-immutability.md`; PTV-04 |
| **Módulo** | opportunity-pipeline |

**Descrição:**

Snapshots de comissão e registros de transição não possuem TTL nem purge automático; a retenção é indefinida, exceto por obrigação legal explícita (LGPD), que exige role administrativo separado e aprovação dupla.

**Critérios de Aceite:**

- RNF-8.1 Nenhum job de purge automático configurado para `opportunity_partner_commissions` (snapshot) nem `opportunity_stage_transitions`.
- RNF-8.2 Role `app` não possui DELETE nessas tabelas.

**Cross-ref:** Req 14; NFR-AUD-03; PTV-04.

### RNF 9 — Idempotência e resiliência da detecção de estagnação

| Campo | Valor |
|-------|-------|
| **Categoria** | Resiliência |
| **Prioridade** | Must |
| **Origem** | README §15/§19 (RISK-PIPE-02); RN-028; TRD §scheduler |
| **Módulo** | opportunity-pipeline |

**Descrição:**

O serviço de detecção de estagnação deve ser idempotente e observável: reexecuções não duplicam `opportunity.stale` e a não execução é alertada.

**Critérios de Aceite:**

- RNF-9.1 Reexecução da detecção no mesmo período não publica `opportunity.stale` duplicado para a mesma oportunidade.
- RNF-9.2 Alerta de monitoramento se o job de detecção não executar no intervalo esperado.

**Cross-ref:** Req 17, PBT-09; RN-028; RISK-PIPE-02.

### RNF 10 — Observabilidade do módulo

| Campo | Valor |
|-------|-------|
| **Categoria** | Observabilidade |
| **Prioridade** | Should |
| **Origem** | README §17; NFR-OBS; ADR-0001 |
| **Módulo** | opportunity-pipeline |

**Descrição:**

Operações do módulo devem ser rastreáveis por logs estruturados, métricas e traces, com `correlation_id` e `tenant_id` propagados, sem expor PII.

**Critérios de Aceite:**

- RNF-10.1 Logs estruturados incluem `correlation_id`, `tenant_id`, `bu_id`, `opportunity_id`, estágio e ação.
- RNF-10.2 Métricas expostas: `opportunities_created_total`, `opportunities_won_total`, `opportunities_lost_total`, `opportunities_stale_total`.
- RNF-10.3 Trace presente na operação de ganho que gera snapshot de comissão.
- RNF-10.4 Nenhum log ou trace contém PII de contato.

**Cross-ref:** Req 14, Req 20; ADR-0001; RN-025.

### RNF 11 — Integridade monetária em centavos

| Campo | Valor |
|-------|-------|
| **Categoria** | Integridade |
| **Prioridade** | Must |
| **Origem** | RN-015; DEC-011; `.forge/rules/domain/money-as-cents.md`; `.forge/rules/domain/nbr-5891-rounding.md` |
| **Módulo** | opportunity-pipeline |

**Descrição:**

Todos os valores monetários (setup, mensal, total, forecast, forecast líquido, comissões) são armazenados, calculados e transmitidos como centavos inteiros; arredondamento segue NBR 5891 (ToEven); ponto flutuante é proibido em cálculo monetário.

**Critérios de Aceite:**

- RNF-11.1 Campos monetários representados em centavos inteiros (referência conceitual `BIGINT`/`long`); `decimal` apenas em camada de apresentação.
- RNF-11.2 Nenhum cálculo monetário usa `float`/`double`.
- RNF-11.3 Toda divisão (forecast, comissão ponderada) arredonda por NBR 5891 ToEven.

**Cross-ref:** Req 8, Req 12, Req 13; RN-015; DEC-011.

### RNF 12 — Disponibilidade Tier 1 do pipeline

| Campo | Valor |
|-------|-------|
| **Categoria** | Disponibilidade |
| **Prioridade** | Should |
| **Origem** | NFR-DISP-01 |
| **Módulo** | opportunity-pipeline |

**Descrição:**

O Kanban e as operações de pipeline são críticos para a operação comercial diária e devem manter a disponibilidade mínima mensal definida para módulos Tier 1.

**Critérios de Aceite:**

- RNF-12.1 Disponibilidade mensal do endpoint de Kanban e das operações de pipeline conforme meta Tier 1 do NFR-DISP-01.

**Cross-ref:** Req 18; NFR-DISP-01.

## 7. Property-Based Testing

### PBT-01 — Unicidade e atomicidade do opportunity_number por tenant

**Mapeia para:** Req 3
**Tipo:** Atomicidade / Anti-colisão concorrente

**Propriedade:**

> Para qualquer conjunto de N criações concorrentes de oportunidade dentro de um mesmo tenant, os N números `AZ-NNNN` gerados são distintos dois a dois, e a restrição `UNIQUE(tenant_id, opportunity_number)` nunca é violada.

### PBT-02 — Imutabilidade do opportunity_number

**Mapeia para:** Req 3
**Tipo:** Invariante

**Propriedade:**

> Para qualquer oportunidade e qualquer sequência de operações de edição, movimentação de estágio, encerramento ou reabertura, o valor de `opportunity_number` ao final é idêntico ao gerado na criação.

### PBT-03 — Invariante do valor_total (TCV)

**Mapeia para:** Req 8
**Tipo:** Invariante matemática

**Propriedade:**

> Para quaisquer `valor_setup ≥ 0`, `valor_mensal ≥ 0` e `duracao_meses ≥ 0` (centavos/meses inteiros), `valor_total = valor_setup + valor_mensal × duracao_meses`, sempre em centavos inteiros e sem perda de precisão.

### PBT-04 — Invariante do forecast_ponderado

**Mapeia para:** Req 7, Req 8
**Tipo:** Invariante matemática

**Propriedade:**

> Para qualquer `valor_total ≥ 0` e qualquer `probabilidade` em [0, 100], `forecast_ponderado = round_NBR5891(valor_total × probabilidade / 100)`, com `0 ≤ forecast_ponderado ≤ valor_total`.

### PBT-05 — Invariante da comissão por componente

**Mapeia para:** Req 11, Req 12
**Tipo:** Invariante matemática

**Propriedade:**

> Para quaisquer valores e percentuais válidos, `comissao_setup = round_NBR5891(valor_setup × pct_setup)`, `comissao_recorrente = round_NBR5891(valor_mensal × meses_comissionados × pct_recorrente)` e `comissao_total = comissao_setup + comissao_recorrente`; quando `valor_fixo` é usado, `comissao_total = valor_fixo`. Todos os resultados em centavos inteiros não negativos.

### PBT-06 — Invariante do forecast líquido

**Mapeia para:** Req 13
**Tipo:** Invariante matemática

**Propriedade:**

> Para qualquer oportunidade, `forecast_liquido = forecast_ponderado − round_NBR5891(comissao_total × probabilidade / 100)` e `forecast_liquido ≤ forecast_ponderado`; na ausência de comissão, `forecast_liquido = forecast_ponderado`.

### PBT-07 — Imutabilidade do snapshot de comissão

**Mapeia para:** Req 14
**Tipo:** Idempotência / Imutabilidade

**Propriedade:**

> Para qualquer snapshot com `is_snapshot = TRUE`, qualquer sequência de tentativas de UPDATE/DELETE deixa o registro inalterado (operações rejeitadas), e existe no máximo um snapshot ativo por oportunidade.

### PBT-08 — Máquina de estados de estágio

**Mapeia para:** Req 6
**Tipo:** State machine

**Propriedade:**

> Para qualquer sequência de transições aplicada a uma oportunidade, somente transições válidas (a partir de `open`: outro `open`, `won` ou `lost`; a partir de `won`/`lost`: apenas reabertura para `open`) alteram o estado; transições inválidas são rejeitadas sem efeito colateral (sem registro de transição, evento ou mudança de categoria).

### PBT-09 — Idempotência da detecção de estagnação

**Mapeia para:** Req 17
**Tipo:** Idempotência

**Propriedade:**

> Para qualquer conjunto de oportunidades e qualquer número de reexecuções da detecção no mesmo período, cada oportunidade estagnada gera no máximo um evento `opportunity.stale` por período de detecção.

### PBT-10 — Isolamento por tenant

**Mapeia para:** Req 18, Req 19, RNF 3
**Tipo:** Anti-enumeração / Isolamento

**Propriedade:**

> Para qualquer par de tenants distintos A e B e qualquer consulta executada no contexto de A, nenhum registro de oportunidade, transição ou comissão pertencente a B é retornado ou afetado.

### PBT-11 — Snapshot preservado após alteração de percentuais do parceiro

**Mapeia para:** Req 14
**Tipo:** Invariante

**Propriedade:**

> Para qualquer oportunidade ganha com snapshot S, qualquer alteração posterior nos percentuais default do parceiro ou no vínculo de comissão deixa os valores de S inalterados (RN-022).

## 8. Glossário local

| Termo | Definição |
|-------|-----------|
| Oportunidade (Opportunity) | Negociação comercial em andamento; aggregate central com owner, conta, parceiro opcional, estágio, valor e comissão. |
| opportunity_number | Número sequencial humano `AZ-NNNN`, único por tenant e imutável (RN-001). |
| owner | Usuário responsável obrigatório pela oportunidade; usuário ativo com membership na BU (RN-002). |
| Estágio (stage) | Fase configurável do funil por BU, com probabilidade default e categoria de ciclo de vida. |
| Categoria de estágio (stage_category) | `open` (aberta), `won` (ganha) ou `lost` (perdida). |
| valor_setup / valor_mensal | Componentes do contrato (implantação e recorrente mensal), em centavos inteiros. |
| valor_total (TCV) | Total Contract Value: `valor_setup + valor_mensal × duracao_meses` (RN-005). |
| probabilidade | Percentual de fechamento [0–100], default do estágio, editável por oportunidade. |
| forecast_ponderado | `valor_total × probabilidade / 100`, em centavos inteiros (RN-006). |
| forecast_liquido | `forecast_ponderado − comissão ponderada` (azim-product-spec §5.3). |
| OpportunityPartnerCommission | Vínculo de comissão de parceiro por componente; vira snapshot imutável ao ganhar. |
| snapshot imutável | Registro de comissão congelado no ganho (`is_snapshot = TRUE`); sem UPDATE/DELETE (RN-007, RN-022). |
| comissão projetada | Comissão calculada de oportunidades abertas (percentuais atuais, sem snapshot). |
| comissão consolidada | Soma dos snapshots de oportunidades ganhas; base de pagamento. |
| stale (estagnada) | Oportunidade aberta sem atividade concluída há ≥ 14 dias corridos (RN-028). |
| motivo_perda (loss_reason) | Razão obrigatória ao mover para categoria `lost` (RN-004). |
| data_fechamento_esperada | Data estimada de fechamento; obrigatória a partir de "Proposta Enviada" (RN-003). |
| objeto de valor | Conceito de domínio imutável definido por seus atributos (ex.: OpportunityNumber, Money, CommissionCalculation). |
| BU | Business Unit — unidade de negócio do tenant com pipeline e estágios próprios. |

> Identificadores técnicos em inglês conforme `.forge/rules/conventions/language-policy.md`. O termo "objeto de valor" é sempre escrito por extenso (nunca "VO").

## 9. Fora do escopo do MVP

- Múltiplos parceiros por oportunidade (rateio N:N): o MVP suporta 1 parceiro por oportunidade; schema preparado para N:N (azim-product-spec §5.3).
- Automação de pipeline por workflow orientada a eventos `opportunity.*` (`workflow-automation` — Fase 2; VAL-PIPE-01 / DDD-VAL-05).
- Scoring de oportunidade com IA (`ai-intelligence` — Fase 3).
- Multimoeda: campo `currency` em `opportunities` e `commissions` (LAC-05; decisão pendente em VAL-TRD-08 antes do schema freeze).
- Portal do parceiro com login próprio (parceiro sem credencial no MVP — DEC-012, RN-021).
- Definição final do mecanismo de geração do `opportunity_number` (SEQUENCE do banco vs aplicação) é decisão de `design.md` (VAL-PIPE-02); este requisito fixa apenas as propriedades de unicidade, atomicidade e imutabilidade.

### Pendência aberta — VAL-07

| Código | Pendência | Impacto | Encaminhamento |
|--------|-----------|---------|----------------|
| VAL-07 | Ganho de oportunidade de canal "Parceiro" com comissão em branco: deve gerar **alerta** (permite ganho com comissão zero) ou **bloqueio** (impede o ganho)? | Afeta o critério 14.1 e a interação com RN-008; muda a regra de fechamento do diferencial competitivo | Decisão de Produto requerida antes de promover este documento a "Aprovado para desenvolvimento". |

## 10. Referências cruzadas

| Origem | Referência |
|--------|------------|
| FRD | docs/product/frd-nfrd/frd.md § RF-06; FRD-pipeline-01..09; RN-001..RN-008, RN-015, RN-016, RN-022, RN-024..RN-028 |
| NFRD | docs/product/frd-nfrd/nfrd.md § NFR-PERF-02, NFR-PERF-03, NFR-SEG-01, NFR-SEG-03, NFR-AUD-01, NFR-AUD-03, NFR-DISP-01, NFR-ESC-01 |
| Product Spec | docs/product/azim-product-spec.md §5.1 (Opportunity), §5.2 (canais), §5.3 (comissão de parceiro), §6 (RF-06) |
| TRD | docs/product/trd/trd.md §7.2, §8.4, §9.3 (eventos `opportunity.*`, `commission.*`), §10.2; TOBJ-05; FLOW-02/07 |
| Data Model | docs/product/data-model/data-model.md §3 (opportunities, opportunity_stage_transitions, opportunity_partner_commissions; UNIQUE(tenant_id, opportunity_number)) |
| Glossário | docs/product/glossary/ubiquitous-language.md § Opportunity Pipeline (BC-01) |
| Módulo | docs/product/modules/opportunity-pipeline/README.md |
| ADR | docs/product/adr/0001-isolamento-multi-tenant-defesa-em-profundidade.md; ADR de snapshot imutável e ADR de unicidade por tenant (a criar) |
| Regras | `.forge/rules/domain/money-as-cents.md`, `.forge/rules/domain/nbr-5891-rounding.md`, `.forge/rules/domain/audit-immutability.md`, `.forge/rules/architecture/security-and-compliance.md`, `.forge/rules/conventions/language-policy.md`, `.forge/rules/conventions/document-versioning.md` |
| Decisões | DEC-001, DEC-002, DEC-006, DEC-010, DEC-011, DEC-012 |
| Pendências | VAL-07 (comissão em branco no ganho), VAL-PIPE-01/02/03, VAL-TRD-08 (multimoeda) |

