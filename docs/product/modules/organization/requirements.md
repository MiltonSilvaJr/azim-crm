# ORG — Organization Management
**Requisitos Funcionais e Não-Funcionais**

- Versão: 0.1.0
- Data: 2026-06-11
- Status: Rascunho para revisão
- Referência pai: docs/product/frd-nfrd/frd.md § RF-03 (FRD-org-01, FRD-org-02)

## Histórico de Versões

| Versão | Data | Status | Descrição da alteração |
|--------|------|--------|------------------------|
| 0.1.0 | 2026-06-11 | Rascunho para revisão | Criação inicial do documento de requisitos do módulo organization |

## 1. Visão Geral

O módulo **organization** (Bounded Context BC-08 — Organization Management; subdomínio de suporte; deployable `azim-api`) é o provedor do modelo organizacional do Azim CRM. Ele administra Business Units (unidades de negócio, doravante "BU"), usuários do tenant, papéis e memberships (vínculos usuário-BU-papel), convites por e-mail e as configurações de pipeline por BU: estágios (stages), canais de origem (origin_channels) e motivos de perda (loss_reasons).

É um módulo Tier 1: todos os demais módulos dependem do contexto organizacional definido aqui — `tenant_id`, `bu_id`, `user_id` e papéis — consumido via padrão Conformist no context map. O módulo é a fonte de verdade do controle de acesso baseado em papel (RBAC — Role-Based Access Control) por BU e do isolamento por tenant via Row-Level Security (RLS).

A identidade do usuário (autenticação, credenciais, tokens de sessão) pertence ao módulo **authentication**; o módulo organization delega a esse módulo a criação da identidade no aceite do convite, mantendo apenas o vínculo `identity_uid`. O provisionamento do tenant pertence ao módulo **tenant-administration**.

## 2. Escopo

### 2.1 Incluído

- CRUD de Business Units com nome único por tenant (MSG-015).
- Inativação lógica de BU com preservação de histórico e bloqueio quando há oportunidades ativas (MSG-016).
- Convite de usuário por e-mail com token temporário, delegando a criação de identidade ao módulo authentication.
- Aceite de convite e ativação do usuário.
- Gestão de UserMemberships: vínculo usuário-BU com papel; um usuário pode ter papéis distintos em BUs distintas.
- Aplicação da matriz RBAC por papel × BU (TAdmin, GestorBU, Vendedor, Viewer).
- Desativação lógica de usuário (soft-delete via `deactivated_at`) preservando histórico e integridade referencial (RN-013).
- Garantia de pelo menos um Tenant Admin ativo por tenant (MSG-017).
- Configuração de estágios de pipeline por BU (nome, probabilidade default, categoria, posição) com seed derivado do processo da Vellus.
- Configuração de canais de origem por BU com seed editável.
- Configuração de motivos de perda por BU com seed editável e mínimo de um motivo para habilitar a BU.
- Provisionamento da BU inicial e do Tenant Admin no consumo do evento `TenantProvisioned`.
- Publicação de contexto de memberships/papéis para o módulo authentication via cache, com invalidação em mudança de papel ou desativação.

### 2.2 Excluído

- Autenticação, credenciais, MFA e emissão de tokens de sessão (pertencem ao módulo authentication).
- Provisionamento, suspensão e branding de tenant (pertencem ao módulo tenant-administration).
- Gestão de oportunidades, atividades, contas e parceiros (pertencem aos respectivos módulos de negócio).
- Autorização granular por recurso individual dentro de cada módulo consumidor (cada módulo aplica RBAC sobre o contexto exportado por organization).

### 2.3 Fora do escopo do MVP

Detalhado na seção 9.

## 3. Personas / Atores

| Persona | Identificador técnico | Descrição |
|---------|----------------------|-----------|
| Tenant Admin | `TAdmin` | Administra todas as BUs e configurações do tenant; convida e desativa usuários; configura estágios, canais e motivos de perda. |
| Gestor de BU | `GestorBU` | Gerencia a sua BU: pipeline, metas e equipe; escopo restrito às BUs onde tem membership de gestor. |
| Vendedor | `Vendedor` | Atua no pipeline da BU; escopo restrito às suas oportunidades. Consumidor do contexto organizacional. |
| Viewer | `Viewer` | Somente leitura no escopo designado. |
| Platform Operator | `PlatOp` | Operador da plataforma; bloqueado por padrão de acessar dados comerciais do tenant. Atua no provisionamento via tenant-administration. |
| Usuário convidado | `InvitedUser` | Pessoa que recebeu convite por e-mail e ainda não ativou a conta. |

