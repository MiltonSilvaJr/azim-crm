# REPORT — Reporting (Relatórios)
**Requisitos Funcionais e Não-Funcionais**

- Versão: 0.1.0
- Data: 2026-06-11
- Status: Rascunho para revisão
- Referência pai: docs/product/frd-nfrd/frd.md § FRD-report-01 a FRD-report-06 (RF-11); docs/product/modules/reporting/README.md

## Histórico de Versões

| Versão | Data | Status | Descrição da alteração |
|--------|------|--------|------------------------|
| 0.1.0 | 2026-06-11 | Rascunho para revisão | Criação inicial do documento de requisitos do módulo reporting (MVP enxuto: funil, forecast, ranking, canais, comissões, export CSV) |

## 1. Visão Geral

O módulo **reporting** (Bounded Context BC-07, subdomínio de suporte — *Supporting Subdomain*) entrega relatórios analíticos e rankings derivados dos dados autoritativos de outros bounded contexts: Opportunity Pipeline (BC-01), Partner Management (BC-03) e Goal & Forecast (BC-05).

Características fundamentais que delimitam o módulo:

- **Sem escrita própria de negócio.** O módulo é exclusivamente de leitura. Ele consome *read models* (projeções otimizadas para leitura) e *views* derivadas, sem possuir entidades transacionais próprias.
- **Não recalcula regras de negócio.** O módulo **reflete** os valores autoritativos já calculados nos módulos de origem (por exemplo, `valor_total`, `forecast_ponderado` e os *snapshots* imutáveis de comissão). Ele agrega, filtra e ordena — nunca redefine fórmulas de domínio.
- **Consistência eventual aceitável.** Para os relatórios, é aceitável que o *read model* esteja eventualmente consistente em relação ao modelo transacional (ver RNF de consistência). Na Fase 1 a leitura é síncrona no mesmo banco; na Fase 2 migra para *read models* assíncronos projetados pelo `azim-reporting-worker`.
- **Isolamento por tenant e controle de acesso por escopo (RBAC).** Todo relatório é restrito ao `tenant_id` do usuário autenticado e ao escopo do seu papel (Vendedor vê os seus; Gestor de BU vê a sua BU; Tenant Admin vê o tenant inteiro).

O *deployable* candidato é o `azim-reporting-worker` (Fase 2); na Fase 1 opera como módulo síncrono dentro do `azim-api`. A decisão entre síncrono na Fase 1 e *worker* assíncrono é registrada como pendência de design (ver VAL-MOD-03 na seção 9 e cross-refs).

## 2. Escopo

### 2.1 Incluído

- Relatório de **funil por estágio** (`FunnelReport`): quantidade de oportunidades e valores por estágio, BU e período.
- Relatório de **forecast por BU e mês** (`ForecastReport`): `forecast_ponderado` agregado e `realizado` por BU e período.
- Relatório de **ranking por responsável** (`RankingReport`): valor ganho e quantidade de oportunidades ganhas por responsável no período.
- Relatório de **oportunidades por canal de origem** (`origin_channel`): distribuição de oportunidades por canal.
- Relatório de **comissões por parceiro** (`CommissionReport`): comissão projetada (oportunidades abertas) × comissão consolidada (snapshots de oportunidades Ganhas), por período.
- **Export CSV** de qualquer relatório (PT-BR, valores em R$, codificação UTF-8 com BOM, sem PII desnecessária).
- **Filtros** por período (mês, trimestre ou intervalo personalizado) e por BU.
- **RBAC por escopo** aplicado a todo relatório e a todo export.
- **Isolamento por tenant** em toda leitura e agregação.

### 2.2 Excluído

- Qualquer escrita em entidades de negócio (pertence aos módulos *upstream*: opportunity-pipeline, partner-management, goal-forecast).
- Recálculo de regras de negócio de valor, forecast ou comissão (os valores são reflexo dos módulos autoritativos).
- Relatórios de auditoria / consulta de `audit_logs` (pertence ao módulo audit-log).

### 2.3 Fora do escopo do MVP

Ver seção 9.

## 3. Personas / Atores

