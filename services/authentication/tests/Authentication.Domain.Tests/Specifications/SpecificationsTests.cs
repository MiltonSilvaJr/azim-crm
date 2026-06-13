using Authentication.Domain.Specifications;
using Authentication.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Authentication.Domain.Tests.Specifications;

/// <summary>
/// Testes unitários das cinco Specifications do domínio (design.md § 4.6).
///
/// Mapeia: Req 4.2 (TokenValiditySpec), Req 4.3/PBT-02 (TenantMatchSpec),
/// Req 5.4 (ActiveUserSpec), Req 7.4/7.5/PBT-05 (InviteUsableSpec),
/// Req 8.4 (EmailMethodSpec).
///
/// Specifications são pure functions — sem efeito colateral.
/// </summary>
public sealed class SpecificationsTests
{
    // =========================================================================
    // TokenValiditySpec
    // =========================================================================

    [Fact(DisplayName = "TokenValiditySpec retorna true para sessão authenticated não expirada")]
    public void TokenValiditySpec_AuthenticatedNotExpired_ReturnsTrue()
    {
        var session = Session.Create(
            SessionState.Authenticated,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddHours(1),
            revocationChecked: true);

        TokenValiditySpec.IsSatisfiedBy(session, DateTimeOffset.UtcNow).Should().BeTrue();
    }

    [Fact(DisplayName = "TokenValiditySpec retorna false para sessão expired")]
    public void TokenValiditySpec_Expired_ReturnsFalse()
    {
        var session = Session.Create(
            SessionState.Expired,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddHours(-2),
            DateTimeOffset.UtcNow.AddHours(-1),
            revocationChecked: false);

        TokenValiditySpec.IsSatisfiedBy(session, DateTimeOffset.UtcNow).Should().BeFalse();
    }

    [Fact(DisplayName = "TokenValiditySpec retorna false para sessão revoked")]
    public void TokenValiditySpec_Revoked_ReturnsFalse()
    {
        var session = Session.Create(
            SessionState.Revoked,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddHours(1),
            revocationChecked: true);

        TokenValiditySpec.IsSatisfiedBy(session, DateTimeOffset.UtcNow).Should().BeFalse();
    }

    [Fact(DisplayName = "TokenValiditySpec retorna false para sessão anonymous")]
    public void TokenValiditySpec_Anonymous_ReturnsFalse()
    {
        var session = Session.Create(
            SessionState.Anonymous,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(1),
            revocationChecked: false);

        TokenValiditySpec.IsSatisfiedBy(session, DateTimeOffset.UtcNow).Should().BeFalse();
    }

    // =========================================================================
    // TenantMatchSpec
    // =========================================================================

    [Fact(DisplayName = "TenantMatchSpec retorna true quando firebase_tenant corresponde ao tenant resolvido")]
    public void TenantMatchSpec_MatchingTenants_ReturnsTrue()
    {
        var firebaseTenant = "firebase-tenant-abc";
        var expectedFirebaseTenant = "firebase-tenant-abc";

        TenantMatchSpec.IsSatisfiedBy(firebaseTenant, expectedFirebaseTenant).Should().BeTrue();
    }

    [Fact(DisplayName = "TenantMatchSpec retorna false quando firebase_tenant diverge do tenant resolvido")]
    public void TenantMatchSpec_DivergentTenants_ReturnsFalse()
    {
        var firebaseTenant = "firebase-tenant-a";
        var expectedFirebaseTenant = "firebase-tenant-b";

        TenantMatchSpec.IsSatisfiedBy(firebaseTenant, expectedFirebaseTenant).Should().BeFalse(
            "token de tenant A não deve ser válido em contexto de tenant B (PBT-02, Req 4.3)");
    }

    [Theory(DisplayName = "TenantMatchSpec retorna false para firebase_tenant nulo ou vazio")]
    [InlineData(null)]
    [InlineData("")]
    public void TenantMatchSpec_NullOrEmptyFirebaseTenant_ReturnsFalse(string? firebaseTenant)
    {
        TenantMatchSpec.IsSatisfiedBy(firebaseTenant!, "firebase-tenant-x").Should().BeFalse();
    }

    // =========================================================================
    // ActiveUserSpec
    // =========================================================================

