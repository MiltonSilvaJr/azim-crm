using GoalForecast.Infrastructure.Membership;
using GoalForecast.Infrastructure.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoalForecast.Infrastructure.Tests.Membership;

/// <summary>
/// Testes de integração do BuMembershipReader contra banco PostgreSQL real.
/// Verifica: owner membro ativo → true; owner não cadastrado → false; owner inativo → false.
/// Mapeia: TASK-20, Req 1.4, DD-004, design §6.5.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class BuMembershipReaderTests(PostgresContainerFixture db)
{
    // ── ST-01 Red ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task IsOwnerMemberOfBu_active_member_returns_true()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        await db.InsertBuMember(tenantId, buId, ownerId, active: true);

        var reader = new BuMembershipReader(db.ConnectionString, NullLogger<BuMembershipReader>.Instance);

        // Act
        var result = await reader.IsOwnerMemberOfBu(tenantId, ownerId, buId);

        // Assert
        result.Should().BeTrue("owner ativo da BU deve retornar true");
    }

    [Fact]
    public async Task IsOwnerMemberOfBu_non_member_returns_false()
    {
        // Arrange — owner não inserido em bu_members
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        var reader = new BuMembershipReader(db.ConnectionString, NullLogger<BuMembershipReader>.Instance);

        // Act
        var result = await reader.IsOwnerMemberOfBu(tenantId, ownerId, buId);

        // Assert
        result.Should().BeFalse("owner não cadastrado deve retornar false");
    }

    [Fact]
    public async Task IsOwnerMemberOfBu_inactive_member_returns_false()
    {
        // Arrange — owner cadastrado mas inativo
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        await db.InsertBuMember(tenantId, buId, ownerId, active: false);

        var reader = new BuMembershipReader(db.ConnectionString, NullLogger<BuMembershipReader>.Instance);

        // Act
        var result = await reader.IsOwnerMemberOfBu(tenantId, ownerId, buId);

        // Assert
        result.Should().BeFalse("owner inativo não deve ser considerado membro");
    }

    [Fact]
    public async Task IsOwnerMemberOfBu_wrong_tenant_returns_false()
    {
        // Arrange — owner membro em tenantA mas consultado com tenantB
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var buId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        await db.InsertBuMember(tenantA, buId, ownerId, active: true);

        var reader = new BuMembershipReader(db.ConnectionString, NullLogger<BuMembershipReader>.Instance);

        // Act — consulta com tenant errado
        var result = await reader.IsOwnerMemberOfBu(tenantB, ownerId, buId);

        // Assert
        result.Should().BeFalse("membership de outro tenant não deve ser visível");
    }
}
