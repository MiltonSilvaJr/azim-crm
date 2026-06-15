# BC-02 — Account Management (Gestão de Contas e Contatos)
**Requisitos Funcionais e Não-Funcionais**

- Versão: 0.1.0
- Data: 2026-06-11
- Status: Rascunho para revisão
- Referência pai: docs/product/frd-nfrd/frd.md § RF-04 (Contas e contatos)

## Histórico de Versões

| Versão | Data | Status | Descrição da alteração |
|--------|------|--------|------------------------|
| 0.1.0 | 2026-06-11 | Rascunho para revisão | Criação inicial do documento de requisitos do módulo account-management |

## 1. Visão Geral

O módulo **account-management** (Bounded Context BC-02 — Subdomínio de Suporte; deployable `azim-api`) é responsável pela gestão de **contas** (empresas clientes) e **contatos** (pessoas físicas vinculadas a uma conta, contendo dados pessoais identificáveis — PII, do inglês *Personally Identifiable Information*).

A conta é **pré-requisito** para a criação de oportunidades no módulo opportunity-pipeline. Cada conta pertence a uma **Business Unit (BU)** específica (`bu_id` obrigatório — ADR-0009), e a visibilidade de uma conta é controlada pelo escopo de BU do usuário autenticado. Um usuário com acesso a múltiplas BUs enxerga todas as contas do seu conjunto de BUs autorizadas; um admin tenant-wide enxerga todas as contas do tenant.

O módulo mantém o mecanismo de **deduplicação (dedupe)** por nome normalizado, exibindo alerta de "conta similar existe" no momento da criação, e expõe a **visão 360°** consolidada de uma conta. Por tratar de PII, o módulo está sujeito à **LGPD** (Lei nº 13.709/2018), com obrigações de mascaramento de dados pessoais em logs, controle de acesso por papel, direito ao esquecimento e base legal documentada.

## 2. Escopo

### 2.1 Incluído

- Criação, leitura, atualização e busca de contas, com dedupe por nome normalizado e alerta de conta similar (não bloqueante).
- Compartilhamento da conta entre todas as BUs do tenant, sem duplicação de registro.
- Gestão de contatos (criar, editar, listar) vinculados a uma conta, com campos PII: nome, e-mail, telefone e cargo.
- Visão 360° da conta: oportunidades das BUs visíveis ao usuário, contatos, atividades e histórico de alterações.
- Direito ao esquecimento: anonimização ou exclusão de dados pessoais de um contato a pedido (LGPD Art. 18).
- Mascaramento de PII em logs e em `delta_json` de auditoria.
- Controle de acesso a contatos (PII) por papel RBAC.
- Isolamento de dados por tenant.
- Emissão de eventos de auditoria em toda escrita de conta e contato.

### 2.2 Excluído

- Gestão de oportunidades vinculadas à conta — pertence ao opportunity-pipeline.
- Gestão de atividades vinculadas à conta — pertence ao activity-management.
- Composição interna do read model de relatórios sobre contas — pertence ao reporting.
- Importação de contas via planilha — pertence ao data-migration na Fase 1.

### 2.3 Fora do escopo do MVP

Ver seção 9.

## 3. Personas / Atores

| Persona | Descrição | Relação com o módulo |
|---------|-----------|----------------------|
| Tenant Admin | Administrador do tenant | Cria/edita contas e contatos em qualquer BU; único papel autorizado a acionar o direito ao esquecimento de contato |
| Gestor de BU | Gestor de uma unidade de negócio | Cria/edita contas; gerencia contatos no escopo das BUs sob sua gestão |
| Vendedor | Usuário comercial | Cria/edita contas; gerencia contatos no escopo das BUs em que é membro |
| Viewer | Usuário de leitura | Visualiza contas e visão 360° dentro do seu escopo de BUs; acesso a contatos (PII) sujeito a restrição por papel |
| opportunity-pipeline | Módulo consumidor (ator de sistema) | Pesquisa contas por nome e referencia `account_id` ao criar oportunidades |
| activity-management | Módulo consumidor (ator de sistema) | Vincula atividades a uma conta |

