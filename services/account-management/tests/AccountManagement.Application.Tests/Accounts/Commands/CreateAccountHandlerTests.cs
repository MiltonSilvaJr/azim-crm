using AccountManagement.Application.Accounts.Commands.CreateAccount;
using AccountManagement.Domain.Accounts;
using AccountManagement.Domain.Accounts.Events;
using AccountManagement.Domain.Accounts.Repositories;
using AccountManagement.Domain.Accounts.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace AccountManagement.Application.Tests.Accounts.Commands;

/// <summary>
/// Testes unitários para <see cref="CreateAccountHandler"/>.
///
/// Mapeia: TASK-04 ST-01, design §5.1, Req 1, DD-006.
/// </summary>
public sealed class CreateAccountHandlerTests
{
    private readonly IAccountRepository _repository;
    private readonly IEventPublisher _publisher;
    private readonly CreateAccountHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();

    public CreateAccountHandlerTests()
    {
        _repository = Substitute.For<IAccountRepository>();
        _publisher = Substitute.For<IEventPublisher>();
        _handler = new CreateAccountHandler(_repository, _publisher);
    }

    [Fact(DisplayName = "CreateAccount: conta criada com sucesso — repositório salva e evento é publicado")]
    public async Task Handle_ValidCommand_SavesAccountAndPublishesEvent()
    {
        // Arrange
        var command = new CreateAccountCommand(
            TenantId: _tenantId,
            Name: "Pag.ai Tecnologia",
            Website: "https://pag.ai",
            Notes: null,
            ConfirmCreateDespiteSimilar: false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        await _repository.Received(1).SaveAsync(
            Arg.Is<Account>(a => a.TenantId == _tenantId && a.Name.Value == "Pag.ai Tecnologia"),
            Arg.Any<CancellationToken>());
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AccountCreated>(e => e.TenantId == _tenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "CreateAccount: com similar existente e confirmCreateDespiteSimilar=true — conta é criada")]
    public async Task Handle_WithSimilarAndConfirmFlag_CreatesAccount()
    {
        // Arrange — não precisa simular similar; a lógica de similaridade é responsabilidade da UI
        var command = new CreateAccountCommand(
            TenantId: _tenantId,
            Name: "Pag AI",
            Website: null,
            Notes: null,
            ConfirmCreateDespiteSimilar: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert — a criação ocorre independentemente da flag (DD-006: POST nunca bloqueia)
        result.Should().NotBeEmpty();
        await _repository.Received(1).SaveAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "CreateAccount: NormalizedName é calculado corretamente no domínio")]
    public async Task Handle_ValidCommand_NormalizedNameIsComputed()
    {
        // Arrange
        Account? savedAccount = null;
        await _repository.SaveAsync(
            Arg.Do<Account>(a => savedAccount = a),
            Arg.Any<CancellationToken>());

        var command = new CreateAccountCommand(
            TenantId: _tenantId,
            Name: "Pág.AI Tecnologia",
            Website: null,
            Notes: null,
            ConfirmCreateDespiteSimilar: false);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        // Algoritmo: NFD sem acento → minúsculas → remove não-alfanumérico (. é removido sem espaço)
        // "Pág.AI Tecnologia" → "pag.ai tecnologia" → "pagai tecnologia"
        savedAccount.Should().NotBeNull();
        savedAccount!.NormalizedName.Value.Should().Be("pagai tecnologia");
    }

    [Fact(DisplayName = "CreateAccount: AccountCreated event NormalizedName sem PII")]
    public async Task Handle_ValidCommand_EventContainsNoPii()
    {
        // Arrange
        AccountCreated? publishedEvent = null;
        await _publisher.PublishAsync(
            Arg.Do<AccountCreated>(e => publishedEvent = e),
            Arg.Any<CancellationToken>());

        var command = new CreateAccountCommand(
            TenantId: _tenantId,
            Name: "Empresa Teste",
            Website: null,
            Notes: null,
            ConfirmCreateDespiteSimilar: false);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert — evento carrega forma normalizada, não o nome original
        publishedEvent.Should().NotBeNull();
        publishedEvent!.NormalizedName.Should().Be("empresa teste");
        publishedEvent.TenantId.Should().Be(_tenantId);
    }
}
