using AuditLog.Application.Behaviors;
using AuditLog.Application.Commands;
using FluentAssertions;
using FluentValidation;
using MediatR;
using NSubstitute;
using Xunit;

namespace AuditLog.Application.Tests.Behaviors;

/// <summary>
/// Testes unitários do <see cref="ValidationBehavior{TRequest,TResponse}"/>.
/// Verifica que requisições inválidas são rejeitadas antes do handler.
/// </summary>
public sealed class ValidationBehaviorTests
{
    // -----------------------------------------------------------------------
    // Rejeita comando inválido (handler nunca é chamado)
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Deve lançar ValidationException para comando inválido")]
    public async Task Should_Throw_ValidationException_For_Invalid_Command()
    {
        var validators = new List<IValidator<RecordAuditEntryCommand>>
        {
            new RecordAuditEntryCommandValidator()
        };

        var sut = new ValidationBehavior<RecordAuditEntryCommand, Unit>(validators);

        var invalidCmd = new RecordAuditEntryCommand
        {
            ActorId = Guid.Empty, // inválido
            EntityType = "",      // inválido
            EntityId = Guid.NewGuid(),
            Action = Domain.ValueObjects.AuditAction.Create,
            RawAfter = new Dictionary<string, object?>(StringComparer.Ordinal) { ["x"] = 1 }
        };

        var handlerCalled = false;
        RequestHandlerDelegate<Unit> next = _ =>
        {
            handlerCalled = true;
            return Task.FromResult(Unit.Value);
        };

        var act = async () => await sut.Handle(invalidCmd, next, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        handlerCalled.Should().BeFalse(because: "handler não deve ser invocado quando validação falha");
    }

    [Fact(DisplayName = "Deve passar para o handler quando não há validadores")]
    public async Task Should_Call_Next_When_No_Validators()
    {
        var sut = new ValidationBehavior<RecordAuditEntryCommand, Unit>(
            Enumerable.Empty<IValidator<RecordAuditEntryCommand>>());

        var handlerCalled = false;
        RequestHandlerDelegate<Unit> next = _ =>
        {
            handlerCalled = true;
            return Task.FromResult(Unit.Value);
        };

        var cmd = ValidCommand();
        await sut.Handle(cmd, next, CancellationToken.None);

        handlerCalled.Should().BeTrue();
    }

    [Fact(DisplayName = "Deve passar para o handler quando comando é válido")]
    public async Task Should_Call_Next_For_Valid_Command()
    {
        var validators = new List<IValidator<RecordAuditEntryCommand>>
        {
            new RecordAuditEntryCommandValidator()
        };

        var sut = new ValidationBehavior<RecordAuditEntryCommand, Unit>(validators);

        var handlerCalled = false;
        RequestHandlerDelegate<Unit> next = _ =>
        {
            handlerCalled = true;
            return Task.FromResult(Unit.Value);
        };

        await sut.Handle(ValidCommand(), next, CancellationToken.None);

        handlerCalled.Should().BeTrue();
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
        RawAfter = new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Test" }
    };
}
