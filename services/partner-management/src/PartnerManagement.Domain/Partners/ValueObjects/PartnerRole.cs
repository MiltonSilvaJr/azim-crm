using PartnerManagement.Domain.Partners.Exceptions;

namespace PartnerManagement.Domain.Partners.ValueObjects;

/// <summary>
/// Objeto de valor que representa o papel tipado de um parceiro (ex.: Indicador, Revendedor).
/// O VO garante não-vazio; a validação contra a lista canônica do tenant é responsabilidade
/// do Aggregate Root via <c>ICanonicalRoleProvider</c> (design §4.1, DD-005).
/// Imutável, com igualdade por valor.
/// Mapeia: Req 5, design §4.3.
/// </summary>
public sealed class PartnerRole : IEquatable<PartnerRole>
{
    /// <summary>Papéis canônicos do seed padrão do sistema.</summary>
    public static readonly IReadOnlyList<string> DefaultCanonicalRoles =
    [
        "Indicador",
        "Revendedor",
        "Distribuidor",
        "Integrador"
    ];

    /// <summary>Valor do papel tipado do parceiro.</summary>
    public string Value { get; }

    private PartnerRole(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Cria um <see cref="PartnerRole"/> válido.
    /// Valida apenas que o valor não é nulo, vazio ou somente espaços.
    /// A validação canônica ocorre no Aggregate Root.
    /// </summary>
    /// <param name="value">Papel a ser encapsulado.</param>
    /// <returns>Instância válida de <see cref="PartnerRole"/>.</returns>
    /// <exception cref="InvalidPartnerRoleException">
    /// Lançada quando <paramref name="value"/> é nulo, vazio ou somente espaços.
    /// </exception>
    public static PartnerRole Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidPartnerRoleException(value);
        }

        return new PartnerRole(value.Trim());
    }

    /// <inheritdoc/>
    public bool Equals(PartnerRole? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PartnerRole other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Value);

    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>Operador de igualdade por valor.</summary>
    public static bool operator ==(PartnerRole? left, PartnerRole? right) =>
        left?.Equals(right) ?? right is null;

    /// <summary>Operador de desigualdade por valor.</summary>
    public static bool operator !=(PartnerRole? left, PartnerRole? right) => !(left == right);
}
