# Audit Log — Generic Subdomain

## 1. Classificação
- **Tipo:** Generic Subdomain
- **Código:** SD-15

## 2. Descrição

Representa a trilha de auditoria imutável para toda escrita em entidades de negócio. Todo evento de criação, edição ou exclusão em oportunidade, conta, contato, atividade, parceiro e meta gera um registro no AuditLog com: user_id, entity_type, entity_id, action, delta_json, timestamp e tenant_id. O registro é append-only e não pode ser editado ou excluído por nenhum usuário.

## 3. Justificativa da Classificação

Generic porque: auditoria imutável é um padrão de compliance implementável com append-only log; sem lógica de negócio proprietária; pode ser implementado como tabela de banco com trigger ou como event store simplificado. Classificado como Generic pelo spec aprovado.

## 4. Capacidades Relacionadas

| Código | Capacidade | Descrição |
|---|---|---|
| CAP-17 | Trilha de Auditoria | Registro imutável cross-cutting de toda escrita em entidade de negócio |

## 5. Regras de Negócio Relevantes

| Regra | Descrição |
|---|---|
| RN-024 | AuditLog imutável: user_id, entity_type, entity_id, action, delta_json, timestamp, tenant_id |
| RN-025 | PII de contatos não aparece em logs (LGPD) — mascaramento obrigatório antes do log |

## 6. Bounded Contexts Relacionados

| Bounded Context | Relação |
|---|---|
| BC-15 Audit Log | Implementa este subdomínio |
| Todos os BCs de escrita | Upstream — publicam eventos de escrita para o AuditLog |

## 7. Pontos a Validar

- Política de retenção de AuditLog: VAL-08 (base legal LGPD) — confirmar com equipe jurídica antes do go-live
- PII em delta_json: o campo deve mascarar nome, e-mail e celular de contatos antes de persistir (RN-025)