| Persona | Identificador técnico | Escopo de leitura em relatórios |
|---------|----------------------|----------------------------------|
| Vendedor | `vendedor` | Apenas as oportunidades das quais é `owner` (os seus dados) dentro das suas BUs |
| Gestor de BU | `gestor_bu` | Todas as oportunidades das BUs em que possui *membership* de gestor |
| Tenant Admin | `tenant_admin` | Todo o tenant (todas as BUs) |
| Viewer | `viewer` | Somente leitura, no escopo da BU (sem export quando não autorizado) |
| Platform Operator | `platform_operator` | **Bloqueado** para dados comerciais de tenants (ver RNF de privacidade) |

> Os papéis e o escopo seguem a matriz de autorização do FRD (§ Matriz de autorização) e o BC-08 Organization Management. Identificadores em inglês; nomes de papéis conforme glossário.

## 4. Lista canônica de relatórios

| Código | Relatório | Read model / fonte | Agregação principal | Origem (RF/FRD) |
|--------|-----------|--------------------|---------------------|-----------------|
| `funnel` | Funil por estágio | `FunnelReport` (opportunities, stages) | Qtd. e soma de `valor_total` e `forecast_ponderado` por estágio | FRD-report-01 |
| `forecast` | Forecast por BU e mês | opportunities, goals | `forecast_ponderado` e `realizado` por BU/mês | FRD-report-02 |
| `ranking` | Ranking por responsável | opportunities | Valor ganho e qtd. ganhas por `owner_id` | FRD-report-03 |
| `channel` | Oportunidades por canal | opportunities | Qtd., `valor_total` e % do total por `origin_channel` | FRD-report-04 |
| `commissions` | Comissões por parceiro | `CommissionReport` (opportunity_partner_commissions, partners) | `comissao_projetada` × `comissao_consolidada` por parceiro | FRD-report-05 |

> Esta é a lista canônica de relatórios do MVP. Qualquer relatório fora desta lista é Fase 2 ou posterior (seção 9).

## 5. Requisitos Funcionais

### Req 1 — Relatório de funil por estágio

**Como** gestor de BU ou Tenant Admin **quero** visualizar a distribuição de oportunidades por estágio do funil **para** entender a saúde e a concentração do pipeline comercial no período.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-report-01 (RF-11); README §4 |
| **Módulo** | reporting |

**Critérios de Aceite:**

- 1.1 O relatório lista, para cada estágio (`stage`) da(s) BU(s) no escopo, a quantidade de oportunidades e a soma de `valor_total` e de `forecast_ponderado`, com valores em centavos inteiros.
- 1.2 Os valores exibidos refletem os valores autoritativos do opportunity-pipeline; o módulo não recalcula `valor_total` nem `forecast_ponderado`.
- 1.3 O relatório é filtrável por período (mês, trimestre ou intervalo personalizado) e por BU.
- 1.4 Os estágios exibidos correspondem aos estágios configurados da BU, incluindo categorias aberta, ganha e perdida.
- 1.5 O resultado é restrito ao escopo RBAC do usuário (ver Req 7) e ao `tenant_id` autenticado (ver Req 8).

**Cross-ref:** Req 6 (forecast), FRD-pipeline-01, RN-006 (forecast_ponderado), RN-015 (centavos inteiros)

### Req 2 — Relatório de ranking por responsável

**Como** gestor de BU ou Tenant Admin **quero** um ranking de responsáveis por valor ganho e quantidade de oportunidades ganhas **para** acompanhar e comparar o desempenho comercial da equipe no período.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-report-03 (RF-11); README §4 |
| **Módulo** | reporting |

**Critérios de Aceite:**

- 2.1 O relatório lista, por `owner_id` (responsável), a quantidade de oportunidades ganhas, o valor ganho (soma de `valor_total` das Ganhas) e o valor em pipeline (soma de `forecast_ponderado` das abertas) no período, em centavos inteiros.
- 2.2 O ranking é ordenado por valor ganho de forma decrescente por padrão.
- 2.3 O nome do responsável (`display_name`) é tratado como dado pessoal (PII) e exibido apenas dentro do escopo RBAC do usuário (ver Req 7 e RNF 4).
- 2.4 Um usuário com papel Vendedor visualiza apenas a sua própria linha; Gestor de BU visualiza os responsáveis da sua BU; Tenant Admin visualiza o tenant inteiro.
- 2.5 O relatório é filtrável por período e por BU.

**Cross-ref:** RNF 4 (privacidade/PII), Req 7 (RBAC), RN-015

