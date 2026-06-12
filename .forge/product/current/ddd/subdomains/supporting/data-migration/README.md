# Data Migration — Supporting Subdomain

## 1. Classificação
- **Tipo:** Supporting Subdomain
- **Código:** SD-11
- **Fase:** 1 (capacidade temporária — pós-migração pode ser arquivada)

## 2. Descrição

Representa a capacidade de importação da planilha Pipeline Vellus.xlsx para o Azim. Fluxo: upload do arquivo → dry-run (relatório de flags sem efeitos colaterais) → triagem assistida (atribuição de owners, definição de estágios e percentuais de comissão) → import transacional (rollback total em falha) → congelamento da planilha (read-only no OneDrive). Capacidade temporária de Fase 1 que não deve poluir o modelo do Pipeline.

## 3. Justificativa da Classificação

Supporting porque: a migração é obrigatória para o go-live da Fase 1 (DEC-010) e tem lógica própria (dry-run, triagem, transacionalidade), mas é uma capacidade temporária e operacional — não um diferencial de produto sustentado.

## 4. Capacidades Relacionadas

| Código | Capacidade | Descrição |
|---|---|---|
| CAP-13 | Migração de Planilha | Dry-run, triagem assistida, import transacional |

## 5. Eventos de Negócio Relacionados

| Evento | Descrição |
|---|---|
| migration.dry_run_completed | Dry-run executado; relatório gerado com flags |
| migration.import_completed | Import transacional concluído com sucesso |
| migration.import_failed | Import falhou; rollback total executado |

## 6. Regras de Negócio Relevantes

| Regra | Descrição |
|---|---|
| RN-023 | Import é transação única; qualquer falha = rollback total; sem dados parciais |
| FRD-migration-01 | Dry-run sem efeitos colaterais no banco |
| DEC-010 | Import da planilha é requisito obrigatório da Fase 1 |

## 7. Bounded Contexts Relacionados

| Bounded Context | Relação |
|---|---|
| BC-09 Data Migration | Implementa este subdomínio |
| BC-01 Opportunity Pipeline | Downstream — importa oportunidades para o modelo do Pipeline após import |
| BC-02 Account Management | Downstream — importa contas |
| BC-03 Partner Management | Downstream — importa parceiros e vinculações |
| BC-08 Organization Management | Upstream — BUs e estágios devem estar configurados antes do import (DEP-04) |

## 8. Pontos a Validar

- DDD-VAL-03: Se este BC deve ser removido após a Fase 1 ou mantido para migrações futuras de outros tenants
- VAL-10: Import parcial por BU — aumenta complexidade mas permite validação incremental
