namespace DataMigration.Application.DTOs;

/// <summary>
/// Atribuição de owner a uma oportunidade triada.
/// Sem PII — referência por índice de linha (RNF 3).
/// </summary>
/// <param name="RowIndex">Índice base-0 da linha na aba Pipeline.</param>
/// <param name="OwnerId">ID do usuário responsável atribuído.</param>
public sealed record OwnerAssignment(int RowIndex, Guid OwnerId);

/// <summary>
/// Resolução de estágio para uma linha com etapa vazia.
/// </summary>
/// <param name="RowIndex">Índice base-0 da linha.</param>
/// <param name="StageId">ID do estágio escolhido.</param>
public sealed record StageResolution(int RowIndex, Guid StageId);

/// <summary>
/// Resolução de percentual de parceiro.
/// Marcado como "a definir" quando <see cref="PartnerId"/> é nulo.
/// </summary>
/// <param name="RowIndex">Índice base-0 da linha.</param>
/// <param name="PartnerId">ID do parceiro (pode ser nulo = sem parceiro).</param>
public sealed record PartnerPctResolution(int RowIndex, Guid? PartnerId);

/// <summary>
/// Decisão de dedupe para um par candidato.
/// </summary>
/// <param name="RowIndexA">Índice da primeira linha.</param>
/// <param name="RowIndexB">Índice da segunda linha.</param>
/// <param name="MergeIntoRowA">Verdadeiro para mesclar B em A; falso para manter ambas.</param>
public sealed record DedupeDecision(int RowIndexA, int RowIndexB, bool MergeIntoRowA);

/// <summary>
/// Snapshot do progresso da triagem assistida.
///
/// Sem PII (DD-007, RNF 3). Persistido como JSONB em
/// <c>MigrationJob.TriageResolutionJson</c> para salvar/retomar entre sessões.
///
/// Rastreia: design §4.1, §5.1, Req 4.4, DD-007, TASK-10.
/// </summary>
public sealed class TriageResolutionDto
{
    /// <summary>Atribuições de owner por linha.</summary>
    public IReadOnlyList<OwnerAssignment> OwnerAssignments { get; init; } = [];

    /// <summary>Resoluções de estágio por linha.</summary>
    public IReadOnlyList<StageResolution> StageResolutions { get; init; } = [];

    /// <summary>Resoluções de parceiro por linha.</summary>
    public IReadOnlyList<PartnerPctResolution> PartnerPctResolutions { get; init; } = [];

    /// <summary>Decisões de dedupe por par.</summary>
    public IReadOnlyList<DedupeDecision> DedupeDecisions { get; init; } = [];
}
