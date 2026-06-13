using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using TenantAdministration.Domain.Policies;
using TenantAdministration.Domain.ValueObjects;
using Xunit;

namespace TenantAdministration.Domain.Tests.Policies;

/// <summary>
/// Testes para WcagContrastPolicy.
/// Cobre TASK-03: PBT-04 e casos de limítrofe.
/// </summary>
public sealed class WcagContrastPolicyTests
{
    private static ColorPair MakePair(string primary, string secondary) =>
        ColorPair.Create(primary, secondary).Value;

    // ──────────────────────────────────────────────
    // Limítrofes determinísticos (DD-003: round(R,2) >= 4.50)
    // ──────────────────────────────────────────────

    [Fact]
    public void Check_BlackOnWhite_Approved()
    {
        // Razão: 21:1
        var pair = MakePair("#000000", "#FFFFFF");
        var (approved, ratio) = WcagContrastPolicy.Check(pair);
        approved.Should().BeTrue();
        ratio.Should().BeGreaterThanOrEqualTo(4.50m);
    }

    [Fact]
    public void Check_WhiteOnWhite_Rejected()
    {
        var pair = MakePair("#FFFFFF", "#FFFFFF");
        var (approved, ratio) = WcagContrastPolicy.Check(pair);
        approved.Should().BeFalse();
        ratio.Should().BeLessThan(4.50m);
    }

    /// <summary>
    /// Caso limítrofe: #777777 sobre #FFFFFF = round(4.478,2) = 4.48 — reprova conforme DD-003.
    /// </summary>
    [Fact]
    public void Check_Ratio_4_48_Rejected()
    {
        // #777777 sobre #FFFFFF ≈ 4.478, round a 2 casas = 4.48 — deve reprovar
        var pair = MakePair("#777777", "#FFFFFF");
        var (approved, ratio) = WcagContrastPolicy.Check(pair);
        approved.Should().BeFalse(because: $"round({ratio},2) = {Math.Round(ratio,2)} < 4.50 deve reprovar");
        Math.Round(ratio, 2).Should().BeLessThan(4.50m);
    }

    /// <summary>
    /// Caso limítrofe: #767676 sobre #FFFFFF = round(4.542,2) = 4.54 — aprova conforme DD-003.
    /// </summary>
    [Fact]
    public void Check_Ratio_4_54_Approved()
    {
        // #767676 sobre #FFFFFF ≈ 4.542, round a 2 casas = 4.54 — deve aprovar
        var pair = MakePair("#767676", "#FFFFFF");
        var (approved, ratio) = WcagContrastPolicy.Check(pair);
        approved.Should().BeTrue(because: $"round({ratio},2) = {Math.Round(ratio,2)} >= 4.50 deve aprovar");
    }

    [Fact]
    public void Check_DarkGrayOnWhite_Approved()
    {
        // #595959 sobre #FFFFFF: L ≈ 0.1022, R = (1+0.05)/(0.1022+0.05) ≈ 6.9 — aprova
        var pair = MakePair("#595959", "#FFFFFF");
        var (approved, ratio) = WcagContrastPolicy.Check(pair);
        approved.Should().BeTrue();
        ratio.Should().BeGreaterThanOrEqualTo(4.50m);
    }

    [Fact]
    public void Check_DoesNotUseIO_OrState()
    {
        // WcagContrastPolicy é estática/pura — não deve lançar para qualquer ColorPair válido
        var pair = MakePair("#1A73E8", "#FFFFFF");
        var act = () => WcagContrastPolicy.Check(pair);
        act.Should().NotThrow();
    }

    // ──────────────────────────────────────────────
    // PBT-04 — aprovação ⟺ round(R,2) >= 4.50
    // ──────────────────────────────────────────────

    /// <summary>
    /// PBT-04: Para qualquer par de cores válido, a aprovação retornada pela policy
    /// deve ser consistente com round(contrastRatio, 2) >= 4.50.
    /// </summary>
    [Property(MaxTest = 300, DisplayName = "PBT-04: Aprovação ↔ round(R,2) ≥ 4.50")]
    public Property Pbt04_ApprovalConsistentWithRoundedRatio()
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

            var (approved, ratio) = WcagContrastPolicy.Check(colorPair.Value);
            var rounded = Math.Round(ratio, 2);

            if (rounded >= 4.50m)
                approved.Should().BeTrue(because: $"round({ratio},2)={rounded} >= 4.50 deve aprovar");
            else
                approved.Should().BeFalse(because: $"round({ratio},2)={rounded} < 4.50 deve reprovar");

            return true;
        });
    }
}
