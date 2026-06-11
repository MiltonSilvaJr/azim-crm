---
title: Numeração sequencial atômica de oportunidade por tenant (AZ-NNNN)
status: Aceito
date: 2026-06-11
deciders:
  - "@MiltonSilvaJr"
consulted:
  - opportunity-pipeline (DD-001, RN-001)
  - Modelo de dados
informed:
  - Times dos bounded contexts de oportunidade
---

# ADR-0003 — Numeração sequencial atômica de oportunidade por tenant (AZ-NNNN)

- **Status:** Aceito
- **Data:** 2026-06-11
- **Autores:** @MiltonSilvaJr
- **Supersede:** —
- **Substituído por:** —

## Contexto e Problema

Cada oportunidade precisa de um **identificador de negócio legível** (formato `AZ-NNNN`), usado por
vendedores e gestores em conversas, e-mails e relatórios. Esse número deve ser **sequencial,
imutável e único dentro do tenant** — e, por ser multi-tenant (ADR-0001), **não pode vazar a
contagem de um tenant para outro** (saber que existem 4.000 oportunidades de um concorrente é
informação comercial sensível). Sob criação concorrente de oportunidades, dois requests simultâneos
não podem receber o mesmo número.

O problema: **como gerar um número sequencial por tenant, sem colisão sob concorrência e sem vazar
contagem entre tenants?**

## Drivers da Decisão

- **Legibilidade de negócio:** identificador estável e amigável (`AZ-NNNN`).
- **Isolamento multi-tenant:** a sequência é privada do tenant; não pode revelar volume alheio.
- **Correção sob concorrência:** geração atômica, sem números duplicados.
- **Imutabilidade:** o número, uma vez atribuído, não muda.

## Decisão

O número da oportunidade é **gerado pelo sistema, imutável**, e **único por tenant** via constraint
`UNIQUE(tenant_id, opportunity_number)`. A geração é **atômica por contador transacional com lock de
linha por tenant**: a transação que cria a oportunidade obtém um lock na linha de contador do tenant
(`SELECT ... FOR UPDATE` ou equivalente), incrementa e usa o valor — serializando apenas requests do
**mesmo** tenant e evitando colisão concorrente. O formato de exibição `AZ-NNNN` é derivado do número.

## Opções Consideradas

### Opção A — Sequence global do PostgreSQL
Uma `SEQUENCE` única compartilhada por todos os tenants.
- **Bom:** atômica e simples; sem lock manual.
- **Ruim:** **vaza contagem entre tenants** — números não consecutivos revelam atividade de outros
  tenants; e a numeração por tenant não seria 1..N. Quebra requisito de isolamento.
- **Veredito:** **Rejeitada** — viola confidencialidade entre tenants.

### Opção B — `UNIQUE` global em opportunity_number (sem tenant_id)
- **Bom:** garante unicidade absoluta.
- **Ruim:** **quebra multi-tenancy** — impede que dois tenants tenham `AZ-0001`; acopla os espaços
  de numeração e reintroduz vazamento de contagem.
- **Veredito:** **Rejeitada** — incompatível com ADR-0001.

### Opção C (escolhida) — Contador por tenant com lock de linha + `UNIQUE(tenant_id, opportunity_number)`
- **Bom:** sequência privada por tenant (1..N); atômica sob concorrência; isolamento preservado;
  imutável; unicidade garantida pelo banco.
- **Ruim:** lock por tenant serializa criações concorrentes do mesmo tenant — ver abaixo.
- **Veredito:** **Aceita.**

## Consequências

### Positivas
- Numeração **previsível e privada** por tenant; sem vazamento de contagem.
- Unicidade garantida pelo banco; correção sob concorrência.
- Número imutável, seguro como referência de negócio.

### Negativas e Mitigações
- **Serialização de criações concorrentes do mesmo tenant** pelo lock de linha. *Mitigação:* o lock
  é por tenant e mantido por janela mínima (apenas incremento); volume de criação simultânea por
  tenant é baixo. Aceitável frente à garantia de não-colisão.
- **Possíveis "buracos" na sequência** se transações abortarem após consumir o número. *Mitigação:*
  o requisito é unicidade e imutabilidade, não contiguidade estrita; documentar que gaps são aceitáveis.

## Conformidade

1. `opportunity_number` é gerado pelo sistema e **não editável** após criação.
2. Constraint `UNIQUE(tenant_id, opportunity_number)` presente.
3. Geração usa lock de linha por tenant em transação (teste de concorrência sem colisão).
4. Nenhuma sequence/UNIQUE global de numeração entre tenants.

## Referências

- Design — opportunity-pipeline **DD-001**, **RN-001** — `docs/product/modules/opportunity-pipeline/design.md`
- Modelo de dados — `docs/product/data-model/data-model.md`
- ADR-0001 — Isolamento multi-tenant
