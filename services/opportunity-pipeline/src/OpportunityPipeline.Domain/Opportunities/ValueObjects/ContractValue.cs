using OpportunityPipeline.Domain.Opportunities.Exceptions;

namespace OpportunityPipeline.Domain.Opportunities.ValueObjects;

/// <summary>
/// Agrega os componentes de valor contratual: setup + mensal × meses.
/// Invariante INV-8: valor_total = valor_setup + valor_mensal × duracao_meses.
/// Regra: duracao_meses obrigatório (> 0) quando valor_mensal > 0 (Req 8.4).
/// Mapeia: Req 8, RNF 11, DD-004, design §4.3.
/// </summary>
public sealed record ContractValue
{
    /// <summary>Valor de setup em centavos.</summary>
    public Money Setup { get; }

    /// <summary>Valor mensal em centavos.</summary>
    public Money Mensal { get; }

    /// <summary>Duração em meses. Obrigatório quando Mensal > 0.</summary>
    public int DuracaoMeses { get; }

    /// <summary>
    /// Valor total calculado: setup + mensal × meses.
    /// Aritmética inteira exata, sem perda de precisão (PBT-03).
    /// </summary>
    public long TotalInCents => Setup.AmountInCents + Mensal.AmountInCents * DuracaoMeses;

    /// <summary>
    /// Cria instância de ContractValue com validação de invariantes.
    /// </summary>
    /// <param name="setup">Valor de setup (≥ 0).</param>
    /// <param name="mensal">Valor mensal (≥ 0).</param>
    /// <param name="duracaoMeses">Duração em meses (≥ 0; obrigatório > 0 quando mensal > 0).</param>
    /// <exception cref="DomainException">Se invariantes violadas.</exception>
    public ContractValue(Money setup, Money mensal, int duracaoMeses)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(mensal);

        if (duracaoMeses < 0)
            throw new DomainException(
                $"duracao_meses não pode ser negativo. Valor recebido: {duracaoMeses}.");

        if (mensal.AmountInCents > 0 && duracaoMeses == 0)
            throw new DomainException(
                "duracao_meses é obrigatório quando há valor mensal (Req 8.4, OP-ERR-012).");

        Setup = setup;
        Mensal = mensal;
        DuracaoMeses = duracaoMeses;
    }

    /// <summary>Retorna representação legível para debug.</summary>
    public override string ToString() =>
        $"setup={Setup}, mensal={Mensal}, meses={DuracaoMeses}, total={TotalInCents} centavos";
}