## 4. Lista canônica de papéis (RBAC)

Papéis válidos para `user_memberships.papel`. PlatOp não é um membership de BU: é um papel de plataforma gerido fora do tenant.

| Papel | Identificador técnico | Escopo | Pode escrever configuração do tenant |
|-------|----------------------|--------|--------------------------------------|
| Tenant Admin | `TAdmin` | Todas as BUs do tenant | Sim |
| Gestor de BU | `GestorBU` | BU(s) onde tem membership | Apenas dentro da sua BU (parceiros, metas, contas, oportunidades) |
| Vendedor | `Vendedor` | Suas oportunidades dentro da BU | Não |
| Viewer | `Viewer` | Leitura no escopo designado | Não |

### 4.1 Lista canônica de categorias de estágio

Valores válidos para `stages.category`.

| Categoria | Identificador técnico | Significado | Cardinalidade no seed |
|-----------|----------------------|-------------|------------------------|
| Aberta | `open` | Estágio de pipeline ativo; oportunidade em andamento | Um ou mais |
| Ganha | `won` | Estágio terminal de sucesso; consolida valores e comissão | Exatamente um |
| Perdida | `lost` | Estágio terminal de perda; exige motivo de perda (RN-004) | Exatamente um |

### 4.2 Lista canônica de estados do convite

Estados de `user_invitations`.

| Estado | Identificador técnico | Descrição |
|--------|----------------------|-----------|
| Pendente | `pending` | Convite emitido, token válido, ainda não aceito |
| Aceito | `accepted` | Convite aceito; usuário ativado; identidade criada no authentication |
| Expirado | `expired` | Token expirado sem aceite (RN-030; janela configurável, default inferido 72h) |
| Revogado | `revoked` | Convite cancelado por TAdmin antes do aceite |

## 5. Requisitos Funcionais

### Req 1 — Criar Business Unit com nome único por tenant

**Como** Tenant Admin **quero** criar uma Business Unit **para** organizar pipeline, equipe e configurações de uma unidade de negócio do tenant.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § FRD-org-01 (RF-03); data-model.md § BC-08 |
| **Módulo** | organization |

**Critérios de Aceite:**

- 1.1 Uma BU é criada com `name` não vazio e fica vinculada ao `tenant_id` do contexto autenticado.
- 1.2 A criação é bloqueada quando já existe uma BU com o mesmo `name` no mesmo tenant, retornando a mensagem MSG-015.
- 1.3 A comparação de unicidade considera o par (`tenant_id`, `name`) e é insensível a espaços extras nas extremidades.
- 1.4 Apenas o papel TAdmin pode criar BU; demais papéis recebem negação de autorização.
- 1.5 A BU criada nasce com `active = true` e fica disponível para associação de usuários e oportunidades.
- 1.6 A criação publica o evento `BUCreated` para o audit-log.

**Cross-ref:** Req 9 (seed de estágios na criação), Req 11 (motivo de perda obrigatório para habilitar), MSG-015

---

### Req 2 — Editar e inativar Business Unit preservando histórico

**Como** Tenant Admin **quero** editar e inativar uma Business Unit **para** manter o cadastro atualizado sem perder o histórico comercial vinculado.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § FRD-org-01 (FE-org01-02, MSG-016); README § 4 |
| **Módulo** | organization |

**Critérios de Aceite:**

- 2.1 É possível atualizar o `name` da BU respeitando a unicidade da Req 1.2.
- 2.2 A inativação é lógica: a BU passa a `active = false` sem exclusão física do registro.
- 2.3 A inativação é bloqueada quando a BU possui oportunidades ativas, exibindo a contagem de oportunidades afetadas (MSG-016).
- 2.4 Toda integridade referencial (`bu_id` em oportunidades, atividades, memberships e configurações) permanece intacta após a inativação.
- 2.5 Uma BU inativa não aparece como opção de seleção ao criar nova oportunidade, mas seu histórico permanece consultável.
- 2.6 A edição do `name` da BU é permitida ao TAdmin e ao GestorBU da própria BU; a inativação é exclusiva do TAdmin.

