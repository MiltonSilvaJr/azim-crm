using GoalForecast.Application.Behaviors;
using GoalForecast.Domain.Authorization;
using MediatR;

namespace GoalForecast.Application.Queries;

/// <summary>
/// Query para o bloco de metas do Digest worker (BC-06).
/// Resposta total: para qualquer período/BU válido, retorna resultado bem-formado.
///
/// Mapeia: Req 9, RN-018, RN-029, design §5.2, §8.6, TASK-14.
/// </summary>
public sealed record GetGoalDigestBlockQuery : IRequest<DigestBlockResult>, IHasPrincipal
{
    /// <summary>Principal autenticado (serviço interno ou Digest worker).</summary>
    public required GoalPrincipal Principal { get; init; }

    /// <summary>Identificador da BU.</summary>
    public required Guid BuId { get; init; }

    /// <summary>Identificador do responsável; nulo para escopo BU.</summary>
    public Guid? OwnerId { get; init; }

    /// <summary>Ano do período.</summary>
    public required int Year { get; init; }

    /// <summary>Mês do período (1..12).</summary>
    public required int Month { get; init; }
}

/// <summary>
/// Resultado do bloco de metas para o Digest.
///
/// Semântica (design §8.6, Req 9.2, RN-018):
/// <list type="bullet">
///   <item><c>Present = false</c> → meta não cadastrada; Digest omite o bloco completamente.</item>
///   <item><c>Present = true</c> → bloco completo com todos os campos em centavos inteiros.</item>
/// </list>
///
/// Campos monetários só presentes quando <see cref="Present"/> = true.
/// <see cref="PipelineUnavailable"/> = true sinaliza degradação ao Digest.
/// </summary>
public sealed record DigestBlockResult
{
    /// <summary>Indica se há meta cadastrada para o período/BU.</summary>
    public bool Present { get; }

    /// <summary>Valor da meta em centavos inteiros. Nulo quando <see cref="Present"/> = false.</summary>
    public long? ValorMeta { get; }

    /// <summary>Valor realizado em centavos inteiros. Nulo quando <see cref="Present"/> = false ou pipeline indisponível.</summary>
    public long? Realizado { get; }

    /// <summary>Gap em centavos inteiros. Nulo quando <see cref="Present"/> = false ou pipeline indisponível.</summary>
    public long? Gap { get; }

    /// <summary>Pipeline disponível em centavos inteiros. Nulo quando <see cref="Present"/> = false ou pipeline indisponível.</summary>
    public long? PipelineDisponivel { get; }

    /// <summary>Indica indisponibilidade do pipeline quando <see cref="Present"/> = true.</summary>
    public bool PipelineUnavailable { get; }

    private DigestBlockResult(
        bool present,
        long? valorMeta,
        long? realizado,
        long? gap,
        long? pipelineDisponivel,
        bool pipelineUnavailable)
    {
        Present = present;
        ValorMeta = valorMeta;
        Realizado = realizado;
        Gap = gap;
        PipelineDisponivel = pipelineDisponivel;
        PipelineUnavailable = pipelineUnavailable;
    }

    /// <summary>
    /// Cria resultado de ausência de meta. Digest deve omitir o bloco (Req 9.2, RN-018).
    /// </summary>
    public static DigestBlockResult Absent() =>
        new(present: false, null, null, null, null, false);

    /// <summary>
    /// Cria resultado com meta presente e todos os campos populados.
    /// </summary>
    public static DigestBlockResult WithMeta(
        long valorMeta,
        long? realizado,
        long? gap,
        long? pipelineDisponivel,
        bool pipelineUnavailable) =>
        new(present: true, valorMeta, realizado, gap, pipelineDisponivel, pipelineUnavailable);
}
