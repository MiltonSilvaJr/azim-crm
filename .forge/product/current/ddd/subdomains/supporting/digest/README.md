# Digest — Supporting Subdomain

## 1. Classificação
- **Tipo:** Supporting Subdomain
- **Código:** SD-06

## 2. Descrição

Representa a capacidade de seleção, composição e envio do digest diário acionável. O digest é o principal mecanismo de alcance proativo ao vendedor — "o CRM vai ao vendedor". Inclui: seleção de destinatários por fuso IANA do tenant, composição do conteúdo (pendências de terça a sexta; azimute da semana na segunda), links autenticados de 1 clique e idempotência garantida pelo EmailDigestLog.

## 3. Justificativa da Classificação

Supporting porque: embora o Digest seja um diferencial de produto (CRM que vai ao vendedor), a lógica de domínio específica (seleção, composição, idempotência) é um conjunto de regras de orquestração sobre dados de outros contextos. O envio físico é Generic (Notification Delivery). Sem lógica de negócio proprietária complexa própria — é principalmente orquestração.

## 4. Capacidades Relacionadas

| Código | Capacidade | Descrição |
|---|---|---|
| CAP-08 | Digest Diário | Seleção por fuso, composição, azimute, idempotência |

## 5. Eventos de Negócio Relacionados

| Evento | Descrição |
|---|---|
| digest.sent | Digest enviado com sucesso para um usuário na data |
| digest.delivery_failed | Falha de entrega registrada no EmailDigestLog |
| activity.completed_via_digest | Atividade concluída via link autenticado do digest |

## 6. Regras de Negócio Relevantes

| Regra | Descrição |
|---|---|
| RN-009 | Seleção de destinatários por fuso IANA do tenant; disparo quando hora local = horario_digest |
| RN-010 | Idempotência: EmailDigestLog(user_id, date) impede reenvio no mesmo dia |
| RN-011 | Usuário sem pendências e sem papel de gestão não recebe o digest |
| RN-018 | Bloco de metas omitido quando não há meta cadastrada no período |
| RN-029 | Azimute da semana incluído apenas na segunda-feira |

## 7. Bounded Contexts Relacionados

| Bounded Context | Relação |
|---|---|
| BC-06 Digest | Implementa este subdomínio |
| BC-01 Opportunity Pipeline | Upstream — opps estagnadas, datas vencidas, fechamentos esperados |
| BC-04 Activity Management | Upstream — atividades vencidas e de hoje |
| BC-05 Goal & Forecast | Upstream — bloco de metas do azimute |
| BC-08 Organization Management | Upstream — usuários, papéis, fuso do tenant |
| BC-14 Notification Delivery | Downstream via ACL — envio físico do e-mail |
| BC-15 Audit Log | Downstream — EmailDigestLog é registro de auditoria de entrega |

## 8. Pontos a Validar

- LAC-03/VAL-03: Postmark vs SendGrid — a escolha do provider não deve afetar este BC (ACL garante)
- Performance: processamento completo em ≤ 5 min (NFR-PERF-05) para garantir entrega às 07:00 BRT