## 4. Lista canônica de operações

| Operação | Entidade | Papel mínimo | Observação |
|----------|----------|--------------|------------|
| Listar / buscar contas | Account | Viewer | Busca por nome; usada também pelo dedupe e pelo pipeline |
| Criar conta | Account | Vendedor | Executa dedupe; alerta de conta similar (não bloqueante) |
| Obter conta (visão 360°) | Account | Viewer | Filtrada pelo escopo de BUs do usuário |
| Atualizar conta | Account | Vendedor | |
| Listar contatos da conta | Contact | Viewer | Acesso a PII restrito ao escopo RBAC |
| Criar contato | Contact | Vendedor | Campos PII: nome, e-mail, telefone, cargo |
| Atualizar contato | Contact | Vendedor | |
| Anonimizar / excluir contato (direito ao esquecimento) | Contact | Tenant Admin | LGPD Art. 18; decisão entre anonimizar ou excluir é pendência jurídica (ver RNF 4 e seção 9) |

## 5. Requisitos Funcionais

### Req 1 — Criar conta com dedupe por nome normalizado

**Como** Vendedor **quero** criar uma conta verificando previamente se já existe uma conta similar **para** evitar registros duplicados da mesma empresa cliente.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § FRD-account-01; RN-014; PRD RF-04 |
| **Módulo** | account-management |

**Critérios de Aceite:**

- 1.1 Ao informar o nome da conta, o sistema calcula o nome normalizado removendo acentos, convertendo para minúsculas e colapsando espaços extras antes de comparar.
- 1.2 Se existir conta no mesmo tenant com nome normalizado igual ao informado, o sistema exibe alerta apresentando a(s) conta(s) similar(es) encontrada(s), com as opções "Usar esta" ou "Criar nova mesmo assim".
- 1.3 O alerta de conta similar é informativo e não bloqueia a criação: o usuário pode confirmar a criação de uma nova conta por escolha explícita.
- 1.4 Ao digitar "Pag.ai", o sistema encontra "PAG.AI " como similar e exibe o alerta (caso Pag.ai/Ethoca).
- 1.5 Nome em branco bloqueia a criação e retorna mensagem de validação (MSG-019).
- 1.6 A conta criada é vinculada ao `tenant_id` do usuário autenticado.
- 1.7 A criação de conta gera evento de auditoria (ver Req 10).

**Cross-ref:** Req 2, Req 3, RN-014, RN-012, PBT-01, PBT-02

---

### Req 2 — Conta única por tenant compartilhada entre BUs

**Como** organização multi-BU **quero** que uma empresa cliente exista como uma única conta no tenant **para** consolidar o relacionamento comercial mesmo quando negociada por diferentes unidades de negócio.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § FRD-account-01 (critério "conta compartilhada entre BUs"); Data Model § BC-02; Linguagem Ubíqua (Account) |
| **Módulo** | account-management |

**Critérios de Aceite:**

- 2.1 Uma conta pertence a uma BU (bu_id) e é visível apenas para usuários com acesso a essa BU ou com escopo tenant-wide (ADR-0009).
- 2.2 A mesma conta pode ser referenciada por oportunidades; o bu_id da conta e o bu_id da oportunidade podem ser distintos (cross-BU reference) sem duplicar o registro da conta.
- 2.3 ~~A unidade de negócio é atributo da oportunidade, não da conta~~ — **revisado por ADR-0009**: `bu_id` é agora atributo da conta, obrigatório e imutável. O isolamento por BU é implementado via EF Global Query Filter + RLS PostgreSQL (VAL-ACC-03).
- 2.4 Uma conta criada durante a criação de uma oportunidade herda a bu_id do contexto de BU do usuário; fica disponível para usuários com acesso à mesma BU.

