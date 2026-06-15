using OpportunityPipeline.Domain.Opportunities.Exceptions;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Domain.Tests.Opportunities.ValueObjects.Domain;

/// <summary>
/// Testes para CommissionTerms — mutual exclusão valor_fixo × percentuais.
/// Mapeia: Req 11, TASK-03.
/// </summary>
public sealed class CommissionTermsTests
{
    [Fact(DisplayName = "CommissionTerms: válido com percentuais")]
    public void CommissionTerms_ValidWithPercentages()
    {
        var terms = new CommissionTerms(
            role: CommissionRole.Revendedor,
            pctSetup: 10m,
            pctRecorrente: 5m,
            valorFixo: null,
            mesesComissionados: 12);

        terms.PctSetup.Should().Be(10m);
        terms.PctRecorrente.Should().Be(5m);
        terms.ValorFixo.Should().BeNull();
        terms.MesesComissionados.Should().Be(12);
    }

    [Fact(DisplayName = "CommissionTerms: válido com valor_fixo")]
    public void CommissionTerms_ValidWithValorFixo()
    {
        var terms = new CommissionTerms(
            role: CommissionRole.Indicador,
            pctSetup: 0m,
            pctRecorrente: 0m,
            valorFixo: new Money(50000L, "BRL"),
            mesesComissionados: 0);

        terms.ValorFixo.Should().NotBeNull();
        terms.ValorFixo!.AmountInCents.Should().Be(50000L);
    }

    [Fact(DisplayName = "CommissionTerms: valor_fixo > 0 com pct_setup > 0 lança DomainException (mutual exclusão)")]
    public void CommissionTerms_FixedAndPctSetup_ThrowsDomainException()
    {
        var act = () => new CommissionTerms(
            role: CommissionRole.Revendedor,
            pctSetup: 10m,
            pctRecorrente: 0m,
            valorFixo: new Money(50000L, "BRL"),
            mesesComissionados: 0);
        act.Should().Throw<DomainException>()
            .WithMessage("*mutuamente excludentes*");
    }

    [Fact(DisplayName = "CommissionTerms: valor_fixo > 0 com pct_recorrente > 0 lança DomainException (mutual exclusão)")]
    public void CommissionTerms_FixedAndPctRecorrente_ThrowsDomainException()
    {
        var act = () => new CommissionTerms(
            role: CommissionRole.Revendedor,
            pctSetup: 0m,
            pctRecorrente: 5m,
            valorFixo: new Money(50000L, "BRL"),
            mesesComissionados: 0);
        act.Should().Throw<DomainException>()
            .WithMessage("*mutuamente excludentes*");
    }

    [Fact(DisplayName = "CommissionTerms: pct_setup negativo lança DomainException")]
    public void CommissionTerms_NegativePctSetup_ThrowsDomainException()
    {
        var act = () => new CommissionTerms(
            role: CommissionRole.Revendedor,
            pctSetup: -1m,
            pctRecorrente: 0m,
            valorFixo: null,
            mesesComissionados: 0);
        act.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "CommissionTerms: pct_setup > 100 lança DomainException")]
    public void CommissionTerms_PctSetupAbove100_ThrowsDomainException()
    {
        var act = () => new CommissionTerms(
            role: CommissionRole.Revendedor,
            pctSetup: 101m,
            pctRecorrente: 0m,
            valorFixo: null,
            mesesComissionados: 0);
        act.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "CommissionTerms: meses_comissionados negativo lança DomainException")]
    public void CommissionTerms_NegativeMeses_ThrowsDomainException()
    {
        var act = () => new CommissionTerms(
            role: CommissionRole.Revendedor,
            pctSetup: 10m,
            pctRecorrente: 0m,
            valorFixo: null,
            mesesComissionados: -1);
        act.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "CommissionTerms: igualdade por valor")]
    public void CommissionTerms_EqualityByValue()
    {
        var a = new CommissionTerms(CommissionRole.Revendedor, 10m, 5m, null, 12);
        var b = new CommissionTerms(CommissionRole.Revendedor, 10m, 5m, null, 12);
        a.Should().Be(b);
    }

    [Fact(DisplayName = "CommissionTerms: imutável — sem setters públicos mutáveis")]
    public void CommissionTerms_IsImmutable()
    {
        var type = typeof(CommissionTerms);
        var writeableProps = type.GetProperties()
            .Where(p => p.CanWrite && p.GetSetMethod()?.IsPublic == true)
            .ToList();
        writeableProps.Should().BeEmpty(because: "CommissionTerms deve ser imutável (record)");
    }
}
