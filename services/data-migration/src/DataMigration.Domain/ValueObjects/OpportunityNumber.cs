using System.Text.RegularExpressions;

namespace DataMigration.Domain.ValueObjects;

/// <summary>
/// Objeto de valor imutável que representa o número de uma oportunidade no formato
/// <c>AZ-NNNN</c> (ex.: "AZ-0043", "AZ-0095").
///
/// Invariantes (design §4.3, DD-004, ADR-0003, Req 8):
///   - Formato: literal "AZ-" seguido de exatamente 4 dígitos decimais.
///   - Imutável após criação; igualdade por valor (<see cref="Value"/>).
///   - Único por tenant — unicidade garantida pelo <see cref="OpportunityNumberAllocator"/>
///     e pela constraint <c>uq_opportunity_number_per_tenant</c> (ADR-0003).
///
/// Rastreia: design §4.3, DD-004, ADR-0003, Req 8, PBT-05, TASK-06.
/// </summary>
public sealed class OpportunityNumber : IEquatable<OpportunityNumber>
{
    /// <summary>Padrão de formato AZ-NNNN (exatamente 4 dígitos após o hífen).</summary>
    private static readonly Regex FormatRegex =
        new(@"^AZ-\d{4}$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    /// <summary>Valor canônico do número no formato AZ-NNNN.</summary>
    public string Value { get; }

    /// <summary>
    /// Número sequencial extraído do formato AZ-NNNN (ex: "AZ-0043" → 43).
    /// </summary>
    public int SequenceNumber { get; }

    private OpportunityNumber(string value)
    {
        Value = value;
        SequenceNumber = int.Parse(value[3..]); // pula "AZ-"
    }

    // =========================================================================
    // Fábrica e validação
    // =========================================================================

    /// <summary>
    /// Tenta parsear uma string no formato AZ-NNNN.
    /// Retorna <c>null</c> quando o formato é inválido.
    /// </summary>
    public static OpportunityNumber? Parse(string? input)
    {
        if (!IsValid(input))
        {
            return null;
        }

        return new OpportunityNumber(input!);
    }

    /// <summary>
    /// Cria um <see cref="OpportunityNumber"/> a partir de um número sequencial.
    /// </summary>
    /// <param name="sequenceNumber">Número sequencial ≥ 1 (formatado como 4 dígitos).</param>
    /// <exception cref="ArgumentOutOfRangeException">Quando sequenceNumber &lt; 1 ou &gt; 9999.</exception>
    public static OpportunityNumber FromSequence(int sequenceNumber)
    {
        if (sequenceNumber < 1 || sequenceNumber > 9999)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequenceNumber),
                $"sequenceNumber deve estar em [1, 9999]. Recebido: {sequenceNumber}.");
        }

        return new OpportunityNumber($"AZ-{sequenceNumber:D4}");
    }

    /// <summary>
    /// Verifica se a string tem o formato válido AZ-NNNN.
    /// </summary>
    public static bool IsValid(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return false;
        }

        return FormatRegex.IsMatch(input);
    }

    // =========================================================================
    // Igualdade por valor
    // =========================================================================

    /// <inheritdoc />
    public bool Equals(OpportunityNumber? other)
    {
        if (other is null)
        {
            return false;
        }

        return string.Equals(Value, other.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as OpportunityNumber);

    /// <inheritdoc />
    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(Value);

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Operador de igualdade por valor.</summary>
    public static bool operator ==(OpportunityNumber? left, OpportunityNumber? right) =>
        Equals(left, right);

    /// <summary>Operador de desigualdade por valor.</summary>
    public static bool operator !=(OpportunityNumber? left, OpportunityNumber? right) =>
        !Equals(left, right);
}
