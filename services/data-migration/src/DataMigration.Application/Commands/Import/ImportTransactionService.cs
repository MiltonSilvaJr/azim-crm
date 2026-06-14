using DataMigration.Application.DTOs;
using DataMigration.Application.Ports;
using DataMigration.Domain.Aggregates;
using DataMigration.Domain.ValueObjects;

namespace DataMigration.Application.Commands.Import;

/// <summary>
/// Serviço de aplicação que executa o pipeline de import dentro de uma transação única.
///
/// Ordem determinística (design §5.3):
///   1. Accounts (com dedupe) + Contacts
///   2. Partners
///   3. Opportunities (com número preservado/gerado)
///   4. Activities (aba Ações Comerciais)
///
/// Cada linha gera um <see cref="MigrationLogEntry"/> no job.
///
/// Rastreia: design §5.3, Req 6, DD-001, DD-003, RN-023, TASK-11 ST-03.
/// </summary>
public sealed class ImportTransactionService
{
    private readonly IAccountImportPort _accountPort;
    private readonly IPartnerImportPort _partnerPort;
    private readonly IOpportunityImportPort _opportunityPort;
    private readonly IActivityImportPort _activityPort;
    private readonly IOpportunityNumberPort _numberPort;
    private readonly IOrganizationReadPort _orgPort;
    private readonly IClock _clock;

    /// <summary>Cria o serviço com as portas injetadas.</summary>
    public ImportTransactionService(
        IAccountImportPort accountPort,
        IPartnerImportPort partnerPort,
        IOpportunityImportPort opportunityPort,
        IActivityImportPort activityPort,
        IOpportunityNumberPort numberPort,
        IOrganizationReadPort orgPort,
        IClock clock)
    {
        _accountPort = accountPort;
        _partnerPort = partnerPort;
        _opportunityPort = opportunityPort;
        _activityPort = activityPort;
        _numberPort = numberPort;
        _orgPort = orgPort;
        _clock = clock;
    }