### Req 3 — Relatório de oportunidades por canal de origem

**Como** gestor de BU ou Tenant Admin **quero** ver a distribuição de oportunidades por canal de origem **para** avaliar o desempenho de cada canal de aquisição.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-report-04 (RF-11); README §4 |
| **Módulo** | reporting |

**Critérios de Aceite:**

- 3.1 O relatório lista, por `origin_channel`, a quantidade de oportunidades, a soma de `valor_total` (centavos inteiros) e o percentual em relação ao total do escopo filtrado.
- 3.2 A soma dos percentuais por canal totaliza 100% (com tolerância de arredondamento de exibição), considerando apenas as oportunidades do escopo filtrado.
- 3.3 Os canais de origem correspondem aos valores configurados por BU no opportunity-pipeline.
- 3.4 O relatório é filtrável por período e por BU e respeita o escopo RBAC e o `tenant_id`.

**Cross-ref:** Req 7 (RBAC), Req 8 (isolamento), glossário `origin_channel`

### Req 4 — Relatório de comissões por parceiro (projetado × consolidado)

**Como** gestor de BU ou Tenant Admin **quero** ver, por parceiro, a comissão projetada e a comissão consolidada **para** acompanhar o comissionamento em pipeline e o já realizado, base confiável para pagamento.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-report-05 (RF-11); FRD-partner-02; README §4 |
| **Módulo** | reporting |

**Critérios de Aceite:**

- 4.1 O relatório lista, por parceiro, a `comissao_projetada` (soma das comissões calculadas de oportunidades abertas), a `comissao_consolidada` (soma dos *snapshots* imutáveis de oportunidades Ganhas) e a quantidade de oportunidades, em centavos inteiros.
- 4.2 A `comissao_consolidada` é derivada exclusivamente dos *snapshots* imutáveis (`opportunity_partner_commissions`), **não** dos percentuais atuais do parceiro; alterações de percentuais após o fechamento não alteram o valor consolidado.
- 4.3 A `comissao_projetada` reflete os percentuais atuais aplicados às oportunidades abertas e pode variar entre execuções do relatório quando os percentuais ou as oportunidades mudam.
- 4.4 O módulo não recalcula as fórmulas de comissão; reflete os valores autoritativos do opportunity-pipeline e do partner-management.
- 4.5 O relatório é filtrável por período e por BU e respeita o escopo RBAC e o `tenant_id`.

**Cross-ref:** PBT-01, PBT-02, RN-007 (snapshot imutável), RN-022, RN-026 (fórmula de comissão), glossário `comissao_projetada` / `comissao_consolidada`

### Req 5 — Export CSV de relatório

**Como** Tenant Admin ou gestor de BU **quero** exportar qualquer relatório em CSV **para** analisar os dados externamente e compartilhar com áreas que operam fora do CRM.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-report-06 (RF-11); CAP-09; README §4 |
| **Módulo** | reporting |

**Critérios de Aceite:**

- 5.1 O export contém todas as colunas do relatório selecionado, em arquivo CSV com codificação UTF-8 com BOM.
- 5.2 Cabeçalhos e rótulos do CSV estão em pt-BR; valores monetários são apresentados em R$ formatado (camada de apresentação), embora a origem do dado seja centavos inteiros.
- 5.3 O CSV não contém PII além do estritamente necessário ao relatório (por exemplo, `display_name` do responsável apenas no ranking, dentro do escopo RBAC); nenhum campo de PII adicional é incluído.
- 5.4 O export respeita exatamente o mesmo escopo RBAC e de `tenant_id` aplicado ao relatório visualizado — não há vazamento de linhas fora do escopo do usuário.
- 5.5 O conteúdo do CSV é consistente com o relatório exibido para o mesmo conjunto de filtros (mesmas linhas, mesmos totais).

**Cross-ref:** RNF 3 (performance export), RNF 4 (PII/privacidade), Req 7 (RBAC), Req 8 (isolamento), NFR-PRIV-01, NFR-PRIV-02

### Req 6 — Relatório de forecast por BU e mês

**Como** gestor de BU ou Tenant Admin **quero** ver o forecast ponderado e o realizado por BU e por mês **para** acompanhar a previsão de receita frente ao realizado.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-report-02 (RF-11); README §4 |
| **Módulo** | reporting |

