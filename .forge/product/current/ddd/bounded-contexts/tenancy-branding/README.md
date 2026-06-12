# Bounded Context Canvas — Tenancy & Branding

## 1. Objetivo

Provisionar tenants na plataforma (Platform Operator) e gerenciar configurações de white-label (Tenant Admin): slug, logo, favicon, cores primária e secundária, fuso horário IANA.

## 2. Classificação DDD
- **Tipo:** Generic Subdomain
- **Subdomínio:** SD-13

## 3. Responsabilidades

- Provisionamento de novos tenants com slug, branding inicial e usuário admin (Platform Operator)
- Configuração de white-label estrito: logo (PNG/SVG ≤ 1 MB), favicon, cor primária, cor secundária (DEC-004)
- Validação de contraste WCAG AA nas cores (RN-020)
- Slug imutável após definição (RN-019)
- Fuso horário IANA do tenant (usado pelo Digest — DEC-009)
- Geração e distribuição do tenant_id como chave de isolamento RLS

## 4. Linguagem Ubíqua

| Termo | Definição | Observações |
|---|---|---|
| tenant | Empresa cliente do Azim como produto SaaS | Isolado por tenant_id via RLS |
| slug | Identificador único do tenant na URL (app.azim.com.br/{slug}) | Imutável após definição (RN-019) |
| branding | Conjunto de elementos visuais: logo, favicon, cor primária, cor secundária | White-label estrito (DEC-004) |
| horario_digest | Hora local de envio do digest no fuso IANA do tenant | Default 07:00 BRT |

## 5. Integrações

| Contexto/Sistema | Tipo | Padrão DDD |
|---|---|---|
| Organization Management | Downstream — recebe tenant_id para criação de BUs e usuário admin inicial | Customer/Supplier |
| Identity & Access | Downstream — cria tenant de identidade no GCP Identity Platform | Customer/Supplier |
| Todos os BCs | Fornece tenant_id para isolamento cross-cutting (RLS) | Conformist |

## 6. Dados Próprios

| Entidade/Tabela | Finalidade | Retenção |
|---|---|---|
| tenants | Registro de tenants com tenant_id, slug, fuso e metadados | Ciclo de vida do tenant |
| tenant_brandings | Configurações de white-label por tenant | Ciclo de vida do tenant |

## 7. Pontos a Validar

- DDD-VAL-04: Consolidação com Organization Management — decidir antes da Fase 2
