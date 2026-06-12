# Bounded Context Canvas — Partner Management

## 1. Objetivo

Gerenciar parceiros comissionados do tenant: cadastro, percentuais default por componente e visão de comissão projetada e consolidada por período.

## 2. Classificação DDD
- **Tipo:** Supporting Subdomain
- **Subdomínio:** SD-03

## 3. Responsabilidades

- CRUD de parceiros com papel tipado e percentuais default (pct_setup, pct_recorrente)
- Visão de comissão projetada (pipeline aberto) e consolidada (oportunidades Ganhas) por período
- Fornecer dados de parceiro para vinculação a oportunidades (canal "Parceiro")

## 4. Fora do Escopo

- Snapshot imutável de comissão (responsabilidade do Opportunity Pipeline — RN-007)
- Acesso do parceiro ao sistema (DEC-012 — fora do MVP)
- Múltiplos parceiros por oportunidade (fora do escopo — roadmap)

## 5. Linguagem Ubíqua

| Termo | Definição | Observações |
|---|---|---|
| Partner | Empresa ou pessoa que origina oportunidades e recebe comissão | Sem login no MVP (DEC-012) |
| pct_setup | Percentual de comissão sobre o componente setup (por padrão do parceiro) | Ex.: 10% sobre valor_setup |
| pct_recorrente | Percentual de comissão sobre o componente recorrente | Ex.: 5% sobre valor_mensal |
| comissao_projetada | Soma das comissões calculadas das oportunidades abertas do parceiro | Snapshot não gerado ainda |
| comissao_consolidada | Soma das comissões dos snapshots imutáveis de oportunidades Ganhas | Baseada em snapshots do Pipeline |

## 6. Atores e Sistemas Relacionados

| Ator/Sistema | Relação |
|---|---|
| Gestor de BU (P-02) | Cria e edita parceiros da sua BU; acessa visão de comissão |
| Tenant Admin (P-04) | Acesso total a parceiros do tenant |
| Opportunity Pipeline | Downstream — consome partner_id e percentuais default ao vincular parceiro |
| Reporting | Downstream — consome dados de comissão para relatório por parceiro |
| Audit Log | Downstream — eventos de escrita |

## 7. Agregados e Entidades

| Tipo | Nome | Descrição | Dono |
|---|---|---|---|
| Aggregate | Partner | Parceiro comissionado com dados e percentuais default | Partner Management |
| Value Object | CommissionDefaults | pct_setup e pct_recorrente default do parceiro | Partner Management |

## 8. Comandos

| Comando | Descrição | Ator |
|---|---|---|
| CreatePartner | Cria parceiro com papel e percentuais default | Gestor de BU, Tenant Admin |
| UpdatePartnerDefaults | Atualiza percentuais default (não afeta snapshots já criados) | Gestor de BU, Tenant Admin |

## 9. Eventos de Domínio

| Evento | Quando | Consumidores |
|---|---|---|
| partner.created | Parceiro criado com sucesso | Audit Log |
| partner.defaults_updated | Percentuais default alterados | Audit Log |

## 10. APIs Expostas

| API | Método | Finalidade |
|---|---|---|
| /api/v1/partners | GET, POST | Listar e criar parceiros |
| /api/v1/partners/{id} | GET, PATCH | Ler e editar parceiro |
| /api/v1/partners/{id}/commission | GET | Visão de comissão projetada e consolidada |

## 11. Integrações

| Contexto/Sistema | Tipo | Padrão DDD |
|---|---|---|
| Opportunity Pipeline | Fornece partner_id e percentuais | Customer/Supplier (Partner = Supplier) |
| Reporting | Fornece dados de comissão para relatórios | Customer/Supplier (Partner = Supplier) |
| Audit Log | Publica eventos de escrita | Published Language |

## 12. Dados Próprios

| Entidade/Tabela | Finalidade | Retenção |
|---|---|---|
| partners | Cadastro de parceiros do tenant | Indefinida |

## 13. Requisitos Não Funcionais Específicos

| Categoria | Requisito |
|---|---|
| Segurança | RLS por tenant_id; parceiro sem acesso ao sistema (DEC-012) |

## 14. Decisões Arquiteturais Relacionadas

| ADR | Decisão |
|---|---|
| DEC-002 | Comissão por componente + snapshot imutável (snapshot gerado no Pipeline, não aqui) |
| DEC-012 | Parceiro sem login no MVP |

## 15. Riscos e Pontos de Atenção

- 11 parceiros da Vellus sem percentuais definidos (VAL-01) — triagem obrigatória na migração
- Percentuais em branco no momento do fechamento como Ganho: comportamento a definir (VAL-07)
