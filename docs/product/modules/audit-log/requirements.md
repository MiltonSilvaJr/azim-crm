# AUD — Audit Log
**Requisitos Funcionais e Não-Funcionais**

- Versão: 0.1.0
- Data: 2026-06-11
- Status: Rascunho para revisão
- Referência pai: docs/product/frd-nfrd/frd.md § RN-024/RN-025; docs/product/frd-nfrd/nfrd.md § 8.9 Auditoria (NFR-AUD); docs/product/data-model/data-model.md § 3 Audit Log (BC-15)

## Histórico de Versões

| Versão | Data | Status | Descrição da alteração |
|--------|------|--------|------------------------|
| 0.1.0 | 2026-06-11 | Rascunho para revisão | Criação inicial do documento a partir do README do módulo, FRD (RN-024/RN-025), NFRD (NFR-AUD-01 a NFR-AUD-03, NFR-PRIV-01) e data model (BC-15, tabela `audit_logs`) |

## 1. Visão Geral

O módulo **Audit Log** é um módulo de aplicação transversal (cross-cutting) e pertence ao bounded context **Audit Log (BC-15)**, classificado como subdomínio genérico (Generic Subdomain) e candidato ao deployable **azim-api**.

Sua única responsabilidade é registrar, de forma **imutável** e **somente-acréscimo** (append-only), toda operação de escrita em entidade de negócio ocorrida em qualquer bounded context do Azim CRM. O módulo não possui lógica de domínio própria: atua como receptor passivo de eventos de auditoria (AuditEvent) publicados pelos módulos de escrita e os persiste na tabela `audit_logs`.

A trilha resultante é consultável apenas em leitura, por papéis administrativos (Tenant Admin e Gestor de BU), e garante que toda alteração de dados de negócio seja rastreável por **quem fez, o quê, quando e qual foi o delta** — sem expor dados pessoais (PII, do inglês *Personally Identifiable Information* — informação pessoal identificável) em texto claro no campo de delta.

A imutabilidade da trilha é requisito de produto (RN-024) e de conformidade com a LGPD (Lei Geral de Proteção de Dados); o mascaramento de PII no delta é requisito de privacidade (RN-025). O módulo é classificado como Tier 1 (criticidade máxima): perda silenciosa de trilha de auditoria é inaceitável.

## 2. Escopo

### 2.1 Incluído

- Recepção de AuditEvents de todos os módulos de escrita por meio de um AuditService centralizado.
- Persistência append-only na tabela `audit_logs`, sem operações de UPDATE, DELETE ou TRUNCATE.
- Registro do conjunto mínimo de campos: `user_id` (autor), `entity_type`, `entity_id`, `action`, `delta_json`, `created_at` (timestamp) e `tenant_id`.
- Registro do delta da entidade (estado antes e depois da operação) no campo `delta_json`.
- Mascaramento de PII no `delta_json` antes da persistência (RN-025).
- Isolamento estrito por `tenant_id` via Row-Level Security (RLS — segurança em nível de linha).
- Consulta somente-leitura da trilha de auditoria, com filtros por `entity_type`, `entity_id`, `user_id` e período, restrita aos papéis Tenant Admin e Gestor de BU.

### 2.2 Excluído

- Qualquer lógica de domínio ou regra de negócio do CRM.
- Escrita direta na tabela `audit_logs` por outros módulos: toda escrita ocorre exclusivamente via AuditService.
- Relatórios analíticos derivados de `audit_logs` (pertencem ao módulo de Reporting — BC-07).
- Emissão de eventos de domínio próprios (este módulo não publica eventos).

### 2.3 Fora do escopo do MVP

Ver seção 9.

## 3. Personas / Atores

