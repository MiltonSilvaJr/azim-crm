---
title: Scheduling do digest por job horário UTC com hora local IANA por tenant
status: Aceito
date: 2026-06-11
deciders:
  - "@MiltonSilvaJr"
consulted:
  - digest (DD, RN-009)
  - TRD §9
informed:
  - Times de notificação e infraestrutura
---

# ADR-0006 — Scheduling do digest por job horário UTC com hora local IANA por tenant

- **Status:** Aceito
- **Data:** 2026-06-11
- **Autores:** @MiltonSilvaJr
- **Supersede:** —
- **Substituído por:** —

## Contexto e Problema

O Azim envia um **digest** periódico por e-mail. Cada tenant configura a **hora local** em que quer
receber (`horario_digest`), em fuso próprio. Disparar na hora local correta para muitos tenants em
fusos diferentes — e lidar com **horário de verão (DST)**, que muda o offset UTC duas vezes por ano
— é o desafio. Criar **um job de scheduler por tenant** não escala (proliferação de jobs, manutenção,
custo) e fica frágil sob DST.

O problema: **como disparar o digest na hora local certa de cada tenant, neutralizando DST, sem criar
um job por tenant, e sem enviar duplicado?**

## Drivers da Decisão

- **Correção de fuso/DST:** entrega na hora local certa o ano inteiro, sem ajuste manual em DST.
- **Escalabilidade:** número de jobs não cresce com o número de tenants.
- **Idempotência:** o digest de um dia não pode ser enviado duas vezes.
- **Segurança:** o disparo do scheduler ao endpoint deve ser autenticado.

## Decisão

Adotamos um **job horário em UTC** que avalia fusos por tenant:

1. **Cloud Scheduler** dispara, **de hora em hora em UTC**, um **endpoint autenticado via OIDC**
   (token de service account validado pelo worker).
2. O **worker** seleciona os tenants/usuários cuja **hora local (fuso IANA)** corresponde, naquele
   instante, ao `horario_digest` configurado **e** cujo dia é **dia útil**. Usar fusos **IANA**
   (ex.: `America/Sao_Paulo`) faz o cálculo respeitar DST automaticamente — **um único job horário**
   cobre todos os fusos.
3. **Idempotência** garantida por **`EmailDigestLog`**: antes de enviar, o worker verifica/registra
   que o digest daquele tenant/usuário/dia já foi enviado, evitando duplicidade.

## Opções Consideradas

### Opção A — Um job de Cloud Scheduler por tenant na hora local
- **Bom:** disparo direto na hora local.
- **Ruim:** **não escala** (1 job por tenant); manutenção e custo crescentes; **frágil sob DST**
  (offset muda, exigindo reconfiguração).
- **Veredito:** **Rejeitada** — proliferação de jobs e fragilidade com DST.

### Opção B — Job horário UTC com cálculo por offset fixo
Job horário, mas calculando hora local por offset numérico armazenado.
- **Bom:** um único job.
- **Ruim:** **offset fixo não trata DST** — erra em duas janelas do ano; manutenção manual.
- **Veredito:** **Rejeitada** — não neutraliza DST.

### Opção C (escolhida) — Job horário UTC + hora local IANA por tenant + EmailDigestLog
- **Bom:** um job só; **DST neutralizado** por fusos IANA; escala com tenants; seguro (OIDC);
  idempotente (`EmailDigestLog`).
- **Ruim:** worker avalia todos os tenants a cada hora — ver abaixo.
- **Veredito:** **Aceita.**

## Consequências

### Positivas
- **Um único job horário** cobre todos os fusos; **DST automático** via IANA.
- Escala com o número de tenants sem alterar o scheduler.
- Disparo **autenticado (OIDC)**; envio **idempotente** por `EmailDigestLog`.

### Negativas e Mitigações
- **Avaliação horária de todos os tenants**, mesmo quando poucos têm digest naquela hora.
  *Mitigação:* consulta indexada por hora local/fuso; custo por avaliação baixo.
- **Granularidade horária** (não envia em minutos arbitrários). *Mitigação:* `horario_digest` é
  definido em hora cheia; suficiente para digest.
- **Dependência de base IANA atualizada.** *Mitigação:* base de fusos mantida na plataforma/runtime.

## Conformidade

1. Cloud Scheduler dispara endpoint de hora em hora em UTC, autenticado por **OIDC**.
2. Seleção usa **fuso IANA** por tenant para comparar com `horario_digest` e checa **dia útil**.
3. Envio do digest é registrado em `EmailDigestLog`; reentrega no mesmo dia é descartada (teste).

## Referências

- Design — digest **DD**, **RN-009** — `docs/product/modules/digest/design.md`
- TRD — **§9** — `docs/product/trd/trd.md`
- ADR-0005 — Provedor de e-mail (Resend); ADR-0004 — Idempotência
