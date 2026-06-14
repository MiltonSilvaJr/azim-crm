namespace ActivityManagement.Infrastructure.Tests.Security;

using ActivityManagement.Infrastructure.Security;
using FluentAssertions;
using Xunit;

/// <summary>
/// Testes de segurança para comparação de token em tempo constante (TASK-23, PBT-03, design §10).
///
/// A comparação deve usar <c>CryptographicOperations.FixedTimeEquals</c> para prevenir
/// ataques de timing que poderiam distinguir token inválido de token válido
/// (anti-enumeração — Req 7.6, RNF 5).
///
/// Mapeia: TASK-23, PBT-03 (indistinguibilidade de tempo), design §10, RNF 5.
/// </summary>
public sealed class ConstantTimeTokenTests
{
    // ── Comparação de hash correta ────────────────────────────────────────────

    [Fact]
    public void ConstantTimeCompare_SameBytes_ReturnsTrue()
    {
        // Arrange
        var hash1 = "abc123def456"u8.ToArray();
        var hash2 = "abc123def456"u8.ToArray();

        // Act
        var result = ConstantTimeComparison.AreEqual(hash1, hash2);

        // Assert
        result.Should().BeTrue(because: "bytes idênticos devem ser considerados iguais");
    }

    [Fact]
    public void ConstantTimeCompare_DifferentBytes_ReturnsFalse()
    {
        // Arrange
        var hash1 = "abc123def456"u8.ToArray();
        var hash2 = "xyz789uvw012"u8.ToArray();

        // Act
        var result = ConstantTimeComparison.AreEqual(hash1, hash2);

        // Assert
        result.Should().BeFalse(because: "bytes diferentes não devem ser considerados iguais");
    }

    [Fact]
    public void ConstantTimeCompare_DifferentLengths_ReturnsFalse()
    {
        // Arrange
        var hash1 = "abc123"u8.ToArray();
        var hash2 = "abc123def"u8.ToArray();

        // Act
        var result = ConstantTimeComparison.AreEqual(hash1, hash2);

        // Assert
        result.Should().BeFalse(because: "hashes de tamanhos diferentes devem retornar false");
    }

    [Fact]
    public void ConstantTimeCompare_EmptyArrays_ReturnsTrue()
    {
        // Arrange
        var hash1 = Array.Empty<byte>();
        var hash2 = Array.Empty<byte>();

        // Act
        var result = ConstantTimeComparison.AreEqual(hash1, hash2);

        // Assert
        result.Should().BeTrue(because: "dois arrays vazios são iguais");
    }

    // ── Comparação de string SHA-256 (hash do token opaco) ───────────────────

    [Fact]
    public void ConstantTimeCompare_SameHexStrings_ReturnsTrue()
    {
        // Arrange: hash SHA-256 hexadecimal típico
        const string hash = "a665a45920422f9d417e4867efdc4fb8a04a1f3fff1fa07e998e86f7f7a27ae3";

        // Act
        var result = ConstantTimeComparison.AreEqualStrings(hash, hash);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ConstantTimeCompare_DifferentHexStrings_ReturnsFalse()
    {
        // Arrange
        const string hash1 = "a665a45920422f9d417e4867efdc4fb8a04a1f3fff1fa07e998e86f7f7a27ae3";
        const string hash2 = "b665a45920422f9d417e4867efdc4fb8a04a1f3fff1fa07e998e86f7f7a27ae4";

        // Act
        var result = ConstantTimeComparison.AreEqualStrings(hash1, hash2);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ConstantTimeCompare_NullOrEmpty_ReturnsFalse()
    {
        // Tokens nulos ou vazios nunca devem ser considerados válidos
        ConstantTimeComparison.AreEqualStrings(null, "qualquer").Should().BeFalse();
        ConstantTimeComparison.AreEqualStrings("qualquer", null).Should().BeFalse();
        ConstantTimeComparison.AreEqualStrings("", "qualquer").Should().BeFalse();
    }
}
