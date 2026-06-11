# BC-09 — Data Migration (Migração de Planilha)
**Requisitos Funcionais e Não-Funcionais**

- Versão: 0.1.0
- Data: 2026-06-11
- Status: Rascunho para revisão
- Referência pai: docs/product/azim-product-spec.md § Parte V — Migração da planilha; docs/product/frd-nfrd/frd.md § MOD-14 / FRD-migration-01..03

## Histórico de Versões

| Versão | Data | Status | Descrição da alteração |
|--------|------|--------|------------------------|
| 0.1.0 | 2026-06-11 | Rascunho para revisão | Criação inicial do documento de requisitos do módulo data-migration |

## 1. Visão Geral

O módulo **data-migration** é um módulo de suporte (Supporting Subdomain) **temporário da Fase 1** do Azim CRM, identificado como Bounded Context **BC-09** e empacotado no deployable **azim-api**. Sua única responsabilidade é realizar a importação controlada e auditável da planilha `Pipeline Vellus.xlsx` (operação comercial atual da Vellus — primeiro tenant, nº 1) para as entidades de domínio do Azim.

O processo é estritamente sequenciado: **upload do arquivo `.xlsx` → dry-run (simulação sem efeitos colaterais) com relatório de triagem → triagem assistida (resolução de pendências) → import transacional (tudo ou nada) → relatório final auditado → congelamento da planilha de origem**.

O módulo escreve em entidades pertencentes a outros módulos (account-management, partner-management, opportunity-pipeline, activities) por meio de suas APIs internas, em uma **única transação atômica** com rollback total em caso de qualquer falha (RN-023). Não realiza ETL recorrente nem import contínuo: é de **uso único** e candidato à remoção após a Fase 1 (VAL-MOD-04 / DDD-VAL-03).

Os dados próprios do módulo são apenas de registro e auditoria do processo: `migration_jobs` (uma linha por tentativa de import) e `migration_logs` (uma linha por registro processado da planilha), ambos temporários.

## 2. Escopo

### 2.1 Incluído

- Upload e parsing do arquivo `Pipeline Vellus.xlsx` (formato `.xlsx`).
- Validação de formato e de estrutura de colunas esperadas, sem efeitos colaterais.
- Dry-run: simulação completa do import sem escrita no banco, produzindo relatório com contagens, dedupes de contas, flags de triagem e divergências de forecast recalculado vs planilha (> R$ 0,01).
- Triagem assistida: resolução de pendências obrigatórias (owners faltantes) e opcionais (estágios faltantes, parceiros sem percentual) antes do import definitivo.
- Import transacional (tudo ou nada) das entidades: accounts (com dedupe), contacts, partners, opportunities (com número preservado/gerado) e activities (aba Ações Comerciais).
- Preservação do número `AZ-NNNN` quando existir na planilha e geração sequencial para os registros sem número (a partir do próximo livre do tenant, ≥ 95).
- Transformação de datas serial Excel para ISO (epoch 1899-12-30).
- Relatório final auditado e publicação do evento `ImportCompleted`.
- Registro do job e do log por linha em `migration_jobs` e `migration_logs`.

### 2.2 Excluído

- Edição ou normalização de dados na planilha de origem (a planilha é insumo read-only após o import).
- Reconciliação contínua entre planilha e Azim após o congelamento.
- Interface de relatórios analíticos do CRM (pertence ao módulo de relatórios).
- Cadastro de percentuais de comissão dos parceiros (preenchidos pela Vellus pós-import, fora deste módulo).

### 2.3 Fora do escopo do MVP

Consolidado na seção 9.

## 3. Personas / Atores

| Persona | Papel RBAC | Responsabilidade neste módulo |
|---------|-----------|-------------------------------|
| Platform Operator (PlatOp) | Platform Operator | Executa upload, dispara dry-run, conduz a triagem assistida e confirma o import transacional. Único ator com acesso aos endpoints de migração. |
| Tenant Admin | Tenant Admin | Valida o relatório de dry-run e a triagem de owners/estágios da operação da Vellus; aprova a execução do import. |
| Sistema (ImportTransactionService) | — | Executor automatizado do dry-run e do import transacional; emite logs, métricas e o evento `ImportCompleted`. |

