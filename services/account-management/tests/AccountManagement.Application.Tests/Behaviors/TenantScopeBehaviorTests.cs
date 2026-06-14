using AccountManagement.Application.Behaviors;
using AccountManagement.Application.Accounts.Queries.GetAccountById;
using FluentAssertions;
using MediatR;

namespace AccountManagement.Application.Tests.Behaviors;

/// <summary>
/// Testes para <see cref="TenantScopeBehavior{TRequest,TResponse}"/>.
///
/// Mapeia: TASK-05 ST-01, design §5.4, Req 10, RNF 5, DD-002.
/// </summary>
public sealed class TenantScopeBehaviorTests
{
    [Fact(DisplayName = "TenantScopeBehavior: injeta TenantId no contexto e chama next")]
    public async Task Behavior_WithValidTenant_CallsNext()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);

        var behavior = new TenantScopeBehavior<GetAccountByIdQuery, object>(tenantContext);
        var request = new GetAccountByIdQuery(Guid.NewGuid());
        var nextCalled = false;

        // MediatR 12.x: RequestHandlerDelegate<T> = Func<CancellationToken, Task<T>>
        Task<object> Next(CancellationToken ct)
        {
            nextCalled = true;
            return Task.FromResult<object>(new object());
        }

        // Act
        await behavior.Handle(request, Next, CancellationToken.None);

        // Assert
        nextCalled.Should().BeTrue();
        tenantContext.TenantId.Should().Be(tenantId);
    }

    [Fact(DisplayName = "TenantScopeBehavior: sem tenant → lança InvalidOperationException")]
    public async Task Behavior_WithoutTenant_ThrowsInvalidOperationException()
    {
        // Arrange
        var tenantContext = new TenantContext(); // sem tenant configurado
        var behavior = new TenantScopeBehavior<GetAccountByIdQuery, object>(tenantContext);
        var request = new GetAccountByIdQuery(Guid.NewGuid());

        Task<object> Next(CancellationToken ct) => Task.FromResult<object>(new object());

        // Act
        var act = () => behavior.Handle(request, Next, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*tenant*");
    }
}
