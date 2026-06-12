# Bounded Context Canvas — Audit Log

## 1. Objetivo

Manter a trilha de auditoria imutável (append-only) para toda escrita em entidades de negócio, com mascaramento de PII antes da persistência.

## 2. Classificação DDD
- **Tipo:** Generic Subdomain
- **Subdomínio:** SD-15

## 3. Responsabilidades

- Receber e persistir eventos de escrita de todos os contextos (append-only)
- Garantir imutabilidade: nenhum registro pode ser editado ou excluído por qualquer papel
- Mascarar PII (nome, e-mail, celular de contatos) antes de persistir em delta_json (RN-025)
- Prover visualização da trilha de auditoria para Tenant Admin (escopo do tenant) e Gestor de BU (escopo da BU)

## 4. Linguagem Ubíqua

| Termo | Definição | Observações |
|---|---|---|
| audit_log | Registro imutável de escrita em entidade de negócio | Append-only; sem UPDATE ou DELETE |
| delta_json | Diff JSON com estado anterior e novo da entidade | PII mascarada (RN-025) |
| imutabilidade | Propriedade que impede qualquer edição ou exclusão do registro | Garantida por design e permissão de banco |

## 5. Integrações

| Contexto/Sistema | Tipo | Padrão DDD |
|---|---|---|
| Todos os BCs de escrita | Publicam eventos para o AuditLog | Published Language |

## 6. Dados Próprios

| Entidade/Tabela | Finalidade | Retenção |
|---|---|---|
| audit_logs | Trilha imutável de toda escrita em entidade de negócio | A definir com equipe jurídica (VAL-08) |

## 7. Decisões Arquiteturais Relacionadas

| ADR | Decisão |
|---|---|
| RN-024 | Campos obrigatórios: user_id, entity_type, entity_id, action, delta_json, timestamp, tenant_id |
| RN-025 | PII de contatos mascarada ou omitida antes do log |

## 8. Pontos a Validar

- VAL-08: Política de retenção de dados do AuditLog sob LGPD — confirmar com equipe jurídica
- Implementação: tabela append-only com constraint de banco vs event store vs write-ahead log separado
