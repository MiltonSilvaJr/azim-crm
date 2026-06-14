namespace AccountManagement.Domain.Accounts.ValueObjects;

/// <summary>
/// Objeto de valor que representa o número de telefone de um contato.
///
/// Invariantes:
/// - Apenas dígitos significativos são retidos (remove formatação, espaços, parênteses, hífens).
/// - Opcional — pode não existir em <see cref="ContactInfo"/>.
/// - Igualdade por valor.
///
/// Nota PII: não exponha o valor em logs, traces ou exceções. Use via
/// <see cref="ContactInfo.ToMasked"/> para auditoria (design §4.3, RNF 1, DD-003).
///
/// Mapeia: design §4.3, Req 5.2.
/// </summary>
public sealed class Phone : IEquatable<Phone>
{
    /// <summary>Dígitos significativos do número de telefone.</summary>
    public string Value { get; }

    private Phone(string value) => Value = value;

    /// <summary>
    /// Cria um <see cref="Phone"/> retendo apenas os dígitos do número informado.
    /// </summary>
    /// <param name="value">Número de telefone em qualquer formato.</param>
    /// <returns>Instância de <see cref="Phone"/> com apenas dígitos.</returns>
    /// <exception cref="ArgumentException">
    /// Lançada quando <paramref name="value"/> é nulo, vazio ou não contém dígitos.
    /// </exception>
    public static Phone Create(string? value)
    {
        if (value is null)
            throw new ArgumentException("O número de telefone não pode ser nulo.", nameof(value));

        var digits = new string(value.Where(char.IsDigit).ToArray());

        if (digits.Length == 0)
            throw new ArgumentException(
                "O número de telefone deve conter ao menos um dígito.", nameof(value));

        return new Phone(digits);
    }

    /// <inheritdoc />
    public bool Equals(Phone? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Phone other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    /// <inheritdoc />
    /// <remarks>
    /// Não use em logs diretamente — use <see cref="ContactInfo.ToMasked"/> (RNF 1).
    /// </remarks>
    public override string ToString() => Value;
}
