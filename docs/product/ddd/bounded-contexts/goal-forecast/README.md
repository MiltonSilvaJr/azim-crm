# Bounded Context Canvas — Goal & Forecast

## 1. Objetivo

Gerenciar metas mensais por BU e por responsável; prover painel de comparação entre realizado, meta e pipeline disponível com graceful degradation quando não há meta cadastrada.

## 2. Classificação DDD
- **Tipo:** Supporting Subdomain
- **Subdomínio:** SD-05

## 3. Responsabilidades

- Cadastro de metas mensais por BU e/ou por responsável
- Derivação automática de agregações trimestral e anual a partir das mensais (RN-027)
- Painel comparativo: realizado vs meta vs pipeline disponível
- Graceful degradation: sem meta, exibe realizado e pipeline sem erro (RN-017)
- Fornecer bloco de metas para o digest de segunda-feira (omitido sem meta — RN-018)

## 4. Fora do Escopo

- Cálculo do realizado (derivado do Opportunity Pipeline)
- Cálculo do pipeline disponível (derivado do Opportunity Pipeline)
- Projeções avançadas e gap analysis (Fase 2)
- Relatórios de comissão (Reporting + Partner Management)

## 5. Linguagem Ubíqua

| Termo | Definição | Observações |
|---|---|---|
| Goal | Meta mensal de valor comercial por BU ou responsável | Em centavos inteiros |
| realizado | Soma do valor_total das oportunidades Ganhas no período | Derivado do Pipeline |
| pipeline_disponivel | Soma do forecast_ponderado das oportunidades abertas | Derivado do Pipeline |
| graceful_degradation | Exibição sem erro quando não há meta cadastrada no período | RN-017 |
| azimute_metas | Bloco do digest de segunda com realizado vs meta e gap | Omitido sem meta (RN-018) |

## 6. Atores e Sistemas Relacionados

| Ator/Sistema | Relação |
|---|---|
| Gestor de BU (P-02) | Cadastra e acompanha metas da sua BU |
| Executivo do Tenant (P-03) | Acompanha metas consolidadas do tenant |
| Tenant Admin (P-04) | Cadastra metas de qualquer BU/responsável |
| Opportunity Pipeline | Upstream — fornece dados de realizado e pipeline |
| Digest | Downstream — consome bloco de metas para o azimute |
| Reporting | Downstream — consome metas para relatórios de forecast |
| Audit Log | Downstream — eventos de escrita |

## 7. Agregados e Entidades

| Tipo | Nome | Descrição | Dono |
|---|---|---|---|
| Aggregate | Goal | Meta mensal por BU e/ou responsável | Goal & Forecast |
| Value Object | GoalPeriod | Período de referência (ano, mês) | Goal & Forecast |

## 8. Comandos

| Comando | Descrição | Ator |
|---|---|---|
| SetMonthlyGoal | Define ou atualiza meta mensal para BU ou responsável | Gestor de BU, Tenant Admin |

## 9. Eventos de Domínio

| Evento | Quando | Consumidores |
|---|---|---|
| goal.updated | Meta mensal criada ou atualizada | Audit Log, Digest |

## 10. APIs Expostas

| API | Método | Finalidade |
|---|---|---|
| /api/v1/goals | GET, POST | Listar e criar metas |
| /api/v1/goals/{bu_id}/{period} | GET, PUT | Ler e atualizar meta do período |
| /api/v1/forecast | GET | Painel comparativo realizado vs meta vs pipeline |

## 11. Integrações

| Contexto/Sistema | Tipo | Padrão DDD |
|---|---|---|
| Opportunity Pipeline | Lê realizado e pipeline para comparativo | Customer/Supplier (Pipeline = Supplier) |
| Digest | Fornece bloco de metas para o azimute | Customer/Supplier (Goal = Supplier) |
| Reporting | Fornece dados de metas para relatórios | Customer/Supplier (Goal = Supplier) |
| Audit Log | Publica eventos de escrita | Published Language |

## 12. Dados Próprios

| Entidade/Tabela | Finalidade | Retenção |
|---|---|---|
| goals | Metas mensais por BU e/ou responsável | Indefinida (histórico gerencial) |

## 13. Requisitos Não Funcionais Específicos

| Categoria | Requisito |
|---|---|
| Disponibilidade | Painel de metas deve degradar graciosamente sem meta cadastrada |

## 14. Decisões Arquiteturais Relacionadas

| ADR | Decisão |
|---|---|
| DEC-003 | Metas por BU e responsável, granularidade mensal, sem campo de meta trimestral/anual separado |

## 15. Riscos e Pontos de Atenção

- Fase 2: projeções avançadas com tendência de fechamento e gap analysis podem exigir modelo mais rico
- Graceful degradation deve ser testada explicitamente para garantir que o painel não exibe NaN ou placeholder vazio
