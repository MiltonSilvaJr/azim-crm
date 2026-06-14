using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PartnerManagement.Application.Partners;
using PartnerManagement.Application.Partners.Commands;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Domain.Partners.Repositories;
using PartnerManagement.Domain.Partners.ValueObjects;
using Xunit;

namespace PartnerManagement.Application.Tests.Partners.Commands;

/// <summary>
/// Testes unitários do <see cref="UpdatePartnerHandler"/>.
/// Mapeia: TASK-09, Req 2, Req 7, design §5.1.
/// </summary>
public sealed class UpdatePartnerHandlerTests
{
    private readonly IPartnerRepository _repository = Substitute.For<IPartnerRepository>();
    private readonly ICanonicalRoleProvider _roleProvider = Substitute.For<ICanonicalRoleProvider>();

    private UpdatePartnerHandler CreateSut() =>
        new(_repository, _roleProvider, NullLogger<UpdatePartnerHandler>.Instance);

    public UpdatePartnerHandlerTests()
    {
        _roleProvider.IsCanonical(Arg.Any<string>(), Arg.Any<Guid>()).Returns(true);
    }

    private Partner BuildPartner(Guid tenantId) =>
        Partner.Create(
            tenantId, "Original Ltda", "Indicador",
            CommissionDefaults.Default, null, null, _roleProvider, Guid.NewGuid());

    private UpdatePartnerCommand ValidUpdateCommand(Guid partnerId, Guid tenantId) =>
        new(
            PartnerId: partnerId,
            TenantId: tenantId,
            Name: "Nome Atualizado",
            Role: "Revendedor",
            CommissionDefaults: CommissionDefaults.Create(
                Percentage.Create(15.00m),
                Percentage.Create(8.00m)),
            ContactEmail: null,
            ContactPhone: null,
            Notes: "Nota atualizada",
            UpdatedBy: Guid.NewGuid());

    [Fact]
    public async Task Handle_ExistingPartner_UpdatesAndPersists()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        Partner partner = BuildPartner(tenantId);
        _repository.GetByIdAsync(partner.Id, Arg.Any<CancellationToken>()).Returns(partner);

        UpdatePartnerHandler sut = CreateSut();

        // Act
        UpdatePartnerResult result = await sut.Handle(
            ValidUpdateCommand(partner.Id, tenantId), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        await _repository.Received(1).UpdateAsync(partner, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PartnerNotFound_ThrowsPartnerNotFoundException()
    {
        // Arrange — repositório retorna null
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Partner?)null);
        UpdatePartnerHandler sut = CreateSut();
        UpdatePartnerCommand command = ValidUpdateCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert — PM-ERR-007
        await sut.Invoking(h => h.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<PartnerNotFoundException>();
    }

    [Fact]
    public async Task Handle_CommissionChange_AccumulatesCommissionUpdatedEvent()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        Partner partner = BuildPartner(tenantId);
        _repository.GetByIdAsync(partner.Id, Arg.Any<CancellationToken>()).Returns(partner);

        UpdatePartnerHandler sut = CreateSut();

        // Act
        await sut.Handle(
            ValidUpdateCommand(partner.Id, tenantId),
            CancellationToken.None);

        // Assert — parceiro com Default (0/0) foi alterado para 15/8 → evento acumulado
        partner.DomainEvents.Should().Contain(e =>
            e.GetType().Name == "PartnerCommissionPercentagesUpdated");
    }

    [Fact]
    public async Task Handle_NoCommissionChange_DoesNotAccumulateCommissionUpdatedEvent()
    {
        // Arrange — Update com mesmos percentuais do default (0/0)
        Guid tenantId = Guid.NewGuid();
        Partner partner = BuildPartner(tenantId);
        _repository.GetByIdAsync(partner.Id, Arg.Any<CancellationToken>()).Returns(partner);

        UpdatePartnerHandler sut = CreateSut();
        UpdatePartnerCommand command = new(
            PartnerId: partner.Id,
            TenantId: tenantId,
            Name: "Nome Atualizado",
            Role: "Indicador",
            CommissionDefaults: CommissionDefaults.Default, // mesmos percentuais
            ContactEmail: null,
            ContactPhone: null,
            Notes: null,
            UpdatedBy: Guid.NewGuid());

        // Act
        await sut.Handle(command, CancellationToken.None);

        // Assert — sem mudança de percentuais → nenhum evento de comissão
        partner.DomainEvents.Should().NotContain(e =>
            e.GetType().Name == "PartnerCommissionPercentagesUpdated");
    }

    [Fact]
    public async Task Handle_WithValidEmail_UpdatesContact()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        Partner partner = BuildPartner(tenantId);
        _repository.GetByIdAsync(partner.Id, Arg.Any<CancellationToken>()).Returns(partner);

        UpdatePartnerHandler sut = CreateSut();
        UpdatePartnerCommand command = ValidUpdateCommand(partner.Id, tenantId) with
        {
            ContactEmail = "novo@exemplo.com.br"
        };

        // Act
        await sut.Handle(command, CancellationToken.None);

        // Assert
        partner.Contact.Should().NotBeNull();
    }
}
