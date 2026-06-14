using GoalForecast.Domain.Exceptions;

namespace GoalForecast.Domain.ValueObjects;

/// <summary>
/// Objeto de valor imutável que representa o período de uma meta (ano + mês).
/// Protege a invariante INV-2: Month ∈ [1..12] e Year deve ter quatro dígitos.
/// Igualdade por valor (record semantics).
///
/// Mapeia: Req 1.3, requirements §4, INV-2, design §4.3, TASK-04.
/// </summary>
public sealed record GoalPeriod
{
    /// <summary>Ano de quatro dígitos (ex.: 2026).</summary>
    public int Year { get; }

    /// <summary>Mês no intervalo [1..12].</summary>
    public int Month { get; }

    /// <summary>
    /// Cria um <see cref="GoalPeriod"/> validando as invariantes INV-2.
    /// Lança <see cref="DomainException"/> GF-ERR-002 se Year ou Month forem inválidos.
    /// </summary>
    /// <param name="year">Ano de quatro dígitos.</param>
    /// <param name="month">Mês entre 1 e 12.</param>
    /// <exception cref="DomainException">GF-ERR-002 quando year ou month estão fora da faixa.</exception>
    public GoalPeriod(int year, int month)
    {
        if (year < 1000 || year > 9999)
            throw new DomainException("GF-ERR-002",
                $"Mês ou ano fora da faixa: Year deve ter quatro dígitos (recebido: {year}).");

        if (month < 1 || month > 12)
            throw new DomainException("GF-ERR-002",
                $"Mês ou ano fora da faixa: Month deve estar entre 1 e 12 (recebido: {month}).");

        Year = year;
        Month = month;
    }

    /// <summary>
    /// Retorna o trimestre (1..4) correspondente ao mês do período.
    /// Q1 = meses 1-3, Q2 = 4-6, Q3 = 7-9, Q4 = 10-12.
    /// </summary>
    public int Quarter() => (Month - 1) / 3 + 1;

    /// <summary>
    /// Retorna o ano do período.
    /// Conveniente para passagem à agregação por ano.
    /// </summary>
    public int YearOf() => Year;

    /// <inheritdoc/>
    public override string ToString() => $"GoalPeriod({Year}/{Month:D2})";
}
