# GF — Goal & Forecast (Metas e Forecast Comparativo)
**Requisitos Funcionais e Não-Funcionais**

- Versão: 0.1.0
- Data: 2026-06-11
- Status: Rascunho para revisão
- Referência pai: docs/product/frd-nfrd/frd.md § FRD-goal-01, FRD-goal-02 (RF-08); docs/product/modules/goal-forecast/README.md

## Histórico de Versões

| Versão | Data | Status | Descrição da alteração |
|--------|------|--------|------------------------|
| 0.1.0 | 2026-06-11 | Rascunho para revisão | Criação inicial do documento de requisitos do módulo goal-forecast |

## 1. Visão Geral

O módulo **goal-forecast** (Bounded Context BC-05 — Goal & Forecast, subdomínio de suporte, deployable `azim-api`) é responsável por dois resultados de negócio:

1. **Cadastro de metas mensais** (`Goal`) de valor comercial, em centavos inteiros, com escopo por unidade de negócio (BU) ou por responsável.
2. **Composição do painel comparativo "Direção"**: realizado vs meta vs pipeline disponível, com agregações trimestral e anual derivadas, e com degradação graciosa quando não há meta cadastrada.

O módulo **não recalcula forecast**. O valor realizado (`realizado`) e o pipeline ponderado disponível (`pipeline_disponivel`) são derivados do módulo **opportunity-pipeline** por leitura síncrona na query (read model `ForecastView`). O goal-forecast é dono apenas da tabela `goals` e da lógica de comparação meta vs realizado vs pipeline.

A entidade-chave é a **meta** (`Goal`): um objeto de domínio dedicado por dono e período, identificado por `(tenant_id, bu_id, owner_id, year, month)`, contendo `valor_meta` em centavos inteiros — o valor total que se espera ganhar (oportunidades fechadas como Ganho) no mês de referência.

## 2. Escopo

### 2.1 Incluído

- CRUD de metas mensais por escopo BU ou Responsável, com unicidade garantida por período.
- Composição do painel "Direção" (RF-08 básico): meta, realizado, pipeline disponível, gap e percentual de atingimento.
- Agregação trimestral e anual derivada da soma dos meses (RN-027).
- Degradação graciosa: sem meta cadastrada, o painel exibe apenas realizado vs pipeline, sem erro (RN-017).
- Fornecimento do bloco de metas para o azimute semanal do Digest, omitido sem meta (RN-018).
- Auditoria imutável de toda escrita de meta (RN-024 / NFR-AUD-01).
- Isolamento por tenant e RBAC com escopo de visibilidade (vendedor vê as suas, gestor vê a BU, executivo/admin veem o tenant).

### 2.2 Excluído

- Cálculo de `valor_total` e `forecast_ponderado` por oportunidade — pertence ao opportunity-pipeline (RN-005, RN-006).
- Composição e envio do e-mail do Digest — pertence ao digest (BC-06).
- Persistência de metas trimestrais ou anuais como registros próprios — agregações são sempre derivadas (RN-027).

### 2.3 Fora do escopo do MVP

Ver seção 9. Em síntese: projeção por data de fechamento esperada (painel "Direção" completo da Fase 2), metas por produto/categoria, forecast preditivo com IA (Fase 3) e metas em moeda diferente de BRL.

## 3. Personas / Atores

| Código | Persona | Papel no módulo |
|--------|---------|-----------------|
| ACT-04 | Administrador do Tenant (Tenant Admin) | Cadastra e edita metas de qualquer BU e responsável do tenant; vê todas as metas e painéis do tenant. |
| ACT-02 | Gestor de BU | Cadastra e edita metas da sua BU (por BU ou por responsável da BU); vê o painel da sua BU; recebe o bloco de metas no azimute. |
| ACT-03 | Executivo do Tenant | Consome o painel consolidado e o azimute; não cadastra metas no MVP. |
| ACT-01 | Vendedor | Consome o painel restrito às metas em que é responsável (`owner_id` próprio); não cadastra metas. |
| (interno) | Digest worker (BC-06) | Consome o bloco de metas por BU/período para o azimute semanal. |
| (interno) | Reporting (BC-07) | Consome metas e forecast comparativo como read model. |

