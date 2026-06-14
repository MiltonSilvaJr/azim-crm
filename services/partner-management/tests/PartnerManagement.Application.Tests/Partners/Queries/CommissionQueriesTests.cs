using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PartnerManagement.Application.Partners;
using PartnerManagement.Application.Partners.Queries;
using PartnerManagement.Application.Ports;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Domain.Partners.Repositories;
using PartnerManagement.Domain.Partners.ValueObjects;
using Xunit;

namespace PartnerManagement.Application.Tests.Partners.Queries;

/// <summary>
/// Testes unitários dos handlers de comissão.
/// Verifica composição do read model, degradação parcial, PM-ERR-007 e PM-ERR-011.
/// Mapeia: TASK-12, Req 9, Req 10, DD-003, design §5.2.
/// </summary>
public sealed class CommissionQueriesTests
{
    private readonly IPartnerRepository _repository = Substitute.For<IPartnerRepository>();
    private readonly IPartnerCommissionReadPort _readPort = Substitute.For<IPartnerCommissionReadPort>();
    private readonly ICanonicalRoleProvider _roleProvider = Substitute.For<ICanonicalRoleProvider>();

    private readonly DateTimeOffset _from = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly DateTimeOffset _to = new(2026, 3, 31, 23, 59, 59, TimeSpan.Zero);

    public CommissionQueriesTests()
    {
        _roleProvider.IsCanonical(Arg.Any<string>(), Arg.Any<Guid>()).Returns(true);
    }

    private Partner BuildActivePartner(Guid tenantId) =>
        Partner.Create(tenantId, "Parceiro X", "Indicador",
            CommissionDefaults.Default, null, null, _roleProvider, Guid.NewGuid());

    private GetPartnerCommissionViewQuery ViewQuery(Guid partnerId, Guid tenantId,
        DateTimeOffset? from = null, DateTimeOffset? to = null) =>
        new(partnerId, tenantId, from ?? _from, to ?? _to);

    private GetPartnerCommissionReportQuery ReportQuery(Guid partnerId, Guid tenantId,
        DateTimeOffset? from = null, DateTimeOffset? to = null) =>
        new(partnerId, tenantId, from ?? _from, to ?? _to);

    private GetPartnerCommissionViewHandler CreateViewSut() =>
        new(_repository, _readPort, NullLogger<GetPartnerCommissionViewHandler>.Instance);

    private GetPartnerCommissionReportHandler CreateReportSut() =>
        new(_repository, _readPort, NullLogger<GetPartnerCommissionReportHandler>.Instance);

    // ========== CommissionView ==========

    [Fact]
    public async Task CommissionView_WithMixedLines_ReturnsSeparateSums()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        Partner partner = BuildActivePartner(tenantId);
        _repository.GetByIdAsync(partner.Id, Arg.Any<CancellationToken>()).Returns(partner);

        // 2 oportunidades abertas (projetadas) e 1 snapshot (consolidada)
        CommissionLine[] lines =
        [
            new CommissionLine(Guid.NewGuid(), 10000L, IsSnapshot: false, _from.AddDays(5)),
            new CommissionLine(Guid.NewGuid(), 20000L, IsSnapshot: false, _from.AddDays(10)),
            new CommissionLine(Guid.NewGuid(), 50000L, IsSnapshot: true, _from.AddDays(15)),
        ];
        _readPort.GetCommissionLinesAsync(tenantId, partner.Id, _from, _to, null, Arg.Any<CancellationToken>())
            .Returns(lines);

        GetPartnerCommissionViewHandler sut = CreateViewSut();

        // Act
        CommissionViewResult result = await sut.Handle(ViewQuery(partner.Id, tenantId), CancellationToken.None);

