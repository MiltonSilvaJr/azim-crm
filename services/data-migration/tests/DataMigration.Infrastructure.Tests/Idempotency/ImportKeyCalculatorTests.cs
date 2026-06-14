using DataMigration.Infrastructure.Idempotency;
using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DataMigration.Infrastructure.Tests.Idempotency;

/// <summary>
/// Testes unitários e de propriedade (PBT-02) para <see cref="ImportKeyCalculator"/>.
///
/// Cobre:
/// - Determinismo: mesmos inputs → mesma chave (PBT-02).
/// - Formato: hex lowercase de 64 caracteres (SHA-256).
/// - Sensibilidade: qualquer mudança de input → chave diferente.
///
/// Rastreia: TASK-19, DD-003, PBT-02, design §6.5.
/// </summary>
public sealed class ImportKeyCalculatorTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // =========================================================================
    // Testes unitários — determinismo e formato
    // =========================================================================

    [Fact]
    public void Calculate_MesmosInputs_RetornaMesmaChave()
    {
        // Act
        var key1 = ImportKeyCalculator.Calculate(TenantId, "Pipeline", 1, "XPTO|AZ-0095|100000");
        var key2 = ImportKeyCalculator.Calculate(TenantId, "Pipeline", 1, "XPTO|AZ-0095|100000");

        // Assert
        key1.Should().Be(key2, "determinística: mesmos inputs → mesma chave (DD-003)");
    }

    [Fact]
    public void Calculate_RetornaHexLower64Chars()
    {
        // Act
        var key = ImportKeyCalculator.Calculate(TenantId, "Pipeline", 0, "payload");

        // Assert
        key.Should().HaveLength(64, "SHA-256 produz 32 bytes = 64 hex chars");
        key.Should().MatchRegex("^[0-9a-f]{64}$", "hex lowercase");
    }

    [Fact]
    public void Calculate_TenantDiferente_RetornaChaveDiferente()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // Act
        var keyA = ImportKeyCalculator.Calculate(tenantA, "Pipeline", 1, "payload");
        var keyB = ImportKeyCalculator.Calculate(tenantB, "Pipeline", 1, "payload");

        // Assert
        keyA.Should().NotBe(keyB, "tenant diferente → chave diferente");
    }

    [Fact]
    public void Calculate_SheetDiferente_RetornaChaveDiferente()
    {
        // Act
        var key1 = ImportKeyCalculator.Calculate(TenantId, "Pipeline", 1, "payload");
        var key2 = ImportKeyCalculator.Calculate(TenantId, "Ações Comerciais", 1, "payload");

        // Assert
        key1.Should().NotBe(key2, "sheet diferente → chave diferente");
    }

    [Fact]
    public void Calculate_RowIndexDiferente_RetornaChaveDiferente()
    {
        // Act
        var key1 = ImportKeyCalculator.Calculate(TenantId, "Pipeline", 1, "payload");
        var key2 = ImportKeyCalculator.Calculate(TenantId, "Pipeline", 2, "payload");

        // Assert
        key1.Should().NotBe(key2, "rowIndex diferente → chave diferente");
    }

    [Fact]
    public void Calculate_PayloadDiferente_RetornaChaveDiferente()
    {
        // Act
        var key1 = ImportKeyCalculator.Calculate(TenantId, "Pipeline", 1, "XPTO|AZ-0095|100000");
        var key2 = ImportKeyCalculator.Calculate(TenantId, "Pipeline", 1, "XPTO|AZ-0096|100000");

        // Assert
        key1.Should().NotBe(key2, "payload diferente → chave diferente");
    }

    [Fact]
    public void Calculate_SourceSheetVazio_LancaArgumentException()
    {
        // Act
        var act = () => ImportKeyCalculator.Calculate(TenantId, "", 1, "payload");

        // Assert
        act.Should().Throw<ArgumentException>("sourceSheet vazio é inválido");
    }

    [Fact]
    public void Calculate_PayloadVazio_LancaArgumentException()
    {
        // Act
        var act = () => ImportKeyCalculator.Calculate(TenantId, "Pipeline", 1, "");

        // Assert
        act.Should().Throw<ArgumentException>("payload vazio é inválido");
    }

    // =========================================================================
    // PBT-02 — Propriedade: determinismo para qualquer rowIndex
    // =========================================================================

    /// <summary>
    /// PBT-02: Para qualquer rowIndex não-negativo, calcular a mesma chave duas vezes
    /// deve retornar o mesmo resultado (determinismo garantido pelo SHA-256).
    /// </summary>
    [Property(MaxTest = 200, QuietOnSuccess = true)]
    public Property Pbt02_ImportKey_Deterministica(NonNegativeInt rowIndex)
    {
        var key1 = ImportKeyCalculator.Calculate(TenantId, "Pipeline", rowIndex.Get, "payload-fixo");
        var key2 = ImportKeyCalculator.Calculate(TenantId, "Pipeline", rowIndex.Get, "payload-fixo");

        return (key1 == key2).ToProperty()
            .Label($"determinismo falhou para rowIndex={rowIndex.Get}");
    }

    /// <summary>
    /// PBT: Para quaisquer dois rowIndexes distintos, as chaves geradas devem ser diferentes.
    /// Garante que linhas diferentes não colidem (sem duplicação em reexecução).
    /// </summary>
    [Property(MaxTest = 100, QuietOnSuccess = true)]
    public Property Pbt_ImportKey_RowsDiferentes_ChavesDiferentes(
        NonNegativeInt row1,
        NonNegativeInt row2)
    {
        if (row1.Get == row2.Get)
        {
            return true.ToProperty(); // inputs iguais não testam este invariante.
        }

        var key1 = ImportKeyCalculator.Calculate(TenantId, "Pipeline", row1.Get, "payload-fixo");
        var key2 = ImportKeyCalculator.Calculate(TenantId, "Pipeline", row2.Get, "payload-fixo");

        return (key1 != key2).ToProperty()
            .Label($"colisão detectada entre row={row1.Get} e row={row2.Get}");
    }
}
