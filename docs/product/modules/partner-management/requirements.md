# PM — Partner Management
**Requisitos Funcionais e Não-Funcionais**

- Versão: 1.0.0
- Data: 2026-06-11
- Status: Rascunho para revisão
- Referência pai: docs/product/frd-nfrd/frd.md § FRD-partner-01 e FRD-partner-02 (RF-05)

## Histórico de Versões

| Versão | Data | Status | Descrição da alteração |
|--------|------|--------|------------------------|
| 1.0.0 | 2026-06-11 | Rascunho para revisão | Criação inicial do documento a partir do README do módulo, FRD (RF-05), NFRD (NFR-SEG, NFR-AUD), TRD (partner-management) e data-model (tabela partners) |

## 1. Visão Geral

O módulo **Partner Management** (Bounded Context BC-03, subdomínio de suporte — _Supporting Subdomain_) é responsável pela gestão dos parceiros comissionados do tenant. Mantém o cadastro de parceiros com papel tipado (_partner_type_) e percentuais de comissão padrão por componente do contrato (setup e recorrente). É o _upstream_ do módulo `opportunity-pipeline`, que consome os dados do parceiro ao vincular um parceiro a uma oportunidade e ao herdar os percentuais padrão.

No MVP (Fase 1), o parceiro **não possui credencial de acesso** à plataforma: é uma entidade de dados gerida exclusivamente pelo tenant (RN-021, DEC-012). O módulo também expõe a visão de comissão do parceiro — comissão projetada (oportunidades abertas) e comissão consolidada (oportunidades ganhas, com base em _snapshot_ imutável) por período. Os números de comissão **não são calculados nem persistidos por este módulo**: eles provêm de um _read model_ do `opportunity-pipeline` (ver dependência declarada na seção 5, Req 9 e Req 10).

O _deployable_ candidato é o `azim-api`. Os dados próprios do módulo residem na tabela `partners` (Cloud SQL / Postgres), isolada por tenant.

## 2. Escopo

### 2.1 Incluído

- CRUD (criar, ler, atualizar) de parceiros com nome, papel tipado e percentuais padrão por componente.
- Inativação e reativação lógica de parceiro (_soft-delete_ via `active`), sem exclusão física.
- Gestão de contato do parceiro (dados de contato comercial do parceiro).
- Exposição de dados do parceiro (`partner_id`, `pct_setup`, `pct_recorrente`, status) para o `opportunity-pipeline` na vinculação.
- Bloqueio de vinculação de parceiro inativo a novas oportunidades.
- Visão de comissão do parceiro: oportunidades originadas, comissão projetada e comissão consolidada por período, compondo o _read model_ do `opportunity-pipeline`.
- Marcação de pendência de preenchimento de percentuais pós-importação (triagem — LAC-01/VAL-01).
- Auditoria append-only de toda escrita em parceiros.
- Isolamento por tenant.

### 2.2 Excluído

- Cálculo e _snapshot_ de comissão por oportunidade — pertence ao `opportunity-pipeline` (RN-026, RN-007, DEC-002).
- Pagamento de comissão ao parceiro — fora do escopo do sistema.
- Persistência do valor de comissão neste módulo — este módulo apenas lê o _read model_ do pipeline.

### 2.3 Fora do escopo do MVP

Ver seção 9.

## 3. Personas / Atores

| Código | Ator / Persona | Tipo | Papel neste módulo |
|--------|----------------|------|--------------------|
| ACT-04 | Administrador do Tenant (Tenant Admin / TAdmin) | Usuário de configuração | Cria, edita, inativa/reativa parceiros; configura percentuais padrão; consulta visão e relatório de comissão |
| ACT-02 | Gestor de BU (GestorBU) | Usuário gerencial | Edita parceiros de sua atuação; consulta visão e relatório de comissão |
| ACT-01 | Vendedor / Executivo de Contas | Usuário primário | Lê a lista de parceiros ativos para vincular parceiro à oportunidade (consumo via pipeline) |
| — | Viewer | Usuário de leitura | Lista parceiros (somente leitura) |
| ACT-06 | Parceiro comissionado | Ator externo (entidade de dados) | Entidade gerida pelo tenant; **sem login no MVP** (RN-021, DEC-012) |
| — | `opportunity-pipeline` | Sistema (módulo consumidor) | Consome `partner_id` e percentuais padrão na vinculação; provê _read model_ de comissão |
| — | `data-migration` | Sistema (módulo consumidor) | Cria parceiros via API interna durante o import transacional da planilha |
| — | `reporting` | Sistema (_read model_) | Lê `partners` para compor relatório de comissões |
| — | `audit-log` | Sistema (_package_) | Recebe AuditEvent de toda escrita em parceiros |

