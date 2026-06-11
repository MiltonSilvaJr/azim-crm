# Bounded Context Canvas — Data Migration

## 1. Objetivo

Importar a planilha Pipeline Vellus.xlsx para o Azim de forma transacional, com dry-run, triagem assistida e rollback total em falha. Capacidade temporária da Fase 1.

## 2. Classificação DDD
- **Tipo:** Supporting Subdomain
- **Subdomínio:** SD-11
- **Fase:** 1 (temporário)

## 3. Responsabilidades

- Executar dry-run: validar estrutura e conteúdo da planilha sem efeitos colaterais
- Relatório de flags: 64 opps sem owner, 24 com parceiro sem %, typos, dedupes detectadas
- Interface de triagem assistida: atribuição de owners, estágios e percentuais de comissão
- Import transacional: rollback total em qualquer falha (RN-023)
- Emitir relatório final auditado pós-import

## 4. Linguagem Ubíqua

| Termo | Definição | Observações |
|---|---|---|
| dry_run | Validação sem escrita no banco; retorna relatório de flags | Sem efeitos colaterais |
| triagem_assistida | Interface de preenchimento de campos obrigatórios ausentes | Owners, estágios, percentuais |
| import_transacional | Import em uma única transação; qualquer falha = rollback total | RN-023 |
| congelamento | Planilha marcada como read-only com banner para o Azim | Pós-import bem-sucedido |

## 5. Integrações

| Contexto/Sistema | Tipo | Padrão DDD |
|---|---|---|
| Opportunity Pipeline | Escreve oportunidades importadas após import bem-sucedido | Customer/Supplier |
| Account Management | Escreve contas importadas | Customer/Supplier |
| Partner Management | Escreve parceiros importados | Customer/Supplier |
| Organization Management | Upstream — BUs e estágios devem existir antes do import (DEP-04) | Conformist |

## 6. Dados Próprios

| Entidade/Tabela | Finalidade | Retenção |
|---|---|---|
| migration_jobs | Histórico de execuções de import | Até remoção do BC (pós-Fase 1) |
| migration_logs | Log detalhado por linha da planilha | Até remoção do BC |

## 7. Riscos e Pontos de Atenção

- DDD-VAL-03: Remover este BC após a Fase 1 ou evoluir para ferramenta de onboarding de tenants
- VAL-02: 64 opps sem owner — triagem deve permitir atribuição em massa por BU
- VAL-10: Import parcial por BU aumenta complexidade; confirmar viabilidade com engenharia
