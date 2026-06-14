namespace DataMigration.Domain.ValueObjects;

/// <summary>
/// Objeto de valor que encapsula o recálculo do forecast ponderado e a detecção
/// de divergência em relação ao valor da planilha.
///
/// Fórmula (design §4.6, DD-005, NBR 5891):
///   <c>forecastCalculado = round_half_even(valorTotal × probabilidade / 100)</c>
///   em centavos inteiros (<see cref="long"/>).
///
/// Divergência detectada somente quando <c>|forecastCalculado − forecastPlanilha| > 1</c>.
///
/// Invariantes:
///   - Cálculo exclusivamente em aritmética inteira via <c>decimal</c> intermediário
///     (sem <c>float</c>/<c>double</c> — `.forge/rules/domain/money-as-cents.md`).
///   - <c>valorTotal</c> ≥ 0 (em centavos, BIGINT).
///   - <c>probabilidade</c> ∈ [0, 100].
///   - Imutável; igualdade por valor.
///
/// Rastreia: design §4.3, §4.6, DD-005, Req 2.3, PBT-06, TASK-05.
/// </summary>
public sealed class ForecastDivergence : IEquatable<ForecastDivergence>
{
    /// <summary>
    /// Threshold de divergência em centavos: divergências de 1 centavo ou menos são ignoradas.
    /// Divergência listada quando <c>|Δ| > DivergenceThresholdCents</c>.
    ///
    /// Rastreia: design §4.6, Req 2.3.
    /// </summary>
    public const long DivergenceThresholdCents = 1L;

    /// <summary>Valor do forecast recalculado em centavos (<c>round_half_even</c>).</summary>
    public long ForecastCalculado { get; }

    /// <summary>Valor do forecast informado na planilha em centavos.</summary>
    public long ForecastPlanilha { get; }

    /// <summary>
    /// Diferença <c>forecastCalculado − forecastPlanilha</c> em centavos.
    /// Positivo quando o calculado é maior; negativo quando é menor.
    /// </summary>
    public long Delta { get; }

    /// <summary>
    /// <c>true</c> quando <c>|Delta| > <see cref="DivergenceThresholdCents"/></c>.
    /// Apenas divergências acima de 1 centavo são listadas no relatório de triagem.
    /// </summary>
    public bool HasDivergence { get; }

    private ForecastDivergence(long forecastCalculado, long forecastPlanilha)
    {
        ForecastCalculado = forecastCalculado;
        ForecastPlanilha = forecastPlanilha;
        Delta = forecastCalculado - forecastPlanilha;
        HasDivergence = Math.Abs(Delta) > DivergenceThresholdCents;
    }

    // =========================================================================
    // Fábrica / cálculo
    // =========================================================================

    /// <summary>
    /// Calcula o forecast ponderado com arredondamento bancário (NBR 5891)
    /// e detecta divergência em relação ao valor da planilha.
    /// </summary>
    /// <param name="valorTotal">
    /// Valor total da oportunidade em centavos (<c>long</c> ≥ 0).
    /// </param>
    /// <param name="probabilidade">
    /// Probabilidade de fechamento em %, inteiro em [0, 100].
    /// </param>
    /// <param name="forecastPlanilha">
    /// Forecast informado na planilha em centavos (<c>long</c>).
    /// </param>
    /// <returns>
    /// <see cref="ForecastDivergence"/> com o forecast calculado e a flag de divergência.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Lançada quando <paramref name="valorTotal"/> &lt; 0 ou
    /// <paramref name="probabilidade"/> fora de [0, 100].
    /// </exception>
    public static ForecastDivergence Calculate(
        long valorTotal,
        int probabilidade,
        long forecastPlanilha)
    {
        if (valorTotal < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(valorTotal),
                $"valorTotal deve ser ≥ 0 (em centavos). Recebido: {valorTotal}.");
        }

        if (probabilidade < 0 || probabilidade > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(probabilidade),
                $"probabilidade deve estar em [0, 100]. Recebido: {probabilidade}.");
        }

        // Cálculo intermediário via decimal para preservar precisão de meio-centavo.
        // Uma única operação de arredondamento no final (NBR 5891 — sem cascata).
        // Proibido float/double (money-as-cents.md, DD-005).
        var exactCents = (decimal)valorTotal * probabilidade / 100m;
        var forecastCalculado = (long)Math.Round(exactCents, MidpointRounding.ToEven);

        return new ForecastDivergence(forecastCalculado, forecastPlanilha);
    }

    // =========================================================================
    // Igualdade por valor
    // =========================================================================

    /// <inheritdoc />
    public bool Equals(ForecastDivergence? other)
    {
        if (other is null)
        {
            return false;
        }

        return ForecastCalculado == other.ForecastCalculado
            && ForecastPlanilha == other.ForecastPlanilha;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ForecastDivergence);

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(ForecastCalculado, ForecastPlanilha);

    /// <summary>Operador de igualdade por valor.</summary>
    public static bool operator ==(ForecastDivergence? left, ForecastDivergence? right) =>
        Equals(left, right);

    /// <summary>Operador de desigualdade por valor.</summary>
    public static bool operator !=(ForecastDivergence? left, ForecastDivergence? right) =>
        !Equals(left, right);

    /// <inheritdoc />
    public override string ToString() =>
        $"ForecastDivergence(calculado={ForecastCalculado}, planilha={ForecastPlanilha}, " +
        $"delta={Delta}, hasDivergence={HasDivergence})";
}
