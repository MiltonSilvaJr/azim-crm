namespace Reporting.Domain.ValueObjects;

/// <summary>
/// Objeto de valor imutável que representa a participação de um canal no volume de oportunidades.
///
/// Invariantes:
/// <list type="bullet">
///   <item><description><see cref="PercentBasisPoints"/> ∈ [0, 10000] (0% a 100% em basis points).</description></item>
///   <item><description>Percentual em inteiros para conservar soma = 100% sem <c>float</c> (PBT-04, DD-010).</description></item>
///   <item><description>A soma dos percentuais de todos os canais deve ser 10.000 — invariante validada pelo handler (TASK-09).</description></item>
/// </list>
///
/// Mapeia: TASK-03, design §4.3, DD-010, PBT-04.
/// </summary>
public sealed record ChannelShare
{
    /// <summary>Identificador único do canal de origem.</summary>
    public Guid ChannelId { get; }

    /// <summary>Nome do canal de origem.</summary>
    public string ChannelName { get; }

    /// <summary>Número de oportunidades no canal.</summary>
    public int Count { get; }

    /// <summary>Total de valor das oportunidades no canal em centavos inteiros.</summary>
    public long TotalCents { get; }

    /// <summary>
    /// Percentual do canal em basis points inteiros (base 10.000 = 100%).
    /// Exemplo: 25% → 2500; 100% → 10000; 0% → 0.
    /// </summary>
    public int PercentBasisPoints { get; }

    private ChannelShare(Guid channelId, string channelName, int count, long totalCents, int percentBasisPoints)
    {
        if (percentBasisPoints < 0 || percentBasisPoints > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(percentBasisPoints),
                $"percentBasisPoints deve estar em [0, 10000]. Recebido: {percentBasisPoints}. (DD-010, PBT-04)");
        }

        ChannelId          = channelId;
        ChannelName        = channelName;
        Count              = count;
        TotalCents         = totalCents;
        PercentBasisPoints = percentBasisPoints;
    }

    /// <summary>
    /// Cria um <see cref="ChannelShare"/> com os dados do canal.
    /// </summary>
    /// <param name="channelId">Identificador do canal.</param>
    /// <param name="channelName">Nome do canal.</param>
    /// <param name="count">Número de oportunidades.</param>
    /// <param name="totalCents">Total em centavos.</param>
    /// <param name="percentBasisPoints">Percentual em basis points [0, 10000].</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Se <paramref name="percentBasisPoints"/> estiver fora de [0, 10000].
    /// </exception>
    public static ChannelShare Create(
        Guid channelId,
        string channelName,
        int count,
        long totalCents,
        int percentBasisPoints) =>
        new(channelId, channelName, count, totalCents, percentBasisPoints);

    /// <inheritdoc/>
    public override string ToString() =>
        $"ChannelShare({ChannelName}, {Count} ops, {TotalCents} cents, {PercentBasisPoints} bp)";
}
