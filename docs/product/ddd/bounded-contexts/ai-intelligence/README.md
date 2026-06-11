# Bounded Context Canvas — AI Intelligence

## 1. Objetivo

Prover capacidades de IA para scoring de oportunidades, resumo 360° de conta, próxima melhor ação e copilot conversacional de vendas, com isolamento total por tenant. (Fase 3)

## 2. Classificação DDD
- **Tipo:** Supporting Subdomain
- **Subdomínio:** SD-10
- **Fase:** 3
- **Stack:** Python / FastAPI / LangGraph / Langfuse

## 3. Responsabilidades

- Scoring de oportunidades em batch noturno via Pub/Sub
- Resumo 360° de conta sob demanda
- Próxima melhor ação por oportunidade com justificativa legível
- Copilot conversacional de vendas (REST síncrono)
- Rastreabilidade completa via Langfuse
- Rate-limit por tenant via Redis; custo de IA visível no painel de operações

## 4. Linguagem Ubíqua

| Termo | Definição | Observações |
|---|---|---|
| scoring | Pontuação de probabilidade de fechamento de oportunidade | Batch noturno |
| briefing | Resumo 360° de conta antes de reunião | Sob demanda |
| next_best_action | Próxima ação sugerida com justificativa legível | Por oportunidade |
| copilot | Assistente conversacional sobre dados do tenant | REST síncrono; isolamento garantido |

## 5. Integrações

| Contexto/Sistema | Tipo | Padrão DDD |
|---|---|---|
| Opportunity Pipeline | Lê dados para scoring e next best action | Customer/Supplier |
| Account Management | Lê dados de conta para briefing | Customer/Supplier |
| Activity Management | Lê histórico para contexto de IA | Customer/Supplier |

## 6. Pontos a Validar

- Dados de treinamento/inferência: confirmar que nenhum dado de um tenant alimenta contexto de outro
- Política de retenção de logs de inferência (Langfuse) sob LGPD
