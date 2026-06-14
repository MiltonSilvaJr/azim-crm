using DataMigration.Domain.ValueObjects;
using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataMigration.Domain.Tests.ValueObjects;

/// <summary>
/// Testes unitários do objeto de valor <see cref="ExcelSerialDate"/>.
///
/// Cobre: TASK-04, Req 9, PBT-03, design §4.3, DD-006.
/// Epoch Excel: 1899-12-30 (acomoda o bug do falso ano bissexto de 1900).
/// </summary>
public sealed class ExcelSerialDateTests
{
    // =========================================================================
    // Casos conhecidos de conversão serial → ISO
    // Epoch: 1899-12-30 (DD-006)
    // =========================================================================

    [Theory(DisplayName = "FromSerial deve converter seriais conhecidos corretamente (epoch 1899-12-30)")]
    // Epoch 1899-12-30: serial N → 1899-12-30 + N dias (DD-006)
    [InlineData(1,     1899, 12, 31)]  // serial 1 = 1899-12-31
    [InlineData(2,     1900,  1,  1)]  // serial 2 = 1900-01-01
    [InlineData(60,    1900,  2, 28)]  // serial 60 = 1900-02-28 (Excel trata como 29-fev fictício)
    [InlineData(61,    1900,  3,  1)]  // serial 61 = 1900-03-01 (após o falso 29-fev)
    [InlineData(45000, 2023,  3, 15)]  // serial 45000 = 2023-03-15
    [InlineData(44927, 2023,  1,  1)]  // serial 44927 = 2023-01-01
    [InlineData(45292, 2024,  1,  1)]  // serial 45292 = 2024-01-01
    public void FromSerial_ShouldConvert_KnownSerials(
        int serial, int year, int month, int day)
    {
        var result = ExcelSerialDate.FromSerial(serial);

        result.Should().NotBeNull();
        result!.Value.Year.Should().Be(year);
        result.Value.Month.Should().Be(month);
        result.Value.Day.Should().Be(day);
    }

    [Fact(DisplayName = "FromSerial(null) deve retornar null sem exceção")]
    public void FromSerial_Null_ShouldReturn_Null()
    {
        var result = ExcelSerialDate.FromSerial(null);
        result.Should().BeNull();
    }

    [Fact(DisplayName = "FromSerial(0) deve retornar null (serial 0 é célula vazia no Excel)")]
    public void FromSerial_Zero_ShouldReturn_Null()
    {
        var result = ExcelSerialDate.FromSerial(0);
        result.Should().BeNull();
    }

    // =========================================================================
    // Flag IsOverdue (design §4.3, Req 9)
    // =========================================================================

    [Fact(DisplayName = "IsOverdue deve ser true para datas no passado")]
    public void IsOverdue_ShouldBeTrue_ForPastDates()
    {
        // serial 1 = 1900-01-01 — definitivamente no passado
        var result = ExcelSerialDate.FromSerial(1, today: new DateOnly(2026, 1, 1));
        result!.IsOverdue.Should().BeTrue();
    }

    [Fact(DisplayName = "IsOverdue deve ser false para datas no futuro")]
    public void IsOverdue_ShouldBeFalse_ForFutureDates()
    {
        // serial 45000 = 2023-03-09; se hoje for 2020-01-01, é futuro
        var result = ExcelSerialDate.FromSerial(45000, today: new DateOnly(2020, 1, 1));
        result!.IsOverdue.Should().BeFalse();
    }

    [Fact(DisplayName = "IsOverdue deve ser true quando data de referência é posterior à data")]
    public void IsOverdue_ShouldBeTrue_WhenTodayAfterDate()
    {
        var today = new DateOnly(2026, 1, 1);
        // serial 45658 ≈ 2025-01-01; como 2026-01-01 > 2025-01-01, é vencida
        var result = ExcelSerialDate.FromSerial(45658, today: today);
        result!.IsOverdue.Should().BeTrue();
    }

    // =========================================================================
    // Igualdade por valor
    // =========================================================================

    [Fact(DisplayName = "ExcelSerialDate com mesmo serial deve ter igualdade por valor")]
    public void ExcelSerialDate_SameSerial_ShouldBeEqual()
    {
        var a = ExcelSerialDate.FromSerial(45000);
        var b = ExcelSerialDate.FromSerial(45000);
        a.Should().Be(b);
    }

    [Fact(DisplayName = "ExcelSerialDate com seriais diferentes deve ser diferente")]
    public void ExcelSerialDate_DifferentSerial_ShouldNotBeEqual()
    {
        var a = ExcelSerialDate.FromSerial(45000);
        var b = ExcelSerialDate.FromSerial(45001);
        a.Should().NotBe(b);
    }

    // =========================================================================
    // ToSerial (round-trip)
    // =========================================================================

    [Theory(DisplayName = "ToSerial deve reproduzir o serial original (round-trip)")]
    [InlineData(1)]
    [InlineData(61)]
    [InlineData(45000)]
    [InlineData(44927)]
    public void ToSerial_ShouldRoundTrip_KnownSerials(int serial)
    {
        var date = ExcelSerialDate.FromSerial(serial)!;
        date.ToSerial().Should().Be(serial);
    }

    // =========================================================================
    // PBT-03 — Round-trip serial → ISO → serial
    // Propriedade: FromSerial(s).ToSerial() == s para qualquer serial válido (1..99999)
    // Mapeia: PBT-03, design §4.3, DD-006, TASK-04.
    // =========================================================================

    /// <summary>
    /// PBT-03: para qualquer serial válido (1..99999),
    /// o round-trip <c>serial → ISO → serial</c> reproduz o serial original.
    ///
    /// Rastreia: PBT-03, design §4.3, DD-006.
    /// </summary>
    [Property(
        DisplayName = "PBT-03: round-trip serial → ISO → serial é determinístico",
        MaxTest = 200)]
    public Property Pbt03_RoundTrip_SerialToIsoToSerial(PositiveInt rawSerial)
    {
        // Limita ao range realista (1..99999 ~ ano 2173)
        var serial = (rawSerial.Get % 99_999) + 1;
        var date = ExcelSerialDate.FromSerial(serial);
        if (date is null)
        {
            return Prop.ToProperty(false).Label($"serial={serial} retornou null (não esperado)");
        }

        var roundTripped = date.ToSerial();
        return Prop.Label(
            roundTripped == serial,
            $"serial={serial}, roundTripped={roundTripped}");
    }

    /// <summary>
    /// PBT-03 (complementar): FromSerial(null) e FromSerial(0) nunca lançam exceção.
    /// </summary>
    [Property(
        DisplayName = "PBT-03: FromSerial com serial nulo/zero nunca lança exceção",
        MaxTest = 100)]
    public Property Pbt03_FromSerial_Null_Or_Zero_NeverThrows(bool useNull)
    {
        try
        {
            if (useNull)
            {
                _ = ExcelSerialDate.FromSerial(null);
            }
            else
            {
                _ = ExcelSerialDate.FromSerial(0);
            }

            return Prop.ToProperty(true);
        }
#pragma warning disable CA1031 // Intencionalmente amplo para PBT
        catch (Exception ex)
        {
            return Prop.Label(false, $"Lançou exceção: {ex.GetType().Name}: {ex.Message}");
        }
#pragma warning restore CA1031
    }
}