> Nota: parceiros comissionados não possuem login no MVP e não são atores deste módulo.

## 4. Lista canônica de estados do job de migração

O agregado `migration_job` evolui por uma máquina de estados linear. Transições inválidas devem ser rejeitadas.

| Estado | Descrição | Transições válidas a partir daqui |
|--------|-----------|-----------------------------------|
| `created` | Job criado; arquivo `.xlsx` recebido e validado quanto a formato/estrutura | `dry_run_completed`, `failed` |
| `dry_run_completed` | Dry-run executado; relatório de triagem disponível; nenhuma escrita no banco | `triage_in_progress`, `failed` |
| `triage_in_progress` | Triagem assistida em andamento; pendências sendo resolvidas | `ready_to_import`, `triage_in_progress` (salvar/retomar) |
| `ready_to_import` | Todas as pendências obrigatórias resolvidas; import habilitado | `importing`, `triage_in_progress` |
| `importing` | Transação de import em execução | `completed`, `rolled_back` |
| `completed` | Import concluído com sucesso; relatório final auditado emitido; `ImportCompleted` publicado | (terminal) |
| `rolled_back` | Falha durante o import; rollback total executado; nenhum dado persistido | `triage_in_progress` (nova tentativa) |
| `failed` | Falha de validação/dry-run antes de qualquer escrita | (terminal por job; novo upload requer novo job) |

Status de cada `migration_log` (por linha processada): `ok` | `aviso` | `erro`.

## 5. Requisitos Funcionais

### Req 1 — Upload da planilha Pipeline Vellus.xlsx

**Como** Platform Operator **quero** enviar o arquivo `Pipeline Vellus.xlsx` ao sistema **para** iniciar o processo de migração de forma controlada.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-migration-01; azim-product-spec.md § Parte V; README MOD-14 |
| **Módulo** | data-migration |

**Critérios de Aceite:**

- 1.1 O upload aceita exclusivamente arquivos no formato `.xlsx`; qualquer outro formato é rejeitado com a mensagem MSG-032 e sem efeitos colaterais no banco.
- 1.2 Um arquivo com estrutura de colunas não reconhecida é rejeitado com a mensagem MSG-033, indicando a lista de colunas esperadas, sem efeitos colaterais.
- 1.3 Cada upload aceito cria um `migration_job` no estado `created`, associado ao tenant da operação.
- 1.4 O upload registra metadados do arquivo (nome, tamanho, contagem de linhas detectadas) sem persistir conteúdo de domínio.

**Cross-ref:** RF-06; FRD-migration-01 (FE-migration01-01, FE-migration01-02); MSG-032, MSG-033

### Req 2 — Dry-run sem efeitos colaterais com relatório de triagem

**Como** Platform Operator **quero** executar uma simulação completa do import **para** revisar contagens, inconsistências e divergências antes de qualquer escrita no banco.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-migration-01; azim-product-spec.md § Parte V; RN-023 |
| **Módulo** | data-migration |

**Critérios de Aceite:**

- 2.1 Nenhuma linha é escrita em nenhuma entidade de domínio durante o dry-run; ao final, o estado do banco é idêntico ao anterior.
- 2.2 O relatório de dry-run apresenta, no mínimo: total de oportunidades, distribuição por BU (Vellus, Axis, Vellus Tech), quantidade sem owner, quantidade sem data de fechamento, oportunidades com parceiro sem percentual, pares de contas candidatas a dedupe e campos com erros de digitação detectados.
- 2.3 Para cada oportunidade, o sistema recalcula o forecast ponderado (`valor_total × probabilidade / 100`, em centavos) e lista no relatório toda divergência superior a R$ 0,01 em relação ao valor de `Forecast (R$)` da planilha.
- 2.4 O relatório lista explicitamente as 64 oportunidades sem responsável da planilha Vellus como pendências obrigatórias de triagem.
- 2.5 Ao concluir, o `migration_job` transita para `dry_run_completed`.

