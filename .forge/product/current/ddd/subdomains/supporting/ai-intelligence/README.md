# AI Intelligence — Supporting Subdomain

## 1. Classificação
- **Tipo:** Supporting Subdomain
- **Código:** SD-10
- **Fase:** 3 (não escopo do MVP nem da Fase 2)

## 2. Descrição

Representa as capacidades de inteligência artificial do Azim: scoring de oportunidades em batch noturno, resumo 360° de conta sob demanda (briefing pré-reunião), próxima melhor ação por oportunidade com justificativa legível e copilot conversacional de vendas. Isolamento total de dados por tenant via rate-limit Redis e rastreabilidade completa via Langfuse.

## 3. Justificativa da Classificação

Supporting porque: IA é um diferencial futuro importante mas depende de validação das fases anteriores; a lógica de domínio das ações de vendas é proprietária mas a infraestrutura (LangGraph, Langfuse) é commodity. Stack completamente diferente (Python / FastAPI) justifica separação em deployable próprio.

## 4. Capacidades Relacionadas

| Código | Capacidade | Descrição |
|---|---|---|
| CAP-12 | Inteligência Artificial | Scoring, resumo 360°, próxima ação, copilot |

## 5. Eventos de Negócio Relacionados

| Evento | Descrição |
|---|---|
| scoring.completed | Score calculado para oportunidade em batch noturno |
| summary.generated | Resumo 360° de conta gerado sob demanda |

## 6. Regras de Negócio Relevantes

| Regra | Descrição |
|---|---|
| RF-13 | Dados jamais cruzam tenants; rate-limit por tenant via Redis |
| RF-13 | Custo de IA por tenant visível no painel de operações |

## 7. Bounded Contexts Relacionados

| Bounded Context | Relação |
|---|---|
| BC-11 AI Intelligence | Implementa este subdomínio |
| BC-01 Opportunity Pipeline | Upstream — dados de pipeline para scoring |
| BC-02 Account Management | Upstream — dados de conta para resumo 360° |
| BC-04 Activity Management | Upstream — histórico de atividades para contexto de IA |

## 8. Pontos a Validar

- Definir antes da Fase 3: quais dados do tenant a IA pode acessar e com qual latência
- Custo de inferência por tenant: como expor no painel de operações sem quebrar isolamento
