# Context Map — Azim CRM

## 1. Visão Geral

O Azim CRM é composto por 15 bounded contexts organizados em torno de um Core Domain (Opportunity Pipeline) apoiado por 10 Supporting Subdomains e 4 Generic Subdomains.

O **Opportunity Pipeline** é o contexto central — todos os outros contextos ou fornecem dados para ele (upstream) ou o consomem (downstream). O isolamento multi-tenant via RLS e tenant_id do Tenancy & Branding é cross-cutting a todos.

Padrões estratégicos utilizados:
- **Anti-Corruption Layer (ACL):** para integração com sistemas externos instáveis (GCP Identity Platform, Postmark/SendGrid)
- **Customer/Supplier:** para relações de dependência entre contextos internos
- **Published Language:** para eventos de domínio consumidos por múltiplos contextos
- **Conformist:** onde um contexto depende de dados de infraestrutura (tenant_id, RBAC) sem poder influenciar o modelo

## 2. Artefatos

- [Relações entre Bounded Contexts](./relations.md)
- [Padrões Estratégicos Utilizados](./patterns.md)
- [Diagrama do Context Map](./diagram.md)

## 3. Hierarquia de Contextos

```
CORE
└── Opportunity Pipeline (BC-01)

SUPPORTING
├── Organization Management (BC-08) — habilita todos os demais
├── Account Management (BC-02) — upstream do Pipeline
├── Partner Management (BC-03) — upstream do Pipeline
├── Activity Management (BC-04) — upstream do Pipeline e do Digest
├── Goal & Forecast (BC-05) — upstream do Digest e Reporting
├── Digest (BC-06) — orquestrador do alcance ao vendedor
├── Reporting (BC-07) — read model derivado
├── Data Migration (BC-09) — temporário Fase 1
├── Workflow Automation (BC-10) — Fase 2
└── AI Intelligence (BC-11) — Fase 3

GENERIC
├── Identity & Access (BC-12) — autenticação
├── Tenancy & Branding (BC-13) — isolamento cross-cutting
├── Notification Delivery (BC-14) — envio de e-mail
└── Audit Log (BC-15) — trilha imutável
```

## 4. Pontos a Validar

- DDD-VAL-04: Consolidação de Tenancy & Branding com Organization Management
- DDD-VAL-05: Contrato de eventos do Pipeline para Workflow Automation (Fase 2)
- DDD-VAL-01: Activity Management como BC vs módulo do Opportunity Pipeline