**Cross-ref:** Req 1, Req 6, Req 10, RN-012, opportunity-pipeline (referência a `account_id`)

---

### Req 3 — Buscar e visualizar contas

**Como** Vendedor **quero** pesquisar e visualizar contas por nome **para** localizar a conta correta ao registrar uma oportunidade ou consultar o relacionamento.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § FRD-account-01; TRD § account-management (GET /api/v1/accounts) |
| **Módulo** | account-management |

**Critérios de Aceite:**

- 3.1 O sistema permite buscar contas por nome dentro do tenant do usuário.
- 3.2 A busca utiliza o nome normalizado para tolerar diferenças de acento, caixa e espaços.
- 3.3 A operação de busca é exposta para consumo pelo opportunity-pipeline na criação de oportunidades.
- 3.4 A busca retorna apenas contas do tenant do usuário autenticado (ver Req 11).

**Cross-ref:** Req 1, Req 2, RNF 7, PBT-04

---

### Req 4 — Editar conta

**Como** Vendedor **quero** atualizar os dados de uma conta **para** manter o cadastro da empresa cliente correto.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § RF-04; TRD § account-management (PATCH /api/v1/accounts/{id}) |
| **Módulo** | account-management |

**Critérios de Aceite:**

- 4.1 O usuário pode atualizar os campos cadastrais da conta (por exemplo nome, site e observações).
- 4.2 Ao alterar o nome, o sistema recalcula o nome normalizado correspondente.
- 4.3 A edição respeita o isolamento por tenant (ver Req 11).
- 4.4 A atualização de conta gera evento de auditoria (ver Req 10).

**Cross-ref:** Req 1, Req 10, Req 11

---

### Req 5 — Gerenciar contatos da conta (PII)

**Como** Vendedor **quero** cadastrar e manter contatos vinculados a uma conta **para** registrar as pessoas de relacionamento da empresa cliente.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § FRD-account-02; NFR-PRIV-02; Data Model § BC-02 (contacts) |
| **Módulo** | account-management |

**Critérios de Aceite:**

- 5.1 Um contato é criado vinculado a uma conta existente (`account_id` obrigatório) e ao `tenant_id`.
- 5.2 Os campos do contato são: nome (obrigatório), e-mail (opcional), telefone (opcional) e cargo (opcional).
- 5.3 O conjunto de campos de PII fica limitado ao mínimo operacional (nome, e-mail, telefone, cargo); nenhum campo adicional de dado pessoal é coletado sem base legal documentada.
- 5.4 E-mail de contato com formato inválido bloqueia o salvamento e retorna mensagem de validação (MSG-020).
- 5.5 Uma conta pode ter N contatos; o mesmo contato pode ser referenciado em múltiplas oportunidades da mesma conta.
- 5.6 O contato criado aparece na visão 360° da conta (ver Req 7).
- 5.7 Criação e atualização de contato geram evento de auditoria com PII mascarada (ver Req 10 e RNF 1).

**Cross-ref:** Req 7, Req 8, Req 11, RNF 1, RNF 2, RNF 6

---

### Req 6 — Visão 360° da conta

**Como** Vendedor **quero** visualizar a visão consolidada de uma conta **para** entender todo o relacionamento comercial antes de uma interação.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § FRD-account-03; Linguagem Ubíqua (visao_360); Data Model § Account360View |
| **Módulo** | account-management |

**Critérios de Aceite:**

- 6.1 A visão 360° exibe, para uma conta: oportunidades, contatos vinculados, atividades e histórico de alterações.
- 6.2 As oportunidades exibidas são apenas as das BUs às quais o usuário tem acesso (RLS + RBAC); a conta em si permanece compartilhada no tenant.
- 6.3 Para a conta Pag.ai, um usuário com acesso às BUs Vellus e Axis vê as oportunidades de ambas; um usuário com acesso apenas à BU Axis vê somente as oportunidades da Axis para a mesma conta.
- 6.4 O histórico é exibido em ordem cronológica decrescente.
- 6.5 As oportunidades e atividades são obtidas dos módulos opportunity-pipeline e activity-management; este módulo apenas compõe a visão.

