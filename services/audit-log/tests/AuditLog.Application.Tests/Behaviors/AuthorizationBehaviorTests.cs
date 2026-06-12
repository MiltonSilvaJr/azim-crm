using AuditLog.Application.Abstractions;
using AuditLog.Application.Behaviors;
using AuditLog.Application.Commands;
using AuditLog.Application.Errors;
using AuditLog.Application.Queries;
using AuditLog.Application.Results;
using AuditLog.Domain.Aggregates;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace AuditLog.Application.Tests.Behaviors;

/// <summary>
/// Testes unitários do <see cref="AuthorizationBehavior{TRequest,TResponse}"/>.
/// Verifica que papéis sem permissão são bloqueados (AUD-ERR-002)
/// e que RecordAuditEntryCommand nunca passa por autorização de consulta.
/// </summary>
public sealed class AuthorizationBehaviorTests
{
    private readonly IUserContext _userContext = Substitute.For<IUserContext>();

    private AuthorizationBehavior<ListAuditLogsQuery, PagedResult<AuditLogAggregate>> BuildQuerySut() =>
        new(_userContext,
            NullLogger<AuthorizationBehavior<ListAuditLogsQuery, PagedResult<AuditLogAggregate>>>.Instance);

    private AuthorizationBehavior<RecordAuditEntryCommand, Unit> BuildCommandSut() =>
        new(_userContext,
            NullLogger<AuthorizationBehavior<RecordAuditEntryCommand, Unit>>.Instance);

    // -----------------------------------------------------------------------
    // Papéis com permissão (TenantAdmin, GestorBU)
    // -----------------------------------------------------------------------

    [Theory(DisplayName = "TenantAdmin e GestorBU devem ter acesso às queries")]
    [InlineData(AuditRoles.TenantAdmin)]
    [InlineData(AuditRoles.GestorBU)]
    public async Task Authorized_Roles_Should_Call_Next(string role)
    {
        _userContext.Role.Returns(role);
        var sut = BuildQuerySut();

        var nextCalled = false;
        await sut.Handle(
            new ListAuditLogsQuery(),
            _ =>
            {
                nextCalled = true;
                return Task.FromResult(new PagedResult<AuditLogAggregate>(
                    Array.Empty<AuditLogAggregate>(), 1, 50, 0));
            },
            CancellationToken.None);

        nextCalled.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // Papéis sem permissão → AUD-ERR-002
    // -----------------------------------------------------------------------

    [Theory(DisplayName = "Papéis sem permissão devem receber AUD-ERR-002")]
    [InlineData("Vendedor")]
    [InlineData("Viewer")]
    [InlineData("PlatformOperator")]
    [InlineData("")]
    public async Task Unauthorized_Roles_Should_Throw_AccessDenied(string role)
    {
        _userContext.Role.Returns(role.Length > 0 ? role : null!);
        var sut = BuildQuerySut();

        var act = async () => await sut.Handle(
            new ListAuditLogsQuery(),
            _ => Task.FromResult(new PagedResult<AuditLogAggregate>(
                Array.Empty<AuditLogAggregate>(), 1, 50, 0)),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<AuditAuthorizationException>();
        ex.Which.ErrorCode.Should().Be(AuditErrorCodes.AccessDenied,
            because: "papel sem permissão deve gerar AUD-ERR-002");
    }

    [Fact(DisplayName = "Papel nulo deve receber AUD-ERR-002")]
    public async Task Null_Role_Should_Throw_AccessDenied()
    {
        _userContext.Role.Returns((string?)null);
        var sut = BuildQuerySut();

        var act = async () => await sut.Handle(
            new ListAuditLogsQuery(),
            _ => Task.FromResult(new PagedResult<AuditLogAggregate>(
                Array.Empty<AuditLogAggregate>(), 1, 50, 0)),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<AuditAuthorizationException>();
        ex.Which.ErrorCode.Should().Be(AuditErrorCodes.AccessDenied);
    }

    // -----------------------------------------------------------------------
    // RecordAuditEntryCommand NÃO passa pela autorização (design §5.4)
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "RecordAuditEntryCommand deve passar sem verificação de papel")]
    public async Task RecordAuditEntryCommand_Should_Not_Require_Authorization()
    {
        // Papel Vendedor (sem acesso a queries) — mas command deve passar
        _userContext.Role.Returns("Vendedor");
        var sut = BuildCommandSut();

        var nextCalled = false;
        await sut.Handle(
            ValidCommand(),
            _ =>
            {
                nextCalled = true;
                return Task.FromResult(Unit.Value);
            },
            CancellationToken.None);

        nextCalled.Should().BeTrue(
            because: "RecordAuditEntryCommand é porta interna confiável e não passa por AuthorizationBehavior de consulta");
    }

    [Fact(DisplayName = "RecordAuditEntryCommand com papel nulo deve passar sem erro")]
    public async Task RecordAuditEntryCommand_With_Null_Role_Should_Pass()
    {
        _userContext.Role.Returns((string?)null);
        var sut = BuildCommandSut();

        var nextCalled = false;
        await sut.Handle(
            ValidCommand(),
            _ =>
            {
                nextCalled = true;
                return Task.FromResult(Unit.Value);
            },
            CancellationToken.None);

        nextCalled.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static RecordAuditEntryCommand ValidCommand() => new()
    {
        ActorId = Guid.NewGuid(),
        EntityType = "Opportunity",
        EntityId = Guid.NewGuid(),
        Action = Domain.ValueObjects.AuditAction.Create,
        RawAfter = new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "T" }
    };
}
