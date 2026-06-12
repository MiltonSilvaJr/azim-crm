# Bounded Context Canvas — Identity & Access

## 1. Objetivo

Gerenciar autenticação e sessão via GCP Identity Platform multi-tenant, com login por e-mail/senha e Google, sessão resolvida pelo slug do tenant e isolamento de identidade por tenant.

## 2. Classificação DDD
- **Tipo:** Generic Subdomain
- **Subdomínio:** SD-12

## 3. Responsabilidades

- Login por e-mail/senha e conta Google via OAuth/OIDC (GCP Identity Platform)
- Resolução de slug → tenant_id no middleware .NET
- Convite com link temporário e expiração configurável (delegado ao Organization Management)
- Recuperação de senha por e-mail (delegado ao Notification Delivery)
- Logout com invalidação de sessão em todos os dispositivos
- Anti-Corruption Layer para proteger o modelo interno do contrato do GCP Identity Platform

## 4. Linguagem Ubíqua

| Termo | Definição | Observações |
|---|---|---|
| session_token | Token de sessão autenticada com tenant_id e user_id incorporados | Renovável; expiração configurável |
| slug | Identificador do tenant na URL canônica (app.azim.com.br/{slug}) | Imutável após definição (RN-019) |
| tenant_identity | Tenant de identidade no GCP Identity Platform correspondente ao tenant Azim | Um para um |

## 5. Integrações

| Contexto/Sistema | Tipo | Padrão DDD |
|---|---|---|
| GCP Identity Platform | Provider externo de identidade | Anti-Corruption Layer |
| Organization Management | Downstream — carrega papéis e BUs após autenticação | Customer/Supplier |
| Notification Delivery | Downstream — e-mails transacionais de recuperação de senha | Customer/Supplier |

## 6. Dados Próprios

Nenhum — identidades gerenciadas pelo GCP Identity Platform. Sessions podem ser cacheadas no Redis (Memorystore).

## 7. Decisões Arquiteturais Relacionadas

| ADR | Decisão |
|---|---|
| DEC-006 | Multi-tenancy: slug → tenant_id resolvido no middleware antes de qualquer lógica de negócio |

## 8. Pontos a Validar

- PRE-01: GCP Identity Platform disponível em southamerica-east1 sem limitações
- VAL-09: Expiração do link de convite (72h = inferência — confirmar)
