using Digest.Domain.Enums;
using Digest.Domain.Policies;
using FsCheck;
using FsCheck.Xunit;
using NodaTime;

namespace Digest.Domain.Tests.Policies;

/// <summary>
/// Testes unitários e PBT-03 para <see cref="RecipientSelectionPolicy"/> (TASK-07).
/// Valida: totalidade da policy, regras de inclusão/exclusão por papel, opt-out, dia da semana.
/// </summary>
public sealed class RecipientSelectionPolicyTests
{
    // ---------------------------------------------------------------
    // Testes nominais — regras explícitas (design §4.6)
    // ---------------------------------------------------------------

    [Fact]
    public void Inactive_user_never_receives()
    {
        var result = RecipientSelectionPolicy.Evaluate(
            papel: RecipientPapel.Vendedor,
            hasOwnPendencias: true,
            optOut: false,
            weekday: IsoDayOfWeek.Monday,
            active: false);

        result.ShouldReceivePendencias.Should().BeFalse();
        result.ShouldReceiveAzimute.Should().BeFalse();
    }

    [Fact]
    public void Viewer_never_receives()
    {
        var result = RecipientSelectionPolicy.Evaluate(
            papel: RecipientPapel.Viewer,
            hasOwnPendencias: true,
            optOut: false,
            weekday: IsoDayOfWeek.Monday,
            active: true);

        result.ShouldReceivePendencias.Should().BeFalse();
        result.ShouldReceiveAzimute.Should().BeFalse();
    }

    [Fact]
    public void Vendedor_with_pendencias_and_no_optout_receives_pendencias()
    {
        var result = RecipientSelectionPolicy.Evaluate(
            papel: RecipientPapel.Vendedor,
            hasOwnPendencias: true,
            optOut: false,
            weekday: IsoDayOfWeek.Wednesday,
            active: true);

        result.ShouldReceivePendencias.Should().BeTrue();
        result.ShouldReceiveAzimute.Should().BeFalse();
    }

    [Fact]
    public void Vendedor_with_pendencias_and_optout_receives_nothing()
    {
        var result = RecipientSelectionPolicy.Evaluate(
            papel: RecipientPapel.Vendedor,
            hasOwnPendencias: true,
            optOut: true,
            weekday: IsoDayOfWeek.Wednesday,
            active: true);

        result.ShouldReceivePendencias.Should().BeFalse();
        result.ShouldReceiveAzimute.Should().BeFalse();
    }

    [Fact]
    public void Vendedor_without_pendencias_never_receives()
    {
        var result = RecipientSelectionPolicy.Evaluate(
            papel: RecipientPapel.Vendedor,
            hasOwnPendencias: false,
            optOut: false,
            weekday: IsoDayOfWeek.Wednesday,
            active: true);

        result.ShouldReceivePendencias.Should().BeFalse();
        result.ShouldReceiveAzimute.Should().BeFalse();
    }

    [Fact]
    public void GestorBU_on_monday_without_pendencias_receives_azimute()
    {
        var result = RecipientSelectionPolicy.Evaluate(
            papel: RecipientPapel.GestorBU,
            hasOwnPendencias: false,
            optOut: false,
            weekday: IsoDayOfWeek.Monday,
            active: true);

        result.ShouldReceivePendencias.Should().BeFalse();
        result.ShouldReceiveAzimute.Should().BeTrue();
    }

    [Fact]
    public void TAdmin_on_monday_receives_azimute_even_with_optout()
    {
        var result = RecipientSelectionPolicy.Evaluate(
            papel: RecipientPapel.TAdmin,
            hasOwnPendencias: false,
            optOut: true,
            weekday: IsoDayOfWeek.Monday,
            active: true);

        result.ShouldReceivePendencias.Should().BeFalse();
        result.ShouldReceiveAzimute.Should().BeTrue("opt-out não suprime azimute para gestão (Req 10.2)");
    }

    [Fact]
    public void GestorBU_on_tuesday_without_pendencias_receives_nothing()
    {
        var result = RecipientSelectionPolicy.Evaluate(
            papel: RecipientPapel.GestorBU,
            hasOwnPendencias: false,
            optOut: false,
            weekday: IsoDayOfWeek.Tuesday,
            active: true);

        result.ShouldReceivePendencias.Should().BeFalse();
        result.ShouldReceiveAzimute.Should().BeFalse();
    }

