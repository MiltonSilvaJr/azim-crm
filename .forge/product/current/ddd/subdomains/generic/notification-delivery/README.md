# Notification Delivery — Generic Subdomain

## 1. Classificação
- **Tipo:** Generic Subdomain
- **Código:** SD-14

## 2. Descrição

Representa a abstração de envio de e-mail transacional via IEmailSender. O provider primário candidato é Postmark; SendGrid é alternativa (LAC-03 / VAL-03). Inclui tratamento de bounce, supressão e webhooks de evento (entregue/aberto) que alimentam KPIs de digest (KPI-03, KPI-04). A abstração protege os demais contextos (especialmente o Digest) do modelo específico do provider.

## 3. Justificativa da Classificação

Generic porque: envio de e-mail transacional é commodity; a abstração IEmailSender permite trocar de provider sem impacto no modelo de domínio; sem lógica de negócio proprietária.

## 4. Capacidades Relacionadas

| Código | Capacidade | Descrição |
|---|---|---|
| CAP-16 | Entrega de Notificações | IEmailSender / Postmark / SendGrid; bounce; webhooks |

## 5. Bounded Contexts Relacionados

| Bounded Context | Relação |
|---|---|
| BC-14 Notification Delivery | Implementa este subdomínio |
| BC-06 Digest | Upstream — consome IEmailSender via ACL |
| BC-08 Organization Management | Upstream — consome IEmailSender para e-mails de convite |
| BC-12 Identity & Access | Upstream — consome IEmailSender para recuperação de senha |

## 6. Pontos a Validar

- VAL-03: Spike de entregabilidade Postmark vs SendGrid deve ser executado antes do primeiro envio em produção
- PRE-03: SPF/DKIM/DMARC do domínio mail.azim.com.br devem estar configurados antes do go-live