## 4. Listas Canônicas

### 4.1 Lista canônica de papéis de parceiro (`partner_type`)

O papel do parceiro classifica sua atuação no processo comercial. O conjunto canônico de seed para o MVP é:

| Valor (`partner_type`) | Descrição |
|------------------------|-----------|
| Indicador | Parceiro que apenas indica/origina oportunidades |
| Revendedor | Parceiro que revende a solução ao cliente final |
| Distribuidor | Parceiro que distribui a solução em escala / canal |
| Integrador | Parceiro que integra e implanta a solução no cliente |

Observações:

- A lista de papéis é configurável pelo tenant (ver glossário, BC-03). Os quatro valores acima são o seed canônico inicial; o tenant pode estender ou ajustar a lista.
- O papel é informativo/classificatório e **não** altera por si o cálculo de comissão — o cálculo depende dos percentuais padrão do parceiro (seção 4.2) herdados pela oportunidade.

### 4.2 Lista canônica de status do parceiro (`active`)

| Valor (`active`) | Significado | Efeito |
|------------------|-------------|--------|
| `true` (ativo) | Parceiro habilitado | Pode ser vinculado a novas oportunidades |
| `false` (inativo) | Parceiro desativado (_soft-delete_) | **Não** pode ser vinculado a novas oportunidades; preserva histórico e vínculos existentes |

### 4.3 Unidade de percentuais e valores monetários

| Grandeza | Unidade canônica | Observação |
|----------|------------------|------------|
| `pct_setup`, `pct_recorrente` | Percentual com 2 casas decimais, no intervalo fechado [0,00; 100,00] | Ex.: `10.00` representa 10%. Persistido como `NUMERIC(5,2)` (data-model §BC-03). Identificadores conceituais: `comissao_default_setup_pct`, `comissao_default_recorrente_pct` |
| Valores monetários de comissão (resultado do cálculo) | Centavos inteiros (`long` / `BIGINT` conceitual) | Calculados pelo `opportunity-pipeline`; nunca por este módulo. Apresentação em BRL é responsabilidade do frontend. Nunca usar `float`/`double` para cálculo monetário |

## 5. Requisitos Funcionais

### Req 1 — Cadastrar parceiro

**Como** Tenant Admin **quero** cadastrar um parceiro com nome, papel tipado e percentuais padrão de comissão **para** disponibilizá-lo para vinculação em oportunidades e para o cálculo de comissão.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD RF-05 / FRD-partner-01; README §4 |
| **Módulo** | partner-management |

**Critérios de Aceite:**

- 1.1 O sistema permite criar um parceiro informando `name` (obrigatório, não vazio).
- 1.2 O sistema exige um `partner_type` pertencente à lista canônica de papéis (seção 4.1).
- 1.3 O sistema aceita `pct_setup` e `pct_recorrente`, cada um no intervalo [0,00; 100,00]; quando não informados, assumem `0,00` (default).
- 1.4 O sistema aceita campo opcional `notes`.
- 1.5 O parceiro criado nasce com `active = true`.
- 1.6 Parceiro criado aparece na seleção de parceiro ao criar/editar oportunidade (consumo via `opportunity-pipeline`).
- 1.7 Nome de parceiro duplicado no tenant gera alerta (MSG-021), permitindo confirmação explícita de criação com nome idêntico se intencional — não bloqueia.
- 1.8 A criação gera AuditEvent (ver RNF 2) e o evento de domínio `PartnerCreated`.

**Cross-ref:** Req 5 (papel tipado), Req 6 (percentuais), RF-05; FRD-partner-01; data-model §BC-03 (tabela `partners`)

### Req 2 — Editar parceiro

**Como** Tenant Admin ou Gestor de BU **quero** editar os dados de um parceiro **para** corrigir o cadastro e atualizar os percentuais padrão de comissão.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD RF-05 / FRD-partner-01; TRD §8.4 (PATCH `/api/v1/partners/{id}`) |
| **Módulo** | partner-management |

**Critérios de Aceite:**

