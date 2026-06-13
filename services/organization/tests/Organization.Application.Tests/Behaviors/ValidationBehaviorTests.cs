using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Organization.Application.Behaviors;
using Xunit;

namespace Organization.Application.Tests.Behaviors;

/// <summary>
/// Testes unitários para <see cref="ValidationBehavior{TRequest,TResponse}"/>.
/// </summary>
public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_WithNoValidators_CallsNext()
    {
        // Arrange
        var behavior = new ValidationBehavior<FakeRequest, string>(
            [],
            NullLogger<ValidationBehavior<FakeRequest, string>>.Instance);

        var nextCalled = false;

        // Act
        var result = await behavior.Handle(
            new FakeRequest("valor"),
            _ => { nextCalled = true; return Task.FromResult("ok"); },
            CancellationToken.None);

        // Assert
        nextCalled.Should().BeTrue();
        result.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_WithValidRequest_CallsNext()
    {
        // Arrange — validador que sempre aprova
        var behavior = new ValidationBehavior<FakeRequest, string>(
            [new AlwaysValidValidator()],
            NullLogger<ValidationBehavior<FakeRequest, string>>.Instance);

        // Act
        var result = await behavior.Handle(
            new FakeRequest("valor"),
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        // Assert
        result.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_WithInvalidRequest_ThrowsValidationException()
    {
        // Arrange — validador que sempre rejeita
        var behavior = new ValidationBehavior<FakeRequest, string>(
            [new AlwaysInvalidValidator()],
            NullLogger<ValidationBehavior<FakeRequest, string>>.Instance);

        var nextCalled = false;

        // Act
        var act = () => behavior.Handle(
            new FakeRequest(""),
            _ => { nextCalled = true; return Task.FromResult("ok"); },
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithMultipleValidationErrors_ThrowsWithAllErrors()
    {
        // Arrange
        var behavior = new ValidationBehavior<FakeRequest, string>(
            [new TwoErrorsValidator()],
            NullLogger<ValidationBehavior<FakeRequest, string>>.Instance);

        // Act
        var act = () => behavior.Handle(
            new FakeRequest(""),
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WithMultipleValidators_AllMustPass()
    {
        // Arrange — um válido + um inválido: deve rejeitar
        var behavior = new ValidationBehavior<FakeRequest, string>(
            [new AlwaysValidValidator(), new AlwaysInvalidValidator()],
            NullLogger<ValidationBehavior<FakeRequest, string>>.Instance);

        // Act
        var act = () => behavior.Handle(
            new FakeRequest("valor"),
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    // ── Stubs ──

    private sealed record FakeRequest(string Valor);

    private sealed class AlwaysValidValidator : AbstractValidator<FakeRequest> { }

    private sealed class AlwaysInvalidValidator : AbstractValidator<FakeRequest>
    {
        public AlwaysInvalidValidator()
        {
            RuleFor(x => x.Valor).Must(_ => false).WithMessage("Campo é inválido");
        }
    }

    private sealed class TwoErrorsValidator : AbstractValidator<FakeRequest>
    {
        public TwoErrorsValidator()
        {
            RuleFor(x => x.Valor).Must(_ => false).WithMessage("Erro 1");
            RuleFor(x => x.Valor).Must(_ => false).WithMessage("Erro 2");
        }
    }
}