**Cross-ref:** Req 2, Req 5, Req 11, PBT-05

---

### Req 7 — Direito ao esquecimento de contato (LGPD)

**Como** Tenant Admin **quero** anonimizar ou excluir os dados pessoais de um contato a pedido do titular **para** atender ao direito ao esquecimento previsto na LGPD preservando o histórico comercial.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | NFR-PRIV-03; NFR-COMP-01; LGPD Art. 18, IV; TRD § account-management (DELETE /api/v1/accounts/{accountId}/contacts/{id}) |
| **Módulo** | account-management |

**Descrição complementar:** a decisão entre **anonimizar** os campos de PII e **excluir fisicamente** o contato é uma pendência jurídica (ver seção 9, VAL-ACC-01). Os critérios abaixo descrevem o comportamento esperado de produto; anonimização e exclusão não devem ser tratadas como sinônimos.

**Critérios de Aceite:**

- 7.1 Apenas o papel Tenant Admin pode acionar a operação de direito ao esquecimento sobre um contato.
- 7.2 Após a operação, nenhum campo de PII do contato (nome, e-mail, telefone) é recuperável por qualquer endpoint do sistema; os valores retornam um substituto (por exemplo `[anonimizado]` ou `[contato removido]`).
- 7.3 As oportunidades e atividades vinculadas ao contato permanecem acessíveis, com a referência ao contato preservada e os dados pessoais substituídos.
- 7.4 A operação é registrada na auditoria (quem solicitou, quando, qual contato), sem incluir PII no registro da própria operação.
- 7.5 A operação é irreversível do ponto de vista de recuperação de PII pelo sistema.

**Cross-ref:** Req 5, Req 10, RNF 1, RNF 3, RNF 4, PBT-03

---

### Req 8 — Auditoria imutável de escrita de conta e contato

**Como** organização sujeita a auditoria e LGPD **quero** que toda escrita em contas e contatos gere registro auditável **para** garantir rastreabilidade sem expor dados pessoais.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | RN-024; módulo README § 10 (AccountCreated, ContactLinked); `.forge/rules/domain/audit-immutability.md` |
| **Módulo** | account-management |

**Critérios de Aceite:**

- 8.1 Toda criação de conta gera registro de auditoria (evento `AccountCreated`).
- 8.2 Toda criação ou atualização de contato gera registro de auditoria (evento `ContactLinked`).
- 8.3 O registro de auditoria contém, no mínimo: `user_id`, `entity_type`, `entity_id`, `action`, `delta_json`, `timestamp` e `tenant_id`.
- 8.4 O `delta_json` de contatos tem os campos de PII (nome, e-mail, telefone) mascarados.
- 8.5 Os registros de auditoria são append-only; não há atualização, exclusão ou TTL automático.

**Cross-ref:** Req 1, Req 4, Req 5, Req 7, RNF 1, RNF 8

---

### Req 9 — Controle de acesso a PII de contatos por papel

**Como** organização sujeita à LGPD **quero** restringir o acesso aos dados de contato por papel **para** limitar a exposição de dados pessoais a quem tem necessidade legítima.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | módulo README § 15 (Segurança); NFR-SEG-03; TRD § account-management (PII restrita ao escopo RBAC) |
| **Módulo** | account-management |

**Critérios de Aceite:**

- 9.1 O acesso à lista de contatos e aos dados de PII de uma conta é restrito por papel RBAC, com papel mínimo Vendedor na BU relacionada à conta.
- 9.2 Toda operação sobre contatos verifica o papel do usuário no escopo apropriado antes de retornar PII.
- 9.3 A operação de direito ao esquecimento exige papel Tenant Admin (ver Req 7).

**Cross-ref:** Req 5, Req 7, RNF 6

---

### Req 10 — Isolamento de dados por tenant