**Cross-ref:** Req 7 (paralelo da desativação de usuário), MSG-016, RN-013

---

### Req 3 — Convidar usuário por e-mail com token temporário

**Como** Tenant Admin **quero** convidar um usuário por e-mail **para** dar acesso de um novo membro ao tenant, definindo papel e BU(s) de atuação.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § FRD-org-02 (RF-03); README § 4; glossário ubíquo BC-08 (convite) |
| **Módulo** | organization |

**Critérios de Aceite:**

- 3.1 O convite é emitido para um endereço de e-mail, com papel e ao menos uma BU de atuação definidos.
- 3.2 O convite gera um token temporário com expiração configurável (RN-030; default inferido de 72 horas) e nasce no estado `pending`.
- 3.3 O e-mail de convite é enviado por meio do módulo notification-delivery; falha de envio não consolida convite inválido (operação atômica entre persistência e enfileiramento do envio).
- 3.4 A criação da identidade de autenticação é delegada ao módulo authentication; organization não armazena credenciais.
- 3.5 Convidar um e-mail que já corresponde a usuário ativo no tenant é bloqueado.
- 3.6 O convite publica o evento `UserInvited` para o audit-log.
- 3.7 Um convite `pending` pode ser revogado pelo TAdmin, transicionando para `revoked` e invalidando o token.

**Cross-ref:** Req 4 (aceite), módulo authentication (criação de identidade), RN-030, MSG da matriz RBAC

---

### Req 4 — Aceitar convite e ativar usuário

**Como** usuário convidado **quero** aceitar o convite recebido **para** ativar minha conta e acessar as BUs às quais fui designado.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § FRD-org-02 (passo 4); README § 4, § 10 (UserActivated) |
| **Módulo** | organization |

**Critérios de Aceite:**

- 4.1 O aceite só é permitido para convite no estado `pending` com token válido e não expirado.
- 4.2 No aceite, o usuário é criado/ativado com `active = true` e o `identity_uid` retornado pelo módulo authentication é vinculado.
- 4.3 No aceite, são criados os memberships definidos no convite (papel por BU).
- 4.4 Aceitar um convite já `accepted` é idempotente: não cria usuários nem memberships duplicados.
- 4.5 Aceitar um convite `expired` ou `revoked` é rejeitado com mensagem objetiva.
- 4.6 O aceite transiciona o convite para `accepted` e publica o evento `UserActivated` (consumido por authentication para carregar memberships).

**Cross-ref:** Req 3, Req 5, PBT-03, PBT-04

---

### Req 5 — Gerenciar memberships (papel por BU)

**Como** Tenant Admin **quero** atribuir e alterar o papel de um usuário em cada BU **para** controlar o que cada pessoa pode fazer em cada unidade de negócio.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § FRD-org-02; data-model.md § user_memberships; README § 4 |
| **Módulo** | organization |

**Critérios de Aceite:**

- 5.1 Um usuário pode ter no máximo um membership por BU (unicidade `tenant_id`, `user_id`, `bu_id`).
- 5.2 Um mesmo usuário pode ter papéis distintos em BUs distintas (ex.: Vendedor na Vellus e GestorBU na Axis).
- 5.3 O `papel` de um membership deve pertencer à lista canônica da seção 4 (`TAdmin`, `GestorBU`, `Vendedor`, `Viewer`).
- 5.4 Apenas TAdmin cria, altera ou remove memberships.
- 5.5 Toda alteração de membership dispara invalidação do cache de memberships do usuário afetado (ver RNF 5).
- 5.6 A remoção de membership é bloqueada quando viola a garantia de Tenant Admin ativo (Req 8).

**Cross-ref:** Req 6, Req 8, RNF 5, PBT-02

---

### Req 6 — Aplicar matriz RBAC por papel × BU

**Como** plataforma **quero** aplicar a matriz de permissões por papel e BU **para** garantir que cada usuário só execute ações autorizadas no seu escopo.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § 15 Matriz de Permissões Funcionais; NFRD § NFR-SEG-03; TRD § 12.3 |
| **Módulo** | organization |

**Critérios de Aceite:**

