using PartnerManagement.Domain.Partners.Exceptions;

namespace PartnerManagement.Domain.Partners.ValueObjects;

/// <summary>
/// Objeto de valor que representa o nome de um parceiro.
/// Imutável, com igualdade por valor.
/// <c>partner.name</c> não é PII por decisão VAL-PARTNER-01 (2026-06-15); pode aparecer em claro.
/// <c>ToString()</c> retorna representação neutra por design defensivo do VO — use <see cref="Value"/>
/// quando precisar do valor em contextos estruturados (logs, contratos, etc.).
/// Mapeia: Req 1.1, design §4.3.
/// </summary>
public sealed class PartnerName : IEquatable<PartnerName>
{
    /// <summary>Comprimento máximo permitido para o nome do parceiro.</summary>
    public const int MaxLength = 255;

    /// <summary>Valor trimado do nome do parceiro.</summary>
    public string Value { get; }

    private PartnerName(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Cria um <see cref="PartnerName"/> válido a partir da string fornecida.
    /// Aplica trim antes da validação.
    /// </summary>
    /// <param name="value">Nome a ser encapsulado.</param>
    /// <returns>Instância válida de <see cref="PartnerName"/>.</returns>
    /// <exception cref="PartnerNameRequiredException">
    /// Lançada quando <paramref name="value"/> é nulo, vazio ou somente espaços após trim.
    /// </exception>
    public static PartnerName Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new PartnerNameRequiredException();
        }

        string trimmed = value.Trim();
        return new PartnerName(trimmed);
    }

    /// <summary>
    /// Retorna representação neutra por design defensivo do VO, evitando vazamento acidental
    /// em interpolações de string e logs não estruturados.
    /// Use <see cref="Value"/> quando precisar do valor em claro.
    /// </summary>
    public override string ToString() => "[PartnerName]";

    /// <inheritdoc/>
    public bool Equals(PartnerName? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PartnerName other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Value);

    /// <summary>Operador de igualdade por valor.</summary>
    public static bool operator ==(PartnerName? left, PartnerName? right) =>
        left?.Equals(right) ?? right is null;

    /// <summary>Operador de desigualdade por valor.</summary>
    public static bool operator !=(PartnerName? left, PartnerName? right) => !(left == right);
}
