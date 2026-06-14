using OpportunityPipeline.Domain.Opportunities.Exceptions;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Domain.Tests.Opportunities.ValueObjects.Domain;

/// <summary>
/// Testes para OpportunityNumber — formato AZ-NNNN, imutabilidade.
/// Mapeia: Req 3, INV-5, PBT-02, TASK-03.
/// </summary>
public sealed class OpportunityNumberTests
{
    [Theory(DisplayName = "OpportunityNumber: formatos válidos")]
    [InlineData("AZ-0001")]
    [InlineData("AZ-9999")]
    [InlineData("AZ-0000")]
    [InlineData("AZ-12345")]
    [InlineData("AZ-100000")]
    public void OpportunityNumber_ValidFormats(string value)
    {
        var number = new OpportunityNumber(value);
        number.Value.Should().Be(value);
    }

    [Theory(DisplayName = "OpportunityNumber: formatos inválidos lançam DomainException")]
    [InlineData("AZ-12")]       // menos de 4 dígitos
    [InlineData("AZ-123")]      // menos de 4 dígitos
    [InlineData("BZ-0001")]     // prefixo errado
    [InlineData("az-0001")]     // prefixo minúsculo
    [InlineData("AZ0001")]      // sem hífen
    [InlineData("AZ-abc")]      // letras no número
    [InlineData("")]            // vazio
    [InlineData("AZ-")]         // sem sequência
    [InlineData("0001")]        // sem prefixo
    public void OpportunityNumber_InvalidFormats_ThrowsDomainException(string value)
    {
        var act = () => new OpportunityNumber(value);
        act.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "OpportunityNumber: ToString retorna o valor formatado")]
    public void OpportunityNumber_ToString_ReturnsFormattedValue()
    {
        var number = new OpportunityNumber("AZ-0001");
        number.ToString().Should().Be("AZ-0001");
    }

    [Fact(DisplayName = "OpportunityNumber: igualdade por valor")]
    public void OpportunityNumber_EqualityByValue()
    {
        var a = new OpportunityNumber("AZ-0001");
        var b = new OpportunityNumber("AZ-0001");
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact(DisplayName = "OpportunityNumber: desigualdade por valor")]
    public void OpportunityNumber_InequalityByValue()
    {
        var a = new OpportunityNumber("AZ-0001");
        var b = new OpportunityNumber("AZ-0002");
        (a != b).Should().BeTrue();
    }

    /// <summary>
    /// PBT-02 parcial: para qualquer string AZ-NNNN válida (4+ dígitos),
    /// o valor após criar é idêntico ao original (imutabilidade).
    /// </summary>
    [Property(MaxTest = 200, DisplayName = "PBT-02: OpportunityNumber — imutável após criação")]
    public Property PBT02_OpportunityNumber_IsImmutable()
    {
        // Gera números válidos AZ-NNNN (4 a 8 dígitos)
        var validGen = Gen.Choose(0, 9999_9999)
            .Select(n => $"AZ-{n:0000}");

        return Prop.ForAll(
            Arb.From(validGen),
            value =>
            {
                var number = new OpportunityNumber(value);
                return number.Value == value && number.ToString() == value;
            });
    }
}
