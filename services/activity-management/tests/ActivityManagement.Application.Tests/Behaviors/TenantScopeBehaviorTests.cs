namespace ActivityManagement.Application.Tests.Behaviors;

using ActivityManagement.Application.Behaviors;
using ActivityManagement.Application.Common;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Testes unitários do <see cref="TenantScopeBehavior{TRequest,TResponse}"/>.
/// Verifica: falha-fechada quando TenantContext ausente; passagem quando presente;
/// requests sem marcador ITenantRequest passam diretamente.
/// Mapeia: RNF 1, DD-002, design §5.4, TASK-06.
/// </summary>
public sealed class TenantScopeBehaviorTests
{
    private sealed record RequestWithTenant : IRequest<string>, ITenantRequest
    {
        public TenantContext? TenantContext { get; set; }
    }

    private sealed record RequestWithoutTenant : IRequest<string>
    {
    }

    [Fact]
    public async Task Handle_WithTenantContext_CallsNext()
    {
        var behavior = new TenantScopeBehavior<RequestWithTenant, string>(
            NullLogger<TenantScopeBehavior<RequestWithTenant, string>>.Instance);
        var request  = new RequestWithTenant
        {
            TenantContext = new TenantContext(
                TenantId:      Guid.NewGuid(),
                BuId:          Guid.NewGuid(),
                UserId:        Guid.NewGuid(),
                Role:          "seller",
                CorrelationId: Guid.NewGuid())
        };

        var response = await behavior.Handle(request, _ => Task.FromResult("ok"), CancellationToken.None);

        response.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_WithoutTenantContext_ThrowsTenantContextMissingException()
    {
        var behavior = new TenantScopeBehavior<RequestWithTenant, string>(
            NullLogger<TenantScopeBehavior<RequestWithTenant, string>>.Instance);
        var request  = new RequestWithTenant { TenantContext = null };

        var act = async () => await behavior.Handle(
            request,
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        await act.Should().ThrowAsync<TenantContextMissingException>();
    }

    [Fact]
    public async Task Handle_RequestWithoutITenantRequest_PassesDirectly()
    {
        var behavior = new TenantScopeBehavior<RequestWithoutTenant, string>(
            NullLogger<TenantScopeBehavior<RequestWithoutTenant, string>>.Instance);
        var request  = new RequestWithoutTenant();

        var response = await behavior.Handle(request, _ => Task.FromResult("sem-tenant"), CancellationToken.None);

        response.Should().Be("sem-tenant");
    }
}
