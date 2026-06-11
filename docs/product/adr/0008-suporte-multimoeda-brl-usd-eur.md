---
title: Suporte a multimoeda (BRL, USD, EUR) com Money = amount_cents + currency
status: Aceito
date: 2026-06-11
deciders:
  - "@MiltonSilvaJr"
consulted:
  - Modelo de dados (Money)
  - opportunity-pipeline, goal-forecast, reporting
informed:
  - Times de oportunidade, metas, financeiro e relatórios
---

# ADR-0008 — Suporte a multimoeda (BRL, USD, EUR) com Money = amount_cents + currency

- **Status:** Aceito
- **Data:** 2026-06-11
- **Autores:** @MiltonSilvaJr
- **Supersede:** —
- **Substituído por:** —

## Contexto e Problema

O Azim CRM atende contratos em **mais de uma moeda** (negócios em USD/EUR além de BRL). O valor
monetário é central no domínio: oportunidades, comissões de parceiro e metas. A representação de
dinheiro precisa ser **exata** (sem erro de ponto flutuante) e **carregar a moeda**, pois um valor
sem moeda é ambíguo. Esta decisão precisa ser tomada **antes do schema-freeze**, porque adicionar
moeda às tabelas monetárias depois exige migração de dados de alto custo.

O problema: **como representar valores monetários em múltiplas moedas, de forma exata e
inequívoca, definindo o escopo de moedas e de conversão para o MVP?**

## Drivers da Decisão

- **Exatidão monetária:** dinheiro em inteiros (cents), nunca float.
- **Não-ambiguidade:** todo valor carrega sua moeda (ISO-4217).
- **Atender contratos USD/EUR**, não só BRL.
- **Escopo de MVP controlado:** evitar complexidade/risco de FX (taxa de câmbio) agora.
- **Decisão antes do schema-freeze** (custo de migração posterior).

## Decisão

Adotamos um **objeto de valor `Money`** composto por:

- **`amount_cents` (BIGINT)** — valor inteiro em centavos (sem ponto flutuante).
- **`currency` (CHAR(3), ISO-4217)** — código da moeda.

O campo `currency` existe em **`opportunities`**, **`opportunity_partner_commissions`** (que
**herda** a moeda da oportunidade) e **`goals`**. Moedas **pré-cadastradas**: **Real (BRL)**,
**Dólar americano (USD)** e **Euro (EUR)**.

Regras de escopo:

- **Cada oportunidade/meta tem moeda única** (não há valores mistos numa mesma entidade).
- **Conversão entre moedas está fora do MVP** — não há câmbio automático.
- **Relatórios consolidam por moeda** (agregam separadamente por `currency`), sem converter.

## Opções Consideradas

### Opção A — Moeda única (BRL)
- **Bom:** modelo mais simples; sem campo de moeda; relatórios triviais.
- **Ruim:** **não atende contratos em USD/EUR** — bloqueia negócios reais; mudar depois exige
  migração pós schema-freeze.
- **Veredito:** **Rejeitada** — não cobre requisito de negócio.

### Opção B — Multimoeda com conversão automática via FX
Armazenar em várias moedas e converter usando taxas de câmbio.
- **Bom:** relatórios consolidados em moeda única; comparação direta.
- **Ruim:** **escopo e risco** — fonte de taxas, data/hora da taxa, ganhos/perdas cambiais,
  reprocessamento histórico; complexidade alta para o MVP.
- **Veredito:** **Rejeitada agora** — fora do MVP; pode ser ADR futuro.

### Opção C (escolhida) — Money (amount_cents + currency), moeda única por entidade, sem conversão no MVP
- **Bom:** exato e inequívoco; atende USD/EUR/BRL; escopo controlado (sem FX); preparado no schema
  antes do freeze.
- **Ruim:** relatórios não consolidam entre moedas — ver abaixo.
- **Veredito:** **Aceita.**

## Consequências

### Positivas
- Valores **exatos** (inteiros) e **inequívocos** (com moeda).
- Atende contratos em **BRL, USD e EUR**.
- **Schema pronto** para multimoeda antes do freeze (sem migração futura para introduzir `currency`).
- Escopo do MVP **enxuto** (sem risco de FX).

### Negativas e Mitigações
- **Relatórios não consolidam entre moedas** (agregam por moeda separadamente). *Mitigação:*
  consolidação por moeda é suficiente para o MVP; conversão fica para ADR futuro com FX.
- **Operações entre entidades de moedas diferentes não são somáveis** diretamente. *Mitigação:*
  comissão **herda** a moeda da oportunidade, evitando mistura; validação impede combinar moedas.
- **Conjunto de moedas fixo (BRL/USD/EUR).** *Mitigação:* moedas pré-cadastradas em tabela de
  referência; ampliar exige cadastro controlado, não mudança de schema.

## Conformidade

1. Toda coluna monetária usa `amount_cents BIGINT` + `currency CHAR(3)` (ISO-4217). Sem float.
2. `currency` presente em `opportunities`, `opportunity_partner_commissions` e `goals`.
3. Comissão herda a moeda da oportunidade (teste de consistência).
4. Moedas restritas a BRL/USD/EUR pré-cadastradas; sem conversão automática no MVP.
5. Relatórios agregam por `currency` sem conversão cambial.

## Referências

- Modelo de dados — objeto de valor **Money** — `docs/product/data-model/data-model.md`
- Design — opportunity-pipeline, goal-forecast, reporting — `docs/product/modules/`
- ADR-0002 — Snapshot de comissão (herda moeda da oportunidade)
