namespace Reporting.Contracts.ReadModels;

/// <summary>
/// Linha do relatório de oportunidades por canal.
/// <see cref="PercentBasisPoints"/> em base 10.000 (100% = 10.000) para conservar soma sem float (DD-010, PBT-04).
/// Valores monetários em centavos inteiros (DD-007).
/// <see cref="Currency"/> é o código ISO-4217 da moeda das oportunidades do canal (ADR-0008).
/// Mapeia: Req 3, design §5.2, TASK-09.
/// </summary>
public sealed record ChannelRow(
    Guid ChannelId,
    string ChannelName,
    int Count,
    long TotalCents,
    int PercentBasisPoints,
    string Currency = "BRL");
