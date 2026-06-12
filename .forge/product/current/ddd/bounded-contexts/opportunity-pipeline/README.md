# Bounded Context Canvas — Opportunity Pipeline

## 1. Objetivo

Gerenciar o ciclo de vida completo de oportunidades comerciais dentro do funil de vendas por BU: criação com owner obrigatório, transições de estágio com regras de validação, modelo de valor Setup + Recorrente, cálculo de forecast ponderado, comissão de parceiro nativa por componente e snapshot imutável ao fechar negócio.

## 2. Classificação DDD

- **Tipo:** Core Domain
- **Subdomínio:** SD-01 Opportunity Pipeline
- **Justificativa:** Diferencial competitivo confirmado — comissão nativa, owner obrigatório, forecast Setup+Recorrente e snapshot imutável. Nenhum CRM commodity oferece esses três juntos. Maior densidade de regras de domínio do sistema (RN-001 a RN-008, RN-016, RN-022, RN-028).

## 3. Responsabilidades

- Criar e gerenciar oportunidades com owner obrigatório e numeração sequencial imutável (AZ-NNNN)
- Controlar transições de estágio com validações (data de fechamento, motivo de perda)
- Calcular valor_total (Setup+Recorrente) e forecast_ponderado por probabilidade de estágio
- Gerenciar OpportunityPartnerCommission com percentuais por componente
- Gerar snapshot imutável de comissão ao mover para "Ganho"
- Detectar oportunidades estagnadas (14 dias sem atividade)
- Exibir Kanban por BU com soma de valor total e forecast por coluna
- Registrar toda transição na linha do tempo da oportunidade

## 4. Fora do Escopo

- Cadastro de contas e contatos (Account Management)
- Cadastro de parceiros e percentuais default (Partner Management)
- Registro e conclusão de atividades (Activity Management)
- Cálculo de metas e painel comparativo (Goal & Forecast)
- Envio do digest diário (Digest)
- Geração de relatórios (Reporting)
- Autenticação e RBAC (Identity & Access, Organization Management)

## 5. Linguagem Ubíqua

| Termo | Definição | Observações |
|---|---|---|
| Opportunity | Negociação comercial em andamento, com valor, owner e estágio definidos | Nunca "lead" (DEC-007: lead é estágio configurável) |
| Stage | Fase do funil com probabilidade default e categoria (aberta/ganha/perdida) | Configurável por BU |
| Owner | Responsável obrigatório pela oportunidade (user_id) | Nunca vazio — RN-002 |
| valor_total | valor_setup + valor_mensal × duracao_meses em centavos inteiros | Calculado, nunca digitado |
| forecast_ponderado | valor_total × probabilidade_do_estagio em centavos inteiros | Exibido em BRL pelo frontend |
| OpportunityPartnerCommission | Entidade que registra percentuais e valor calculado de comissão | Snapshot imutável ao ganhar |
| snapshot | Registro imutável dos valores de comissão no momento do fechamento como "Ganho" | Não editável por nenhum papel |
| stale | Oportunidade aberta sem atividade há 14+ dias corridos | Sinalizado no Kanban e no digest |
| data_fechamento_esperada | Data estimada de fechamento — obrigatória a partir de "Proposta Enviada" | Bloqueante na transição |
| motivo_perda | Razão do fechamento como "Perdido" — selecionado da lista configurável da BU | Bloqueante na transição |

## 6. Atores e Sistemas Relacionados

| Ator/Sistema | Relação com o contexto |
|---|---|
| Vendedor (P-01) | Cria oportunidades, avança estágios, registra comissão |
| Gestor de BU (P-02) | Visualiza pipeline da BU, redistribui oportunidades, reabre Ganhas/Perdidas |
| Tenant Admin (P-04) | Configura estágios, canais e motivos de perda; reabre oportunidades |
| Account Management | Upstream — fornece account_id para vínculo |
| Partner Management | Upstream — fornece partner_id e percentuais default |
| Activity Management | Upstream — fornece dados de última atividade para detecção de stale |
| Digest | Downstream — consome opps estagnadas, datas vencidas e fechamentos esperados |
| Reporting | Downstream — consome read models de oportunidades |
| Audit Log | Downstream — recebe AuditLog de toda escrita |

## 7. Agregados e Entidades

| Tipo | Nome | Descrição | Dono |
|---|---|---|---|
| Aggregate | Opportunity | Oportunidade comercial com todo o seu ciclo de vida | Opportunity Pipeline |
| Entity | OpportunityStageTransition | Registro de cada transição de estágio com timestamp e ator | Opportunity Pipeline |
| Entity | OpportunityPartnerCommission | Comissão por componente; snapshot imutável ao ganhar | Opportunity Pipeline |
| Value Object | OpportunityNumber | Número sequencial imutável no formato AZ-NNNN | Opportunity Pipeline |
| Value Object | Money | Valor monetário em centavos inteiros (BRL no MVP) | Transversal |
| Value Object | CommissionCalculation | Resultado calculado: pct_setup × valor_setup + pct_recorrente × valor_mensal × meses | Opportunity Pipeline |

## 8. Comandos