- 2.1 O sistema permite atualizar `name`, `partner_type`, `pct_setup`, `pct_recorrente` e `notes`.
- 2.2 As mesmas validações de domínio da criação aplicam-se à edição (papel canônico; percentuais em [0,00; 100,00]; nome não vazio).
- 2.3 A edição dos percentuais padrão **não** altera a comissão consolidada de oportunidades já ganhas (a comissão consolidada usa _snapshot_ imutável — ver Req 9 e PBT-05).
- 2.4 A edição dos percentuais padrão passa a valer para vinculações futuras; pode alterar a comissão projetada de oportunidades abertas (ver Req 9).
- 2.5 Toda edição gera AuditEvent com autor, _delta_ e timestamp (ver RNF 2).

**Cross-ref:** Req 6, Req 9, PBT-05; FRD-partner-01; NFR-AUD-01

### Req 3 — Inativar e reativar parceiro

**Como** Tenant Admin **quero** inativar (e reativar) um parceiro sem excluí-lo fisicamente **para** impedir novas vinculações de parceiros que não estão mais ativos, preservando o histórico.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | README §4 e §19 (RISK-PARTNER-01); data-model §BC-03 (`active`) |
| **Módulo** | partner-management |

**Critérios de Aceite:**

- 3.1 O sistema permite alternar `active` entre `true` e `false` (_soft-delete_); nunca há exclusão física do registro.
- 3.2 Parceiro com `active = false` não aparece na lista de parceiros disponíveis para vinculação a novas oportunidades (ver Req 4 e Req 8).
- 3.3 Inativar um parceiro **não** remove nem altera vínculos existentes em oportunidades já criadas, nem comissões já consolidadas.
- 3.4 A reativação restaura a disponibilidade do parceiro para novas vinculações.
- 3.5 A operação é idempotente: inativar um parceiro já inativo (ou reativar um já ativo) não produz efeito adicional além do registro de auditoria correspondente (ver PBT-02).
- 3.6 Inativação e reativação geram AuditEvent.

**Cross-ref:** Req 4, Req 8, PBT-02; README §19 (RISK-PARTNER-01)

### Req 4 — Listar parceiros para seleção

**Como** Vendedor (via `opportunity-pipeline`) **quero** obter a lista de parceiros ativos do tenant **para** vincular um parceiro à oportunidade e herdar seus percentuais padrão.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD RF-05; TRD §8.4 (GET `/api/v1/partners`); README §9 e §13 |
| **Módulo** | partner-management |

**Critérios de Aceite:**

- 4.1 O sistema retorna, no mínimo, `partner_id`, `name`, `partner_type`, `pct_setup` e `pct_recorrente` dos parceiros do tenant.
- 4.2 Por padrão, a lista para vinculação retorna apenas parceiros com `active = true`.
- 4.3 Os percentuais padrão retornados são pré-preenchidos na oportunidade ao associar o parceiro (consumo pelo `opportunity-pipeline`).
- 4.4 A lista é restrita ao tenant do solicitante (ver RNF 1 — isolamento).
- 4.5 Papel Viewer pode listar parceiros em modo somente leitura.

**Cross-ref:** Req 1, Req 8; FRD-partner-01; TRD §8.4; RNF 1

### Req 5 — Papel tipado canônico do parceiro

**Como** Tenant Admin **quero** classificar o parceiro por um papel tipado **para** organizar os parceiros por sua atuação no processo comercial.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD-partner-01 (papel tipado: Distribuidor, Indicador, Revenda); glossário BC-03 (`partner_type`) |
| **Módulo** | partner-management |

**Critérios de Aceite:**

- 5.1 Todo parceiro possui exatamente um `partner_type`.
- 5.2 O `partner_type` deve pertencer à lista canônica de papéis vigente no tenant (seed: Indicador, Revendedor, Distribuidor, Integrador — seção 4.1).
- 5.3 Tentar gravar um `partner_type` fora da lista vigente é rejeitado.
- 5.4 O papel é classificatório e não altera, por si só, o cálculo de comissão.

**Cross-ref:** Req 1, seção 4.1; glossário BC-03

### Req 6 — Percentuais padrão de comissão por componente

**Como** Tenant Admin **quero** definir percentuais padrão de comissão por componente do contrato (setup e recorrente) **para** que sejam herdados pela oportunidade ao vincular o parceiro.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD RF-05 / FRD-partner-01; glossário BC-03 (`CommissionDefaults`); data-model §BC-03 |
| **Módulo** | partner-management |

**Critérios de Aceite:**