**Como** plataforma multi-tenant **quero** que contas e contatos de um tenant nunca sejam acessíveis a outro tenant **para** garantir o isolamento de dados comerciais e pessoais.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | RN-012; NFR-SEG-01; PRD § 8.3 |
| **Módulo** | account-management |

**Critérios de Aceite:**

- 10.1 Toda conta e todo contato carrega `tenant_id`.
- 10.2 Consultas, buscas e o dedupe operam exclusivamente sobre dados do tenant do usuário autenticado.
- 10.3 Nenhuma conta ou contato de um tenant é retornado, deduplicado ou referenciado em operações de outro tenant.
- 10.4 Vazamento de dados entre tenants é tratado como incidente de severidade 1.

**Cross-ref:** Req 1, Req 3, Req 6, RNF 5, PBT-04

---

## 6. Requisitos Não-Funcionais

### RNF 1 — PII de contatos ausente de logs

| Campo | Valor |
|-------|-------|
| **Categoria** | Privacidade |
| **Prioridade** | Must |
| **Origem** | NFR-PRIV-01; RN-025; LGPD Art. 6º (minimização) |
| **Módulo** | account-management |

**Descrição:**

Nome, e-mail e telefone de contatos jamais devem aparecer em logs estruturados, mensagens de erro ou traces; devem ser mascarados ou omitidos antes do registro.

**Critérios de Aceite:**

- RNF-1.1 Após criar ou editar um contato com e-mail e telefone reais, nenhum desses valores aparece em texto claro no Cloud Logging ou em traces.
- RNF-1.2 O `delta_json` de auditoria de contatos apresenta os campos de PII mascarados.
- RNF-1.3 Mensagens de erro relacionadas a contatos não expõem PII.
- RNF-1.4 Existe verificação automatizada (teste de integração ou scan de logs) que falha se PII de contato for detectada nos logs.

**Cross-ref:** Req 5, Req 8, `.forge/rules/architecture/observability.md`

---

### RNF 2 — Minimização de dados pessoais coletados

| Campo | Valor |
|-------|-------|
| **Categoria** | Privacidade |
| **Prioridade** | Must |
| **Origem** | NFR-PRIV-02; LGPD Art. 6º, I |
| **Módulo** | account-management |

**Descrição:**

O módulo coleta apenas os dados pessoais estritamente necessários à operação comercial, limitados ao conjunto nome, e-mail, telefone e cargo.

**Critérios de Aceite:**

- RNF-2.1 O cadastro de contato não aceita campos de PII além de nome, e-mail, telefone e cargo.
- RNF-2.2 A introdução de qualquer novo campo de dado pessoal exige base legal documentada e revisão de LGPD by design.

**Cross-ref:** Req 5, RNF 3

---

### RNF 3 — Base legal LGPD documentada (pendência)

| Campo | Valor |
|-------|-------|
| **Categoria** | Privacidade |
| **Prioridade** | Must |
| **Origem** | NFR-PRIV-04; LGPD Art. 7º; LAC-07 / VAL-08 (a confirmar com jurídico) |
| **Módulo** | account-management |

**Descrição:**

Cada categoria de dado pessoal de contato tratada pelo módulo deve ter base legal explícita documentada conforme a LGPD. A definição da base legal é uma **pendência** a confirmar com a equipe jurídica antes do go-live (ver seção 9, VAL-ACC-02).

**Critérios de Aceite:**

- RNF-3.1 O registro de atividades de tratamento (RAT) com a base legal para os dados de contato é aprovado pela equipe jurídica antes do go-live em produção.
- RNF-3.2 Enquanto a base legal não for confirmada, a pendência permanece registrada na seção 9 e no `approvals.yaml`.

**Cross-ref:** Req 5, Req 7, RNF 4

---

### RNF 4 — Política de retenção e descarte de contatos (pendência)

| Campo | Valor |
|-------|-------|
| **Categoria** | Privacidade |
| **Prioridade** | Must |
| **Origem** | NFR (Operabilidade — retenção de dados de negócio); VAL-08 / VAL-MOD-05; LGPD |
| **Módulo** | account-management |

