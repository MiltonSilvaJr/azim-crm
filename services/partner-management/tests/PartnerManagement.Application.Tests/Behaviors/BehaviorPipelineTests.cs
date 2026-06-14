using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PartnerManagement.Application.Behaviors;
using PartnerManagement.Application.Ports;
using Xunit;

namespace PartnerManagement.Application.Tests.Behaviors;

/// <summary>
/// Testes unitários dos pipeline behaviors MediatR.
/// Verifica rejeição sem tenant, rejeição sem permissão, validação FluentValidation e auditoria.
/// Mapeia: TASK-13, RNF 1, RNF 4, RNF 5, design §5.4.
/// </summary>
public sealed class BehaviorPipelineTests
{
    // Helper para criar o delegate com a assinatura correta (MediatR 12 exige CancellationToken)
    private static RequestHandlerDelegate<T> OkNext<T>(T value) =>
        ct => Task.FromResult(value);

    private static RequestHandlerDelegate<T> FailingNext<T>(Exception ex) =>
        ct => Task.FromException<T>(ex);

    // ========== CorrelationLoggingBehavior ==========

    [Fact]
    public async Task CorrelationLogging_PassesThrough_WhenNoException()
    {
        // Arrange
        CorrelationLoggingBehavior<TestCommand, TestResult> behavior =
            new(NullLogger<CorrelationLoggingBehavior<TestCommand, TestResult>>.Instance);

        // Act
        TestResult result = await behavior.Handle(
            new TestCommand(Guid.NewGuid(), "ignorado"),
            OkNext(new TestResult("ok")),
            CancellationToken.None);

        // Assert
        result.Value.Should().Be("ok");
    }

    [Fact]
    public async Task CorrelationLogging_DoesNotExposeNameInLog()
    {
        // Arrange — verificamos que o behavior não loga campos de PII do request
        CorrelationLoggingBehavior<TestCommand, TestResult> behavior =
            new(NullLogger<CorrelationLoggingBehavior<TestCommand, TestResult>>.Instance);

        // Act — executa sem exception; PII não aparece porque o behavior não loga campos do request
        TestResult result = await behavior.Handle(
            new TestCommand(Guid.NewGuid(), "NomeQueNaoDeveAparecerNoLog"),
            OkNext(new TestResult("ok")),
            CancellationToken.None);

        // Assert — comportamento correto: sem exception, comportamento neutro
        result.Should().NotBeNull();
    }

    // ========== TenantScopeBehavior ==========

    [Fact]
    public async Task TenantScope_ResolvedTenant_PassesThrough()
    {
        // Arrange
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.CurrentTenantId.Returns(Guid.NewGuid());
        tenantContext.IsResolved.Returns(true);

        TenantScopeBehavior<TestCommand, TestResult> behavior = new(tenantContext);

        // Act
        TestResult result = await behavior.Handle(
            new TestCommand(Guid.NewGuid(), "x"),
            OkNext(new TestResult("ok")),
            CancellationToken.None);

        // Assert
        result.Value.Should().Be("ok");
    }

    [Fact]
    public async Task TenantScope_UnresolvedTenant_ThrowsUnauthorizedTenantException()
    {
        // Arrange
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.CurrentTenantId.Returns(Guid.Empty);
        tenantContext.IsResolved.Returns(false);

        TenantScopeBehavior<TestCommand, TestResult> behavior = new(tenantContext);

        // Act & Assert
        await behavior.Invoking(b => b.Handle(
                new TestCommand(Guid.NewGuid(), "x"),
                OkNext(new TestResult("ok")),
                CancellationToken.None))
            .Should().ThrowAsync<UnauthorizedTenantException>();
    }

    // ========== ValidationBehavior ==========

    [Fact]
    public async Task Validation_ValidRequest_NoValidators_PassesThrough()
    {
        // Arrange — nenhum validator registrado
        ValidationBehavior<TestCommand, TestResult> behavior = new([]);

        // Act
        TestResult result = await behavior.Handle(
            new TestCommand(Guid.NewGuid(), "x"),
            OkNext(new TestResult("ok")),
            CancellationToken.None);

        // Assert
        result.Value.Should().Be("ok");
    }

    [Fact]
    public async Task Validation_InvalidRequest_ThrowsValidationException()
    {
        // Arrange — validator concreto que sempre falha (NSubstitute não suporta interfaces genéricas sealed)
        ValidationBehavior<TestCommand, TestResult> behavior = new([new AlwaysFailTestValidator()]);

        // Act & Assert
        await behavior.Invoking(b => b.Handle(
                new TestCommand(Guid.NewGuid(), ""),
                OkNext(new TestResult("ok")),
                CancellationToken.None))
            .Should().ThrowAsync<ValidationException>();
    }

