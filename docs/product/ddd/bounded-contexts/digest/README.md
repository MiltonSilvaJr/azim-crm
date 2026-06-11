# Bounded Context Canvas — Digest

## 1. Objetivo

Orquestrar a seleção de destinatários, composição do conteúdo e envio do digest diário acionável por e-mail, garantindo idempotência e entrega no horário configurado do tenant.

## 2. Classificação DDD
- **Tipo:** Supporting Subdomain
- **Subdomínio:** SD-06

## 3. Responsabilidades

- Selecionar destinatários por fuso IANA do tenant e presença de pendências (RN-009, RN-011)
- Compor conteúdo de terça a sexta: atividades vencidas, de hoje, opps estagnadas, datas vencidas
- Compor "azimute da semana" na segunda: pipeline por estágio/BU, variação semanal, ganhos, fechamentos, bloco de metas
- Garantir idempotência via EmailDigestLog (user_id + date) — RN-010
- Processar links autenticados de 1 clique para conclusão/reagendamento de atividades
- Registrar status de entrega (entregue, aberto, bounce) via webhooks do provider

## 4. Fora do Escopo

- Envio físico do e-mail (Notification Delivery via ACL)
- Registro de atividades concluídas via digest (Activity Management)
- Composição de notificações in-app (Workflow Automation, Fase 2)

## 5. Linguagem Ubíqua

| Termo | Definição | Observações |
|---|---|---|
| digest | E-mail diário acionável enviado ao usuário com pendências comerciais | O CRM vai ao vendedor |
| azimute_da_semana | Conteúdo especial do digest de segunda-feira com visão de pipeline e metas | Apenas na segunda (RN-029) |
| EmailDigestLog | Registro por (user_id, date) que garante idempotência do digest | Impede reenvio no mesmo dia |
| horario_digest | Hora local de envio do digest configurada no tenant (default 07:00) | Por fuso IANA do tenant |
| pendencia | Atividade vencida, atividade de hoje, opp estagnada ou data de fechamento vencida | Critério de inclusão (RN-011) |
| link_autenticado | URL com token de ação incluída no digest para 1 clique | Expiração; idempotência |

## 6. Atores e Sistemas Relacionados

| Ator/Sistema | Relação |
|---|---|
| Cloud Scheduler | Dispara job horário UTC; este BC processa para cada tenant |
| Vendedor (P-01) | Destinatário; age via link autenticado |
| Gestor de BU (P-02) | Destinatário do azimute; recebe na segunda sempre |
| Executivo (P-03) | Destinatário do azimute; recebe na segunda sempre |
| Activity Management | Upstream — atividades vencidas e de hoje |
| Opportunity Pipeline | Upstream — opps estagnadas, datas vencidas, fechamentos esperados |
| Goal & Forecast | Upstream — bloco de metas do azimute |
| Organization Management | Upstream — usuários, papéis, fuso do tenant |
| Notification Delivery | Downstream via ACL — envio físico do e-mail |
| Audit Log | Downstream — EmailDigestLog como registro |

## 7. Agregados e Entidades

| Tipo | Nome | Descrição | Dono |
|---|---|---|---|
| Aggregate | DigestJob | Execução do digest para um tenant em uma data | Digest |
| Entity | EmailDigestLog | Registro de envio por (user_id, date) para idempotência | Digest |

## 8. Comandos

| Comando | Descrição | Ator |
|---|---|---|
| TriggerDigestJob | Inicia o processamento do digest para tenants elegíveis | Cloud Scheduler |
| SendUserDigest | Compõe e enfileira o digest do usuário | Digest Service (interno) |
| ExecuteActionViaDigest | Processa ação (concluir/reagendar) via link autenticado | Vendedor (via e-mail) |

## 9. Eventos de Domínio

| Evento | Quando | Consumidores |
|---|---|---|
| digest.sent | Digest enviado com sucesso para usuário na data | Audit Log; KPI-03/KPI-04 |
| digest.delivery_failed | Falha de entrega registrada | Audit Log; alerta operacional |

## 10. APIs Expostas

| API | Método | Finalidade |
|---|---|---|
| /api/v1/digest/action/{token} | POST | Processar ação via link autenticado do digest |

## 11. Integrações

| Contexto/Sistema | Tipo | Padrão DDD |
|---|---|---|
| Activity Management | Lê atividades vencidas e de hoje | Customer/Supplier |
| Opportunity Pipeline | Lê opps estagnadas e fechamentos esperados | Customer/Supplier |
| Goal & Forecast | Lê metas do período para azimute | Customer/Supplier |
| Notification Delivery | Envia e-mail via IEmailSender | Anti-Corruption Layer |
| Organization Management | Lê usuários, papéis e fuso do tenant | Conformist |

## 12. Dados Próprios

| Entidade/Tabela | Finalidade | Retenção |
|---|---|---|
| email_digest_logs | Idempotência e auditoria de envio por (user_id, date) | 90 dias (KPI-03/KPI-04) |
| digest_action_tokens | Tokens de ação para links autenticados de 1 clique | Curta duração (horas) |

## 13. Requisitos Não Funcionais Específicos

| Categoria | Requisito |
|---|---|
| Performance | Processamento completo em ≤ 5 min após trigger (NFR-PERF-05) |
| Disponibilidade | Entrega entre 07:00 e 07:05 BRT; falha = alerta operacional imediato |
| Resiliência | Idempotência obrigatória — retentativas não duplicam envio (RN-010) |
| Observabilidade | EmailDigestLog com status entregue/aberto/bounce; alimenta KPI-03/KPI-04 |

## 14. Decisões Arquiteturais Relacionadas

| ADR | Decisão |
|---|---|
| DEC-008 | Postmark / IEmailSender como abstração; SendGrid como alternativa |
| DEC-009 | Digest por fuso IANA do tenant; job horário UTC no Cloud Scheduler |

## 15. Riscos e Pontos de Atenção

- LAC-03/VAL-03: Postmark vs SendGrid — ACL garante que a troca não impacta este BC
- PRE-03: SPF/DKIM/DMARC devem estar configurados antes do primeiro envio em produção
- NFR-PERF-05: Com 1.000+ destinatários, processamento de 5 min exige paralelismo controlado
