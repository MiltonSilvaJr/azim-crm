using Reporting.Domain.Enums;

namespace Reporting.Domain.ValueObjects;

/// <summary>
/// Objeto de valor imutável que representa uma categoria de estágio refletida da origem.
///
/// Invariantes:
/// <list type="bullet">
///   <item><description><see cref="StageId"/> não pode ser <see cref="Guid.Empty"/>.</description></item>
///   <item><description><see cref="StageName"/> não pode ser nulo ou vazio.</description></item>
///   <item><description>A <see cref="Category"/> é refletida do BC-01 — o reporting não reclassifica (P2, DD-003).</description></item>
/// </list>
///
/// Mapeia: TASK-03, design §4.3, DD-003.
/// </summary>
public sealed record StageBucket
{
    /// <summary>Identificador do estágio na pipeline.</summary>
    public Guid StageId { get; }

    /// <summary>Nome do estágio (ex: "Proposta Enviada", "Ganha", "Perdida").</summary>
    public string StageName { get; }

    /// <summary>
    /// Categoria do estágio (<c>Open</c>, <c>Won</c> ou <c>Lost</c>).
    /// Refletida da origem sem reclassificação.
    /// </summary>
    public StageCategory Category { get; }

    private StageBucket(Guid stageId, string stageName, StageCategory category)
    {
        if (stageId == Guid.Empty)
        {
            throw new ArgumentException(
                "stageId não pode ser Guid.Empty. (design §4.3)",
                nameof(stageId));
        }

        if (string.IsNullOrWhiteSpace(stageName))
        {
            throw new ArgumentException(
                "stageName não pode ser nulo, vazio ou apenas espaços. (design §4.3)",
                nameof(stageName));
        }

        StageId   = stageId;
        StageName = stageName;
        Category  = category;
    }

    /// <summary>
    /// Cria um <see cref="StageBucket"/> com os dados do estágio.
    /// </summary>
    /// <param name="stageId">Identificador do estágio.</param>
    /// <param name="stageName">Nome do estágio.</param>
    /// <param name="category">Categoria do estágio (refletida da origem).</param>
    /// <exception cref="ArgumentException">
    ///   Se <paramref name="stageId"/> for vazio ou <paramref name="stageName"/> for nulo/vazio.
    /// </exception>
    public static StageBucket Create(Guid stageId, string stageName, StageCategory category) =>
        new(stageId, stageName, category);

    /// <inheritdoc/>
    public override string ToString() => $"StageBucket({StageName}, {Category})";
}
