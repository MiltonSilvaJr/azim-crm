---
title: Ownership do digest_action_token — tabela compartilhada com owner único de schema
status: Aceito
date: 2026-06-15
deciders:
  - "@MiltonSilvaJr"
consulted:
  - VAL-ACT-01 (approvals.yaml activity-management)
  - VAL-ACT-02 (TTL configurável por tenant — PR #18)
  - digest design §6.4/§7, DD-003, DD-007
  - activity-management requirements Req 7, RNF 5
  - ADR-0001 (isolamento multi-tenant)
informed:
  - Times de digest (BC-06) e activity-management (BC-04)
  - Plataforma/infra (provisionamento de banco compartilhado)
---

# ADR-0006 — Ownership do `digest_action_token`: tabela compartilhada com owner único de schema

- **Status:** Aceito
- **Data:** 2026-06-15
- **Autores:** @MiltonSilvaJr
- **Supersede:** —
- **Substituído por:** —

> Formaliza a decisão registrada em **VAL-ACT-01** (`services/activity-management/approvals.yaml`).
> Este ADR corrige a redação original do VAL-ACT-01 ("activity-management é owner; Digest consome"),
> que ficou **invertida** em relação à implementação consolidada após o PR #18 — quem **emite** e
> **owna o ciclo de vida** do token é o digest (BC-06); o activity-management (BC-04) é **consumidor**.

## Contexto e Problema

O token de ação de um clique (`digest_action_token`) é o mecanismo que permite a um usuário concluir
ou reagendar uma atividade direto pelo link do e-mail de digest, sem login interativo. O token em
claro transita **uma única vez** no link; o banco persiste apenas o `token_hash` (SHA-256, DD-007).

O ciclo de vida cruza **dois bounded contexts**:

- **BC-06 (digest)** — **emite** o token (`IActionTokenFactory.IssueAsync` + `DigestActionToken.Issue`),
  define `expires_at = created_at + TTL` (TTL configurável por tenant, default 48h — VAL-ACT-02,
  PR #18) e executa o **purge** dos tokens expirados.
- **BC-04 (activity-management)** — **consome** o token: valida por hash (`IDigestActionTokenPort.FindByHashAsync`)
  e grava `used_at` na transação que processa a ação (`MarkUsedAsync`, `WHERE used_at IS NULL` — uso único).

O problema concreto encontrado na implementação atual (Ondas 1-6): **ambos os módulos definem e migram
a tabela `digest_action_tokens` em bancos físicos separados** (`azim_digest` no digest;
`activity_management` no activity-management), cada um com seu próprio `CREATE TABLE`. Isso é
**incoerente**: um token emitido pelo digest (num banco) jamais seria encontrado pelo activity-management
(em outro banco). É necessário decidir **qual contexto é dono do schema** e **onde a tabela vive**, sem
co-ownership de migrations (RE-05 — risco de migrations concorrentes na mesma tabela).

## Drivers da Decisão

- **Fonte única de verdade:** emissão (`digest`) e consumo (`activity-management`) precisam operar sobre
  a **mesma linha** — não pode haver duas cópias do token em dois bancos.
- **Owner único de schema:** evitar que dois deployables apliquem DDL/migration na mesma tabela (RE-05).
- **Isolamento multi-tenant preservado:** RLS + Global Query Filter por `tenant_id` em ambos os acessos (ADR-0001).
- **Segurança do token:** persistir apenas o hash; uso único garantido por índice e por `WHERE used_at IS NULL` (DD-007, RNF 5/7).
- **Coerência com o código já consolidado:** o digest já concentra emissão, TTL e purge (PR #18).

## Decisão

`digest_action_tokens` é uma **única tabela física**, em um **banco de dados compartilhado** entre os
deployables BC-06 (digest) e BC-04 (activity-management). O **digest é o owner exclusivo do schema**;
o activity-management é **consumidor** que mapeia a mesma tabela apenas para consumo.

1. **Owner de schema = digest.** A DDL, as políticas de RLS, os índices e **todas as migrations** de
   `digest_action_tokens` vivem **apenas** em `Digest.Infrastructure`. O activity-management **não cria
   nem altera** a tabela — sua migration não deve conter `CREATE TABLE`/`ALTER` para `digest_action_tokens`.
2. **Banco compartilhado.** Em produção, as connection strings do digest e do activity-management
   apontam para o **mesmo banco**. A tabela existe uma única vez.
3. **Contrato cross-context na tabela** (sem FK física para `activities` — referência lógica, DD-001):
   - **digest:** `INSERT` na emissão (com `expires_at` derivado do TTL por tenant) e `DELETE`/purge dos expirados.
   - **activity-management:** `UPDATE` exclusivamente de `used_at` (`WHERE used_at IS NULL` — idempotente, uso único).
     Validação por `token_hash`; retorna indistinguível de inexistente para outro tenant (anti-enumeração, Req 7.6).
4. **Isolamento.** Ambos os acessos respeitam RLS (`tenant_id = current_setting('app.current_tenant')::uuid`)
   e o Global Query Filter de tenant (ADR-0001). O token em claro nunca é persistido (DD-007).

## Opções Consideradas

### Opção A — Co-ownership / dois bancos (estado atual do código)
Cada módulo mapeia e migra `digest_action_tokens` no seu próprio banco.
- **Bom:** nenhum acoplamento de banco entre os serviços.
- **Ruim:** **duas cópias do token** em bancos distintos — emissão e consumo não se encontram; o fluxo
  simplesmente não funciona. Duas migrations criando a mesma tabela (RE-05).
- **Veredito:** **Rejeitada** — incoerência funcional.

### Opção B — activity-management como owner do schema; digest grava via port
O token vive ao lado de `activities`; o digest passa a gravar via port/API ao emitir.
- **Bom:** consistência forte local no `used_at`; token perto da atividade referenciada.
- **Ruim:** inverte o fluxo já consolidado (emissão + TTL + purge estão no digest); a **geração do e-mail
  passa a depender do activity-management no ar**. Maior retrabalho.
- **Veredito:** **Rejeitada** — contraria o código consolidado (PR #18) e acopla o caminho de emissão.

### Opção C (escolhida) — Tabela compartilhada em 1 banco, digest como owner de schema
- **Bom:** fonte única de verdade; owner único de migration (digest); coerente com emissão/TTL/purge já no digest;
  RLS preserva isolamento em ambos os lados.
- **Ruim:** dois deployables compartilham um schema/banco (acoplamento de dados) — ver mitigações.
- **Veredito:** **Aceita.**

## Consequências

### Positivas
- **Fonte única de verdade:** emissão e consumo operam sobre a mesma linha; sem sincronização entre cópias.
- **Owner de migration inequívoco** (digest) — elimina o risco RE-05 de migrations concorrentes.
- **Isolamento multi-tenant intacto** (RLS + Query Filter dos dois lados, ADR-0001).

### Negativas e Mitigações
- **Acoplamento de dados entre dois deployables (banco compartilhado).** *Mitigação:* contrato de acesso
  estreito e explícito (digest escreve/expurga; activity só `UPDATE used_at`); nenhuma outra tabela é
  compartilhada; mudanças de schema são **aditivas** e coordenadas, sempre via migration do digest.
- **activity-management depende da migration do digest ter rodado.** *Mitigação:* ordem de deploy
  documentada (digest aplica schema antes); o activity-management nunca emite DDL para a tabela.
- **Risco de drift entre os dois mapeamentos EF da mesma tabela.** *Mitigação:* o mapeamento do
  activity-management é somente de consumo (sem ownership de colunas além de `used_at`); testes de
  schema (`DbContextSchemaTests`) validam a forma esperada.

## Conformidade

1. **DDL/migrations de `digest_action_tokens` existem apenas em `Digest.Infrastructure`.** O
   activity-management **não** deve recriar/alterar a tabela (o bloco `CREATE TABLE IF NOT EXISTS
   digest_action_tokens` da migration `20260614000001_Initial` deve ser removido/tornado inerte —
   ver "Alinhamento de código pendente").
2. **Connection strings do digest e do activity-management apontam para o mesmo banco** em produção
   (configuração de infra — alinhar com VAL-TRD-05 / Cloud SQL em `southamerica-east1`).
3. **digest:** `INSERT` na emissão com `expires_at` = `created_at` + TTL por tenant (default 48h, VAL-ACT-02);
   purge dos expirados. **activity-management:** `UPDATE used_at` apenas, `WHERE used_at IS NULL`.
4. **RLS por tenant** ativa nas duas rotas de acesso; persistência apenas do `token_hash` (DD-007).

### Alinhamento de código pendente (não executado neste ADR)
A implementação atual ainda diverge da decisão e precisa convergir em tarefa dedicada:
- Unificar para **um banco compartilhado** (hoje `azim_digest` ≠ `activity_management`).
- **Remover** a criação/owned-DDL de `digest_action_tokens` do activity-management (passa a só consumir).
- Garantir que o mapeamento EF do activity-management seja de consumo (sem migrations da tabela).

## Referências

- VAL-ACT-01 / VAL-ACT-02 — `services/activity-management/approvals.yaml`, `services/activity-management/approvals.yaml`
- digest — `design.md §6.4/§7`, `DD-003`, `DD-007`, `requirements.md Req 7` — `docs/product/modules/digest/`
- activity-management — `Req 7`, `RNF 5`, `IDigestActionTokenPort`, `DigestActionTokenAdapter`
- ADR-0001 — Isolamento multi-tenant em defesa em profundidade
- ADR-0010 — Scheduling do digest (consumidor; renumerado a partir do antigo ADR-0006)
