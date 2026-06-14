namespace ActivityManagement.Application.Tests.Behaviors;

using ActivityManagement.Application.Behaviors;
using ActivityManagement.Application.Common;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Testes unitários do <see cref="AuthorizationBehavior{TRequest,TResponse}"/>.
/// Verifica: viewer rejeita Command de escrita (ACT-ERR-007); seller e bu_manager passam;
/// Query sem marcador IRequireWriteRole passa independente de papel.
/// Mapeia: Req 13, design §5.4, ACT-ERR-007, TASK-06.
/// </summary>
public sealed class AuthorizationBehaviorTests
{
    private sealed record WriteCommand : IRequest<string>, ITenantRequest, IRequireWriteRole
    {
        public TenantContext? TenantContext { get; set; }
    }

    private sealed record ReadQuery : IRequest<string>, ITenantRequest
    {
        public TenantContext? TenantContext { get; set; }
    }

    private static TenantContext ContextForRole(string role) => new(
        TenantId:      Guid.NewGuid(),
        BuId:          Guid.NewGuid(),
        UserId:        Guid.NewGuid(),
        Role:          role,
        CorrelationId: Guid.NewGuid());

    [Theory]
    [InlineData("seller")]
    [InlineData("bu_manager")]
    public async Task Handle_WriteCommand_WithWriteRole_CallsNext(string role)
    {
        var behavior = new AuthorizationBehavior<WriteCommand, string>(
            NullLogger<AuthorizationBehavior<WriteCommand, string>>.Instance);
        var request  = new WriteCommand { TenantContext = ContextForRole(role) };

        var response = await behavior.Handle(request, _ => Task.FromResult("ok"), CancellationToken.None);

        response.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_WriteCommand_WithViewerRole_ThrowsInsufficientScopeException()
    {
        var behavior = new AuthorizationBehavior<WriteCommand, string>(
            NullLogger<AuthorizationBehavior<WriteCommand, string>>.Instance);
        var request  = new WriteCommand { TenantContext = ContextForRole("viewer") };

        var act = async () => await behavior.Handle(
            request,
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InsufficientScopeException>()
            .Where(ex => ex.UserRole == "viewer");
    }

    [Fact]
    public async Task Handle_ReadQuery_WithViewerRole_PassesDirectly()
    {
        var behavior = new AuthorizationBehavior<ReadQuery, string>(
            NullLogger<AuthorizationBehavior<ReadQuery, string>>.Instance);
        var request  = new ReadQuery { TenantContext = ContextForRole("viewer") };

        var response = await behavior.Handle(request, _ => Task.FromResult("leitura"), CancellationToken.None);

        response.Should().Be("leitura");
    }

    [Fact]
    public async Task Handle_WriteCommand_WithoutTenantContext_CallsNext()
    {
        var behavior = new AuthorizationBehavior<WriteCommand, string>(
            NullLogger<AuthorizationBehavior<WriteCommand, string>>.Instance);
        var request  = new WriteCommand { TenantContext = null };

        var response = await behavior.Handle(request, _ => Task.FromResult("ok"), CancellationToken.None);

        response.Should().Be("ok");
    }
}
