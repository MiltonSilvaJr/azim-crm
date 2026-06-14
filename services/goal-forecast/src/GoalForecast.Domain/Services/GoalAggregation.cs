using GoalForecast.Domain.Aggregates;
using GoalForecast.Domain.ValueObjects;

namespace GoalForecast.Domain.Services;

/// <summary>
/// Serviço de domínio puro que soma metas mensais em agregações trimestrais e anuais.
/// Função total, determinística, sem I/O, sem arredondamento, sem perda de precisão.
/// Meses ausentes contribuem com <see cref="Money.Zero"/> (RN-027).
/// Agregações trimestral/anual nunca são persistidas — derivadas na query (DD-003).
///
/// Mapeia: Req 7, PBT-02, RN-027, design §4.6, TASK-06.
/// </summary>
public static class GoalAggregation
{
    /// <summary>
    /// Soma as metas mensais do trimestre que contém o mês de <paramref name="period"/>.
    /// Filtra <paramref name="goals"/> pelo mesmo ano e pelos 3 meses do trimestre.
    /// Meses ausentes contribuem com <see cref="Money.Zero"/>.
    /// Resultado exato em centavos inteiros, sem arredondamento.
    /// </summary>
    /// <param name="goals">Conjunto de metas disponíveis (pode incluir meses de outros trimestres).</param>
    /// <param name="period">Período de referência para determinar o trimestre (Q1..Q4).</param>
    /// <returns>Soma exata dos centavos dos meses do trimestre, ou <see cref="Money.Zero"/> se vazio.</returns>
    public static Money SumByQuarter(IEnumerable<Goal> goals, GoalPeriod period)
    {
        var quarter = period.Quarter();
        var year = period.Year;
        var firstMonth = (quarter - 1) * 3 + 1;

        var totalCents = goals
            .Where(g =>
                g.Period.Year == year &&
                g.Period.Quarter() == quarter)
            .Sum(g => g.ValorMeta.Cents);

        return Money.Of(totalCents);
    }

    /// <summary>
    /// Soma as metas mensais de todos os 12 meses do <paramref name="year"/>.
    /// Filtra <paramref name="goals"/> pelo ano. Meses ausentes contribuem com <see cref="Money.Zero"/>.
    /// Resultado exato em centavos inteiros, sem arredondamento.
    /// </summary>
    /// <param name="goals">Conjunto de metas disponíveis (pode incluir meses de outros anos).</param>
    /// <param name="year">Ano de referência para a agregação.</param>
    /// <returns>Soma exata dos centavos dos 12 meses do ano, ou <see cref="Money.Zero"/> se vazio.</returns>
    public static Money SumByYear(IEnumerable<Goal> goals, int year)
    {
        var totalCents = goals
            .Where(g => g.Period.Year == year)
            .Sum(g => g.ValorMeta.Cents);

        return Money.Of(totalCents);
    }
}
