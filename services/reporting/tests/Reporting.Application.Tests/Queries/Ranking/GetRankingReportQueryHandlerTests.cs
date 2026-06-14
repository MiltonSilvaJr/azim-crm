using FluentAssertions;
using NSubstitute;
using Reporting.Application.Policies;
using Reporting.Application.Ports;
using Reporting.Application.Queries.Ranking;
using Reporting.Contracts.ReadModels;
using Reporting.Contracts.Responses;
using Reporting.Domain.Enums;
using Reporting.Domain.ValueObjects;
using Xunit;

namespace Reporting.Application.Tests.Queries.Ranking;

/// <summary>
/// Testes do handler de ranking — TASK-08, ST-01.
/// Verifica: Vendedor só vê a própria linha, PiiMinimizationPolicy, ordenação, centavos.
/// Mapeia: Req 2, Req 2.2, Req 2.4, DD-008, RNF 4.
/// </summary>
public sealed class GetRankingReportQueryHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OwnerId1 = Guid.NewGuid();
    private static readonly Guid OwnerId2 = Guid.NewGuid();
    private static readonly Period ValidPeriod =
        Period.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));

    private static GetRankingReportQueryHandler MakeHandler(IReportingReadRepository repo) =>
        new(repo, new PiiMinimizationPolicy());

    // ──────────────────────────────────────────────────────────────
    // Vendedor: apenas a própria linha (Req 2.4)
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Vendedor: resposta contém exatamente 1 linha (a própria) — Req 2.4")]
    public async Task Handle_Vendedor_ReturnsOnlyOwnRow()
    {
        var repo  = Substitute.For<IReportingReadRepository>();
        var scope = ReportScope.Create(TenantId, ReportingRole.Vendedor, [], OwnerId1);
        var handler = MakeHandler(repo);
        var query   = new GetRankingReportQuery(ValidPeriod, null, scope);

        // Repositório retorna linhas de dois owners (a RLS garante tenant, mas o scope de Vendedor filtra)
        repo.GetRankingAsync(scope, ValidPeriod, null, Arg.Any<CancellationToken>())
            .Returns([
                new RankingRow(OwnerId1, "Alice", 5, 500_000L, 200_000L),
                new RankingRow(OwnerId2, "Bob",   3, 300_000L, 100_000L)
            ]);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Rows.Should().HaveCount(1,
            because: "Vendedor só enxerga a própria linha (Req 2.4)");
        result.Rows[0].OwnerId.Should().Be(OwnerId1);
    }

    // ──────────────────────────────────────────────────────────────
    // GestorBU: linhas filtradas pela BU
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "GestorBU: resposta contém as linhas do repositório (scope RBAC aplicado pelo repo)")]
    public async Task Handle_GestorBu_ReturnsRepositoryRows()
    {
        var repo  = Substitute.For<IReportingReadRepository>();
        var buId  = Guid.NewGuid();
        var scope = ReportScope.Create(TenantId, ReportingRole.GestorBU, [buId], null);
        var handler = MakeHandler(repo);
        var query   = new GetRankingReportQuery(ValidPeriod, [buId], scope);

        repo.GetRankingAsync(scope, ValidPeriod, Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns([
                new RankingRow(OwnerId1, "Alice", 5, 500_000L, 200_000L),
                new RankingRow(OwnerId2, "Bob",   3, 300_000L, 100_000L)
            ]);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Rows.Should().HaveCount(2);
    }

    // ──────────────────────────────────────────────────────────────
    // PiiMinimizationPolicy: display_name incluído para TenantAdmin
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "TenantAdmin: display_name presente na resposta (DD-008, PiiMinimizationPolicy)")]
    public async Task Handle_TenantAdmin_DisplayNamePresent()
    {
        var repo  = Substitute.For<IReportingReadRepository>();
        var scope = ReportScope.Create(TenantId, ReportingRole.TenantAdmin, [], null);
        var handler = MakeHandler(repo);
        var query   = new GetRankingReportQuery(ValidPeriod, null, scope);

        repo.GetRankingAsync(scope, ValidPeriod, null, Arg.Any<CancellationToken>())
            .Returns([new RankingRow(OwnerId1, "Alice Souza", 5, 500_000L, 200_000L)]);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Rows[0].DisplayName.Should().Be("Alice Souza");
    }

    // ──────────────────────────────────────────────────────────────
    // Ordenação: wonValueCents desc (Req 2.2)
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Ordenação: linhas ordenadas por wonValueCents desc (Req 2.2)")]
    public async Task Handle_OrdersByWonValueCentsDescending()
    {
        var repo  = Substitute.For<IReportingReadRepository>();
        var scope = ReportScope.Create(TenantId, ReportingRole.TenantAdmin, [], null);
        var handler = MakeHandler(repo);
        var query   = new GetRankingReportQuery(ValidPeriod, null, scope);

        repo.GetRankingAsync(scope, ValidPeriod, null, Arg.Any<CancellationToken>())
            .Returns([
                new RankingRow(OwnerId2, "Bob",   3, 300_000L, 100_000L),
                new RankingRow(OwnerId1, "Alice", 5, 500_000L, 200_000L)
            ]);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Rows[0].OwnerId.Should().Be(OwnerId1,
            because: "Alice tem maior wonValueCents (500k > 300k) — deve ser a primeira linha");
        result.Rows[1].OwnerId.Should().Be(OwnerId2);
    }

    // ──────────────────────────────────────────────────────────────
    // Centavos preservados
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Centavos: wonValueCents e pipelineForecastCents preservados sem conversão (DD-007)")]
    public async Task Handle_PreservesCentsWithoutConversion()
    {
        var repo  = Substitute.For<IReportingReadRepository>();
        var scope = ReportScope.Create(TenantId, ReportingRole.TenantAdmin, [], null);
        var handler = MakeHandler(repo);
        var query   = new GetRankingReportQuery(ValidPeriod, null, scope);

        repo.GetRankingAsync(scope, ValidPeriod, null, Arg.Any<CancellationToken>())
            .Returns([new RankingRow(OwnerId1, "Alice", 7, 9_876_543L, 4_321_098L)]);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Rows[0].WonValueCents.Should().Be(9_876_543L);
        result.Rows[0].PipelineForecastCents.Should().Be(4_321_098L);
    }

    [Fact(DisplayName = "Resultado: RankingReportResponse retornado com tipo correto")]
    public async Task Handle_ReturnsRankingReportResponse()
    {
        var repo  = Substitute.For<IReportingReadRepository>();
        var scope = ReportScope.Create(TenantId, ReportingRole.TenantAdmin, [], null);
        var handler = MakeHandler(repo);
        var query   = new GetRankingReportQuery(ValidPeriod, null, scope);

        repo.GetRankingAsync(scope, ValidPeriod, null, Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await handler.Handle(query, CancellationToken.None);
        result.Should().BeOfType<RankingReportResponse>();
    }
}
