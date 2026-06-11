# Compliance Flows — Azim CRM

**Status:** Rascunho para revisão
**Versão:** v0.1

---

## 1. Objetivo

Documentar os fluxos regulatórios, normativos ou legais que impactam a arquitetura modular da solução Azim CRM.

---

## 2. Compliance Aplicável

| Compliance / Norma / Lei | Aplicável? | Motivo | Diagrama |
|---|---|---|---|
| LGPD (Lei nº 13.709/2018) | Sim | contacts (nome, e-mail, telefone), users (e-mail, display_name) são dados pessoais; audit_logs contém delta_json de entidades com PII | compliance-lgpd.md |
| PCI DSS | Não aplicável | Azim CRM não processa, transmite nem armazena dados de cartão de crédito em nenhum módulo | — |
| SOX / Auditoria Financeira | Ponto a Validar | Comissões imutáveis (opportunity_partner_commissions) podem ser relevantes em auditoria financeira futura | Avaliar com jurídico |
| WCAG 2.1 nível AA | Sim (usabilidade) | Requisito de acessibilidade para o azim-web (RN-020) | — |

---

## 3. Resumo do Impacto LGPD por Módulo

| Módulo | Dado Pessoal | Categoria | Ação Obrigatória |
|---|---|---|---|
| account-management | contacts.name, contacts.email, contacts.phone | PII direto | Mascarar em logs; política de retenção; acesso restrito por papel |
| organization | users.email, users.display_name | PII direto | Mascarar email em logs; expiração de sessão |
| audit-log | delta_json (contém PII de contacts e users) | PII indireta | Mascaramento obrigatório via PiiMasker antes da persistência |
| notification-delivery | e-mail do destinatário (em trânsito) | PII em trânsito | DPA com Postmark/SendGrid; não logar e-mail completo |
| digest | email_digest_logs.user_id (referência a usuário) | PII referenciada | Retenção de 90 dias; sem PII direta nos logs do worker |
| ai-intelligence (Fase 3) | dados de oportunidades/contas enviados ao LLM | PII potencial | Minimizar dados; DPA com provedor de LLM; não enviar PII sem base legal |

---

## 4. Observações

- O diagrama detalhado de fluxo LGPD está em `compliance-lgpd.md`.
- PCI DSS não requer diagrama — Azim CRM está totalmente fora do escopo PCI.
- WCAG AA é requisito de usabilidade do azim-web; não requer diagrama de fluxo de compliance.
- Ponto a Validar VAL-MOD-05: confirmar com jurídico prazos de retenção e descarte de contacts e audit_logs antes do go-live.