**Cross-ref:** Req 4, Req 6, Req 8; PBT-04, PBT-06; FRD-migration-01

### Req 3 — Mapeamento canônico das colunas da planilha

**Como** sistema **quero** mapear cada coluna da planilha para a entidade e campo de destino corretos **para** garantir importação fiel e rastreável da operação da Vellus.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | azim-product-spec.md § Parte V (tabela de mapeamento) |
| **Módulo** | data-migration |

**Critérios de Aceite:**

- 3.1 `BU` mapeia para `BusinessUnit`, aplicando trim de espaços ("Sertão " → "Sertão"); BU não cadastrada é flag de triagem.
- 3.2 `Empresa / Cliente` mapeia para `Account`, sujeito a dedupe por nome normalizado (ver Req 7).
- 3.3 `Oportunidade / Descrição` mapeia para `titulo`; vazio gera "Oportunidade — {conta}" com flag de triagem.
- 3.4 `Parceiro` mapeia para `Partner` e vínculo; "-" indica sem parceiro; percentuais de comissão não são importados (preenchidos pós-import).
- 3.5 `Probabilidade (%)` é preservada e sobrepõe o default do estágio.
- 3.6 `Valor Setup`, `Mensal` e `Meses` mapeiam para os campos de valor; vazios assumem 0; valores monetários são representados em centavos inteiros.
- 3.7 `Status / Próximos Passos` é preservado como nota na oportunidade e, quando acionável, vira atividade pendente proposta na triagem.

**Cross-ref:** Req 7, Req 8, Req 9, Req 10; data-model.md § opportunities; `.forge/rules/domain/money-as-cents.md`

### Req 4 — Triagem obrigatória de owners faltantes

**Como** Tenant Admin **quero** atribuir responsáveis a todas as oportunidades sem owner **para** que o import só conclua com 100% das oportunidades com responsável definido.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-migration-02; PRD VAL-02 / LAC-02; RN-002 |
| **Módulo** | data-migration |

**Critérios de Aceite:**

- 4.1 As 64 linhas sem responsável entram em fila de atribuição obrigatória; o import transacional não pode ser iniciado enquanto restar qualquer oportunidade sem owner.
- 4.2 A interface permite atribuição em massa: associar um owner a todas as oportunidades de uma BU de uma só vez, além de atribuição individual.
- 4.3 O comando de execução do import é rejeitado (estado permanece em `triage_in_progress`) se houver ao menos uma oportunidade sem owner.
- 4.4 O progresso da triagem pode ser salvo e retomado em outra sessão sem perda de dados.
- 4.5 Quando todas as pendências obrigatórias estiverem resolvidas, o `migration_job` transita para `ready_to_import`.

**Cross-ref:** Req 6; PBT-07; FRD-migration-02; RN-002 (owner obrigatório)

### Req 5 — Triagem de estágios faltantes e parceiros sem percentual

**Como** Tenant Admin **quero** resolver estágios vazios e parceiros sem percentual durante a triagem **para** reduzir flags antes do import sem bloquear a migração desnecessariamente.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Should |
| **Origem** | azim-product-spec.md § Parte V; FRD-migration-02; PRD VAL-01 |
| **Módulo** | data-migration |

**Critérios de Aceite:**

- 5.1 `Etapa` é mapeada 1:1 com o seed de estágios da BU; as 13 linhas com etapa vazia recebem o estágio "Lead" com flag de triagem.
- 5.2 Parceiros citados sem percentual de comissão são listados como flag não bloqueante; o admin pode definir o percentual ou marcar como "a definir".
- 5.3 Diferentemente dos owners (Req 4), estágios faltantes e percentuais ausentes não impedem a conclusão do import.

**Cross-ref:** Req 4, Req 7; FRD-migration-02; PRD VAL-01 (percentuais de comissão)