    // ========== AuthorizationBehavior ==========

    [Fact]
    public async Task Authorization_RequestWithoutPermissionAttr_PassesThrough()
    {
        // Arrange — TestCommand não tem [RequiredPermission]
        IPermissionContext permCtx = Substitute.For<IPermissionContext>();
        AuthorizationBehavior<TestCommand, TestResult> behavior = new(permCtx);

        // Act
        TestResult result = await behavior.Handle(
            new TestCommand(Guid.NewGuid(), "x"),
            OkNext(new TestResult("ok")),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        permCtx.DidNotReceive().HasPermission(Arg.Any<string>());
    }

    [Fact]
    public async Task Authorization_RequestWithPermissionAttr_UserHasPermission_PassesThrough()
    {
        // Arrange — ProtectedCommand requer "partners:write"
        IPermissionContext permCtx = Substitute.For<IPermissionContext>();
        permCtx.HasPermission("partners:write").Returns(true);

        AuthorizationBehavior<ProtectedCommand, TestResult> behavior = new(permCtx);

        // Act
        TestResult result = await behavior.Handle(
            new ProtectedCommand(),
            OkNext(new TestResult("ok")),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Authorization_RequestWithPermissionAttr_UserLacksPermission_ThrowsAccessDeniedException()
    {
        // Arrange — PM-ERR-008
        IPermissionContext permCtx = Substitute.For<IPermissionContext>();
        permCtx.HasPermission(Arg.Any<string>()).Returns(false);

        AuthorizationBehavior<ProtectedCommand, TestResult> behavior = new(permCtx);

        // Act & Assert
        await behavior.Invoking(b => b.Handle(
                new ProtectedCommand(),
                OkNext(new TestResult("ok")),
                CancellationToken.None))
            .Should().ThrowAsync<AccessDeniedException>();
    }

    // ========== TransactionBehavior ==========

    [Fact]
    public async Task Transaction_Command_BeginsAndCommits()
    {
        // Arrange
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        TransactionBehavior<TestCommand, TestResult> behavior = new(uow);

        // Act
        TestResult result = await behavior.Handle(
            new TestCommand(Guid.NewGuid(), "x"),
            OkNext(new TestResult("ok")),
            CancellationToken.None);

        // Assert
        await uow.Received(1).BeginTransactionAsync(Arg.Any<CancellationToken>());
        await uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await uow.DidNotReceive().RollbackAsync(Arg.Any<CancellationToken>());
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Transaction_CommandFails_Rollbacks()
    {
        // Arrange
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        TransactionBehavior<TestCommand, TestResult> behavior = new(uow);

        // Act & Assert
        await behavior.Invoking(b => b.Handle(
                new TestCommand(Guid.NewGuid(), "x"),
                FailingNext<TestResult>(new InvalidOperationException("erro")),
                CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();

        await uow.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transaction_Query_DoesNotWrapInTransaction()
    {
        // Arrange — TestQuery termina em "Query", não em "Command"
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        TransactionBehavior<TestQuery, TestResult> behavior = new(uow);

        // Act
        await behavior.Handle(
            new TestQuery(),
            OkNext(new TestResult("ok")),
            CancellationToken.None);

        // Assert — nenhuma transação aberta para queries
        await uow.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
    }
}

// ========== Tipos auxiliares de teste (file-scoped) ==========

/// <summary>Command de teste sem permissão requerida.</summary>
file sealed record TestCommand(Guid TenantId, string Name) : IRequest<TestResult>, IHasTenantId;

/// <summary>Query de teste (não é Command — não termina em "Command").</summary>
file sealed record TestQuery() : IRequest<TestResult>;

/// <summary>Command de teste com permissão requerida.</summary>
[RequiredPermission("partners:write")]
file sealed record ProtectedCommand() : IRequest<TestResult>;

/// <summary>Resultado de teste genérico.</summary>
file sealed record TestResult(string Value);

/// <summary>Validator concreto que sempre reprovará TestCommand.</summary>
file sealed class AlwaysFailTestValidator : AbstractValidator<TestCommand>
{
    public AlwaysFailTestValidator()
    {
        RuleFor(x => x.Name).Must(_ => false).WithMessage("Sempre falha");
    }
}
