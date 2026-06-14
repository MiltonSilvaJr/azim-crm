using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PartnerManagement.Infrastructure.Idempotency;
using PartnerManagement.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace PartnerManagement.Infrastructure.Tests.Idempotency;

/// <summary>
/// Testes de integração para <see cref="IdempotencyKeyRepository"/> (TASK-20).
/// Verifica: deduplicação (Find existente retorna entry), Add persiste nova chave,
/// isolamento por tenant (cross-tenant não encontra a chave).
/// Usa PostgreSQL real via Testcontainers.
/// Mapeia: design §6.5, PM-ERR-010, TASK-20.
/// </summary>
[Trait("Category", "Integration")]
public sealed class IdempotencyKeyRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    private string _connectionString = null!;

    public IdempotencyKeyRepositoryTests()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("partner_management_idempotency_test")
            .WithUsername("app_test")
            .WithPassword("test_secret")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        using PartnerManagementDbContext ctx = CreateDbContext(Guid.NewGuid());
        await ctx.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    // =========================================================================
    // AddAsync + FindAsync (deduplicação)
    // =========================================================================

    /// <summary>
    /// TASK-20: Add + FindAsync retorna a chave persistida para o mesmo tenant.
    /// </summary>
    [Fact(DisplayName = "TASK-20: AddAsync e FindAsync retornam chave de idempotência")]
    public async Task AddAsync_ThenFindAsync_ReturnsExistingKey()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        string idempotencyKey = $"op_{Guid.NewGuid():N}";

        IdempotencyKey entry = IdempotencyKey.Create(
            tenantId,
            key: idempotencyKey,
            requestHash: "sha256_abc123",
            createdAt: DateTimeOffset.UtcNow);

        // Act — persiste
        await using (PartnerManagementDbContext ctxWrite = CreateDbContext(tenantId))
        {
            IdempotencyKeyRepository repo = new(ctxWrite);
            await repo.AddAsync(entry);
            await ctxWrite.SaveChangesAsync();
        }

        // Act — busca
        await using PartnerManagementDbContext ctxRead = CreateDbContext(tenantId);
        IdempotencyKeyRepository repoRead = new(ctxRead);
        IdempotencyKey? found = await repoRead.FindAsync(tenantId, idempotencyKey);

        // Assert
        found.Should().NotBeNull("chave de idempotência deve ser encontrada após Add");
        found!.Key.Should().Be(idempotencyKey);
        found.RequestHash.Should().Be("sha256_abc123");
        found.TenantId.Should().Be(tenantId);
    }

    /// <summary>
    /// TASK-20: FindAsync retorna null para chave inexistente (primeira entrega).
    /// </summary>
    [Fact(DisplayName = "TASK-20: FindAsync retorna null para chave inexistente")]
    public async Task FindAsync_ReturnsNull_WhenKeyDoesNotExist()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();

        await using PartnerManagementDbContext ctx = CreateDbContext(tenantId);
        IdempotencyKeyRepository repo = new(ctx);

        // Act
        IdempotencyKey? found = await repo.FindAsync(tenantId, "chave-inexistente");

        // Assert
        found.Should().BeNull("chave inexistente deve retornar null — primeira entrega");
    }

    /// <summary>
    /// TASK-20: FindAsync com tenant diferente não encontra chave de outro tenant.
    /// </summary>
    [Fact(DisplayName = "TASK-20: FindAsync não encontra chave de outro tenant (isolamento)")]
    public async Task FindAsync_CrossTenant_ReturnsNull()
    {
        // Arrange
        Guid tenantA = Guid.NewGuid();
        Guid tenantB = Guid.NewGuid();
        string key = $"op_{Guid.NewGuid():N}";

        IdempotencyKey entry = IdempotencyKey.Create(
            tenantA,
            key: key,
            requestHash: "sha256_xyz",
            createdAt: DateTimeOffset.UtcNow);

        await using (PartnerManagementDbContext ctxA = CreateDbContext(tenantA))
        {
            IdempotencyKeyRepository repoA = new(ctxA);
            await repoA.AddAsync(entry);
            await ctxA.SaveChangesAsync();
        }

        // Act — busca no contexto do tenant B com a mesma chave
        await using PartnerManagementDbContext ctxB = CreateDbContext(tenantB);
        IdempotencyKeyRepository repoB = new(ctxB);
        IdempotencyKey? found = await repoB.FindAsync(tenantB, key);

        // Assert — chave de tenant A não deve ser encontrada por tenant B
        found.Should().BeNull("isolamento de idempotência deve ser por tenant");
    }

    /// <summary>
    /// TASK-20: SetResponseRef atualiza a referência de resposta na chave.
    /// </summary>
    [Fact(DisplayName = "TASK-20: SetResponseRef persiste referência ao recurso criado")]
    public async Task SetResponseRef_PersistsResponseRef()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        string key = $"op_{Guid.NewGuid():N}";
        Guid partnerId = Guid.NewGuid();

        IdempotencyKey entry = IdempotencyKey.Create(
            tenantId, key: key, requestHash: "sha256_ref", createdAt: DateTimeOffset.UtcNow);

        await using (PartnerManagementDbContext ctxWrite = CreateDbContext(tenantId))
        {
            IdempotencyKeyRepository repo = new(ctxWrite);
            await repo.AddAsync(entry);
            await ctxWrite.SaveChangesAsync();
        }

        // Act — atualiza com referência ao recurso criado
        await using (PartnerManagementDbContext ctxUpdate = CreateDbContext(tenantId))
        {
            IdempotencyKeyRepository repoUpdate = new(ctxUpdate);
            IdempotencyKey? toUpdate = await repoUpdate.FindAsync(tenantId, key);
            toUpdate.Should().NotBeNull();
            toUpdate!.SetResponseRef(partnerId);
            await ctxUpdate.SaveChangesAsync();
        }

        // Assert
        await using PartnerManagementDbContext ctxRead = CreateDbContext(tenantId);
        IdempotencyKeyRepository repoRead = new(ctxRead);
        IdempotencyKey? found = await repoRead.FindAsync(tenantId, key);

        found!.ResponseRef.Should().Be(partnerId, "referência ao recurso deve estar persistida");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private PartnerManagementDbContext CreateDbContext(Guid tenantId)
    {
        DbContextOptions<PartnerManagementDbContext> options = new DbContextOptionsBuilder<PartnerManagementDbContext>()
            .UseNpgsql(_connectionString)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        return new PartnerManagementDbContext(options, new StaticTenantContext(tenantId));
    }

    private sealed class StaticTenantContext : Application.Ports.ITenantContext
    {
        public StaticTenantContext(Guid tenantId) => CurrentTenantId = tenantId;
        public Guid CurrentTenantId { get; }
    }
}