### Req 6 — Import transacional tudo ou nada com rollback total

**Como** Platform Operator **quero** executar o import como uma transação atômica **para** que nenhum dado parcial seja persistido em caso de qualquer falha.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-migration-03; RN-023; PRD J-04 |
| **Módulo** | data-migration |

**Critérios de Aceite:**

- 6.1 O import escreve em accounts, contacts, partners, opportunities e activities dentro de uma única transação de banco; falha em qualquer passo dispara rollback total e o estado anterior é integralmente preservado (mensagem MSG-034).
- 6.2 A execução exige confirmação explícita do operador e só é aceita quando o job está em `ready_to_import`.
- 6.3 Em sucesso, o job transita para `completed`; em falha, transita para `rolled_back` sem nenhum registro de domínio persistido.
- 6.4 Cada linha processada gera um `migration_log` com status `ok`, `aviso` ou `erro` e mensagem correspondente.

**Cross-ref:** Req 4, Req 11, Req 12; PBT-01; RNF 5; FRD-migration-03; MSG-034

### Req 7 — Criação de contas com dedupe e contatos vinculados

**Como** sistema **quero** consolidar empresas repetidas em uma única conta e vincular seus contatos **para** evitar duplicação e preservar o relacionamento no tenant.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | azim-product-spec.md § Parte V; data-model.md § accounts/contacts; RN-014 |
| **Módulo** | data-migration |

**Critérios de Aceite:**

- 7.1 Empresas com o mesmo nome normalizado (ex.: Pag.ai, Ethoca, Autopass) são consolidadas em uma única conta com N oportunidades.
- 7.2 Pares candidatos a dedupe são apresentados na triagem; o operador decide merge ou manutenção de ambas (RN-014: alerta, não bloqueio).
- 7.3 Contatos (`Nome`/`E-mail`/`Celular`) são criados quando presentes e vinculados à conta como contato principal.
- 7.4 Nenhum dado pessoal de contato é registrado em `migration_logs` (ver RNF 3).

**Cross-ref:** Req 3, Req 10; PBT-04; RNF 3; data-model.md § accounts; RN-014, RN-025

### Req 8 — Preservação e geração de número AZ-NNNN

**Como** sistema **quero** preservar o número de oportunidade existente e gerar número sequencial para os que não têm **para** manter rastreabilidade e substituir a fórmula manual da planilha.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | RN-001; FRD-migration-03; azim-product-spec.md § Parte V |
| **Módulo** | data-migration |

**Critérios de Aceite:**

- 8.1 Quando a coluna `#` contém um número (ex.: `AZ-0043`), ele é preservado como `opportunity_number` da oportunidade importada.
- 8.2 Oportunidades sem número recebem o próximo número livre da sequência do tenant, iniciando em `AZ-0095` (≥ 95).
- 8.3 Os números atribuídos são únicos por tenant; não há colisão entre números preservados e gerados dentro do mesmo job.
- 8.4 O número é imutável após a criação e nunca é reutilizado.

**Cross-ref:** Req 6; PBT-05; RN-001; data-model.md § uq_opportunity_number_per_tenant; ADR-0003

### Req 9 — Transformação de datas serial Excel para ISO

**Como** sistema **quero** converter datas no formato serial do Excel para datas ISO **para** persistir corretamente as datas de fechamento esperadas.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | azim-product-spec.md § Parte V |
| **Módulo** | data-migration |

**Critérios de Aceite:**

- 9.1 `Data Esperada Fechamento` em serial Excel é convertida para data ISO usando o epoch 1899-12-30.
- 9.2 Datas no passado são mantidas e sinalizadas com flag "vencida" no relatório.
- 9.3 Células vazias resultam em `data_fechamento_esperada` nula, sem erro de import.

**Cross-ref:** Req 3; PBT-03; data-model.md § opportunities (data_fechamento_esperada)

### Req 10 — Import de atividades da aba Ações Comerciais

