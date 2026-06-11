---
title: Outbox transacional e idempotência para eventos de domínio
status: Aceito
date: 2026-06-11
deciders:
  - "@MiltonSilvaJr"
consulted:
  - TRD §9
  - NFR-RES (resiliência)
informed:
  - Times de todos os bounded contexts produtores/consumidores de eventos
---

# ADR-0004 — Outbox transacional e idempotência para eventos de domínio

- **Status:** Aceito
- **Data:** 2026-06-11
- **Autores:** @MiltonSilvaJr
- **Supersede:** —
- **Substituído por:** —

## Contexto e Problema

Mudanças de estado de domínio (ex.: oportunidade movida para Ganho) precisam **publicar eventos**
que outros contextos consomem (notificação, comissão, relatórios). O risco clássico do **dual-write**:
se a mudança de estado é persistida no banco e o evento é publicado no broker em operações separadas,
uma falha entre elas produz **inconsistência** — estado mudou mas evento se perdeu, ou evento foi
publicado e o estado não persistiu. Em um sistema financeiro/CRM isso causa comissões não
processadas, notificações fantasma ou divergência entre contextos.

O problema: **como garantir que um evento de domínio seja publicado se e somente se a mudança de
estado foi persistida, de forma atômica, e que consumidores não processem duplicatas?**

## Drivers da Decisão

- **Atomicidade estado↔evento:** sem dual-write; evento e mudança vivem na mesma transação.
- **Entrega confiável (at-least-once):** evento não pode se perder em falha de rede/broker.
- **Idempotência:** entrega at-least-once implica duplicatas; consumidor não pode processar 2x.
- **Evolução de contrato:** eventos mudam; consumidores precisam de versionamento estável.

## Decisão

Adotamos o padrão **Transactional Outbox** com **consumidores idempotentes**:

1. **Outbox transacional** — o evento de domínio é gravado em uma tabela `outbox` na **mesma
   transação** que persiste a mudança de estado. Commit atômico: ou ambos, ou nenhum.
2. **Relay → Pub/Sub** — um processo relay lê o outbox e publica no **Google Pub/Sub** (at-least-once),
   marcando o evento como publicado. Falhas são retentadas sem perder o evento.
3. **Consumidores idempotentes** — cada consumidor usa **chave de idempotência (padrão Inbox)** para
   detectar e descartar reprocessamento de um evento já tratado.
4. **Versionamento** — eventos são versionados com sufixo `.v1` (ex.: `opportunity.won.v1`),
   permitindo evolução sem quebrar consumidores existentes.

## Opções Consideradas

### Opção A — Publicar direto no Pub/Sub durante o request
Publicar o evento no broker dentro do handler, junto da escrita no banco.
- **Bom:** menor latência percebida; sem tabela/relay extra.
- **Ruim:** **dual-write** — perde atomicidade; falha entre persistir e publicar gera inconsistência
  silenciosa.
- **Veredito:** **Rejeitada** — inconsistência inaceitável em domínio financeiro.

### Opção B — Two-Phase Commit (2PC) entre banco e broker
Coordenar banco e Pub/Sub em transação distribuída.
- **Bom:** atomicidade teórica entre recursos.
- **Ruim:** **complexidade alta**, suporte fraco/ausente no Pub/Sub, acoplamento e fragilidade
  operacional; coordenador vira ponto de falha.
- **Veredito:** **Rejeitada** — complexidade desproporcional.

### Opção C (escolhida) — Outbox transacional + relay + idempotência
- **Bom:** atomicidade estado↔evento via transação local; entrega confiável; consumidores seguros
  contra duplicata; contratos versionados.
- **Ruim:** componente relay e latência de publicação (eventual) — ver abaixo.
- **Veredito:** **Aceita.**

## Consequências

### Positivas
- **Sem dual-write:** evento publicado sse a mudança persistiu.
- **Resiliência:** eventos sobrevivem a falhas de broker/rede (NFR-RES).
- **Consumidores seguros** contra reprocessamento; contratos evoluíveis via `.vN`.

### Negativas e Mitigações
- **Latência eventual** entre commit e publicação (relay assíncrono). *Mitigação:* relay de baixa
  latência; consumidores projetados para consistência eventual.
- **Componente relay a operar/monitorar.** *Mitigação:* métricas de lag e backlog do outbox;
  alertas; reuso de infraestrutura compartilhada entre contextos.
- **Idempotência exige store de chaves no consumidor.** *Mitigação:* tabela Inbox padronizada;
  chave de idempotência derivada do id do evento.

## Conformidade

1. Todo evento de domínio é gravado no outbox na **mesma transação** da mudança de estado.
2. Relay publica no Pub/Sub e marca como publicado (at-least-once, com retentativa).
3. Consumidores descartam duplicatas via chave de idempotência/Inbox (teste de reentrega).
4. Eventos nomeados com versão (`.v1`, …).

## Referências

- TRD — **§9 (eventos/integração)** — `docs/product/trd/trd.md`
- TRD — **NFR-RES (resiliência)** — `docs/product/trd/trd.md`
- ADR-0002 — Snapshot de comissão (consumidor de `opportunity.won`)
