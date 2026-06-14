using FluentAssertions;
using MediatR;
using NSubstitute;
using OpportunityPipeline.Application.Behaviors;
using OpportunityPipeline.Application.Common;
using Xunit;

namespace OpportunityPipeline.Application.Tests.Behaviors;

/// <summary>
/// Testes do RbacBehavior.
/// Cobre: ST-01 (Vendedor em reabertura → ForbiddenException; TenantAdmin passa; Viewer em escrita → ForbiddenException).
/// Mapeia: RNF 4, design §5.4, design §10, TASK-08.
/// </summary>
public sealed class RbacBehaviorTests
{
    private readonly RbacBehavior<FakeCommand, string> _sut = new();

    // Comando com atributo RequiresRole — apenas GestorBU e TenantAdmin
    [RequiresRole(UserRole.GestorBU, UserRole.TenantAdmin)]
    private sealed class ReopenCommand : IAuthenticatedCommand
    {
        public UserRole UserRole { get; init; }
        public string CorrelationId { get; init; } = "test-correlation";
    }

    // Comando de escrita — Vendedor, GestorBU, TenantAdmin
    [RequiresRole(UserRole.Vendedor, UserRole.GestorBU, UserRole.TenantAdmin)]
    private sealed class WriteCommand : IAuthenticatedCommand
    {
        public UserRole UserRole { get; init; }
        public string CorrelationId { get; init; } = "test-correlation";
    }

    // Fakes para o teste de RbacBehavior<TRequest, TResponse>
    [RequiresRole(UserRole.GestorBU, UserRole.TenantAdmin)]
    private sealed class FakeCommand : IAuthenticatedCommand
    {
        public required UserRole UserRole { get; init; }
        public string CorrelationId { get; init; } = "fake-correlation";
    }

    private static RequestHandlerDelegate<string> OkDelegate()
        => _ => Task.FromResult("ok");

    [Fact]
    public async Task Vendedor_em_operacao_de_reabertura_deve_receber_ForbiddenException()
    {
        // Arrange
        var command = new FakeCommand { UserRole = UserRole.Vendedor };

        // Act
        var act = () => _sut.Handle(command, OkDelegate(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .Where(ex => ex.UserRole == UserRole.Vendedor);
    }

    [Fact]
    public async Task TenantAdmin_em_operacao_de_reabertura_deve_passar()
    {
        // Arrange
        var command = new FakeCommand { UserRole = UserRole.TenantAdmin };

        // Act
        var result = await _sut.Handle(command, OkDelegate(), CancellationToken.None);

        // Assert
        result.Should().Be("ok");
    }

    [Fact]
    public async Task GestorBU_em_operacao_de_reabertura_deve_passar()
    {
        // Arrange
        var command = new FakeCommand { UserRole = UserRole.GestorBU };

        // Act
        var result = await _sut.Handle(command, OkDelegate(), CancellationToken.None);

        // Assert
        result.Should().Be("ok");
    }

    [Fact]
    public async Task Viewer_em_operacao_de_escrita_deve_receber_ForbiddenException()
    {
        // Arrange — usar behavior com WriteCommand (Viewer não está na lista)
        var behavior = new RbacBehavior<ViewerWriteCommand, string>();
        var command = new ViewerWriteCommand { UserRole = UserRole.Viewer };

        // Act
        var act = () => behavior.Handle(command, OkDelegate(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .Where(ex => ex.UserRole == UserRole.Viewer);
    }

    [Fact]
    public async Task Command_sem_atributo_RequiresRole_deve_passar_sem_verificacao()
    {
        // Arrange
        var behavior = new RbacBehavior<NoRoleCommand, string>();
        var command = new NoRoleCommand { UserRole = UserRole.Viewer };

        // Act
        var result = await behavior.Handle(command, OkDelegate(), CancellationToken.None);

        // Assert
        result.Should().Be("ok");
    }

    [RequiresRole(UserRole.Vendedor, UserRole.GestorBU, UserRole.TenantAdmin)]
    private sealed class ViewerWriteCommand : IAuthenticatedCommand
    {
        public required UserRole UserRole { get; init; }
        public string CorrelationId { get; init; } = "viewer-test";
    }

    // Sem atributo RequiresRole
    private sealed class NoRoleCommand : IAuthenticatedCommand
    {
        public required UserRole UserRole { get; init; }
        public string CorrelationId { get; init; } = "no-role-test";
    }
}