**Como** sistema **quero** importar as ações comerciais como atividades vinculadas **para** preservar o histórico acionável e alimentar o digest diário.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Should |
| **Origem** | azim-product-spec.md § Parte V (aba Ações Comerciais) |
| **Módulo** | data-migration |

**Critérios de Aceite:**

- 10.1 Cada linha da aba "Ações Comerciais" gera uma `Activity` com tipo, data planejada, status e prioridade mapeados.
- 10.2 O vínculo `Relacionada ao Pipeline (#)` é resolvido como FK real para a oportunidade pela numeração preservada (Req 8).
- 10.3 Typos conhecidos em nomes de responsável (ex.: "Miilton" → usuário Milton) são corrigidos via mapa nome→usuário.
- 10.4 Linhas cujo número relacionado não encontra oportunidade correspondente são registradas como `aviso`, sem abortar a transação por si só.

**Cross-ref:** Req 6, Req 8; data-model.md § Activity

### Req 11 — Relatório final auditado e evento de conclusão

**Como** Tenant Admin **quero** um relatório final auditável após o import **para** comprovar o que foi migrado e dar baixa na operação de planilha.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-migration-03; README § Eventos; NFR-AUD |
| **Módulo** | data-migration |

**Critérios de Aceite:**

- 11.1 Após import bem-sucedido, um relatório final é disponibilizado para download contendo contagens importadas por entidade, número de flags resolvidos e divergências de forecast registradas.
- 11.2 A conclusão publica o evento `ImportCompleted`, consumido pelo módulo audit-log.
- 11.3 O registro de auditoria do import é append-only e não pode ser alterado após a emissão.

**Cross-ref:** Req 6; RNF 4; FRD-migration-03; README § Eventos Publicados; `.forge/rules/domain/audit-immutability.md`

### Req 12 — Idempotência do import

**Como** Platform Operator **quero** que reexecutar o import não duplique dados **para** permitir nova tentativa segura após falha sem efeitos cumulativos.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | RN-023; NFR-RES-02; decisão arquitetural (rollback + nova tentativa) |
| **Módulo** | data-migration |

**Critérios de Aceite:**

- 12.1 Um job que terminou em `rolled_back` pode ser reexecutado sem gerar registros parciais remanescentes da tentativa anterior.
- 12.2 Reexecutar o import a partir do mesmo conjunto de dados triados não cria contas, contatos, parceiros, oportunidades ou atividades duplicados.
- 12.3 Números de oportunidade preservados não são reatribuídos nem duplicados em reexecuções.

**Cross-ref:** Req 6, Req 8; PBT-02; NFR-RES-02

### Req 13 — Congelamento da planilha de origem

**Como** Tenant Admin **quero** congelar a planilha após o import **para** evitar divergência entre a fonte antiga e o Azim.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | PRD PRM-05 / PRE-05; azim-product-spec.md § Parte V |
| **Módulo** | data-migration |

**Critérios de Aceite:**

- 13.1 Após o import bem-sucedido, o relatório final orienta o congelamento da planilha (read-only no OneDrive) com banner apontando para o Azim.
- 13.2 O congelamento é instrução operacional registrada na auditoria; o módulo não altera o arquivo de origem.

**Cross-ref:** Req 11; PRD PRM-05, PRE-05

### Req 14 — Ciclo de vida temporário do módulo

**Como** Arquiteto **quero** que o módulo seja removível após a Fase 1 **para** manter a higiene arquitetural conforme decisão de validação.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Should |
| **Origem** | DDD-VAL-03 / VAL-MOD-04; VAL-MIGR-01; README § Pontos a Validar |
| **Módulo** | data-migration |

**Critérios de Aceite:**

- 14.1 O módulo e as tabelas `migration_jobs` e `migration_logs` são candidatos a arquivamento/remoção após a confirmação do import da Fase 1.
- 14.2 A remoção do módulo não pode deixar dependências ativas nos módulos de domínio (accounts, partners, opportunities, activities).
- 14.3 A decisão de remoção é registrada e rastreável (VAL-MIGR-01).

