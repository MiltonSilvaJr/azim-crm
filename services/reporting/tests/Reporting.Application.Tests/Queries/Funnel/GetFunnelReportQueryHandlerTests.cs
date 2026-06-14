using FluentAssertions;
using NSubstitute;
using Reporting.Application.Ports;
using Reporting.Application.Queries.Funnel;
using Reporting.Contracts.ReadModels;
using Reporting.Contracts.Responses;
using Reporting.Domain.Enums;
using Reporting.Domain.ValueObjects;
using Xunit;

namespace Reporting.Application.Tests.Queries.Funnel;

/// <summary>
/// Testes do handler de funil — TASK-06, ST-01.
/// Verifica: escopo por papel, preservação de centavos, validação de período.
/// Mapeia: Req 1, design §5.2, §5.3, DD-007.
/// </summary>
public sealed class GetFunnelReportQueryHandlerTests
{
    private static readonly Guid TenantId  = Guid.NewGuid();
    private static readonly Guid OwnerId   = Guid.NewGuid();
    private static readonly Guid BuId1     = Guid.NewGuid();
    private static readonly Period ValidPeriod =
        Period.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));

    private static ReportScope ScopeVendedor() =>
        ReportScope.Create(TenantId, ReportingRole.Vendedor, [], OwnerId);

    private static ReportScope ScopeGestorBu() =>
        ReportScope.Create(TenantId, ReportingRole.GestorBU, [BuId1], null);

    private static ReportScope ScopeTenantAdmin() =>
        ReportScope.Create(TenantId, ReportingRole.TenantAdmin, [], null);

    private static FunnelRow MakeRow(Guid stageId, long totalCents, long forecastCents) =>
        new(stageId, "Estágio", "open", 1, totalCents, forecastCents);

    // ──────────────────────────────────────────────────────────────
    // Escopo RBAC
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Vendedor: handler chama repositório com escopo Vendedor")]
    public async Task Handle_VendedorScope_PassesScopeToRepository()
    {
        var repo    = Substitute.For<IReportingReadRepository>();
        var scope   = ScopeVendedor();
        var handler = new GetFunnelReportQueryHandler(repo);
        var query   = new GetFunnelReportQuery(ValidPeriod, null, scope);

        repo.GetFunnelAsync(scope, ValidPeriod, null, Arg.Any<CancellationToken>())
            .Returns([]);

        await handler.Handle(query, CancellationToken.None);

        await repo.Received(1).GetFunnelAsync(scope, ValidPeriod, null, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "GestorBU: handler chama repositório com escopo GestorBU")]
    public async Task Handle_GestorBuScope_PassesScopeToRepository()
    {
        var repo    = Substitute.For<IReportingReadRepository>();
        var scope   = ScopeGestorBu();
        var handler = new GetFunnelReportQueryHandler(repo);
        var buIds   = new[] { BuId1 };
        var query   = new GetFunnelReportQuery(ValidPeriod, buIds, scope);

        repo.GetFunnelAsync(scope, ValidPeriod, buIds, Arg.Any<CancellationToken>())
            .Returns([]);

        await handler.Handle(query, CancellationToken.None);

        await repo.Received(1).GetFunnelAsync(scope, ValidPeriod, buIds, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "TenantAdmin: handler chama repositório com escopo TenantAdmin")]
    public async Task Handle_TenantAdminScope_PassesScopeToRepository()
    {
        var repo    = Substitute.For<IReportingReadRepository>();
        var scope   = ScopeTenantAdmin();
        var handler = new GetFunnelReportQueryHandler(repo);
        var query   = new GetFunnelReportQuery(ValidPeriod, null, scope);

        repo.GetFunnelAsync(scope, ValidPeriod, null, Arg.Any<CancellationToken>())
            .Returns([]);

        await handler.Handle(query, CancellationToken.None);

        await repo.Received(1).GetFunnelAsync(scope, ValidPeriod, null, Arg.Any<CancellationToken>());
    }

    // ──────────────────────────────────────────────────────────────
    // Preservação de centavos
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Centavos: handler preserva totalCents e weightedForecastCents sem conversão")]
    public async Task Handle_PreservesCentsWithoutConversion()
    {
        var repo    = Substitute.For<IReportingReadRepository>();
        var scope   = ScopeTenantAdmin();
        var handler = new GetFunnelReportQueryHandler(repo);
        var query   = new GetFunnelReportQuery(ValidPeriod, null, scope);
        var stageId = Guid.NewGuid();

        repo.GetFunnelAsync(scope, ValidPeriod, null, Arg.Any<CancellationToken>())
            .Returns([MakeRow(stageId, 4_500_000L, 1_800_000L)]);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Stages.Should().HaveCount(1);
        result.Stages[0].TotalCents.Should().Be(4_500_000L);
        result.Stages[0].WeightedForecastCents.Should().Be(1_800_000L);
    }

    // ──────────────────────────────────────────────────────────────
    // Resposta mapeada corretamente
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Resposta: FunnelReportResponse contém as linhas do repositório")]
    public async Task Handle_ReturnsAllRowsFromRepository()
    {
        var repo    = Substitute.For<IReportingReadRepository>();
        var scope   = ScopeTenantAdmin();
        var handler = new GetFunnelReportQueryHandler(repo);
        var query   = new GetFunnelReportQuery(ValidPeriod, null, scope);

        var rows = new List<FunnelRow>
        {
            MakeRow(Guid.NewGuid(), 100_000L, 50_000L),
            MakeRow(Guid.NewGuid(), 200_000L, 100_000L)
        };
        repo.GetFunnelAsync(scope, ValidPeriod, null, Arg.Any<CancellationToken>())
            .Returns(rows);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().BeOfType<FunnelReportResponse>();
        result.Stages.Should().HaveCount(2);
    }
}