**Critérios de Aceite:**

- 6.1 O relatório lista, por BU e por mês/ano do período, a soma de `forecast_ponderado` das oportunidades abertas e o `realizado` (soma de `valor_total` das oportunidades Ganhas), em centavos inteiros.
- 6.2 Os valores refletem os valores autoritativos do opportunity-pipeline; o módulo não recalcula `forecast_ponderado` nem `realizado`.
- 6.3 Quando há meta cadastrada no goal-forecast para a BU/mês, o relatório pode exibir o comparativo; a ausência de meta não gera erro nem placeholder inválido (degradação graciosa).
- 6.4 O relatório é filtrável por período e por BU e respeita o escopo RBAC e o `tenant_id`.

**Cross-ref:** Req 1 (funil), FRD-goal-02, RN-006, RN-017 (graceful degradation), glossário `realizado` / `pipeline_disponivel`

### Req 7 — Controle de acesso por escopo (RBAC)

**Como** plataforma multi-tenant **quero** que todo relatório e export seja filtrado pelo escopo do papel do usuário **para** garantir que cada usuário veja apenas os dados que tem direito de acessar.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § Matriz de autorização; NFR-SEG-03; README §4/§16 |
| **Módulo** | reporting |

**Critérios de Aceite:**

- 7.1 Um usuário com papel Vendedor obtém em qualquer relatório apenas as oportunidades das quais é `owner`.
- 7.2 Um usuário com papel Gestor de BU obtém apenas dados das BUs em que possui *membership* de gestor.
- 7.3 Um usuário com papel Tenant Admin obtém dados de todas as BUs do seu tenant.
- 7.4 O escopo RBAC é verificado no servidor em todo endpoint de relatório e de export; a ausência ou insuficiência de escopo resulta em negação de acesso, não em dados parciais não autorizados.
- 7.5 O escopo aplicado ao export CSV é idêntico ao escopo aplicado ao relatório visualizado.

**Cross-ref:** Req 8 (isolamento por tenant), RNF 4, NFR-SEG-03, NFR-SEG-01

### Req 8 — Isolamento por tenant na leitura de relatórios

**Como** plataforma multi-tenant **quero** que toda leitura de relatório seja restrita ao tenant do usuário **para** impedir qualquer vazamento de dados entre tenants.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | NFR-SEG-01; DEC-006 (multi-tenancy pool + RLS); README §16 |
| **Módulo** | reporting |

**Critérios de Aceite:**

- 8.1 Toda query e todo *read model* de relatório é filtrado pelo `tenant_id` do usuário autenticado.
- 8.2 Não existe caminho de relatório ou export que retorne dados de um `tenant_id` diferente do contexto autenticado.
- 8.3 O isolamento por tenant é verificado por teste automatizado de isolamento (não há dependência apenas de filtro na camada de aplicação).

**Cross-ref:** PBT-03, NFR-SEG-01, NFR-MAN-02 (testes de isolamento), Req 7

## 6. Requisitos Não-Funcionais

### RNF 1 — Latência dos relatórios enxutos

| Campo | Valor |
|-------|-------|
| **Categoria** | Performance |
| **Prioridade** | Must |
| **Origem** | NFR-PERF-07 (alvo proposto, ver PTV-01); NFR-ESC-01 |
| **Módulo** | reporting |

**Descrição:**

Os relatórios enxutos (funil, forecast, ranking, canais e comissões), para períodos de até 12 meses, devem responder dentro do SLO de latência, sem degradar a operação da API transacional.

**Critérios de Aceite:**

- RNF-1.1 Latência p95 ≤ 3.000 ms (3 s) para qualquer relatório com período ≤ 12 meses, medida nos endpoints de relatório (alvo proposto a confirmar — PTV-01).
- RNF-1.2 O alvo é validado em teste de carga pré-release com volume de referência de até 2.000 oportunidades por tenant.
- RNF-1.3 A geração de relatório na Fase 1 não viola os SLOs dos endpoints transacionais do pipeline (mitigação de RISK-REPORT-01).

**Cross-ref:** NFR-PERF-07, NFR-ESC-01, RISK-REPORT-01, VAL-MOD-03

### RNF 2 — Consistência eventual do read model

