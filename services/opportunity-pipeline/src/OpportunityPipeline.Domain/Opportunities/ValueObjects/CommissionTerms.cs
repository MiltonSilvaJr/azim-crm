using OpportunityPipeline.Domain.Opportunities.Exceptions;

namespace OpportunityPipeline.Domain.Opportunities.ValueObjects;

/// <summary>
/// Termos de comissão do parceiro. Imutável.
/// Regra: valor_fixo é mutuamente excludente aos percentuais (Req 11.4, OP-ERR-014).
/// Percentuais em [0, 100]. meses_comissionados ≥ 0.
/// Mapeia: Req 11, design §4.3.
/// </summary>
public sealed record CommissionTerms
{
    /// <summary>Papel do parceiro.</summary>
    public CommissionRole Role { get; }

    /// <summary>Percentual de comissão sobre setup [0, 100].</summary>
    public decimal PctSetup { get; }

    /// <summary>Percentual de comissão sobre recorrente [0, 100].</summary>
    public decimal PctRecorrente { get; }

    /// <summary>Valor fixo de comissão (centavos). Excludente com percentuais.</summary>
    public Money? ValorFixo { get; }

    /// <summary>Quantidade de meses comissionados (≥ 0).</summary>
    public int MesesComissionados { get; }

    /// <summary>
    /// Cria instância com validação das invariantes.
    /// </summary>
    /// <exception cref="DomainException">Se invariantes violadas (OP-ERR-014).</exception>
    public CommissionTerms(
        CommissionRole role,
        decimal pctSetup,
        decimal pctRecorrente,
        Money? valorFixo,
        int mesesComissionados)
    {
        // Mutual exclusão: valor_fixo × percentuais
        if (valorFixo is not null && valorFixo.AmountInCents > 0
            && (pctSetup > 0m || pctRecorrente > 0m))
        {
            throw new DomainException(
                "valor_fixo e percentuais de comissão são mutuamente excludentes (Req 11.4, OP-ERR-014).");
        }

        if (pctSetup < 0m || pctSetup > 100m)
            throw new DomainException(
                $"pct_setup deve estar em [0, 100]. Valor recebido: {pctSetup}.");

        if (pctRecorrente < 0m || pctRecorrente > 100m)
            throw new DomainException(
                $"pct_recorrente deve estar em [0, 100]. Valor recebido: {pctRecorrente}.");

        if (mesesComissionados < 0)
            throw new DomainException(
                $"meses_comissionados não pode ser negativo. Valor recebido: {mesesComissionados}.");

        Role = role;
        PctSetup = pctSetup;
        PctRecorrente = pctRecorrente;
        ValorFixo = valorFixo;
        MesesComissionados = mesesComissionados;
    }
}