| Persona | Papel RBAC | Relação com o módulo |
|---|---|---|
| Módulo de escrita | Ator de sistema (não humano) | Publica AuditEvent para o AuditService a cada escrita em entidade de negócio. Inclui opportunity-pipeline, account-management, partner-management, activity-management, goal-forecast, organization e tenant-administration. |
| Tenant Admin (TAdmin) | Tenant Admin | Consulta a trilha de auditoria de todo o seu tenant (somente leitura). |
| Gestor de BU (GestorBU) | Gestor de BU | Consulta a trilha de auditoria restrita às BUs (Business Units — unidades de negócio) das quais é membro (somente leitura). |
| Platform Operator | Platform Operator | Não tem acesso a dados comerciais de tenants por padrão; qualquer acesso de suporte autorizado é, ele próprio, registrado na trilha de auditoria (NFR-SEG correlato). |

## 4. Lista canônica de tipos de operação (action)

O campo `action` registra o tipo de operação de escrita auditada. Os valores canônicos no MVP são:

| Valor (`action`) | Significado |
|---|---|
| `create` | Criação de uma nova instância de entidade de negócio |
| `update` | Edição de uma instância existente, incluindo transição de estágio e demais mutações de atributos |
| `delete` | Exclusão lógica ou física de uma instância de entidade de negócio |

Entidades de negócio auditadas (`entity_type`), conforme NFR-AUD-01: `Opportunity`, `Account`, `Contact`, `Partner`, `Goal`, `Activity` e `OpportunityPartnerCommission`. A lista de `entity_type` é extensível por novos módulos de escrita sem alteração estrutural da trilha.

## 5. Requisitos Funcionais

### REQ-001 — Registrar toda escrita em entidade de negócio

**Como** plataforma Azim CRM **quero** registrar na trilha de auditoria toda operação de escrita em entidade de negócio **para** garantir rastreabilidade completa de quem alterou o quê e quando.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD RN-024; NFRD NFR-AUD-01; PRD §8.6; rule `.forge/rules/domain/audit-immutability.md` |
| **Módulo** | audit-log |

**Critérios de Aceite:**

- 001.1 Toda operação `create`, `update` ou `delete` em entidade de negócio (`Opportunity`, `Account`, `Contact`, `Partner`, `Goal`, `Activity`, `OpportunityPartnerCommission`) gera exatamente um registro em `audit_logs`.
- 001.2 Não existe operação de escrita em entidade de negócio sem o respectivo registro de auditoria correspondente (cobertura de 100%).
- 001.3 A transição de estágio e o encerramento de oportunidade (Ganho/Perdido) são tratados como `update` e geram registro de auditoria.
- 001.4 A geração do snapshot imutável de comissão ao mover oportunidade para "Ganho" gera registro de auditoria com `user_id` e timestamp.

**Cross-ref:** REQ-006 deste módulo; FRD-pipeline-06, FRD-pipeline-07; NFR-AUD-01; rule `.forge/rules/domain/audit-immutability.md`

### REQ-002 — Conteúdo mínimo do registro de auditoria

**Como** Tenant Admin **quero** que cada registro de auditoria contenha autor, entidade, ação, delta e timestamp **para** reconstruir com precisão o histórico de qualquer entidade.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD RN-024; data model § 3 (tabela `audit_logs`); PRD §8.6 |
| **Módulo** | audit-log |

**Critérios de Aceite:**

- 002.1 Cada registro contém os campos obrigatórios e não nulos: `id`, `tenant_id`, `user_id`, `entity_type`, `entity_id`, `action`, `delta_json` e `created_at`.
- 002.2 `user_id` identifica o autor humano ou de sistema responsável pela operação; nunca é vazio.
- 002.3 `action` assume exclusivamente um dos valores canônicos da seção 4 (`create`, `update`, `delete`).
- 002.4 `created_at` é o timestamp do servidor no momento da persistência e nunca é fornecido nem sobrescrito pelo módulo de escrita chamador.
- 002.5 O registro não possui campo `updated_at`: não há semântica de atualização de registro de auditoria.

**Cross-ref:** REQ-001, REQ-003; data model § 3 Audit Log (BC-15); NFR-AUD-01

