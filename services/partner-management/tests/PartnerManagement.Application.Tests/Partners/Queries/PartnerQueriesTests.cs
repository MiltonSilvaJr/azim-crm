using FluentAssertions;
using NSubstitute;
using PartnerManagement.Application.Partners;
using PartnerManagement.Application.Partners.Queries;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Domain.Partners.Repositories;
using PartnerManagement.Domain.Partners.ValueObjects;
using Xunit;

namespace PartnerManagement.Application.Tests.Partners.Queries;

/// <summary>
/// Testes unitários dos handlers de query de parceiros.
/// Mapeia: TASK-11, Req 4, Req 8, Req 11.3, design §5.2.
/// </summary>
public sealed class PartnerQueriesTests
{
    private readonly IPartnerRepository _repository = Substitute.For<IPartnerRepository>();
    private readonly ICanonicalRoleProvider _roleProvider = Substitute.For<ICanonicalRoleProvider>();

    public PartnerQueriesTests()
    {
        _roleProvider.IsCanonical(Arg.Any<string>(), Arg.Any<Guid>()).Returns(true);
    }

    private Partner BuildActivePartner(Guid tenantId, string name = "Parceiro A") =>
        Partner.Create(tenantId, name, "Indicador",
            CommissionDefaults.Create(Percentage.Create(10m), Percentage.Create(5m)),
            null, null, _roleProvider, Guid.NewGuid());

    private Partner BuildInactivePartner(Guid tenantId, string name = "Parceiro B")
    {
        Partner p = Partner.Create(tenantId, name, "Revendedor",
            CommissionDefaults.Default, null, null, _roleProvider, Guid.NewGuid());
        p.Deactivate();
        return p;
    }

    private Partner BuildTriagePendingPartner(Guid tenantId, string name = "Parceiro C") =>
        Partner.Create(tenantId, name, "Integrador",
            CommissionDefaults.Default, null, null, _roleProvider, Guid.NewGuid());

    // ========== ListPartnersHandler ==========

    [Fact]
    public async Task ListPartners_DefaultQuery_ReturnsOnlyActivePartners()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        Partner active = BuildActivePartner(tenantId);
        _repository.ListAsync(true, false, 1, 20, Arg.Any<CancellationToken>())
            .Returns((new[] { active }.AsReadOnly() as IReadOnlyList<Partner>, 1));

        ListPartnersHandler sut = new(_repository);

        // Act
        ListPartnersResult result = await sut.Handle(
            new ListPartnersQuery(tenantId), CancellationToken.None);

        // Assert
        result.Partners.Should().HaveCount(1);
        result.Partners[0].Active.Should().BeTrue();
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task ListPartners_TriagePendingFilter_ReturnsOnlyTriagePendingPartners()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        Partner triage = BuildTriagePendingPartner(tenantId);
        _repository.ListAsync(true, true, 1, 20, Arg.Any<CancellationToken>())
            .Returns((new[] { triage }.AsReadOnly() as IReadOnlyList<Partner>, 1));

        ListPartnersHandler sut = new(_repository);

        // Act
        ListPartnersResult result = await sut.Handle(
            new ListPartnersQuery(tenantId, Active: true, TriagePending: true),
            CancellationToken.None);

