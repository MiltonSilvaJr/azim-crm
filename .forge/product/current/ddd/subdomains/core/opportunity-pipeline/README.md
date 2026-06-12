# Opportunity Pipeline — Core Domain

## 1. Classificação
- **Tipo:** Core Domain
- **Código:** SD-01

## 2. Descrição

Representa a capacidade nuclear do Azim CRM: o ciclo de vida completo de uma oportunidade comercial dentro do funil de vendas de uma BU. Inclui a criação da oportunidade com owner obrigatório, a progressão entre estágios configuráveis, o modelo de valor Setup + Recorrente, o cálculo de forecast ponderado por probabilidade de estágio, a gestão da comissão de parceiro por componente e o snapshot imutável ao fechar o negócio.

Este é o domínio que diferencia o Azim dos concorrentes (Salesforce, Zoho) — especialmente pela comissão de parceiro nativa com snapshot imutável.

## 3. Justificativa da Classificação

| Critério | Avaliação |
|---|---|
| Diferenciação estratégica | Sim — comissão nativa, owner obrigatório e forecast Setup+Recorrente são diferenciais confirmados |
| Complexidade de negócio | Alta — 30 regras de negócio (RN-001 a RN-028) mais invariantes de snapshot |
| Risco operacional | Crítico — R$ 9.013.250 em forecast da Vellus; falha aqui compromete a operação |
| Frequência de mudança | Alta — estágios configuráveis, probabilidades ajustáveis, canal de origem evolutivo |
| Possibilidade de compra | Não — nenhum CRM disponível oferece comissão nativa por componente com snapshot imutável |
| Dependência regulatória | Sim — LGPD (dados de contatos vinculados); trilha de auditoria obrigatória |
| Conhecimento especializado | Sim — modelo Setup+Recorrente, cálculo de comissão, regras de transição de estágio |

## 4. Capacidades Relacionadas

| Código | Capacidade | Descrição |
|---|---|---|
| CAP-01 | Gestão de Pipeline | Ciclo de vida completo de oportunidades |
| CAP-02 | Comissão Nativa de Parceiro | Cálculo por componente e snapshot imutável |
| CAP-03 | Forecast Ponderado | valor_total e forecast_ponderado por estágio |

## 5. Eventos de Negócio Relacionados

| Evento | Descrição |
|---|---|
| opportunity.created | Oportunidade criada com owner, conta e estágio inicial |
| opportunity.stage_changed | Oportunidade movida entre estágios com validação de regras |
| opportunity.won | Oportunidade fechada como Ganha; snapshot de comissão criado |
| opportunity.lost | Oportunidade fechada como Perdida com motivo |
| opportunity.stale | Oportunidade sem atividade há mais de 14 dias corridos |
| opportunity.reopened | Oportunidade Ganha/Perdida reaberta por Tenant Admin ou Gestor de BU |

## 6. Regras de Negócio Relevantes

| Regra | Descrição |
|---|---|
| RN-001 | Numeração sequencial imutável por tenant (AZ-NNNN) |
| RN-002 | owner_id obrigatório; pré-preenchido com usuário autenticado |
| RN-003 | data_fechamento_esperada obrigatória a partir de "Proposta Enviada" |
| RN-004 | motivo_perda obrigatório ao mover para "Perdido" |
| RN-005 | valor_total = valor_setup + valor_mensal × duracao_meses (centavos inteiros) |
| RN-006 | forecast_ponderado = valor_total × probabilidade_do_estagio |
| RN-007 | Snapshot imutável de comissão ao mover para "Ganho" |
| RN-008 | Canal "Parceiro" exige partner_id obrigatório |
| RN-016 | Reabertura restrita a Tenant Admin e Gestor de BU |
| RN-022 | Snapshot de comissão preservado após alterações futuras dos percentuais |
| RN-028 | Estagnação = 14 dias corridos sem atividade registrada |

## 7. Bounded Contexts Relacionados

| Bounded Context | Relação |
|---|---|
| BC-01 Opportunity Pipeline | Implementa este subdomínio |
| BC-02 Account Management | Upstream — fornece contas e contatos |
| BC-03 Partner Management | Upstream — fornece parceiros e percentuais default |
| BC-04 Activity Management | Upstream — fornece dados de atividades para detecção de estagnação |
| BC-15 Audit Log | Downstream — recebe eventos de escrita |

## 8. Pontos a Validar

- DDD-VAL-01: Activities podem ser módulo interno vs BC separado
- VAL-07: Comportamento ao fechar como Ganho com comissão em branco (alerta vs bloqueio)
- VAL-05: Multimoeda — se necessário antes do schema freeze, impacta valor_setup, valor_mensal e comissão
