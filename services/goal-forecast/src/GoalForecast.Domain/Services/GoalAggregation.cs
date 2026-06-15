using GoalForecast.Domain.Aggregates;
using GoalForecast.Domain.Exceptions;
using GoalForecast.Domain.ValueObjects;

namespace GoalForecast.Domain.Services;

/// <summary>
/// Serviço de domínio puro que soma metas mensais em agregações trimestrais e anuais.
/// Função total, determinística, sem I/O, sem arredondamento, sem perda de precisão.
/// Meses ausentes contribuem com <see cref="Money.Zero"/> (RN-027).
/// Agregações trimestral/anual nunca são persistidas — derivadas na query (DD-003).
/// Somas são sempre por moeda: nunca agrupa BRL+USD+EUR (ADR-0008).
///
/// Mapeia: Req 7, PBT-02, RN-027, ADR-0008, design §4.6, TASK-06.
/// </summary>
public static class GoalAggregation
{
    /// <summary>
    /// Soma as metas mensais do trimestre que contém o mês de <paramref name="period"/>
    /// filtrando pela <paramref name="currency"/> explícita (ADR-0008).
    /// Meses ausentes contribuem com <see cref="Money.Zero"/> na moeda indicada.
    /// Resultado exato em centavos inteiros, sem arredondamento.
    /// Lança <see cref="DomainException"/> se goals de moedas distintas forem misturados.
    /// </summary>
    /// <param name="goals">Conjunto de metas disponíveis (pode incluir meses de outros trimestres).</param>
    /// <param name="period">Período de referência para determinar o trimestre (Q1..Q4).</param>
    /// <param name="currency">Código ISO-4217 da moeda da agregação (BRL, USD ou EUR).</param>
    /// <returns>Soma exata dos centavos dos meses do trimestre na <paramref name="currency"/> indicada,
    /// ou <see cref="Money.ZeroIn(string)"/> se vazio.</returns>
    public static Money SumByQuarter(IEnumerable<Goal> goals, GoalPeriod period, string currency = "BRL")
    {
        var quarter = period.Quarter();
        var year = period.Year;

        var filtered = goals
            .Where(g =>
                g.Period.Year == year &&
                g.Period.Quarter() == quarter)
            .ToList();

        EnsureSingleCurrency(filtered, currency);

        var totalCents = filtered.Sum(g => g.ValorMeta.Cents);

        return Money.Of(totalCents, currency);
    }

    /// <summary>
    /// Soma as metas mensais de todos os 12 meses do <paramref name="year"/>
    /// filtrando pela <paramref name="currency"/> explícita (ADR-0008).
    /// Meses ausentes contribuem com <see cref="Money.Zero"/> na moeda indicada.
    /// Resultado exato em centavos inteiros, sem arredondamento.
    /// Lança <see cref="DomainException"/> se goals de moedas distintas forem misturados.
    /// </summary>
    /// <param name="goals">Conjunto de metas disponíveis (pode incluir meses de outros anos).</param>
    /// <param name="year">Ano de referência para a agregação.</param>
    /// <param name="currency">Código ISO-4217 da moeda da agregação (BRL, USD ou EUR).</param>
    /// <returns>Soma exata dos centavos dos 12 meses do ano na <paramref name="currency"/> indicada,
    /// ou <see cref="Money.ZeroIn(string)"/> se vazio.</returns>
    public static Money SumByYear(IEnumerable<Goal> goals, int year, string currency = "BRL")
    {
        var filtered = goals
            .Where(g => g.Period.Year == year)
            .ToList();

        EnsureSingleCurrency(filtered, currency);

        var totalCents = filtered.Sum(g => g.ValorMeta.Cents);

        return Money.Of(totalCents, currency);
    }

    /// <summary>
    /// Valida que todos os goals filtrados possuem a mesma moeda esperada (ADR-0008).
    /// Lança <see cref="DomainException"/> (GF-ERR-001) em mismatch.
    /// </summary>
    private static void EnsureSingleCurrency(IReadOnlyList<Goal> goals, string expectedCurrency)
    {
        foreach (var goal in goals)
        {
            if (!string.Equals(goal.ValorMeta.Currency, expectedCurrency, StringComparison.Ordinal))
                throw new DomainException("GF-ERR-001",
                    $"Agregação entre moedas diferentes não é permitida: esperado '{expectedCurrency}', " +
                    $"encontrado '{goal.ValorMeta.Currency}' (ADR-0008).");
        }
    }
}