## 4. Lista canônica de escopos de meta

Toda meta (`Goal`) tem exatamente um escopo, determinado pela combinação de `bu_id` e `owner_id`. Não há outros escopos no MVP.

| Escopo | `bu_id` | `owner_id` | Significado |
|--------|---------|------------|-------------|
| `BU` | obrigatório | nulo | Meta agregada da unidade de negócio no mês. |
| `RESPONSAVEL` | obrigatório | obrigatório | Meta individual de um responsável dentro de uma BU no mês. |

Regras canônicas:

- `bu_id` é sempre obrigatório (não existe meta global de tenant no MVP).
- O escopo `RESPONSAVEL` exige que o usuário em `owner_id` seja membro da BU em `bu_id`.
- A granularidade temporal canônica é mensal (`year`, `month` com `month` entre 1 e 12). Trimestre e ano são sempre derivados, nunca persistidos (RN-027).

## 5. Requisitos Funcionais

### Req 1 — Cadastrar meta mensal

**Como** Administrador do Tenant ou Gestor de BU **quero** cadastrar uma meta mensal de valor em centavos inteiros por BU ou por responsável **para** estabelecer o alvo comercial do período.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-goal-01 (RF-08); DEC-003; RN-027 |
| **Módulo** | goal-forecast |

**Critérios de Aceite:**

- 1.1 É possível criar uma meta informando escopo (`BU` ou `RESPONSAVEL`), `bu_id`, `owner_id` (obrigatório apenas no escopo `RESPONSAVEL`), `year`, `month` e `valor_meta`.
- 1.2 `valor_meta` é recebido, armazenado e transmitido como inteiro de centavos (objeto de valor monetário); requisições com `valor_meta` não inteiro ou negativo são rejeitadas com erro de validação.
- 1.3 `month` deve estar entre 1 e 12 e `year` deve ser um ano de quatro dígitos; valores fora da faixa são rejeitados.
- 1.4 No escopo `RESPONSAVEL`, o `owner_id` deve ser membro da BU em `bu_id`; caso contrário a criação é rejeitada.
- 1.5 A meta criada fica disponível no painel comparativo (Req 5) no mês de referência correspondente.

**Cross-ref:** Req 2, Req 10, RN-015, RN-027, data-model § Goal & Forecast (BC-05)

### Req 2 — Garantir unicidade da meta por dono e período

**Como** Gestor de BU **quero** que exista no máximo uma meta por escopo e período **para** evitar metas conflitantes ou duplicadas no painel.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | data-model § goals UNIQUE; README § 4; RISK-GOAL-02 |
| **Módulo** | goal-forecast |

**Critérios de Aceite:**

- 2.1 A combinação `(tenant_id, bu_id, owner_id, year, month)` é única; uma segunda criação com a mesma chave é tratada como atualização (upsert) ou rejeitada com erro de conflito, nunca gerando dois registros.
- 2.2 Uma condição de corrida (duas criações simultâneas da mesma chave) resulta em exatamente uma meta persistida.
- 2.3 Metas de escopos distintos para o mesmo período coexistem (ex.: meta da BU e meta individual de um responsável da mesma BU no mesmo mês).

**Cross-ref:** Req 1, PBT-01, RISK-GOAL-02

### Req 3 — Consultar metas por período e escopo

**Como** persona autorizada **quero** listar as metas de um período e escopo **para** acompanhar os alvos cadastrados.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-goal-01; README § 9 (GET /v1/goals); TRD § goal-forecast |
| **Módulo** | goal-forecast |

**Critérios de Aceite:**