    /// <summary>
    /// Executa o import de todas as linhas em ordem determinística.
    /// Deve ser chamado dentro de uma transação ativa (IUnitOfWork.BeginAsync).
    ///
    /// Retorna o <see cref="ImportReportDto"/> com contagens por entidade.
    /// Lança exceção em qualquer falha — cabe ao handler fazer rollback.
    /// </summary>
    public async Task<ImportReportDto> ExecuteAsync(
        MigrationJob job,
        IReadOnlyList<SourceRow> allRows,
        TriageResolutionDto resolution,
        CancellationToken cancellationToken)
    {
        var pipelineRows = allRows
            .Where(r => string.Equals(r.SheetName, "pipeline", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var acoesRows = allRows
            .Where(r => string.Equals(r.SheetName, "acoes_comerciais", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Mapa rowIndex → ownerId da triagem
        var ownerMap = resolution.OwnerAssignments
            .ToDictionary(a => a.RowIndex, a => a.OwnerId);

        // Mapa rowIndex → stageId da triagem
        var stageMap = resolution.StageResolutions
            .ToDictionary(s => s.RowIndex, s => s.StageId);

        // Contagens
        int accountsCreated = 0, partnersCreated = 0, opportunitiesCreated = 0, activitiesCreated = 0;

        // Mapa accountName → accountId (para evitar recriação dentro do loop)
        var accountCache = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        // Mapa de oportunidade criada para resolução de atividades
        var opportunityNumberToId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        var now = _clock.UtcNow;

        // === Passo 1: Accounts (dedupe por NormalizedName) ===
        foreach (var row in pipelineRows)
        {
            var conta = GetCell(row, "Conta") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(conta))
            {
                continue;
            }

            var normalizedName = NormalizedName.From(conta);
            if (accountCache.ContainsKey(normalizedName.Value))
            {
                continue; // Já criado neste import (dedupe)
            }

            var importKey = BuildImportKey(job.TenantId, "account", row.SheetName, row.RowIndex, normalizedName.Value);
            var accountResult = await _accountPort.CreateOrGetAsync(
                new AccountImportRequest(normalizedName, importKey, job.TenantId),
                cancellationToken);

            accountCache[normalizedName.Value] = accountResult.AccountId;

            if (accountResult.IsNew)
            {
                accountsCreated++;
            }
        }

        // === Passo 2: Partners ===
        var partnerCache = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in pipelineRows)
        {
            var parceiro = GetCell(row, "Parceiro") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(parceiro) || parceiro.Trim() == "-")
            {
                continue; // Sem parceiro
            }

            if (partnerCache.ContainsKey(parceiro))
            {
                continue;
            }

            var importKey = BuildImportKey(job.TenantId, "partner", row.SheetName, row.RowIndex, parceiro);
            var partnerResult = await _partnerPort.CreateOrGetAsync(
                new PartnerImportRequest(parceiro, importKey, job.TenantId),
                cancellationToken);

            partnerCache[parceiro] = partnerResult.PartnerId;

            if (partnerResult.IsNew)
            {
                partnersCreated++;
            }
        }

        // === Passo 3: Opportunities ===
        foreach (var row in pipelineRows)
        {
            var conta = GetCell(row, "Conta") ?? string.Empty;
            if (!accountCache.TryGetValue(NormalizedName.From(conta).Value, out var accountId))
            {
                accountId = Guid.Empty;
            }

            // Owner da triagem
            if (!ownerMap.TryGetValue(row.RowIndex, out var ownerId))
            {
                ownerId = Guid.Empty; // Fallback (não deve ocorrer — OwnerRequiredSpecification já validou)
            }

            // Estágio: triagem ou fallback "Lead"
            Guid stageId;
            if (!stageMap.TryGetValue(row.RowIndex, out stageId))
            {
                stageId = await _orgPort.GetStageIdByNameAsync("Lead", job.TenantId, cancellationToken)
                    ?? Guid.Empty;
            }

            // Número de oportunidade: preservar ou alocar
            var numeroRaw = GetCell(row, "Nº Oportunidade") ?? string.Empty;
            OpportunityNumber oppNumber;
            if (!string.IsNullOrWhiteSpace(numeroRaw) && OpportunityNumber.IsValid(numeroRaw))
            {
                oppNumber = OpportunityNumber.Parse(numeroRaw)!;
            }
            else
            {
                oppNumber = await _numberPort.AllocateNextAsync(job.TenantId, cancellationToken);
            }

            // Parceiro
            Guid? partnerId = null;
            var parceiro = GetCell(row, "Parceiro") ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(parceiro) && parceiro.Trim() != "-"
                && partnerCache.TryGetValue(parceiro, out var pid))
            {
                partnerId = pid;
            }

            // Valores monetários (centavos)
            long.TryParse(GetCell(row, "Valor Setup"), out var valorSetup);
            long.TryParse(GetCell(row, "Valor Mensal"), out var valorMensal);
            int.TryParse(GetCell(row, "Meses"), out var meses);
            long.TryParse(GetCell(row, "Forecast (R$)"), out var forecastPlanilha);
            var valorTotal = valorSetup + (valorMensal * meses);

            // Título (fallback para "Oportunidade — {conta}")
            var titulo = GetCell(row, "Título");
            if (string.IsNullOrWhiteSpace(titulo))
            {
                titulo = $"Oportunidade — {conta}";
            }

            var importKey = BuildImportKey(job.TenantId, "opportunity", row.SheetName, row.RowIndex, oppNumber.Value);

            var oppResult = await _opportunityPort.CreateOrUpdateAsync(
                new OpportunityImportRequest(
                    OpportunityNumber: oppNumber,
                    Title: titulo,
                    AccountId: accountId,
                    OwnerId: ownerId,
                    StageId: stageId,
                    PartnerId: partnerId,
                    ValorSetupCents: valorSetup,
                    ValorMensalCents: valorMensal,
                    Meses: meses,
                    ForecastPonderadoCents: forecastPlanilha,
                    CloseDate: null,
                    ImportKey: importKey,
                    TenantId: job.TenantId),
                cancellationToken);

            opportunityNumberToId[oppNumber.Value] = oppResult.OpportunityId;

            if (oppResult.IsNew)
            {
                opportunitiesCreated++;
            }

            job.AddLogEntry(
                sourceSheet: row.SheetName,
                sourceRowIndex: row.RowIndex,
                status: MigrationLogStatus.Ok,
                message: $"Linha {row.RowIndex}: oportunidade {oppNumber.Value} importada.",
                importKey: importKey,
                at: now);
        }

        // === Passo 4: Activities (aba Ações Comerciais) ===
        foreach (var row in acoesRows)
        {
            var numeroOpp = GetCell(row, "Nº Oportunidade") ?? string.Empty;
            var importKey = BuildImportKey(job.TenantId, "activity", row.SheetName, row.RowIndex, numeroOpp);

            if (!opportunityNumberToId.TryGetValue(numeroOpp, out var oppId))
            {
                // Atividade sem oportunidade correspondente: gera aviso, não aborta (Req 10.4)
                job.AddLogEntry(
                    sourceSheet: row.SheetName,
                    sourceRowIndex: row.RowIndex,
                    status: MigrationLogStatus.Aviso,
                    message: $"Linha {row.RowIndex}: oportunidade '{numeroOpp}' não encontrada; atividade ignorada.",
                    importKey: importKey,
                    at: now);
                continue;
            }

            var actResult = await _activityPort.CreateOrUpdateAsync(
                new ActivityImportRequest(
                    OpportunityId: oppId,
                    Type: GetCell(row, "Ação") ?? "Genérica",
                    Description: $"Atividade importada da linha {row.RowIndex}",
                    ActivityDate: null,
                    OwnerId: Guid.Empty, // resolvido pelo OwnerTypoMappingPolicy (Onda 4)
                    ImportKey: importKey,
                    TenantId: job.TenantId),
                cancellationToken);

            if (actResult.IsNew)
            {
                activitiesCreated++;
            }

            job.AddLogEntry(
                sourceSheet: row.SheetName,
                sourceRowIndex: row.RowIndex,
                status: MigrationLogStatus.Ok,
                message: $"Linha {row.RowIndex}: atividade importada.",
                importKey: importKey,
                at: now);
        }

        return new ImportReportDto
        {
            JobId = job.Id,
            Counts = new ImportEntityCounts(
                Accounts: accountsCreated,
                Contacts: 0,  // vinculados via AccountImportPort (Onda 4)
                Partners: partnersCreated,
                Opportunities: opportunitiesCreated,
                Activities: activitiesCreated),
            FlagsResolved = resolution.OwnerAssignments.Count
                + resolution.StageResolutions.Count
                + resolution.PartnerPctResolutions.Count
                + resolution.DedupeDecisions.Count,
            ForecastDivergences = 0,  // calculado pelo TriageReport do dry-run
            CompletedAt = now,
            SourceFileHash = job.SourceFileHash,
        };
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static string GetCell(SourceRow row, string column) =>
        row.Cells.TryGetValue(column, out var v) ? v ?? string.Empty : string.Empty;

    /// <summary>
    /// Gera a chave de idempotência por linha (DD-003).
    /// hash(tenantId, entity, sourceSheet, sourceRowIndex, discriminator)
    /// </summary>
    private static string BuildImportKey(
        Guid tenantId,
        string entity,
        string sourceSheet,
        int rowIndex,
        string discriminator) =>
        $"{tenantId}:{entity}:{sourceSheet}:{rowIndex}:{discriminator}";
}
