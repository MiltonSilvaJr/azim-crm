# Bounded Context Canvas — Reporting

## 1. Objetivo

Prover relatórios operacionais e analíticos derivados de dados de outros contextos. Read-only. Sem escrita própria em entidades de negócio.

## 2. Classificação DDD
- **Tipo:** Supporting Subdomain
- **Subdomínio:** SD-07

## 3. Responsabilidades

- Funil por estágio: total de oportunidades e valor por estágio no período
- Forecast por BU e mês: forecast ponderado por BU e período
- Ranking por responsável: valor total e ganhos no período
- Oportunidades por canal de origem
- Comissões por parceiro: projetado (pipeline aberto) e consolidado (oportunidades Ganhas)
- Export CSV de todos os relatórios

## 4. Fora do Escopo

- Escrita em qualquer entidade de negócio
- Painel de metas e forecast (Goal & Forecast)
- Digest e notificações (Digest, Workflow Automation)

## 5. Linguagem Ubíqua

| Termo | Definição | Observações |
|---|---|---|
| funil | Distribuição de oportunidades por estágio com valor | Relatório de funil |
| ranking | Classificação de responsáveis por valor total e ganhos | Por período e BU |
| canal | Canal de origem da oportunidade (Parceiro, Inbound, Prospecção ativa, etc.) | Configurável por BU |
| comissao_projetada | Soma das comissões calculadas de opps abertas (não snapshot) | Usa percentuais atuais |
| comissao_consolidada | Soma dos snapshots imutáveis de opps Ganhas | Baseada em OpportunityPartnerCommission |

## 6. Integrações

| Contexto/Sistema | Tipo | Padrão DDD |
|---|---|---|
| Opportunity Pipeline | Lê oportunidades via read model ou view | Read Model |
| Partner Management | Lê parceiros para relatório de comissão | Read Model |
| Goal & Forecast | Lê metas para relatórios de forecast | Read Model |
| Organization Management | Consome tenant_id, bu_id e user_id para escopo de acesso | Conformist |

## 7. Dados Próprios

Nenhum dado próprio — apenas read models derivados de outros contextos. Pode manter materialized views ou projeções otimizadas para performance.

## 8. Requisitos Não Funcionais Específicos

| Categoria | Requisito |
|---|---|
| Performance | Queries de relatório não devem impactar latência da API transacional |
| Segurança | Resultados filtrados por RBAC: tenant_id, bu_id, owner_id |

## 9. Riscos e Pontos de Atenção

- DDD-VAL-02: Pode não precisar de BC formal — pode ser camada de queries dentro do azim-api
- Evitar joins diretos entre tabelas de contextos diferentes; usar read models ou views materializadas