        // Assert
        result.Partners.Should().HaveCount(1);
        result.Partners[0].IsTriagePending.Should().BeTrue();
    }

    [Fact]
    public async Task ListPartners_Pagination_ReturnsCorrectPage()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        _repository.ListAsync(true, false, 2, 5, Arg.Any<CancellationToken>())
            .Returns((Array.Empty<Partner>().AsReadOnly() as IReadOnlyList<Partner>, 0));

        ListPartnersHandler sut = new(_repository);

        // Act
        ListPartnersResult result = await sut.Handle(
            new ListPartnersQuery(tenantId, Page: 2, PageSize: 5),
            CancellationToken.None);

        // Assert
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(5);
    }

    [Fact]
    public async Task ListPartners_EmptyResult_ReturnsEmptyList()
    {
        // Arrange
        _repository.ListAsync(Arg.Any<bool?>(), Arg.Any<bool>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<Partner>().AsReadOnly() as IReadOnlyList<Partner>, 0));

        ListPartnersHandler sut = new(_repository);

        // Act
        ListPartnersResult result = await sut.Handle(
            new ListPartnersQuery(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.Partners.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    // ========== GetPartnerByIdHandler ==========

    [Fact]
    public async Task GetPartnerById_ExistingPartner_ReturnsDetail()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        Partner partner = BuildActivePartner(tenantId);
        _repository.GetByIdAsync(partner.Id, Arg.Any<CancellationToken>()).Returns(partner);

        GetPartnerByIdHandler sut = new(_repository);

        // Act
        PartnerDetail result = await sut.Handle(
            new GetPartnerByIdQuery(partner.Id, tenantId), CancellationToken.None);

        // Assert
        result.PartnerId.Should().Be(partner.Id);
        result.Active.Should().BeTrue();
        result.PctSetup.Should().Be(10.00m);
        result.PctRecorrente.Should().Be(5.00m);
        result.IsTriagePending.Should().BeFalse();
    }

    [Fact]
    public async Task GetPartnerById_NotFound_ThrowsPartnerNotFoundException()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Partner?)null);
        GetPartnerByIdHandler sut = new(_repository);

        // Act & Assert — PM-ERR-007
        await sut.Invoking(h => h.Handle(
                new GetPartnerByIdQuery(Guid.NewGuid(), Guid.NewGuid()),
                CancellationToken.None))
            .Should().ThrowAsync<PartnerNotFoundException>();
    }

    [Fact]
    public async Task GetPartnerById_InactivePartner_ReturnsActiveAsFalse()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        Partner partner = BuildInactivePartner(tenantId);
        _repository.GetByIdAsync(partner.Id, Arg.Any<CancellationToken>()).Returns(partner);

        GetPartnerByIdHandler sut = new(_repository);

        // Act
        PartnerDetail result = await sut.Handle(
            new GetPartnerByIdQuery(partner.Id, tenantId), CancellationToken.None);

        // Assert
        result.Active.Should().BeFalse();
    }

    // ========== GetPartnerEligibilityHandler ==========

    [Fact]
    public async Task GetEligibility_ActivePartner_ReturnsActiveTrue()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        Partner partner = BuildActivePartner(tenantId);
        _repository.GetByIdAsync(partner.Id, Arg.Any<CancellationToken>()).Returns(partner);

        GetPartnerEligibilityHandler sut = new(_repository);

        // Act
        PartnerEligibilityResult result = await sut.Handle(
            new GetPartnerEligibilityQuery(partner.Id, tenantId), CancellationToken.None);

        // Assert
        result.PartnerId.Should().Be(partner.Id);
        result.Active.Should().BeTrue();
    }

    [Fact]
    public async Task GetEligibility_InactivePartner_ReturnsActiveFalse()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        Partner partner = BuildInactivePartner(tenantId);
        _repository.GetByIdAsync(partner.Id, Arg.Any<CancellationToken>()).Returns(partner);

        GetPartnerEligibilityHandler sut = new(_repository);

        // Act
        PartnerEligibilityResult result = await sut.Handle(
            new GetPartnerEligibilityQuery(partner.Id, tenantId), CancellationToken.None);

        // Assert
        result.Active.Should().BeFalse();
    }

    [Fact]
    public async Task GetEligibility_NotFound_ThrowsPartnerNotFoundException()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Partner?)null);
        GetPartnerEligibilityHandler sut = new(_repository);

        // Act & Assert — PM-ERR-007
        await sut.Invoking(h => h.Handle(
                new GetPartnerEligibilityQuery(Guid.NewGuid(), Guid.NewGuid()),
                CancellationToken.None))
            .Should().ThrowAsync<PartnerNotFoundException>();
    }
}
