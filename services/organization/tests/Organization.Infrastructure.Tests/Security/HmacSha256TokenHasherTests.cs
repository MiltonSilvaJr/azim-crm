using FluentAssertions;
using NSubstitute;
using Organization.Application.Ports;
using Organization.Infrastructure.Security;
using Xunit;

namespace Organization.Infrastructure.Tests.Security;

/// <summary>
/// Testes unitários de <see cref="HmacSha256TokenHasher"/> — TASK-26 ST-01/ST-02 (Onda 6).
/// Valida: hash com pepper difere do token em claro, mudança de pepper produz hash diferente,
/// idempotência do hash, entropia do token gerado.
/// Referências: design §10, DD-007, RNF 3; TASK-26.
/// </summary>
public sealed class HmacSha256TokenHasherTests
{
    private static HmacSha256TokenHasher CreateHasher(string pepper = "pepper-secreto-de-teste")
    {
        var secretProvider = Substitute.For<ISecretProvider>();
        secretProvider
            .GetSecretAsync(HmacSha256TokenHasher.PepperSecretName, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(pepper));
        return new HmacSha256TokenHasher(secretProvider);
    }

    // ── ST-01: hash é diferente do token em claro ────────────────────────

    [Fact]
    public void Hash_IsNotEqualToPlainToken()
    {
        // Arrange
        var hasher = CreateHasher();
        const string plain = "token-em-claro-para-teste";

        // Act
        var hash = hasher.Hash(plain);

        // Assert
        hash.Should().NotBe(plain,
            "o hash deve diferir do token em claro (DD-007)");

        hash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateToken_PlainTokenDiffersFromHash()
    {
        // Arrange
        var hasher = CreateHasher();

        // Act
        var (plain, hash) = hasher.GenerateToken();

        // Assert
        hash.Should().NotBe(plain,
            "token em claro e hash devem ser distintos");

        plain.Should().NotBeNullOrEmpty();
        hash.Should().NotBeNullOrEmpty();
    }

    // ── ST-01: pepper altera o hash ──────────────────────────────────────

    [Fact]
    public void Hash_WithDifferentPepper_ProducesDifferentHash()
    {
        // Arrange — ST-01: pepper alterado deve produzir hash diferente
        var hasher1 = CreateHasher("pepper-A");
        var hasher2 = CreateHasher("pepper-B");
        const string plain = "mesmo-token-para-ambos";

        // Act
        var hash1 = hasher1.Hash(plain);
        var hash2 = hasher2.Hash(plain);

        // Assert
        hash1.Should().NotBe(hash2,
            "pepper diferente deve produzir hash diferente (HMAC-SHA256 com pepper)");
    }

    // ── Idempotência do hash ─────────────────────────────────────────────

    [Fact]
    public void Hash_SameInputAndSamePepper_ProducesSameHash()
    {
        // Arrange — mesma entrada e mesmo pepper deve ser determinístico
        var hasher = CreateHasher("mesmo-pepper");
        const string plain = "token-para-idempotencia";

        // Act
        var hash1 = hasher.Hash(plain);
        var hash2 = hasher.Hash(plain);

        // Assert
        hash1.Should().Be(hash2,
            "HMAC-SHA256 é determinístico para mesma chave e mensagem");
    }

    // ── Tokens gerados são distintos (entropia) ──────────────────────────

    [Fact]
    public void GenerateToken_ProducesUniqueTokensOnEachCall()
    {
        // Arrange
        var hasher = CreateHasher();

        // Act
        var (plain1, hash1) = hasher.GenerateToken();
        var (plain2, hash2) = hasher.GenerateToken();

        // Assert — tokens e hashes devem ser distintos (entropia criptográfica)
        plain1.Should().NotBe(plain2,
            "tokens gerados devem ser únicos por RandomNumberGenerator.Fill");

        hash1.Should().NotBe(hash2,
            "hashes de tokens únicos devem ser distintos");
    }

    // ── Formato do hash ──────────────────────────────────────────────────

    [Fact]
    public void Hash_ProducesHexString()
    {
        // Arrange
        var hasher = CreateHasher();

        // Act
        var hash = hasher.Hash("qualquer-token");

        // Assert — HMAC-SHA256 = 32 bytes = 64 hex chars
        hash.Should().HaveLength(64,
            "HMAC-SHA256 produz 256 bits = 32 bytes = 64 caracteres hexadecimais");

        hash.Should().MatchRegex("^[0-9a-f]+$",
            "hash deve ser hexadecimal em letras minúsculas");
    }

    // ── Segredos não aparecem no hash ────────────────────────────────────

    [Fact]
    public void Hash_DoesNotRevealPepperValue()
    {
        // Arrange
        const string pepper = "segredo-super-secreto-pepper";
        var hasher = CreateHasher(pepper);

        // Act
        var hash = hasher.Hash("algum-token");

        // Assert — pepper nunca deve aparecer no hash em texto claro
        hash.Should().NotContain(pepper,
            "o pepper não deve aparecer no hash em nenhum formato");
    }

    // ── PepperSecretName está correto ────────────────────────────────────

    [Fact]
    public void HmacSha256TokenHasher_PepperSecretName_IsCorrectlyDefined()
    {
        // Arrange & Assert
        HmacSha256TokenHasher.PepperSecretName.Should().Be("organization_token_pepper",
            "nome do segredo deve ser consistente entre registro e acesso");
    }
}
