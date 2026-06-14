using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PartnerManagement.Application.Partners;
using PartnerManagement.Application.Partners.Commands;
using PartnerManagement.Application.Ports;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Domain.Partners.Exceptions;
using PartnerManagement.Domain.Partners.Repositories;
using PartnerManagement.Domain.Partners.ValueObjects;
using Xunit;

namespace PartnerManagement.Application.Tests.Partners.Commands;

/// <summary>
/// Testes unitários do <see cref="CreatePartnerHandler"/>.
/// Mapeia: TASK-09, Req 1, Req 7, Req 11, design §5.1.
/// </summary>
public sealed class CreatePartnerHandlerTests
{
    private readonly IPartnerRepository _repository = Substitute.For<IPartnerRepository>();
    private readonly ICanonicalRoleProvider _roleProvider = Substitute.For<ICanonicalRoleProvider>();
    private readonly IPartnerMetrics _metrics = Substitute.For<IPartnerMetrics>();

    private CreatePartnerHandler CreateSut() =>
        new(_repository, _roleProvider, _metrics, NullLogger<CreatePartnerHandler>.Instance);

    private CreatePartnerCommand ValidCommand(
        string name = "Acme Ltda",
        string role = "Indicador",
        decimal pctSetup = 10.00m,
        decimal pctRecorrente = 5.00m,
        string? email = null,
        string? phone = null,
        bool confirm = false) =>
        new(
            TenantId: Guid.NewGuid(),
            Name: name,
            Role: role,
            CommissionDefaults: CommissionDefaults.Create(
                Percentage.Create(pctSetup),
                Percentage.Create(pctRecorrente)),
            ContactEmail: email,
            ContactPhone: phone,
            Notes: null,
            CreatedBy: Guid.NewGuid(),
            ConfirmCreateDespiteDuplicate: confirm);

    public CreatePartnerHandlerTests()
    {
        // Por padrão, papel é canônico e sem duplicatas
        _roleProvider.IsCanonical(Arg.Any<string>(), Arg.Any<Guid>()).Returns(true);
        _repository.FindByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Partner>());
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesPartnerAndReturnsId()
    {
        // Arrange
        CreatePartnerHandler sut = CreateSut();
        CreatePartnerCommand command = ValidCommand();

        // Act
        CreatePartnerResult result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.PartnerId.Should().NotBeEmpty();
        result.DuplicateNameAlert.Should().BeFalse();
        await _repository.Received(1).AddAsync(Arg.Any<Partner>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidCommand_PartnerHasActiveStatus()
    {
        // Arrange
        Partner? capturedPartner = null;
        await _repository.AddAsync(Arg.Do<Partner>(p => capturedPartner = p), Arg.Any<CancellationToken>());
        CreatePartnerHandler sut = CreateSut();

        // Act
        await sut.Handle(ValidCommand(), CancellationToken.None);

        // Assert
        capturedPartner.Should().NotBeNull();
        capturedPartner!.Status.Should().Be(PartnerStatus.Active);
    }

    [Fact]
    public async Task Handle_ValidCommand_PartnerAccumulatesCreatedEvent()
    {
        // Arrange
        Partner? capturedPartner = null;
        await _repository.AddAsync(Arg.Do<Partner>(p => capturedPartner = p), Arg.Any<CancellationToken>());
        CreatePartnerHandler sut = CreateSut();

        // Act
        await sut.Handle(ValidCommand(), CancellationToken.None);

        // Assert
        capturedPartner!.DomainEvents.Should().HaveCount(1);
        capturedPartner.DomainEvents[0].GetType().Name.Should().Be("PartnerCreated");
    }

    [Fact]
    public async Task Handle_DuplicateName_ReturnsDuplicateAlertWithoutBlocking()
    {
        // Arrange — repositório retorna 1 parceiro com nome similar
        Partner existingPartner = Partner.Create(
            Guid.NewGuid(), "Acme Ltda", "Indicador",
            CommissionDefaults.Default, null, null, _roleProvider, Guid.NewGuid());
        _repository.FindByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { existingPartner });

        CreatePartnerHandler sut = CreateSut();

        // Act
        CreatePartnerResult result = await sut.Handle(ValidCommand(name: "Acme Ltda"), CancellationToken.None);

        // Assert — alerta ativado mas criação prosseguiu
        result.DuplicateNameAlert.Should().BeTrue();
        await _repository.Received(1).AddAsync(Arg.Any<Partner>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmptyName_ThrowsPartnerNameRequiredException()
    {
        // Arrange — o domínio lança ao criar PartnerName vazio
        CreatePartnerHandler sut = CreateSut();
        CreatePartnerCommand command = ValidCommand(name: "   ");

        // Act & Assert
        await sut.Invoking(h => h.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<PartnerNameRequiredException>();
    }

    [Fact]
    public async Task Handle_InvalidRole_ThrowsInvalidPartnerRoleException()
    {
        // Arrange — papel não canônico
        _roleProvider.IsCanonical(Arg.Any<string>(), Arg.Any<Guid>()).Returns(false);
        CreatePartnerHandler sut = CreateSut();

        // Act & Assert
        await sut.Invoking(h => h.Handle(ValidCommand(role: "PapelInexistente"), CancellationToken.None))
            .Should().ThrowAsync<InvalidPartnerRoleException>();
    }

    [Fact]
    public async Task Handle_InvalidEmail_ThrowsInvalidPartnerContactException()
    {
        // Arrange — e-mail malformado — a exceção é lançada pelo VO Email
        CreatePartnerHandler sut = CreateSut();
        CreatePartnerCommand command = ValidCommand(email: "nao-eh-email");

        // Act & Assert
        await sut.Invoking(h => h.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<InvalidPartnerContactException>();
    }

    [Fact]
    public async Task Handle_ZeroPercentuals_AcceptedAsTriagePending()
    {
        // Arrange — aceitar percentuais 0/0 (Req 11.1 — data-migration)
        CreatePartnerHandler sut = CreateSut();
        CreatePartnerCommand command = ValidCommand(pctSetup: 0.00m, pctRecorrente: 0.00m);

        // Act
        CreatePartnerResult result = await sut.Handle(command, CancellationToken.None);

        // Assert — cria sem erro
        result.PartnerId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_ValidCommandWithContact_StoresContact()
    {
        // Arrange
        Partner? capturedPartner = null;
        await _repository.AddAsync(Arg.Do<Partner>(p => capturedPartner = p), Arg.Any<CancellationToken>());
        CreatePartnerHandler sut = CreateSut();

        // Act
        await sut.Handle(
            ValidCommand(email: "parceiro@exemplo.com.br", phone: "11999990000"),
            CancellationToken.None);

        // Assert
        capturedPartner!.Contact.Should().NotBeNull();
    }
}