### REQ-003 — Delta antes/depois da operação

**Como** Gestor de BU **quero** ver o que mudou em cada operação (estado anterior e posterior) **para** entender a natureza exata de cada alteração.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD RN-024; data model § 3 (`delta_json JSONB`); PRD §8.6 |
| **Módulo** | audit-log |

**Critérios de Aceite:**

- 003.1 O campo `delta_json` registra o diff da entidade com os valores anteriores (antes) e posteriores (depois) dos atributos alterados na operação.
- 003.2 Para `action = create`, o delta representa o estado inicial completo da entidade (sem estado anterior).
- 003.3 Para `action = delete`, o delta representa o último estado conhecido da entidade antes da exclusão (sem estado posterior).
- 003.4 Para `action = update`, o delta contém apenas os atributos efetivamente alterados, cada um com seu par antes/depois.
- 003.5 Valores monetários presentes no delta são representados em centavos inteiros, sem uso de ponto flutuante.

**Cross-ref:** REQ-002, REQ-004; rule `.forge/rules/domain/money-as-cents.md`; PBT-03

### REQ-004 — Mascaramento de PII no delta

**Como** responsável por conformidade LGPD **quero** que dados pessoais sejam mascarados no delta antes da persistência **para** evitar exposição de PII em texto claro na trilha de auditoria.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD RN-025; NFRD NFR-PRIV-01; LGPD; rule `.forge/rules/architecture/security-and-compliance.md` |
| **Módulo** | audit-log |

**Critérios de Aceite:**

- 004.1 Antes da persistência, os campos de PII de contatos (nome, e-mail, celular) presentes no `delta_json` são mascarados ou omitidos.
- 004.2 Após a persistência, nenhum valor de PII de contato é recuperável em texto claro a partir do `delta_json`.
- 004.3 O mascaramento ocorre por entidade auditada conforme configuração de campos PII por `entity_type`.
- 004.4 O mascaramento preserva a capacidade de saber que um campo PII foi alterado (presença do campo no delta), sem revelar seu conteúdo.
- 004.5 O mascaramento aplica-se independentemente do módulo de escrita de origem (garantia centralizada no AuditService).

**Cross-ref:** REQ-006; RNF-002; NFR-PRIV-01; RN-025; PBT-04

### REQ-005 — Isolamento por tenant na trilha

**Como** tenant do Azim CRM **quero** que minha trilha de auditoria seja completamente isolada de outros tenants **para** garantir confidencialidade dos meus dados em ambiente multi-tenant.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | data model § 3 (RLS por `tenant_id`); DEC-005; rule `.forge/rules/architecture/security-and-compliance.md` |
| **Módulo** | audit-log |

**Critérios de Aceite:**

- 005.1 Todo registro de `audit_logs` possui `tenant_id` não nulo, atribuído a partir do contexto da operação de origem.
- 005.2 Toda consulta à trilha é filtrada por `tenant_id` via Row-Level Security; nenhum registro de outro tenant é retornado em nenhuma circunstância.
- 005.3 Nenhum chamador pode definir manualmente um `tenant_id` distinto do contexto autenticado da operação.

**Cross-ref:** REQ-007, REQ-008; data model § 3; PBT-05

### REQ-006 — Recepção centralizada via AuditService

**Como** arquiteto da plataforma **quero** que toda escrita de auditoria passe por um serviço centralizado **para** garantir mascaramento de PII e política append-only de forma uniforme.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | README do módulo § 4/§ 13; data model § 4 (fronteiras de persistência); decisão arquitetural |
| **Módulo** | audit-log |

**Critérios de Aceite:**

- 006.1 Os módulos de escrita publicam AuditEvent ao AuditService; nenhum módulo escreve diretamente na tabela `audit_logs`.
- 006.2 O AuditService aplica o mascaramento de PII (REQ-004) antes de qualquer persistência.
- 006.3 O AuditService é a única via de inserção em `audit_logs`.
- 006.4 O AuditEvent transporta, no mínimo: `entity_type`, `entity_id`, `action`, delta bruto, `user_id` e contexto de `tenant_id`.

