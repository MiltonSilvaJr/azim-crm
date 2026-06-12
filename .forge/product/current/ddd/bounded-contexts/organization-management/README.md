# Bounded Context Canvas — Organization Management

## 1. Objetivo

Gerenciar a estrutura organizacional do tenant: BUs com configurações próprias (estágios, canais, motivos de perda), usuários, papéis RBAC e memberships por BU.

## 2. Classificação DDD
- **Tipo:** Supporting Subdomain
- **Subdomínio:** SD-08

## 3. Responsabilidades

- CRUD de BUs com estágios configuráveis, canais de origem e motivos de perda por BU
- Convite de usuários por e-mail com papel e membership por BU
- Gestão de papéis: Platform Operator, Tenant Admin, Gestor de BU, Vendedor, Viewer
- Desativação de usuário sem exclusão física (histórico preservado — RN-013)
- Fornecer contexto de RBAC (tenant_id, bu_id, user_id, papéis) para todos os demais contextos

## 4. Fora do Escopo

- Autenticação e sessão (Identity & Access)
- Provisionamento do tenant e branding (Tenancy & Branding)
- Dados comerciais (todos os outros BCs)

## 5. Linguagem Ubíqua

| Termo | Definição | Observações |
|---|---|---|
| BU (Business Unit) | Unidade de negócio do tenant com pipeline, usuários e configurações próprias | Ex.: Vellus, Axis, Vellus Tech |
| papel | Papel RBAC do usuário (PlatOp, TAdmin, GestorBU, Vendedor, Viewer) | Usuário pode ter papéis distintos por BU |
| membership | Vínculo entre um usuário e uma BU com papel específico | Múltiplos memberships por usuário |
| convite | Token temporário para ativação de conta de novo usuário | Expiração configurável (RN-030) |
| desativacao | Remoção do acesso sem exclusão física do usuário | FKs e histórico preservados (RN-013) |

## 6. Atores e Sistemas Relacionados

| Ator/Sistema | Relação |
|---|---|
| Tenant Admin (P-04) | Gerencia BUs, usuários e papéis |
| Platform Operator (P-05) | Provisiona tenants (neste BC: configura estrutura inicial) |
| Identity & Access | Upstream — autenticação delega sessão; Organization valida usuário no tenant |
| Notification Delivery | Downstream — e-mails de convite e recuperação de senha |
| Todos os BCs | Downstream — consomem tenant_id, bu_id, user_id e papéis |
| Audit Log | Downstream — eventos de escrita |

## 7. Agregados e Entidades

| Tipo | Nome | Descrição | Dono |
|---|---|---|---|
| Aggregate | BusinessUnit | BU com estágios, canais e motivos de perda configuráveis | Organization Management |
| Aggregate | User | Usuário do tenant com papel e memberships por BU | Organization Management |
| Entity | UserMembership | Vínculo usuário-BU com papel | Organization Management |
| Entity | UserInvitation | Convite com token temporário e expiração | Organization Management |
| Entity | Stage | Estágio de BU com probabilidade e categoria — escrita exclusiva deste BC; consumido por Opportunity Pipeline via referência (stage_id) | Organization Management |

## 8. Comandos

| Comando | Descrição | Ator |
|---|---|---|
| CreateBU | Cria BU com nome e configurações | Tenant Admin |
| ConfigureStages | Define estágios da BU com probabilidade e categoria | Tenant Admin |
| InviteUser | Envia convite com papel e BU(s) | Tenant Admin |
| DeactivateUser | Desativa usuário preservando histórico | Tenant Admin |
| AssignRole | Atribui ou altera papel de um usuário em uma BU | Tenant Admin |

## 9. Eventos de Domínio

| Evento | Quando | Consumidores |
|---|---|---|
| bu.created | BU criada com sucesso | Audit Log |
| user.invited | Convite enviado | Notification Delivery, Audit Log |
| user.activated | Usuário ativou a conta via convite | Audit Log |
| user.deactivated | Usuário desativado | Audit Log, Digest (remove de destinatários) |
| stage.configured | Estágio criado/atualizado | Audit Log, Opportunity Pipeline |

## 10. APIs Expostas

| API | Método | Finalidade |
|---|---|---|
| /api/v1/business-units | GET, POST | Listar e criar BUs |
| /api/v1/business-units/{id}/stages | GET, POST, PATCH | Configurar estágios da BU |
| /api/v1/users | GET, POST | Listar usuários e convidar |
| /api/v1/users/{id}/deactivate | POST | Desativar usuário |
| /api/v1/users/{id}/roles | PATCH | Alterar papel em BU |

## 11. Integrações

| Contexto/Sistema | Tipo | Padrão DDD |
|---|---|---|
| Identity & Access | Valida token de sessão e carrega usuário | Anti-Corruption Layer |
| Notification Delivery | Envia e-mails de convite via IEmailSender | Customer/Supplier |
| Audit Log | Publica eventos de escrita | Published Language |

## 12. Dados Próprios

| Entidade/Tabela | Finalidade | Retenção |
|---|---|---|
| business_units | Configuração de BUs do tenant | Ciclo de vida do tenant |
| stages | Estágios configuráveis por BU | Ciclo de vida da BU |
| origin_channels | Canais de origem por BU | Ciclo de vida da BU |
| loss_reasons | Motivos de perda por BU | Ciclo de vida da BU |
| users | Usuários do tenant (desativados preservados) | Indefinida (histórico) |
| user_memberships | Vínculos usuário-BU com papel | Indefinida |
| user_invitations | Convites pendentes | Curta duração (até ativação ou expiração) |

## 13. Requisitos Não Funcionais Específicos

| Categoria | Requisito |
|---|---|
| Segurança | RBAC aplicado em nível de API e verificado por RLS; matriz de permissões estrita |
| Performance | Carregamento de papéis e memberships via cache (Redis) — NFR-PERF-01 |

## 14. Decisões Arquiteturais Relacionadas

| ADR | Decisão |
|---|---|
| DEC-001 | Estágios configuráveis por BU — owned aqui, consumed pelo Pipeline |
| DEC-006 | Multi-tenancy pooled + RLS |

## 15. Riscos e Pontos de Atenção

- DDD-VAL-04: Sobreposição com Tenancy & Branding — considerar consolidação
- Estágios seed para Vellus: Lead (10%), Prospecção (25%), Diagnóstico (50%), Proposta Enviada (60%), Negociação (75%), Fechamento Provável (90%), Ganho (100%), Perdido (0%)
