# Tenancy & Branding — Generic Subdomain

## 1. Classificação
- **Tipo:** Generic Subdomain
- **Código:** SD-13

## 2. Descrição

Representa o provisionamento de tenants pela plataforma (Platform Operator) e a configuração de white-label pelo Tenant Admin: slug imutável, logo, favicon, cor primária, cor secundária, fuso horário IANA. O white-label é estrito (DEC-004): apenas 5 elementos configuráveis — sem CSS, fontes ou layout customizável. O tenant_id gerado aqui é a chave de isolamento cross-cutting usada por todos os demais contextos via RLS.

## 3. Justificativa da Classificação

Generic porque: provisionamento multi-tenant e white-label são capacidades de infraestrutura de plataforma SaaS; sem diferencial de domínio; implementável com padrões conhecidos (pool + RLS).

## 4. Capacidades Relacionadas

| Código | Capacidade | Descrição |
|---|---|---|
| CAP-15 | Tenancy e Branding | Provisionamento, slug imutável, white-label estrito |

## 5. Eventos de Negócio Relacionados

| Evento | Descrição |
|---|---|
| tenant.provisioned | Novo tenant criado com slug, tenant_id e branding inicial |
| tenant.branding_changed | Logo, favicon ou cores atualizados pelo Tenant Admin |

## 6. Regras de Negócio Relevantes

| Regra | Descrição |
|---|---|
| RN-019 | Slug imutável após definição e confirmação |
| RN-020 | Logo: PNG/SVG ≤ 1 MB; cores validadas por contraste WCAG AA 4.5:1 |
| DEC-004 | White-label estrito: apenas logo, favicon, cor primária, cor secundária, slug |
| RN-012 | tenant_id cross-cutting em toda entidade de negócio (RLS) |

## 7. Bounded Contexts Relacionados

| Bounded Context | Relação |
|---|---|
| BC-13 Tenancy & Branding | Implementa este subdomínio |
| BC-08 Organization Management | Downstream — BUs e usuários pertencem a um tenant provisionado |
| Todos os BCs | Downstream — consomem tenant_id para isolamento RLS |

## 8. Pontos a Validar

- DDD-VAL-04: Consolidação com Organization Management — ambos configurados pelo Tenant Admin; considerar BC único de Tenant Administration
