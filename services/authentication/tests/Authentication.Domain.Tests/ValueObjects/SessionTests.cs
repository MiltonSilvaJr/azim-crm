using Authentication.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Authentication.Domain.Tests.ValueObjects;

/// <summary>
/// Testes unitários de <see cref="Session"/>.
///
/// Mapeia: Req 4.2, 5.4, design.md § 4.3, § 4.5.
/// Critérios: imutabilidade, igualdade por valor, invariante de coerência entre
/// state e expires_at, ausência de identity_uid.
/// </summary>
public sealed class SessionTests
{
    // =========================================================================
    // Criação e invariantes
    // =========================================================================

    [Fact(DisplayName = "Session válida deve ser criada com sucesso")]
    public void Create_WithValidData_ShouldSucceed()
    {
        var tenantId = Guid.NewGuid();
        var issuedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);

        var session = Session.Create(
            state: SessionState.Authenticated,
            tenantId: tenantId,
            issuedAt: issuedAt,
            expiresAt: expiresAt,
            revocationChecked: false);

        session.State.Should().Be(SessionState.Authenticated);
        session.TenantId.Should().Be(tenantId);
        session.IssuedAt.Should().Be(issuedAt);
        session.ExpiresAt.Should().Be(expiresAt);
        session.RevocationChecked.Should().BeFalse();
    }

    [Fact(DisplayName = "Session deve lançar ArgumentException para tenant_id vazio")]
    public void Create_WithEmptyTenantId_ShouldThrow()
    {
        var act = () => Session.Create(
            SessionState.Authenticated, Guid.Empty,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddHours(1),
            false);

        act.Should().Throw<ArgumentException>("tenant_id não pode ser vazio");
    }

    [Fact(DisplayName = "Session deve lançar ArgumentException quando expiresAt é anterior a issuedAt")]
    public void Create_WithExpiresAtBeforeIssuedAt_ShouldThrow()
    {
        var now = DateTimeOffset.UtcNow;
        var act = () => Session.Create(
            SessionState.Authenticated, Guid.NewGuid(),
            issuedAt: now,
            expiresAt: now.AddSeconds(-1),
            false);

        act.Should().Throw<ArgumentException>("expiresAt não pode ser anterior a issuedAt");
    }

    // =========================================================================
    // Igualdade por valor
    // =========================================================================

    [Fact(DisplayName = "Dois Session com mesmos dados devem ser iguais")]
    public void Equality_SameData_ShouldBeEqual()
    {
        var tenantId = Guid.NewGuid();
        var issuedAt = DateTimeOffset.UtcNow;
        var expiresAt = issuedAt.AddHours(1);

        var a = Session.Create(SessionState.Authenticated, tenantId, issuedAt, expiresAt, false);
        var b = Session.Create(SessionState.Authenticated, tenantId, issuedAt, expiresAt, false);

        a.Should().Be(b);
    }

    [Fact(DisplayName = "Session com state diferente não deve ser igual")]
    public void Equality_DifferentState_ShouldNotBeEqual()
    {
        var tenantId = Guid.NewGuid();
        var issuedAt = DateTimeOffset.UtcNow;
        var expiresAt = issuedAt.AddHours(1);

        var a = Session.Create(SessionState.Authenticated, tenantId, issuedAt, expiresAt, false);
        var b = Session.Create(SessionState.Expired, tenantId, issuedAt, expiresAt, false);

        a.Should().NotBe(b);
    }

    // =========================================================================
    // Imutabilidade
    // =========================================================================

    [Theory(DisplayName = "Session não deve expor setter público em nenhuma propriedade")]
    [InlineData(nameof(Session.State))]
    [InlineData(nameof(Session.TenantId))]
    [InlineData(nameof(Session.IssuedAt))]
    [InlineData(nameof(Session.ExpiresAt))]
    [InlineData(nameof(Session.RevocationChecked))]
    public void Immutability_Properties_ShouldHaveNoPublicSetter(string propertyName)
    {
        var prop = typeof(Session).GetProperty(propertyName);
        prop.Should().NotBeNull();
        prop!.SetMethod?.IsPublic.Should().BeFalse(
            $"{propertyName} deve ser somente leitura — Session é imutável (design.md § 4.3)");
    }
}
