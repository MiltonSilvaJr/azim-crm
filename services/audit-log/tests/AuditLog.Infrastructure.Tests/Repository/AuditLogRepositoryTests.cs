using AuditLog.Application.Abstractions;
using AuditLog.Domain.Aggregates;
using AuditLog.Domain.ValueObjects;
using AuditLog.Infrastructure.Persistence;
using AuditLog.Infrastructure.Repositories;
using AuditLog.Infrastructure.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace AuditLog.Infrastructure.Tests.Repository;

/// <summary>
/// Testes de integração para <see cref="AuditLogRepository"/>.
/// Verifica persistência, leitura, filtro global de tenant e restrição append-only no código.
/// </summary>
[Collection(PostgresTestCollection.Name)]
public sealed class AuditLogRepositoryTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private AuditLogDbContext _dbContext = null!;
    private AuditLogRepository _repository = null!;

    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();

    public AuditLogRepositoryTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Aplica migration para criar tabela (usa superuser — contorna REVOKE no setup)
        await using var migratorCtx = TestDbContextFactory.Create(
            _fixture.SuperuserConnectionString, TenantA);
        await migratorCtx.Database.MigrateAsync();

        RebuildContextForTenant(TenantA);
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.TruncateAuditLogsAsync();
    }

    // ------------------------------------------------------------------ AddAsync

    [Fact]
    public async Task AddAsync_ValidAggregate_PersistsToDatabase()
    {
        var log = BuildLog(TenantA);

        await _repository.AddAsync(log);
        await _dbContext.SaveChangesAsync();

        var logId = log.Id;
        var saved = await _dbContext.AuditLogs
            .FirstOrDefaultAsync(x => x.Id == logId);
        saved.Should().NotBeNull();
        saved!.Id.Value.Should().Be(log.Id.Value);
        saved.TenantId.Value.Should().Be(TenantA);
        saved.CreatedAt.Should().NotBe(default);
    }

    [Fact]
    public async Task AddAsync_CreatedAt_IsNeverNull()
    {
        var log = BuildLog(TenantA);

        await _repository.AddAsync(log);
        await _dbContext.SaveChangesAsync();

        var logId = log.Id;
        var saved = await _dbContext.AuditLogs
            .FirstOrDefaultAsync(x => x.Id == logId);
        saved!.CreatedAt.Should().NotBe(default(DateTimeOffset));
    }

    // ------------------------------------------------------------------ Filtro global de tenant

    [Fact]
    public async Task GlobalTenantFilter_QueriesReturnOnlyCurrentTenantRecords()
    {
        await InsertLogForTenantAsync(TenantA);
        await InsertLogForTenantAsync(TenantB);

        RebuildContextForTenant(TenantA);
        var items = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ToListAsync(_dbContext.AuditLogs);

        items.Should().OnlyContain(x => x.TenantId.Value == TenantA);
        items.Should().HaveCount(1);
    }

    // ------------------------------------------------------------------ FindByEntityAsync

    [Fact]
    public async Task FindByEntityAsync_ReturnsOnlyMatchingEntity()
    {
        var entityId = Guid.NewGuid();
        var log = BuildLog(TenantA, entityId: entityId);
        var otherLog = BuildLog(TenantA, entityType: "Account");

        await _repository.AddAsync(log);
        await _repository.AddAsync(otherLog);
        await _dbContext.SaveChangesAsync();

        var entityRef = EntityReference.Create("Opportunity", entityId);
        var results = await _repository.FindByEntityAsync(TenantId.From(TenantA), entityRef);

        results.Should().HaveCount(1);
        results[0].EntityReference.EntityType.Should().Be("Opportunity");
        results[0].EntityReference.EntityId.Should().Be(entityId);
    }

    // ------------------------------------------------------------------ ListAsync

    [Fact]
    public async Task ListAsync_Paginated_ReturnsCorrectPage()
    {
        for (var i = 0; i < 5; i++)
        {
            await _repository.AddAsync(BuildLog(TenantA));
        }
        await _dbContext.SaveChangesAsync();

        var (items, total) = await _repository.ListAsync(
            TenantId.From(TenantA), page: 1, pageSize: 3);

        items.Should().HaveCount(3);
        total.Should().Be(5);
    }

    // ------------------------------------------------------------------ Append-only no código

    [Fact]
    public void Repository_DoesNotExposeUpdateOrRemove()
    {
        var type = typeof(Domain.Repositories.IAuditLogRepository);
        type.GetMethod("UpdateAsync").Should().BeNull("repositório deve ser append-only (RNF-001)");
        type.GetMethod("RemoveAsync").Should().BeNull("repositório deve ser append-only (RNF-001)");
        type.GetMethod("DeleteAsync").Should().BeNull("repositório deve ser append-only (RNF-001)");
    }

    // ------------------------------------------------------------------ Helpers

    private void RebuildContextForTenant(Guid tenantId)
    {
        _dbContext?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _dbContext = TestDbContextFactory.Create(_fixture.SuperuserConnectionString, tenantId);
        _repository = new AuditLogRepository(_dbContext);
    }

    private async Task InsertLogForTenantAsync(Guid tenantId)
    {
        await using var ctx = TestDbContextFactory.Create(_fixture.SuperuserConnectionString, tenantId);
        var repo = new AuditLogRepository(ctx);
        await repo.AddAsync(BuildLog(tenantId));
        await ctx.SaveChangesAsync();
    }

    private static AuditLogAggregate BuildLog(
        Guid tenantId,
        Guid? entityId = null,
        string entityType = "Opportunity")
    {
        var clock = Substitute.For<Domain.Abstractions.IClock>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);

        return AuditLogAggregate.Create(
            TenantId.From(tenantId),
            ActorId.From(Actor),
            EntityReference.Create(entityType, entityId ?? Guid.NewGuid()),
            Domain.ValueObjects.AuditAction.Create,
            AuditDelta.ForCreate(new Dictionary<string, object?> { ["name"] = "Test" }),
            clock);
    }
}