**Cross-ref:** README § 20 VAL-MIGR-01; DDD Segmentation § 11 DDD-VAL-03

## 6. Requisitos Não-Funcionais

### RNF 1 — Desempenho do dry-run e do import

| Campo | Valor |
|-------|-------|
| **Categoria** | Performance |
| **Prioridade** | Must |
| **Origem** | NFR-PERF-06; FRD-migration-01/03; README § 15 |
| **Módulo** | data-migration |

**Descrição:**

O dry-run e o import transacional devem completar dentro de janela de tempo definida, excluindo a etapa manual de triagem assistida.

**Critérios de Aceite:**

- RNF-1.1 O dry-run de 108 oportunidades (volume real da Vellus) conclui em ≤ 5 minutos.
- RNF-1.2 O import transacional de até 500 oportunidades conclui em ≤ 5 minutos, medido por timestamp de início e término no log estruturado.
- RNF-1.3 O teste de ≤ 5 minutos é validado em ambiente de staging com os volumes de 108 e 500 linhas, com rollback total testado explicitamente.

**Cross-ref:** NFR-PERF-06; PTV-01

### RNF 2 — Isolamento por tenant

| Campo | Valor |
|-------|-------|
| **Categoria** | Segurança |
| **Prioridade** | Must |
| **Origem** | decisão arquitetural (RLS por tenant_id); data-model.md § Tenancy |
| **Módulo** | data-migration |

**Descrição:**

Toda escrita e leitura do import ocorrem no contexto do tenant da operação (Vellus, nº 1), sem vazamento entre tenants.

**Critérios de Aceite:**

- RNF-2.1 Todos os registros criados (accounts, contacts, partners, opportunities, activities) carregam o `tenant_id` da Vellus.
- RNF-2.2 `migration_jobs` e `migration_logs` são isolados por `tenant_id`; um job não acessa dados de outro tenant.
- RNF-2.3 A numeração `AZ-NNNN` é única por tenant, não global.

**Cross-ref:** Req 8; data-model.md § RLS; ADR-0003

### RNF 3 — PII ausente dos logs de migração

| Campo | Valor |
|-------|-------|
| **Categoria** | Privacidade |
| **Prioridade** | Must |
| **Origem** | NFR-PRIV-01; RN-025; README RISK-MIGR-03; LGPD |
| **Módulo** | data-migration |

**Descrição:**

Os logs de migração não devem conter dados pessoais (nome, e-mail, telefone) de contatos. PII deve ser mascarada em logs e ausente de mensagens de erro.

**Critérios de Aceite:**

- RNF-3.1 `migration_logs` registra apenas índice da linha, status e mensagem técnica; nunca nome, e-mail ou telefone de contato.
- RNF-3.2 Mensagens de erro retornadas ao operador não expõem PII; referenciam a linha por índice.
- RNF-3.3 Auditoria do import não confunde anonimização com deleção; LGPD by design é aplicado aos dados pessoais importados.

**Cross-ref:** Req 7; NFR-PRIV-01; RN-025; `.forge/rules/architecture/security-and-compliance.md`

### RNF 4 — Auditoria append-only do import

| Campo | Valor |
|-------|-------|
| **Categoria** | Auditoria |
| **Prioridade** | Must |
| **Origem** | NFR-AUD; RN-023; README § Eventos |
| **Módulo** | data-migration |

**Descrição:**

O resultado do import deve ser auditável e imutável após a emissão, com rastreabilidade por linha processada.

**Critérios de Aceite:**

- RNF-4.1 O evento `ImportCompleted` gera registro de auditoria append-only no módulo audit-log.
- RNF-4.2 Os `migration_logs` por linha permitem reconstruir o que foi processado, com status e mensagem.
- RNF-4.3 Registros de auditoria do import não admitem UPDATE nem DELETE após emissão.

**Cross-ref:** Req 11; `.forge/rules/domain/audit-immutability.md`; `.forge/rules/architecture/observability.md`

