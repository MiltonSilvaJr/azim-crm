using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Domain.Opportunities.Services;

/// <summary>
/// Serviço de domínio puro: calcula comissão por componente com NbrRounding ToEven.
/// Fórmulas:
///   comissao_setup = round(valor_setup × pct_setup / 100)
///   comissao_recorrente = round(valor_mensal × meses_comissionados × pct_recorrente / 100)
///   comissao_total = setup + recorrente
///   quando valor_fixo definido: comissao_total = valor_fixo
/// Sem dependências de infraestrutura. Nunca float/double.
/// Mapeia: Req 12, PBT-05, design §4.6.
/// </summary>
public static class CommissionCalculator
{
    /// <summary>
    /// Calcula a comissão do parceiro por componente.
    /// </summary>
    /// <param name="contractValue">Valor contratual (setup, mensal, meses).</param>
    /// <param name="terms">Termos de comissão (percentuais ou valor fixo).</param>
    /// <returns>Resultado imutável do cálculo de comissão.</returns>
    public static CommissionCalculation Calculate(ContractValue contractValue, CommissionTerms terms)
    {
        ArgumentNullException.ThrowIfNull(contractValue);
        ArgumentNullException.ThrowIfNull(terms);

        // Moeda herdada do contrato (ADR-0008)
        var currency = contractValue.Currency;

        // Quando valor_fixo definido e positivo, ignora percentuais
        if (terms.ValorFixo is not null && terms.ValorFixo.AmountInCents > 0)
        {
            return new CommissionCalculation(
                ComissaoSetup: Money.Zero(currency),
                ComissaoRecorrente: Money.Zero(currency),
                ComissaoTotal: terms.ValorFixo);
        }

        // comissao_setup = round(valor_setup × pct_setup / 100)
        var comissaoSetupCents = NbrRounding.RoundHalfToEven(
            (long)(contractValue.Setup.AmountInCents * terms.PctSetup),
            100);

        // comissao_recorrente = round(valor_mensal × meses_comissionados × pct_recorrente / 100)
        var baseRecorrente = contractValue.Mensal.AmountInCents * terms.MesesComissionados;
        var comissaoRecorrenteCents = NbrRounding.RoundHalfToEven(
            (long)(baseRecorrente * terms.PctRecorrente),
            100);

        var comissaoTotalCents = comissaoSetupCents + comissaoRecorrenteCents;

        return new CommissionCalculation(
            ComissaoSetup: new Money(comissaoSetupCents, currency),
            ComissaoRecorrente: new Money(comissaoRecorrenteCents, currency),
            ComissaoTotal: new Money(comissaoTotalCents, currency));
    }
}
