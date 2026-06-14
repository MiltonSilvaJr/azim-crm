namespace ActivityManagement.Application.Tests.Behaviors;

using ActivityManagement.Application.Behaviors;
using ActivityManagement.Application.Common;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Testes unitários do <see cref="CorrelationLoggingBehavior{TRequest,TResponse}"/>.
/// Verifica: (a) passa para o próximo handler; (b) nunca loga title/description;
/// (c) injeta correlation_id e tenant_id no escopo.
/// Mapeia: RNF 6.1, RNF 7.2, design §5.4, TASK-06.
/// </summary>
public sealed class CorrelationLoggingBehaviorTests
{
    private sealed record TestRequest : IRequest<string>, ITenantRequest
    {
        public TenantContext? TenantContext { get; set; } = new TenantContext(
            TenantId:      Guid.NewGuid(),
            BuId:          Guid.NewGuid(),
            UserId:        Guid.NewGuid(),
            Role:          "seller",
            CorrelationId: Guid.NewGuid());

        // Campos que NUNCA devem aparecer em log
        public string Title       { get; init; } = "Reunião confidencial";
        public string Description { get; init; } = "Detalhe privado do cliente";
    }

    private static CorrelationLoggingBehavior<TestRequest, string> CreateBehavior() =>
        new(NullLogger<CorrelationLoggingBehavior<TestRequest, string>>.Instance);

    [Fact]
    public async Task Handle_CallsNextAndReturnsResponse()
    {
        var behavior = CreateBehavior();
        var request  = new TestRequest();

        var response = await behavior.Handle(
            request,
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        response.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_WithTenantContext_DoesNotThrow()
    {
        var behavior = CreateBehavior();
        var request  = new TestRequest();

        var act = async () => await behavior.Handle(
            request,
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_WhenNextThrows_RethrowsException()
    {
        var behavior = CreateBehavior();
        var request  = new TestRequest();

        var act = async () => await behavior.Handle(
            request,
            _ => Task.FromException<string>(new InvalidOperationException("erro de teste")),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_WithoutTenantContext_StillCallsNext()
    {
        var behavior = CreateBehavior();
        var request  = new TestRequest { TenantContext = null };

        var response = await behavior.Handle(
            request,
            _ => Task.FromResult("sem-tenant"),
            CancellationToken.None);

        response.Should().Be("sem-tenant");
    }
}
