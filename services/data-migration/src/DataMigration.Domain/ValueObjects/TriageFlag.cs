namespace DataMigration.Domain.ValueObjects;

/// <summary>
/// Tipos de pendência de triagem detectados durante o dry-run.
///
/// Rastreia: design §4.3, Req 2, 4, 5.
/// </summary>
public enum TriageFlagType
{
    /// <summary>Oportunidade sem responsável (owner) atribuído — bloqueante.</summary>
    OwnerMissing,

    /// <summary>Oportunidade sem etapa de funil — não bloqueante (fallback "Lead").</summary>
    StageMissing,

    /// <summary>Parceiro sem percentual informado — não bloqueante.</summary>
    PartnerPctMissing,

    /// <summary>Par de contas candidatos a dedupe — não bloqueante (decisão humana).</summary>
    DedupeCandidate,

    /// <summary>BU (Business Unit) desconhecida no tenant — não bloqueante.</summary>
    BuUnknown,

    /// <summary>Possível typo no nome do responsável — não bloqueante.</summary>
    Typo,
}

/// <summary>
/// Severidade da pendência de triagem.
/// </summary>
public enum TriageSeverity
{
    /// <summary>Pendência que bloqueia a transição para <c>importing</c>.</summary>
    Blocking,

    /// <summary>Pendência informativa; não bloqueia o import.</summary>
    NonBlocking,
}

/// <summary>
/// Objeto de valor que representa uma marcação de pendência de triagem.
///
/// Referencia a linha da planilha por índice (sem PII — RNF 3).
/// Imutável; igualdade por valor (<see cref="FlagType"/>, <see cref="Severity"/>,
/// <see cref="SourceRowIndex"/>).
///
/// Rastreia: design §4.3, Req 2, 4, 5, RNF 3, TASK-07.
/// </summary>
public sealed class TriageFlag : IEquatable<TriageFlag>
{
    /// <summary>Tipo de pendência.</summary>
    public TriageFlagType FlagType { get; }

    /// <summary>Severidade da pendência.</summary>
    public TriageSeverity Severity { get; }

    /// <summary>
    /// Índice base-0 da linha na aba de origem.
    /// Referência sem PII (RNF 3).
    /// </summary>
    public int SourceRowIndex { get; }

    /// <summary>
    /// Cria um <see cref="TriageFlag"/> com tipo, severidade e índice de linha.
    /// </summary>
    public TriageFlag(TriageFlagType flagType, TriageSeverity severity, int sourceRowIndex)
    {
        if (sourceRowIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceRowIndex), "Índice de linha deve ser ≥ 0.");
        }

        FlagType = flagType;
        Severity = severity;
        SourceRowIndex = sourceRowIndex;
    }

    /// <summary>
    /// Cria um <see cref="TriageFlag"/> para uma linha com severidade derivada do tipo.
    /// <see cref="TriageFlagType.OwnerMissing"/> é bloqueante; demais são não-bloqueantes.
    /// </summary>
    public static TriageFlag ForRow(TriageFlagType flagType, int sourceRowIndex)
    {
        var severity = flagType == TriageFlagType.OwnerMissing
            ? TriageSeverity.Blocking
            : TriageSeverity.NonBlocking;

        return new TriageFlag(flagType, severity, sourceRowIndex);
    }

    /// <inheritdoc />
    public bool Equals(TriageFlag? other)
    {
        if (other is null)
        {
            return false;
        }

        return FlagType == other.FlagType
            && Severity == other.Severity
            && SourceRowIndex == other.SourceRowIndex;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as TriageFlag);

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(FlagType, Severity, SourceRowIndex);

    /// <inheritdoc />
    public override string ToString() =>
        $"TriageFlag({FlagType}, {Severity}, row={SourceRowIndex})";

    /// <summary>Operador de igualdade por valor.</summary>
    public static bool operator ==(TriageFlag? left, TriageFlag? right) =>
        Equals(left, right);

    /// <summary>Operador de desigualdade por valor.</summary>
    public static bool operator !=(TriageFlag? left, TriageFlag? right) =>
        !Equals(left, right);
}