- 3.1 É possível listar metas filtrando por `bu_id`, por `owner_id`, por `year` e por `month`.
- 3.2 A listagem retorna apenas metas do tenant do solicitante (isolamento por tenant).
- 3.3 O resultado respeita o escopo de visibilidade RBAC definido na Req 12.
- 3.4 Valores monetários (`valor_meta`) são retornados como inteiros de centavos.

**Cross-ref:** Req 12, RNF 1, RNF 2

### Req 4 — Atualizar meta existente

**Como** Administrador do Tenant ou Gestor de BU **quero** atualizar o `valor_meta` de uma meta existente **para** corrigir ou reajustar o alvo do período.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-goal-01; README § 9 (PUT /v1/goals/{id}) |
| **Módulo** | goal-forecast |

**Critérios de Aceite:**

- 4.1 É possível atualizar o `valor_meta` de uma meta identificada por seu `id`, preservando a chave de unicidade `(tenant_id, bu_id, owner_id, year, month)`.
- 4.2 A atualização respeita as validações de centavos inteiros e não-negatividade da Req 1.2.
- 4.3 Uma atualização que viole a unicidade (ex.: tentar reescrever a chave de período para uma já existente) é rejeitada com erro de conflito.
- 4.4 Toda atualização registra a alteração na trilha de auditoria (Req 10).

**Cross-ref:** Req 1, Req 2, Req 10

### Req 5 — Compor painel comparativo "Direção" (realizado vs meta vs pipeline)

**Como** Gestor de BU ou Executivo **quero** ver o painel comparativo do período **para** entender a direção comercial: quanto foi realizado, quanto falta para a meta e quanto há de pipeline disponível.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-goal-02 (RF-08); UC-05; RN-017 |
| **Módulo** | goal-forecast |

**Critérios de Aceite:**

- 5.1 Para um período (`year`, `month`) e escopo (BU ou responsável), o painel retorna `valor_meta`, `realizado`, `pipeline_disponivel`, `gap` e `pct_atingimento`.
- 5.2 `realizado` é a soma do `valor_total` das oportunidades Ganhas no período (derivado do opportunity-pipeline — Req 8).
- 5.3 `pipeline_disponivel` é a soma do `forecast_ponderado` das oportunidades abertas no período (derivado do opportunity-pipeline — Req 8).
- 5.4 `gap = valor_meta − realizado` (em centavos inteiros, podendo ser negativo quando a meta for superada).
- 5.5 `pct_atingimento = realizado / valor_meta`, calculado e exibido apenas quando há meta cadastrada e `valor_meta > 0`.
- 5.6 Todos os valores monetários do painel são inteiros de centavos; a formatação em BRL é responsabilidade do frontend.
- 5.7 O painel é filtrável por BU e por responsável e respeita o escopo de visibilidade RBAC (Req 12).

**Cross-ref:** Req 6, Req 7, Req 8, Req 12, PBT-03, RN-006, RN-015

### Req 6 — Degradação graciosa sem meta cadastrada

**Como** persona que consome o painel **quero** ver realizado e pipeline mesmo sem meta cadastrada **para** não ficar sem informação quando a meta ainda não foi definida.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | RN-017; NFR-RES-03; PRD RF-08; README § 15 |
| **Módulo** | goal-forecast |

**Critérios de Aceite:**

- 6.1 Quando não há meta cadastrada para o período/escopo, o painel retorna `realizado` e `pipeline_disponivel`, com `valor_meta`, `gap` e `pct_atingimento` ausentes (nulos), sem erro HTTP 404 ou erro conspícuo.
- 6.2 A resposta nunca contém `NaN`, valor vazio que se confunda com zero, ou placeholder de erro no lugar de `valor_meta`.
- 6.3 A ausência de meta é representada de forma explícita (campo de meta nulo) para que o frontend exiba "Meta não cadastrada".
- 6.4 A consulta do painel é uma operação total: para qualquer período/escopo válido ela retorna um resultado bem-formado, com ou sem meta.

