# Bounded Context Canvas — Notification Delivery

## 1. Objetivo

Abstrair o envio de e-mail transacional via IEmailSender, protegendo os demais contextos da instabilidade ou troca de providers de e-mail (Postmark / SendGrid).

## 2. Classificação DDD
- **Tipo:** Generic Subdomain
- **Subdomínio:** SD-14

## 3. Responsabilidades

- Implementação de IEmailSender com Postmark (candidato primário) ou SendGrid (alternativa)
- Bounce handling e supressão de endereços inválidos
- Webhooks de evento (entregue/aberto) → alimenta KPI-03 e KPI-04
- Domínio de envio: mail.azim.com.br com SPF/DKIM/DMARC validados (DEC-008)

## 4. Linguagem Ubíqua

| Termo | Definição | Observações |
|---|---|---|
| IEmailSender | Abstração de envio de e-mail (interface .NET) | Implementação: Postmark ou SendGrid |
| bounce | Devolução de e-mail por endereço inválido ou caixa cheia | Registrado e suprimido |
| delivery_event | Evento de entrega/abertura do e-mail | Alimenta KPI-03/KPI-04 |

## 5. Integrações

| Contexto/Sistema | Tipo | Padrão DDD |
|---|---|---|
| Digest | Consome IEmailSender para envio do digest | Anti-Corruption Layer (Digest isolado do provider) |
| Organization Management | Consome IEmailSender para convites | Customer/Supplier |
| Identity & Access | Consome IEmailSender para recuperação de senha | Customer/Supplier |
| Postmark / SendGrid | Provider externo | Adapter |

## 6. Dados Próprios

Nenhum — logs de entrega ficam no EmailDigestLog do Digest e nos logs do provider.

## 7. Pontos a Validar

- VAL-03: Spike Postmark vs SendGrid; critério: entregabilidade ≥ 98% (KPI-03)
- PRE-03: SPF/DKIM/DMARC do domínio mail.azim.com.br antes do go-live
