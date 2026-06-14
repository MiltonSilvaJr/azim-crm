using DataMigration.Domain.ValueObjects;
using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataMigration.Domain.Tests.ValueObjects;

/// <summary>
/// Testes unitários de <see cref="OpportunityNumber"/> e <see cref="OpportunityNumberAllocator"/>.
///
/// Cobre: TASK-06, Req 8, PBT-05, design §4.3, DD-004, ADR-0003, RNF 2.3.
/// Formato: AZ-NNNN (AZ- + 4 dígitos com zero-padding).
/// Preservados da planilha: mantidos inalterados.
/// Gerados: sequência ≥ 95, sem colisão com preservados.
/// </summary>
public sealed class OpportunityNumberTests
{
    // =========================================================================
    // OpportunityNumber — parsing e validação
    // =========================================================================

    [Theory(DisplayName = "Parse deve aceitar formatos válidos AZ-NNNN")]
    [InlineData("AZ-0001")]
    [InlineData("AZ-0095")]
    [InlineData("AZ-9999")]
    [InlineData("AZ-0043")]
    public void Parse_ShouldAccept_ValidFormats(string input)
    {
        var number = OpportunityNumber.Parse(input);
        number.Should().NotBeNull();
        number!.Value.Should().Be(input);
    }

    [Theory(DisplayName = "Parse deve rejeitar formatos inválidos")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("AZ-0")]         // menos de 4 dígitos
    [InlineData("AZ-00001")]     // mais de 4 dígitos
    [InlineData("az-0001")]      // minúsculas
    [InlineData("BZ-0001")]      // prefixo errado
    [InlineData("AZ0001")]       // sem hífen
    [InlineData("AZ-ABCD")]      // letras em vez de dígitos
    public void Parse_ShouldReturnNull_ForInvalidFormats(string? input)
    {
        var number = OpportunityNumber.Parse(input);
        number.Should().BeNull();
    }

    [Theory(DisplayName = "IsValid deve retornar true para formatos válidos")]
    [InlineData("AZ-0001")]
    [InlineData("AZ-0095")]
    [InlineData("AZ-9999")]
    public void IsValid_ShouldReturnTrue_ForValidFormats(string input)
    {
        OpportunityNumber.IsValid(input).Should().BeTrue();
    }

    [Theory(DisplayName = "IsValid deve retornar false para formatos inválidos")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("AZ-00")]
    [InlineData("az-0001")]
    public void IsValid_ShouldReturnFalse_ForInvalidFormats(string? input)
    {
        OpportunityNumber.IsValid(input).Should().BeFalse();
    }

    // =========================================================================
    // OpportunityNumber — imutabilidade e igualdade por valor
    // =========================================================================

    [Fact(DisplayName = "OpportunityNumber deve ser imutável após criação")]
    public void OpportunityNumber_ShouldBeImmutable_AfterCreation()
    {
        var number = OpportunityNumber.Parse("AZ-0043")!;
        number.Value.Should().Be("AZ-0043");
        // Não há setter — compilação garante imutabilidade
    }

    [Fact(DisplayName = "OpportunityNumber com mesmo valor deve ser igual por valor")]
    public void OpportunityNumber_SameValue_ShouldBeEqual()
    {
        var a = OpportunityNumber.Parse("AZ-0043");
        var b = OpportunityNumber.Parse("AZ-0043");
        a.Should().Be(b);
    }

    [Fact(DisplayName = "OpportunityNumber com valores diferentes deve ser diferente")]
    public void OpportunityNumber_DifferentValues_ShouldNotBeEqual()
    {
        var a = OpportunityNumber.Parse("AZ-0043");
        var b = OpportunityNumber.Parse("AZ-0044");
        a.Should().NotBe(b);
    }

    // =========================================================================
    // OpportunityNumber — extração de sequência
    // =========================================================================

    [Theory(DisplayName = "SequenceNumber deve extrair o número inteiro do formato AZ-NNNN")]
    [InlineData("AZ-0001", 1)]
    [InlineData("AZ-0043", 43)]
    [InlineData("AZ-0095", 95)]
    [InlineData("AZ-9999", 9999)]
    public void SequenceNumber_ShouldExtract_Correctly(string input, int expected)
    {
        var number = OpportunityNumber.Parse(input)!;
        number.SequenceNumber.Should().Be(expected);
    }

    // =========================================================================
    // OpportunityNumberAllocator — comportamento básico
    // =========================================================================

    [Fact(DisplayName = "Allocate deve preservar números existentes sem alteração")]
    public void Allocate_ShouldPreserve_ExistingNumbers()
    {
        var preserved = new[]
        {
            OpportunityNumber.Parse("AZ-0010")!,
            OpportunityNumber.Parse("AZ-0020")!,
            OpportunityNumber.Parse("AZ-0030")!,
        };

        var result = OpportunityNumberAllocator.Allocate(
            preserved: preserved,
            nextFreeSequence: 95,
            generateCount: 0);

        result.Should().Contain(n => n.Value == "AZ-0010");
        result.Should().Contain(n => n.Value == "AZ-0020");
        result.Should().Contain(n => n.Value == "AZ-0030");
    }

    [Fact(DisplayName = "Allocate deve gerar números sequenciais a partir de nextFreeSequence")]
    public void Allocate_ShouldGenerate_SequentialNumbers()
    {
        var result = OpportunityNumberAllocator.Allocate(
            preserved: Array.Empty<OpportunityNumber>(),
            nextFreeSequence: 95,
            generateCount: 3);

        result.Should().Contain(n => n.Value == "AZ-0095");
        result.Should().Contain(n => n.Value == "AZ-0096");
        result.Should().Contain(n => n.Value == "AZ-0097");
    }

