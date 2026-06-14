using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PartnerManagement.Application.Partners;
using PartnerManagement.Application.Partners.Commands;
using PartnerManagement.Application.Ports;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Domain.Partners.Repositories;
using PartnerManagement.Domain.Partners.ValueObjects;
using Xunit;

namespace PartnerManagement.Application.Tests.Partners.Commands;

/// <summary>
/// Testes unitários dos handlers <see cref="DeactivatePartnerHandler"/> e <see cref="ReactivatePartnerHandler"/>.
/// Verifica idempotência, eventos de domínio e auditoria sempre registrada (DD-006, PBT-02).
/// Mapeia: TASK-10, Req 3, design §5.1.
/// </summary>
public sealed class DeactivateReactivateHandlerTests
{
    private readonly IPartnerRepository _repository = Substitute.For<IPartnerRepository>();
    private readonly IAuditPublisher _auditPublisher = Substitute.For<IAuditPublisher>();
    private readonly ICanonicalRoleProvider _roleProvider = Substitute.For<ICanonicalRoleProvider>();
    private readonly IPartnerMetrics _metrics = Substitute.For<IPartnerMetrics>();

    public DeactivateReactivateHandlerTests()
    {
        _roleProvider.IsCanonical(Arg.Any<string>(), Arg.Any<Guid>()).Returns(true);
    }

    private Partner BuildActivePartner(Guid tenantId) =>
        Partner.Create(tenantId, "Parceiro Teste", "Indicador",
            CommissionDefaults.Default, null, null, _roleProvider, Guid.NewGuid());

    // ========== Deactivate ==========

    [Fact]
    public async Task DeactivateHandler_ActivePartner_TransitionsToInactiveAndEmitsEvent()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        Partner partner = BuildActivePartner(tenantId);
        _repository.GetByIdAsync(partner.Id, Arg.Any<CancellationToken>()).Returns(partner);

        DeactivatePartnerHandler sut = new(_repository, _auditPublisher, _metrics,
            NullLogger<DeactivatePartnerHandler>.Instance);

