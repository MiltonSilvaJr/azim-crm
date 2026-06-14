using PartnerManagement.Domain.Partners.Exceptions;

namespace PartnerManagement.Domain.Partners.ValueObjects;

/// <summary>
/// Objeto de valor que encapsula um percentual de comissão no intervalo fechado [0,00; 100,00]
/// com exatamente 2 casas decimais.
/// Usa <see cref="decimal"/> — proibido <c>float</c> ou <c>double</c> (RNF 6.3, DD-004).
/// Mapeado para NUMERIC(5,2) no PostgreSQL (design §7).
/// Mapeia: Req 6.3, RNF 6, PBT-03, design §4.3.
/// </summary>
public sealed class Percentage : IEquatable<Percentage>
{
    private const decimal MinValue = 0.00m;
    private const decimal MaxValue = 100.00m;

    /// <summary>Valor do percentual com exatamente 2 casas decimais.</summary>
    public decimal Value { get; }

    private Percentage(decimal value)
    {
        Value = value;
    }

    /// <summary>
    /// Cria um <see cref="Percentage"/> válido.
    /// O valor é arredondado para 2 casas decimais antes da validação (NBR-5891 ToEven).
    /// </summary>
    /// <param name="value">Percentual em <see cref="decimal"/>.</param>
    /// <returns>Instância válida de <see cref="Percentage"/>.</returns>
    /// <exception cref="PercentageOutOfRangeException">
    /// Lançada quando o valor está fora de [0,00; 100,00] após arredondamento.
    /// </exception>
    public static Percentage Create(decimal value)
    {
        // Arredondamento NBR-5891 (ToEven) antes da validação de intervalo
        decimal rounded = Math.Round(value, 2, MidpointRounding.ToEven);

        if (rounded < MinValue || rounded > MaxValue)
        {
            throw new PercentageOutOfRangeException();
        }

        return new Percentage(rounded);
    }

    /// <inheritdoc/>
    public bool Equals(Percentage? other) =>
        other is not null && Value == other.Value;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Percentage other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Value);

    /// <summary>Retorna a representação textual do percentual (ex.: "25,00%").</summary>
    public override string ToString() => $"{Value:0.00}%";

    /// <summary>Operador de igualdade por valor.</summary>
    public static bool operator ==(Percentage? left, Percentage? right) =>
        left?.Equals(right) ?? right is null;

    /// <summary>Operador de desigualdade por valor.</summary>
    public static bool operator !=(Percentage? left, Percentage? right) => !(left == right);
}
