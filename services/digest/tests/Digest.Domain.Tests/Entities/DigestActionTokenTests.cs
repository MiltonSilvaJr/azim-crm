using Digest.Domain.Entities;
using Digest.Domain.Enums;
using Digest.Domain.ValueObjects;

namespace Digest.Domain.Tests.Entities;

/// <summary>
/// Testes unitários para <see cref="DigestActionToken"/> (TASK-06, VAL-ACT-02).
/// Valida: token_hash persistido, expires_at calculado com TTL customizável,
/// rejeição de TTL não-positivo, action válida, tenant_id obrigatório.
/// </summary>
public sealed class DigestActionTokenTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ActivityId = Guid.NewGuid();
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(48);

    // ---------------------------------------------------------------
    // Criação
    // ---------------------------------------------------------------

    [Fact]
    public void Issue_creates_entity_with_token_hash()
    {
        var token = ActionToken.Issue();
        var entity = DigestActionToken.Issue(TenantId, UserId, ActivityId, ActionType.Complete, token, DefaultTtl);

        entity.TokenHash.Should().BeEquivalentTo(token.TokenHash);
        entity.TenantId.Should().Be(TenantId);
        entity.UserId.Should().Be(UserId);
        entity.ActivityId.Should().Be(ActivityId);
        entity.Action.Should().Be(ActionType.Complete);
    }

    [Fact]
    public void Issue_sets_expires_at_48h_after_creation_with_default_ttl()
    {
        var token = ActionToken.Issue();
        var before = DateTimeOffset.UtcNow;
        var entity = DigestActionToken.Issue(TenantId, UserId, ActivityId, ActionType.Complete, token, DefaultTtl);
        var after = DateTimeOffset.UtcNow;

        entity.ExpiresAt.Should().BeOnOrAfter(before.AddHours(48));
        entity.ExpiresAt.Should().BeOnOrBefore(after.AddHours(48).AddSeconds(1));
    }

    [Fact]
    public void Issue_sets_expires_at_using_custom_ttl()
    {
        // VAL-ACT-02: TTL configurável por tenant
        var customTtl = TimeSpan.FromHours(72);
        var token = ActionToken.Issue();
        var before = DateTimeOffset.UtcNow;
        var entity = DigestActionToken.Issue(TenantId, UserId, ActivityId, ActionType.Complete, token, customTtl);
        var after = DateTimeOffset.UtcNow;

        entity.ExpiresAt.Should().BeOnOrAfter(before.Add(customTtl));
        entity.ExpiresAt.Should().BeOnOrBefore(after.Add(customTtl).AddSeconds(1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-48)]
    public void Issue_rejects_non_positive_ttl(int hours)
    {
        // VAL-ACT-02: TTL deve ser positivo
        var token = ActionToken.Issue();
        var invalidTtl = TimeSpan.FromHours(hours);

        var act = () => DigestActionToken.Issue(TenantId, UserId, ActivityId, ActionType.Complete, token, invalidTtl);

        act.Should().Throw<ArgumentOutOfRangeException>(
            because: "TTL não-positivo deve ser rejeitado (VAL-ACT-02)");
    }

    [Fact]
    public void Issue_reschedule_action_is_valid()
    {
        var token = ActionToken.Issue();
        var entity = DigestActionToken.Issue(TenantId, UserId, ActivityId, ActionType.Reschedule, token, DefaultTtl);
        entity.Action.Should().Be(ActionType.Reschedule);
    }

    [Fact]
    public void Issue_requires_non_empty_tenant_id()
    {
        var token = ActionToken.Issue();
        var act = () => DigestActionToken.Issue(Guid.Empty, UserId, ActivityId, ActionType.Complete, token, DefaultTtl);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Issue_requires_non_empty_user_id()
    {
        var token = ActionToken.Issue();
        var act = () => DigestActionToken.Issue(TenantId, Guid.Empty, ActivityId, ActionType.Complete, token, DefaultTtl);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Issue_requires_non_empty_activity_id()
    {
        var token = ActionToken.Issue();
        var act = () => DigestActionToken.Issue(TenantId, UserId, Guid.Empty, ActionType.Complete, token, DefaultTtl);
        act.Should().Throw<ArgumentException>();
    }

    // ---------------------------------------------------------------
    // Segurança: token em claro nunca acessível após construção
    // ---------------------------------------------------------------

    [Fact]
    public void Entity_does_not_expose_clear_token()
    {
        var token = ActionToken.Issue();
        var entity = DigestActionToken.Issue(TenantId, UserId, ActivityId, ActionType.Complete, token, DefaultTtl);

        // A entidade só expõe TokenHash, não o token em claro
        var properties = typeof(DigestActionToken).GetProperties();
        var hasClearToken = properties.Any(p =>
            p.Name.Contains("Clear", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Plain", StringComparison.OrdinalIgnoreCase));

        hasClearToken.Should().BeFalse("a entidade não deve expor o token em claro (DD-007)");
    }

    // ---------------------------------------------------------------
    // Ciclo de vida
    // ---------------------------------------------------------------

    [Fact]
    public void IsExpired_returns_false_when_expires_at_is_future()
    {
        var token = ActionToken.Issue();
        var entity = DigestActionToken.Issue(TenantId, UserId, ActivityId, ActionType.Complete, token, DefaultTtl);
        entity.IsExpired(DateTimeOffset.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void IsExpired_returns_true_when_expires_at_is_past()
    {
        var token = ActionToken.Issue();
        var entity = DigestActionToken.Issue(TenantId, UserId, ActivityId, ActionType.Complete, token, DefaultTtl);
        // Simula "agora" como 49 horas no futuro (além das 48h default)
        entity.IsExpired(DateTimeOffset.UtcNow.AddHours(49)).Should().BeTrue();
    }

    [Fact]
    public void IsExpired_respects_custom_ttl()
    {
        // VAL-ACT-02: token com TTL de 24h expira antes das 48h default
        var shortTtl = TimeSpan.FromHours(24);
        var token = ActionToken.Issue();
        var entity = DigestActionToken.Issue(TenantId, UserId, ActivityId, ActionType.Complete, token, shortTtl);

        entity.IsExpired(DateTimeOffset.UtcNow.AddHours(25)).Should().BeTrue(
            because: "token com TTL de 24h deve expirar após 25h");
        entity.IsExpired(DateTimeOffset.UtcNow.AddHours(23)).Should().BeFalse(
            because: "token com TTL de 24h não deve expirar em 23h");
    }

    [Fact]
    public void UsedAt_is_null_on_creation()
    {
        var token = ActionToken.Issue();
        var entity = DigestActionToken.Issue(TenantId, UserId, ActivityId, ActionType.Complete, token, DefaultTtl);
        entity.UsedAt.Should().BeNull();
    }
}
