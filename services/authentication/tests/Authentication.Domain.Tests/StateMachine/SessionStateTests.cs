using Authentication.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Authentication.Domain.Tests.StateMachine;

/// <summary>
/// Testes de transição da máquina de estados de sessão (design.md § 4.5).
///
/// Mapeia: Req 4.2, 9.4, design.md § 4.5.
/// Garante que:
///   - authenticated → expired quando now >= expires_at
///   - expired e revoked negam acesso (IsAccessible retorna false)
///   - anonymous nega acesso
///   - authenticated concede acesso quando não expirado e não revogado
/// </summary>
public sealed class SessionStateTests
{
    // =========================================================================
    // IsAccessible — regras de acesso por estado
    // =========================================================================

    [Fact(DisplayName = "Session authenticated com token válido deve ser acessível")]
    public void IsAccessible_Authenticated_NotExpired_ShouldBeTrue()
    {
        var session = Session.Create(
            SessionState.Authenticated,
            tenantId: Guid.NewGuid(),
            issuedAt: DateTimeOffset.UtcNow.AddMinutes(-5),
            expiresAt: DateTimeOffset.UtcNow.AddHours(1),
            revocationChecked: true);

        session.IsAccessible(DateTimeOffset.UtcNow).Should().BeTrue();
    }

    [Fact(DisplayName = "Session authenticated com token expirado não deve ser acessível")]
    public void IsAccessible_Authenticated_Expired_ShouldBeFalse()
    {
        var issuedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var expiresAt = DateTimeOffset.UtcNow.AddHours(-1); // já expirou

        var session = Session.Create(
            SessionState.Authenticated,
            tenantId: Guid.NewGuid(),
            issuedAt: issuedAt,
            expiresAt: expiresAt,
            revocationChecked: false);

        session.IsAccessible(DateTimeOffset.UtcNow).Should().BeFalse(
            "token expirado não concede acesso (Req 9.4, design.md § 4.5)");
    }

    [Fact(DisplayName = "Session expired não deve ser acessível (estado terminal)")]
    public void IsAccessible_Expired_ShouldBeFalse()
    {
        var issuedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var expiresAt = DateTimeOffset.UtcNow.AddHours(-1);

        var session = Session.Create(
            SessionState.Expired,
            tenantId: Guid.NewGuid(),
            issuedAt: issuedAt,
            expiresAt: expiresAt,
            revocationChecked: false);

        session.IsAccessible(DateTimeOffset.UtcNow).Should().BeFalse(
            "estado expired é terminal — acesso negado sem novo token (Req 9.4)");
    }

    [Fact(DisplayName = "Session revoked não deve ser acessível (estado terminal)")]
    public void IsAccessible_Revoked_ShouldBeFalse()
    {
        var session = Session.Create(
            SessionState.Revoked,
            tenantId: Guid.NewGuid(),
            issuedAt: DateTimeOffset.UtcNow.AddMinutes(-5),
            expiresAt: DateTimeOffset.UtcNow.AddHours(1), // expiresAt futuro, mas revogado
            revocationChecked: true);

        session.IsAccessible(DateTimeOffset.UtcNow).Should().BeFalse(
            "estado revoked é terminal — acesso negado sem novo login (Req 9.4)");
    }

    [Fact(DisplayName = "Session anonymous não deve ser acessível")]
    public void IsAccessible_Anonymous_ShouldBeFalse()
    {
        var session = Session.Create(
            SessionState.Anonymous,
            tenantId: Guid.NewGuid(),
            issuedAt: DateTimeOffset.UtcNow,
            expiresAt: DateTimeOffset.UtcNow.AddHours(1),
            revocationChecked: false);

        session.IsAccessible(DateTimeOffset.UtcNow).Should().BeFalse(
            "anonymous não tem acesso a recursos protegidos");
    }

    // =========================================================================
    // Transições de estado (design.md § 4.5)
    // =========================================================================

    [Fact(DisplayName = "Não deve existir transição direta de expired para acesso autorizado")]
    public void Transition_ExpiredToAuthorized_IsImpossibleWithoutNewToken()
    {
        // Uma sessão expirada não pode reutilizar o mesmo token para obter acesso
        // A única forma de retornar a authenticated é com novo token válido (novo Session)
        var issuedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var expiresAt = DateTimeOffset.UtcNow.AddHours(-1);

        var expiredSession = Session.Create(
            SessionState.Expired,
            tenantId: Guid.NewGuid(),
            issuedAt: issuedAt,
            expiresAt: expiresAt,
            revocationChecked: false);

        // Tentativa de verificar acesso em qualquer momento futuro
        var futureNow = DateTimeOffset.UtcNow.AddMinutes(5);
        expiredSession.IsAccessible(futureNow).Should().BeFalse(
            "expired é estado terminal — não há transição direta para acesso autorizado (design.md § 4.5)");
    }

    [Fact(DisplayName = "Não deve existir transição direta de revoked para acesso autorizado")]
    public void Transition_RevokedToAuthorized_IsImpossibleWithoutNewLogin()
    {
        var session = Session.Create(
            SessionState.Revoked,
            tenantId: Guid.NewGuid(),
            issuedAt: DateTimeOffset.UtcNow.AddMinutes(-5),
            expiresAt: DateTimeOffset.UtcNow.AddHours(1),
            revocationChecked: true);

        session.IsAccessible(DateTimeOffset.UtcNow).Should().BeFalse(
            "revoked é estado terminal — não há transição direta para acesso autorizado (design.md § 4.5)");
    }
}