**Descrição:**

Deve existir política documentada de retenção e descarte de contatos (PII) compatível com a LGPD, incluindo a decisão entre anonimização e exclusão física no direito ao esquecimento. A política é uma **pendência** a confirmar com a equipe jurídica antes do go-live (ver seção 9, VAL-ACC-01).

**Critérios de Aceite:**

- RNF-4.1 A política de retenção e descarte de contatos é documentada e aprovada antes do go-live.
- RNF-4.2 A decisão entre anonimizar ou excluir fisicamente o contato no direito ao esquecimento é registrada formalmente e refletida no comportamento da Req 7.

**Cross-ref:** Req 7, RNF 3

---

### RNF 5 — Isolamento multi-tenant com defesa em profundidade

| Campo | Valor |
|-------|-------|
| **Categoria** | Segurança |
| **Prioridade** | Must |
| **Origem** | NFR-SEG-01; RN-012; DEC-006 |
| **Módulo** | account-management |

**Descrição:**

O isolamento por tenant é aplicado em profundidade sobre contas e contatos, de forma que dados de tenants distintos coexistam no mesmo banco sem possibilidade de acesso cruzado.

**Critérios de Aceite:**

- RNF-5.1 Toda consulta a contas e contatos é restringida pelo `tenant_id` do contexto autenticado.
- RNF-5.2 Existe teste de isolamento entre tenants como gate obrigatório de integração contínua.
- RNF-5.3 Nenhuma operação (busca, dedupe, visão 360°) atravessa a fronteira de tenant.

**Cross-ref:** Req 10, PBT-04

---

### RNF 6 — Acesso a PII restrito por RBAC

| Campo | Valor |
|-------|-------|
| **Categoria** | Segurança |
| **Prioridade** | Must |
| **Origem** | NFR-SEG-03; módulo README § 15 |
| **Módulo** | account-management |

**Descrição:**

Todo endpoint que retorna PII de contatos verifica o papel do usuário antes de expor os dados, com papel mínimo Vendedor na BU relacionada à conta.

**Critérios de Aceite:**

- RNF-6.1 Requisição sem papel mínimo suficiente recebe negação de acesso e nenhum dado de PII.
- RNF-6.2 A verificação de papel ocorre em todos os endpoints de contato (listar, criar, atualizar, anonimizar).

**Cross-ref:** Req 9, Req 7

---

### RNF 7 — Performance de busca de contas

| Campo | Valor |
|-------|-------|
| **Categoria** | Performance |
| **Prioridade** | Should |
| **Origem** | módulo README § 15 (Performance); risco RISK-ACC-02 |
| **Módulo** | account-management |

**Descrição:**

A busca de contas por nome (usada também pelo dedupe e pelo pipeline) deve responder de forma rápida mesmo com volume relevante de contas no tenant, apoiada em índice sobre o nome normalizado.

**Critérios de Aceite:**

- RNF-7.1 A busca por nome normalizado é servida por índice dedicado, sem varredura completa da tabela.
- RNF-7.2 O dedupe na criação de conta executa a verificação de similaridade pela mesma via indexada.

**Cross-ref:** Req 1, Req 3

---

### RNF 8 — Imutabilidade dos registros de auditoria

| Campo | Valor |
|-------|-------|
| **Categoria** | Auditoria |
| **Prioridade** | Must |
| **Origem** | RN-024; `.forge/rules/domain/audit-immutability.md`; NFR-AUD |
| **Módulo** | account-management |

**Descrição:**

Os registros de auditoria gerados pelas escritas de conta e contato são append-only e não sofrem atualização, exclusão ou expurgo automático.

**Critérios de Aceite:**

- RNF-8.1 Não há operação de UPDATE, DELETE ou TRUNCATE disponível ao serviço de aplicação sobre os registros de auditoria.
- RNF-8.2 Não há TTL ou job de purge automático sobre os registros de auditoria.
- RNF-8.3 Eventual expurgo legal segue processo administrativo com aprovação dupla.

