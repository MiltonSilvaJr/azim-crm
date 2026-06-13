using FluentAssertions;
using NotificationDelivery.Infrastructure.Telemetry;
using Xunit;

namespace NotificationDelivery.Infrastructure.Tests.Telemetry;

/// <summary>
/// Testes do <see cref="EmailHasher"/> (mascaramento de PII).
///
/// Cobre os critérios de aceite da TASK-15 (ST-01..ST-03):
/// - Hash não contém fragmento do e-mail original (ST-01a);
/// - Hash é determinístico (ST-01b);
/// - Dois e-mails distintos produzem hashes distintos (ST-01c);
/// - EmailHasher é stateless (ST-03);
/// - PBT-03 base estrutural: o hash não contém '@', fragmento de usuário, domínio ou TLD.
/// </summary>
public sealed class EmailHasherTests
{
    // -------------------------------------------------------------------------
    // ST-01a: hash não contém fragmento do e-mail original (RNF 4, DD-008, PBT-03)
    // -------------------------------------------------------------------------

    [Theory(DisplayName = "Hash: resultado não contém fragmentos do e-mail original")]
    [InlineData("user@example.com", "user", "example", ".com")]
    [InlineData("joao.silva@azim.com.br", "joao", "silva", "azim")]
    [InlineData("maria+tag@empresa.io", "maria", "empresa", ".io")]
    [InlineData("TEST@DOMAIN.ORG", "test", "domain", ".org")]
    public void Hash_DoesNotContainEmailFragments(
        string email, string part1, string part2, string part3)
    {
        // Act
        var hash = EmailHasher.Hash(email);

        // Assert — nenhum fragmento do e-mail aparece no hash (PBT-03 invariante)
        hash.Should().NotContain("@",
            because: "o símbolo '@' não deve aparecer no hash mascarado (RNF 4)");
        hash.Should().NotContain(part1,
            because: $"o fragmento '{part1}' do e-mail original não deve aparecer no hash");
        hash.Should().NotContain(part2,
            because: $"o fragmento '{part2}' do e-mail original não deve aparecer no hash");
        hash.Should().NotContain(part3,
            because: $"o fragmento '{part3}' do e-mail original não deve aparecer no hash");
    }

    // -------------------------------------------------------------------------
    // ST-01b: determinismo — Hash(x) == Hash(x) sempre (Req 6.4 análogo)
    // -------------------------------------------------------------------------

    [Theory(DisplayName = "Hash: determinístico para o mesmo e-mail")]
    [InlineData("user@example.com")]
    [InlineData("joao.silva@azim.com.br")]
    [InlineData("UPPER@CASE.COM")]
    public void Hash_IsDeterministic(string email)
    {
        // Act — chama duas vezes
        var hash1 = EmailHasher.Hash(email);
        var hash2 = EmailHasher.Hash(email);

        // Assert
        hash1.Should().Be(hash2,
            because: "o hash deve ser idêntico para o mesmo e-mail (PBT-03 invariante)");
    }

    // -------------------------------------------------------------------------
    // ST-01c: dois e-mails distintos → hashes distintos
    // -------------------------------------------------------------------------

    [Theory(DisplayName = "Hash: e-mails distintos produzem hashes distintos")]
    [InlineData("user1@example.com", "user2@example.com")]
    [InlineData("a@b.com", "c@d.com")]
    [InlineData("joao@azim.com.br", "maria@azim.com.br")]
    public void Hash_DistinctEmails_ProduceDistinctHashes(string email1, string email2)
    {
        // Act
        var hash1 = EmailHasher.Hash(email1);
        var hash2 = EmailHasher.Hash(email2);

        // Assert
        hash1.Should().NotBe(hash2,
            because: "e-mails distintos devem produzir hashes distintos (low collision rate)");
    }

    // -------------------------------------------------------------------------
    // ST-02: formato do hash (prefixo + comprimento)
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "Hash: formato 'email#' + 12 chars hex")]
    public void Hash_HasCorrectFormat()
    {
        // Act
        var hash = EmailHasher.Hash("user@example.com");

        // Assert
        hash.Should().StartWith(EmailHasher.HashPrefix,
            because: "o hash deve ter o prefixo 'email#' para identificação em logs");
        hash.Should().HaveLength(
            EmailHasher.HashPrefix.Length + EmailHasher.HashLength,
            because: $"o hash deve ter {EmailHasher.HashLength} chars hex após o prefixo");

        var hexPart = hash[EmailHasher.HashPrefix.Length..];
        hexPart.Should().MatchRegex("^[0-9a-f]+$",
            because: "a parte hex deve conter apenas caracteres hexadecimais minúsculos");
    }

    // -------------------------------------------------------------------------
    // ST-03: normalização (case-insensitive — UPPER e lower são equivalentes)
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "Hash: normaliza e-mail para lowercase antes de hashar")]
    public void Hash_NormalizesToLowercase()
    {
        // Act
        var hashLower = EmailHasher.Hash("user@example.com");
        var hashUpper = EmailHasher.Hash("USER@EXAMPLE.COM");
        var hashMixed = EmailHasher.Hash("User@Example.COM");

        // Assert — todos equivalentes após normalização
        hashLower.Should().Be(hashUpper,
            because: "e-mails em maiúsculas e minúsculas são o mesmo destinatário (normalização)");
        hashLower.Should().Be(hashMixed);
    }

    [Fact(DisplayName = "Hash: remove espaços antes de hashar (trim)")]
    public void Hash_TrimsWhitespace()
    {
        // Act
        var hashTrimmed = EmailHasher.Hash("user@example.com");
        var hashPadded = EmailHasher.Hash("  user@example.com  ");

        // Assert
        hashTrimmed.Should().Be(hashPadded,
            because: "espaços em volta do e-mail não devem alterar o hash");
    }

    // -------------------------------------------------------------------------
    // IsHashed: detecção de valor já mascarado
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "IsHashed: retorna true para hash gerado por Hash()")]
    public void IsHashed_WithHashedValue_ReturnsTrue()
    {
        // Arrange
        var hash = EmailHasher.Hash("user@example.com");

        // Act + Assert
        EmailHasher.IsHashed(hash).Should().BeTrue();
    }

    [Fact(DisplayName = "IsHashed: retorna false para e-mail em claro")]
    public void IsHashed_WithPlainEmail_ReturnsFalse()
    {
        // Act + Assert
        EmailHasher.IsHashed("user@example.com").Should().BeFalse();
    }

    [Fact(DisplayName = "IsHashed: retorna false para null")]
    public void IsHashed_WithNull_ReturnsFalse()
    {
        EmailHasher.IsHashed(null).Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // ArgumentNullException para null
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "Hash: lança ArgumentNullException para e-mail nulo")]
    public void Hash_WithNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => EmailHasher.Hash(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