| Campo | Valor |
|-------|-------|
| **Categoria** | Integridade |
| **Prioridade** | Must |
| **Origem** | Decisão arquitetural (TRD §10.4 read models; README §1); RISK-REPORT-02 |
| **Módulo** | reporting |

**Descrição:**

Os relatórios são derivados de *read models*. Na Fase 1 a leitura é síncrona sobre o mesmo banco (consistência forte na prática). Na Fase 2, com *read models* assíncronos projetados por evento, é aceitável consistência eventual em relação ao modelo transacional. A defasagem deve ser limitada e observável.

**Critérios de Aceite:**

- RNF-2.1 A consistência eventual do *read model* é declarada explicitamente como característica do módulo e documentada para o consumidor do relatório.
- RNF-2.2 Na Fase 2, o relatório expõe o instante (`timestamp`) da última atualização do *read model* utilizado.
- RNF-2.3 Na Fase 2, existe um limite de defasagem (SLA de atualização do *read model*) definido e monitorado; a violação do limite é alertável (mitigação de RISK-REPORT-02).
- RNF-2.4 O módulo nunca apresenta valores recalculados localmente que divirjam dos valores autoritativos dos módulos de origem — divergências decorrem apenas de defasagem temporal do *read model*, não de regra de negócio reimplementada.

**Cross-ref:** RISK-REPORT-02, VAL-MOD-03, Req 1, Req 4, Req 6

### RNF 3 — Tempo de geração do export CSV

| Campo | Valor |
|-------|-------|
| **Categoria** | Performance |
| **Prioridade** | Should |
| **Origem** | FRD-report-06 (critérios comuns); README §13 |
| **Módulo** | reporting |

**Descrição:**

O export CSV deve ser gerado e disponibilizado para download dentro de um limite de tempo previsível, sem impactar a operação transacional.

**Critérios de Aceite:**

- RNF-3.1 O export CSV é gerado em ≤ 10 s para o volume de referência (até 2.000 oportunidades por tenant, período ≤ 12 meses).
- RNF-3.2 O tempo de export é incluído na medição de latência de relatórios.

**Cross-ref:** RNF 1, Req 5

### RNF 4 — Privacidade de PII em relatórios e exports

| Campo | Valor |
|-------|-------|
| **Categoria** | Privacidade |
| **Prioridade** | Must |
| **Origem** | NFR-PRIV-01, NFR-PRIV-02; LGPD; README §16 |
| **Módulo** | reporting |

**Descrição:**

Relatórios e exports podem conter dados pessoais (por exemplo, `display_name` de responsável no ranking). A exposição deve respeitar o princípio de minimização e o escopo RBAC, e PII não deve aparecer em logs.

**Critérios de Aceite:**

- RNF-4.1 Nenhum relatório ou export inclui PII além do estritamente necessário ao seu objetivo (minimização de dados).
- RNF-4.2 PII de pessoas (responsável, contato) não é registrada em logs do módulo (zero ocorrências de nome, e-mail ou celular nos logs).
- RNF-4.3 Mensagens de erro do módulo não expõem dados pessoais nem dados de outros tenants.
- RNF-4.4 PII só é exibida dentro do escopo RBAC do usuário (ver Req 7).

**Cross-ref:** NFR-PRIV-01, NFR-PRIV-02, Req 2, Req 5, Req 7

### RNF 5 — Bloqueio de acesso de Platform Operator a dados comerciais

| Campo | Valor |
|-------|-------|
| **Categoria** | Segurança |
| **Prioridade** | Must |
| **Origem** | NFR-PRIV-05; README §16 |
| **Módulo** | reporting |

**Descrição:**

O papel Platform Operator não pode acessar dados comerciais de tenants por meio dos relatórios.

**Critérios de Aceite:**

- RNF-5.1 Requisições de relatório ou export originadas de um Platform Operator a dados comerciais de tenant são negadas.
- RNF-5.2 A negação é verificável por teste automatizado.

**Cross-ref:** NFR-PRIV-05, NFR-SEG-03, Req 7

### RNF 6 — Observabilidade da geração de relatórios

| Campo | Valor |
|-------|-------|
| **Categoria** | Observabilidade |
| **Prioridade** | Should |
| **Origem** | README §17; ADR-0001 (correlationId e tenant_id) |
| **Módulo** | reporting |

**Descrição:**

