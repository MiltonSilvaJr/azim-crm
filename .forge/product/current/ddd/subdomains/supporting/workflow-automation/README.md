# Workflow Automation — Supporting Subdomain

## 1. Classificação
- **Tipo:** Supporting Subdomain
- **Código:** SD-09
- **Fase:** 2 (não escopo do MVP)

## 2. Descrição

Representa a criação e execução de workflows de automação comercial por BU de forma visual, sem código. Inclui editor React Flow com nós de Gatilho (eventos de domínio), Condição, Ações (criar atividade, atualizar campo, notificar in-app, enviar e-mail, webhook) e Fim. Execução sempre assíncrona via Pub/Sub → worker, com log de execução por nó.

## 3. Justificativa da Classificação

Supporting porque: automações reduzem trabalho manual repetitivo mas não são o diferencial competitivo central; é uma funcionalidade comum em CRMs modernos. Importante para a Fase 2 mas não para o MVP.

## 4. Capacidades Relacionadas

| Código | Capacidade | Descrição |
|---|---|---|
| CAP-11 | Automações Visuais | Editor React Flow; execução assíncrona via Pub/Sub |

## 5. Eventos de Negócio Relacionados

| Evento | Descrição |
|---|---|
| workflow.created | Workflow de automação criado por BU |
| workflow.triggered | Gatilho de automação ativado por evento de domínio |
| workflow.executed | Nó de workflow executado com log |

## 6. Regras de Negócio Relevantes

| Regra | Descrição |
|---|---|
| FRD-workflow-02 | Execução sempre assíncrona via Pub/Sub → worker; nunca síncrona |

## 7. Bounded Contexts Relacionados

| Bounded Context | Relação |
|---|---|
| BC-10 Workflow Automation | Implementa este subdomínio |
| BC-01 Opportunity Pipeline | Upstream — eventos de domínio disparam gatilhos de automação |
| BC-04 Activity Management | Downstream — automações podem criar atividades |
| BC-14 Notification Delivery | Downstream — automações podem enviar e-mails |

## 8. Pontos a Validar

- DDD-VAL-05: Contrato de eventos entre Pipeline e Workflow Automation — pull vs push; confirmar antes da Fase 2
