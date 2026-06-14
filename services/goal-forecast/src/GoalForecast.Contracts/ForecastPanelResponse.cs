namespace GoalForecast.Contracts;

/// <summary>
/// Resposta do painel comparativo "Direção" (GET /api/v1/forecast).
/// Operação total: sempre 200, nunca 404, nunca NaN (PBT-04, DD-006, DD-007).
///
/// Semântica dos campos nulos:
/// - ValorMeta/Gap/PctAtingimento = null → meta não cadastrada (DD-006, Req 6).
/// - Realizado/PipelineDisponivel = null quando PipelineUnavailable = true (DD-007).
/// - PipelineUnavailable = true → pipeline indisponível; sem zero confundível.
///
/// Todos os valores monetários em centavos inteiros (RNF 4).
///
/// Mapeia: design §8.4, DD-006, DD-007, PBT-03, PBT-04, TASK-21, TASK-24.
/// </summary>
public sealed record ForecastPanelResponse(
    /// <summary>Escopo da consulta: "BU" ou "RESPONSAVEL".</summary>
    string Scope,

    /// <summary>Identificador da BU.</summary>
    Guid BuId,

    /// <summary>Identificador do responsável; nulo para escopo BU.</summary>
    Guid? OwnerId,

    /// <summary>Ano do período.</summary>
    int Year,

    /// <summary>Mês do período (1..12).</summary>
    int Month,

    /// <summary>
    /// Valor da meta em centavos inteiros.
    /// Nulo quando meta não cadastrada para o período (DD-006).
    /// </summary>
    long? ValorMeta,

    /// <summary>
    /// Valor realizado em centavos inteiros.
    /// Nulo quando pipeline indisponível (DD-007).
    /// </summary>
    long? Realizado,

    /// <summary>
    /// Pipeline disponível em centavos inteiros.
    /// Nulo quando pipeline indisponível (DD-007).
    /// </summary>
    long? PipelineDisponivel,

    /// <summary>
    /// Gap (valorMeta - realizado) em centavos inteiros.
    /// Nulo quando meta ausente ou pipeline indisponível (DD-006, DD-007).
    /// </summary>
    long? Gap,

    /// <summary>
    /// Percentual de atingimento (realizado / valorMeta).
    /// Nulo quando meta ausente, meta zero ou pipeline indisponível.
    /// Nunca NaN (PBT-04).
    /// </summary>
    double? PctAtingimento,

    /// <summary>
    /// Indica que o pipeline está indisponível.
    /// Quando true, Realizado/PipelineDisponivel/Gap/PctAtingimento são nulos (DD-007, RNF 6).
    /// </summary>
    bool PipelineUnavailable);