    [Fact(DisplayName = "ActiveUserSpec retorna true para usuário ativo com user_id válido")]
    public void ActiveUserSpec_ActiveUser_ReturnsTrue()
    {
        ActiveUserSpec.IsSatisfiedBy(userId: Guid.NewGuid(), isActive: true).Should().BeTrue();
    }

    [Fact(DisplayName = "ActiveUserSpec retorna false para usuário inativo (→ 403)")]
    public void ActiveUserSpec_InactiveUser_ReturnsFalse()
    {
        ActiveUserSpec.IsSatisfiedBy(userId: Guid.NewGuid(), isActive: false).Should().BeFalse(
            "usuário inativo → 403 (Req 5.4)");
    }

    [Fact(DisplayName = "ActiveUserSpec retorna false para user_id vazio (sem user_id no tenant)")]
    public void ActiveUserSpec_EmptyUserId_ReturnsFalse()
    {
        ActiveUserSpec.IsSatisfiedBy(userId: Guid.Empty, isActive: true).Should().BeFalse(
            "identity_uid sem user_id ativo → 403 (Req 5.4)");
    }

    // =========================================================================
    // InviteUsableSpec (design.md § 4.6, seção 16.5, PBT-05)
    // =========================================================================

    [Fact(DisplayName = "InviteUsableSpec retorna true para link issued não expirado")]
    public void InviteUsableSpec_Issued_NotExpired_ReturnsTrue()
    {
        InviteUsableSpec.IsSatisfiedBy(
            state: InviteLinkState.Issued,
            expiresAt: DateTimeOffset.UtcNow.AddHours(24),
            now: DateTimeOffset.UtcNow)
        .Should().BeTrue();
    }

    [Fact(DisplayName = "InviteUsableSpec retorna false para link consumed (estado terminal)")]
    public void InviteUsableSpec_Consumed_ReturnsFalse()
    {
        InviteUsableSpec.IsSatisfiedBy(
            state: InviteLinkState.Consumed,
            expiresAt: DateTimeOffset.UtcNow.AddHours(24),
            now: DateTimeOffset.UtcNow)
        .Should().BeFalse("consumed é estado terminal — nunca reabilita acesso (PBT-05, Req 7.4)");
    }

    [Fact(DisplayName = "InviteUsableSpec retorna false para link expired (estado terminal)")]
    public void InviteUsableSpec_Expired_ReturnsFalse()
    {
        InviteUsableSpec.IsSatisfiedBy(
            state: InviteLinkState.Expired,
            expiresAt: DateTimeOffset.UtcNow.AddHours(-1),
            now: DateTimeOffset.UtcNow)
        .Should().BeFalse("expired é estado terminal — nunca reabilita acesso (PBT-05, Req 7.5)");
    }

    [Fact(DisplayName = "InviteUsableSpec retorna false para link issued com prazo atingido")]
    public void InviteUsableSpec_Issued_PastExpiry_ReturnsFalse()
    {
        InviteUsableSpec.IsSatisfiedBy(
            state: InviteLinkState.Issued,
            expiresAt: DateTimeOffset.UtcNow.AddHours(-1),
            now: DateTimeOffset.UtcNow)
        .Should().BeFalse("prazo atingido equivale a expired independente do estado armazenado");
    }

    // =========================================================================
    // EmailMethodSpec
    // =========================================================================

    [Fact(DisplayName = "EmailMethodSpec retorna true para método password (pode redefinir senha)")]
    public void EmailMethodSpec_PasswordMethod_ReturnsTrue()
    {
        EmailMethodSpec.IsSatisfiedBy(signInProvider: "password").Should().BeTrue();
    }

    [Fact(DisplayName = "EmailMethodSpec retorna false para método google (não usa senha)")]
    public void EmailMethodSpec_GoogleMethod_ReturnsFalse()
    {
        EmailMethodSpec.IsSatisfiedBy(signInProvider: "google.com").Should().BeFalse(
            "usuário Google não deve receber opção de recuperação de senha (Req 8.4)");
    }

    [Theory(DisplayName = "EmailMethodSpec retorna false para qualquer provider não-password")]
    [InlineData("google.com")]
    [InlineData("microsoft.com")]
    [InlineData("github.com")]
    [InlineData("saml.provider")]
    public void EmailMethodSpec_NonPasswordProviders_ReturnsFalse(string provider)
    {
        EmailMethodSpec.IsSatisfiedBy(signInProvider: provider).Should().BeFalse();
    }
}
