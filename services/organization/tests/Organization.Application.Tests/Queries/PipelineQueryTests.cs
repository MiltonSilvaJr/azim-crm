using FluentAssertions;
using NSubstitute;
using Organization.Application.Ports;
using Organization.Application.Queries;
using Organization.Domain.Aggregates;
using Organization.Domain.ValueObjects;
using Xunit;

namespace Organization.Application.Tests.Queries;

/// <summary>
/// Testes unitários para as queries de pipeline e configuração da BU.
/// </summary>
public sealed class PipelineQueryTests
{
    private readonly IBusinessUnitRepository _buRepo = Substitute.For<IBusinessUnitRepository>();
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public PipelineQueryTests()
    {
        _tenantContext.TenantId.Returns(_tenantId);
        _tenantContext.UserId.Returns(Guid.NewGuid());
        _tenantContext.CorrelationId.Returns(Guid.NewGuid());
        _tenantContext.RolesByBu.Returns(new Dictionary<Guid, string>());
    }

    // ── ListStagesQuery ────────────────────────────────────────────────────────

    [Fact]
    public async Task ListStages_ReturnsStagesOrderedByPosition()
    {
        // Arrange — BU com 3 estágios fora de ordem de inserção
        var bu = CreateActiveBu();
        bu.AddStage("C", Probability.Create(90), StageCategory.Won, 3, Guid.NewGuid());
        bu.AddStage("A", Probability.Create(10), StageCategory.Open, 1, Guid.NewGuid());
        bu.AddStage("B", Probability.Create(50), StageCategory.Open, 2, Guid.NewGuid());
        _buRepo.GetByIdAsync(bu.Id, Arg.Any<CancellationToken>()).Returns(bu);

        var handler = new ListStagesQueryHandler(_buRepo, _tenantContext);

        // Act
        var result = await handler.Handle(new ListStagesQuery(bu.Id), CancellationToken.None);

        // Assert — ordenados por position (1, 2, 3)
        result.Should().HaveCount(3);
        result.Select(s => s.Position).Should().BeInAscendingOrder();
        result.First().Name.Should().Be("A");
        result.Last().Name.Should().Be("C");
    }

    // ── ListBusinessUnitsQuery ─────────────────────────────────────────────────

    [Fact]
    public async Task ListBusinessUnits_ReturnsOnlyActiveBus()
    {
        // Arrange
        var buActive = CreateActiveBu();
        _buRepo.ListActiveAsync(1, 20, Arg.Any<CancellationToken>())
            .Returns(new List<BusinessUnit> { buActive }.AsReadOnly());

        var handler = new ListBusinessUnitsQueryHandler(_buRepo, _tenantContext);

        // Act
        var result = await handler.Handle(new ListBusinessUnitsQuery(1, 20), CancellationToken.None);

        // Assert
        result.Should().ContainSingle();
        result.First().Name.Should().Be("BU Teste");
    }

    // ── ListOriginChannelsQuery ────────────────────────────────────────────────

    [Fact]
    public async Task ListOriginChannels_ReturnsOnlyActiveChannels()
    {
        // Arrange
        var bu = CreateActiveBu();
        var channelActiveId = Guid.NewGuid();
        var channelInactiveId = Guid.NewGuid();
        bu.AddOriginChannel("Instagram", channelActiveId);
        bu.AddOriginChannel("Facebook", channelInactiveId);
        bu.DeactivateOriginChannel(channelInactiveId);
        _buRepo.GetByIdAsync(bu.Id, Arg.Any<CancellationToken>()).Returns(bu);

        var handler = new ListOriginChannelsQueryHandler(_buRepo, _tenantContext);

        // Act
        var result = await handler.Handle(new ListOriginChannelsQuery(bu.Id), CancellationToken.None);

        // Assert — apenas canais ativos
        result.Should().ContainSingle(c => c.Name == "Instagram");
        result.Should().NotContain(c => c.Name == "Facebook");
    }

    // ── ListLossReasonsQuery ───────────────────────────────────────────────────

    [Fact]
    public async Task ListLossReasons_ReturnsOnlyActiveReasons()
    {
        // Arrange
        var bu = CreateActiveBu();
        var reasonActiveId = Guid.NewGuid();
        var reasonInactiveId = Guid.NewGuid();
        bu.AddLossReason("Preço", reasonActiveId);
        bu.AddLossReason("Produto inadequado", reasonInactiveId);
        bu.DeactivateLossReason(reasonInactiveId);
        _buRepo.GetByIdAsync(bu.Id, Arg.Any<CancellationToken>()).Returns(bu);

        var handler = new ListLossReasonsQueryHandler(_buRepo, _tenantContext);

        // Act
        var result = await handler.Handle(new ListLossReasonsQuery(bu.Id), CancellationToken.None);

        // Assert — apenas motivos ativos
        result.Should().ContainSingle(r => r.Name == "Preço");
        result.Should().NotContain(r => r.Name == "Produto inadequado");
    }

    // ── ListUsersQuery ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ListUsers_ReturnsActiveUsersWithRoles()
    {
        // Arrange
        var user = Organization.Domain.Aggregates.User.Activate(
            "u@test.com", "Usuário", "uid-1", _tenantId, _now);
        var buId = Guid.NewGuid();
        user.AssignMembership(buId, Role.GestorBU, Guid.NewGuid());
        _userRepo.ListActiveAsync(1, 20, Arg.Any<CancellationToken>())
            .Returns(new List<Organization.Domain.Aggregates.User> { user }.AsReadOnly());

        var handler = new ListUsersQueryHandler(_userRepo, _tenantContext);

        // Act
        var result = await handler.Handle(new ListUsersQuery(1, 20), CancellationToken.None);

        // Assert
        result.Should().ContainSingle();
        result.First().Email.Should().Be("u@test.com");
        result.First().Memberships.Should().ContainSingle(m => m.Role == "GestorBU");
    }

    // ── ListMembershipsQuery ───────────────────────────────────────────────────

    [Fact]
    public async Task ListMemberships_ReturnsMembershipsForUser()
    {
        // Arrange
        var user = Organization.Domain.Aggregates.User.Activate(
            "u@test.com", "Usuário", "uid-1", _tenantId, _now);
        var buId1 = Guid.NewGuid();
        var buId2 = Guid.NewGuid();
        user.AssignMembership(buId1, Role.TAdmin, Guid.NewGuid());
        user.AssignMembership(buId2, Role.Vendedor, Guid.NewGuid());
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var handler = new ListMembershipsQueryHandler(_userRepo, _tenantContext);

        // Act
        var result = await handler.Handle(new ListMembershipsQuery(user.Id), CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(m => m.BuId == buId1 && m.Role == "TAdmin");
        result.Should().Contain(m => m.BuId == buId2 && m.Role == "Vendedor");
    }

    // ── Helpers ──

    private BusinessUnit CreateActiveBu()
        => BusinessUnit.Create(BusinessUnitName.Create("BU Teste"), _tenantId, _now);
}
