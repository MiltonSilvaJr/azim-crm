using FluentAssertions;
using FluentValidation;
using MediatR;
using TenantAdministration.Application.Behaviors;
using TenantAdministration.Application.Exceptions;
using Xunit;
using ValidationException = TenantAdministration.Application.Exceptions.ValidationException;

namespace TenantAdministration.Application.Tests.Behaviors;

public sealed class ValidationBehaviorTests
{
    private readonly RequestHandlerDelegate<string> _next = ct => Task.FromResult("ok");

    [Fact(DisplayName = "Sem validators: handler é chamado normalmente")]
    public async Task NoValidators_ShouldCallNext()
    {
        var behavior = new ValidationBehavior<SampleCommand, string>([]);
        var result = await behavior.Handle(new SampleCommand("valid"), _next, CancellationToken.None);
        result.Should().Be("ok");
    }

    [Fact(DisplayName = "Command válido: handler é chamado")]
    public async Task ValidCommand_ShouldCallNext()
    {
        var behavior = new ValidationBehavior<SampleCommand, string>([new SampleCommandValidator()]);
        var result = await behavior.Handle(new SampleCommand("valid"), _next, CancellationToken.None);
        result.Should().Be("ok");
    }

    [Fact(DisplayName = "Command inválido: ValidationException é lançada antes de chamar o handler")]
    public async Task InvalidCommand_ShouldThrowValidationException_BeforeHandler()
    {
        var handlerCalled = false;
        RequestHandlerDelegate<string> next = ct =>
        {
            handlerCalled = true;
            return Task.FromResult("ok");
        };

        var behavior = new ValidationBehavior<SampleCommand, string>([new SampleCommandValidator()]);
        var act = () => behavior.Handle(new SampleCommand(""), next, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        handlerCalled.Should().BeFalse("o handler não deve ser chamado se a validação falhar");
    }

    [Fact(DisplayName = "ValidationException contém erros agrupados por campo")]
    public async Task InvalidCommand_ErrorsGroupedByField()
    {
        var behavior = new ValidationBehavior<SampleCommand, string>([new SampleCommandValidator()]);
        var ex = await Record.ExceptionAsync(
            () => behavior.Handle(new SampleCommand(""), _next, CancellationToken.None));

        ex.Should().BeOfType<ValidationException>();
        var ve = (ValidationException)ex!;
        ve.Errors.Should().ContainKey("Name");
    }

    // ──────────────────────────────────────────────────────────────
    // Stubs
    // ──────────────────────────────────────────────────────────────

    private sealed record SampleCommand(string Name) : IRequest<string>;

    private sealed class SampleCommandValidator : AbstractValidator<SampleCommand>
    {
        public SampleCommandValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Name é obrigatório.");
        }
    }
}
