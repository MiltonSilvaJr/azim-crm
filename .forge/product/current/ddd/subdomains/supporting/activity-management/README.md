# Activity Management — Supporting Subdomain

## 1. Classificação
- **Tipo:** Supporting Subdomain
- **Código:** SD-04

## 2. Descrição

Representa o registro e acompanhamento de atividades comerciais (call, reunião, e-mail, tarefa) vinculadas a oportunidades e contas. Inclui a visão de to-do diário (vencidas, hoje, próximas), conclusão em 1 clique e a detecção de estagnação de oportunidades (14 dias sem atividade). Alimenta diretamente o Digest com o conteúdo de pendências por usuário.

## 3. Justificativa da Classificação

Supporting porque: atividades são um mecanismo de suporte ao pipeline e ao digest; a lógica de detecção de estagnação é relativamente simples (contagem de dias); sem diferencial competitivo autônomo.

## 4. Capacidades Relacionadas

| Código | Capacidade | Descrição |
|---|---|---|
| CAP-06 | Atividades e Follow-ups | CRUD, to-do diário, conclusão 1 clique, detecção de estagnação |

## 5. Eventos de Negócio Relacionados

| Evento | Descrição |
|---|---|
| activity.created | Atividade registrada vinculada a oportunidade ou conta |
| activity.completed | Atividade concluída (portal ou link do digest) |
| activity.overdue | Atividade com data passada e não concluída |
| opportunity.stale | Oportunidade sem atividade há 14+ dias (gerado por Activity ou Pipeline — a definir) |

## 6. Regras de Negócio Relevantes

| Regra | Descrição |
|---|---|
| RN-028 | Estagnação: oportunidade aberta sem atividade nos últimos 14 dias corridos |

## 7. Bounded Contexts Relacionados

| Bounded Context | Relação |
|---|---|
| BC-04 Activity Management | Implementa este subdomínio |
| BC-01 Opportunity Pipeline | Upstream — atividades vinculadas a oportunidades; estagnação detectada aqui |
| BC-02 Account Management | Upstream — atividades podem ser vinculadas a contas |
| BC-06 Digest | Downstream — consome atividades vencidas/hoje para composição do digest |
| BC-15 Audit Log | Downstream — recebe eventos de escrita |

## 8. Pontos a Validar

- DDD-VAL-01: Se Activity Management deve ser um BC separado ou módulo interno ao Opportunity Pipeline — o ciclo de vida de uma atividade é relativamente dependente da oportunidade
- Ownership do evento opportunity.stale: gerado pelo Activity Management (ao detectar ausência de atividade) ou pelo Pipeline ao receber query?
