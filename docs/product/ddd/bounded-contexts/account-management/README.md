# Bounded Context Canvas — Account Management

## 1. Objetivo

Gerenciar contas (empresas clientes) e contatos associados com deduplicação por nome normalizado e visão 360° consolidada por tenant.

## 2. Classificação DDD
- **Tipo:** Supporting Subdomain
- **Subdomínio:** SD-02

## 3. Responsabilidades

- CRUD de contas com dedupe por nome normalizado (alerta, não bloqueio)
- CRUD de contatos vinculados à conta (nome, e-mail, celular, cargo)
- Visão 360°: oportunidades de todas as BUs visíveis ao usuário, contatos, atividades e histórico cronológico
- Conta compartilhada entre BUs sem duplicação (resolve Pag.ai/Ethoca entre Vellus e Axis)

## 4. Fora do Escopo

- Gerenciamento de parceiros (Partner Management)
- Registro de atividades (Activity Management)
- Pipeline de oportunidades (Opportunity Pipeline)

## 5. Linguagem Ubíqua

| Termo | Definição | Observações |
|---|---|---|
| Account | Empresa cliente; pode ser vinculada a oportunidades de múltiplas BUs | Compartilhada no tenant, não duplicada por BU |
| Contact | Pessoa física vinculada a uma conta (nome, e-mail, celular, cargo) | PII protegida por LGPD (RN-025) |
| dedupe | Deduplicação por nome normalizado — alerta antes de criar conta similar | Sem acentos, minúsculas, sem espaços extras |
| visao_360 | Visão consolidada da conta: opps, contatos, atividades e histórico | Filtrada por BUs do usuário |

## 6. Atores e Sistemas Relacionados

| Ator/Sistema | Relação |
|---|---|
| Vendedor (P-01) | Cria e edita contas; acessa visão 360° das suas BUs |
| Gestor de BU (P-02) | Edita contas da sua BU; acessa visão 360° |
| Tenant Admin (P-04) | Acesso total a contas do tenant |
| Opportunity Pipeline | Downstream — consome account_id |
| Activity Management | Downstream — atividades vinculadas a contas |
| Reporting | Downstream — dados de conta em relatórios |
| Audit Log | Downstream — eventos de escrita |

## 7. Agregados e Entidades

| Tipo | Nome | Descrição | Dono |
|---|---|---|---|
| Aggregate | Account | Empresa cliente com dados cadastrais e vínculos | Account Management |
| Entity | Contact | Pessoa física vinculada à conta | Account Management |
| Value Object | NormalizedName | Nome normalizado para dedupe (sem acentos, lowercase) | Account Management |

## 8. Comandos

| Comando | Descrição | Ator |
|---|---|---|
| CreateAccount | Cria conta com verificação de dedupe | Vendedor, Gestor, TAdmin |
| UpdateAccount | Edita dados cadastrais da conta | Vendedor (suas opps), Gestor, TAdmin |
| AddContact | Adiciona contato à conta | Vendedor, Gestor, TAdmin |
| UpdateContact | Edita dados do contato | Vendedor, Gestor, TAdmin |

## 9. Eventos de Domínio

| Evento | Quando | Consumidores |
|---|---|---|
| account.created | Conta criada com sucesso | Audit Log |
| contact.added | Contato adicionado à conta | Audit Log |

## 10. APIs Expostas

| API | Método | Finalidade |
|---|---|---|
| /api/v1/accounts | GET, POST | Listar e criar contas |
| /api/v1/accounts/{id} | GET, PATCH | Ler e editar conta |
| /api/v1/accounts/{id}/contacts | GET, POST | Contatos da conta |
| /api/v1/accounts/{id}/360 | GET | Visão 360° da conta |

## 11. Integrações

| Contexto/Sistema | Tipo | Padrão DDD |
|---|---|---|
| Opportunity Pipeline | Fornece contas para oportunidades | Customer/Supplier (Account = Supplier) |
| Organization Management | Consome tenant_id e bu_id para escopo de visibilidade | Conformist |
| Audit Log | Publica eventos de escrita | Published Language |

## 12. Dados Próprios

| Entidade/Tabela | Finalidade | Retenção |
|---|---|---|
| accounts | Cadastro de contas do tenant | Indefinida |
| contacts | Contatos vinculados às contas | Indefinida; PII sujeita a LGPD |

## 13. Requisitos Não Funcionais Específicos

| Categoria | Requisito |
|---|---|
| Privacidade | PII de contatos (nome, e-mail, celular) não em logs (RN-025; LGPD) |
| Segurança | RLS por tenant_id; visibilidade de conta por BU do usuário |

## 14. Decisões Arquiteturais Relacionadas

| ADR | Decisão |
|---|---|
| DEC-006 | Multi-tenancy pooled + RLS |
| DEC-011 | Valores em centavos inteiros (não aplicável diretamente, mas inherited) |

## 15. Riscos e Pontos de Atenção

- Conta compartilhada entre BUs: escopo de visibilidade por tenant_id, não por bu_id — confirmar RLS
- Política de retenção de PII de contatos (VAL-08) — confirmar com equipe jurídica
