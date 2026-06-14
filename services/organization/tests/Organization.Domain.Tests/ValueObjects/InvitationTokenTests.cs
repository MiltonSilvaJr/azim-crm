using FluentAssertions;
using Organization.Domain.ValueObjects;
using Xunit;

namespace Organization.Domain.Tests.ValueObjects;

/// <summary>
/// Testes unitários para o objeto de valor <see cref="InvitationToken"/>.
/// Cobre: valor em claro nunca persistido, hash aceito diretamente, construção sem hash rejeitada.
/// </summary>
public sealed class InvitationTokenTests
{
    [Fact]
    public void FromHash_WhenHashIsValid_ShouldSucceed()
    {
        var hash = "sha256hash_abc123";
        var token = InvitationToken.FromHash(hash);
        token.TokenHash.Should().Be(hash);
    }

    [Fact]
    public void FromHash_WhenHashIsNull_ShouldThrow()
    {
        var act = () => InvitationToken.FromHash(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromHash_WhenHashIsEmpty_ShouldThrow()
    {
        var act = () => InvitationToken.FromHash("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromHash_WhenHashIsWhitespace_ShouldThrow()
    {
        var act = () => InvitationToken.FromHash("   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void InvitationToken_ShouldNotExposeRawValue()
    {
        // O objeto de valor não tem propriedade para o valor em claro.
        var token = InvitationToken.FromHash("somehash");
        var properties = typeof(InvitationToken).GetProperties();
        properties.Should().NotContain(p =>
            p.Name.Equals("Value", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Equals("ClearText", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Equals("PlainText", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Equals("Token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Equality_WhenSameHash_ShouldBeEqual()
    {
        var a = InvitationToken.FromHash("hash123");
        var b = InvitationToken.FromHash("hash123");
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_WhenDifferentHashes_ShouldNotBeEqual()
    {
        var a = InvitationToken.FromHash("hash123");
        var b = InvitationToken.FromHash("hash456");
        a.Should().NotBe(b);
    }

    [Fact]
    public void Matches_WhenHashMatches_ShouldReturnTrue()
    {
        var hash = "testhash_xyz";
        var token = InvitationToken.FromHash(hash);
        token.Matches(hash).Should().BeTrue();
    }

    [Fact]
    public void Matches_WhenHashDoesNotMatch_ShouldReturnFalse()
    {
        var token = InvitationToken.FromHash("correcthash");
        token.Matches("wronghash").Should().BeFalse();
    }
}