**Cross-ref:** REQ-001, REQ-004, REQ-005; data model § 4 Fronteiras de Persistência

### REQ-007 — Consulta somente-leitura da trilha

**Como** Tenant Admin **quero** consultar a trilha de auditoria com filtros **para** investigar alterações por entidade, autor e período.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | README do módulo § 4/§ 9; FRD matriz RBAC (Ver AuditLog); data model § 3 (API somente leitura) |
| **Módulo** | audit-log |

**Critérios de Aceite:**

- 007.1 A trilha é exposta exclusivamente em leitura; não há operação de criação, edição ou exclusão de registros via API.
- 007.2 A consulta aceita filtros por `entity_type`, `entity_id`, `user_id` e período (intervalo de `created_at`).
- 007.3 É possível obter o histórico de auditoria de uma entidade específica informando `entity_type` e `entity_id`.
- 007.4 Os resultados são paginados e ordenados por `created_at`.
- 007.5 A consulta não altera o estado da trilha (operação idempotente quanto a efeitos colaterais).

**Cross-ref:** REQ-005, REQ-008; PBT-06

### REQ-008 — Restrição de acesso à consulta por papel e escopo

**Como** responsável por segurança **quero** restringir a consulta da trilha a papéis administrativos com escopo adequado **para** evitar exposição indevida do histórico.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD matriz RBAC (Ver AuditLog: Tenant — TAdmin; sua BU — GestorBU); README do módulo § 9 |
| **Módulo** | audit-log |

**Critérios de Aceite:**

- 008.1 Apenas os papéis Tenant Admin e Gestor de BU podem consultar a trilha de auditoria.
- 008.2 O Tenant Admin acessa a trilha de todo o seu tenant.
- 008.3 O Gestor de BU acessa apenas registros associados às BUs das quais é membro.
- 008.4 Papéis sem permissão (Vendedor, Viewer, Platform Operator por padrão) recebem resposta de acesso negado ao tentar consultar a trilha.
- 008.5 O acesso de suporte autorizado do Platform Operator, quando concedido, é ele próprio registrado como evento auditável.

**Cross-ref:** REQ-005, REQ-007; FRD § matriz RBAC; NFR-SEG (acesso de Platform Operator)

## 6. Requisitos Não-Funcionais

### RNF-001 — Imutabilidade append-only garantida na persistência

| Campo | Valor |
|-------|-------|
| **Categoria** | Integridade |
| **Prioridade** | Must |
| **Origem** | NFRD NFR-AUD-01; FRD RN-024; rule `.forge/rules/domain/audit-immutability.md`; data model § 3 |
| **Módulo** | audit-log |

**Descrição:**

A tabela `audit_logs` é estritamente append-only: uma vez inserido, nenhum registro pode ser alterado, excluído ou truncado, inclusive pelo papel de aplicação. A imutabilidade é garantida na camada de persistência, não apenas pela disciplina da aplicação.

**Critérios de Aceite:**

- RNF-001.1 Qualquer tentativa de UPDATE, DELETE ou TRUNCATE em `audit_logs` pelo papel de aplicação é rejeitada com erro.
- RNF-001.2 Teste de integração comprova que a tentativa de UPDATE/DELETE retorna exceção e o registro permanece inalterado.
- RNF-001.3 Zero registros de auditoria são alterados ou removidos automaticamente ao longo do ciclo de vida do sistema.

**Cross-ref:** REQ-001, REQ-002; NFR-AUD-01; REST-NF-06; PBT-01

### RNF-002 — Ausência de PII em logs operacionais e traces

| Campo | Valor |
|-------|-------|
| **Categoria** | Privacidade |
| **Prioridade** | Must |
| **Origem** | NFRD NFR-PRIV-01; FRD RN-025; LGPD Art. 6º; rule `.forge/rules/architecture/observability.md` |
| **Módulo** | audit-log |

