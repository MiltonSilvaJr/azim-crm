---
title: Isolamento multi-tenant em defesa em profundidade (pooled multi-tenancy)
status: Aceito
date: 2026-06-11
deciders:
  - "@MiltonSilvaJr"
consulted:
  - Pipeline de specs (PRD/TRD/Data Model)
  - Research adversarial DEC-006
informed:
  - Times de todos os bounded contexts do Azim CRM
---

# ADR-0001 — Isolamento multi-tenant em defesa em profundidade (pooled multi-tenancy)

- **Status:** Aceito
- **Data:** 2026-06-11
- **Autores:** @MiltonSilvaJr
- **Supersede:** —
- **Substituído por:** —

## Contexto e Problema

O Azim CRM é um produto **multi-tenant**: um único ambiente atende múltiplas organizações
(tenants), e os dados de negócio de cada tenant — oportunidades, contas, parceiros, atividades,
metas — são **comercialmente confidenciais**. Um vazamento entre tenants (tenant A enxergando
dados do tenant B) configura violação de confidencialidade comercial, potencial incidente LGPD
e quebra de confiança irreparável. O PRD classifica esse evento como **incidente sev-1**.

O problema concreto a resolver: **como garantir, de forma estrutural e consistente em todos os
bounded contexts, que nenhuma consulta de aplicação consiga ler ou escrever dados fora do tenant
corrente — mesmo na presença de bugs de aplicação, queries ad-hoc ou esquecimento de filtro?**

Durante a **Fase 7 do pipeline de specs** surgiu um conflito de governança entre dois artefatos:

- A rule `.forge/rules/conventions/database-naming.md` então prescrevia "isolamento via coluna,
  não RLS" — ou seja, confiar exclusivamente no `tenant_id` filtrado pela aplicação.
- O **DEC-006** (decisão arquitetural do PRD), o **TRD §12 (segurança)** e o **data-model.md**
  descreviam RLS no PostgreSQL como camada adicional de **defesa em profundidade**.

Esse conflito precisava ser resolvido de forma autoritativa antes de o schema físico e os testes
de isolamento serem implementados, porque a estratégia de isolamento é **difícil de mudar após o
deploy em produção** (migração de dados de alto custo e alto risco — PRD §restrições).

Este ADR formaliza a decisão tomada pelo Milton em 2026-06-11, encerrando o conflito. A rule
`database-naming.md` já foi corrigida para refletir esta decisão.

## Drivers da Decisão

- **Confidencialidade comercial:** dados de tenants concorrentes coexistem no mesmo banco; o
  isolamento é requisito de segurança, não conveniência.
- **Defesa em profundidade:** uma única camada de proteção (filtro de aplicação) é um ponto único
  de falha; um bug nela vaza dados sem rede de segurança.
- **Conformidade e auditoria:** LGPD e a postura de segurança do produto exigem controle
  demonstrável de acesso a dados pessoais por tenant.
- **Consistência entre bounded contexts:** todos os contextos manipulam dados multi-tenant; a
  regra precisa ser uniforme para não criar lacunas exploráveis.
- **Custo operacional adequado ao estágio do produto:** a solução não pode impor sobrecarga
  operacional de provisionamento por tenant que o estágio atual do Azim não justifica.
- **Verificabilidade em CI:** o isolamento precisa ser testável automaticamente como gate de
  merge (KPI-06).

## Decisão

Para **todas as tabelas multi-tenant de domínio** do Azim CRM, adotamos **isolamento em defesa em
profundidade sobre pooled multi-tenancy**, com três camadas obrigatórias e cumulativas:

1. **`tenant_id` obrigatório (eixo estrutural).**
   Toda tabela de dados de negócio tem coluna `tenant_id UUID NOT NULL`. É o eixo de
   particionamento lógico de todos os dados de tenant.

2. **EF Core Global Query Filter por `tenant_id` (proteção de aplicação).**
   Obrigatório. O `DbContext` aplica filtro global por `tenant_id` em todas as entidades
   multi-tenant, de modo que consultas da aplicação sejam escopadas ao tenant corrente por padrão.

3. **Row-Level Security (RLS) no PostgreSQL (proteção de banco).**
   Obrigatória em todas as tabelas multi-tenant de domínio. As políticas comparam
   `tenant_id = current_setting('app.current_tenant')::uuid`. O fluxo de runtime:
   - O **middleware** resolve o tenant da requisição (`slug → id`, com cache em Redis).
   - O **interceptor de conexão** executa `SET app.current_tenant = @tenant_id` ao abrir/alugar a
     conexão, antes de qualquer comando de negócio.
   - As políticas RLS aplicam o escopo no nível do banco, independentemente do que a aplicação
     envie — inclusive em queries ad-hoc fora do EF.