- 6.1 O módulo expõe, para os módulos consumidores, o conjunto de papéis ativos do usuário por BU (contexto RBAC).
- 6.2 Operações de escrita de configuração do tenant (BU, estágios, canais, motivos de perda, convites, desativação) são restritas a TAdmin, conforme a matriz da seção 15 do FRD.
- 6.3 Um usuário sem membership ativo em uma BU não obtém contexto de acesso àquela BU.
- 6.4 A negação de autorização é o comportamento padrão (deny-by-default) quando não há papel que conceda a ação.
- 6.5 Edição de snapshot de comissão é bloqueada para todos os papéis (consistência com a matriz; reforço documental, regra aplicada no módulo consumidor).

**Cross-ref:** RNF 1, RNF 2, FRD § 15, NFR-SEG-03

---

### Req 7 — Desativar usuário preservando histórico

**Como** Tenant Admin **quero** desativar um usuário **para** revogar o acesso sem perder o histórico de registros vinculados a ele.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § FRD-org-02 (RN-013, FE-org02-02, MSG-018); README § 4 |
| **Módulo** | organization |

**Critérios de Aceite:**

- 7.1 A desativação é lógica: define `active = false` e registra `deactivated_at`, sem exclusão física (RN-013).
- 7.2 Um usuário desativado não consegue autenticar (efeito via invalidação de cache e propagação ao authentication).
- 7.3 Todas as FKs e histórico (oportunidades, atividades, audit logs) permanecem intactos; o nome histórico do usuário continua exibido nos registros.
- 7.4 Ao desativar um usuário com atividades futuras atribuídas, o sistema alerta e solicita reatribuição antes de confirmar (MSG-018).
- 7.5 A desativação publica `UserDeactivated`, consumido pelo authentication para invalidar o cache de memberships.
- 7.6 Apenas TAdmin desativa usuários.

**Cross-ref:** Req 8, RN-013, MSG-018, PBT-05

---

### Req 8 — Garantir ao menos um Tenant Admin ativo

**Como** plataforma **quero** impedir a remoção do último Tenant Admin ativo **para** evitar que um tenant fique sem administrador e em bloqueio operacional.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § FRD-org-02 (FE-org02-01, MSG-017); README § 19 (RISK-ORG-01) |
| **Módulo** | organization |

**Critérios de Aceite:**

- 8.1 A desativação de usuário é bloqueada quando ela deixaria o tenant sem nenhum TAdmin ativo, retornando MSG-017.
- 8.2 A remoção ou rebaixamento do membership TAdmin é bloqueada nas mesmas condições.
- 8.3 A verificação considera apenas usuários com `active = true` e membership de papel `TAdmin`.
- 8.4 A operação bloqueada não altera nenhum estado (atomicidade: rejeita por completo).

**Cross-ref:** Req 5, Req 7, MSG-017, PBT-02

---

### Req 9 — Configurar estágios de pipeline por BU com seed da Vellus

**Como** Tenant Admin **quero** configurar os estágios do pipeline de cada BU **para** modelar o funil comercial com probabilidade e categoria adequados, partindo de um seed pronto.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § FRD-org-01 (passo 3, RN-001); TRD § DEC-001; data-model.md § stages |
| **Módulo** | organization |

**Critérios de Aceite:**

- 9.1 Cada estágio tem `name`, `probability` (0 a 100), `category` (`open` | `won` | `lost`) e `position` (ordem no funil).
- 9.2 Ao criar uma nova BU, os estágios seed derivados do processo da Vellus são pré-carregados (Lead → ... → Ganho/Perdido), cada um com probabilidade default e categoria.
- 9.3 O seed contém ao menos um estágio `open`, exatamente um `won` e exatamente um `lost`.
- 9.4 O `name` de estágio é único por BU (`tenant_id`, `bu_id`, `name`).
- 9.5 O TAdmin pode adicionar, renomear, reordenar e ajustar a probabilidade dos estágios; GestorBU pode configurar estágios da sua BU conforme a matriz RBAC.
- 9.6 A configuração de estágios sempre preserva exatamente um estágio `won` e um `lost`, impedindo remover o último de cada categoria terminal.
- 9.7 As `position` dos estágios de uma BU são distintas entre si e definem a ordem de exibição no Kanban.

