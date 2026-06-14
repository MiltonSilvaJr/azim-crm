using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Reporting.Application.Ports;
using Reporting.Contracts.ReadModels;
using Reporting.Contracts.Responses;
using Reporting.Domain.Enums;
using Reporting.Domain.ValueObjects;
using Xunit;

namespace Reporting.Application.Tests.Ports;

/// <summary>
/// Testes de contrato de porta — TASK-05, ST-01.
/// Verificam assinaturas e comportamentos esperados das quatro interfaces de porta
/// usando doubles (stubs/mocks com NSubstitute).
/// Mapeia: design §5.2, §5.3, §6.4, DD-004, Req 5, Req 7.
/// </summary>
public sealed class PortContractTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OwnerId  = Guid.NewGuid();

    private static ReportScope ValidScope() =>
        ReportScope.Create(TenantId, ReportingRole.TenantAdmin, [], null);

    private static Period ValidPeriod() =>
        Period.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));

    // ──────────────────────────────────────────────────────────────
    // IReportingReadRepository
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task IReportingReadRepository_GetFunnelAsync_ReturnsTypedCollection()
    {
        // Arrange
        var repo = Substitute.For<IReportingReadRepository>();
        var scope = ValidScope();
        var period = ValidPeriod();
        var expected = new List<FunnelRow>
        {
            new(Guid.NewGuid(), "Prospecção", "open", 5, 100_000L, 50_000L)
        };
        repo.GetFunnelAsync(scope, period, null, Arg.Any<CancellationToken>())
            .Returns(expected);

        // Act
        var result = await repo.GetFunnelAsync(scope, period, null);

        // Assert
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task IReportingReadRepository_GetForecastAsync_ReturnsTypedCollection()
    {
        var repo = Substitute.For<IReportingReadRepository>();
        var scope = ValidScope();
        var period = ValidPeriod();
        var expected = new List<ForecastRow>
        {
            new(Guid.NewGuid(), "BU Alpha", 2026, 1, 200_000L, 100_000L, null)
        };
        repo.GetForecastAsync(scope, period, null, Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await repo.GetForecastAsync(scope, period, null);
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task IReportingReadRepository_GetRankingAsync_ReturnsTypedCollection()
    {
        var repo = Substitute.For<IReportingReadRepository>();
        var scope = ValidScope();
        var period = ValidPeriod();
        var expected = new List<RankingRow>
        {
            new(OwnerId, "João Silva", 3, 500_000L, 250_000L)
        };
        repo.GetRankingAsync(scope, period, null, Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await repo.GetRankingAsync(scope, period, null);
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task IReportingReadRepository_GetChannelAsync_ReturnsTypedCollection()
    {
        var repo = Substitute.For<IReportingReadRepository>();
        var scope = ValidScope();
        var period = ValidPeriod();
        var expected = new List<ChannelRow>
        {
            new(Guid.NewGuid(), "Indicação", 10, 1_000_000L, 10_000)
        };
        repo.GetChannelAsync(scope, period, null, Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await repo.GetChannelAsync(scope, period, null);
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task IReportingReadRepository_GetCommissionsAsync_ReturnsTypedCollection()
    {
        var repo = Substitute.For<IReportingReadRepository>();
        var scope = ValidScope();
        var period = ValidPeriod();
        var expected = new List<CommissionRow>
        {
            new(Guid.NewGuid(), "Parceiro A", 15_000L, true, "won")
        };
        repo.GetCommissionsAsync(scope, period, null, Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await repo.GetCommissionsAsync(scope, period, null);
        result.Should().BeEquivalentTo(expected);
    }

    // ──────────────────────────────────────────────────────────────
    // IScopeResolver
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task IScopeResolver_ResolveAsync_ReturnsScope()
    {
        var resolver = Substitute.For<IScopeResolver>();
        var expected = ValidScope();
        resolver.ResolveAsync(TenantId, OwnerId, Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await resolver.ResolveAsync(TenantId, OwnerId);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task IScopeResolver_ResolveAsync_ThrowsForEmptyTenantId()
    {
        // Contrato: IScopeResolver lança para tenantId vazio (design §5.3, Req 7)
        var resolver = Substitute.For<IScopeResolver>();
        resolver.ResolveAsync(Guid.Empty, OwnerId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new ArgumentException("tenantId não pode ser vazio"));

        var act = () => resolver.ResolveAsync(Guid.Empty, OwnerId);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ──────────────────────────────────────────────────────────────
    // ICsvReportWriter
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void ICsvReportWriter_WriteFunnel_ReturnsBytes()
    {
        var writer = Substitute.For<ICsvReportWriter>();
        var response = new FunnelReportResponse([]);
        writer.WriteFunnel(response).Returns([0xEF, 0xBB, 0xBF]);

        var result = writer.WriteFunnel(response);
        result.Should().NotBeEmpty();
    }

    // ──────────────────────────────────────────────────────────────
    // ICsvStorage
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ICsvStorage_UploadAsync_ReturnsCsvUploadResult()
    {
        var storage = Substitute.For<ICsvStorage>();
        var objectName = "reports/tenant/funnel/hash/scope.csv";
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF };
        var expected = new CsvUploadResult(
            "https://storage.googleapis.com/bucket/object?X-Goog-Expires=900",
            DateTimeOffset.UtcNow.AddMinutes(15),
            objectName);
        storage.UploadAsync(objectName, bytes, Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await storage.UploadAsync(objectName, bytes);
        result.SignedUrl.Should().StartWith("https://");
        result.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }
}