    [Fact(DisplayName = "Allocate não deve colidir preservados com gerados")]
    public void Allocate_ShouldNot_CollidePReservedWithGenerated()
    {
        var preserved = new[]
        {
            OpportunityNumber.Parse("AZ-0095")!,
            OpportunityNumber.Parse("AZ-0096")!,
        };

        // Dois gerados devem pular 95 e 96 e começar em 97
        var result = OpportunityNumberAllocator.Allocate(
            preserved: preserved,
            nextFreeSequence: 95,
            generateCount: 2);

        var values = result.Select(n => n.Value).ToHashSet();
        values.Should().Contain("AZ-0095");
        values.Should().Contain("AZ-0096");
        values.Should().Contain("AZ-0097");
        values.Should().Contain("AZ-0098");
        values.Should().HaveCount(4);
    }

    [Fact(DisplayName = "Allocate deve retornar conjunto único sem duplicatas")]
    public void Allocate_ShouldReturn_UniqueSet()
    {
        var preserved = new[]
        {
            OpportunityNumber.Parse("AZ-0010")!,
            OpportunityNumber.Parse("AZ-0020")!,
        };

        var result = OpportunityNumberAllocator.Allocate(
            preserved: preserved,
            nextFreeSequence: 95,
            generateCount: 5);

        var values = result.Select(n => n.Value).ToList();
        values.Should().OnlyHaveUniqueItems();
    }

    [Fact(DisplayName = "nextFreeSequence menor que 95 deve ser elevado para 95 (design §4.3)")]
    public void Allocate_ShouldElevateNextFree_ToMinimum95()
    {
        var result = OpportunityNumberAllocator.Allocate(
            preserved: Array.Empty<OpportunityNumber>(),
            nextFreeSequence: 1,  // deve ser elevado para 95
            generateCount: 2);

        result.All(n => n.SequenceNumber >= 95).Should().BeTrue();
    }

    // =========================================================================
    // PBT-05 — FsCheck
    // Propriedades:
    //   (a) preservados permanecem inalterados
    //   (b) gerados iniciam no próximo livre ≥ 95
    //   (c) conjunto final único por tenant (sem colisão)
    // Mapeia: PBT-05, design §4.3, DD-004, ADR-0003, TASK-06.
    // =========================================================================

    /// <summary>
    /// PBT-05 (a): todos os preservados aparecem inalterados no resultado.
    /// </summary>
    [Property(
        DisplayName = "PBT-05 (a): preservados aparecem inalterados no resultado",
        MaxTest = 200)]
    public Property Pbt05a_Preserved_AppearUnchanged(
        NonNegativeInt rawCount, PositiveInt rawStart)
    {
        var count = rawCount.Get % 10; // 0..9 preservados
        var start = 1 + (rawStart.Get % 90); // 1..90

        var preserved = Enumerable.Range(start, count)
            .Select(i => OpportunityNumber.Parse($"AZ-{i:D4}")!)
            .ToArray();

        var result = OpportunityNumberAllocator.Allocate(
            preserved: preserved,
            nextFreeSequence: 95,
            generateCount: 0);

        var resultValues = result.Select(n => n.Value).ToHashSet();
        var allPreservedPresent = preserved.All(p => resultValues.Contains(p.Value));

        return Prop.Label(allPreservedPresent, $"count={count}, start={start}");
    }

    /// <summary>
    /// PBT-05 (b): gerados têm sequência ≥ 95.
    /// </summary>
    [Property(
        DisplayName = "PBT-05 (b): gerados têm sequência ≥ 95",
        MaxTest = 200)]
    public Property Pbt05b_Generated_HaveSequenceAtLeast95(
        NonNegativeInt rawGenerate)
    {
        var generateCount = rawGenerate.Get % 10; // 0..9 gerados

        var result = OpportunityNumberAllocator.Allocate(
            preserved: Array.Empty<OpportunityNumber>(),
            nextFreeSequence: 95,
            generateCount: generateCount);

        var allAtLeast95 = result.All(n => n.SequenceNumber >= 95);
        return Prop.Label(allAtLeast95, $"generateCount={generateCount}");
    }

    /// <summary>
    /// PBT-05 (c): conjunto final não tem duplicatas.
    /// </summary>
    [Property(
        DisplayName = "PBT-05 (c): conjunto final é único (sem colisão)",
        MaxTest = 200)]
    public Property Pbt05c_FinalSet_IsUnique(
        NonNegativeInt rawPreserved, NonNegativeInt rawGenerate)
    {
        var preservedCount = rawPreserved.Get % 5;  // 0..4
        var generateCount = rawGenerate.Get % 5;    // 0..4

        var preserved = Enumerable.Range(1, preservedCount)
            .Select(i => OpportunityNumber.Parse($"AZ-{i:D4}")!)
            .ToArray();

        var result = OpportunityNumberAllocator.Allocate(
            preserved: preserved,
            nextFreeSequence: 95,
            generateCount: generateCount);

        var values = result.Select(n => n.Value).ToList();
        var isUnique = values.Count == values.Distinct().Count();

        return Prop.Label(
            isUnique,
            $"preserved={preservedCount}, generated={generateCount}, total={values.Count}");
    }
}
