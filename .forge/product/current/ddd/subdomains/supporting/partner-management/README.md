# Partner Management — Supporting Subdomain

## 1. Classificação
- **Tipo:** Supporting Subdomain
- **Código:** SD-03

## 2. Descrição

Representa a gestão de parceiros comissionados do tenant. Parceiros têm papel tipado, percentuais default por componente (pct_setup, pct_recorrente) e uma visão de comissão projetada (pipeline aberto) e consolidada (oportunidades Ganhas) por período. No MVP, o parceiro é uma entidade de dados sem acesso ao sistema (DEC-012).

## 3. Justificativa da Classificação

Supporting porque: a gestão do cadastro de parceiro é necessária ao Core mas não é o diferencial em si; o diferencial está no snapshot imutável de comissão (Core Domain). O Partner Management provê os dados defaults que o Pipeline usa no cálculo.

## 4. Capacidades Relacionadas

| Código | Capacidade | Descrição |
|---|---|---|
| CAP-05 | Gestão de Parceiros | CRUD com percentuais default; visão de comissão projetada/consolidada |

## 5. Eventos de Negócio Relacionados

| Evento | Descrição |
|---|---|
| partner.created | Parceiro cadastrado com papel e percentuais default |
| partner.commission_percentages_updated | Percentuais default alterados (não afeta snapshots já criados) |

## 6. Regras de Negócio Relevantes

| Regra | Descrição |
|---|---|
| RN-021 | Parceiro não tem login, credencial ou portal no MVP (DEC-012) |
| RN-026 | Fórmula: comissão = pct_setup × valor_setup + pct_recorrente × valor_mensal × meses_comissionados |
| RN-022 | Snapshot imutável não é afetado por edição futura dos percentuais do parceiro |
| RN-008 | Canal "Parceiro" na oportunidade exige partner_id (regra implementada no Pipeline) |

## 7. Bounded Contexts Relacionados

| Bounded Context | Relação |
|---|---|
| BC-03 Partner Management | Implementa este subdomínio |
| BC-01 Opportunity Pipeline | Downstream — consome parceiros e percentuais; publica snapshots de comissão |
| BC-07 Reporting | Downstream — consome dados de comissão para relatórios |
| BC-15 Audit Log | Downstream — recebe eventos de escrita |

## 8. Pontos a Validar

- 11 parceiros da Vellus sem percentuais definidos na planilha (VAL-01) — a triagem de migração deve forçar ou alertar
- Portal do parceiro (DEC-012): escopo futuro; quando implementado, muda o modelo de acesso completamente
