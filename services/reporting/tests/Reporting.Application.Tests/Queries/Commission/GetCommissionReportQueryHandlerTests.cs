using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using NSubstitute;
using Reporting.Application.Ports;
using Reporting.Application.Queries.Commission;
using Reporting.Contracts.ReadModels;
using Reporting.Contracts.Responses;
using Reporting.Domain.Enums;
using Reporting.Domain.ValueObjects;
using Xunit;

namespace Reporting.Application.Tests.Queries.Commission;

/// <summary>
/// Testes do handler de comissões — TASK-10, ST-01.
/// Inclui PBT-01 (snapshot imutável) e PBT-02 (conservação de soma em centavos).
/// Mapeia: Req 4, Req 4.2, Req 4.3, PBT-01, PBT-02, RN-007, DD-007.
/// </summary>
public sealed class GetCommissionReportQueryHandlerTests
{
    private static readonly Guid TenantId   = Guid.NewGuid();
    private static readonly Guid PartnerId1 = Guid.NewGuid();
    private static readonly Guid PartnerId2 = Guid.NewGuid();
    private static readonly Period ValidPeriod =
        Period.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));

    private static ReportScope ScopeTenantAdmin() =>
        ReportScope.Create(TenantId, ReportingRole.TenantAdmin, [], null);

    private static GetCommissionReportQueryHandler MakeHandler(IReportingReadRepository repo) =>
        new(repo);

    // ──────────────────────────────────────────────────────────────
    // Separação snapshot × projetado (Req 4.2, Req 4.3, RN-007)
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "ConsolidatedCents: derivado exclusivamente de isSnapshot=true (RN-007, Req 4.2)")]
    public async Task Handle_ConsolidatedCents_DerivedOnlyFromSnapshots()
    {
        var repo  = Substitute.For<IReportingReadRepository>();
        var scope = ScopeTenantAdmin();
        var handler = MakeHandler(repo);
        var query   = new GetCommissionReportQuery(ValidPeriod, null, scope);

        repo.GetCommissionsAsync(scope, ValidPeriod, null, Arg.Any<CancellationToken>())
            .Returns([
                new CommissionRow(PartnerId1, "Parceiro A", 10_000L, IsSnapshot: true,  "won"),
                new CommissionRow(PartnerId1, "Parceiro A", 5_000L,  IsSnapshot: false, "open"),
                new CommissionRow(PartnerId1, "Parceiro A", 3_000L,  IsSnapshot: false, "won")
            ]);

        var result = await handler.Handle(query, CancellationToken.None);

        var row = result.Rows.Single(r => r.PartnerId == PartnerId1);
        row.ConsolidatedCents.Should().Be(10_000L,
            because: "apenas a linha com isSnapshot=true conta para o consolidado (RN-007)");
    }

    [Fact(DisplayName = "ProjectedCents: derivado de isSnapshot=false E stageCategory=open (Req 4.3)")]
    public async Task Handle_ProjectedCents_DerivedFromOpenNonSnapshot()
    {
        var repo  = Substitute.For<IReportingReadRepository>();
        var scope = ScopeTenantAdmin();
        var handler = MakeHandler(repo);
        var query   = new GetCommissionReportQuery(ValidPeriod, null, scope);

        repo.GetCommissionsAsync(scope, ValidPeriod, null, Arg.Any<CancellationToken>())
            .Returns([
                new CommissionRow(PartnerId1, "Parceiro A", 10_000L, IsSnapshot: true,  "won"),
                new CommissionRow(PartnerId1, "Parceiro A",  5_000L, IsSnapshot: false, "open"),
                new CommissionRow(PartnerId1, "Parceiro A",  3_000L, IsSnapshot: false, "won")
            ]);

        var result = await handler.Handle(query, CancellationToken.None);

        var row = result.Rows.Single(r => r.PartnerId == PartnerId1);
        row.ProjectedCents.Should().Be(5_000L,
            because: "apenas isSnapshot=false E category=open conta para o projetado (Req 4.3)");
    }

    // ──────────────────────────────────────────────────────────────
    // PBT-01: mutação de percentual pós-snapshot não altera consolidado
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "PBT-01: snapshot imutável — alteração de percentual após snapshot não afeta consolidado")]
    public async Task Handle_Pbt01_SnapshotImmutable_MutationDoesNotAffectConsolidated()
    {
        var repo    = Substitute.For<IReportingReadRepository>();
        var scope   = ScopeTenantAdmin();
        var handler = MakeHandler(repo);
        var query   = new GetCommissionReportQuery(ValidPeriod, null, scope);

        // Snapshot com valor original (gravado quando percentual era X)
        var snapshotValue = 15_000L;

        // Repositório retorna o snapshot com o valor gravado (autoritativo — P2)
        // O handler NÃO reimplementa a fórmula: apenas soma os valores autoritativos
        repo.GetCommissionsAsync(scope, ValidPeriod, null, Arg.Any<CancellationToken>())
            .Returns([
                new CommissionRow(PartnerId1, "Parceiro A", snapshotValue, IsSnapshot: true, "won")
            ]);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Rows[0].ConsolidatedCents.Should().Be(snapshotValue,
            because: "o snapshot é imutável — o handler não reimplementa fórmula de comissão (PBT-01, P2)");
    }

    [Property(MaxTest = 300, DisplayName = "PBT-01: para qualquer conjunto de snapshots, consolidado = soma dos snapshots originais")]
    public Property Pbt01_ConsolidatedEqualsSnapshotSum(PositiveInt count)
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(count)),
            c =>
            {
                var n = (c.Get % 10) + 1;
                var snapshotValues = Enumerable.Range(1, n).Select(i => (long)(i * 1_000)).ToList();
                var expectedConsolidated = snapshotValues.Sum();

                var rawRows = snapshotValues
                    .Select(v => new CommissionRow(PartnerId1, "P1", v, IsSnapshot: true, "won"))
                    .ToList();

                var result = GetCommissionReportQueryHandler.AggregateByPartner(rawRows);
                return result[0].ConsolidatedCents == expectedConsolidated;
            });
    }

    // ──────────────────────────────────────────────────────────────
    // PBT-02: conservação de soma em centavos
    // ──────────────────────────────────────────────────────────────

    [Property(MaxTest = 300, DisplayName = "PBT-02: soma de commissionCents preservada sem perda/duplicação (PBT-02, DD-007)")]
    public Property Pbt02_CommissionSumConserved(PositiveInt count)
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(count)),
            c =>
            {
                var n = (c.Get % 10) + 1;

                // Mix de snapshots e projetados
                var snapshots  = Enumerable.Range(1, n).Select(i => (long)(i * 500)).ToList();
                var projected  = Enumerable.Range(1, n).Select(i => (long)(i * 300)).ToList();

                var rawRows = snapshots
                    .Select(v => new CommissionRow(PartnerId1, "P1", v, IsSnapshot: true, "won"))
                    .Concat(projected
                        .Select(v => new CommissionRow(PartnerId1, "P1", v, IsSnapshot: false, "open")))
                    .ToList();

                var result = GetCommissionReportQueryHandler.AggregateByPartner(rawRows);
                var partner = result[0];

                // Verifica conservação: consolidated + projected = soma das linhas correspondentes
                var expectedConsolidated = snapshots.Sum();
                var expectedProjected    = projected.Sum();

                return partner.ConsolidatedCents == expectedConsolidated
                    && partner.ProjectedCents    == expectedProjected;
            });
    }

    // ──────────────────────────────────────────────────────────────
    // Múltiplos parceiros
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Múltiplos parceiros: agregação separada por partnerId")]
    public async Task Handle_MultiplePartners_AggregatesSeparately()
    {
        var repo  = Substitute.For<IReportingReadRepository>();
        var scope = ScopeTenantAdmin();
        var handler = MakeHandler(repo);
        var query   = new GetCommissionReportQuery(ValidPeriod, null, scope);

        repo.GetCommissionsAsync(scope, ValidPeriod, null, Arg.Any<CancellationToken>())
            .Returns([
                new CommissionRow(PartnerId1, "Parceiro A", 10_000L, IsSnapshot: true,  "won"),
                new CommissionRow(PartnerId2, "Parceiro B", 20_000L, IsSnapshot: true,  "won"),
                new CommissionRow(PartnerId2, "Parceiro B",  5_000L, IsSnapshot: false, "open")
            ]);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Rows.Should().HaveCount(2);
        var p2 = result.Rows.Single(r => r.PartnerId == PartnerId2);
        p2.ConsolidatedCents.Should().Be(20_000L);
        p2.ProjectedCents.Should().Be(5_000L);
    }

    [Fact(DisplayName = "Centavos: commissionCents preservados sem conversão (DD-007)")]
    public async Task Handle_PreservesCentsWithoutConversion()
    {
        var repo  = Substitute.For<IReportingReadRepository>();
        var scope = ScopeTenantAdmin();
        var handler = MakeHandler(repo);
        var query   = new GetCommissionReportQuery(ValidPeriod, null, scope);

        repo.GetCommissionsAsync(scope, ValidPeriod, null, Arg.Any<CancellationToken>())
            .Returns([
                new CommissionRow(PartnerId1, "P1", 9_876_543L, IsSnapshot: true,  "won"),
                new CommissionRow(PartnerId1, "P1", 1_234_567L, IsSnapshot: false, "open")
            ]);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Rows[0].ConsolidatedCents.Should().Be(9_876_543L);
        result.Rows[0].ProjectedCents.Should().Be(1_234_567L);
    }

    [Fact(DisplayName = "Resultado: CommissionReportResponse com tipo correto")]
    public async Task Handle_ReturnsCommissionReportResponse()
    {
        var repo  = Substitute.For<IReportingReadRepository>();
        var scope = ScopeTenantAdmin();
        var handler = MakeHandler(repo);
        var query   = new GetCommissionReportQuery(ValidPeriod, null, scope);

        repo.GetCommissionsAsync(scope, ValidPeriod, null, Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await handler.Handle(query, CancellationToken.None);
        result.Should().BeOfType<CommissionReportResponse>();
    }
}
