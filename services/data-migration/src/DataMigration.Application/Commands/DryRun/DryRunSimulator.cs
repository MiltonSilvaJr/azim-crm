using DataMigration.Application.DTOs;
using DataMigration.Domain.Policies;
using DataMigration.Domain.ValueObjects;

namespace DataMigration.Application.Commands.DryRun;

/// <summary>
/// Serviço de aplicação que simula o pipeline do import sem persistência.
///
/// Executa: CanonicalRowMapper → policies → coleta de contagens e flags.
/// Roda dentro de uma transação rollback-only (IUnitOfWork.BeginRollbackOnlyAsync).
///
/// Rastreia: design §5.3 (RunDryRunHandler), TASK-09 ST-03.
/// </summary>
public static class DryRunSimulator
{
    /// <summary>
    /// Simula o processamento de todas as linhas e produz o <see cref="TriageReportDto"/>.
    /// Sem efeito colateral (transação externa marcada rollback-only).
    ///
    /// Rastreia: design §5.3, Req 2, PBT-04.
    /// </summary>
    public static TriageReportDto Simulate(IReadOnlyList<SourceRow> allRows)
    {
        var pipelineRows = allRows
            .Where(r => string.Equals(r.SheetName, "pipeline", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var acoesRows = allRows
            .Where(r => string.Equals(r.SheetName, "acoes_comerciais", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var flags = new List<TriageFlag>();
        var forecastDivergences = new List<ForecastDivergenceEntry>();
        var buCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var normalizedNames = new List<NormalizedName>();

        foreach (var row in pipelineRows)
        {
            ProcessPipelineRow(row, flags, forecastDivergences, buCounts, normalizedNames);
        }

        // Dedupe: detecta grupos com mesmo NormalizedName (AccountDedupePolicy)
        var policy = new AccountDedupePolicy();
        var duplicateGroups = policy.FindDuplicateCandidates(normalizedNames);

        // Marca a primeira linha de cada grupo como dedupe candidate
        // (associação por índice via normalizedNames paralelo a pipelineRows)
        var nameToFirstRowIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < pipelineRows.Count; i++)
        {
            var conta = GetCell(pipelineRows[i], "Conta");
            if (string.IsNullOrWhiteSpace(conta))
            {
                continue;
            }

            var normalized = conta.Trim().ToLowerInvariant();
            nameToFirstRowIndex.TryAdd(normalized, pipelineRows[i].RowIndex);
        }

        var dedupePairs = new List<DedupeCandidatePair>();
        foreach (var group in duplicateGroups)
        {
            var rowIndicesInGroup = pipelineRows
                .Where(r =>
                {
                    var c = GetCell(r, "Conta");
                    return !string.IsNullOrWhiteSpace(c)
                        && c.Trim().ToLowerInvariant() == group.Key;
                })
                .Select(r => r.RowIndex)
                .ToList();

            for (int i = 0; i < rowIndicesInGroup.Count - 1; i++)
            {
                dedupePairs.Add(new DedupeCandidatePair(
                    rowIndicesInGroup[i],
                    rowIndicesInGroup[i + 1],
                    group.Key));
                flags.Add(TriageFlag.ForRow(TriageFlagType.DedupeCandidate, rowIndicesInGroup[i]));
            }
        }

        // Contagem de contas distintas pós-dedupe (PBT-04)
        var distinctAccounts = normalizedNames
            .Select(n => n.Value)
            .Distinct(StringComparer.Ordinal)
            .Count();

        var ownerMissing = flags.Count(f => f.FlagType == TriageFlagType.OwnerMissing);
        var stageMissing = flags.Count(f => f.FlagType == TriageFlagType.StageMissing);
        var partnerPctMissing = flags.Count(f => f.FlagType == TriageFlagType.PartnerPctMissing);
        var typoCounts = flags.Count(f => f.FlagType == TriageFlagType.Typo);

        var buDistribution = buCounts
            .Select(kv => new BuDistributionEntry(kv.Key, kv.Value))
            .OrderByDescending(e => e.Count)
            .ToList();

        return new TriageReportDto
        {
            TotalOpportunities = pipelineRows.Count,
            TotalAccounts = distinctAccounts,
            TotalActivities = acoesRows.Count,
            ByBu = buDistribution,
            OwnerMissingCount = ownerMissing,
            StageMissingCount = stageMissing,
            PartnerPctMissingCount = partnerPctMissing,
            DedupeCandidateCount = dedupePairs.Count,
            TypoCount = typoCounts,
            Flags = flags.AsReadOnly(),
            DedupeCandidates = dedupePairs.AsReadOnly(),
            ForecastDivergences = forecastDivergences.AsReadOnly(),
        };
    }

    // =========================================================================
    // Processamento de linha Pipeline
    // =========================================================================

    private static void ProcessPipelineRow(
        SourceRow row,
        List<TriageFlag> flags,
        List<ForecastDivergenceEntry> forecastDivergences,
        Dictionary<string, int> buCounts,
        List<NormalizedName> normalizedNames)
    {
        var idx = row.RowIndex;

        // BU (trim)
        var buRaw = GetCell(row, "BU");
        var bu = buRaw?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(bu))
        {
            buCounts[bu] = buCounts.GetValueOrDefault(bu) + 1;
        }

        // Conta (NormalizedName para dedupe — PBT-04)
        var contaRaw = GetCell(row, "Conta");
        if (!string.IsNullOrWhiteSpace(contaRaw))
        {
            normalizedNames.Add(NormalizedName.From(contaRaw));
        }

        // Owner — flag bloqueante se vazio
        var responsavel = GetCell(row, "Responsável");
        if (string.IsNullOrWhiteSpace(responsavel))
        {
            flags.Add(TriageFlag.ForRow(TriageFlagType.OwnerMissing, idx));
        }

        // Estágio — StageFallbackPolicy: não bloqueante, aplica "Lead"
        var estagio = GetCell(row, "Estágio");
        if (string.IsNullOrWhiteSpace(estagio))
        {
            flags.Add(TriageFlag.ForRow(TriageFlagType.StageMissing, idx));
        }

        // Parceiro — PartnerPctPendingPolicy: "-" = sem parceiro; vazio = sem %
        var parceiro = GetCell(row, "Parceiro");
        if (!string.IsNullOrWhiteSpace(parceiro) && parceiro.Trim() != "-")
        {
            // Tem parceiro mas sem percentual (percentuais não são importados — Req 3.4)
            flags.Add(TriageFlag.ForRow(TriageFlagType.PartnerPctMissing, idx));
        }

        // Divergência de Forecast (ForecastDivergencePolicy — PBT-06)
        var valorSetupStr = GetCell(row, "Valor Setup");
        var valorMensalStr = GetCell(row, "Valor Mensal");
        var mesesStr = GetCell(row, "Meses");
        var forecastStr = GetCell(row, "Forecast (R$)");

        if (long.TryParse(valorSetupStr, out var valorSetup)
            && long.TryParse(valorMensalStr, out var valorMensal)
            && int.TryParse(mesesStr, out var meses)
            && long.TryParse(forecastStr, out var forecastPlanilha))
        {
            var valorTotal = valorSetup + (valorMensal * meses);
            // Probabilidade não está disponível em todas as linhas;
            // na ausência usamos 100% como valor neutro para o recálculo.
            // A lógica real de probabilidade vem da oportunidade no módulo-alvo.
            // Aqui detectamos divergência de cálculo básico (Req 2.3).
            var divergence = Domain.ValueObjects.ForecastDivergence.Calculate(
                valorTotal: valorTotal,
                probabilidade: 100,
                forecastPlanilha: forecastPlanilha);

            if (divergence.HasDivergence)
            {
                forecastDivergences.Add(new ForecastDivergenceEntry(
                    SourceRowIndex: idx,
                    ForecastPlanilhaCents: forecastPlanilha,
                    ForecastCalculadoCents: divergence.ForecastCalculado,
                    DeltaCents: divergence.Delta));
            }
        }
    }

    private static string? GetCell(SourceRow row, string column) =>
        row.Cells.TryGetValue(column, out var v) ? v : null;
}
