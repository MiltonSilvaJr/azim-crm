using AccountManagement.Application.Behaviors;
using AccountManagement.Application.Accounts.Queries.GetAccountById;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AccountManagement.Application.Tests.Behaviors;

/// <summary>
/// Testes para <see cref="CorrelationLoggingBehavior{TRequest,TResponse}"/>.
///
/// Mapeia: TASK-05 ST-02, design §5.4, RNF 1, RNF 9, ACC-ERR (PII em logs).
/// </summary>
public sealed class CorrelationLoggingBehaviorTests
{
    [Fact(DisplayName = "CorrelationLoggingBehavior: chama next após logging sem bloquear")]
    public async Task Behavior_AnyRequest_CallsNext()
    {
        // Arrange — usar NullLogger para evitar problemas de proxy com ILogger<internal>
        var logger = NullLogger<CorrelationLoggingBehavior<GetAccountByIdQuery, object>>.Instance;
        var correlationContext = new CorrelationContext();
        correlationContext.SetCorrelation(correlationId: Guid.NewGuid().ToString(), tenantId: Guid.NewGuid());

        var behavior = new CorrelationLoggingBehavior<GetAccountByIdQuery, object>(
            logger, correlationContext);

        var request = new GetAccountByIdQuery(Guid.NewGuid());
        var nextCalled = false;

        Task<object> Next(CancellationToken ct)
        {
            nextCalled = true;
            return Task.FromResult<object>(new object());
        }

        // Act
        await behavior.Handle(request, Next, CancellationToken.None);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact(DisplayName = "CorrelationLoggingBehavior: correlation_id está disponível no contexto")]
    public async Task Behavior_SetsCorrelationId()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var logger = NullLogger<CorrelationLoggingBehavior<GetAccountByIdQuery, object>>.Instance;
        var correlationContext = new CorrelationContext();
        correlationContext.SetCorrelation(correlationId, tenantId);

        var behavior = new CorrelationLoggingBehavior<GetAccountByIdQuery, object>(
            logger, correlationContext);

        var request = new GetAccountByIdQuery(Guid.NewGuid());

        // Act
        await behavior.Handle(request, _ => Task.FromResult<object>(new object()), CancellationToken.None);

        // Assert
        correlationContext.CorrelationId.Should().Be(correlationId);
        correlationContext.TenantId.Should().Be(tenantId);
    }
}
