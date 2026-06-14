namespace Reporting.Contracts.ReadModels;

/// <summary>
/// Linha do relatório de ranking por responsável.
/// <see cref="DisplayName"/> é <c>null</c> quando <c>PiiMinimizationPolicy</c> determina omissão (DD-008, RNF 4).
/// Valores monetários em centavos inteiros (DD-007).
/// Mapeia: Req 2, design §5.2, TASK-08.
/// </summary>
public sealed record RankingRow(
    Guid OwnerId,
    string? DisplayName,
    int WonCount,
    long WonValueCents,
    long PipelineForecastCents);
