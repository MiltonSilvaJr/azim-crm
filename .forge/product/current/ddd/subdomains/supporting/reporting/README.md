# Reporting — Supporting Subdomain

## 1. Classificação
- **Tipo:** Supporting Subdomain
- **Código:** SD-07

## 2. Descrição

Representa a geração de relatórios operacionais e analíticos para gestores e executivos. Inclui: funil por estágio, forecast por BU e mês, ranking por responsável, oportunidades por canal de origem, comissões por parceiro (projetadas e consolidadas) e export CSV. Todos os relatórios são read models derivados de dados de outros contextos — sem escrita própria.

## 3. Justificativa da Classificação

Supporting porque: relatórios são importantes mas são uma camada de leitura sobre dados primários de outros contextos. Não possuem modelo de domínio próprio complexo. Implementação típica com views, queries otimizadas ou materialized views.

## 4. Capacidades Relacionadas

| Código | Capacidade | Descrição |
|---|---|---|
| CAP-09 | Relatórios | Funil, forecast, ranking, canais, comissões, export CSV |

## 5. Eventos de Negócio Relacionados

Reporting não gera eventos de domínio — é exclusivamente leitura.

## 6. Regras de Negócio Relevantes

Sem regras de negócio próprias. Aplica regras de escopo de acesso (RBAC) para determinar quais dados o usuário pode ver nos relatórios.

## 7. Bounded Contexts Relacionados

| Bounded Context | Relação |
|---|---|
| BC-07 Reporting | Implementa este subdomínio |
| BC-01 Opportunity Pipeline | Upstream — fonte primária dos dados de oportunidades e comissão |
| BC-03 Partner Management | Upstream — dados de parceiros para relatório de comissões |
| BC-05 Goal & Forecast | Upstream — dados de metas para relatório de forecast |
| BC-08 Organization Management | Upstream — filtros por BU, responsável e papéis |

## 8. Pontos a Validar

- DDD-VAL-02: Reporting pode não precisar de BC formal — pode ser apenas uma camada de queries dentro do azim-api com read models projections
- Fase 2: Dashboards avançados podem exigir modelo de leitura mais complexo (event sourcing ou CQRS explícito)