**Descrição:**

Os logs operacionais, mensagens de erro e traces emitidos pelo módulo jamais contêm PII de contatos (nome, e-mail, celular) em texto claro, complementando o mascaramento já aplicado ao `delta_json` persistido.

**Critérios de Aceite:**

- RNF-002.1 Logs estruturados do módulo contêm apenas `correlation_id`, `tenant_id`, `entity_type`, `entity_id` e metadados não sensíveis.
- RNF-002.2 Scan automatizado de logs não detecta padrões de e-mail nem de telefone brasileiro nos logs do módulo.
- RNF-002.3 Mensagens de erro do módulo não expõem conteúdo de `delta_json` nem valores de campos PII.

**Cross-ref:** REQ-004; NFR-PRIV-01; RN-025; PBT-04

### RNF-003 — Não bloqueio do caso de uso principal

| Campo | Valor |
|-------|-------|
| **Categoria** | Performance |
| **Prioridade** | Should |
| **Origem** | README do módulo § 15; NFRD NFR-PERF-02; VAL-AUDIT-02 |
| **Módulo** | audit-log |

**Descrição:**

A persistência de auditoria não deve degradar a latência percebida do caso de uso de negócio principal, preservando o orçamento de desempenho definido para as operações de escrita.

**Critérios de Aceite:**

- RNF-003.1 A criação/edição de oportunidade, incluindo gravação de auditoria, atende ao SLO de p95 ≤ 500 ms a 50 RPS (requisições por segundo), conforme NFR-PERF-02.
- RNF-003.2 A estratégia de persistência de auditoria (síncrona ou em background com garantia de entrega) preserva a garantia de que toda escrita gere registro (REQ-001), sem perda em caso de processamento assíncrono.

**Cross-ref:** REQ-001; NFR-PERF-02; VAL-AUDIT-02

### RNF-004 — Alerta em falha de auditoria (sem perda silenciosa)

| Campo | Valor |
|-------|-------|
| **Categoria** | Observabilidade |
| **Prioridade** | Must |
| **Origem** | README do módulo § 17; RISK-AUDIT-01; rule `.forge/rules/architecture/observability.md` |
| **Módulo** | audit-log |

**Descrição:**

A falha na persistência de um registro de auditoria nunca pode ocorrer de forma silenciosa. Toda falha é observável e dispara alerta operacional imediato.

**Critérios de Aceite:**

- RNF-004.1 O módulo expõe o contador `audit_events_received_total` e o contador `audit_insert_failures_total`.
- RNF-004.2 Um alerta é disparado imediatamente quando `audit_insert_failures_total` for maior que zero em janela de 5 minutos.
- RNF-004.3 Falhas de persistência de auditoria são registradas em logging separado (sem PII), permitindo reprocessamento via mecanismo de dead-letter/retry.

**Cross-ref:** REQ-001; RISK-AUDIT-01; NFR-OBS

### RNF-005 — Retenção sem purge automático

| Campo | Valor |
|-------|-------|
| **Categoria** | Auditoria |
| **Prioridade** | Must |
| **Origem** | NFRD NFR-AUD-03; rule `.forge/rules/domain/audit-immutability.md`; VAL-AUDIT-01; VAL-08 |
| **Módulo** | audit-log |

**Descrição:**

Os dados de auditoria não possuem TTL (time to live — tempo de vida) nem purge automático. A retenção é indefinida, exceto por obrigação legal explícita, cujo descarte exige papel administrativo separado e aprovação dupla.

**Critérios de Aceite:**

- RNF-005.1 Nenhum job de purge automático está configurado para a tabela `audit_logs`.
- RNF-005.2 O papel de aplicação não possui permissão de DELETE em `audit_logs`.
- RNF-005.3 Qualquer descarte legal segue processo documentado com registro prévio e aprovação dupla.

