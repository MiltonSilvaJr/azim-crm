# data-migration — BC-09 Migração de Planilha

**Módulo temporário da Fase 1.** Responsável pela importação única, controlada e auditável da planilha `Pipeline Vellus.xlsx` para as entidades de domínio do Azim CRM.

- **Bounded Context:** BC-09 — Data Migration (Supporting Subdomain SD-11)
- **Deployable:** `azim-api` (módulo embarcado)
- **Stack:** .NET 10 / C# / EF Core 9.0.6 / Npgsql 9.0.4 / ClosedXML (Infrastructure)
- **ADRs:** ADR-0001 (multi-tenant), ADR-0003 (opportunity_number por tenant)
- **Status:** Onda 1 — Bootstrap concluída

---

## Objetivo

Importar a planilha `Pipeline Vellus.xlsx` para as entidades `Account`, `Contact`, `Partner`, `Opportunity` e `Activity`, respeitando:

- Atomicidade tudo-ou-nada (RN-023, DD-001)
- Isolamento multi-tenant em profundidade (ADR-0001)
- PII fora dos logs (RNF 3, LGPD)
- Idempotência por `import_key` (DD-003, Req 12)

O processo é sequenciado em cinco etapas: **upload → dry-run → triagem assistida → import transacional → relatório final**.

---

## Responsabilidades

- Receber e validar arquivo `.xlsx` (extensão, estrutura de colunas, tamanho)
- Executar simulação de import sem efeitos colaterais (dry-run)
- Suportar triagem assistida de pendências (owners, estágios, parceiros, dedupe)
- Executar import transacional tudo-ou-nada em uma única transação Postgres
- Emitir `ImportCompleted` via Outbox para audit-log
- Gerar relatório final auditado com instrução de congelamento da planilha

## Não responsabilidades

- Gerenciamento de ciclo de vida de `Account`, `Contact`, `Partner`, `Opportunity`, `Activity` (pertencem aos respectivos bounded contexts)
- Integração HTTP entre módulos (acoplamento in-process — DD-001)
- Persistência após remoção do módulo (módulo temporário — Req 14, DD-009)

---

## Estrutura Clean Architecture

```
DataMigration.Domain          -> sem dependências de infraestrutura
DataMigration.Application     -> Domain, Contracts
DataMigration.Infrastructure  -> Application, Domain, Contracts
DataMigration.Api             -> Application, Infrastructure, Contracts
DataMigration.Contracts       -> sem dependências internas
```

Validada automaticamente por `DataMigration.Architecture.Tests` (NetArchTest) em todo PR.

---

## API REST

Base path: `/api/v1/migrations`

| Método | Path | Autorização | Descrição |
|--------|------|-------------|-----------|
| POST | `/upload` | PlatOp | Upload do `.xlsx` |
| POST | `/{jobId}/dry-run` | PlatOp | Simulação sem efeitos colaterais |
| GET | `/{jobId}/triage` | PlatOp, TenantAdmin | Relatório de triagem |
| POST | `/{jobId}/triage/owners` | TenantAdmin | Atribuição de owners |
| POST | `/{jobId}/triage/stages` | TenantAdmin | Resolução de estágios |
| POST | `/{jobId}/triage/partners` | TenantAdmin | Resolução de percentuais |
| POST | `/{jobId}/triage/dedupe` | TenantAdmin | Decisão de dedupe |
| POST | `/{jobId}/ready` | TenantAdmin | Marca `ready_to_import` |
| POST | `/{jobId}/execute` | PlatOp | Import transacional (requer `confirmation: true`) |
| GET | `/{jobId}/status` | PlatOp, TenantAdmin | Estado e contagens |
| GET | `/{jobId}/report` | TenantAdmin | Relatório final auditado |

Catálogo de erros: `MIG-ERR-001..010` (design §12).

---

## Eventos publicados

| Evento | Canal | Quando |
|--------|-------|--------|
| `migration.import_completed.v1` | Pub/Sub (Outbox) | Import concluído com sucesso |
| `migration.dry_run_completed.v1` | observabilidade | Dry-run concluído |
| `migration.import_failed.v1` | observabilidade | Rollback total executado |

Nenhum evento consumido.

---

## Dependências

- **Banco:** Cloud SQL / PostgreSQL (`southamerica-east1`), compartilhado com `azim-api`
- **Portas in-process:** `IAccountImportPort`, `IPartnerImportPort`, `IOpportunityImportPort`, `IActivityImportPort`, `IOpportunityNumberPort`, `IOrganizationReadPort`
- **ClosedXML:** confinado a `DataMigration.Infrastructure` (DD-002)
- **EF Core 9.0.6 / Npgsql 9.0.4:** em `DataMigration.Infrastructure`

---

## Configuração e variáveis de ambiente

| Variável | Descrição | Obrigatória |
|----------|-----------|-------------|
| `ConnectionStrings__DataMigration` | Connection string Postgres | Sim |
| `FeatureFlags__migration.import_enabled` | Habilita endpoint de execute | Sim |
| `DataMigration__MaxFileSizeBytes` | Tamanho máximo do .xlsx (default: 10 MB) | Não |

---

## Como executar localmente

```bash
# A partir da raiz do monorepo
cd services/data-migration
dotnet build DataMigration.slnx
dotnet test DataMigration.slnx
```

---

## Health checks

Herdados do `azim-api` (liveness/readiness). Sem endpoints próprios.

---

## Observabilidade

- Logs estruturados com `migration_job_id`, `source_sheet`, `source_row_index`, sem PII
- Métricas: `migration_rows_processed_total`, `migration_rows_failed_total`, `migration_duration_seconds`
- Traces: span por etapa (parse, dry-run, import) com `correlation_id`

---

## Ciclo de vida

Módulo temporário (Req 14). Após confirmação da migração da Fase 1:

1. Desabilitar flag `migration.import_enabled`
2. Planejar remoção do módulo e das tabelas `migration_jobs`/`migration_logs` (VAL-MIGR-01)

---

## Troubleshooting

| Sintoma | Causa provável | Ação |
|---------|---------------|------|
| `MIG-ERR-001` | Arquivo não-`.xlsx` | Reenviar no formato correto |
| `MIG-ERR-002` | Colunas ausentes | Ajustar planilha às colunas esperadas |
| `MIG-ERR-006` | Owner faltante | Atribuir owner via triagem |
| `MIG-ERR-007` | Import revertido | Verificar linha indicada e reexecutar |
| `MIG-ERR-009` | Flag desabilitada | Solicitar habilitação ao PlatOp |
| `MIG-ERR-010` | BU/estágio ausente | Configurar BUs/estágios antes do import |

---

*Decisão in-process (DD-001): o import invoca casos de uso dos módulos-alvo dentro de um `IUnitOfWork` compartilhado — não via HTTP — para garantir atomicidade tudo-ou-nada (RN-023).*