**Cross-ref:** Req 5, Req 9, PBT-04, RN-017, RN-018

### Req 7 — Agregar metas em trimestre e ano (derivado)

**Como** Gestor de BU ou Executivo **quero** ver a meta agregada por trimestre e por ano **para** acompanhar o alvo acumulado sem cadastrar metas adicionais.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | RN-027; FRD-goal-01; DEC-003 |
| **Módulo** | goal-forecast |

**Critérios de Aceite:**

- 7.1 A meta trimestral de um escopo é a soma das metas dos três meses do trimestre para o mesmo escopo.
- 7.2 A meta anual de um escopo é a soma das metas dos doze meses do ano para o mesmo escopo.
- 7.3 Meses sem meta cadastrada contribuem com zero para a agregação, sem gerar erro.
- 7.4 Não existe registro persistido de meta trimestral ou anual; a agregação é sempre calculada na consulta.

**Cross-ref:** Req 1, Req 5, PBT-02, RN-027

### Req 8 — Derivar realizado e pipeline por leitura do opportunity-pipeline

**Como** módulo goal-forecast **quero** obter realizado e pipeline ponderado a partir do opportunity-pipeline **para** compor o painel sem recalcular forecast.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | README § 13/14; data-model § ForecastView; TRD § goal-forecast; RN-006 |
| **Módulo** | goal-forecast |

**Critérios de Aceite:**

- 8.1 O módulo consome `won_total` (realizado) e `forecast_ponderado` (pipeline disponível) do opportunity-pipeline por leitura síncrona na query (read model `ForecastView`), filtrados por tenant, BU/responsável e período.
- 8.2 O goal-forecast não calcula `valor_total` nem `forecast_ponderado` por oportunidade; essa lógica permanece no opportunity-pipeline (RN-005, RN-006).
- 8.3 A dependência de leitura do opportunity-pipeline é explícita e read-only; o goal-forecast não escreve em dados do pipeline.
- 8.4 Em indisponibilidade da leitura do pipeline, o módulo degrada de forma controlada (ver RNF 6), sem retornar valores monetários incorretos.

**Cross-ref:** Req 5, Req 6, RNF 6, RISK-GOAL-01, RN-006

### Req 9 — Fornecer bloco de metas ao Digest

**Como** Digest worker **quero** consultar o bloco de metas por BU e período **para** compor o azimute semanal de segunda-feira.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-digest-03; RN-018; RN-029; README § 13 |
| **Módulo** | goal-forecast |

**Critérios de Aceite:**

- 9.1 O módulo expõe os dados de meta (realizado vs meta, gap, pipeline disponível) por BU e período para o Digest.
- 9.2 Quando não há meta cadastrada para o período, o bloco é sinalizado como ausente, para que o Digest o omita por completo (sem espaço vazio ou "Meta: —").
- 9.3 Os valores fornecidos ao Digest são inteiros de centavos.

**Cross-ref:** Req 6, RN-018, RN-029

### Req 10 — Auditar toda escrita de meta

**Como** Administrador do Tenant **quero** que toda criação e atualização de meta seja registrada em trilha imutável **para** garantir rastreabilidade das mudanças de alvo.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | RN-024; NFR-AUD-01; README § 10 (GoalUpdated) |
| **Módulo** | goal-forecast |

**Critérios de Aceite:**

- 10.1 Toda criação e atualização de meta gera um registro de auditoria com `user_id`, `entity_type`, `entity_id`, `action`, `delta_json`, `timestamp` e `tenant_id`.
- 10.2 O registro de auditoria é append-only; não é editável nem removível por nenhum papel.
- 10.3 A escrita de auditoria é publicada via mecanismo de auditoria do sistema (evento `GoalUpdated` / `goal.updated.v1`).

**Cross-ref:** Req 1, Req 4, RNF 5, RN-024

