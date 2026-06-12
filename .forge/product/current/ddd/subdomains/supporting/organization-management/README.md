# Organization Management — Supporting Subdomain

## 1. Classificação
- **Tipo:** Supporting Subdomain
- **Código:** SD-08

## 2. Descrição

Representa a estrutura organizacional de um tenant: BUs (unidades de negócio) com configurações próprias (estágios, canais de origem, motivos de perda), usuários, papéis (RBAC) e memberships por BU. Inclui convite de usuários, desativação (sem exclusão física), e a matriz de permissões que governa o acesso a todos os demais contextos.

## 3. Justificativa da Classificação

Supporting porque: a estrutura organizacional é necessária para o pipeline mas não é o diferencial; qualquer sistema multi-tenant com RBAC precisa disso. A configurabilidade de estágios e canais por BU é importante mas implementável com commodity.

## 4. Capacidades Relacionadas

| Código | Capacidade | Descrição |
|---|---|---|
| CAP-10 | Gestão de Organização | BUs, usuários, papéis, RBAC, convites |

## 5. Eventos de Negócio Relacionados

| Evento | Descrição |
|---|---|
| bu.created | BU criada no tenant com configurações iniciais |
| user.invited | Usuário convidado com papel e BU(s) |
| user.deactivated | Usuário desativado (histórico preservado) |
| stage.configured | Estágio de BU criado/atualizado com probabilidade |

## 6. Regras de Negócio Relevantes

| Regra | Descrição |
|---|---|
| RN-013 | Desativação de usuário preserva todo o histórico (FKs intactas) |
| RN-030 | Link de convite com expiração configurável (referência: 72h) |
| MSG-016 | Não inativar BU com oportunidades ativas |
| MSG-017 | Tenant deve ter ao menos um Tenant Admin ativo |

## 7. Bounded Contexts Relacionados

| Bounded Context | Relação |
|---|---|
| BC-08 Organization Management | Implementa este subdomínio |
| BC-12 Identity & Access | Upstream — autenticação delega para Identity Platform; Organization valida usuário no tenant |
| BC-01 Opportunity Pipeline | Downstream — consome BU, owner_id, estágios e canais |
| Todos os BCs | Downstream — consomem user_id, bu_id e papéis para RBAC |
| BC-15 Audit Log | Downstream — recebe eventos de escrita |

## 8. Pontos a Validar

- DDD-VAL-04: Sobreposição com Tenancy & Branding — slug está no Tenancy, BUs estão aqui, mas ambos configurados pelo Tenant Admin. Pode ser consolidado.
- Estágios seed para Vellus: Lead (10%) → Prospecção (25%) → Diagnóstico (50%) → Proposta Enviada (60%) → Negociação (75%) → Fechamento Provável (90%) → Ganho (100%) / Perdido (0%)
