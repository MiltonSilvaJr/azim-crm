namespace Reporting.Domain.ValueObjects;

/// <summary>
/// Objeto de valor imutável que representa uma janela temporal para filtro de relatório.
///
/// Invariantes:
/// <list type="bullet">
///   <item><description><see cref="From"/> ≤ <see cref="To"/>.</description></item>
///   <item><description>Janela de 0 a 12 meses está dentro do caminho de SLO (RNF 1).</description></item>
/// </list>
///
/// Mapeia: TASK-03, design §4.3, RNF 1.
/// </summary>
public sealed record Period
{
    /// <summary>Data de início do período (inclusive).</summary>
    public DateOnly From { get; }

    /// <summary>Data de fim do período (inclusive).</summary>
    public DateOnly To { get; }

    private Period(DateOnly from, DateOnly to)
    {
        if (from > to)
        {
            throw new ArgumentException(
                $"O from ({from}) não pode ser posterior ao to ({to}). (design §4.3)",
                nameof(from));
        }

        From = from;
        To   = to;
    }

    /// <summary>
    /// Cria um <see cref="Period"/> com as datas informadas.
    /// </summary>
    /// <param name="from">Data de início (inclusive).</param>
    /// <param name="to">Data de fim (inclusive).</param>
    /// <exception cref="ArgumentException">Se <paramref name="from"/> for posterior a <paramref name="to"/>.</exception>
    public static Period Create(DateOnly from, DateOnly to) => new(from, to);

    /// <summary>
    /// Retorna <c>true</c> se a janela está dentro do limite de SLO (≤ 12 meses).
    /// Janelas maiores são aceitas mas sinalizadas como fora-de-SLO (RNF 1).
    /// </summary>
    public bool IsWithinSloWindow()
    {
        // Compara em meses aproximados (365 dias / 12 = ~30,4)
        var days = To.DayNumber - From.DayNumber;
        return days <= 366; // até 12 meses (ano bissexto incluso)
    }

    /// <inheritdoc/>
    public override string ToString() => $"Period({From:yyyy-MM-dd} → {To:yyyy-MM-dd})";
}