### Req 11 — Projeção por data de fechamento esperada (painel "Direção" completo)

**Como** Gestor de BU ou Executivo **quero** ver a projeção de fechamento do mês baseada nas datas de fechamento esperadas **para** estimar a probabilidade de atingir a meta.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Should |
| **Origem** | PRD RF-08 (Fase 2); README § 22; matriz de rastreabilidade FRD (RF-08 — Fase 2) |
| **Módulo** | goal-forecast |

**Critérios de Aceite:**

- 11.1 O painel projeta o valor esperado de fechamento no período considerando `data_fechamento_esperada` e `forecast_ponderado` das oportunidades abertas (derivados do opportunity-pipeline).
- 11.2 A projeção é apresentada junto de realizado, meta e pipeline disponível, sem substituí-los.
- 11.3 Este requisito é da **Fase 2** e não integra o MVP (ver seção 9); o MVP entrega o painel comparativo básico (Req 5) sem projeção.

**Cross-ref:** Req 5, Req 8, seção 9, RN-006

### Req 12 — Isolar por tenant e aplicar RBAC com escopo de visibilidade

**Como** plataforma multi-tenant **quero** restringir cadastro e visualização de metas conforme tenant e papel **para** garantir confidencialidade e governança.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | NFR-SEG-01; NFR-SEG-03; FRD § matriz de permissões; DEC-006 |
| **Módulo** | goal-forecast |

**Critérios de Aceite:**

- 12.1 Toda operação de meta e painel é restrita ao tenant do solicitante (isolamento por `tenant_id`).
- 12.2 Cadastro e edição de metas: Tenant Admin para qualquer BU do tenant; Gestor de BU apenas para a sua BU; Vendedor e Executivo não cadastram metas no MVP.
- 12.3 Visualização: Tenant Admin e Executivo veem o tenant; Gestor de BU vê a sua BU; Vendedor vê apenas as metas em que é o responsável (`owner_id` próprio).
- 12.4 Tentativas de acesso fora do escopo permitido são negadas (autorização verificada em todo endpoint), sem vazar a existência de metas de outras BUs ou tenants.

**Cross-ref:** Req 3, Req 5, RNF 1, RNF 2, NFR-SEG-01, NFR-SEG-03

## 6. Requisitos Não-Funcionais

### RNF 1 — Isolamento multi-tenant com defesa em profundidade

| Campo | Valor |
|-------|-------|
| **Categoria** | Segurança |
| **Prioridade** | Must |
| **Origem** | NFR-SEG-01; DEC-006; RISCO-T01 |
| **Módulo** | goal-forecast |

**Descrição:**

O acesso a metas e painéis deve ser isolado por tenant em múltiplas camadas, de modo que a falha de uma camada não cause vazamento de dados entre tenants.

**Critérios de Aceite:**

- RNF-1.1 Todas as consultas e escritas em `goals` aplicam filtro por `tenant_id` na camada de aplicação e isolamento por Row-Level Security (RLS) no banco.
- RNF-1.2 Testes de isolamento em CI verificam que um tenant não acessa metas de outro tenant via API nem via SQL sem contexto de tenant; falha bloqueia o merge (KPI-06: 100%).
- RNF-1.3 Violação de isolamento em produção é tratada como incidente sev-1 e dispara alerta.

**Cross-ref:** NFR-SEG-01, Req 12

### RNF 2 — RBAC verificado em todo endpoint

| Campo | Valor |
|-------|-------|
| **Categoria** | Segurança |
| **Prioridade** | Must |
| **Origem** | NFR-SEG-03; FRD § matriz de permissões |
| **Módulo** | goal-forecast |

**Descrição:**

Todo endpoint de metas e painel deve verificar o papel (role) e o escopo do solicitante antes de executar a operação.

**Critérios de Aceite:**