        // Act
        DeactivatePartnerResult result = await sut.Handle(
            new DeactivatePartnerCommand(partner.Id, tenantId, Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        result.TransitionEffective.Should().BeTrue();
        partner.Status.Should().Be(PartnerStatus.Inactive);
        partner.DomainEvents.Should().Contain(e => e.GetType().Name == "PartnerDeactivated");
        await _repository.Received(1).UpdateAsync(partner, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateHandler_AlreadyInactivePartner_IsIdempotentAndNoTransitionEvent()
    {
        // Arrange — inativar para deixar no estado Inactive
        Guid tenantId = Guid.NewGuid();
        Partner partner = BuildActivePartner(tenantId);
        partner.Deactivate(); // estado inicial: Inactive
        partner.ClearDomainEvents(); // limpar eventos da inativação inicial

        _repository.GetByIdAsync(partner.Id, Arg.Any<CancellationToken>()).Returns(partner);

        DeactivatePartnerHandler sut = new(_repository, _auditPublisher, _metrics,
            NullLogger<DeactivatePartnerHandler>.Instance);

        // Act — tentativa idempotente
        DeactivatePartnerResult result = await sut.Handle(
            new DeactivatePartnerCommand(partner.Id, tenantId, Guid.NewGuid()),
            CancellationToken.None);

        // Assert — sem transição efetiva, sem evento, auditoria registrada
        result.TransitionEffective.Should().BeFalse();
        partner.DomainEvents.Should().NotContain(e => e.GetType().Name == "PartnerDeactivated");
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Partner>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateHandler_AlwaysRecordsAudit()
    {
        // Arrange — mesmo idempotente, auditoria deve ser registrada
        Guid tenantId = Guid.NewGuid();
        Partner partner = BuildActivePartner(tenantId);
        partner.Deactivate();
        partner.ClearDomainEvents();

        _repository.GetByIdAsync(partner.Id, Arg.Any<CancellationToken>()).Returns(partner);

        DeactivatePartnerHandler sut = new(_repository, _auditPublisher, _metrics,
            NullLogger<DeactivatePartnerHandler>.Instance);

        // Act
        await sut.Handle(
            new DeactivatePartnerCommand(partner.Id, tenantId, Guid.NewGuid()),
            CancellationToken.None);

        // Assert — auditoria chamada independentemente da transição efetiva
        await _auditPublisher.Received(1).PublishAsync(
            "Partner", partner.Id, tenantId,
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateHandler_PartnerNotFound_ThrowsPartnerNotFoundException()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Partner?)null);
        DeactivatePartnerHandler sut = new(_repository, _auditPublisher, _metrics,
            NullLogger<DeactivatePartnerHandler>.Instance);

        // Act & Assert — PM-ERR-007
        await sut.Invoking(h => h.Handle(
                new DeactivatePartnerCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
                CancellationToken.None))
            .Should().ThrowAsync<PartnerNotFoundException>();
    }

    // ========== Reactivate ==========

    [Fact]
    public async Task ReactivateHandler_InactivePartner_TransitionsToActiveAndEmitsEvent()
    {
        // Arrange — partir de inativo
        Guid tenantId = Guid.NewGuid();
        Partner partner = BuildActivePartner(tenantId);
        partner.Deactivate();
        partner.ClearDomainEvents();

        _repository.GetByIdAsync(partner.Id, Arg.Any<CancellationToken>()).Returns(partner);

        ReactivatePartnerHandler sut = new(_repository, _auditPublisher, _metrics,
            NullLogger<ReactivatePartnerHandler>.Instance);

        // Act
        ReactivatePartnerResult result = await sut.Handle(
            new ReactivatePartnerCommand(partner.Id, tenantId, Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        result.TransitionEffective.Should().BeTrue();
        partner.Status.Should().Be(PartnerStatus.Active);
        partner.DomainEvents.Should().Contain(e => e.GetType().Name == "PartnerReactivated");
        await _repository.Received(1).UpdateAsync(partner, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReactivateHandler_AlreadyActivePartner_IsIdempotentAndNoTransitionEvent()
    {
        // Arrange — parceiro já ativo
        Guid tenantId = Guid.NewGuid();
        Partner partner = BuildActivePartner(tenantId);
        partner.ClearDomainEvents();

        _repository.GetByIdAsync(partner.Id, Arg.Any<CancellationToken>()).Returns(partner);

        ReactivatePartnerHandler sut = new(_repository, _auditPublisher, _metrics,
            NullLogger<ReactivatePartnerHandler>.Instance);

        // Act
        ReactivatePartnerResult result = await sut.Handle(
            new ReactivatePartnerCommand(partner.Id, tenantId, Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        result.TransitionEffective.Should().BeFalse();
        partner.DomainEvents.Should().NotContain(e => e.GetType().Name == "PartnerReactivated");
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Partner>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReactivateHandler_AlwaysRecordsAudit()
    {
        // Arrange — mesmo idempotente
        Guid tenantId = Guid.NewGuid();
        Partner partner = BuildActivePartner(tenantId);
        partner.ClearDomainEvents();

        _repository.GetByIdAsync(partner.Id, Arg.Any<CancellationToken>()).Returns(partner);

        ReactivatePartnerHandler sut = new(_repository, _auditPublisher, _metrics,
            NullLogger<ReactivatePartnerHandler>.Instance);

        // Act
        await sut.Handle(
            new ReactivatePartnerCommand(partner.Id, tenantId, Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        await _auditPublisher.Received(1).PublishAsync(
            "Partner", partner.Id, tenantId,
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReactivateHandler_PartnerNotFound_ThrowsPartnerNotFoundException()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Partner?)null);
        ReactivatePartnerHandler sut = new(_repository, _auditPublisher, _metrics,
            NullLogger<ReactivatePartnerHandler>.Instance);

        // Act & Assert — PM-ERR-007
        await sut.Invoking(h => h.Handle(
                new ReactivatePartnerCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
                CancellationToken.None))
            .Should().ThrowAsync<PartnerNotFoundException>();
    }
}
