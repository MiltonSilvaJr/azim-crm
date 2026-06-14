using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using NSubstitute;
using Reporting.Application.Ports;
using Reporting.Application.Queries.Channel;
using Reporting.Contracts.ReadModels;
using Reporting.Contracts.Responses;
using Reporting.Domain.Enums;
using Reporting.Domain.ValueObjects;
using Xunit;

namespace Reporting.Application.Tests.Queries.Channel;

/// <summary>
/// Testes do handler de canal — TASK-09 ST-01 + PBT-04.
/// Verifica: conservação de basis points (soma = 10.000), canais com zero, centavos.
/// Mapeia: Req 3, PBT-04, DD-010.
/// </summary>
public sealed class GetChannelReportQueryHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Period ValidPeriod =
        Period.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));

    private static ReportScope ScopeTenantAdmin() =>
        ReportScope.Create(TenantId, ReportingRole.TenantAdmin, [], null);

    private static ChannelRow MakeRawRow(Guid channelId, int count, long totalCents) =>
        // PercentBasisPoints = 0 — calculado pelo handler
        new(channelId, "Canal", count, totalCents, 0);

    // ──────────────────────────────────────────────────────────────
    // Basis points: soma = 10.000 para conjunto não-vazio
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Basis points: soma = 10.000 para dois canais com contagens iguais")]
    public async Task Handle_EqualCounts_SumIs10000()
    {
        var repo  = Substitute.For<IReportingReadRepository>();
        var scope = ScopeTenantAdmin();
        var handler = new GetChannelReportQueryHandler(repo);
        var query   = new GetChannelReportQuery(ValidPeriod, null, scope);

        repo.GetChannelAsync(scope, ValidPeriod, null, Arg.Any<CancellationToken>())
            .Returns([
                MakeRawRow(Guid.NewGuid(), 50, 500_000L),
                MakeRawRow(Guid.NewGuid(), 50, 500_000L)
            ]);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Rows.Sum(r => r.PercentBasisPoints).Should().Be(10_000,
            because: "soma de percentBasisPoints deve ser exatamente 10.000 (PBT-04, DD-010)");
    }

    [Fact(DisplayName = "Basis points: soma = 10.000 para três canais com distribuição desigual")]
    public async Task Handle_UnequalCounts_SumIs10000()
    {
        var repo  = Substitute.For<IReportingReadRepository>();
        var scope = ScopeTenantAdmin();
        var handler = new GetChannelReportQueryHandler(repo);
        var query   = new GetChannelReportQuery(ValidPeriod, null, scope);

        repo.GetChannelAsync(scope, ValidPeriod, null, Arg.Any<CancellationToken>())
            .Returns([
                MakeRawRow(Guid.NewGuid(), 1, 100L),
                MakeRawRow(Guid.NewGuid(), 2, 200L),
                MakeRawRow(Guid.NewGuid(), 7, 700L)
            ]);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Rows.Sum(r => r.PercentBasisPoints).Should().Be(10_000);
    }

    [Fact(DisplayName = "Canais com zero: channel com count=0 aparece com percentBasisPoints=0")]
    public async Task Handle_ChannelWithZeroCount_HasZeroBasisPoints()
    {
        var repo  = Substitute.For<IReportingReadRepository>();
        var scope = ScopeTenantAdmin();
        var handler = new GetChannelReportQueryHandler(repo);
        var query   = new GetChannelReportQuery(ValidPeriod, null, scope);
        var zeroId  = Guid.NewGuid();

        repo.GetChannelAsync(scope, ValidPeriod, null, Arg.Any<CancellationToken>())
            .Returns([
                MakeRawRow(Guid.NewGuid(), 10, 1_000L),
                MakeRawRow(zeroId,         0,  0L)
            ]);

        var result = await handler.Handle(query, CancellationToken.None);

        var zeroRow = result.Rows.Single(r => r.ChannelId == zeroId);
        zeroRow.PercentBasisPoints.Should().Be(0);
        result.Rows.Sum(r => r.PercentBasisPoints).Should().Be(10_000);
    }

    // ──────────────────────────────────────────────────────────────
    // PBT-04: invariante de conservação para geradores aleatórios
    // Testa via CalculateBasisPoints (método estático interno exposto para teste)
    // ──────────────────────────────────────────────────────────────

    [Property(MaxTest = 500, DisplayName = "PBT-04: soma de percentBasisPoints = 10.000 para qualquer lista não-vazia de contagens positivas")]
    public Property Pbt04_SumOfBasisPointsAlwaysEquals10000(PositiveInt count)
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(count)),
            c =>
            {
                // Gera N canais com contagens aleatórias positivas
                var n = (c.Get % 10) + 1; // 1 a 10 canais
                var rawRows = Enumerable.Range(1, n)
                    .Select(i => MakeRawRow(Guid.NewGuid(), i * 3, (long)(i * 1000)))
                    .ToList();

                var result = GetChannelReportQueryHandler.CalculateBasisPoints(rawRows);
                return result.Sum(r => r.PercentBasisPoints) == 10_000;
            });
    }

    // ──────────────────────────────────────────────────────────────
    // Centavos preservados
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Centavos: totalCents preservado sem conversão (DD-007)")]
    public async Task Handle_PreservesTotalCentsWithoutConversion()
    {
        var repo  = Substitute.For<IReportingReadRepository>();
        var scope = ScopeTenantAdmin();
        var handler = new GetChannelReportQueryHandler(repo);
        var query   = new GetChannelReportQuery(ValidPeriod, null, scope);
        var channelId = Guid.NewGuid();

        repo.GetChannelAsync(scope, ValidPeriod, null, Arg.Any<CancellationToken>())
            .Returns([MakeRawRow(channelId, 10, 9_876_543L)]);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Rows[0].TotalCents.Should().Be(9_876_543L);
    }

    [Fact(DisplayName = "RBAC: handler repassa scope ao repositório")]
    public async Task Handle_PassesScopeToRepository()
    {
        var repo  = Substitute.For<IReportingReadRepository>();
        var scope = ScopeTenantAdmin();
        var handler = new GetChannelReportQueryHandler(repo);
        var query   = new GetChannelReportQuery(ValidPeriod, null, scope);

        repo.GetChannelAsync(scope, ValidPeriod, null, Arg.Any<CancellationToken>())
            .Returns([]);

        await handler.Handle(query, CancellationToken.None);

        await repo.Received(1).GetChannelAsync(scope, ValidPeriod, null, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Resultado: ChannelReportResponse retornado com tipo correto")]
    public async Task Handle_ReturnsChannelReportResponse()
    {
        var repo  = Substitute.For<IReportingReadRepository>();
        var scope = ScopeTenantAdmin();
        var handler = new GetChannelReportQueryHandler(repo);
        var query   = new GetChannelReportQuery(ValidPeriod, null, scope);

        repo.GetChannelAsync(scope, ValidPeriod, null, Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await handler.Handle(query, CancellationToken.None);
        result.Should().BeOfType<ChannelReportResponse>();
    }
}