- RNF-2.1 Endpoints de escrita de meta exigem papel Tenant Admin (qualquer BU) ou Gestor de BU (sua BU); demais papéis recebem negação de autorização.
- RNF-2.2 Endpoints de leitura aplicam o escopo de visibilidade da Req 12 (vendedor: as suas; gestor: a BU; admin/executivo: o tenant).
- RNF-2.3 Negação de autorização não revela a existência de metas fora do escopo do solicitante.

**Cross-ref:** NFR-SEG-03, Req 12

### RNF 3 — Latência do painel comparativo e da consulta de metas

| Campo | Valor |
|-------|-------|
| **Categoria** | Performance |
| **Prioridade** | Should |
| **Origem** | NFR-PERF-07; NFR-PERF-01; README § 15 |
| **Módulo** | goal-forecast |

**Descrição:**

O painel comparativo "Direção" e as consultas de metas devem responder em tempo real na query, sem cache separado, dentro dos SLOs de relatórios.

**Critérios de Aceite:**

- RNF-3.1 O painel comparativo, para um período de até 12 meses agregados e volume de referência de até 2.000 oportunidades por tenant, responde com p95 ≤ 3.000 ms (alvo a confirmar conforme NFR-PERF-07).
- RNF-3.2 Operações de CRUD de meta respondem dentro do SLO geral de API (NFR-PERF-01).
- RNF-3.3 O cálculo de realizado e pipeline ocorre na query por leitura do read model, sem armazenamento intermediário de cache dedicado.

**Cross-ref:** NFR-PERF-07, NFR-PERF-01, Req 5

### RNF 4 — Integridade monetária em centavos inteiros

| Campo | Valor |
|-------|-------|
| **Categoria** | Integridade |
| **Prioridade** | Must |
| **Origem** | RN-015; DEC-011; data-model § goals |
| **Módulo** | goal-forecast |

**Descrição:**

Todo valor monetário do módulo (`valor_meta`, `realizado`, `pipeline_disponivel`, `gap`) deve ser representado como inteiro de centavos em domínio, persistência e transporte de API, sem uso de ponto flutuante.

**Critérios de Aceite:**

- RNF-4.1 `valor_meta` é persistido como inteiro de 64 bits (referência conceitual `BIGINT`) em centavos; cálculos de gap e agregação operam apenas com inteiros.
- RNF-4.2 A API recebe e retorna valores monetários como inteiros de centavos; conversão para BRL com casas decimais ocorre apenas na camada de apresentação.
- RNF-4.3 Nenhum cálculo monetário usa `float`/`double`; quando houver razão (`pct_atingimento`) ela é derivada e não substitui o valor inteiro de origem.

**Cross-ref:** RN-015, DEC-011, PBT-05

### RNF 5 — Auditoria imutável de escrita de meta

| Campo | Valor |
|-------|-------|
| **Categoria** | Auditoria |
| **Prioridade** | Must |
| **Origem** | NFR-AUD-01; RN-024; README § 10 |
| **Módulo** | goal-forecast |

**Descrição:**

Toda escrita em `goals` deve gerar trilha de auditoria imutável e append-only.

**Critérios de Aceite:**

- RNF-5.1 Cada criação e atualização de meta produz exatamente um registro de auditoria com `user_id`, `entity_type`, `entity_id`, `action`, `delta_json`, `timestamp` e `tenant_id`.
- RNF-5.2 Registros de auditoria não podem ser editados nem excluídos por nenhum papel.
- RNF-5.3 A retenção dos dados de auditoria segue a política transversal de retenção (NFR-AUD-03).

**Cross-ref:** NFR-AUD-01, NFR-AUD-03, Req 10

### RNF 6 — Resiliência à indisponibilidade do opportunity-pipeline

| Campo | Valor |
|-------|-------|
| **Categoria** | Resiliência |
| **Prioridade** | Should |
| **Origem** | NFR-RES-03; RISK-GOAL-01; README § 19 |
| **Módulo** | goal-forecast |

**Descrição:**

