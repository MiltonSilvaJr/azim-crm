namespace Reporting.Contracts.ReadModels;

/// <summary>
/// Linha do relatório de funil por estágio.
/// Valores monetários em centavos inteiros (DD-007).
/// Mapeia: Req 1, design §5.2, TASK-06.
/// </summary>
public sealed record FunnelRow(
    Guid StageId,
    string StageName,
    string Category,
    int Count,
    long TotalCents,
    long WeightedForecastCents);
