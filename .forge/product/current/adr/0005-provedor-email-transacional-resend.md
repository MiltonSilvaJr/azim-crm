---
title: Provedor de e-mail transacional — Resend atrás da ACL IEmailSender
status: Aceito
date: 2026-06-11
deciders:
  - "@MiltonSilvaJr"
consulted:
  - notification-delivery (DD-001)
informed:
  - Times de notificação e infraestrutura
---

# ADR-0005 — Provedor de e-mail transacional: Resend atrás da ACL IEmailSender

- **Status:** Aceito
- **Data:** 2026-06-11
- **Autores:** @MiltonSilvaJr
- **Supersede:** —
- **Substituído por:** —

## Contexto e Problema

O Azim CRM envia **e-mails transacionais** (convites, notificações, digest — ver ADR-0006). É
preciso escolher um **provedor de envio** e definir como isolá-lo do domínio para que a escolha não
contamine o código de negócio nem dificulte uma eventual troca. A entregabilidade depende de
**autenticação de domínio** (SPF/DKIM/DMARC) corretamente configurada.

O problema: **qual provedor de e-mail transacional adotar e como encapsulá-lo para preservar a
portabilidade?**

## Drivers da Decisão

- **Entregabilidade:** autenticação de domínio (SPF/DKIM/DMARC) e boa reputação de envio.
- **DX e simplicidade de integração:** API limpa, observável.
- **Portabilidade:** decisão reversível; trocar de provedor não deve tocar o domínio.
- **Custo adequado ao estágio do produto.**

## Decisão

Adotamos **Resend** como provedor de e-mail transacional, integrado **atrás da ACL `IEmailSender`**
por meio da implementação `ResendEmailSender`. O domínio depende apenas da interface `IEmailSender`;
o provedor concreto é detalhe de infraestrutura.

**Postmark** e **SendGrid** **não** são adotados agora, mas permanecem como **alternativas viáveis
atrás da mesma interface** — uma futura troca implementa outro `IEmailSender` sem alterar o domínio.

A configuração de **SPF/DKIM/DMARC** no domínio de envio é **gate de go-live**: nenhum envio em
produção antes da autenticação de domínio validada.

## Opções Consideradas

### Opção A — Postmark
- **Bom:** forte reputação em e-mail transacional; bom tooling.
- **Ruim:** não selecionado nesta decisão (custo/encaixe). Permanece como alternativa atrás de `IEmailSender`.
- **Veredito:** **Não adotado agora** (reserva).

### Opção B — SendGrid
- **Bom:** ampla adoção, escala.
- **Ruim:** complexidade/peso maior do que o necessário neste estágio; não selecionado.
- **Veredito:** **Não adotado agora** (reserva).

### Opção C (escolhida) — Resend atrás de `IEmailSender`
- **Bom:** API moderna e simples; boa DX; encaixe no estágio do produto; isolado por ACL,
  preservando portabilidade.
- **Ruim:** provedor mais recente que os incumbentes — dependência mitigada pela ACL.
- **Veredito:** **Aceita.**

## Consequências

### Positivas
- Integração rápida com boa DX; domínio desacoplado do provedor via `IEmailSender`.
- **Troca de provedor barata:** basta nova implementação da ACL (Postmark/SendGrid prontos como reserva).
- Entregabilidade tratada como gate explícito (SPF/DKIM/DMARC).

### Negativas e Mitigações
- **Acoplamento operacional ao Resend** (limites de envio, status, suporte). *Mitigação:* ACL
  `IEmailSender` isola o domínio; monitorar bounces/erros; reserva de provedores alternativos.
- **Dependência da configuração correta de DNS/autenticação.** *Mitigação:* SPF/DKIM/DMARC como
  **gate de go-live** verificável antes de produção.

## Conformidade

1. Domínio depende somente de `IEmailSender`; nenhum acoplamento direto ao SDK do provedor fora da ACL.
2. `ResendEmailSender` é a implementação ativa.
3. SPF/DKIM/DMARC do domínio de envio validados antes do go-live (gate verificável).

## Referências

- Design — notification-delivery **DD-001** — `docs/product/modules/notification-delivery/design.md`
- ADR-0006 — Scheduling do digest (consumidor de envio de e-mail)