### RNF 5 — Resiliência e atomicidade da transação

| Campo | Valor |
|-------|-------|
| **Categoria** | Resiliência |
| **Prioridade** | Must |
| **Origem** | RN-023; NFR-RES-01; README RISK-MIGR-02 |
| **Módulo** | data-migration |

**Descrição:**

O import deve ser atômico: qualquer falha resulta em rollback total, sem dados parciais, e nova tentativa segura.

**Critérios de Aceite:**

- RNF-5.1 Falha em qualquer passo do import dispara rollback total da transação única do Postgres; nenhum registro de domínio é persistido.
- RNF-5.2 O cenário de rollback é testado em staging com o volume real de 108 linhas antes do go-live.
- RNF-5.3 Após rollback, o operador pode reexecutar sem efeitos cumulativos (ver Req 12).

**Cross-ref:** Req 6, Req 12; PBT-01, PBT-02; NFR-RES-01

### RNF 6 — Observabilidade do processo

| Campo | Valor |
|-------|-------|
| **Categoria** | Observabilidade |
| **Prioridade** | Should |
| **Origem** | README § 17; NFR-OBS |
| **Módulo** | data-migration |

**Descrição:**

O processo de migração deve expor métricas e logs estruturados suficientes para acompanhar progresso e diagnosticar falhas, sem PII.

**Critérios de Aceite:**

- RNF-6.1 São expostas as métricas `migration_rows_processed_total`, `migration_rows_failed_total` e `migration_duration_seconds`.
- RNF-6.2 Há log estruturado por linha com índice, status e mensagem (sem PII).
- RNF-6.3 Ao final da execução, o PlatOp é notificado via log com o resultado consolidado.

**Cross-ref:** RNF 3; README § 17; `.forge/rules/architecture/observability.md`

## 7. Property-Based Testing

### PBT-01 — Atomicidade do import

**Mapeia para:** Req 6, RNF 5
**Tipo:** Atomicidade

**Propriedade:**

> Para qualquer conjunto de linhas triadas em que ao menos uma linha provoque falha em qualquer passo, após a execução do import o estado do banco é idêntico ao estado anterior (zero registros de domínio persistidos).

### PBT-02 — Idempotência da reexecução

**Mapeia para:** Req 12, RNF 5
**Tipo:** Idempotência

**Propriedade:**

> Para qualquer conjunto de dados triados sem pendências obrigatórias, executar o import uma vez com sucesso e reexecutá-lo a partir do mesmo conjunto não altera a contagem de registros criados por entidade (nenhuma duplicação).

### PBT-03 — Round-trip de datas serial Excel ↔ ISO

**Mapeia para:** Req 9
**Tipo:** Round-trip

**Propriedade:**

> Para qualquer serial Excel válido (epoch 1899-12-30), converter para ISO e de volta para serial reproduz o serial original; valores vazios produzem data nula sem erro.

### PBT-04 — Conservação de contagem no import

**Mapeia para:** Req 2, Req 7
**Tipo:** Invariante matemática

**Propriedade:**

> Para qualquer planilha válida, o número de oportunidades criadas no import é igual ao número de linhas de oportunidade válidas; o número de contas criadas é igual ao número de nomes normalizados distintos após dedupe.

### PBT-05 — Preservação e unicidade do número AZ-NNNN

**Mapeia para:** Req 8, RNF 2
**Tipo:** Anti-enumeração / Invariante

**Propriedade:**

> Para qualquer conjunto de oportunidades importadas, os números preservados permanecem inalterados, os números gerados começam no próximo livre da sequência do tenant (≥ 95) e o conjunto final de `opportunity_number` é único por tenant, sem colisão entre preservados e gerados.

### PBT-06 — Detecção de divergência de forecast recalculado

**Mapeia para:** Req 2
**Tipo:** Invariante matemática

**Propriedade:**