**Cross-ref:** RNF-001; NFR-AUD-03; VAL-AUDIT-01; ADR-0003 (`politica-retencao-logs-e-auditoria`, a definir)

### RNF-006 — Health check de capacidade de inserção

| Campo | Valor |
|-------|-------|
| **Categoria** | Operabilidade |
| **Prioridade** | Should |
| **Origem** | README do módulo § 17; rule `.forge/rules/architecture/observability.md` |
| **Módulo** | audit-log |

**Descrição:**

O módulo expõe verificação de saúde que confirma conectividade com o banco e a permissão de INSERT na tabela `audit_logs`, sinalizando indisponibilidade antes que escritas de negócio sejam afetadas.

**Critérios de Aceite:**

- RNF-006.1 O health check verifica conectividade com o banco de auditoria.
- RNF-006.2 O health check verifica que o papel de aplicação possui permissão de INSERT em `audit_logs`.
- RNF-006.3 A ausência de qualquer das condições acima resulta em estado de saúde degradado/indisponível reportado.

**Cross-ref:** RNF-004; NFR-OBS

## 7. Property-Based Testing

### PBT-01 — Imutabilidade append-only

**Mapeia para:** REQ-001, REQ-002, RNF-001
**Tipo:** State machine | Invariante matemática

**Propriedade:**

> Para qualquer registro de auditoria já persistido e qualquer sequência de tentativas de UPDATE, DELETE ou TRUNCATE pelo papel de aplicação, o registro permanece byte-a-byte idêntico ao estado inserido e toda tentativa de mutação é rejeitada.

### PBT-02 — Conservação: toda escrita gera exatamente um registro com autor e delta

**Mapeia para:** REQ-001, REQ-002, REQ-003
**Tipo:** Invariante matemática | Atomicidade

**Propriedade:**

> Para qualquer sequência de operações de escrita geradas sobre entidades de negócio, o número de registros de auditoria resultantes é igual ao número de operações de escrita, e cada registro possui `user_id` não vazio e `delta_json` não vazio coerente com a operação.

### PBT-03 — Round-trip do delta

**Mapeia para:** REQ-003
**Tipo:** Round-trip

**Propriedade:**

> Para qualquer par (estado anterior, estado posterior) de uma operação `update`, aplicar o `delta_json` (parte "depois") sobre o estado anterior reproduz exatamente o estado posterior nos atributos alterados; para `create`, o delta reconstrói o estado inicial; para `delete`, o delta reconstrói o último estado conhecido.

### PBT-04 — Mascaramento de PII no delta

**Mapeia para:** REQ-004, RNF-002
**Tipo:** Invariante (anti-vazamento)

**Propriedade:**

> Para qualquer entidade gerada contendo campos PII (nome, e-mail, celular) com valores arbitrários, o `delta_json` persistido não contém nenhum dos valores PII originais em texto claro, ainda que registre a presença da alteração do campo.

### PBT-05 — Isolamento por tenant

**Mapeia para:** REQ-005, REQ-008
**Tipo:** Invariante (anti-vazamento) | Anti-enumeração

**Propriedade:**

> Para qualquer conjunto de registros de auditoria pertencentes a múltiplos tenants e qualquer consulta executada no contexto de um `tenant_id` específico, o resultado contém exclusivamente registros desse `tenant_id`, independentemente dos filtros aplicados.

### PBT-06 — Consulta somente-leitura não muta a trilha

**Mapeia para:** REQ-007
**Tipo:** Idempotência

**Propriedade:**

> Para qualquer estado da trilha e qualquer combinação de filtros de consulta, executar a consulta uma ou várias vezes não altera o conjunto, a ordem nem o conteúdo dos registros persistidos em `audit_logs`.

## 8. Glossário local

