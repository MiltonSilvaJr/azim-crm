using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Organization.Application.Ports;
using Organization.Infrastructure.Cache;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace Organization.Infrastructure.Tests.Cache;

/// <summary>
/// Testes de integração de <see cref="RedisMembershipCache"/> via Testcontainers Redis.
/// Valida: GET, SET, invalidação, degradação segura e ausência de PII nas chaves.
/// TASK-17 (Onda 4 — Infrastructure).
/// </summary>
[Trait("Category", "Integration")]
public sealed class RedisMembershipCacheTests : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private IConnectionMultiplexer _multiplexer = null!;

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
        _multiplexer = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        _multiplexer.Dispose();
        await _redis.DisposeAsync();
    }

    private RedisMembershipCache CreateCache(TimeSpan? ttl = null)
    {
        var opts = Options.Create(new MembershipCacheOptions
        {
            Ttl = ttl ?? TimeSpan.FromMinutes(5),
        });
        return new RedisMembershipCache(
            _multiplexer, opts, NullLogger<RedisMembershipCache>.Instance,
            Substitute.For<IOrganizationMetrics>());
    }

    private static MembershipCacheEntry MakeEntry(params (Guid buId, string role)[] memberships)
    {
        var dict = memberships.ToDictionary(m => m.buId, m => m.role);
        return new MembershipCacheEntry(dict, DateTimeOffset.UtcNow);
    }

    // ── ST-06: Set e Get armazenam e recuperam corretamente ──────────────

    [Fact(DisplayName = "ST-06: SetAsync armazena e GetAsync recupera MembershipCacheEntry")]
    public async Task Set_Get_RoundTrip()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var buId = Guid.NewGuid();
        var entry = MakeEntry((buId, "Vendedor"));
        var cache = CreateCache();

        // Act
        await cache.SetAsync(tenantId, userId, entry);
        var result = await cache.GetAsync(tenantId, userId);

        // Assert
        result.Should().NotBeNull();
        result!.RolesByBu.Should().ContainKey(buId);
        result.RolesByBu[buId].Should().Be("Vendedor");
    }

    // ── ST-06: Get retorna null em cache miss ─────────────────────────────

    [Fact(DisplayName = "ST-06: GetAsync retorna null em cache miss")]
    public async Task Get_RetornaNullEmMiss()
    {
        // Arrange
        var cache = CreateCache();

        // Act
        var result = await cache.GetAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    // ── ST-06: Invalidação remove a entrada ──────────────────────────────

    [Fact(DisplayName = "ST-06: InvalidateAsync remove entrada do cache")]
    public async Task Invalidate_RemoveEntrada()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var entry = MakeEntry((Guid.NewGuid(), "TAdmin"));
        var cache = CreateCache();

        await cache.SetAsync(tenantId, userId, entry);

        // Act
        await cache.InvalidateAsync(tenantId, userId);
        var after = await cache.GetAsync(tenantId, userId);

        // Assert
        after.Should().BeNull("entrada deve ser removida após InvalidateAsync");
    }

    // ── ST-06: TTL expira a entrada corretamente ─────────────────────────

    [Fact(DisplayName = "ST-06: Entrada expira após TTL")]
    public async Task Entry_ExpiraAposTtl()
    {
        // Arrange — TTL de 200ms para testar expiração sem delay longo
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var entry = MakeEntry((Guid.NewGuid(), "Viewer"));
        var cache = CreateCache(ttl: TimeSpan.FromMilliseconds(200));

        await cache.SetAsync(tenantId, userId, entry);

        // Act
        await Task.Delay(350); // aguarda expiração
        var after = await cache.GetAsync(tenantId, userId);

        // Assert
        after.Should().BeNull("entrada deve expirar após TTL");
    }

    // ── ST-06: Isolamento por tenant — chaves distintas ──────────────────

    [Fact(DisplayName = "ST-06: Chaves de tenants distintos não colidem")]
    public async Task Chaves_TenantsDivergem()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var buA = Guid.NewGuid();
        var buB = Guid.NewGuid();
        var entryA = MakeEntry((buA, "GestorBU"));
        var entryB = MakeEntry((buB, "Vendedor"));
        var cache = CreateCache();

        // Act
        await cache.SetAsync(tenantA, userId, entryA);
        await cache.SetAsync(tenantB, userId, entryB);

        var resultA = await cache.GetAsync(tenantA, userId);
        var resultB = await cache.GetAsync(tenantB, userId);

        // Assert
        resultA!.RolesByBu.Should().ContainKey(buA);
        resultB!.RolesByBu.Should().ContainKey(buB);
        resultA.RolesByBu.Should().NotContainKey(buB, "tenantA não deve ver entrada de tenantB");
        resultB.RolesByBu.Should().NotContainKey(buA, "tenantB não deve ver entrada de tenantA");
    }

    // ── ST-06: Degradação segura com Redis indisponível ──────────────────

    [Fact(DisplayName = "ST-06: GetAsync retorna null quando Redis está indisponível")]
    public async Task Get_RetornaNullQuandoRedisIndisponivel()
    {
        // Arrange — multiplexer que falha
        var failingRedis = Substitute.For<IConnectionMultiplexer>();
        var failingDb = Substitute.For<IDatabase>();
        failingRedis.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(failingDb);
        failingDb.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns<RedisValue>(_ => throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "host down"));

        var opts = Options.Create(new MembershipCacheOptions { Ttl = TimeSpan.FromMinutes(5) });
        var cache = new RedisMembershipCache(
            failingRedis, opts, NullLogger<RedisMembershipCache>.Instance,
            Substitute.For<IOrganizationMetrics>());

        // Act — não deve lançar exceção
        var result = await cache.GetAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().BeNull("degradação segura: Redis indisponível retorna null");
    }

    [Fact(DisplayName = "ST-06: SetAsync não propaga exceção quando Redis está indisponível")]
    public async Task Set_NaoPropagaExcecaoQuandoRedisIndisponivel()
    {
        // Arrange
        var failingRedis = Substitute.For<IConnectionMultiplexer>();
        var failingDb = Substitute.For<IDatabase>();
        failingRedis.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(failingDb);
        failingDb.StringSetAsync(
                Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns<bool>(_ => throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "host down"));

        var opts = Options.Create(new MembershipCacheOptions { Ttl = TimeSpan.FromMinutes(5) });
        var cache = new RedisMembershipCache(
            failingRedis, opts, NullLogger<RedisMembershipCache>.Instance,
            Substitute.For<IOrganizationMetrics>());
        var entry = MakeEntry((Guid.NewGuid(), "Viewer"));

        // Act + Assert — não deve lançar
        await FluentActions.Invoking(() => cache.SetAsync(Guid.NewGuid(), Guid.NewGuid(), entry))
            .Should().NotThrowAsync();
    }

    // ── ST-06: Ausência de PII na chave Redis ────────────────────────────

    [Fact(DisplayName = "ST-06: Chaves Redis não contêm e-mail nem dados PII")]
    public async Task Chaves_NaoContemPii()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var entry = MakeEntry((Guid.NewGuid(), "Viewer"));
        var cache = CreateCache();
        await cache.SetAsync(tenantId, userId, entry);

        // Act — inspeciona chaves no Redis diretamente
        var server = _multiplexer.GetServer(_redis.GetConnectionString());
        var keys = server.Keys(pattern: "org:rbac:*").Select(k => k.ToString()).ToList();

        // Assert
        keys.Should().NotBeEmpty();
        keys.Should().AllSatisfy(k =>
        {
            k.Should().NotContain("@", "chave não deve conter e-mail");
            k.Should().NotContain("name", "chave não deve conter nome");
            // Formato esperado: org:rbac:{guid}:{guid}
            k.Should().MatchRegex(@"^org:rbac:[0-9a-f\-]{36}:[0-9a-f\-]{36}$");
        });
    }
}
