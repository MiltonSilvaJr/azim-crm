using FluentAssertions;
using NSubstitute;
using Reporting.Application.Ports;
using Reporting.Application.Queries.Forecast;
using Reporting.Contracts.ReadModels;
using Reporting.Contracts.Responses;
using Reporting.Domain.Enums;
using Reporting.Domain.ValueObjects;
using Xunit;

namespace Reporting.Application.Tests.Queries.Forecast;

/// <summary>
/// Testes do handler de forecast — TASK-07, ST-01.
/// Verifica: degradação graciosa (meta ausente = null, nunca zero), preservação de centavos, RBAC.
/// Mapeia: Req 6, Req 6.3, design §5.2, §5.3, DD-007, P8.
/// </summary>
public sealed class GetForecastReportQueryHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BuId     = Guid.NewGuid();
    private static readonly Period ValidPeriod =
        Period.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));

    private static ReportScope ScopeTenantAdmin() =>
        ReportScope.Create(TenantId, ReportingRole.TenantAdmin, [], null);

    private static ReportScope ScopeGestorBu() =>
        ReportScope.Create(TenantId, ReportingRole.GestorBU, [BuId], null);

    // ──────────────────────────────────────────────────────────────
    // Degradação graciosa: meta ausente → null, nunca zero (Req 6.3)
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Degradação graciosa: goalCents null quando meta ausente (Req 6.3)")]
    public async Task Handle_WhenGoalAbsent_GoalCentsIsNull()
    {
        var repo    = Substitute.For<IReportingReadRepository>();
        var scope   = ScopeTenantAdmin();
        var handler = new GetForecastReportQueryHandler(repo);
        var query   = new GetForecastReportQuery(ValidPeriod, null, scope);

        repo.GetForecastAsync(scope, ValidPeriod, null, Arg.Any<CancellationToken>())
            .Returns([new ForecastRow(BuId, "BU Alpha", 2026, 1, 200_000L, 100_000L, null)]);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Rows.Should().HaveCount(1);
        result.Rows[0].GoalCents.Should().BeNull(
            because: "ausência de meta não gera zero artificial — degradação graciosa (Req 6.3, P8)");
    }

    [Fact(DisplayName = "Degradação graciosa: goalCents preenchido quando meta cadastrada (Req 6)")]
    public async Task Handle_WhenGoalPresent_GoalCentsIsPopulated()
    {
        var repo    = Substitute.For<IReportingReadRepository>();
        var scope   = ScopeTenantAdmin();
        var handler = new GetForecastReportQueryHandler(repo);
        var query   = new GetForecastReportQuery(ValidPeriod, null, scope);

        repo.GetForecastAsync(scope, ValidPeriod, null, Arg.Any<CancellationToken>())
            .Returns([new ForecastRow(BuId, "BU Alpha", 2026, 1, 200_000L, 100_000L, 300_000L)]);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Rows[0].GoalCents.Should().Be(300_000L);
    }

    [Fact(DisplayName = "Degradação graciosa: mesma BU com e sem meta — ambas as linhas presentes")]
    public async Task Handle_MixedGoalPresence_ReturnsBothRows()
    {
        var repo    = Substitute.For<IReportingReadRepository>();
        var scope   = ScopeTenantAdmin();
        var handler = new GetForecastReportQueryHandler(repo);
        var query   = new GetForecastReportQuery(ValidPeriod, null, scope);
        var bu2     = Guid.NewGuid();

        repo.GetForecastAsync(scope, ValidPeriod, null, Arg.Any<CancellationToken>())
            .Returns([
                new ForecastRow(BuId, "BU Alpha", 2026, 1, 200_000L, 100_000L, 300_000L),
                new ForecastRow(bu2,  "BU Beta",  2026, 1, 50_000L,  25_000L,  null)
            ]);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Rows.Should().HaveCount(2);
        result.Rows[1].GoalCents.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────
    // Preservação de centavos
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Centavos: weightedForecastCents e realizedCents preservados sem conversão (DD-007)")]
    public async Task Handle_PreservesCentsWithoutConversion()
    {
        var repo    = Substitute.For<IReportingReadRepository>();
        var scope   = ScopeTenantAdmin();
        var handler = new GetForecastReportQueryHandler(repo);
        var query   = new GetForecastReportQuery(ValidPeriod, null, scope);

        repo.GetForecastAsync(scope, ValidPeriod, null, Arg.Any<CancellationToken>())
            .Returns([new ForecastRow(BuId, "BU Alpha", 2026, 1, 987_654L, 543_210L, 1_000_000L)]);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Rows[0].WeightedForecastCents.Should().Be(987_654L);
        result.Rows[0].RealizedCents.Should().Be(543_210L);
        result.Rows[0].GoalCents.Should().Be(1_000_000L);
    }

    // ──────────────────────────────────────────────────────────────
    // RBAC: scope repassado ao repositório
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "RBAC: handler repassa scope ao repositório (design §5.3)")]
    public async Task Handle_PassesScopeToRepository()
    {
        var repo    = Substitute.For<IReportingReadRepository>();
        var scope   = ScopeGestorBu();
        var handler = new GetForecastReportQueryHandler(repo);
        var buIds   = new[] { BuId };
        var query   = new GetForecastReportQuery(ValidPeriod, buIds, scope);

        repo.GetForecastAsync(scope, ValidPeriod, buIds, Arg.Any<CancellationToken>())
            .Returns([]);

        await handler.Handle(query, CancellationToken.None);

        await repo.Received(1).GetForecastAsync(scope, ValidPeriod, buIds, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Resultado: ForecastReportResponse retornado com tipo correto")]
    public async Task Handle_ReturnsForecastReportResponse()
    {
        var repo    = Substitute.For<IReportingReadRepository>();
        var scope   = ScopeTenantAdmin();
        var handler = new GetForecastReportQueryHandler(repo);
        var query   = new GetForecastReportQuery(ValidPeriod, null, scope);

        repo.GetForecastAsync(scope, ValidPeriod, null, Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().BeOfType<ForecastReportResponse>();
    }
}
