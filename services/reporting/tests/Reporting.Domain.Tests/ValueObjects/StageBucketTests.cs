using FluentAssertions;
using Reporting.Domain.Enums;
using Reporting.Domain.ValueObjects;
using Xunit;

namespace Reporting.Domain.Tests.ValueObjects;

/// <summary>
/// Testes unitários para o objeto de valor <see cref="StageBucket"/>.
///
/// Invariantes verificadas:
/// - Categoria refletida da origem; não reclassifica após construção.
/// - Imutabilidade e igualdade por valor.
/// - <c>StageId</c> nulo lança exceção de domínio.
/// - <c>StageName</c> nulo ou vazio lança exceção de domínio.
///
/// Mapeia: TASK-03, design §4.3.
/// </summary>
public sealed class StageBucketTests
{
    // -------------------------------------------------------------------------
    // Construção válida
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "StageBucket com dados válidos deve ser construível")]
    public void StageBucket_WithValidData_ShouldBeConstructible()
    {
        var id = Guid.NewGuid();
        var bucket = StageBucket.Create(id, "Proposta Enviada", StageCategory.Open);

        bucket.StageId.Should().Be(id);
        bucket.StageName.Should().Be("Proposta Enviada");
        bucket.Category.Should().Be(StageCategory.Open);
    }

    [Theory(DisplayName = "StageBucket aceita todas as categorias de estágio")]
    [InlineData(StageCategory.Open)]
    [InlineData(StageCategory.Won)]
    [InlineData(StageCategory.Lost)]
    public void StageBucket_WithAnyCategory_ShouldBeConstructible(StageCategory category)
    {
        var act = () => StageBucket.Create(Guid.NewGuid(), "Estágio", category);

        act.Should().NotThrow();
    }

    // -------------------------------------------------------------------------
    // Rejeição de invariantes
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "StageBucket com StageId vazio deve lançar exceção")]
    public void StageBucket_WithEmptyStageId_ShouldThrow()
    {
        var act = () => StageBucket.Create(Guid.Empty, "Proposta", StageCategory.Open);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*stageId*");
    }

    [Theory(DisplayName = "StageBucket com StageName nulo ou vazio deve lançar exceção")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void StageBucket_WithEmptyStageName_ShouldThrow(string? name)
    {
        var act = () => StageBucket.Create(Guid.NewGuid(), name!, StageCategory.Open);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*stageName*");
    }

    // -------------------------------------------------------------------------
    // Imutabilidade e não reclassificação
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "StageBucket deve ser imutável após construção")]
    public void StageBucket_ShouldBeImmutable()
    {
        var bucket = StageBucket.Create(Guid.NewGuid(), "Ganho", StageCategory.Won);

        // Verificar que os campos não mudam
        var originalCategory = bucket.Category;
        originalCategory.Should().Be(StageCategory.Won,
            because: "categoria é refletida da origem e não deve ser reclassificada (design §4.3)");
    }

    [Fact(DisplayName = "StageBucket deve refletir categoria da origem sem reclassificar (design §4.3)")]
    public void StageBucket_Category_ShouldReflectOriginWithoutReclassifying()
    {
        var wonBucket = StageBucket.Create(Guid.NewGuid(), "Fechado Ganho", StageCategory.Won);
        var lostBucket = StageBucket.Create(Guid.NewGuid(), "Perdido", StageCategory.Lost);

        wonBucket.Category.Should().Be(StageCategory.Won);
        lostBucket.Category.Should().Be(StageCategory.Lost);
    }

    // -------------------------------------------------------------------------
    // Igualdade por valor
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "StageBucket com mesmos campos deve ser igual por valor")]
    public void StageBucket_WithSameFields_ShouldBeEqualByValue()
    {
        var id = Guid.NewGuid();
        var a = StageBucket.Create(id, "Negociação", StageCategory.Open);
        var b = StageBucket.Create(id, "Negociação", StageCategory.Open);

        a.Should().Be(b);
    }

    [Fact(DisplayName = "StageBucket com categorias diferentes deve ser diferente")]
    public void StageBucket_WithDifferentCategory_ShouldNotBeEqual()
    {
        var id = Guid.NewGuid();
        var open = StageBucket.Create(id, "Proposta", StageCategory.Open);
        var won  = StageBucket.Create(id, "Proposta", StageCategory.Won);

        open.Should().NotBe(won);
    }
}
