using AccountManagement.Domain.Accounts.Services;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace AccountManagement.Domain.Tests.Accounts.Services;

/// <summary>
/// Testes unitários e de propriedade para o domain service <see cref="NameNormalizer"/>.
///
/// PBT-01: idempotência — normalize(normalize(x)) == normalize(x) para qualquer string Unicode.
/// PBT-02: equivalência por forma normalizada — nomes que diferem apenas por
///         acento/caixa/espaço/pontuação produzem forma normalizada idêntica.
///
/// Mapeia: design §4.3, TASK-02 ST-01 e ST-02, Req 1.1, DD-005, PBT-01, PBT-02.
/// </summary>
public sealed class NameNormalizerTests
{
    private static readonly NameNormalizer Normalizer = new();

    // =========================================================================
    // Testes determinísticos — exemplos concretos
    // =========================================================================

    [Theory(DisplayName = "Normaliza nomes com acentos, caixa, pontuação e espaços")]
    [InlineData("PAG.AI", "pagai")]
    [InlineData("Pag.ai", "pagai")]
    [InlineData("pag ai", "pag ai")]
    [InlineData("  Pãg.Ái  ", "pagai")]
    [InlineData("AZIM CRM", "azim crm")]
    [InlineData("Azim  CRM", "azim crm")]
    [InlineData("São Paulo", "sao paulo")]
    [InlineData("ABC---XYZ", "abcxyz")]
    public void Normalize_ProducesExpectedOutput(string input, string expected)
    {
        var result = Normalizer.Normalize(input);

        result.Should().Be(expected);
    }

    [Theory(DisplayName = "Nomes equivalentes produzem forma normalizada idêntica")]
    [InlineData("PAG.AI", "Pag.ai")]
    [InlineData("Sao Paulo", "São Paulo")]
    [InlineData("AZIM", "azim")]
    [InlineData("Côté", "cote")]
    public void Normalize_EquivalentNames_ProduceSameForm(string a, string b)
    {
        var normalizedA = Normalizer.Normalize(a);
        var normalizedB = Normalizer.Normalize(b);

        normalizedA.Should().Be(normalizedB);
    }

    [Fact(DisplayName = "Normalização de string nula retorna string vazia")]
    public void Normalize_NullInput_ReturnsEmpty()
    {
        var result = Normalizer.Normalize(null!);

        result.Should().BeEmpty();
    }

    [Fact(DisplayName = "Normalização de string vazia retorna string vazia")]
    public void Normalize_EmptyInput_ReturnsEmpty()
    {
        var result = Normalizer.Normalize("");

        result.Should().BeEmpty();
    }

    [Fact(DisplayName = "Normalização de somente pontuação retorna string vazia")]
    public void Normalize_OnlyPunctuation_ReturnsEmpty()
    {
        var result = Normalizer.Normalize("!@#$%^&*()");

        result.Should().BeEmpty();
    }

    // =========================================================================
    // PBT-01 — Idempotência da normalização
    // Mapeia: requirements PBT-01, design §4.3, design §13, TASK-02 ST-02.
    // =========================================================================

    /// <summary>
    /// PBT-01: normalize(normalize(x)) == normalize(x) para qualquer string não-nula gerada.
    ///
    /// Cobre a propriedade de idempotência do <see cref="NameNormalizer"/> (DD-005).
    /// FsCheck.Xunit 3.x com [Property] aceita função retornando bool diretamente.
    /// Mínimo de 1 000 amostras (MaxTest = 1000).
    ///
    /// Mapeia: requirements PBT-01, design §4.3, TASK-02 ST-02.
    /// </summary>
    [Property(
        DisplayName = "PBT-01: normalize(normalize(x)) == normalize(x) para qualquer string Unicode",
        MaxTest = 1000)]
    public bool PBT01_Normalization_IsIdempotent(NonNull<string> input)
    {
        var value = input.Item;
        var once = Normalizer.Normalize(value);
        var twice = Normalizer.Normalize(once);

        return once == twice;
    }

    // =========================================================================
    // PBT-02 — Equivalência por forma normalizada
    // Mapeia: requirements PBT-02, design §4.3, design §13, TASK-02 ST-02.
    // =========================================================================

    /// <summary>
    /// PBT-02: nomes que diferem apenas por caixa (upper/lower) produzem forma normalizada idêntica.
    ///
    /// Gera strings não-nulas arbitrárias e verifica que upper e lower produzem a mesma forma.
    /// Mínimo de 1 000 amostras (MaxTest = 1000).
    ///
    /// Mapeia: requirements PBT-02, design §4.3, TASK-02 ST-02, DD-005.
    /// </summary>
    [Property(
        DisplayName = "PBT-02: forma normalizada é a mesma independente de caixa",
        MaxTest = 1000)]
    public bool PBT02_Normalization_IsCaseInsensitive(NonNull<string> input)
    {
        var value = input.Item;
        var lower = Normalizer.Normalize(value.ToLowerInvariant());
        var upper = Normalizer.Normalize(value.ToUpperInvariant());

        return lower == upper;
    }

    /// <summary>
    /// PBT-02 complementar: espaços adicionais ao redor do texto não alteram a forma normalizada.
    ///
    /// Verifica que o colapso de espaços e o trim são idempotentes com qualquer entrada.
    /// Mínimo de 1 000 amostras (MaxTest = 1000).
    ///
    /// Mapeia: requirements PBT-02, design §4.3, TASK-02 ST-02, DD-005.
    /// </summary>
    [Property(
        DisplayName = "PBT-02: espaços adicionais nas bordas não alteram a forma normalizada",
        MaxTest = 1000)]
    public bool PBT02_Normalization_CollapsesLeadingAndTrailingSpaces(NonNull<string> input)
    {
        var value = input.Item;
        var withSpaces = "   " + value + "   ";
        var normal = Normalizer.Normalize(value);
        var spacedNormal = Normalizer.Normalize(withSpaces);

        return normal == spacedNormal;
    }
}
