using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using TenantAdministration.Domain.Policies;
using TenantAdministration.Domain.ValueObjects;
using Xunit;

namespace TenantAdministration.Domain.Tests.Policies;

/// <summary>
/// Testes para ToneDerivationService.
/// Cobre TASK-03: PBT-05 e determinismo.
/// </summary>
public sealed class ToneDerivationServiceTests
{
    private static ColorPair MakePair(string primary, string secondary) =>
        ColorPair.Create(primary, secondary).Value;

    [Fact]
    public void Derive_ValidColorPair_ReturnsDerivedTones()
    {
        var pair = MakePair("#1A73E8", "#34A853");
        var tones = ToneDerivationService.Derive(pair);
        tones.Should().NotBeNull();
        tones.PrimaryHover.Should().MatchRegex("^#[0-9A-F]{6}$");
        tones.PrimaryActive.Should().MatchRegex("^#[0-9A-F]{6}$");
        tones.PrimaryMuted.Should().MatchRegex("^#[0-9A-F]{6}$");
        tones.SecondaryHover.Should().MatchRegex("^#[0-9A-F]{6}$");
        tones.SecondaryActive.Should().MatchRegex("^#[0-9A-F]{6}$");
        tones.SecondaryMuted.Should().MatchRegex("^#[0-9A-F]{6}$");
    }

    [Fact]
    public void Derive_SameInput_ReturnsSameOutput()
    {
        var pair = MakePair("#1A73E8", "#34A853");
        var t1 = ToneDerivationService.Derive(pair);
        var t2 = ToneDerivationService.Derive(pair);
        t1.Should().Be(t2);
    }

    [Fact]
    public void Derive_IsStateless_NoSideEffects()
    {
        // Chamadas sucessivas com pares diferentes não afetam umas às outras
        var pair1 = MakePair("#000000", "#FFFFFF");
        var pair2 = MakePair("#FF0000", "#0000FF");

        var t1a = ToneDerivationService.Derive(pair1);
        _ = ToneDerivationService.Derive(pair2);
        var t1b = ToneDerivationService.Derive(pair1);

        t1a.Should().Be(t1b, because: "chamadas intermediárias não devem afetar o resultado");
    }

    [Fact]
    public void DerivedTones_IsImmutableRecord()
    {
        var type = typeof(DerivedTones);
        // Record positional: propriedades só podem ter init setter (IsExternalInit) ou nenhum setter
        // Não pode haver setter público regular (Set sem IsExternalInit)
        foreach (var prop in type.GetProperties())
        {
            var setter = prop.SetMethod;
            if (setter is null) continue;

            // Init setters têm o modificador IsExternalInit obrigatório
            var hasInitModifier = setter.ReturnParameter
                .GetRequiredCustomModifiers()
                .Any(m => m.FullName?.Contains("IsExternalInit") == true);

            var isPrivate = setter.IsPrivate;

            (hasInitModifier || isPrivate).Should().BeTrue(
                because: $"propriedade {prop.Name} não deve ter setter público regular em DerivedTones");
        }
    }

    // ──────────────────────────────────────────────
    // PBT-05 — determinismo: derive(c) == derive(c)
    // ──────────────────────────────────────────────

    /// <summary>
    /// PBT-05: ToneDerivationService é determinístico: para qualquer par de cores,
    /// múltiplas chamadas retornam exatamente o mesmo resultado.
    /// </summary>
    [Property(MaxTest = 200, DisplayName = "PBT-05: Derive idempotente")]
    public Property Pbt05_DeriveIsDeterministic()
    {
        var hexChars = "0123456789ABCDEF";
        var gen = Gen.Choose(0, 15)
            .ArrayOf(6)
            .Select(digits => "#" + new string(digits.Select(d => hexChars[d]).ToArray()))
            .SelectMany(p => Gen.Choose(0, 15)
                .ArrayOf(6)
                .Select(digits => "#" + new string(digits.Select(d => hexChars[d]).ToArray()))
                .Select(s => (Primary: p, Secondary: s)));

        return Prop.ForAll(Arb.From(gen), pair =>
        {
            var colorPair = ColorPair.Create(pair.Primary, pair.Secondary);
            if (colorPair.IsFailure) return true;

            var t1 = ToneDerivationService.Derive(colorPair.Value);
            var t2 = ToneDerivationService.Derive(colorPair.Value);
            var t3 = ToneDerivationService.Derive(colorPair.Value);

            t1.Should().Be(t2, because: "primeira e segunda chamada devem ser iguais");
            t2.Should().Be(t3, because: "segunda e terceira chamada devem ser iguais");

            return true;
        });
    }
}
