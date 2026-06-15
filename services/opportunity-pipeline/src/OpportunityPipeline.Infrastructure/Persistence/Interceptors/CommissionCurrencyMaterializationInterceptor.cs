using Microsoft.EntityFrameworkCore.Diagnostics;
using OpportunityPipeline.Domain.Opportunities.Entities;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Interceptor de materialização EF Core que reconstrói os objetos <see cref="Money"/>
/// dentro de <see cref="CommissionCalculation"/> e <see cref="CommissionTerms"/> com a
/// currency correta após o EF hidratar <see cref="OpportunityPartnerCommission"/>.
///
/// O EF usa conversores simples que produzem Money("BRL") como placeholder.
/// Este interceptor lê <c>OpportunityPartnerCommission.Currency</c> e substitui
/// os Money com instâncias que portam a currency real (ADR-0008).
///
/// Mapeia: ADR-0008, design §7.3.
/// </summary>
public sealed class CommissionCurrencyMaterializationInterceptor : IMaterializationInterceptor
{
    /// <inheritdoc/>
    public object InitializedInstance(MaterializationInterceptionData materializationData, object instance)
    {
        if (instance is not OpportunityPartnerCommission commission)
            return instance;

        var currency = commission.Currency;
        if (string.IsNullOrEmpty(currency) || string.Equals(currency, "BRL", StringComparison.Ordinal))
            return instance; // "BRL" já é o valor do placeholder — sem necessidade de reconstruir

        // Reconstrói CommissionCalculation com currency correta
        var oldCalc = commission.Calculation;
        var newCalc = new CommissionCalculation(
            ComissaoSetup: new Money(oldCalc.ComissaoSetup.AmountInCents, currency),
            ComissaoRecorrente: new Money(oldCalc.ComissaoRecorrente.AmountInCents, currency),
            ComissaoTotal: new Money(oldCalc.ComissaoTotal.AmountInCents, currency));

        // Reconstrói CommissionTerms com ValorFixo corrigido
        var oldTerms = commission.Terms;
        Money? newValorFixo = oldTerms.ValorFixo is null
            ? null
            : new Money(oldTerms.ValorFixo.AmountInCents, currency);

        var newTerms = new CommissionTerms(
            oldTerms.Role,
            oldTerms.PctSetup,
            oldTerms.PctRecorrente,
            newValorFixo,
            oldTerms.MesesComissionados);

        commission.ApplyCurrencyFix(newCalc, newTerms);

        return instance;
    }
}