Quando a leitura do opportunity-pipeline (origem de realizado e pipeline disponível) falha ou expira, o painel deve degradar de forma controlada, sem retornar valores monetários incorretos e sem propagar erro não tratado.

**Critérios de Aceite:**

- RNF-6.1 Falha ou timeout na leitura do pipeline é contida (ex.: circuit breaker) e não derruba o endpoint do painel.
- RNF-6.2 Quando realizado/pipeline não puderem ser obtidos, o painel sinaliza a indisponibilidade do dado derivado em vez de exibir zero como se fosse valor confirmado.
- RNF-6.3 A indisponibilidade do pipeline é observável (log estruturado com `correlation_id`, `tenant_id`, `bu_id`, período).

**Cross-ref:** NFR-RES-03, RISK-GOAL-01, Req 8

### RNF 7 — Observabilidade das operações de meta

| Campo | Valor |
|-------|-------|
| **Categoria** | Observabilidade |
| **Prioridade** | Should |
| **Origem** | README § 17; NFR-OBS (transversal) |
| **Módulo** | goal-forecast |

**Descrição:**

As operações do módulo devem emitir logs estruturados e métricas que permitam acompanhar uso e diagnosticar problemas.

**Critérios de Aceite:**

- RNF-7.1 Logs estruturados de escrita e consulta incluem `correlation_id`, `tenant_id`, `bu_id`, período e ação.
- RNF-7.2 São expostas as métricas `goals_created_total` e `goals_updated_total`.
- RNF-7.3 Logs não expõem dados sensíveis além do necessário para diagnóstico.

**Cross-ref:** README § 17

## 7. Property-Based Testing

### PBT-01 — Upsert de meta é idempotente por chave

**Mapeia para:** Req 1, Req 2
**Tipo:** Idempotência

**Propriedade:**

> Para qualquer sequência de cadastros de meta com a mesma chave `(tenant_id, bu_id, owner_id, year, month)`, o estado final contém exatamente uma meta cujo `valor_meta` é o do último cadastro aplicado; nunca há dois registros para a mesma chave.

### PBT-02 — Agregação é a soma exata dos meses

**Mapeia para:** Req 7
**Tipo:** Invariante matemática

**Propriedade:**

> Para qualquer conjunto de metas mensais de um mesmo escopo, a meta trimestral é igual à soma dos `valor_meta` dos três meses do trimestre e a meta anual é igual à soma dos doze meses; meses ausentes contribuem com zero. A soma é exata em centavos inteiros, sem perda nem arredondamento.

### PBT-03 — Invariantes do painel comparativo

**Mapeia para:** Req 5
**Tipo:** Invariante matemática

**Propriedade:**

> Para qualquer `valor_meta` e `realizado` em centavos inteiros, `gap = valor_meta − realizado` exatamente; e quando `valor_meta > 0`, `pct_atingimento = realizado / valor_meta`. Para `realizado = valor_meta`, `gap = 0` e `pct_atingimento = 1`.

### PBT-04 — Consulta do painel é função total (graceful degradation)

**Mapeia para:** Req 6
**Tipo:** Anti-enumeração / totalidade

**Propriedade:**

> Para qualquer período e escopo válidos, com ou sem meta cadastrada, a consulta do painel retorna um resultado bem-formado (sem exceção, sem 404, sem `NaN`): com meta presente, inclui `valor_meta`/`gap`/`pct_atingimento`; sem meta, esses campos são nulos e `realizado`/`pipeline_disponivel` permanecem presentes.

### PBT-05 — Round-trip monetário em centavos inteiros

**Mapeia para:** Req 1, RNF 4
**Tipo:** Round-trip

**Propriedade:**

> Para qualquer `valor_meta` inteiro não-negativo de centavos, persistir e ler de volta a meta preserva o valor exatamente; o transporte via API (serialização/desserialização) preserva o inteiro sem conversão para ponto flutuante.