**Cross-ref:** Req 1, módulo opportunity-pipeline (consumo de stages), DEC-001, DEC-007, PBT-06, PBT-07

---

### Req 10 — Configurar canais de origem por BU

**Como** Tenant Admin **quero** configurar os canais de origem por BU **para** classificar a procedência das oportunidades segundo a realidade comercial da unidade.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § FRD-org-01 (passo 4); glossário ubíquo BC-01 (origin_channel); data-model.md § origin_channels |
| **Módulo** | organization |

**Critérios de Aceite:**

- 10.1 Cada BU nasce com uma lista seed editável de canais de origem: Parceiro, Indicação, Prospecção ativa, Inbound, Evento, Base/Cliente existente, Outro.
- 10.2 O TAdmin pode adicionar, renomear e desativar canais de origem da BU.
- 10.3 O canal "Parceiro" implica que a oportunidade exige `partner_id` (RN-003/RN-008, regra aplicada no módulo consumidor); a configuração não remove esse vínculo semântico.
- 10.4 Os canais ativos da BU são expostos ao módulo opportunity-pipeline.

**Cross-ref:** Req 11, módulo opportunity-pipeline, RN-003

---

### Req 11 — Configurar motivos de perda por BU

**Como** Tenant Admin **quero** configurar os motivos de perda por BU **para** habilitar o registro padronizado da razão de perda ao encerrar uma oportunidade.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | FRD § FRD-org-01 (passo 5); glossário ubíquo BC-01 (motivo_perda, RN-004); data-model.md § loss_reasons |
| **Módulo** | organization |

**Critérios de Aceite:**

- 11.1 Cada BU possui uma lista editável de motivos de perda.
- 11.2 A BU só é habilitada para operação quando possui ao menos um motivo de perda configurado.
- 11.3 O TAdmin pode adicionar, renomear e desativar motivos de perda da BU.
- 11.4 Os motivos de perda ativos da BU são expostos ao módulo opportunity-pipeline para seleção obrigatória ao mover oportunidade para `lost` (RN-004).

**Cross-ref:** Req 9 (categoria `lost`), módulo opportunity-pipeline, RN-004

---

### Req 12 — Provisionar BU inicial e Tenant Admin no provisionamento do tenant

**Como** plataforma **quero** criar a BU inicial e o Tenant Admin ao receber `TenantProvisioned` **para** que o tenant nasça operável com administrador e pipeline padrão.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | README § 11 (TenantProvisioned); TRD § eventos; FRD § RF-03 |
| **Módulo** | organization |

**Critérios de Aceite:**

- 12.1 Ao consumir `TenantProvisioned`, o módulo cria a BU inicial do tenant com o seed de estágios, canais de origem e ao menos um motivo de perda.
- 12.2 O usuário Tenant Admin inicial é criado/ativado e recebe membership `TAdmin` na BU inicial.
- 12.3 O consumo do evento é idempotente: reprocessar `TenantProvisioned` do mesmo tenant não duplica BU, usuário ou memberships.
- 12.4 Após o provisionamento, a garantia da Req 8 (ao menos um TAdmin ativo) é satisfeita.

**Cross-ref:** Req 1, Req 8, Req 9, módulo tenant-administration, PBT-03

---

### Req 13 — Exportar contexto de RBAC para o módulo authentication via cache

**Como** módulo authentication **quero** obter os memberships e papéis ativos de um usuário **para** montar o contexto de autorização da sessão sem consultar o banco a cada requisição.

| Campo | Valor |
|-------|-------|
| **Prioridade** | Must |
| **Origem** | README § 13 (cache Redis), § 19 (RISK-ORG-02); TRD § 7.2; FRD § RF-03 |
| **Módulo** | organization |

**Critérios de Aceite:**

- 13.1 O módulo mantém, no cache de memberships, o mapa `user_id → papéis por BU` por tenant.
- 13.2 O cache é invalidado explicitamente em criação, alteração e remoção de membership e em desativação de usuário.
- 13.3 O cache não armazena PII (e-mail ou display_name); apenas identificadores e papéis.
- 13.4 O contexto exportado reflete apenas usuários `active = true` e BUs `active = true`.
- 13.5 Em caso de cache indisponível, o contexto é recomposto a partir do banco sem conceder acesso indevido (degradação segura).