| Comando | Descrição | Ator/Sistema origem |
|---|---|---|
| CreateOpportunity | Cria oportunidade com owner obrigatório e numeração sequencial | Vendedor, Gestor de BU, Tenant Admin |
| MoveOpportunityStage | Transiciona oportunidade para outro estágio com validações | Vendedor (owner), Gestor de BU, Tenant Admin |
| SetPartnerCommission | Define percentuais de comissão por componente na oportunidade | Vendedor (owner), Gestor de BU, Tenant Admin |
| CloseOpportunityAsWon | Move para "Ganho" e gera snapshot imutável de comissão | Vendedor (owner), Gestor de BU, Tenant Admin |
| CloseOpportunityAsLost | Move para "Perdido" com motivo de perda obrigatório | Vendedor (owner), Gestor de BU, Tenant Admin |
| ReopenOpportunity | Reabre oportunidade Ganha ou Perdida | Tenant Admin, Gestor de BU |
| UpdateOpportunityValue | Atualiza valor_setup, valor_mensal, duracao_meses; recalcula valor_total e forecast | Vendedor (owner), Gestor de BU, Tenant Admin |

## 9. Eventos de Domínio

| Evento | Quando ocorre | Consumidores |
|---|---|---|
| opportunity.created | Oportunidade criada com sucesso | Audit Log, Digest (próxima execução) |
| opportunity.stage_changed | Transição de estágio validada e registrada | Audit Log, Digest, Workflow Automation |
| opportunity.won | Fechamento como Ganho; snapshot criado | Audit Log, Reporting, Digest |
| opportunity.lost | Fechamento como Perdido com motivo | Audit Log, Reporting, Digest |
| opportunity.stale | Detectado sem atividade há 14+ dias | Digest, Audit Log |
| opportunity.reopened | Reabertura de Ganha ou Perdida | Audit Log, Digest |

## 10. APIs Expostas

| API | Método | Finalidade |
|---|---|---|
| /api/v1/opportunities | POST | Criar oportunidade |
| /api/v1/opportunities/{id} | GET, PATCH | Ler e editar oportunidade |
| /api/v1/opportunities/{id}/stage | PATCH | Mover estágio (valida regras) |
| /api/v1/opportunities/{id}/commission | PUT | Registrar comissão de parceiro |
| /api/v1/kanban | GET | Visão Kanban da BU com colunas e somas |
| /api/v1/opportunities | GET | Visão lista com filtros salvos |

## 11. Integrações

| Contexto/Sistema | Tipo de relação | Padrão DDD |
|---|---|---|
| Account Management | Lê account_id para vínculo | Customer/Supplier |
| Partner Management | Lê partner_id e percentuais default | Customer/Supplier |
| Activity Management | Lê última atividade para detecção de stale | Customer/Supplier |
| Digest | Publica eventos para seleção de conteúdo do digest | Published Language |
| Audit Log | Publica eventos de escrita | Published Language |
| Organization Management | Consome tenant_id, bu_id, user_id e papéis para RBAC | Conformist |

## 12. Dados Próprios

| Entidade/Tabela/Collection | Finalidade | Retenção |
|---|---|---|
| opportunities | Dados completos da oportunidade | Indefinida (histórico comercial) |
| opportunity_stage_transitions | Linha do tempo de estágios | Indefinida (auditoria) |
| opportunity_partner_commissions | Comissão por componente; snapshot imutável | Indefinida (relatório de comissões) |

> **Nota de ownership:** as tabelas `stages`, `origin_channels` e `loss_reasons` são de escrita exclusiva do **Organization Management** (BC-08), que as configura por BU. O Opportunity Pipeline as consome por referência (`stage_id`, `origin_channel`, `loss_reason`) via API de Organization Management — sem escrita própria nessas tabelas.

## 13. Requisitos Não Funcionais Específicos

| Categoria | Requisito |
|---|---|
| Performance | Kanban: p95 ≤ 2 s com 2.000 opps (NFR-PERF-02); criação/edição: p95 ≤ 500 ms (NFR-PERF-03) |
| Segurança | RLS obrigatório; testes de isolamento de tenant em CI como gate (NFR-SEG; RN-012) |
| Observabilidade | AuditLog para toda escrita; trilha de linha do tempo em tempo real |
| Disponibilidade | 99,5% uptime mensal (NFR-DISP) |
| Compliance | LGPD: PII de contatos não em logs (RN-025); snapshot imutável de comissão |

## 14. Decisões Arquiteturais Relacionadas

| ADR | Decisão |
|---|---|
| DEC-001 | Estágios configuráveis por BU com probabilidade default |
| DEC-002 | Comissão por componente (pct_setup, pct_recorrente) + snapshot imutável ao ganhar |
| DEC-011 | Valores monetários como centavos inteiros em toda entidade |
| DEC-006 | Multi-tenancy pooled + RLS — tenant_id em toda tabela |

## 15. Riscos e Pontos de Atenção

- Risco: snapshot de comissão pode ser gerado com campos em branco se parceiro não tiver percentuais (VAL-07) — definir se alerta ou bloqueio
- Risco: performance do Kanban com 2.000+ oportunidades por tenant exige índices compostos (tenant_id, bu_id, stage_id)
- Ponto: multimoeda (LAC-05/VAL-05) — se necessário antes do schema freeze, impacta valor_setup, valor_mensal e valor_total
