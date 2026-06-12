# Architectural Decision Records (ADR) — Azim CRM

Este diretório guarda os **Architectural Decision Records** do Azim CRM no formato
[MADR](https://adr.github.io/madr/). Cada ADR registra uma decisão arquiteturalmente
significativa: o contexto, as forças em jogo, as opções reais consideradas e a justificativa
da escolha — inclusive o porquê das alternativas rejeitadas.

## Convenções

- **Idioma:** Português Brasileiro no texto; identificadores (tabelas, colunas, código) em inglês.
- **Arquivo:** `NNNN-titulo-em-kebab-case.md` (numeração sequencial a partir de `0001`).
- **Status válidos:** `Proposto`, `Em Revisão`, `Aceito`, `Depreciado`, `Substituído por ADR-XXXX`.
- **Imutabilidade:** ADR aceito não é reescrito. Mudança de rumo gera um **novo** ADR que
  supersede o anterior (atualizando o status do antigo para `Substituído por ADR-XXXX`).

## Próximo número livre

`0009`

## Tabela mestra

| ADR | Título | Status | Data | Autores |
|-----|--------|--------|------|---------|
| [0001](./0001-isolamento-multi-tenant-defesa-em-profundidade.md) | Isolamento multi-tenant em defesa em profundidade (pooled multi-tenancy) | Aceito | 2026-06-11 | @MiltonSilvaJr |
| [0002](./0002-snapshot-imutavel-comissao-parceiro.md) | Snapshot imutável de comissão de parceiro no Ganho da oportunidade | Aceito | 2026-06-11 | @MiltonSilvaJr |
| [0003](./0003-numeracao-sequencial-atomica-oportunidade-por-tenant.md) | Numeração sequencial atômica de oportunidade por tenant (AZ-NNNN) | Aceito | 2026-06-11 | @MiltonSilvaJr |
| [0004](./0004-outbox-transacional-idempotencia-eventos-dominio.md) | Outbox transacional e idempotência para eventos de domínio | Aceito | 2026-06-11 | @MiltonSilvaJr |
| [0005](./0005-provedor-email-transacional-resend.md) | Provedor de e-mail transacional: Resend atrás da ACL IEmailSender | Aceito | 2026-06-11 | @MiltonSilvaJr |
| [0006](./0006-scheduling-digest-job-horario-utc-hora-local-iana.md) | Scheduling do digest por job horário UTC com hora local IANA por tenant | Aceito | 2026-06-11 | @MiltonSilvaJr |
| [0007](./0007-stack-observabilidade-gcp.md) | Stack de observabilidade: GCP Cloud Logging, Monitoring e Trace | Aceito | 2026-06-11 | @MiltonSilvaJr |
| [0008](./0008-suporte-multimoeda-brl-usd-eur.md) | Suporte a multimoeda (BRL, USD, EUR) com Money = amount_cents + currency | Aceito | 2026-06-11 | @MiltonSilvaJr |
