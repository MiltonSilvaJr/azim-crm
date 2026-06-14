using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using PartnerManagement.Domain.Partners.Exceptions;
using PartnerManagement.Domain.Partners.ValueObjects;
using Xunit;

namespace PartnerManagement.Domain.Tests.Partners.PropertyTests;

/// <summary>
/// PBT-03: round-trip e domínio dos percentuais de comissão.
/// Propriedades:
/// - Para todo percentual válido em [0,00; 100,00] (gerado como centésimos inteiros),
///   criar e recuperar o valor preserva o valor exato sem erro de arredondamento.
/// - Todo valor fora do intervalo é rejeitado com <see cref="PercentageOutOfRangeException"/>.
/// Nenhum float/double nos geradores nem na implementação (RNF 6.3, DD-004).
/// Mapeia: PBT-03, Req 6.5, RNF 6.2, design §4.3, TASK-08.
/// </summary>
[Trait("Category", "PBT")]
public sealed class PercentageRoundTripPbt
{
    // =========================================================================
    // PBT-03a: round-trip para valores válidos
    // Gerador: inteiros de 0 a 10000 representando centésimos (0,00 a 100,00).
    // Sem float/double.
    // =========================================================================

    [Property(MaxTest = 500, Arbitrary = new[] { typeof(ValidPercentageCentsArb) },
        DisplayName = "PBT-03a: percentual válido → round-trip exato sem perda")]
    public bool ValidPercentage_RoundTrip_PreservesExactValue(ValidPercentageCents wrapper)
    {
        // Converte centésimos inteiros para decimal com 2 casas (sem float)
        decimal value = wrapper.Cents / 100m;
        Percentage pct = Percentage.Create(value);
        return pct.Value == value;
    }

    // =========================================================================
    // PBT-03b: valores fora do intervalo sempre rejeitados
    // =========================================================================

    [Property(MaxTest = 300, Arbitrary = new[] { typeof(OutOfRangePercentageCentsArb) },
        DisplayName = "PBT-03b: percentual fora do intervalo → sempre rejeitado")]
    public bool OutOfRangePercentage_AlwaysRejected(OutOfRangePercentageCents wrapper)
    {
        decimal value = wrapper.Cents / 100m;

        try
        {
            Percentage.Create(value);
            return false; // não devia ter sido aceito
        }
        catch (PercentageOutOfRangeException)
        {
            return true; // esperado
        }
    }

    // =========================================================================
    // PBT-03c: CommissionDefaults round-trip de ambos os percentuais
    // =========================================================================

    [Property(MaxTest = 300, Arbitrary = new[] { typeof(ValidPercentageCentsArb) },
        DisplayName = "PBT-03c: CommissionDefaults — round-trip de ambos os percentuais")]
    public bool CommissionDefaults_BothPercentages_RoundTrip(ValidPercentageCents setupWrapper, ValidPercentageCents recorrenteWrapper)
    {
        decimal setup = setupWrapper.Cents / 100m;
        decimal recorrente = recorrenteWrapper.Cents / 100m;

        CommissionDefaults defaults = CommissionDefaults.Create(
            Percentage.Create(setup),
            Percentage.Create(recorrente));

        return defaults.PctSetup.Value == setup && defaults.PctRecorrente.Value == recorrente;
    }

    // =========================================================================
    // PBT-03d: extremos do intervalo sempre aceitos
    // =========================================================================

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(BoundaryPercentageCentsArb) },
        DisplayName = "PBT-03d: extremos do intervalo (0,00 e 100,00) sempre aceitos")]
    public bool BoundaryValues_AreAlwaysAccepted(BoundaryPercentageCents wrapper)
    {
        decimal value = wrapper.Cents / 100m;

        try
        {
            Percentage pct = Percentage.Create(value);
            return pct.Value == value;
        }
        catch (PercentageOutOfRangeException)
        {
            return false; // extremo não deveria ser rejeitado
        }
    }
}

// =========================================================================
// Tipos wrapper (evitam conflito de Arbitrary para o mesmo tipo primitivo)
// =========================================================================

/// <summary>Wrapper para centésimos de percentual válido [0; 10000].</summary>
public sealed record ValidPercentageCents(int Cents);

/// <summary>Wrapper para centésimos de percentual fora do intervalo válido.</summary>
public sealed record OutOfRangePercentageCents(int Cents);

/// <summary>Wrapper para centésimos nos extremos do intervalo.</summary>
public sealed record BoundaryPercentageCents(int Cents);

// =========================================================================
// Arbitrary providers (API FsCheck 3.x: FsCheck.Fluent.Gen e FsCheck.Fluent.Arb)
// Sem float/double.
// =========================================================================

/// <summary>Gera centésimos válidos: inteiros no intervalo [0; 10000] → [0,00; 100,00].</summary>
public static class ValidPercentageCentsArb
{
    public static Arbitrary<ValidPercentageCents> Generate() =>
        Gen.Choose(0, 10000)
            .Select(c => new ValidPercentageCents(c))
            .ToArbitrary();
}

/// <summary>Gera centésimos fora do intervalo: inteiros negativos ou acima de 10000.</summary>
public static class OutOfRangePercentageCentsArb
{
    public static Arbitrary<OutOfRangePercentageCents> Generate() =>
        Gen.OneOf(
                Gen.Choose(-10000, -1),
                Gen.Choose(10001, 20000))
            .Select(c => new OutOfRangePercentageCents(c))
            .ToArbitrary();
}

/// <summary>Gera centésimos nos extremos do intervalo: 0, 1, 9999, 10000.</summary>
public static class BoundaryPercentageCentsArb
{
    private static readonly int[] _boundaries = [0, 1, 9999, 10000];

    public static Arbitrary<BoundaryPercentageCents> Generate() =>
        Gen.Elements(_boundaries)
            .Select(c => new BoundaryPercentageCents(c))
            .ToArbitrary();
}
