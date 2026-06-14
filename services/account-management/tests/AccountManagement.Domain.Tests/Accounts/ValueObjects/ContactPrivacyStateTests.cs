using AccountManagement.Domain.Accounts.ValueObjects;
using FluentAssertions;
using Xunit;

namespace AccountManagement.Domain.Tests.Accounts.ValueObjects;

/// <summary>
/// Testes unitários para o objeto de valor <see cref="ContactPrivacyState"/>.
///
/// Mapeia: design §4.3, design §4.5, TASK-02 ST-01, Req 7.5, PBT-03.
/// Invariante: apenas estados <c>Active</c> e <c>Anonymized</c>; transição monotônica irreversível.
/// </summary>
public sealed class ContactPrivacyStateTests
{
    [Fact(DisplayName = "Estado inicial é Active")]
    public void Initial_StateIsActive()
    {
        var state = ContactPrivacyState.Active;

        state.Should().Be(ContactPrivacyState.Active);
    }

    [Fact(DisplayName = "Estado Anonymized existe")]
    public void Anonymized_StateExists()
    {
        var state = ContactPrivacyState.Anonymized;

        state.Should().Be(ContactPrivacyState.Anonymized);
    }

    [Fact(DisplayName = "Active e Anonymized são estados distintos")]
    public void Active_AndAnonymized_AreDistinct()
    {
        ContactPrivacyState.Active.Should().NotBe(ContactPrivacyState.Anonymized);
    }

    [Fact(DisplayName = "Active isActive retorna verdadeiro")]
    public void Active_IsActive_ReturnsTrue()
    {
        ContactPrivacyState.Active.IsActive.Should().BeTrue();
    }

    [Fact(DisplayName = "Anonymized isAnonymized retorna verdadeiro")]
    public void Anonymized_IsAnonymized_ReturnsTrue()
    {
        ContactPrivacyState.Anonymized.IsAnonymized.Should().BeTrue();
    }

    [Fact(DisplayName = "Active não é Anonymized")]
    public void Active_IsNotAnonymized()
    {
        ContactPrivacyState.Active.IsAnonymized.Should().BeFalse();
    }

    [Fact(DisplayName = "Igualdade por valor para ContactPrivacyState")]
    public void Equality_SameState_AreEqual()
    {
        var a = ContactPrivacyState.Active;
        var b = ContactPrivacyState.Active;

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