Cada geração de relatório e export deve ser observável para diagnóstico de performance e auditoria operacional.

**Critérios de Aceite:**

- RNF-6.1 Cada geração registra log com `correlation_id`, `tenant_id`, tipo de relatório, filtros aplicados e duração de execução — sem PII (coerente com RNF 4).
- RNF-6.2 São expostas métricas de `reports_generated_total` e `report_generation_duration_seconds` segmentadas por tipo de relatório.
- RNF-6.3 É possível configurar alerta quando `report_generation_duration_seconds` ultrapassa o limite definido (SLA a confirmar).

**Cross-ref:** ADR-0001, RNF 1, RNF 4, NFR-OBS-01

### RNF 7 — Disponibilidade Tier 2

| Campo | Valor |
|-------|-------|
| **Categoria** | Disponibilidade |
| **Prioridade** | Should |
| **Origem** | NFR-DISP-02 (Tier 2); README §2 |
| **Módulo** | reporting |

**Descrição:**

Relatórios são classificados como Tier 2 — importantes, mas com degradação tolerável por algumas horas e sem SLA crítico de latência.

**Critérios de Aceite:**

- RNF-7.1 Disponibilidade mensal mínima de 99,0% para os endpoints de relatório (≤ 7h18min de indisponibilidade/mês).
- RNF-7.2 A indisponibilidade de relatórios não bloqueia operações transacionais do pipeline.

**Cross-ref:** NFR-DISP-02, README §2

## 7. Property-Based Testing

### PBT-01 — Comissão consolidada é imutável a alterações de percentual do parceiro

**Mapeia para:** Req 4, RNF 2
**Tipo:** Invariante matemática

**Propriedade:**

> Para qualquer conjunto de oportunidades Ganhas com *snapshots* de comissão e qualquer alteração posterior dos percentuais default do parceiro, a `comissao_consolidada` calculada pelo relatório permanece igual à soma dos *snapshots* imutáveis registrados no momento do fechamento — independente dos percentuais atuais.

### PBT-02 — Conservação da soma de comissão por parceiro

**Mapeia para:** Req 4
**Tipo:** Invariante matemática

**Propriedade:**

> Para qualquer conjunto de oportunidades de um parceiro no escopo filtrado, a `comissao_projetada` total do relatório é igual à soma das comissões projetadas das oportunidades abertas, e a `comissao_consolidada` total é igual à soma dos *snapshots* das oportunidades Ganhas — sem perda, duplicação ou erro de arredondamento, operando em centavos inteiros.

### PBT-03 — Isolamento por tenant (anti-vazamento)

**Mapeia para:** Req 8, Req 7
**Tipo:** Anti-enumeração / Isolamento

**Propriedade:**

> Para qualquer relatório executado por um usuário do tenant A com quaisquer filtros, nenhuma linha do resultado pertence a um `tenant_id` diferente de A; o conjunto resultado é sempre subconjunto dos dados do tenant A.

### PBT-04 — Conservação da distribuição por canal

**Mapeia para:** Req 3
**Tipo:** Invariante matemática

**Propriedade:**

> Para qualquer conjunto de oportunidades no escopo filtrado, a soma das quantidades por `origin_channel` é igual à quantidade total de oportunidades do escopo, e a soma dos percentuais por canal é 100% (a menos de arredondamento de exibição).

### PBT-05 — Equivalência relatório × export CSV

**Mapeia para:** Req 5
**Tipo:** Round-trip

**Propriedade:**

> Para qualquer relatório e qualquer conjunto de filtros, as linhas e os totais materializados no export CSV são equivalentes às linhas e totais do relatório exibido para os mesmos filtros e o mesmo escopo RBAC.

## 8. Glossário local

