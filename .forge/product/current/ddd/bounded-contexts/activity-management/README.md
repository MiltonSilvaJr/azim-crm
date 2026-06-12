# Bounded Context Canvas — Activity Management

## 1. Objetivo

Gerenciar atividades comerciais vinculadas a oportunidades e contas: CRUD de atividades, to-do diário por usuário, conclusão em 1 clique e detecção de oportunidades estagnadas (14 dias sem atividade).

## 2. Classificação DDD
- **Tipo:** Supporting Subdomain
- **Subdomínio:** SD-04

## 3. Responsabilidades

- CRUD de atividades com tipo (call, reunião, e-mail, tarefa), data, hora, responsável e descrição
- Visão "Meu dia / Minha semana": atividades vencidas, de hoje e próximas
- Conclusão em 1 clique a partir da lista ou de link autenticado do digest
- Sugestão de próxima atividade ao concluir
- Detecção de oportunidades estagnadas para sinalização no Kanban e no digest

## 4. Fora do Escopo

- Envio do digest (Digest)
- Transições de estágio (Opportunity Pipeline)
- Metas e forecast (Goal & Forecast)

## 5. Linguagem Ubíqua

| Termo | Definição | Observações |
|---|---|---|
| Activity | Ação comercial registrada (call, reunião, e-mail, tarefa) | Vinculada a oportunidade ou conta |
| overdue | Atividade com data passada e status não concluída | Alimenta o digest |
| stagnation | Oportunidade aberta sem atividade nos últimos 14 dias corridos | RN-028 |
| to_do | Lista de atividades do usuário: vencidas, hoje, próximas | Visão "Meu dia / Minha semana" |

## 6. Atores e Sistemas Relacionados

| Ator/Sistema | Relação |
|---|---|
| Vendedor (P-01) | Cria, edita e conclui atividades das suas oportunidades |
| Gestor de BU (P-02) | Acessa atividades da sua BU; redistribui |
| Digest | Downstream — consome atividades vencidas e de hoje |
| Opportunity Pipeline | Upstream (vínculo) — atividades referenciadas por opportunity_id |
| Account Management | Upstream (vínculo) — atividades podem ser vinculadas a account_id |
| Audit Log | Downstream — eventos de escrita |

## 7. Agregados e Entidades

| Tipo | Nome | Descrição | Dono |
|---|---|---|---|
| Aggregate | Activity | Ação comercial com tipo, data, responsável e status | Activity Management |

## 8. Comandos

| Comando | Descrição | Ator |
|---|---|---|
| CreateActivity | Cria atividade vinculada a oportunidade ou conta | Vendedor, Gestor, TAdmin |
| CompleteActivity | Marca atividade como concluída | Vendedor (sua), Gestor, TAdmin |
| RescheduleActivity | Altera data/hora de uma atividade | Vendedor (sua), Gestor, TAdmin |

## 9. Eventos de Domínio

| Evento | Quando | Consumidores |
|---|---|---|
| activity.created | Atividade criada | Audit Log |
| activity.completed | Atividade concluída (portal ou digest) | Audit Log, Digest (idempotência) |
| activity.overdue | Atividade vencida e não concluída (detectada no digest job) | Digest |

## 10. APIs Expostas

| API | Método | Finalidade |
|---|---|---|
| /api/v1/activities | GET, POST | Listar e criar atividades |
| /api/v1/activities/{id}/complete | POST | Concluir atividade (com idempotência) |
| /api/v1/activities/my-day | GET | To-do diário do usuário |
| /api/v1/opportunities/{id}/stale | GET | Verificar estagnação de oportunidade |

## 11. Integrações

| Contexto/Sistema | Tipo | Padrão DDD |
|---|---|---|
| Digest | Fornece lista de atividades vencidas e de hoje | Customer/Supplier (Activity = Supplier) |
| Opportunity Pipeline | Recebe opportunity_id como referência | Customer/Supplier |
| Organization Management | Consome user_id para atribuição e RBAC | Conformist |
| Audit Log | Publica eventos de escrita | Published Language |

## 12. Dados Próprios

| Entidade/Tabela | Finalidade | Retenção |
|---|---|---|
| activities | Atividades comerciais com tipo, data, status e vínculos | Indefinida (histórico) |

## 13. Requisitos Não Funcionais Específicos

| Categoria | Requisito |
|---|---|
| Performance | Detecção de estagnação em tempo real — índice em (tenant_id, opportunity_id, completed_at) |
| Segurança | RLS por tenant_id; escopo de visibilidade por bu_id e owner_id |

## 14. Decisões Arquiteturais Relacionadas

| ADR | Decisão |
|---|---|
| RN-028 | Critério de estagnação: 14 dias corridos sem atividade registrada |

## 15. Riscos e Pontos de Atenção

- DDD-VAL-01: Se deve ser BC separado ou módulo do Opportunity Pipeline
- Idempotência da conclusão via digest: o link autenticado deve funcionar apenas uma vez (verificação por activity_id + completed_at)
