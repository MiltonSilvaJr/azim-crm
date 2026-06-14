namespace Reporting.Contracts.ReadModels;

/// <summary>
/// Linha do relatório de forecast por BU/mês.
/// <see cref="GoalCents"/> é <c>null</c> quando não há meta cadastrada (degradação graciosa — Req 6.3, P8).
/// Valores monetários em centavos inteiros (DD-007).
/// Mapeia: Req 6, design §5.2, TASK-07.
/// </summary>
public sealed record ForecastRow(
    Guid BuId,
    string BuName,
    int Year,
    int Month,
    long WeightedForecastCents,
    long RealizedCents,
    long? GoalCents);