        // Assert
        result.ProjectedCommissionCents.Should().Be(30000L);  // 10000 + 20000
        result.ConsolidatedCommissionCents.Should().Be(50000L);
        result.CommissionUnavailable.Should().BeFalse();
    }

    [Fact]
    public async Task CommissionView_EmptyLines_ReturnsBothZero()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        Partner partner = BuildActivePartner(tenantId);
        _repository.GetByIdAsync(partner.Id, Arg.Any<CancellationToken>()).Returns(partner);
        _readPort.GetCommissionLinesAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(),
            Arg.Any<DateTimeOffset>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<CommissionLine>());

        GetPartnerCommissionViewHandler sut = CreateViewSut();

        // Act
        CommissionViewResult result = await sut.Handle(ViewQuery(partner.Id, tenantId), CancellationToken.None);

        // Assert
        result.ProjectedCommissionCents.Should().Be(0L);
        result.ConsolidatedCommissionCents.Should().Be(0L);
        result.CommissionUnavailable.Should().BeFalse();
    }

    [Fact]
    public async Task CommissionView_ReadPortUnavailable_ReturnsDegradedResult()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        Partner partner = BuildActivePartner(tenantId);
        _repository.GetByIdAsync(partner.Id, Arg.Any<CancellationToken>()).Returns(partner);
        _readPort.GetCommissionLinesAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(),
            Arg.Any<DateTimeOffset>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Serviço indisponível"));

        GetPartnerCommissionViewHandler sut = CreateViewSut();

        // Act — não deve lançar exceção ao cliente
        CommissionViewResult result = await sut.Handle(ViewQuery(partner.Id, tenantId), CancellationToken.None);

        // Assert — dados de cadastro disponíveis, seção de comissão marcada como indisponível
        result.CommissionUnavailable.Should().BeTrue();
        result.ProjectedCommissionCents.Should().Be(0L);
        result.ConsolidatedCommissionCents.Should().Be(0L);
    }

    [Fact]
    public async Task CommissionView_InvalidPeriod_ThrowsInvalidCommissionPeriodException()
    {
        // Arrange — from >= to
        Guid tenantId = Guid.NewGuid();
        Guid partnerId = Guid.NewGuid();
        GetPartnerCommissionViewHandler sut = CreateViewSut();

        // Act & Assert — PM-ERR-011
        await sut.Invoking(h => h.Handle(
                ViewQuery(partnerId, tenantId, from: _to, to: _from), CancellationToken.None))
            .Should().ThrowAsync<InvalidCommissionPeriodException>();
    }

    [Fact]
    public async Task CommissionView_PartnerNotFound_ThrowsPartnerNotFoundException()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Partner?)null);
        GetPartnerCommissionViewHandler sut = CreateViewSut();

        // Act & Assert — PM-ERR-007
        await sut.Invoking(h => h.Handle(
                ViewQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None))
            .Should().ThrowAsync<PartnerNotFoundException>();
    }

    // ========== CommissionReport ==========

    [Fact]
    public async Task CommissionReport_WithLines_ReturnsLineByLine()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        Partner partner = BuildActivePartner(tenantId);
        _repository.GetByIdAsync(partner.Id, Arg.Any<CancellationToken>()).Returns(partner);

        Guid oppId = Guid.NewGuid();
        CommissionLine[] lines = [new CommissionLine(oppId, 15000L, IsSnapshot: true, _from.AddDays(20))];
        _readPort.GetCommissionLinesAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(),
            Arg.Any<DateTimeOffset>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(lines);

        GetPartnerCommissionReportHandler sut = CreateReportSut();

        // Act
        CommissionReportResult result = await sut.Handle(ReportQuery(partner.Id, tenantId), CancellationToken.None);

        // Assert
        result.Lines.Should().HaveCount(1);
        result.Lines[0].OpportunityId.Should().Be(oppId);
        result.Lines[0].CommissionCents.Should().Be(15000L);
        result.Lines[0].IsSnapshot.Should().BeTrue();
        result.CommissionUnavailable.Should().BeFalse();
    }

    [Fact]
    public async Task CommissionReport_ReadPortUnavailable_ReturnsDegradedResult()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        Partner partner = BuildActivePartner(tenantId);
        _repository.GetByIdAsync(partner.Id, Arg.Any<CancellationToken>()).Returns(partner);
        _readPort.GetCommissionLinesAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(),
            Arg.Any<DateTimeOffset>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Timeout"));

        GetPartnerCommissionReportHandler sut = CreateReportSut();

        // Act
        CommissionReportResult result = await sut.Handle(ReportQuery(partner.Id, tenantId), CancellationToken.None);

        // Assert — degradação parcial sem lançar 5xx
        result.CommissionUnavailable.Should().BeTrue();
        result.Lines.Should().BeEmpty();
    }

    [Fact]
    public async Task CommissionReport_InvalidPeriod_ThrowsInvalidCommissionPeriodException()
    {
        // Arrange — from == to (inválido)
        Guid tenantId = Guid.NewGuid();
        GetPartnerCommissionReportHandler sut = CreateReportSut();

        await sut.Invoking(h => h.Handle(
                ReportQuery(Guid.NewGuid(), tenantId, from: _from, to: _from), CancellationToken.None))
            .Should().ThrowAsync<InvalidCommissionPeriodException>();
    }

    [Fact]
    public async Task CommissionReport_PartnerNotFound_ThrowsPartnerNotFoundException()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Partner?)null);
        GetPartnerCommissionReportHandler sut = CreateReportSut();

        await sut.Invoking(h => h.Handle(
                ReportQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None))
            .Should().ThrowAsync<PartnerNotFoundException>();
    }
}
