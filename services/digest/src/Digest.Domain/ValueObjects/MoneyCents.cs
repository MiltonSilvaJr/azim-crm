namespace Digest.Domain.ValueObjects;

/// <summary>
/// Valor monetário representado como inteiro em centavos (BRL no MVP).
/// Proíbe construção a partir de <see langword="double"/> ou <see langword="float"/>
/// para evitar erros de arredondamento de ponto flutuante (DD-010, Req 5.5, regra money-as-cents).
/// Imutável; igualdade por valor.
/// </summary>
public sealed record MoneyCents
{
    /// <summary>Instância representando zero centavos.</summary>
    public static readonly MoneyCents Zero = new(0L);

    /// <summary>Valor em centavos (não-negativo).</summary>
    public long Cents { get; }

    /// <summary>
    /// Constrói <see cref="MoneyCents"/> a partir de um valor em centavos.
    /// </summary>
    /// <param name="cents">Valor em centavos. Deve ser não-negativo.</param>
    /// <exception cref="ArgumentOutOfRangeException">Quando <paramref name="cents"/> é negativo.</exception>
    public MoneyCents(long cents)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cents, nameof(cents));
        Cents = cents;
    }

    /// <summary>
    /// Construtor privado bloqueado para <see langword="double"/> — proíbe construção acidental com ponto flutuante.
    /// </summary>
    // ReSharper disable once UnusedParameter.Local
    [Obsolete("Não use double para construir MoneyCents. Use long em centavos.", error: true)]
    private MoneyCents(double _) => throw new NotSupportedException("Use long em centavos.");

    /// <summary>
    /// Construtor privado bloqueado para <see langword="float"/> — proíbe construção acidental com ponto flutuante.
    /// </summary>
    // ReSharper disable once UnusedParameter.Local
    [Obsolete("Não use float para construir MoneyCents. Use long em centavos.", error: true)]
    private MoneyCents(float _) => throw new NotSupportedException("Use long em centavos.");

    /// <summary>Retorna a soma de dois valores monetários.</summary>
    public MoneyCents Add(MoneyCents other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new MoneyCents(Cents + other.Cents);
    }

    /// <summary>
    /// Retorna a diferença entre dois valores monetários.
    /// </summary>
    /// <exception cref="InvalidOperationException">Quando o resultado seria negativo.</exception>
    public MoneyCents Subtract(MoneyCents other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other.Cents > Cents)
            throw new InvalidOperationException(
                $"Subtração resultaria em valor negativo: {Cents} - {other.Cents}.");
        return new MoneyCents(Cents - other.Cents);
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Cents / 100m:F2}";
}