**Cross-ref:** Req 5, Req 7, RNF 1, RNF 5, RISK-ORG-02

## 6. Requisitos Não-Funcionais

### RNF 1 — Isolamento por tenant com defesa em profundidade

| Campo | Valor |
|-------|-------|
| **Categoria** | Segurança |
| **Prioridade** | Must |
| **Origem** | NFRD § NFR-SEG-01; TRD § DEC-006, § 12.4 |
| **Módulo** | organization |

**Descrição:**

Todo acesso a dados de organização (business_units, users, user_memberships, user_invitations, stages, origin_channels, loss_reasons) deve ser isolado por `tenant_id` em camadas independentes, de modo que a falha de uma camada não resulte em vazamento entre tenants. Vazamento entre tenants é incidente de severidade 1.

**Critérios de Aceite:**

- RNF-1.1 Toda tabela de organização carrega `tenant_id` não nulo e é filtrada por `tenant_id` do contexto corrente.
- RNF-1.2 O isolamento é garantido por filtro de aplicação e por RLS no banco, validados por testes de isolamento em CI como gate obrigatório (zero falhas; KPI-06).
- RNF-1.3 Uma tentativa de leitura ou escrita cruzada entre tenants (via API ou SQL sem contexto) é bloqueada e não retorna nenhum registro.

**Cross-ref:** NFR-SEG-01, DEC-006, PBT-01

---

### RNF 2 — RBAC verificado em todo endpoint do módulo

| Campo | Valor |
|-------|-------|
| **Categoria** | Segurança |
| **Prioridade** | Must |
| **Origem** | NFRD § NFR-SEG-03; FRD § 15; TRD § 12.3 |
| **Módulo** | organization |

**Descrição:**

Toda rota que lê ou modifica dados de organização deve verificar explicitamente as permissões RBAC do usuário autenticado segundo a matriz da seção 15 do FRD, com comportamento deny-by-default.

**Critérios de Aceite:**

- RNF-2.1 Toda combinação papel × operação de configuração do módulo tem teste automatizado cobrindo permissão e negação.
- RNF-2.2 Requisição sem papel que conceda a ação é negada com resposta de autorização, sem efeito colateral no estado.
- RNF-2.3 A verificação de RBAC na aplicação é confirmada pela RLS no banco (defesa em profundidade).

**Cross-ref:** Req 6, NFR-SEG-03, FRD § 15

---

### RNF 3 — Proteção de PII de usuários (LGPD)

| Campo | Valor |
|-------|-------|
| **Categoria** | Privacidade |
| **Prioridade** | Must |
| **Origem** | README § 16, § 17; NFRD § Privacidade; LGPD (Lei 13.709/2018) |
| **Módulo** | organization |

**Descrição:**

Os campos `users.email`, `users.display_name` e o e-mail em `user_invitations` são dados pessoais (PII). Devem ser protegidos em logs, mensagens de erro e interfaces conforme a LGPD by design.

**Critérios de Aceite:**

- RNF-3.1 E-mail e display_name nunca aparecem em texto claro em logs estruturados; são mascarados.
- RNF-3.2 Mensagens de erro não expõem PII de usuários (inclusive em fluxo de convite e enumeração de e-mail).
- RNF-3.3 Dados de usuários desativados não são exibidos em interfaces não autorizadas; nome histórico é preservado apenas nos registros de negócio vinculados.
- RNF-3.4 Desativação não equivale a anonimização nem a deleção; histórico permanece para fins de auditoria.

**Cross-ref:** RNF 6, Req 7, LGPD

---

### RNF 4 — Auditoria imutável de toda escrita

| Campo | Valor |
|-------|-------|
| **Categoria** | Auditoria |
| **Prioridade** | Must |
| **Origem** | NFRD § NFR-AUD; README § 17; rule `.forge/rules/domain/audit-immutability.md` |
| **Módulo** | organization |

**Descrição:**

Toda operação de escrita em entidade de organização (BU, usuário, membership, convite, configurações de pipeline) deve gerar um registro de auditoria append-only via o módulo audit-log.

**Critérios de Aceite:**

- RNF-4.1 Criação, alteração, desativação/inativação e configuração geram entrada de auditoria com `tenant_id`, `correlation_id`, ator, entidade e ação.
- RNF-4.2 Registros de auditoria são append-only: não são atualizados nem deletados pelo módulo.
- RNF-4.3 O registro de auditoria não contém PII em texto claro (consistente com RNF 3).

