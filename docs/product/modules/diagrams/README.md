# Module Architecture Diagrams — Azim CRM

**Status:** Rascunho para revisão
**Versão:** v0.1
**Data:** 2026-06-11

---

## 1. Objetivo

Este diretório consolida os diagramas derivados da estrutura modular da solução Azim CRM.

---

## 2. Diagramas Disponíveis

| Diagrama | Arquivo | Finalidade |
|---|---|---|
| Arquitetura da Solução | solution-architecture.md | Módulos, deployables, sistemas externos e relações de alto nível |
| Dependências entre Módulos | module-dependencies.md | Grafo de dependências diretas entre módulos |
| Fluxos de Integração | integration-flows.md | Fluxos principais: digest, pipeline, migração |
| Fluxos de Compliance | compliance-flows.md | Consolidação dos compliance aplicáveis (LGPD) |
| Compliance LGPD | compliance-lgpd.md | Fluxo de PII por módulo e regras LGPD aplicáveis |

---

## 3. Diagramas de Compliance

| Compliance | Arquivo | Aplicável? | Motivo |
|---|---|---|---|
| LGPD / Privacidade | compliance-lgpd.md | Sim | contacts (nome, e-mail, telefone), users (e-mail, display_name), audit_logs (delta_json com PII mascarada) |
| PCI DSS | — | Não aplicável | Azim CRM não processa, transmite nem armazena dados de cartão de crédito |
| SOX / Auditoria Financeira | — | Ponto a Validar | Comissões imutáveis podem ser relevantes — avaliar com jurídico |

---

## 4. Notas

- Todos os diagramas usam sintaxe Mermaid compatível com GitHub Markdown.
- Diagramas de módulos individuais estão nos READMEs de cada módulo (seção §18).
- Atualizar os diagramas de arquitetura ao confirmar ou alterar módulos e deployables.
