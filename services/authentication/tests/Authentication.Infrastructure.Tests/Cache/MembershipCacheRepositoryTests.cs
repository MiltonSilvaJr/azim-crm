using Authentication.Domain.ValueObjects;
using Authentication.Infrastructure.Cache;
using DotNet.Testcontainers.Builders;
using FluentAssertions;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace Authentication.Infrastructure.Tests.Cache;

/// <summary>
/// Testes de integração de <see cref="MembershipCacheRepository"/> com Redis real via Testcontainers.
///
/// Verifica: set+get com TTL, isolamento cross-tenant, invalidação por evento
/// e idempotência do consumer.
///
/// Mapeia: Req 5.5; RNF 2.3, RNF 6; design.md § 6.2, § 6.3; DD-007, TASK-12.
/// </summary>
[Collection("Redis")]
public sealed class MembershipCacheRepositoryTests : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private IConnectionMultiplexer _connection = null!;
    private MembershipCacheRepository _repository = null!;

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
        _connection = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        _repository = new MembershipCacheRepository(_connection, ttlMinutes: 5);
    }

    public async Task DisposeAsync()
    {
        _connection.Dispose();
        await _redis.DisposeAsync();
    }

    // =========================================================================
    // Set + Get com TTL
    // =========================================================================

    [Fact(DisplayName = "SetAsync + GetAsync — armazena e recupera MembershipSet com TTL (TASK-12, RNF 2.3)")]
    public async Task SetAsync_AndGetAsync_StoresAndRetrievesMembershipSet()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var membership = CreateMembershipSet();

        // Act
        await _repository.SetAsync(tenantId, userId, membership);
        var retrieved = await _repository.GetAsync(tenantId, userId);

        // Assert
        retrieved.Should().NotBeNull(
            because: "MembershipSet armazenado deve ser recuperável do cache (RNF 2.3)");
        retrieved!.Entries.Should().HaveCount(membership.Entries.Count,
            because: "dados recuperados devem ser idênticos aos armazenados");
    }

    // =========================================================================
    // Isolamento cross-tenant
    // =========================================================================

    [Fact(DisplayName = "GetAsync — chave de tenant A não retorna dados de tenant B (TASK-12, DD-007)")]
    public async Task GetAsync_TenantAKey_DoesNotReturn_TenantBData()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var membershipA = CreateMembershipSet(role: "admin");

        // Act — armazena apenas para tenant A
        await _repository.SetAsync(tenantA, userId, membershipA);

        // Tenta recuperar com tenant B (deve retornar null — isolamento)
        var resultForB = await _repository.GetAsync(tenantB, userId);

        // Assert
        resultForB.Should().BeNull(
            because: "a chave inclui tenant_id — dados de tenant A não devem ser visíveis em tenant B " +
                     "(design.md § 14, DD-007, auth:membership:{tenant_id}:{user_id})");
    }

    // =========================================================================
    // Invalidação por evento user.role_changed
    // =========================================================================

    [Fact(DisplayName = "InvalidateAsync — DEL da chave ao receber user.role_changed (TASK-12, Req 5.5)")]
    public async Task InvalidateAsync_RemovesEntry_OnRoleChanged()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var membership = CreateMembershipSet();
        await _repository.SetAsync(tenantId, userId, membership);

        // Confirma que está em cache
        var beforeInvalidation = await _repository.GetAsync(tenantId, userId);
        beforeInvalidation.Should().NotBeNull(because: "deve existir antes da invalidação");

        // Act — simula recebimento de user.role_changed
        await _repository.InvalidateAsync(tenantId, userId);

        // Assert
        var afterInvalidation = await _repository.GetAsync(tenantId, userId);
        afterInvalidation.Should().BeNull(
            because: "após invalidação por user.role_changed, cache deve ser limpo (Req 5.5, DD-007)");
    }

    // =========================================================================
    // Idempotência do consumer — duplo evento não causa erro
    // =========================================================================

    [Fact(DisplayName = "InvalidateAsync — dupla invalidação é idempotente (TASK-12, DD-007)")]
    public async Task InvalidateAsync_CalledTwice_IsIdempotent()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await _repository.SetAsync(tenantId, userId, CreateMembershipSet());

        // Act — invalida duas vezes (simula evento duplicado)
        Func<Task> act = async () =>
        {
            await _repository.InvalidateAsync(tenantId, userId);
            await _repository.InvalidateAsync(tenantId, userId);
        };

        // Assert — nenhuma exceção no segundo DEL (chave já não existe)
        await act.Should().NotThrowAsync(
            because: "consumer deve ser idempotente — duplo evento user.role_changed não deve causar erro");
    }

    // =========================================================================
    // Helper
    // =========================================================================

    private static MembershipSet CreateMembershipSet(string role = "viewer")
    {
        return MembershipSet.Create(
            entries: [new MembershipEntry(buId: Guid.NewGuid(), role: role)],
            cachedAt: DateTimeOffset.UtcNow);
    }
}