**Cross-ref:** Req 8, RNF 4

---

### RNF 9 — Observabilidade do módulo

| Campo | Valor |
|-------|-------|
| **Categoria** | Observabilidade |
| **Prioridade** | Should |
| **Origem** | módulo README § 17; NFR-OBS-01 |
| **Módulo** | account-management |

**Descrição:**

O módulo emite logs estruturados e métricas que permitem operar e diagnosticar o cadastro de contas e o dedupe, sempre com `tenant_id` e sem PII.

**Critérios de Aceite:**

- RNF-9.1 Logs estruturados incluem `correlation_id`, `tenant_id` e `account_id`, sem nome, e-mail ou telefone em texto claro.
- RNF-9.2 São expostas métricas mínimas: `accounts_created_total`, `contacts_created_total` e `dedupe_blocked_total`.
- RNF-9.3 Existe alerta para taxa de erro de criação de conta acima de 5%.

**Cross-ref:** Req 1, Req 5, RNF 1

---

### RNF 10 — Residência de dados no Brasil

| Campo | Valor |
|-------|-------|
| **Categoria** | Privacidade |
| **Prioridade** | Must |
| **Origem** | NFR-POR (residência de dados); DEC-005; LGPD (soberania de dados) |
| **Módulo** | account-management |

**Descrição:**

Os dados de contas e contatos (incluindo PII) residem exclusivamente em região GCP do Brasil (`southamerica-east1`), sem replicação para fora do país sem decisão explícita documentada.

**Critérios de Aceite:**

- RNF-10.1 A persistência de contas e contatos ocorre exclusivamente em `southamerica-east1`.
- RNF-10.2 Qualquer replicação cross-region de dados de conta/contato exige ADR aprovado.

**Cross-ref:** Req 10, RNF 5

## 7. Property-Based Testing

### PBT-01 — Idempotência da normalização de nome

**Mapeia para:** Req 1, Req 3, Req 4
**Tipo:** Idempotência

**Propriedade:**

> Para qualquer nome de conta gerado, `normalize(normalize(nome)) == normalize(nome)`; aplicar a normalização repetidamente não altera o resultado.

---

### PBT-02 — Equivalência de nomes pela forma normalizada

**Mapeia para:** Req 1
**Tipo:** Invariante matemática

**Propriedade:**

> Para qualquer par de nomes que difiram apenas por acentuação, caixa ou espaços extras, a forma normalizada é idêntica; e, dentro do mesmo tenant, duas contas com a mesma forma normalizada são sempre detectadas como similares pelo dedupe.

---

### PBT-03 — Irreversibilidade da anonimização preservando referência

**Mapeia para:** Req 7
**Tipo:** State machine

**Propriedade:**

> Para qualquer contato submetido ao direito ao esquecimento, após a operação nenhum endpoint retorna seus campos de PII originais, enquanto a referência ao contato (`contact_id`) permanece válida nas oportunidades e atividades vinculadas. A transição de "ativo" para "anonimizado/excluído" não admite caminho de volta que recupere a PII.

---

### PBT-04 — Anti-cross-tenant em contas e contatos

**Mapeia para:** Req 3, Req 10, RNF 5
**Tipo:** Anti-enumeração

**Propriedade:**

> Para qualquer conjunto gerado de contas e contatos pertencentes a tenants distintos, nenhuma operação de busca, dedupe ou leitura executada no contexto de um tenant retorna ou referencia registros de outro tenant.

---

### PBT-05 — Visão 360° respeita o escopo de BUs do usuário

**Mapeia para:** Req 6
**Tipo:** Invariante matemática

**Propriedade:**

> Para qualquer usuário e qualquer conta, o conjunto de oportunidades exibido na visão 360° é subconjunto das oportunidades cujas BUs estão no escopo de acesso do usuário.

