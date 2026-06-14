using FluentAssertions;
using NSubstitute;
using Organization.Application.Ports;
using Organization.Application.Queries;
using Organization.Domain.Aggregates;
using Organization.Domain.ValueObjects;
using Xunit;

namespace Organization.Application.Tests.Queries;

/// <summary>
/// Testes unitários para <see cref="GetRbacContextQueryHandler"/> e <see cref="MembershipCacheProjector"/>.
/// </summary>
public sealed class RbacContextQueryTests
{
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IMembershipCache _cache = Substitute.For<IMembershipCache>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public RbacContextQueryTests()
    {
        _tenantContext.TenantId.Returns(_tenantId);
        _tenantContext.UserId.Returns(Guid.NewGuid());
        _tenantContext.CorrelationId.Returns(Guid.NewGuid());
        _tenantContext.RolesByBu.Returns(new Dictionary<Guid, string>());
        _clock.UtcNow.Returns(_now);
    }

    [Fact]
    public async Task GetRbacContext_CacheHit_ReturnsCachedRoles()
    {
        // Arrange — cache hit
        var buId = Guid.NewGuid();
        var cacheEntry = new MembershipCacheEntry(
            new Dictionary<Guid, string> { [buId] = "TAdmin" },
            _now);

        _cache.GetAsync(_tenantId, _userId, Arg.Any<CancellationToken>())
            .Returns(cacheEntry);

        var handler = new GetRbacContextQueryHandler(_userRepo, _cache, _clock, _tenantContext);

        // Act
        var result = await handler.Handle(new GetRbacContextQuery(_userId), CancellationToken.None);

        // Assert — cache foi usado; banco não foi consultado
        result.RolesByBu.Should().ContainKey(buId).WhoseValue.Should().Be("TAdmin");
        await _userRepo.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetRbacContext_CacheMiss_HydratesFromDatabase()
    {
        // Arrange — cache miss; banco tem memberships
        var buId = Guid.NewGuid();
        var user = CreateUserWithMembership(buId, Role.GestorBU);
        _cache.GetAsync(_tenantId, _userId, Arg.Any<CancellationToken>()).Returns((MembershipCacheEntry?)null);
        _userRepo.GetByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(user);

        var handler = new GetRbacContextQueryHandler(_userRepo, _cache, _clock, _tenantContext);

        // Act
        var result = await handler.Handle(new GetRbacContextQuery(_userId), CancellationToken.None);

        // Assert — banco consultado e cache atualizado
        result.RolesByBu.Should().ContainKey(buId).WhoseValue.Should().Be("GestorBU");
        await _cache.Received(1).SetAsync(
            _tenantId,
            _userId,
            Arg.Any<MembershipCacheEntry>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetRbacContext_CacheMiss_NoUserInDatabase_ReturnsDenyByDefault()
    {
        // Arrange — cache miss + usuário não encontrado (degradação segura)
        _cache.GetAsync(_tenantId, _userId, Arg.Any<CancellationToken>()).Returns((MembershipCacheEntry?)null);
        _userRepo.GetByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns((Organization.Domain.Aggregates.User?)null);

        var handler = new GetRbacContextQueryHandler(_userRepo, _cache, _clock, _tenantContext);

        // Act
        var result = await handler.Handle(new GetRbacContextQuery(_userId), CancellationToken.None);

        // Assert — deny-by-default: mapa vazio, sem PII
        result.RolesByBu.Should().BeEmpty();
        result.Email.Should().BeNull();
        result.DisplayName.Should().BeNull();
    }

    [Fact]
    public async Task GetRbacContext_Response_NeverIncludesPii()
    {
        // Arrange — cache miss; banco tem usuário com e-mail e nome
        var buId = Guid.NewGuid();
        var user = CreateUserWithMembership(buId, Role.Vendedor);
        _cache.GetAsync(_tenantId, _userId, Arg.Any<CancellationToken>()).Returns((MembershipCacheEntry?)null);
        _userRepo.GetByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(user);

        var handler = new GetRbacContextQueryHandler(_userRepo, _cache, _clock, _tenantContext);

        // Act
        var result = await handler.Handle(new GetRbacContextQuery(_userId), CancellationToken.None);

        // Assert — PII não exposta no resultado de RBAC (Req 13.3, RNF 3.3)
        result.Email.Should().BeNull("resposta RBAC não deve expor e-mail");
        result.DisplayName.Should().BeNull("resposta RBAC não deve expor nome de exibição");
    }

    // ── Helpers ──

    private Organization.Domain.Aggregates.User CreateUserWithMembership(Guid buId, Role role)
    {
        var user = Organization.Domain.Aggregates.User.Activate(
            "user@test.com", "Usuário Teste", "uid-test", _tenantId, _now);
        user.AssignMembership(buId, role, Guid.NewGuid());
        return user;
    }
}