## 8. Glossário local

| Termo | Definição |
|-------|-----------|
| Goal (meta) | Objeto de domínio dedicado por dono e período: `(tenant_id, bu_id, owner_id, year, month)` com `valor_meta` em centavos inteiros. |
| GoalPeriod | Objeto de valor que representa o período da meta (`year`, `month`). |
| valor_meta | Valor da meta em centavos inteiros BRL (DEC-011, RN-015) — valor total esperado de ganho no mês. |
| realizado | Soma do `valor_total` das oportunidades Ganhas no período; derivado do opportunity-pipeline. |
| pipeline_disponivel | Soma do `forecast_ponderado` das oportunidades abertas no período; derivado do opportunity-pipeline. |
| gap | Diferença `valor_meta − realizado`, em centavos inteiros; negativo quando a meta é superada. |
| pct_atingimento | Razão `realizado / valor_meta`, calculada apenas com meta cadastrada e `valor_meta > 0`. |
| painel "Direção" | Painel comparativo de realizado vs meta vs pipeline disponível (e, na Fase 2, projeção por data de fechamento). |
| graceful degradation | Comportamento do painel sem meta cadastrada: exibe realizado e pipeline sem erro (RN-017). |
| azimute_metas | Bloco do azimute semanal do Digest com realizado vs meta e gap; omitido sem meta (RN-018). |
| escopo de meta | Classificação `BU` ou `RESPONSAVEL` da meta (ver seção 4). |

> "Objeto de valor" é usado sempre por extenso. Identificadores técnicos (`bu_id`, `owner_id`, `valor_meta`, `forecast_ponderado`) permanecem em inglês conforme política de linguagem.

## 9. Fora do escopo do MVP

- **Projeção por data de fechamento esperada** (painel "Direção" completo) — Req 11, **Fase 2**. O MVP entrega o painel comparativo básico (Req 5) sem projeção.
- **Metas por produto ou categoria** — fora do MVP (README § 5).
- **Forecast preditivo com IA** (previsão de demanda) — pertence ao ai-intelligence, **Fase 3** (README § 5).
- **Metas em moeda diferente de BRL** — depende de decisão de multimoeda ainda em aberto (VAL-05); fora do MVP.
- **Cadastro de metas por Vendedor ou Executivo** — no MVP apenas Tenant Admin e Gestor de BU cadastram (Req 12).
- **Meta global de tenant sem BU** — fora do MVP; `bu_id` é sempre obrigatório (seção 4).

## 10. Referências cruzadas

| Referência | Relação |
|------------|---------|
| docs/product/frd-nfrd/frd.md § FRD-goal-01, FRD-goal-02 (RF-08) | Origem funcional do cadastro de metas e do painel comparativo. |
| docs/product/frd-nfrd/frd.md § RN-006, RN-015, RN-017, RN-018, RN-024, RN-027 | Regras de negócio aplicáveis. |
| docs/product/frd-nfrd/nfrd.md § NFR-SEG-01, NFR-SEG-03, NFR-PERF-01, NFR-PERF-07, NFR-AUD-01, NFR-RES-03 | Requisitos não-funcionais transversais. |
| docs/product/trd/trd.md § goal-forecast; § ForecastView | Decisões técnicas, endpoints e read model. |
| docs/product/data-model/data-model.md § Goal & Forecast (BC-05) | Modelo da tabela `goals` e unicidade. |
| docs/product/modules/opportunity-pipeline/requirements.md | Dependência de leitura (realizado e forecast_ponderado) — quando existir. |
| docs/product/modules/digest/requirements.md | Consumidor do bloco de metas (azimute) — quando existir. |
| docs/product/glossary/ubiquitous-language.md § Goal & Forecast (BC-05) | Linguagem ubíqua âncora. |
| DEC-003, DEC-011, DEC-006 | Decisões: granularidade mensal; centavos inteiros; multi-tenancy pool + RLS. |
