# Bounded Context Canvas — Workflow Automation

## 1. Objetivo

Permitir a criação e execução de workflows de automação comercial por BU de forma visual, sem código, com execução sempre assíncrona via Pub/Sub. (Fase 2)

## 2. Classificação DDD
- **Tipo:** Supporting Subdomain
- **Subdomínio:** SD-09
- **Fase:** 2

## 3. Responsabilidades

- Editor visual de workflows por BU com React Flow
- Nós: Gatilho (eventos de domínio), Condição, Ações, Fim
- Execução assíncrona via Pub/Sub → worker (nunca síncrona — FRD-workflow-02)
- Log de execução por nó; botão "Executar teste"
- Templates prontos

## 4. Linguagem Ubíqua

| Termo | Definição | Observações |
|---|---|---|
| workflow | Sequência de nós de automação por BU | Editor visual React Flow |
| trigger | Nó de gatilho ativado por evento de domínio (ex.: opportunity.stage_changed) | Upstream: Pipeline |
| action_node | Nó de ação (criar atividade, notificar, enviar e-mail, webhook, atualizar campo) | TBD |

## 5. Integrações

| Contexto/Sistema | Tipo | Padrão DDD |
|---|---|---|
| Opportunity Pipeline | Consome eventos de domínio como triggers | Customer/Supplier |
| Activity Management | Cria atividades como ação | Customer/Supplier |
| Notification Delivery | Envia e-mails como ação | Customer/Supplier |

## 6. Pontos a Validar

- DDD-VAL-05: Contrato de eventos do Pipeline — confirmar antes da Fase 2
- Isolamento de workflows entre tenants via Pub/Sub (subscription por tenant ou filtro de tenant_id)