    [Fact]
    public void GestorBU_on_monday_with_pendencias_receives_both()
    {
        var result = RecipientSelectionPolicy.Evaluate(
            papel: RecipientPapel.GestorBU,
            hasOwnPendencias: true,
            optOut: false,
            weekday: IsoDayOfWeek.Monday,
            active: true);

        result.ShouldReceivePendencias.Should().BeTrue();
        result.ShouldReceiveAzimute.Should().BeTrue();
    }

    [Fact]
    public void GestorBU_on_saturday_never_receives_azimute()
    {
        // Sábado: mesmo gestor não deve receber
        var result = RecipientSelectionPolicy.Evaluate(
            papel: RecipientPapel.GestorBU,
            hasOwnPendencias: false,
            optOut: false,
            weekday: IsoDayOfWeek.Saturday,
            active: true);

        result.ShouldReceiveAzimute.Should().BeFalse();
    }

    // ---------------------------------------------------------------
    // PBT-03: policy é total — nenhuma combinação produz resultado indefinido
    // e as três propriedades são sempre satisfeitas:
    //   P1: inativo ou Viewer → nunca recebe
    //   P2: pendência ∧ ¬optOut ∧ ativo ∧ ¬Viewer → recebe pendências
    //   P3: segunda ∧ gestão ∧ ativo → recebe azimute (opt-out não suprime)
    // ---------------------------------------------------------------

    [Property(MaxTest = 500, Arbitrary = new[] { typeof(RecipientArbitraries) })]
    public Property PBT03_policy_is_total_and_correct(PolicyInput input)
    {
        var result = RecipientSelectionPolicy.Evaluate(
            papel: input.Papel,
            hasOwnPendencias: input.HasOwnPendencias,
            optOut: input.OptOut,
            weekday: input.Weekday,
            active: input.Active);

        // P1: inativo ou Viewer → nunca recebe nada
        if (!input.Active || input.Papel == RecipientPapel.Viewer)
            return (!result.ShouldReceivePendencias && !result.ShouldReceiveAzimute).ToProperty();

        var isGestao = input.Papel is RecipientPapel.GestorBU or RecipientPapel.TAdmin;
        var isMonday = input.Weekday == IsoDayOfWeek.Monday;

        // P2: pendência ∧ ¬optOut → recebe pendências
        var expectedPendencias = input.HasOwnPendencias && !input.OptOut;

        // P3: segunda ∧ gestão → recebe azimute (opt-out irrelevante para azimute)
        var expectedAzimute = isMonday && isGestao;

        return (result.ShouldReceivePendencias == expectedPendencias
                && result.ShouldReceiveAzimute == expectedAzimute).ToProperty();
    }
}

// ---------------------------------------------------------------------------
// Arbitraries para PBT-03
// ---------------------------------------------------------------------------

/// <summary>Entrada gerada arbitrariamente para PBT-03.</summary>
public record PolicyInput(
    RecipientPapel Papel,
    bool HasOwnPendencias,
    bool OptOut,
    IsoDayOfWeek Weekday,
    bool Active);

/// <summary>Geradores FsCheck para PBT-03.</summary>
public static class RecipientArbitraries
{
    public static Arbitrary<PolicyInput> ArbitraryPolicyInput()
    {
        var papeis = (RecipientPapel[])Enum.GetValues(typeof(RecipientPapel));
        // Todos os dias (incluindo fins de semana para verificar que gestor não recebe azimute)
        var weekdays = new[]
        {
            IsoDayOfWeek.Monday,
            IsoDayOfWeek.Tuesday,
            IsoDayOfWeek.Wednesday,
            IsoDayOfWeek.Thursday,
            IsoDayOfWeek.Friday,
            IsoDayOfWeek.Saturday,
            IsoDayOfWeek.Sunday,
        };

        var genPapel = Gen.Elements(papeis);
        var genWeekday = Gen.Elements(weekdays);
        var genBool = Arb.Default.Bool().Generator;

        var gen = from papel in genPapel
                  from pendencias in genBool
                  from optOut in genBool
                  from weekday in genWeekday
                  from active in genBool
                  select new PolicyInput(papel, pendencias, optOut, weekday, active);

        return Arb.From(gen);
    }
}