**Cross-ref:** RNF 3, RNF 6, NFR-AUD, audit-immutability

---

### RNF 5 — Disponibilidade e consistência do cache de memberships

| Campo | Valor |
|-------|-------|
| **Categoria** | Disponibilidade |
| **Prioridade** | Must |
| **Origem** | README § 13, § 19 (RISK-ORG-02); TRD § 7.2, § cache |
| **Módulo** | organization |

**Descrição:**

A consulta de BUs e papéis é crítica para todos os módulos e é servida por cache (Memorystore/Redis). O cache deve equilibrar disponibilidade e consistência, evitando que um usuário opere com permissão desatualizada após mudança de papel.

**Critérios de Aceite:**

- RNF-5.1 Mudança de membership ou desativação de usuário invalida explicitamente o cache do usuário afetado.
- RNF-5.2 O cache possui TTL configurado para limitar a janela máxima de inconsistência mesmo sem invalidação explícita.
- RNF-5.3 Indisponibilidade do cache não concede acesso indevido: o contexto é recomposto a partir do banco com deny-by-default (degradação segura).

**Cross-ref:** Req 13, RISK-ORG-02

---

### RNF 6 — Observabilidade das operações de organização

| Campo | Valor |
|-------|-------|
| **Categoria** | Observabilidade |
| **Prioridade** | Should |
| **Origem** | NFRD § NFR-OBS-01; README § 17 |
| **Módulo** | organization |

**Descrição:**

As operações de convite, desativação e configuração devem ser observáveis com logs estruturados e métricas, sem expor PII.

**Critérios de Aceite:**

- RNF-6.1 Todo log estruturado inclui `correlation_id` e `tenant_id` (e `bu_id`/`user_id` quando aplicável), sem e-mail em texto claro.
- RNF-6.2 São expostas as métricas `users_invited_total`, `users_deactivated_total` e `bu_created_total`.
- RNF-6.3 Há alerta operacional quando o último TAdmin de um tenant é alvo de desativação (consistente com MSG-017).

**Cross-ref:** RNF 3, Req 8, NFR-OBS-01

## 7. Property-Based Testing

### PBT-01 — Isolamento por tenant em qualquer consulta

**Mapeia para:** Req 6, RNF 1
**Tipo:** Invariante matemática

**Propriedade:**

> Para qualquer conjunto gerado de tenants e registros de organização, toda consulta executada no contexto de um `tenant_id` retorna exclusivamente registros daquele tenant — nunca registros de outro tenant.

---

### PBT-02 — Invariante de Tenant Admin ativo

**Mapeia para:** Req 5, Req 7, Req 8
**Tipo:** Invariante matemática

**Propriedade:**

> Para qualquer sequência gerada de operações de desativação de usuário, remoção e rebaixamento de membership, o número de Tenant Admins ativos do tenant nunca chega a zero; operações que violariam essa condição são rejeitadas sem alterar o estado.

---

### PBT-03 — Idempotência de provisionamento e aceite de convite

**Mapeia para:** Req 4, Req 12
**Tipo:** Idempotência

**Propriedade:**

> Para qualquer evento de provisionamento de tenant ou aceite de convite, reprocessar o mesmo evento/token produz o mesmo estado final, sem criar usuários, BUs ou memberships duplicados.

---

### PBT-04 — Máquina de estados do convite

**Mapeia para:** Req 3, Req 4
**Tipo:** State machine

**Propriedade:**

> Para qualquer sequência gerada de transições de convite, apenas as transições válidas são aceitas — `pending → accepted`, `pending → revoked`, `pending → expired` — e qualquer transição a partir de um estado terminal (`accepted`, `revoked`, `expired`) é rejeitada.

---

### PBT-05 — Preservação de integridade na desativação

**Mapeia para:** Req 2, Req 7
**Tipo:** Invariante matemática

**Propriedade:**

> Para qualquer usuário ou BU com registros vinculados, a desativação/inativação preserva todas as referências (`bu_id`, `owner_id`, FKs de histórico) — a contagem de registros vinculados antes e depois da operação é idêntica e nenhum registro é fisicamente removido.

---