## 8. Glossário local

| Termo | Definição |
|-------|-----------|
| Account (conta) | Empresa cliente com ciclo de vida independente do pipeline; compartilhada por todo o tenant, não por BU |
| Contact (contato) | Pessoa física vinculada a uma conta; contém PII (nome, e-mail, telefone, cargo) |
| dedupe (deduplicação) | Verificação por nome normalizado, antes de criar uma conta, que gera alerta (não bloqueio) de conta similar |
| normalized_name (nome normalizado) | Nome da conta sem acentos, em minúsculas e sem espaços extras, usado para dedupe e busca |
| visão 360° | Visão consolidada de uma conta: oportunidades das BUs visíveis ao usuário, contatos, atividades e histórico |
| PII (dado pessoal identificável) | Dados pessoais de contato sujeitos à LGPD: nome, e-mail, telefone |
| Direito ao esquecimento | Direito do titular (LGPD Art. 18) de ter seus dados pessoais anonimizados ou excluídos a pedido |
| objeto de valor | Conceito de domínio imutável definido por seus atributos; o nome normalizado é tratado como objeto de valor da conta |
| tenant | Empresa cliente do Azim como produto SaaS, com isolamento completo de dados por `tenant_id` |
| BU (unidade de negócio) | Unidade de negócio do tenant com pipeline e membros próprios; atributo da oportunidade, não da conta |

## 9. Fora do escopo do MVP

- Deduplicação avançada com IA ou correspondência aproximada (fuzzy matching) — Fase 3 / ai-intelligence.
- Importação de contas via planilha — pertence ao data-migration na Fase 1.
- Mescla (merge) automática de contas duplicadas detectadas — o dedupe do MVP apenas alerta; a resolução de duplicatas em lote é tratada no fluxo de migração.
- Endpoint completo de direitos do titular (acesso e correção além da exclusão) — a planejar (VAL-ACC-02).

**Pendências a confirmar antes do go-live:**

| Código | Pendência | Impacto |
|--------|-----------|---------|
| VAL-ACC-01 | Política de retenção e descarte de contatos (PII) sob LGPD e decisão entre anonimização e exclusão física no direito ao esquecimento | Define o comportamento da Req 7 e da RNF 4 |
| VAL-ACC-02 | Base legal LGPD para tratamento de dados pessoais de contatos (RAT) e escopo dos direitos do titular | Define conformidade (RNF 3); confirmar com jurídico |
| VAL-ACC-03 | Confirmação da regra de visibilidade "conta por tenant, não por BU" com produto | Confirmada no data model; validação de produto pendente |

## 10. Referências cruzadas

| Referência | Origem |
|------------|--------|
| RF-04 — Contas e contatos | docs/product/frd-nfrd/frd.md § FRD-account-01, FRD-account-02, FRD-account-03 |
| RN-012 (isolamento por tenant), RN-014 (dedupe), RN-024 (auditoria), RN-025 (PII fora de logs) | docs/product/frd-nfrd/frd.md § Regras de Negócio |
| NFR-PRIV-01..04, NFR-SEG-01/03, NFR-COMP-01, NFR-AUD, NFR-POR | docs/product/frd-nfrd/nfrd.md |
| Tabelas accounts e contacts; Account360View | docs/product/data-model/data-model.md § Account Management (BC-02) |
| Operações e endpoints; DELETE de contato (LGPD Art. 18) | docs/product/trd/trd.md § account-management |
| Linguagem ubíqua de BC-02 | docs/product/glossary/ubiquitous-language.md § Account Management |
| Imutabilidade de auditoria; PII fora de logs; centavos | `.forge/rules/domain/audit-immutability.md`, `.forge/rules/architecture/observability.md`, `.forge/rules/architecture/security-and-compliance.md` |
| VAL-08 / LAC-07 (base legal e retenção LGPD) | docs/product/frd-nfrd/frd.md § Pontos a Validar; docs/product/trd/trd.md § VAL-TRD-03 |