Regras de governança que acompanham a decisão:

- **RLS não é opcional.** Só pode ser **dispensada por exceção formal documentada** (um novo
  ADR/DD explícito que justifique a exceção para uma tabela específica) — **nunca por omissão**.
- **Pooled multi-tenancy:** banco/schema compartilhado, isolamento por `tenant_id`.
  **Não** schema-por-tenant e **não** banco-por-tenant.
- **Um único usuário de banco para a aplicação** (não um usuário de banco por tenant); o
  isolamento é garantido pela variável de sessão `app.current_tenant` + políticas RLS, não por
  credenciais distintas.
- **Verificação obrigatória:** testes de isolamento de tenant em CI são gate de merge (KPI-06);
  qualquer vazamento entre tenants é tratado como **incidente sev-1**.

## Opções Consideradas

### Opção A — Isolamento apenas por coluna + EF Global Query Filter

Confiar exclusivamente no `tenant_id` filtrado pela aplicação (EF Core), sem RLS no banco.

- **Bom:** menor complexidade de infraestrutura; sem necessidade de gerenciar variável de sessão
  nem políticas RLS; performance previsível.
- **Bom:** modelo mental simples para desenvolvedores.
- **Ruim:** **ponto único de falha** — um bug no global query filter, uma entidade nova sem
  filtro configurado, ou uma query ad-hoc fora do EF vaza dados sem nenhuma rede de segurança.
- **Ruim:** não há proteção no nível do banco; qualquer acesso direto ao banco (scripts,
  ferramentas de BI, migrations mal escritas) ignora o isolamento.
- **Ruim:** postura de segurança/auditoria mais fraca para um produto com dados comercialmente
  confidenciais de tenants concorrentes.
- **Veredito:** **Rejeitada** — sem proteção de banco, o risco de bypass é incompatível com a
  classificação sev-1 do evento de vazamento.

### Opção B — RLS opcional por módulo

Permitir que cada bounded context decida se ativa RLS, mantendo EF filter como base comum.

- **Bom:** flexibilidade por contexto; módulos com dados menos sensíveis poderiam economizar a
  sobrecarga de RLS.
- **Ruim:** **inconsistência estrutural** — gera um mapa irregular de proteção, difícil de
  auditar e raciocinar sobre garantias globais.
- **Ruim:** o "opcional" tende a virar "ausente por omissão" sob pressão de prazo, recriando o
  ponto único de falha da Opção A nos módulos sem RLS — **bypass sem rede de segurança**.
- **Ruim:** dificulta o teste de isolamento uniforme em CI (cada módulo com regra diferente).
- **Veredito:** **Rejeitada** — inconsistência e risco de bypass por omissão superam o ganho
  marginal de flexibilidade.

### Opção C — Schema por tenant ou banco por tenant (isolamento físico)

Provisionar um schema (ou banco) dedicado por tenant, com isolamento físico forte.

- **Bom:** isolamento físico mais forte; "blast radius" de um vazamento reduzido por construção;
  facilita extração/exclusão de dados de um tenant.
- **Ruim:** **custo operacional alto** — provisionamento, migrations e versionamento de schema
  multiplicados por número de tenants; complexidade de conexões e de pool.
- **Ruim:** escalabilidade de onboarding de tenants prejudicada; operação de migrations vira
  processo de fan-out arriscado.
- **Ruim:** custo não se justifica para o **estágio atual** do produto e o volume de tenants
  previsto.
- **Veredito:** **Rejeitada** — custo operacional desproporcional ao estágio do produto. Pode ser
  reavaliada por um ADR futuro se a escala/regulação exigir isolamento físico.

### Opção D (escolhida) — `tenant_id` + EF Global Query Filter + RLS (defesa em profundidade), pooled

Camadas cumulativas de aplicação e banco sobre banco/schema compartilhado.

- **Bom:** duas camadas independentes; um bug em uma é contido pela outra.
- **Bom:** proteção no nível do banco cobre acessos fora do EF.
- **Bom:** uniforme em todos os contextos; testável em CI.
- **Bom:** custo operacional compatível com pooled multi-tenancy (um banco, um usuário).
- **Ruim:** complexidade adicional de runtime (variável de sessão, interceptor, políticas) —
  ver Consequências Negativas.
- **Veredito:** **Aceita** — melhor equilíbrio entre segurança, consistência e custo para o
  estágio atual.

