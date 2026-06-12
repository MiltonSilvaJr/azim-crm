# Goal & Forecast — Supporting Subdomain

## 1. Classificação
- **Tipo:** Supporting Subdomain
- **Código:** SD-05

## 2. Descrição

Representa o cadastro de metas comerciais mensais por BU e/ou por responsável, e a comparação contínua com o realizado e o pipeline disponível. Implementa graceful degradation: sem meta cadastrada, o painel exibe apenas realizado vs pipeline sem erro. Agrega automaticamente metas trimestrais e anuais a partir das mensais.

## 3. Justificativa da Classificação

Supporting porque: metas e forecast comparativo são importantes para gestores e executivos mas não diferencia o Azim do mercado; derivam do modelo de pipeline (Core). A lógica de agregação é simples (soma dos meses).

## 4. Capacidades Relacionadas

| Código | Capacidade | Descrição |
|---|---|---|
| CAP-07 | Metas e Forecast Comparativo | Cadastro mensal; painel realizado vs meta vs pipeline |

## 5. Eventos de Negócio Relacionados

| Evento | Descrição |
|---|---|
| goal.updated | Meta mensal cadastrada ou atualizada por BU/responsável |

## 6. Regras de Negócio Relevantes

| Regra | Descrição |
|---|---|
| RN-017 | Graceful degradation: sem meta, painel exibe realizado e pipeline sem erro ou placeholder |
| RN-018 | Bloco de metas completamente omitido no digest sem meta cadastrada |
| RN-027 | Agregação derivada: trimestral = soma de 3 meses; anual = soma de 12 meses |

## 7. Bounded Contexts Relacionados

| Bounded Context | Relação |
|---|---|
| BC-05 Goal & Forecast | Implementa este subdomínio |
| BC-01 Opportunity Pipeline | Upstream — realizado e pipeline derivados das oportunidades |
| BC-06 Digest | Downstream — consome metas para o bloco do azimute da semana |
| BC-07 Reporting | Downstream — consome metas para relatórios de forecast |
| BC-15 Audit Log | Downstream — recebe eventos de escrita |

## 8. Pontos a Validar

- Fase 2: projeções avançadas com tendência de fechamento e gap analysis — pode exigir modelo mais complexo que muda a classificação de Supporting