### PBT-06 — Conservação das categorias terminais de estágio

**Mapeia para:** Req 9
**Tipo:** Invariante matemática

**Propriedade:**

> Para qualquer sequência gerada de operações de configuração de estágios em uma BU, sempre existe exatamente um estágio `won` e exatamente um estágio `lost`, e ao menos um estágio `open`; operações que violariam essa conservação são rejeitadas.

---

### PBT-07 — Unicidade e ordenação de estágios por BU

**Mapeia para:** Req 1, Req 9
**Tipo:** Invariante matemática

**Propriedade:**

> Para qualquer conjunto gerado de estágios de uma BU, os pares (`tenant_id`, `bu_id`, `name`) são únicos e as `position` formam uma ordem total sem empates dentro da BU.

## 8. Glossário local

| Termo | Definição |
|-------|-----------|
| Business Unit (BU) | Unidade de negócio dentro de um tenant; possui pipeline, estágios, canais e membros próprios. |
| membership | Vínculo entre um usuário e uma BU com papel específico; um usuário pode ter vários memberships. |
| papel (RBAC) | Papel de controle de acesso baseado em papel: TAdmin, GestorBU, Vendedor, Viewer. |
| convite | Token temporário enviado por e-mail para ativação de conta de novo usuário. |
| desativação lógica | Marcação de usuário/BU como inativo (`active = false`) sem exclusão física, preservando histórico (RN-013). |
| seed | Conjunto inicial de configurações pré-carregado ao criar uma BU (estágios, canais, motivos de perda), derivado do processo da Vellus. |
| categoria de estágio | Classificação do estágio: `open` (aberta), `won` (ganha), `lost` (perdida). |
| RLS | Row-Level Security — isolamento por linha no Postgres por `tenant_id`. |
| RBAC | Role-Based Access Control — autorização baseada no papel do usuário por BU. |
| contexto de RBAC | Mapa de papéis por BU de um usuário, exportado ao authentication via cache. |
| objeto de valor | Tipo imutável definido por seus atributos, sem identidade própria (termo de DDD; nunca abreviado). |

## 9. Fora do escopo do MVP

- Hierarquia de BUs (BU pai/filha) ou agrupamento de BUs.
- Papéis customizados além dos quatro canônicos (TAdmin, GestorBU, Vendedor, Viewer).
- Permissões granulares por recurso individual configuráveis pelo tenant.
- Autoatendimento de convite (auto-cadastro sem convite de TAdmin).
- Login e gestão de identidade de parceiros (parceiros sem login no MVP — DEC-012).
- Consolidação de Tenancy & Branding com Organization Management (VAL-ORG-01 / DDD-VAL-04) — avaliar após estabilização da Fase 1.
- Histórico versionado de mudanças de papel exposto ao usuário final (auditoria existe via audit-log, mas sem interface dedicada).

## 10. Referências cruzadas

| Referência | Origem |
|-----------|--------|
| RF-03 (BUs, usuários e papéis) | docs/product/frd-nfrd/frd.md § 16 |
| FRD-org-01, FRD-org-02 | docs/product/frd-nfrd/frd.md |
| Matriz de Permissões Funcionais | docs/product/frd-nfrd/frd.md § 15 |
| RN-013 (desativação preserva histórico) | docs/product/frd-nfrd/frd.md § Regras de Negócio |
| MSG-015, MSG-016, MSG-017, MSG-018 | docs/product/frd-nfrd/frd.md § Mensagens |
| NFR-SEG-01, NFR-SEG-03, NFR-AUD, NFR-OBS-01 | docs/product/frd-nfrd/nfrd.md |
| DEC-001 (estágios por BU), DEC-006 (pool + RLS), DEC-007 (Lead como estágio) | docs/product/trd/trd.md |
| Tabelas business_units, users, user_memberships, user_invitations, stages, origin_channels, loss_reasons | docs/product/data-model/data-model.md § BC-08 |
| Linguagem ubíqua BC-08 (Organization Management) e BC-01 | docs/product/glossary/ubiquitous-language.md |
| README do módulo | docs/product/modules/organization/README.md |
| RISK-ORG-01, RISK-ORG-02 | docs/product/modules/organization/README.md § 19 |
| rule audit-immutability | `.forge/rules/domain/audit-immutability.md` |