- 6.1 O parceiro possui `pct_setup` (`comissao_default_setup_pct`) — percentual padrão sobre o componente de setup do contrato.
- 6.2 O parceiro possui `pct_recorrente` (`comissao_default_recorrente_pct`) — percentual padrão sobre o componente recorrente do contrato.
- 6.3 Cada percentual é validado no intervalo fechado [0,00; 100,00], com precisão de 2 casas decimais; valores negativos ou acima de 100 são rejeitados.
- 6.4 Os dois percentuais compõem o objeto de valor `CommissionDefaults`, herdado pela oportunidade na vinculação (não persistido de forma duplicada — a herança é do pipeline).
- 6.5 A unidade dos percentuais é documentada (seção 4.3) e preservada em leitura e escrita sem perda de precisão (ver PBT-03).
- 6.6 Os valores monetários resultantes do cálculo de comissão são expressos em centavos inteiros e calculados pelo `opportunity-pipeline`, nunca por este módulo.

**Cross-ref:** Req 9, PBT-03, RNF 6; glossário BC-03; `.forge/rules/domain/money-as-cents.md`

### Req 7 — Gerir contato do parceiro

**Como** Tenant Admin **quero** registrar e atualizar os dados de contato comercial do parceiro **para** ter o canal de comunicação do parceiro associado ao cadastro.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Should |
| **Origem** | Insumo do módulo (contato do parceiro); README §16 e §19 (RISK-PARTNER-02 / VAL-PARTNER-01) |
| **Módulo** | partner-management |

**Critérios de Aceite:**

- 7.1 O sistema permite registrar dados de contato do parceiro (ex.: e-mail e/ou telefone comercial) associados ao parceiro.
- 7.2 E-mail de contato com formato inválido é rejeitado.
- 7.3 Dados de contato do parceiro que possam constituir dado pessoal (PII) não são incluídos em logs de sistema nem em mensagens de erro (ver RNF 4).
- 7.4 Toda escrita de contato do parceiro gera AuditEvent.

**Cross-ref:** RNF 4; README §16 (LGPD), §19 (RISK-PARTNER-02), §20 (VAL-PARTNER-01)

### Req 8 — Bloquear vinculação de parceiro inativo

**Como** sistema (na fronteira com o `opportunity-pipeline`) **quero** impedir que um parceiro inativo seja vinculado a uma nova oportunidade **para** evitar comissão calculada para parceiro sem contrato ativo.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | README §4 e §19 (RISK-PARTNER-01) |
| **Módulo** | partner-management |

**Critérios de Aceite:**

- 8.1 O módulo expõe o status (`active`) do parceiro para que o `opportunity-pipeline` valide a elegibilidade na vinculação.
- 8.2 Uma tentativa de vincular parceiro com `active = false` a uma nova oportunidade é rejeitada na fronteira do pipeline.
- 8.3 A inativação de um parceiro não invalida vínculos já existentes em oportunidades anteriores.

**Cross-ref:** Req 3, Req 4; README §19 (RISK-PARTNER-01); RN-008

### Req 9 — Exibir visão de comissão do parceiro (projetada × consolidada)

**Como** Tenant Admin ou Gestor de BU **quero** ver, por parceiro, as oportunidades originadas, a comissão projetada (pipeline aberto) e a comissão consolidada (oportunidades ganhas) por período **para** acompanhar a comissão em andamento e a realizada.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD RF-05 / FRD-partner-02; glossário BC-03 (`comissao_projetada`, `comissao_consolidada`) |
| **Módulo** | partner-management |

**Critérios de Aceite:**

- 9.1 A visão exibe as oportunidades originadas pelo parceiro no período selecionado.
- 9.2 A comissão **projetada** é a soma das comissões calculadas das oportunidades **abertas** do parceiro, usando os percentuais atuais (sem _snapshot_).
- 9.3 A comissão **consolidada** é a soma dos _snapshots_ imutáveis das oportunidades **ganhas** do parceiro.
- 9.4 Os números de comissão provêm de um _read model_ do `opportunity-pipeline` (`opportunity_partner_commissions`); este módulo **não** recalcula nem duplica a fórmula de comissão.
- 9.5 A comissão consolidada não muda quando os percentuais padrão do parceiro são editados após o fechamento (ver Req 2.3 e PBT-05).
- 9.6 A comissão projetada pode variar se os percentuais padrão do parceiro forem editados (oportunidades abertas).
- 9.7 O filtro de período funciona corretamente nas duas visões (projetada e consolidada).
- 9.8 Os valores são apresentados formatados em BRL; o cálculo subjacente é em centavos inteiros.

**Cross-ref:** Req 2, Req 6, Req 10, PBT-01, PBT-05; FRD-partner-02; RN-026, RN-007; TRD §11.x (`CommissionReport`)

### Req 10 — Relatório de comissões por parceiro por período

