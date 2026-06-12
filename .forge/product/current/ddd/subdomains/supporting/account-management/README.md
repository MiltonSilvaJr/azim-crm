# Account Management — Supporting Subdomain

## 1. Classificação
- **Tipo:** Supporting Subdomain
- **Código:** SD-02

## 2. Descrição

Representa a gestão de contas (empresas clientes) e contatos associados. Uma conta pode estar vinculada a oportunidades de múltiplas BUs (resolve o problema de duplicatas entre Vellus e Axis). Oferece visão 360° consolidada: oportunidades de todas as BUs visíveis ao usuário, contatos, atividades e histórico cronológico.

## 3. Justificativa da Classificação

Supporting porque: contas e contatos são necessários ao pipeline mas não são o diferencial; dedupe por nome normalizado é uma boa prática implementável com commodity; qualquer CRM tem cadastro de contas. A visão 360° é diferenciada em organização, mas não em complexidade de domínio.

## 4. Capacidades Relacionadas

| Código | Capacidade | Descrição |
|---|---|---|
| CAP-04 | Gestão de Contas e Contatos | CRUD, dedupe, visão 360° |

## 5. Eventos de Negócio Relacionados

| Evento | Descrição |
|---|---|
| account.created | Conta criada no tenant com nome normalizado |
| account.contact_linked | Contato associado a uma conta |
| account.duplicate_detected | Similaridade de nome detectada no momento da criação |

## 6. Regras de Negócio Relevantes

| Regra | Descrição |
|---|---|
| RN-014 | Dedupe por nome normalizado (sem acentos, minúsculas, sem espaços extras) — alerta, não bloqueio |
| RN-025 | PII de contatos (nome, e-mail, celular) não aparece em logs (LGPD) |

## 7. Bounded Contexts Relacionados

| Bounded Context | Relação |
|---|---|
| BC-02 Account Management | Implementa este subdomínio |
| BC-01 Opportunity Pipeline | Downstream — consome contas para vincular a oportunidades |
| BC-04 Activity Management | Downstream — atividades podem ser vinculadas a contas |
| BC-15 Audit Log | Downstream — recebe eventos de escrita |

## 8. Pontos a Validar

- Conta compartilhada entre BUs requer que todos os usuários com acesso a qualquer BU do tenant possam ver a conta — confirmar escopo de visibilidade com produto (RLS por tenant, não por BU, para contas)
