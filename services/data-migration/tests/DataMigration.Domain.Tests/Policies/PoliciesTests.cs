using DataMigration.Domain.Policies;
using DataMigration.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace DataMigration.Domain.Tests.Policies;

/// <summary>
/// Testes das policies e specifications de domínio.
///
/// Cobre: TASK-07, design §4.6, Req 4, 5, 7; TASK-03 ST-03 (OwnerRequiredSpecification).
/// </summary>
public sealed class PoliciesTests
{
    // =========================================================================
    // AccountDedupePolicy
    // =========================================================================

    [Fact(DisplayName = "AccountDedupePolicy deve detectar pares com mesmo NormalizedName")]
    public void AccountDedupePolicy_ShouldDetect_DuplicatePairs()
    {
        var names = new[]
        {
            NormalizedName.From("Pag.ai"),
            NormalizedName.From("pag.ai"),   // mesmo nome normalizado
            NormalizedName.From("Azim"),
        };

        var policy = new AccountDedupePolicy();
        var candidates = policy.FindDuplicateCandidates(names);

        candidates.Should().HaveCount(1);
        candidates.First().First().Value.Should().Be("pag.ai");
    }

    [Fact(DisplayName = "AccountDedupePolicy não deve detectar duplicatas quando todos distintos")]
    public void AccountDedupePolicy_ShouldNotDetect_WhenAllDistinct()
    {
        var names = new[]
        {
            NormalizedName.From("Empresa A"),
            NormalizedName.From("Empresa B"),
            NormalizedName.From("Empresa C"),
        };

        var policy = new AccountDedupePolicy();
        var candidates = policy.FindDuplicateCandidates(names);

        candidates.Should().BeEmpty();
    }

    // =========================================================================
    // StageFallbackPolicy
    // =========================================================================

    [Fact(DisplayName = "StageFallbackPolicy deve retornar 'Lead' para etapa vazia")]
    public void StageFallbackPolicy_ShouldReturn_Lead_ForEmptyStage()
    {
        var policy = new StageFallbackPolicy();
        var result = policy.Apply(stageName: null);

        result.StageName.Should().Be("Lead");
        result.Flag.Should().NotBeNull();
        result.Flag!.FlagType.Should().Be(TriageFlagType.StageMissing);
        result.Flag.Severity.Should().Be(TriageSeverity.NonBlocking);
    }

    [Fact(DisplayName = "StageFallbackPolicy não deve alterar etapa preenchida")]
    public void StageFallbackPolicy_ShouldNotChange_FilledStage()
    {
        var policy = new StageFallbackPolicy();
        var result = policy.Apply(stageName: "Proposta");

        result.StageName.Should().Be("Proposta");
        result.Flag.Should().BeNull();
    }

    [Fact(DisplayName = "StageFallbackPolicy nunca deve bloquear o import")]
    public void StageFallbackPolicy_ShouldNeverBlock_Import()
    {
        var policy = new StageFallbackPolicy();
        var result = policy.Apply(stageName: null);

        // Flag gerado é não-bloqueante
        if (result.Flag is not null)
        {
            result.Flag.Severity.Should().Be(TriageSeverity.NonBlocking);
        }
    }

    // =========================================================================
    // PartnerPctPendingPolicy
    // =========================================================================

    [Fact(DisplayName = "PartnerPctPendingPolicy deve gerar flag não-bloqueante para parceiro sem percentual")]
    public void PartnerPctPendingPolicy_ShouldGenerateNonBlockingFlag_WhenNoPct()
    {
        var policy = new PartnerPctPendingPolicy();
        var result = policy.Apply(partnerName: "Parceiro X", pctProvided: false);

        result.Flag.Should().NotBeNull();
        result.Flag!.FlagType.Should().Be(TriageFlagType.PartnerPctMissing);
        result.Flag.Severity.Should().Be(TriageSeverity.NonBlocking);
    }

    [Fact(DisplayName = "PartnerPctPendingPolicy não deve gerar flag quando percentual fornecido")]
    public void PartnerPctPendingPolicy_ShouldNotGenerateFlag_WhenPctProvided()
    {
        var policy = new PartnerPctPendingPolicy();
        var result = policy.Apply(partnerName: "Parceiro X", pctProvided: true);

        result.Flag.Should().BeNull();
    }

    // =========================================================================
    // OwnerTypoMappingPolicy
    // =========================================================================

    [Fact(DisplayName = "OwnerTypoMappingPolicy deve corrigir typo conhecido 'Miilton' → 'Milton'")]
    public void OwnerTypoMappingPolicy_ShouldCorrect_KnownTypo()
    {
        var mapping = new Dictionary<string, string>
        {
            ["Miilton"] = "Milton",
            ["Josee"]   = "José",
        };

        var policy = new OwnerTypoMappingPolicy(mapping);
        policy.TryResolve("Miilton", out var corrected).Should().BeTrue();
        corrected.Should().Be("Milton");
    }

    [Fact(DisplayName = "OwnerTypoMappingPolicy deve retornar false para nome sem typo mapeado")]
    public void OwnerTypoMappingPolicy_ShouldReturnFalse_ForUnknownName()
    {
        var policy = new OwnerTypoMappingPolicy(new Dictionary<string, string>());
        policy.TryResolve("Milton", out var corrected).Should().BeFalse();
        corrected.Should().BeNull();
    }

    [Fact(DisplayName = "OwnerTypoMappingPolicy deve ser case-sensitive na chave do mapa")]
    public void OwnerTypoMappingPolicy_ShouldBeCaseSensitive_OnKey()
    {
        var mapping = new Dictionary<string, string> { ["Miilton"] = "Milton" };
        var policy = new OwnerTypoMappingPolicy(mapping);
        policy.TryResolve("miilton", out _).Should().BeFalse(); // case diferente
    }

    // =========================================================================
    // OwnerRequiredSpecification (extraída no ST-03 de TASK-03)
    // =========================================================================

    [Fact(DisplayName = "OwnerRequiredSpecification deve ser true quando todos têm owner")]
    public void OwnerRequiredSpecification_ShouldBeTrue_WhenAllHaveOwner()
    {
        OwnerRequiredSpecification.IsSatisfied(ownerlessCandidateCount: 0)
            .Should().BeTrue();
    }

    [Fact(DisplayName = "OwnerRequiredSpecification deve ser false quando há sem owner")]
    public void OwnerRequiredSpecification_ShouldBeFalse_WhenAnyMissingOwner()
    {
        OwnerRequiredSpecification.IsSatisfied(ownerlessCandidateCount: 1)
            .Should().BeFalse();
    }

    // =========================================================================
    // ColumnNames — constantes de colunas esperadas
    // =========================================================================

    [Fact(DisplayName = "ColumnNames deve expor constantes não-nulas e não-vazias")]
    public void ColumnNames_ShouldExposeNonEmpty_Constants()
    {
        ColumnNames.All.Should().NotBeEmpty();
        ColumnNames.All.Should().AllSatisfy(c => c.Should().NotBeNullOrWhiteSpace());
    }
}
