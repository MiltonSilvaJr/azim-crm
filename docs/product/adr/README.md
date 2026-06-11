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

`0002`

## Tabela mestra

| ADR | Título | Status | Data | Autores |
|-----|--------|--------|------|---------|
| [0001](./0001-isolamento-multi-tenant-defesa-em-profundidade.md) | Isolamento multi-tenant em defesa em profundidade (pooled multi-tenancy) | Aceito | 2026-06-11 | @MiltonSilvaJr |