| Termo | Definição | Origem |
|-------|-----------|--------|
| read model (modelo de leitura) | Projeção de dados otimizada para leitura, derivada de um ou mais bounded contexts, sem escrita de negócio própria | README §7; TRD §10.4 |
| FunnelReport | Relatório de oportunidades por estágio, BU e período | README §7 |
| ForecastReport | Relatório de `forecast_ponderado` e `realizado` por BU e período | README §7 |
| RankingReport | Ranking de responsáveis por valor ganho no período | README §7 |
| CommissionReport | Comissões por parceiro derivadas de `opportunity_partner_commissions` e `partners` | README §7 |
| comissao_projetada | Soma das comissões calculadas de oportunidades abertas (percentuais atuais; pode variar) | Linguagem Ubíqua BC-07 |
| comissao_consolidada | Soma dos *snapshots* imutáveis de oportunidades Ganhas (base confiável para pagamento) | Linguagem Ubíqua BC-07 |
| origin_channel (canal de origem) | Canal de origem da oportunidade usado para análise por canal | Linguagem Ubíqua BC-01 |
| forecast_ponderado | `valor_total × probabilidade_do_estagio`, em centavos inteiros (valor autoritativo do pipeline) | Linguagem Ubíqua BC-01; RN-006 |
| realizado | Soma do `valor_total` das oportunidades Ganhas no período | Linguagem Ubíqua BC-05 |
| export CSV | Export dos dados do relatório em formato CSV (UTF-8 com BOM) para download | README §7 |
| consistência eventual | Propriedade do *read model* (Fase 2) de refletir o modelo transacional após defasagem temporal limitada | TRD §10.4; RISK-REPORT-02 |

> PII = *Personally Identifiable Information* (informação pessoal identificável). Termos monetários sempre em centavos inteiros no domínio; exibição em R$ é responsabilidade da camada de apresentação.

## 9. Fora do escopo do MVP

- **Dashboards avançados e em tempo real** (Fase 2): dashboards interativos, drill-down e visualizações em tempo real estão fora do MVP. O MVP entrega os cinco relatórios enxutos da seção 4 com export CSV.
- **Read models assíncronos via `azim-reporting-worker`** (Fase 2): na Fase 1 a leitura é síncrona no `azim-api`; a migração para *worker* dedicado com projeção por evento (Pub/Sub) é Fase 2.
- **Relatórios de auditoria** (consulta de `audit_logs`): pertence ao módulo audit-log.
- **Relatórios de IA / scoring** (Fase 3): pertence ao módulo ai-intelligence.
- **Pendência de design — VAL-MOD-03 / VAL-TRD-02 / DDD-VAL-02:** a decisão entre manter o reporting como **módulo síncrono do `azim-api` na Fase 1** ou promovê-lo a **`azim-reporting-worker` assíncrono** desde já é uma decisão de arquitetura a ser resolvida no `design.md`. Recomendação documentada (TRD/README): manter síncrono na Fase 1 e promover a *worker* na Fase 2. **Esta pendência não bloqueia os requisitos funcionais deste documento**, que descrevem o "o quê" independentemente do *deployable* escolhido.

## 10. Referências cruzadas

| Referência | Origem | Relação |
|------------|--------|---------|
| RF-11 / FRD-report-01..06 | docs/product/frd-nfrd/frd.md | Requisitos funcionais de relatórios e export CSV |
| NFR-PERF-07, NFR-ESC-01 | docs/product/frd-nfrd/nfrd.md | Latência e escalabilidade de relatórios |
| NFR-SEG-01, NFR-SEG-03 | docs/product/frd-nfrd/nfrd.md | Isolamento por tenant e RBAC por endpoint |
| NFR-PRIV-01, NFR-PRIV-02, NFR-PRIV-05 | docs/product/frd-nfrd/nfrd.md | PII fora de logs, minimização, bloqueio de Platform Operator |
| NFR-DISP-02 | docs/product/frd-nfrd/nfrd.md | Disponibilidade Tier 2 |
| VAL-MOD-03 / VAL-TRD-02 / DDD-VAL-02 | docs/product/trd/trd.md §6, §11 | Pendência: reporting síncrono × worker assíncrono |
| TRD §7.4, §10.4 | docs/product/trd/trd.md | Deployable azim-reporting-worker; read models FunnelReport/CommissionReport |
| Data Model §6 | docs/product/data-model/data-model.md | Read models e views de reporting |
| RN-006, RN-007, RN-015, RN-017, RN-022, RN-026 | docs/product/frd-nfrd/frd.md | Regras autoritativas refletidas (não recalculadas) pelo reporting |
| ADR-0001 | docs/product/adr/ | correlationId e tenant_id em logs/traces/eventos |
| RISK-REPORT-01, RISK-REPORT-02 | docs/product/modules/reporting/README.md §19 | Impacto de latência (Fase 1) e read model defasado (Fase 2) |