| Termo | Definição |
|---|---|
| AuditLog | Registro imutável de uma operação de escrita em entidade de negócio. |
| AuditEvent | Evento publicado por um módulo de escrita ao AuditService, contendo entidade, ação, delta bruto, autor e contexto de tenant. |
| AuditService | Serviço de domínio centralizado que recebe AuditEvent, aplica mascaramento de PII e persiste em `audit_logs`. Única via de inserção. |
| PiiMasker | Serviço de domínio que mascara campos PII (nome, e-mail, celular) no `delta_json` por `entity_type`. |
| delta_json | Campo JSONB com o diff da entidade (antes/depois) da operação, com PII mascarada (RN-025). |
| entity_type | Nome da entidade de negócio auditada (ex.: `Opportunity`, `Account`, `Contact`). |
| action | Tipo de operação auditada: `create`, `update` ou `delete`. |
| append-only | Política de inserção sem UPDATE nem DELETE, garantida na camada de persistência. |
| PII | Personally Identifiable Information — informação pessoal identificável (nome, e-mail, celular de contatos). |
| RLS | Row-Level Security — segurança em nível de linha; isolamento por `tenant_id` na camada de banco. |
| tenant_id | Identificador único (UUID) do tenant; chave de RLS, transversal a todas as tabelas. |
| objeto de valor | Tipo de domínio imutável definido por seus atributos, sem identidade própria (ex.: representação de valor monetário em centavos). |

## 9. Fora do escopo do MVP

- Política de retenção e arquivamento de `audit_logs` sob LGPD: a ser definida com a equipe jurídica antes do go-live (VAL-AUDIT-01 / VAL-08 / NFR-AUD-03 / ADR-0003).
- Mecanismo de descarte legal (purge) com papel administrativo separado e aprovação dupla: processo a documentar; não implementado no MVP.
- Relatórios analíticos e dashboards derivados de `audit_logs`: pertencem ao módulo de Reporting (BC-07).
- Avaliação de retenção sob SOX para `OpportunityPartnerCommission` (snapshot de comissão): ponto a validar com jurídico (VAL-AUDIT-03).
- Decisão definitiva entre persistência síncrona e assíncrona (ex.: Outbox Pattern) da auditoria: pertence ao `design.md` (VAL-AUDIT-02).
- Exportação da trilha de auditoria para sistemas externos.

## 10. Referências cruzadas

| Referência | Origem |
|---|---|
| RN-024 — AuditLog imutável para toda escrita em entidade de negócio | docs/product/frd-nfrd/frd.md |
| RN-025 — PII de contatos não aparece em logs | docs/product/frd-nfrd/frd.md |
| NFR-AUD-01 — AuditLog imutável para toda escrita | docs/product/frd-nfrd/nfrd.md § 8.9 |
| NFR-AUD-03 — Retenção de dados de auditoria | docs/product/frd-nfrd/nfrd.md § 8.9 |
| NFR-PRIV-01 — PII ausente de logs | docs/product/frd-nfrd/nfrd.md |
| NFR-PERF-02 — SLO de criação/edição de oportunidade | docs/product/frd-nfrd/nfrd.md |
| REST-NF-06 — Tabelas de auditoria append-only | docs/product/frd-nfrd/nfrd.md |
| Tabela `audit_logs` (BC-15) | docs/product/data-model/data-model.md § 3 |
| Fronteiras de persistência (AuditService) | docs/product/data-model/data-model.md § 4 |
| Linguagem ubíqua (Account/Contact PII, BU, tenant_id) | docs/product/glossary/ubiquitous-language.md |
| README do módulo Audit Log | docs/product/modules/audit-log/README.md |
| Money como centavos inteiros | rule `.forge/rules/domain/money-as-cents.md` |
| Imutabilidade de auditoria | rule `.forge/rules/domain/audit-immutability.md` |
| Segurança e compliance | rule `.forge/rules/architecture/security-and-compliance.md` |
| Observabilidade | rule `.forge/rules/architecture/observability.md` |
| ADR-0003 — política de retenção de logs e auditoria (a definir) | docs/product/adr/ (sugerido) |
