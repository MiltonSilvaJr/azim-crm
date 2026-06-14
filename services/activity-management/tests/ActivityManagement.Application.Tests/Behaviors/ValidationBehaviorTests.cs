namespace ActivityManagement.Application.Tests.Behaviors;

using ActivityManagement.Application.Behaviors;
using FluentValidation;
using MediatR;

/// <summary>
/// Testes unitários do <see cref="ValidationBehavior{TRequest,TResponse}"/>.
/// Verifica: rejeita request inválido sem atingir handler; passa request válido; sem validator passa diretamente.
/// Mapeia: design §5.4/§5.5, TASK-06.
/// </summary>
public sealed class ValidationBehaviorTests
{
    private sealed record TestRequest(string Title) : IRequest<string>;

    private sealed class TestRequestValidator : AbstractValidator<TestRequest>
    {
        public TestRequestValidator()
        {
            RuleFor(r => r.Title).NotEmpty().WithMessage("Título é obrigatório.");
        }
    }

    [Fact]
    public async Task Handle_ValidRequest_CallsNext()
    {
        var validators = new List<IValidator<TestRequest>> { new TestRequestValidator() };
        var behavior   = new ValidationBehavior<TestRequest, string>(validators);
        var request    = new TestRequest("Reunião");

        var response = await behavior.Handle(request, _ => Task.FromResult("ok"), CancellationToken.None);

        response.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_InvalidRequest_ThrowsValidationException()
    {
        var validators = new List<IValidator<TestRequest>> { new TestRequestValidator() };
        var behavior   = new ValidationBehavior<TestRequest, string>(validators);
        var request    = new TestRequest(Title: "");

        var act = async () => await behavior.Handle(
            request,
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ActivityManagement.Application.Behaviors.ValidationException>()
            .Where(ex => ex.Errors.ContainsKey("Title"));
    }

    [Fact]
    public async Task Handle_NoValidators_PassesDirectly()
    {
        var behavior = new ValidationBehavior<TestRequest, string>(Enumerable.Empty<IValidator<TestRequest>>());
        var request  = new TestRequest(Title: "");

        // Sem validators, mesmo request "inválido" passa
        var response = await behavior.Handle(request, _ => Task.FromResult("ok"), CancellationToken.None);

        response.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_InvalidRequest_HandlerIsNotCalled()
    {
        var validators     = new List<IValidator<TestRequest>> { new TestRequestValidator() };
        var behavior       = new ValidationBehavior<TestRequest, string>(validators);
        var request        = new TestRequest(Title: "");
        var handlerCalled  = false;

        var act = async () => await behavior.Handle(
            request,
            _ =>
            {
                handlerCalled = true;
                return Task.FromResult("ok");
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<ActivityManagement.Application.Behaviors.ValidationException>();
        handlerCalled.Should().BeFalse("handler não deve ser atingido em caso de validação inválida");
    }
}
