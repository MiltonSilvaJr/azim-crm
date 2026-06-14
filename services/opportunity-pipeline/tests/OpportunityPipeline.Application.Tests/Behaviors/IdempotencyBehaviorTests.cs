using FluentAssertions;
using NSubstitute;
using OpportunityPipeline.Application.Behaviors;
using OpportunityPipeline.Application.Common;
using Xunit;

namespace OpportunityPipeline.Application.Tests.Behaviors;

/// <summary>
/// Testes do IdempotencyBehavior.
/// Cobre: ST-02 (mesma Idempotency-Key no mesmo tenant retorna resposta cacheada sem invocar handler).
/// Mapeia: NFR-RES-02, design §5.4, design §6.5, TASK-08.
/// </summary>
public sealed class IdempotencyBehaviorTests
{
    private sealed class IdempotentWriteCommand : IAuthenticatedCommand, IIdempotentCommand
    {
        public required UserRole UserRole { get; init; }
        public string CorrelationId { get; init; } = "test-correlation";
        public string? IdempotencyKey { get; init; }
    }

    private sealed class NonIdempotentCommand : IAuthenticatedCommand
    {
        public UserRole UserRole => UserRole.Vendedor;
        public string CorrelationId => "test-correlation";
    }

    private static TenantContext BuildTenantContext()
    {
        var ctx = new TenantContext();
        ctx.Initialize(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        return ctx;
    }

    [Fact]
    public async Task Mesma_chave_mesmo_tenant_retorna_resposta_cacheada_sem_invocar_handler()
    {
        // Arrange
        var store = Substitute.For<IIdempotencyStore>();
        var tenantContext = BuildTenantContext();
        var behavior = new IdempotencyBehavior<IdempotentWriteCommand, string>(store, tenantContext);

        var command = new IdempotentWriteCommand
        {
            UserRole = UserRole.Vendedor,
            IdempotencyKey = "key-abc"
        };

        // JSON serializado da resposta cacheada
        store.TryGetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("\"resposta-cacheada\"");

        var handlerCallCount = 0;
        Task<string> Handler(CancellationToken _)
        {
            handlerCallCount++;
            return Task.FromResult("nova-resposta");
        }

        // Act
        var result = await behavior.Handle(command, Handler, CancellationToken.None);

        // Assert
        result.Should().Be("resposta-cacheada");
        handlerCallCount.Should().Be(0, "handler não deve ser invocado na segunda requisição");
        await store.DidNotReceive().SetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Primeira_requisicao_com_chave_nova_invoca_handler_e_cacheia_resposta()
    {
        // Arrange
        var store = Substitute.For<IIdempotencyStore>();
        var tenantContext = BuildTenantContext();
        var behavior = new IdempotencyBehavior<IdempotentWriteCommand, string>(store, tenantContext);

        var command = new IdempotentWriteCommand
        {
            UserRole = UserRole.Vendedor,
            IdempotencyKey = "key-nova"
        };

        store.TryGetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var handlerCallCount = 0;
        Task<string> Handler(CancellationToken _)
        {
            handlerCallCount++;
            return Task.FromResult("resposta-handler");
        }

        // Act
        var result = await behavior.Handle(command, Handler, CancellationToken.None);

        // Assert
        result.Should().Be("resposta-handler");
        handlerCallCount.Should().Be(1);
        await store.Received(1).SetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Command_sem_IdempotencyKey_nao_participa_do_cache()
    {
        // Arrange
        var store = Substitute.For<IIdempotencyStore>();
        var tenantContext = BuildTenantContext();
        var behavior = new IdempotencyBehavior<IdempotentWriteCommand, string>(store, tenantContext);

        var command = new IdempotentWriteCommand
        {
            UserRole = UserRole.Vendedor,
            IdempotencyKey = null // sem chave
        };

        var handlerCallCount = 0;
        Task<string> Handler(CancellationToken _)
        {
            handlerCallCount++;
            return Task.FromResult("resposta");
        }

        // Act
        var result = await behavior.Handle(command, Handler, CancellationToken.None);

        // Assert
        result.Should().Be("resposta");
        handlerCallCount.Should().Be(1);
        await store.DidNotReceive().TryGetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Command_que_nao_implementa_IIdempotentCommand_e_repassado_diretamente()
    {
        // Arrange
        var store = Substitute.For<IIdempotencyStore>();
        var tenantContext = BuildTenantContext();
        var behavior = new IdempotencyBehavior<NonIdempotentCommand, string>(store, tenantContext);

        var command = new NonIdempotentCommand();
        var handlerCallCount = 0;
        Task<string> Handler(CancellationToken _)
        {
            handlerCallCount++;
            return Task.FromResult("resposta-direta");
        }

        // Act
        var result = await behavior.Handle(command, Handler, CancellationToken.None);

        // Assert
        result.Should().Be("resposta-direta");
        handlerCallCount.Should().Be(1);
        await store.DidNotReceive().TryGetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Chaves_de_tenants_diferentes_nao_colidem()
    {
        // Arrange — dois contextos distintos com mesma IdempotencyKey literal
        var store = Substitute.For<IIdempotencyStore>();

        var tenant1 = new TenantContext();
        tenant1.Initialize(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var tenant2 = new TenantContext();
        tenant2.Initialize(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var beh1 = new IdempotencyBehavior<IdempotentWriteCommand, string>(store, tenant1);
        var beh2 = new IdempotencyBehavior<IdempotentWriteCommand, string>(store, tenant2);

        var command = new IdempotentWriteCommand { UserRole = UserRole.Vendedor, IdempotencyKey = "mesma-key" };

        store.TryGetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        // Act
        await beh1.Handle(command, _ => Task.FromResult("r1"), CancellationToken.None);
        await beh2.Handle(command, _ => Task.FromResult("r2"), CancellationToken.None);

        // Assert — dois SetAsync com chaves distintas (tenant diferente no prefixo)
        await store.Received(2).SetAsync(
            Arg.Is<string>(k => k.StartsWith(tenant1.TenantId.ToString()) || k.StartsWith(tenant2.TenantId.ToString())),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}
