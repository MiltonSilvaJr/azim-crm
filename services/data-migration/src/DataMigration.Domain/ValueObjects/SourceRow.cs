namespace DataMigration.Domain.ValueObjects;

/// <summary>
/// Objeto de valor que representa uma linha bruta da planilha.
///
/// Contém as células como dicionário coluna→valor (string raw).
/// Imutável; igualdade por <see cref="SheetName"/> e <see cref="RowIndex"/>.
///
/// Nota: <see cref="Cells"/> pode conter PII (nome, e-mail, telefone) —
/// nunca deve ser logado diretamente (RNF 3). O mapeamento canônico
/// (<c>CanonicalRowMapper</c>) isola a transformação.
///
/// Rastreia: design §4.3, Req 1, 3, RNF 3, TASK-07.
/// </summary>
public sealed class SourceRow : IEquatable<SourceRow>
{
    /// <summary>Nome da aba de origem ("pipeline" ou "acoes_comerciais").</summary>
    public string SheetName { get; }

    /// <summary>Índice base-0 da linha na aba.</summary>
    public int RowIndex { get; }

    /// <summary>
    /// Células da linha como dicionário coluna→valor (string raw).
    /// Leitura somente — nunca mutado após criação.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Cells { get; }

    /// <summary>
    /// Cria um <see cref="SourceRow"/> a partir dos dados brutos da planilha.
    /// </summary>
    public SourceRow(
        string sheetName,
        int rowIndex,
        IReadOnlyDictionary<string, string?> cells)
    {
        if (string.IsNullOrWhiteSpace(sheetName))
        {
            throw new ArgumentException("sheetName é obrigatório.", nameof(sheetName));
        }

        if (rowIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rowIndex), "Índice deve ser ≥ 0.");
        }

        SheetName = sheetName;
        RowIndex = rowIndex;
        Cells = cells ?? throw new ArgumentNullException(nameof(cells));
    }

    /// <summary>
    /// Sobrecarga que aceita <see cref="Dictionary{TKey,TValue}"/>.
    /// </summary>
    public SourceRow(string sheetName, int rowIndex, Dictionary<string, string?> cells)
        : this(sheetName, rowIndex, (IReadOnlyDictionary<string, string?>)cells)
    {
    }

    /// <inheritdoc />
    public bool Equals(SourceRow? other)
    {
        if (other is null)
        {
            return false;
        }

        return string.Equals(SheetName, other.SheetName, StringComparison.Ordinal)
            && RowIndex == other.RowIndex;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as SourceRow);

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(SheetName, RowIndex);

    /// <inheritdoc />
    public override string ToString() => $"SourceRow({SheetName}[{RowIndex}])";

    /// <summary>Operador de igualdade por valor.</summary>
    public static bool operator ==(SourceRow? left, SourceRow? right) =>
        Equals(left, right);

    /// <summary>Operador de desigualdade por valor.</summary>
    public static bool operator !=(SourceRow? left, SourceRow? right) =>
        !Equals(left, right);
}
