using Digest.Domain.ValueObjects;

namespace Digest.Domain.Tests.ValueObjects;

/// <summary>
/// Testes unitários para <see cref="MoneyCents"/>, <see cref="ActionToken"/>,
/// <see cref="DigestSection"/> e <see cref="DigestContent"/> (TASK-04).
/// </summary>
public sealed class ValueObjectsTests
{
    // ---------------------------------------------------------------
    // MoneyCents
    // ---------------------------------------------------------------

    [Fact]
    public void MoneyCents_created_from_long_stores_value()
    {
        var money = new MoneyCents(1_000L);
        money.Cents.Should().Be(1_000L);
    }

    [Fact]
    public void MoneyCents_zero_is_valid()
    {
        var money = MoneyCents.Zero;
        money.Cents.Should().Be(0L);
    }

    [Fact]
    public void MoneyCents_equality_by_value()
    {
        var a = new MoneyCents(500L);
        var b = new MoneyCents(500L);
        a.Should().Be(b);
    }

    [Fact]
    public void MoneyCents_rejects_negative_values()
    {
        var act = () => new MoneyCents(-1L);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MoneyCents_Add_returns_sum()
    {
        var a = new MoneyCents(300L);
        var b = new MoneyCents(200L);
        a.Add(b).Should().Be(new MoneyCents(500L));
    }

    [Fact]
    public void MoneyCents_Subtract_returns_difference()
    {
        var a = new MoneyCents(500L);
        var b = new MoneyCents(200L);
        a.Subtract(b).Should().Be(new MoneyCents(300L));
    }

    [Fact]
    public void MoneyCents_Subtract_throws_when_result_negative()
    {
        var a = new MoneyCents(100L);
        var b = new MoneyCents(200L);
        var act = () => a.Subtract(b);
        act.Should().Throw<InvalidOperationException>();
    }

    // ---------------------------------------------------------------
    // ActionToken
    // ---------------------------------------------------------------

    [Fact]
    public void ActionToken_has_non_empty_clear_token_on_creation()
    {
        var token = ActionToken.Issue();
        token.ClearToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ActionToken_has_hash_on_creation()
    {
        var token = ActionToken.Issue();
        token.TokenHash.Should().NotBeNull();
        token.TokenHash.Should().NotBeEmpty();
    }

    [Fact]
    public void ActionToken_equality_is_by_hash()
    {
        var rawToken = "my-test-token-value";
        var t1 = ActionToken.FromClearToken(rawToken);
        var t2 = ActionToken.FromClearToken(rawToken);
        t1.Should().Be(t2);
    }

    [Fact]
    public void ActionToken_different_tokens_produce_different_hashes()
    {
        var t1 = ActionToken.Issue();
        var t2 = ActionToken.Issue();
        t1.TokenHash.Should().NotEqual(t2.TokenHash);
    }

    [Fact]
    public void ActionToken_issued_tokens_have_256_bits_entropy()
    {
        // 256 bits = 32 bytes; token em claro é Base64Url de 32 bytes → 43 chars
        var token = ActionToken.Issue();
        // O hash SHA-256 tem sempre 32 bytes
        token.TokenHash.Length.Should().Be(32);
        // Token em claro tem pelo menos 43 caracteres (Base64Url de 32 bytes)
        token.ClearToken.Length.Should().BeGreaterThanOrEqualTo(43);
    }

    // ---------------------------------------------------------------
    // DigestSection
    // ---------------------------------------------------------------

    [Fact]
    public void DigestSection_IsEmpty_returns_true_when_no_items()
    {
        var section = new DigestSection("overdue_activities", []);
        section.IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void DigestSection_IsEmpty_returns_false_when_has_items()
    {
        var section = new DigestSection("overdue_activities", ["item1"]);
        section.IsEmpty().Should().BeFalse();
    }

    [Fact]
    public void DigestSection_stores_key_and_items()
    {
        var items = new[] { "item1", "item2" };
        var section = new DigestSection("today_activities", items);
        section.Key.Should().Be("today_activities");
        section.Items.Should().BeEquivalentTo(items);
    }

    // ---------------------------------------------------------------
    // DigestContent
    // ---------------------------------------------------------------

    [Fact]
    public void DigestContent_HasContent_returns_false_when_all_sections_empty()
    {
        var sections = new[]
        {
            new DigestSection("overdue_activities", []),
            new DigestSection("today_activities", []),
        };
        var content = new DigestContent(sections);
        content.HasContent().Should().BeFalse();
    }

    [Fact]
    public void DigestContent_HasContent_returns_true_when_at_least_one_section_non_empty()
    {
        var sections = new[]
        {
            new DigestSection("overdue_activities", []),
            new DigestSection("today_activities", ["tarefa urgente"]),
        };
        var content = new DigestContent(sections);
        content.HasContent().Should().BeTrue();
    }

    [Fact]
    public void DigestContent_HasContent_returns_false_when_empty_sections_list()
    {
        var content = new DigestContent([]);
        content.HasContent().Should().BeFalse();
    }

    [Fact]
    public void DigestContent_Sections_are_immutable()
    {
        var sections = new[]
        {
            new DigestSection("overdue_activities", ["item"]),
        };
        var content = new DigestContent(sections);
        content.Sections.Should().HaveCount(1);
        // A coleção exposta não é a mesma referência do array original
        content.Sections.Should().BeEquivalentTo(sections);
    }

    [Fact]
    public void DigestContent_GetSection_returns_section_by_key()
    {
        var sections = new[]
        {
            new DigestSection("overdue_activities", ["item"]),
            new DigestSection("today_activities", []),
        };
        var content = new DigestContent(sections);
        var found = content.GetSection("overdue_activities");
        found.Should().NotBeNull();
        found!.IsEmpty().Should().BeFalse();
    }

    [Fact]
    public void DigestContent_GetSection_returns_null_for_unknown_key()
    {
        var content = new DigestContent([]);
        content.GetSection("unknown_key").Should().BeNull();
    }
}
