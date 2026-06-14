# data-migration — BC-09 Migração de Planilha

**Módulo temporário da Fase 1.** Responsável pela importação única, controlada e auditável da planilha `Pipeline Vellus.xlsx` para as entidades de domínio do Azim CRM.

- **Bounded Context:** BC-09 — Data Migration (Supporting Subdomain SD-11)
- **Deployable:** `azim-api` (módulo embarcado)
- **Stack:** .NET 10 / C# / EF Core 9.0.6 / Npgsql 9.0.4 / ClosedXML (Infrastructure)
- **ADRs:** ADR-0001 (multi-tenant), ADR-0003 (opportunity_number por tenant)
- **Status:** Implementado — 6 ondas, 28 TASKs, 415 testes verdes

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
- Integração HTTP entre módulos — **o acoplamento é in-process via portas** (DD-001; não há chamadas HTTP entre módulos)
- Persistência após remoção do módulo (módulo temporário — Req 14, DD-009)

---

## Fronteira de Integração — In-Process, Não HTTP (DD-001)

> **IMPORTANTE:** o import invoca os casos de uso dos módulos-alvo **in-process**,
> dentro de um `IUnitOfWork` compartilhado — **não via HTTP**. Chamadas HTTP entre
> módulos teriam transações independentes, impossibilitando rollback atômico (RN-023).
>
> Todos os módulos vivem no mesmo deployable `azim-api` e no mesmo banco Postgres.
> As portas (`IAccountImportPort`, `IPartnerImportPort`, `IOpportunityImportPort`,
> `IActivityImportPort`, `IOpportunityNumberPort`, `IOrganizationReadPort`) são
> implementadas por adaptadores in-process na camada Infrastructure.
>
> Rastreia: DD-001, design §2, §6.4.

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
- **Portas in-process (DD-001):** `IAccountImportPort`, `IPartnerImportPort`, `IOpportunityImportPort`, `IActivityImportPort`, `IOpportunityNumberPort`, `IOrganizationReadPort`
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

- Logs estruturados com `migration_job_id`, `source_sheet`, `source_row_index`, sem PII (TASK-24, RNF 3)
- Métricas: `migration_rows_processed_total`, `migration_rows_failed_total`, `migration_duration_seconds`, `migration_forecast_divergences_total`, `migration_jobs_state_total{state}` (TASK-25, RNF 6)
- Traces: span por etapa (parse, dry-run, import) com `correlation_id` (ActivitySource `DataMigration`)
- Alertas: `services/data-migration/observability/alerts.yaml`

---

## Ondas de Implementação

| Onda | Foco | TASKs | Status |
|------|------|-------|--------|
| Onda 1 | Bootstrap | TASK-01, TASK-02 | Implementado |
| Onda 2 | Domain | TASK-03..TASK-07 | Implementado |
| Onda 3 | Application | TASK-08..TASK-13 | Implementado |
| Onda 4 | Infrastructure | TASK-14..TASK-20 | Implementado |
| Onda 5 | API + Contratos | TASK-21..TASK-23 | Implementado |
| Onda 6 | Hardening | TASK-24..TASK-28 | Implementado |

Total: **28 TASKs**, **7 PBTs**, **415 testes verdes**.

---

## Ciclo de vida (VAL-MIGR-01)

Módulo temporário (Req 14). Após confirmação da migração da Fase 1:

1. Desabilitar flag `migration.import_enabled`
2. Aguardar retenção de 30 dias dos dados de auditoria
3. Executar plano de remoção: `docs/product/modules/data-migration/lifecycle-removal.md`

---

## Troubleshooting

| Sintoma | Causa provável | Ação |
|---------|---------------|------|
| `MIG-ERR-001` | Arquivo não-`.xlsx` | Reenviar no formato correto |
| `MIG-ERR-002` | Colunas ausentes | Ajustar planilha às colunas esperadas |
| `MIG-ERR-006` | Owner faltante | Atribuir owner via triagem |
| `MIG-ERR-007` | Import revertido | Verificar linha indicada por índice e reexecutar |
| `MIG-ERR-009` | Flag desabilitada | Solicitar habilitação ao PlatOp |
| `MIG-ERR-010` | BU/estágio ausente | Configurar BUs/estágios antes do import |

---

## §20 — VAL-MIGR-01: Decisão de Remoção Pós-Fase 1

| Campo | Valor |
|-------|-------|
| Decisão | Remoção do módulo data-migration após confirmação da Fase 1 |
| Data de registro | 2026-06-14 |
| Critérios de ativação | Import concluído + relatório auditado + planilha congelada |
| Plano de remoção | `docs/product/modules/data-migration/lifecycle-removal.md` |
| Rastreabilidade | Req 14, DD-009, VAL-TRD-11, DDD-VAL-03 |
| Responsável | Platform Operator (PlatOp) |