## Consequências

### Positivas

- `tenant_id` permanece como **eixo estrutural** de todos os dados de negócio.
- Soma **proteção de aplicação (EF)** e **proteção de banco (RLS)** — defesa em profundidade real.
- **Reduz risco de bypass:** acessos fora do EF (queries ad-hoc, ferramentas, migrations) ainda
  são escopados pelas políticas RLS.
- **Padroniza todos os bounded contexts** com a mesma garantia, simplificando auditoria.
- **Melhora postura de segurança e auditoria** (alinhamento com LGPD e confidencialidade comercial).
- Habilita **gate de CI verificável** (KPI-06): vazamento entre tenants = incidente sev-1 com
  teste de isolamento obrigatório.

### Negativas e Mitigações

- **Complexidade de runtime adicional** (variável de sessão `app.current_tenant`, interceptor de
  conexão, políticas RLS por tabela).
  *Mitigação:* encapsular a configuração de RLS e o `SET app.current_tenant` em infraestrutura
  compartilhada (interceptor + base migration/convention), de modo que novos contextos herdem o
  comportamento sem reimplementar.

- **Risco de "tenant não setado":** se o interceptor não executar `SET app.current_tenant` antes
  de uma query, as políticas RLS podem bloquear todo acesso (falha fechada) ou — se mal
  configuradas — abrir acesso indevido.
  *Mitigação:* políticas RLS desenhadas para **falhar fechado** (sem `app.current_tenant` válido,
  nega acesso); teste de isolamento em CI cobrindo o caminho "sem tenant setado".

- **Custo de operações administrativas/cross-tenant legítimas** (jobs de plataforma, migrations
  de dados, suporte) que precisam atravessar tenants.
  *Mitigação:* essas operações exigem caminho privilegiado explícito e auditado; qualquer
  dispensa de RLS para uma tabela exige **exceção formal documentada** (novo ADR/DD), nunca
  omissão.

- **Overhead de performance** da avaliação de políticas RLS e do `SET` por conexão.
  *Mitigação:* índices em `tenant_id` (ver `database-naming.md`); cache Redis na resolução
  `slug → id`; reaproveitamento de conexões com o `SET` aplicado pelo interceptor.

- **Acoplamento ao PostgreSQL:** RLS é recurso do PostgreSQL; migrar para outro SGBD exigiria
  reprojetar a camada de banco.
  *Mitigação:* aceitável — o PostgreSQL é a escolha de banco do Azim (TRD); a camada EF
  permanece portável e a dependência fica isolada nas migrations/políticas.

## Conformidade

Critérios verificáveis para considerar uma tabela/contexto conforme a este ADR:

1. Toda tabela de dados de negócio possui `tenant_id UUID NOT NULL` (lint de schema/migration).
2. Toda entidade multi-tenant tem **EF Core Global Query Filter** por `tenant_id` configurado
   (revisão de `DbContext`/convenção).
3. Toda tabela multi-tenant de domínio tem **política RLS habilitada** comparando
   `tenant_id = current_setting('app.current_tenant')::uuid`, com comportamento **falha-fechada**.
4. O interceptor de conexão executa `SET app.current_tenant` antes de comandos de negócio.
5. **Testes de isolamento de tenant em CI** passam a 100% (KPI-06) — gate de merge; incluem o
   caminho "sem tenant setado" e tentativa de leitura cross-tenant.
6. Qualquer tabela sem RLS está coberta por **exceção formal documentada** (ADR/DD explícito).

## Referências

- PRD — **DEC-006** (decisão arquitetural de multi-tenancy; research adversarial: 3 alegações,
  0 refutadas) — `docs/product/prd/prd.md`
- PRD — **KPI-06** (100% de aprovação dos testes de isolamento em CI) — `docs/product/prd/prd.md`
- TRD — **§12 (segurança)** — `docs/product/trd/trd.md`
- Modelo de dados — `docs/product/data-model/data-model.md`
- Design — **Organization (DD-001)** — `docs/product/modules/organization/design.md`
- Design — **Account Management (DD-002)** — `docs/product/modules/account-management/design.md`
- Rule corrigida — `.forge/rules/conventions/database-naming.md` (§ Multi-tenancy)

### ADRs futuros planejados (quando aplicável)

- Eventual reavaliação de isolamento físico (schema/banco por tenant) caso escala ou regulação
  passem a justificar — exigiria novo ADR superseando a Opção C aqui rejeitada.
- Qualquer **dispensa de RLS** para tabela específica deve ser registrada como ADR/DD próprio.
