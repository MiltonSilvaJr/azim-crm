# Plano de Remoção Pós-Fase 1 — Módulo data-migration (BC-09)

- Versão: 0.1.0
- Data: 2026-06-14
- Status: Planejado (pré-remoção)
- Rastreabilidade: Req 14, DD-009, VAL-MIGR-01, VAL-TRD-11, DDD-VAL-03

## 1. Contexto

O módulo `data-migration` (BC-09, Supporting Subdomain SD-11) é **temporário**
pela natureza da Fase 1: realizar a importação única e controlada da planilha
`Pipeline Vellus.xlsx` para as entidades de domínio do Azim CRM.

Após a confirmação do sucesso da migração, o módulo deve ser removido para
manter higiene arquitetural e reduzir a superfície de ataque (DD-009, Req 14.1).

**Princípio:** o acoplamento é unidirecional — o módulo conhece os módulos-alvo
(via portas in-process), mas os módulos-alvo **não conhecem** o data-migration.
A remoção não deve deixar dependências ativas nos módulos de domínio (Req 14.2).

## 2. Pré-condições para Remoção

Antes de iniciar qualquer etapa de remoção, confirmar:

- [ ] Import da Fase 1 concluído com sucesso (`status = completed`)
- [ ] `ImportReport` gerado, revisado e arquivado
- [ ] `ImportCompleted` publicado no Outbox e consumido pelo audit-log (append-only)
- [ ] Planilha `Pipeline Vellus.xlsx` congelada no OneDrive (leitura apenas + banner)
- [ ] Todos os dados validados no CRM pela equipe Vellus
- [ ] Feature flag `migration.import_enabled` desabilitada

## 3. Checklist de Remoção (sequência obrigatória)

### Etapa 1 — Desabilitar a feature flag

```yaml
# appsettings.json ou variável de ambiente
migration:
  import_enabled: false
```

- Ação: definir `migration.import_enabled = false` em produção
- Efeito: endpoint de import passa a retornar `MIG-ERR-009` (403)
- Validação: chamar `POST /api/v1/migrations/{jobId}/execute` deve retornar 403

### Etapa 2 — Confirmar zero consumidores do módulo

Executar verificação de arquitetura:

```bash
# Valida que nenhum módulo de domínio referencia DataMigration.*
dotnet test tests/DataMigration.Architecture.Tests --filter "FullyQualifiedName~ReverseDepend"
```

Validar manualmente que nenhum módulo de domínio (accounts, opportunities, etc.)
importa namespace `DataMigration.*` no código de produção.

### Etapa 3 — Retenção de dados de auditoria (30 dias)

As tabelas `migration_jobs` e `migration_logs` devem ser mantidas por **30 dias**
após o import bem-sucedido (design §7, TRD §retenção) para auditoria e troubleshooting.

- Data prevista de drop: 30 dias após `finished_at` do job
- Responsável: Platform Operator (PlatOp)
- Evidência: `ImportReport` arquivado em `docs/product/modules/data-migration/`

### Etapa 4 — Drop das tabelas de migração

Criar migration EF Core de drop (executada como Cloud Run Job pré-tráfego):

```sql
-- Migration de drop (executar apenas após retenção de 30 dias)
DROP TABLE IF EXISTS migration_logs CASCADE;
DROP TABLE IF EXISTS migration_jobs CASCADE;
-- outbox_events do módulo: verificar se relay já processou todos os eventos
-- antes de remover registros específicos do módulo.
DELETE FROM outbox_events WHERE event_type IN (
    'migration.import_completed.v1',
    'migration.dry_run_completed.v1',
    'migration.import_failed.v1'
);
```

### Etapa 5 — Remover projetos da solution

```bash
# Remover projetos de produção
dotnet sln DataMigration.slnx remove src/DataMigration.Domain/DataMigration.Domain.csproj
dotnet sln DataMigration.slnx remove src/DataMigration.Application/DataMigration.Application.csproj
dotnet sln DataMigration.slnx remove src/DataMigration.Infrastructure/DataMigration.Infrastructure.csproj
dotnet sln DataMigration.slnx remove src/DataMigration.Api/DataMigration.Api.csproj
dotnet sln DataMigration.slnx remove src/DataMigration.Contracts/DataMigration.Contracts.csproj

# Remover projetos de teste
dotnet sln DataMigration.slnx remove tests/DataMigration.Domain.Tests/...
dotnet sln DataMigration.slnx remove tests/DataMigration.Application.Tests/...
dotnet sln DataMigration.slnx remove tests/DataMigration.Infrastructure.Tests/...
dotnet sln DataMigration.slnx remove tests/DataMigration.Api.Tests/...
dotnet sln DataMigration.slnx remove tests/DataMigration.Architecture.Tests/...

# Remover pastas físicas
rm -rf services/data-migration/src/DataMigration.*
rm -rf services/data-migration/tests/DataMigration.*
```

### Etapa 6 — Remover referências ao módulo no host

Em `DataMigration.Api/Program.cs` (ou no host principal do `azim-api`):
- Remover `builder.Services.AddDataMigrationInfrastructure(...)`.
- Remover rotas do `MigrationController`.
- Verificar que nenhum outro `Program.cs` referencia o módulo.

### Etapa 7 — Arquivar documentação

- Mover `docs/product/modules/data-migration/` para `docs/product/archive/data-migration/`
- Registrar data de remoção e hash do commit de drop no `CHANGELOG.md` do serviço

## 4. Critérios de Encerramento da Remoção (VAL-MIGR-01)

| Critério | Verificado em |
|---------|---------------|
| `migration.import_enabled = false` em produção | Etapa 1 |
| Zero dependências reversas confirmadas | Etapa 2 |
| Dados de auditoria retidos por 30 dias | Etapa 3 |
| Tabelas `migration_*` dropadas | Etapa 4 |
| Projetos removidos da solution | Etapa 5 |
| Referências removidas do host | Etapa 6 |
| Documentação arquivada | Etapa 7 |
| CI verde após remoção | Pós-Etapa 7 |

## 5. Riscos

| Risco | Mitigação |
|-------|-----------|
| Drop de tabela antes da retenção obrigatória (30 dias) | Bloquear Etapa 4 com data explícita no checklist |
| Referência residual em módulo de domínio | Teste de arquitetura reverso (TASK-27, Regra F) executado em CI |
| Perda de evidência de auditoria | `ImportCompleted` já no audit-log append-only antes do drop |
| Flag desabilitada antes da confirmação do import | Pré-condições obrigatórias na Seção 2 |

## 6. Aprovação (VAL-MIGR-01)

| Campo | Valor |
|-------|-------|
| Decisão | Remoção do módulo após confirmação da Fase 1 |
| Data de decisão | 2026-06-14 |
| Critérios de ativação | Todas as pré-condições da Seção 2 atendidas |
| Responsável pela remoção | Platform Operator (PlatOp) |
| Rastreabilidade | Req 14, DD-009, VAL-TRD-11, DDD-VAL-03 |