**Como** Tenant Admin ou Gestor de BU **quero** um relatório de comissões por parceiro **para** consolidar parceiro × oportunidades × comissão projetada e consolidada no período.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD RF-05/RF-11 (FRD-report-05); TRD §8.4 (GET `/api/v1/partners/{id}/commissions`), FLOW-08 |
| **Módulo** | partner-management |

**Critérios de Aceite:**

- 10.1 O relatório lista, por parceiro, as oportunidades vinculadas e os valores de comissão projetada e consolidada no período.
- 10.2 O relatório compõe dados do cadastro de parceiros (`partners`) com o _read model_ de comissão do `opportunity-pipeline` (`opportunity_partner_commissions`); não recalcula a comissão.
- 10.3 O acesso ao relatório é restrito a Tenant Admin e Gestor de BU (ver RNF 1).
- 10.4 Os valores monetários do relatório derivam de centavos inteiros e são apresentados em BRL.

**Cross-ref:** Req 9; FRD-report-05; TRD §8.4 e FLOW-08; RNF 1

### Req 11 — Marcar percentuais pendentes de preenchimento pós-importação

**Como** Tenant Admin **quero** identificar parceiros importados sem percentuais de comissão definidos **para** completar o cadastro na triagem, já que os percentuais dos parceiros reais da Vellus não existem na planilha de origem.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | Pendência LAC-01 / VAL-01 (percentuais dos 11 parceiros reais da Vellus inexistentes na planilha; preenchidos pós-import); FRD-partner-01 (flag de triagem no dry-run) |
| **Módulo** | partner-management |

**Critérios de Aceite:**

- 11.1 Parceiro criado via `data-migration` sem percentuais definidos é aceito com `pct_setup` e `pct_recorrente` em `0,00` e marcado como pendente de triagem.
- 11.2 O _dry-run_ da migração sinaliza com _flag_ de triagem os parceiros sem percentuais definidos.
- 11.3 A visão de parceiros permite identificar os parceiros pendentes de definição de percentuais para preenchimento manual pós-import.
- 11.4 Enquanto os percentuais permanecerem em `0,00`, a comissão projetada calculada para as oportunidades desses parceiros é zero — comportamento esperado até o preenchimento.

**Cross-ref:** Req 6, Req 1; FRD-partner-01; pendência LAC-01/VAL-01; README §13 (data-migration)

### Req 12 — Parceiro sem credencial de acesso no MVP

**Como** produto **quero** que o parceiro seja uma entidade de dados gerida pelo tenant, sem login na plataforma **para** manter a fronteira de escopo do MVP (sem portal do parceiro).

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | RN-021; DEC-012; README §5 e §20 (VAL-PARTNER-02) |
| **Módulo** | partner-management |

**Critérios de Aceite:**

- 12.1 Nenhuma identidade é provisionada no Identity Platform para o parceiro.
- 12.2 O parceiro não possui qualquer fluxo de autenticação ou autoatendimento no MVP.
- 12.3 Toda gestão do parceiro (CRUD, percentuais, contato) é realizada por usuários do tenant com papel adequado (ver RNF 1).

**Cross-ref:** seção 9 (Fora do escopo do MVP); RN-021; DEC-012; README §20 (VAL-PARTNER-02)

## 6. Requisitos Não-Funcionais

### RNF 1 — Autorização RBAC e isolamento por tenant na gestão de parceiros

| Campo | Valor |
|-------|-------|
| **Categoria** | Segurança |
| **Prioridade** | Must |
| **Origem** | NFR-SEG-01; NFR-SEG-03; README §15; data-model §BC-03 (`tenant_id`) |
| **Módulo** | partner-management |

**Descrição:**

Toda operação de escrita em parceiros (criar, editar, inativar/reativar, gerir contato) deve ser restrita a Tenant Admin e Gestor de BU; a leitura segue a matriz RBAC. Todos os dados de parceiro são isolados por tenant em profundidade (filtro de aplicação + RLS no Postgres), de modo que um tenant nunca acesse parceiros de outro.

**Critérios de Aceite:**

- RNF-1.1 Criar e editar parceiro: exclusivo de Tenant Admin e Gestor de BU; demais papéis recebem 403.
- RNF-1.2 Listar parceiros: permitido a Viewer e papéis superiores, sempre restrito ao tenant.
- RNF-1.3 Relatório de comissões: restrito a Tenant Admin e Gestor de BU.
- RNF-1.4 Requisição de tenant A nunca retorna nem afeta parceiros de tenant B (bloqueio na API e na RLS), verificável por teste de isolamento em CI (ver PBT-04).

