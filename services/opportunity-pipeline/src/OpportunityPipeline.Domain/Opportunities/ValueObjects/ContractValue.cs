using OpportunityPipeline.Domain.Opportunities.Exceptions;

namespace OpportunityPipeline.Domain.Opportunities.ValueObjects;

/// <summary>
/// Agrega os componentes de valor contratual: setup + mensal × meses + moeda.
/// Invariante INV-8: valor_total = valor_setup + valor_mensal × duracao_meses.
/// Regra: duracao_meses obrigatório (> 0) quando valor_mensal > 0 (Req 8.4).
/// Regra: Setup e Mensal na mesma moeda (ADR-0008).
/// O EF Core mapeia os três escalares: SetupCents, MensalCents, DuracaoMeses e Currency.
/// Mapeia: Req 8, RNF 11, DD-004, design §4.3, ADR-0008.
/// </summary>
public sealed class ContractValue
{
    // ── Backing fields mapeados pelo EF Core ────────────────────────────────
    // EF lê/escreve esses campos diretamente via UsePropertyAccessMode(Field).

    private long _setupCents;
    private long _mensalCents;

    /// <summary>Centavos de setup — campo mapeado pelo EF Core.</summary>
    internal long SetupCents
    {
        get => _setupCents;
        private set => _setupCents = value;
    }

    /// <summary>Centavos de mensal — campo mapeado pelo EF Core.</summary>
    internal long MensalCents
    {
        get => _mensalCents;
        private set => _mensalCents = value;
    }

    /// <summary>Duração em meses. Obrigatório quando Mensal > 0.</summary>
    public int DuracaoMeses { get; private set; }

    /// <summary>
    /// Moeda do contrato (ISO-4217: BRL, USD, EUR). Imutável após criação (ADR-0008).
    /// Mapeada como coluna pelo EF Core.
    /// </summary>
    public string Currency { get; private set; } = null!;

    // ── Value objects derivados (não mapeados pelo EF) ──────────────────────

    /// <summary>Valor de setup em centavos na moeda do contrato.</summary>
    public Money Setup => new(_setupCents, Currency);

    /// <summary>Valor mensal em centavos na moeda do contrato.</summary>
    public Money Mensal => new(_mensalCents, Currency);

    /// <summary>
    /// Valor total calculado: setup + mensal × meses.
    /// Aritmética inteira exata, sem perda de precisão (PBT-03).
    /// </summary>
    public long TotalInCents => _setupCents + _mensalCents * DuracaoMeses;

    /// <summary>
    /// Construtor privado sem parâmetros para uso exclusivo do EF Core na materialização.
    /// </summary>
#pragma warning disable CS8618
    private ContractValue() { }
#pragma warning restore CS8618

    /// <summary>
    /// Cria instância de ContractValue com validação de invariantes.
    /// Exige que Setup e Mensal sejam na mesma moeda (ADR-0008).
    /// </summary>
    /// <param name="setup">Valor de setup (≥ 0).</param>
    /// <param name="mensal">Valor mensal (≥ 0).</param>
    /// <param name="duracaoMeses">Duração em meses (≥ 0; obrigatório > 0 quando mensal > 0).</param>
    /// <exception cref="DomainException">Se invariantes violadas ou moedas divergentes.</exception>
    public ContractValue(Money setup, Money mensal, int duracaoMeses)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(mensal);

        // ADR-0008: Setup e Mensal devem estar na mesma moeda.
        if (!string.Equals(setup.Currency, mensal.Currency, StringComparison.Ordinal))
            throw new DomainException(
                $"Setup e Mensal devem estar na mesma moeda. Recebido: setup={setup.Currency}, mensal={mensal.Currency} (ADR-0008).");

        if (duracaoMeses < 0)
            throw new DomainException(
                $"duracao_meses não pode ser negativo. Valor recebido: {duracaoMeses}.");

        if (mensal.AmountInCents > 0 && duracaoMeses == 0)
            throw new DomainException(
                "duracao_meses é obrigatório quando há valor mensal (Req 8.4, OP-ERR-012).");

        _setupCents = setup.AmountInCents;
        _mensalCents = mensal.AmountInCents;
        DuracaoMeses = duracaoMeses;
        Currency = setup.Currency;
    }

    /// <summary>Verifica igualdade por valor.</summary>
    public override bool Equals(object? obj) =>
        obj is ContractValue other &&
        _setupCents == other._setupCents &&
        _mensalCents == other._mensalCents &&
        DuracaoMeses == other.DuracaoMeses &&
        string.Equals(Currency, other.Currency, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(_setupCents, _mensalCents, DuracaoMeses, Currency);

    /// <summary>Retorna representação legível para debug.</summary>
    public override string ToString() =>
        $"setup={_setupCents} {Currency}, mensal={_mensalCents} {Currency}, meses={DuracaoMeses}, total={TotalInCents}";
}
