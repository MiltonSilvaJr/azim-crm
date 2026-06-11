---
title: Stack de observabilidade — GCP Cloud Logging, Monitoring e Trace
status: Aceito
date: 2026-06-11
deciders:
  - "@MiltonSilvaJr"
consulted:
  - DEC-005
  - PTV-08 / VAL-TRD-07
  - TRD §14
informed:
  - Times de plataforma e todos os bounded contexts
---

# ADR-0007 — Stack de observabilidade: GCP Cloud Logging, Monitoring e Trace

- **Status:** Aceito
- **Data:** 2026-06-11
- **Autores:** @MiltonSilvaJr
- **Supersede:** —
- **Substituído por:** —

## Contexto e Problema

O Azim CRM precisa de **observabilidade** (logs, métricas, tracing distribuído) para operar com
segurança. Havia uma **divergência de governança**: uma rule de engenharia citava o stack
**Prometheus/Loki/Jaeger**, enquanto a decisão de plataforma **DEC-005** e o **TRD §14** apontavam o
ecossistema **GCP** como alvo de deploy. A validação **PTV-08/VAL-TRD-07** sinalizou esse conflito,
que precisa ser resolvido de forma autoritativa para que logging/tracing sejam implementados de modo
consistente. Há ainda restrições transversais: **`tenant_id` deve ser dimensão de observabilidade** e
**logs não podem conter PII**.

O problema: **qual stack de observabilidade adotar, encerrando a divergência, sem perder os
princípios de correlação, estrutura e auditoria das rules?**

## Drivers da Decisão

- **Coerência com a plataforma:** o Azim roda em GCP (Cloud Run/Pub/Sub/Scheduler — DEC-005).
- **Custo operacional:** evitar operar um stack de observabilidade self-hosted em paralelo.
- **Multi-tenant observável:** `tenant_id` como dimensão para isolar e diagnosticar por tenant.
- **Privacidade:** sem PII em logs (LGPD).
- **Encerrar conflito de governança** entre rule e DEC-005/TRD.

## Decisão

Adotamos o **stack nativo do GCP** para observabilidade (alinhado a **DEC-005**):

- **Cloud Logging** — logs estruturados.
- **Cloud Monitoring** — métricas e alertas.
- **Cloud Trace** — tracing distribuído.

A rule que citava Prometheus/Loki/Jaeger é **corrigida** para refletir o stack GCP. Os **princípios**
das rules são **mantidos**: **`correlationId`** propagado entre serviços, **logs estruturados**,
**auditoria imutável**, e **`tenant_id` como label estruturado** em logs/métricas/traces. **PII não é
registrada em logs.**

## Opções Consideradas

### Opção A — Prometheus + Loki + Jaeger (self-hosted)
- **Bom:** stack open-source consolidado; portável entre nuvens.
- **Ruim:** **custo operacional** de operar e escalar três sistemas; **redundante** com o que o GCP
  já oferece gerenciado; diverge de DEC-005/TRD §14.
- **Veredito:** **Rejeitada** — sobrecarga operacional sem ganho frente ao stack gerenciado do GCP.

### Opção B (escolhida) — GCP Cloud Logging/Monitoring/Trace
- **Bom:** integrado à plataforma de deploy; gerenciado (sem operar infra de observabilidade);
  encerra o conflito de governança; preserva os princípios das rules.
- **Ruim:** acoplamento ao GCP — ver abaixo.
- **Veredito:** **Aceita.**

## Consequências

### Positivas
- Observabilidade **gerenciada**, sem operar stack próprio.
- **Conflito de governança encerrado** (rule alinhada a DEC-005/TRD §14).
- Diagnóstico por tenant via `tenant_id` estruturado; auditoria e correlação preservadas.

### Negativas e Mitigações
- **Acoplamento ao GCP** (lock-in de observabilidade). *Mitigação:* aceitável — o Azim já é GCP por
  DEC-005; instrumentação via OpenTelemetry mantém portabilidade da camada de coleta.
- **Risco de PII vazar em logs.** *Mitigação:* convenção e revisão de logging estruturado proíbem PII;
  redação/scrubbing no pipeline de logs; verificação em revisão.
- **Custo de ingestão/retenção** pode crescer com volume. *Mitigação:* níveis de log, sampling de
  trace e políticas de retenção.

## Conformidade

1. Logs em **Cloud Logging** estruturados, com `correlationId` e `tenant_id` como labels; **sem PII**.
2. Métricas/alertas em **Cloud Monitoring**; tracing distribuído em **Cloud Trace**.
3. Rule de observabilidade corrigida para citar o stack GCP (sem Prometheus/Loki/Jaeger).
4. Auditoria imutável preservada conforme princípios das rules.

## Referências

- **DEC-005** (decisão de plataforma GCP) — `docs/product/prd/prd.md`
- Validação — **PTV-08 / VAL-TRD-07** (conflito de governança)
- TRD — **§14 (observabilidade)** — `docs/product/trd/trd.md`
- Rule corrigida — `.forge/rules/` (observabilidade)