**Cross-ref:** NFR-SEG-01, NFR-SEG-03; PBT-04; `.forge/rules/architecture/security-and-compliance.md`

### RNF 2 — Auditoria append-only de toda escrita em parceiros

| Campo | Valor |
|-------|-------|
| **Categoria** | Auditoria |
| **Prioridade** | Must |
| **Origem** | NFR-AUD-01; README §4, §15 e §17 |
| **Módulo** | partner-management |

**Descrição:**

Toda operação de escrita em entidade de parceiro deve gerar exatamente um registro imutável (append-only) de auditoria, contendo autor, _delta_ e timestamp, sem possibilidade de alteração ou exclusão. O evento de domínio `PartnerCreated` é publicado na criação.

**Critérios de Aceite:**

- RNF-2.1 Criar, editar, inativar e reativar parceiro geram cada um exatamente 1 registro de auditoria.
- RNF-2.2 Registros de auditoria não podem ser alterados nem excluídos pela aplicação (imutabilidade enforced no banco).
- RNF-2.3 Cada registro contém autor, _delta_ (antes/depois) e timestamp.
- RNF-2.4 A criação de parceiro publica o evento `PartnerCreated` consumido por `audit-log`.

**Cross-ref:** NFR-AUD-01; `.forge/rules/domain/audit-immutability.md`; README §10

### RNF 3 — Retenção indefinida dos dados de auditoria de parceiros

| Campo | Valor |
|-------|-------|
| **Categoria** | Auditoria |
| **Prioridade** | Should |
| **Origem** | NFR-AUD-03; ADR-0003 (sugerido) |
| **Módulo** | partner-management |

**Descrição:**

Os registros de auditoria das escritas em parceiros não devem ter TTL nem _purge_ automático. A remoção só pode ocorrer por obrigação legal explícita (LGPD), mediante processo administrativo com aprovação dupla.

**Critérios de Aceite:**

- RNF-3.1 Nenhum job de _purge_ automático incide sobre os registros de auditoria de parceiros.
- RNF-3.2 Eventual remoção legal exige role administrativo separado e aprovação de dois membros, com registro prévio.

**Cross-ref:** NFR-AUD-03; ADR-0003; PTV-04

### RNF 4 — Tratamento de PII no cadastro de parceiro (LGPD)

| Campo | Valor |
|-------|-------|
| **Categoria** | Privacidade |
| **Prioridade** | Should |
| **Origem** | README §16 (LGPD marginal), §19 (RISK-PARTNER-02), §20 (VAL-PARTNER-01); RN-025 |
| **Módulo** | partner-management |

**Descrição:**

`partner.name` e os dados de contato do parceiro podem constituir dado pessoal quando o parceiro for pessoa física. Tais dados não devem aparecer em logs operacionais nem em mensagens de erro, e devem estar sujeitos à política de retenção e mascaramento aplicável (LGPD by design).

**Critérios de Aceite:**

- RNF-4.1 Nome e dados de contato de parceiro não aparecem em logs de sistema (Cloud Logging).
- RNF-4.2 Mensagens de erro não expõem dados pessoais do parceiro.
- RNF-4.3 Fica registrada a pendência VAL-PARTNER-01 (confirmar com produto/jurídico se `partner.name` é PII) antes de aprovar a fronteira LGPD do módulo.

**Cross-ref:** RN-025; README §16, §19, §20; `.forge/rules/architecture/security-and-compliance.md`; `.forge/rules/architecture/observability.md`

### RNF 5 — Observabilidade da gestão de parceiros

| Campo | Valor |
|-------|-------|
| **Categoria** | Observabilidade |
| **Prioridade** | Should |
| **Origem** | README §17; ADR-0001 (`correlationId-e-tenant-id-propagation`) |
| **Módulo** | partner-management |

**Descrição:**

As operações do módulo devem ser observáveis por meio de log estruturado com `correlation_id`, `tenant_id`, `partner_id` e ação, e de métricas de negócio sobre o ciclo de vida do parceiro.

**Critérios de Aceite:**

- RNF-5.1 Cada operação de escrita registra log estruturado com `correlation_id`, `tenant_id`, `partner_id` e ação (sem PII — ver RNF 4).
- RNF-5.2 São expostas as métricas `partners_created_total` e `partners_deactivated_total`.

**Cross-ref:** README §17; NFR-OBS-01/03; ADR-0001

### RNF 6 — Integridade da unidade de percentuais e valores monetários

