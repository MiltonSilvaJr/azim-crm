namespace GoalForecast.Contracts;

/// <summary>
/// Resposta da agregação derivada de metas (GET /api/v1/forecast/aggregate).
/// Soma derivada em memória — nunca persistida (DD-003, RN-027).
/// Valor em centavos inteiros (RNF 4).
///
/// Mapeia: design §8.5, DD-003, RN-027, TASK-21, TASK-24.
/// </summary>
public sealed record ForecastAggregateResponse(
    /// <summary>Granularidade: "quarter" ou "year".</summary>
    string Granularity,

    /// <summary>Ano de referência.</summary>
    int Year,

    /// <summary>Trimestre (1..4); presente apenas quando Granularity = "quarter".</summary>
    int? Quarter,

    /// <summary>
    /// Soma derivada das metas em centavos inteiros.
    /// Meses ausentes contribuem com zero (RN-027, PBT-02).
    /// </summary>
    long ValorMetaAgregado);
