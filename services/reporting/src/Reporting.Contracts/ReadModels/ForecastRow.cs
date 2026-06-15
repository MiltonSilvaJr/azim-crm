using System.Text.Json.Serialization;

namespace Reporting.Contracts.ReadModels;

/// <summary>
/// Linha do relatório de forecast por BU/mês.
/// <see cref="GoalCents"/> é <c>null</c> quando não há meta cadastrada (degradação graciosa — Req 6.3, P8).
/// Quando <c>null</c>, o campo é omitido da serialização JSON (<c>JsonIgnoreCondition.WhenWritingNull</c>).
/// Valores monetários em centavos inteiros (DD-007).
/// <see cref="Currency"/> é o código ISO-4217 da moeda das oportunidades da BU (ADR-0008).
/// Mapeia: Req 6, design §5.2, TASK-07, TASK-20.
/// </summary>
public sealed record ForecastRow(
    Guid BuId,
    string BuName,
    int Year,
    int Month,
    long WeightedForecastCents,
    long RealizedCents,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    long? GoalCents,
    string Currency = "BRL");