| Campo | Valor |
|-------|-------|
| **Categoria** | Integridade |
| **Prioridade** | Must |
| **Origem** | `.forge/rules/domain/money-as-cents.md`; data-model §BC-03; OOS-05 (BRL no MVP) |
| **Módulo** | partner-management |

**Descrição:**

Os percentuais padrão devem ser tratados com unidade e precisão bem definidas (percentual com 2 casas decimais no intervalo [0,00; 100,00]). Qualquer valor monetário associado a comissão deve ser tratado em centavos inteiros; é proibido o uso de `float`/`double` em cálculo monetário. No MVP, todos os valores assumem BRL.

**Critérios de Aceite:**

- RNF-6.1 Percentuais fora do intervalo [0,00; 100,00] são rejeitados na escrita.
- RNF-6.2 Percentuais preservam precisão de 2 casas em leitura/escrita, sem erro de arredondamento de ponto flutuante (ver PBT-03).
- RNF-6.3 Nenhum valor monetário de comissão é representado ou calculado como `float`/`double` no domínio; arredondamento, quando aplicável, segue NBR 5891 ToEven.
- RNF-6.4 Todos os valores assumem BRL no MVP (sem multimoeda — OOS-05).

**Cross-ref:** Req 6, PBT-03; `.forge/rules/domain/money-as-cents.md`; `.forge/rules/domain/nbr-5891-rounding.md`

### RNF 7 — Desempenho da listagem e da visão de comissão

| Campo | Valor |
|-------|-------|
| **Categoria** | Performance |
| **Prioridade** | Should |
| **Origem** | README §9 (consumo pelo pipeline); inferência não funcional alinhada ao NFR-PERF do produto |
| **Módulo** | partner-management |

**Descrição:**

A listagem de parceiros (consumida pelo pipeline na vinculação) e a visão de comissão por parceiro devem responder em tempo adequado à interação síncrona, sem degradar a criação de oportunidade.

**Critérios de Aceite:**

- RNF-7.1 A listagem de parceiros ativos do tenant responde em ≤ 1 s (p95) para o volume esperado do tenant.
- RNF-7.2 A visão de comissão do parceiro responde em ≤ 2 s (p95), respeitando o filtro de período.

**Cross-ref:** README §9; NFR-PERF (produto)

## 7. Property-Based Testing

### PBT-01 — Conservação da soma de comissão por parceiro

**Mapeia para:** Req 9, Req 10
**Tipo:** Invariante matemática

**Propriedade:**

> Para qualquer conjunto de oportunidades vinculadas a um parceiro em um período, a comissão projetada total exibida na visão do parceiro é igual à soma das comissões projetadas das oportunidades abertas, e a comissão consolidada total é igual à soma dos _snapshots_ das oportunidades ganhas — sem dupla contagem nem omissão.

### PBT-02 — Idempotência da inativação/reativação

**Mapeia para:** Req 3
**Tipo:** Idempotência

**Propriedade:**

> Para qualquer parceiro, aplicar a operação de inativação N≥1 vezes resulta no mesmo estado final `active = false` (e analogamente para reativação em `active = true`); aplicações repetidas não alteram o estado de domínio além do registro de auditoria correspondente.

### PBT-03 — Round-trip e domínio dos percentuais

**Mapeia para:** Req 6, RNF 6
**Tipo:** Round-trip / Invariante matemática

**Propriedade:**

> Para qualquer percentual válido gerado no intervalo [0,00; 100,00] com 2 casas decimais, persistir e ler de volta preserva exatamente o valor (sem perda de precisão); para qualquer valor fora do intervalo ou com sinal negativo, a escrita é rejeitada.

### PBT-04 — Isolamento por tenant (anti-vazamento)

**Mapeia para:** RNF 1
**Tipo:** Anti-enumeração

**Propriedade:**

> Para qualquer `partner_id` pertencente ao tenant B, nenhuma requisição executada no contexto do tenant A retorna, lista ou modifica esse parceiro — independentemente do `partner_id` informado.

### PBT-05 — Imutabilidade da comissão consolidada frente à edição de percentuais

**Mapeia para:** Req 2, Req 9
**Tipo:** Invariante (state machine / imutabilidade)

**Propriedade:**

> Para qualquer parceiro com oportunidades já ganhas, qualquer alteração nos percentuais padrão (`pct_setup`, `pct_recorrente`) do parceiro mantém inalterada a comissão consolidada (baseada em _snapshot_ imutável), alterando no máximo a comissão projetada das oportunidades ainda abertas.

## 8. Glossário local