> Para qualquer oportunidade, o forecast recalculado é `round_to_even(valor_total × probabilidade / 100)` em centavos; toda divergência superior a 1 centavo (R$ 0,01) em relação ao `Forecast (R$)` da planilha é, e somente ela é, listada no relatório.

### PBT-07 — Invariante de owner obrigatório antes do import

**Mapeia para:** Req 4
**Tipo:** State machine

**Propriedade:**

> Para qualquer estado de triagem, o job só pode transitar para `importing` se nenhuma oportunidade estiver sem owner; existindo ao menos uma oportunidade sem owner, a transição é rejeitada e o estado permanece em `triage_in_progress`.

## 8. Glossário local

| Termo | Definição |
|-------|-----------|
| dry-run | Execução simulada do import sem qualquer escrita no banco; produz relatório de contagens, dedupes, flags e divergências. |
| triagem assistida | Revisão e resolução humana das pendências do dry-run antes de confirmar o import. |
| import transacional | Import real executado em uma única transação de banco com rollback total em caso de falha (RN-023). |
| rollback total | Reversão completa da transação: nenhum registro de domínio é persistido se houver qualquer erro. |
| congelamento | Instrução operacional para tornar a planilha de origem read-only após o import bem-sucedido. |
| flag de triagem | Marcação de pendência (obrigatória ou opcional) atribuída a um registro no dry-run. |
| serial Excel | Representação numérica de data no Excel; epoch 1899-12-30. |
| migration_job | Registro de uma execução (tentativa) de migração e seu estado. |
| migration_log | Registro por linha processada da planilha: status (ok/aviso/erro) e mensagem técnica. |
| forecast ponderado | `valor_total × probabilidade / 100`, em centavos inteiros; recalculado pelo sistema. |

> Objetos de valor citados (número de oportunidade, valor monetário) são definidos no glossário de domínio (ubiquitous-language.md). Identificadores técnicos em inglês; documento em pt-BR.

## 9. Fora do escopo do MVP

- Import contínuo, incremental ou ETL recorrente (módulo é de uso único na Fase 1).
- Import de formatos além de `.xlsx`.
- Dedupe automática de contas sem revisão humana (a triagem é manual — RN-014).
- Import parcial por BU (VAL-10 — a confirmar com engenharia; se adotado, cada import parcial deve permanecer atômico).
- Migração de histórico de atividades além das ações comerciais ativas (VAL-MIGR-02 — a confirmar com produto).
- Cadastro dos percentuais de comissão dos parceiros (VAL-01 — preenchidos pela Vellus pós-import).
- Reconciliação planilha↔Azim após o congelamento.

## 10. Referências cruzadas

| Referência | Onde |
|-----------|------|
| MOD-14 / FRD-migration-01..03 | docs/product/frd-nfrd/frd.md |
| Mapeamento de colunas da planilha | docs/product/azim-product-spec.md § Parte V |
| Processo de migração e congelamento | docs/product/prd/prd.md § Parte V; PRM-05/PRE-05 |
| RN-001 (numeração), RN-002 (owner), RN-014 (dedupe), RN-023 (rollback), RN-025 (PII) | docs/product/frd-nfrd/frd.md § Regras de Negócio |
| NFR-PERF-06, NFR-RES-01/02, NFR-PRIV-01, NFR-AUD | docs/product/frd-nfrd/nfrd.md |
| Tabelas migration_jobs / migration_logs | docs/product/data-model/data-model.md § Data Migration (BC-09) |
| Entidades de destino (accounts, contacts, partners, opportunities, activities) | docs/product/data-model/data-model.md |
| Glossário de domínio | docs/product/glossary/ubiquitous-language.md |
| README do módulo (riscos, eventos, pontos a validar) | docs/product/modules/data-migration/README.md |
| Money em centavos / arredondamento NBR 5891 | `.forge/rules/domain/money-as-cents.md`; `.forge/rules/domain/nbr-5891-rounding.md` |
| Auditoria imutável / observabilidade | `.forge/rules/domain/audit-immutability.md`; `.forge/rules/architecture/observability.md` |
