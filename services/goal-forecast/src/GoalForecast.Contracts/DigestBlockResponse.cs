namespace GoalForecast.Contracts;

/// <summary>
/// Resposta do bloco de metas para o Digest worker (GET /api/v1/internal/goals/digest-block).
///
/// Semântica (design §8.6, Req 9.2, RN-018):
/// - Present = false → meta não cadastrada; Digest omite o bloco completamente.
/// - Present = true → bloco completo com todos os campos em centavos inteiros.
///
/// Campos monetários só presentes quando Present = true.
/// Todos os valores monetários em centavos inteiros (RNF 4).
///
/// Mapeia: design §8.6, Req 9, RN-018, RN-029, TASK-21, TASK-25.
/// </summary>
public sealed record DigestBlockResponse
{
    /// <summary>
    /// Indica se há meta cadastrada para o período/BU.
    /// false → Digest deve omitir o bloco; campos monetários são nulos.
    /// </summary>
    public bool Present { get; init; }

    /// <summary>Valor da meta em centavos inteiros. Nulo quando Present = false.</summary>
    public long? ValorMeta { get; init; }

    /// <summary>Valor realizado em centavos inteiros. Nulo quando Present = false ou pipeline indisponível.</summary>
    public long? Realizado { get; init; }

    /// <summary>Gap em centavos inteiros. Nulo quando Present = false ou pipeline indisponível.</summary>
    public long? Gap { get; init; }

    /// <summary>Pipeline disponível em centavos inteiros. Nulo quando Present = false ou pipeline indisponível.</summary>
    public long? PipelineDisponivel { get; init; }

    /// <summary>Indica indisponibilidade do pipeline quando Present = true.</summary>
    public bool PipelineUnavailable { get; init; }

    /// <summary>
    /// Cria resposta de ausência de meta.
    /// O Digest deve omitir o bloco completamente (Req 9.2, RN-018).
    /// </summary>
    public static DigestBlockResponse Absent() =>
        new() { Present = false };

    /// <summary>
    /// Cria resposta com meta presente e todos os campos populados.
    /// </summary>
    public static DigestBlockResponse WithMeta(
        long valorMeta,
        long? realizado,
        long? gap,
        long? pipelineDisponivel,
        bool pipelineUnavailable) =>
        new()
        {
            Present = true,
            ValorMeta = valorMeta,
            Realizado = realizado,
            Gap = gap,
            PipelineDisponivel = pipelineDisponivel,
            PipelineUnavailable = pipelineUnavailable
        };
}