| Termo | Definição |
|-------|-----------|
| Partner (Parceiro) | Empresa ou pessoa que origina oportunidades e recebe comissão por componente; sem login no MVP (RN-021, DEC-012) |
| `partner_type` | Papel tipado do parceiro no processo comercial (seed: Indicador, Revendedor, Distribuidor, Integrador); configurável pelo tenant |
| `pct_setup` / `comissao_default_setup_pct` | Percentual padrão de comissão sobre o componente de setup do contrato; intervalo [0,00; 100,00] |
| `pct_recorrente` / `comissao_default_recorrente_pct` | Percentual padrão de comissão sobre o componente recorrente do contrato; intervalo [0,00; 100,00] |
| CommissionDefaults | Objeto de valor com `pct_setup` e `pct_recorrente` padrão do parceiro; herdado pela oportunidade na vinculação |
| comissão projetada (`comissao_projetada`) | Soma das comissões calculadas das oportunidades abertas do parceiro, com percentuais atuais (sem _snapshot_) |
| comissão consolidada (`comissao_consolidada`) | Soma dos _snapshots_ imutáveis de comissão das oportunidades ganhas do parceiro |
| `active` | Status do parceiro: ativo (`true`) ou inativo (_soft-delete_, `false`) |
| _snapshot_ | Registro imutável dos valores de comissão no fechamento como "Ganho"; gerado e mantido pelo `opportunity-pipeline` |
| _read model_ | Modelo de leitura exposto pelo `opportunity-pipeline` (`opportunity_partner_commissions`) consumido por este módulo para a visão de comissão |
| Objeto de valor | Estrutura de domínio imutável, identificada por seu valor (ex.: CommissionDefaults) — termo sempre por extenso |

## 9. Fora do escopo do MVP

- Login, portal ou autoatendimento do parceiro (RN-021, DEC-012, OOS-02) — registrar como requisito de Fase 2 (VAL-PARTNER-02).
- Cálculo e _snapshot_ de comissão por oportunidade — pertence ao `opportunity-pipeline` (RN-026, RN-007, DEC-002).
- Pagamento de comissão ao parceiro — fora do escopo do sistema.
- Múltiplos parceiros por oportunidade (OOS-03).
- Suporte a multimoeda — todos os valores assumem BRL no MVP (OOS-05, LAC-05).

## 10. Referências cruzadas

| Referência | Origem | Relação com este módulo |
|------------|--------|--------------------------|
| RF-05 | FRD §11 (FRD-partner-01, FRD-partner-02) | Requisito de produto que origina o módulo |
| RF-11 (FRD-report-05) | FRD §11 | Relatório de comissões por parceiro |
| NFR-SEG-01 | NFRD §8.x | Multi-tenancy com defesa em profundidade (RNF 1, PBT-04) |
| NFR-SEG-03 | NFRD §8.x | RBAC verificado em todo endpoint (RNF 1) |
| NFR-AUD-01 | NFRD §8.x | AuditLog imutável para escrita em parceiros (RNF 2) |
| NFR-AUD-03 | NFRD §8.x | Retenção de dados de auditoria (RNF 3) |
| TRD §8.4 (partner-management) | TRD | Endpoints `/api/v1/partners` e `/api/v1/partners/{id}/commissions` |
| TRD FLOW-08 | TRD | Relatório de comissões por parceiro (projetado/consolidado) |
| data-model §BC-03 | Data Model | Tabela `partners` (dados próprios) |
| RN-021 / DEC-012 | Discovery / glossário | Parceiro sem login no MVP (Req 12) |
| RN-008 | FRD §13 | Canal "Parceiro" exige `partner_id` (Req 8) |
| RN-026 / RN-007 / DEC-002 | FRD / TRD | Cálculo e _snapshot_ de comissão (no `opportunity-pipeline`) |
| Pendência LAC-01 / VAL-01 | Insumo de migração | Percentuais dos parceiros reais da Vellus preenchidos pós-import (Req 11) |
| ADR-0001 | TRD §ADRs | Propagação de `correlationId` e `tenant_id` (RNF 5) |
| ADR-0003 | TRD §ADRs | Política de retenção de logs e auditoria (RNF 3) |
| `.forge/rules/domain/money-as-cents.md` | Regras | Unidade monetária em centavos (RNF 6) |
| `.forge/rules/domain/nbr-5891-rounding.md` | Regras | Arredondamento ToEven (RNF 6) |
| `.forge/rules/domain/audit-immutability.md` | Regras | Auditoria append-only (RNF 2, RNF 3) |
