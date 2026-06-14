using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PartnerManagement.Application.Partners;
using PartnerManagement.Application.Partners.Queries;
using PartnerManagement.Application.Ports;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Domain.Partners.Repositories;
using PartnerManagement.Domain.Partners.ValueObjects;
using Xunit;

namespace PartnerManagement.Application.Tests.Partners.PropertyTests;

/// <summary>
/// PBT-01: Para qualquer conjunto de oportunidades gerado aleatoriamente, a soma projetada
/// exibida = soma das comissões das oportunidades abertas, e a consolidada = soma dos snapshots
/// das ganhas, sem dupla contagem nem omissão.
/// PBT-05: Editar pct_setup/pct_recorrente do parceiro mantém a comissão consolidada (snapshot)
/// inalterada após a edição.
/// Mapeia: TASK-14, PBT-01, PBT-05, Req 9, Req 2.3, design §13.
/// </summary>
public sealed class CommissionConservationPbt
{
    private static readonly DateTimeOffset From = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 12, 31, 23, 59, 59, TimeSpan.Zero);

    private static ICanonicalRoleProvider BuildRoleProvider()
    {
        ICanonicalRoleProvider rp = Substitute.For<ICanonicalRoleProvider>();
        rp.IsCanonical(Arg.Any<string>(), Arg.Any<Guid>()).Returns(true);
        return rp;
    }

    private static Partner BuildPartner(ICanonicalRoleProvider rp, CommissionDefaults? defaults = null) =>
        Partner.Create(Guid.NewGuid(), "Parceiro PBT", "Indicador",
            defaults ?? CommissionDefaults.Default, null, null, rp, Guid.NewGuid());

    private static GetPartnerCommissionViewHandler BuildHandler(
        Partner partner,
        IReadOnlyList<CommissionLine> lines)
    {
        IPartnerRepository repo = Substitute.For<IPartnerRepository>();
        repo.GetByIdAsync(partner.Id, Arg.Any<CancellationToken>()).Returns(partner);

        IPartnerCommissionReadPort port = Substitute.For<IPartnerCommissionReadPort>();
        port.GetCommissionLinesAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(),
            Arg.Any<DateTimeOffset>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(lines);

        IPartnerMetrics metrics = Substitute.For<IPartnerMetrics>();

        return new GetPartnerCommissionViewHandler(repo, port, metrics,
            NullLogger<GetPartnerCommissionViewHandler>.Instance);
    }

    // ========== PBT-01 — Conservação da soma de comissão ==========

    /// <summary>
    /// PBT-01: Dado qualquer conjunto de linhas abertas (não snapshot),
    /// a projetada é a soma delas e a consolidada é zero.
    /// </summary>
    [Property(MaxTest = 200, QuietOnSuccess = true)]
    public Property PBT01_OnlyOpenLines_ConsolidatedIsZero(NonNegativeInt count, PositiveInt seed)
    {
        // Limita entre 0 e 10 linhas
        int n = count.Get % 11;
        System.Random rng = new(seed.Get);
        long[] openValues = Enumerable.Range(0, n).Select(_ => (long)rng.Next(0, 100_001)).ToArray();

        ICanonicalRoleProvider rp = BuildRoleProvider();
        Partner partner = BuildPartner(rp);

        CommissionLine[] lines = openValues
            .Select(v => new CommissionLine(Guid.NewGuid(), v, IsSnapshot: false, From))
            .ToArray();

        GetPartnerCommissionViewHandler handler = BuildHandler(partner, lines);
        CommissionViewResult result = handler.Handle(
            new GetPartnerCommissionViewQuery(partner.Id, partner.TenantId, From, To),
            CancellationToken.None).GetAwaiter().GetResult();

        return (result.ConsolidatedCommissionCents == 0L
            && result.ProjectedCommissionCents == openValues.Sum()
            && !result.CommissionUnavailable).ToProperty();
    }

    /// <summary>
    /// PBT-01: Dado qualquer conjunto de snapshots (oportunidades ganhas),
    /// a consolidada é a soma deles e a projetada é zero.
    /// </summary>
    [Property(MaxTest = 200, QuietOnSuccess = true)]
    public Property PBT01_OnlySnapshotLines_ProjectedIsZero(NonNegativeInt count, PositiveInt seed)
    {
        int n = count.Get % 11;
        System.Random rng = new(seed.Get);
        long[] wonValues = Enumerable.Range(0, n).Select(_ => (long)rng.Next(0, 100_001)).ToArray();

        ICanonicalRoleProvider rp = BuildRoleProvider();
        Partner partner = BuildPartner(rp);

        CommissionLine[] lines = wonValues
            .Select(v => new CommissionLine(Guid.NewGuid(), v, IsSnapshot: true, From))
            .ToArray();

        GetPartnerCommissionViewHandler handler = BuildHandler(partner, lines);
        CommissionViewResult result = handler.Handle(
            new GetPartnerCommissionViewQuery(partner.Id, partner.TenantId, From, To),
            CancellationToken.None).GetAwaiter().GetResult();

        return (result.ProjectedCommissionCents == 0L
            && result.ConsolidatedCommissionCents == wonValues.Sum()
            && !result.CommissionUnavailable).ToProperty();
    }

    /// <summary>
    /// PBT-01: Dado um conjunto misto (abertas e snapshots), projetada = soma das abertas
    /// e consolidada = soma dos snapshots, sem dupla contagem.
    /// </summary>
    [Property(MaxTest = 200, QuietOnSuccess = true)]
    public Property PBT01_MixedLines_NoDoubleCounting(
        NonNegativeInt openCount, NonNegativeInt wonCount, PositiveInt seed)
    {
        int nOpen = openCount.Get % 6;
        int nWon = wonCount.Get % 6;
        System.Random rng = new(seed.Get);

        long[] openValues = Enumerable.Range(0, nOpen).Select(_ => (long)rng.Next(0, 100_001)).ToArray();
        long[] wonValues = Enumerable.Range(0, nWon).Select(_ => (long)rng.Next(0, 100_001)).ToArray();

        ICanonicalRoleProvider rp = BuildRoleProvider();
        Partner partner = BuildPartner(rp);

        CommissionLine[] lines =
            [.. openValues.Select(v => new CommissionLine(Guid.NewGuid(), v, IsSnapshot: false, From)),
             .. wonValues.Select(v => new CommissionLine(Guid.NewGuid(), v, IsSnapshot: true, From))];

        GetPartnerCommissionViewHandler handler = BuildHandler(partner, lines);
        CommissionViewResult result = handler.Handle(
            new GetPartnerCommissionViewQuery(partner.Id, partner.TenantId, From, To),
            CancellationToken.None).GetAwaiter().GetResult();

        return (result.ProjectedCommissionCents == openValues.Sum()
            && result.ConsolidatedCommissionCents == wonValues.Sum()
            && !result.CommissionUnavailable).ToProperty();
    }

    // ========== PBT-05 — Imutabilidade da comissão consolidada ==========

    /// <summary>
    /// PBT-05: Editar pct_setup/pct_recorrente mantém a consolidada (snapshot) inalterada.
    /// A comissão consolidada é snapshot: foi calculada no momento da venda e não muda
    /// com alterações de percentual do parceiro (design §13, DD-003).
    /// </summary>
    [Property(MaxTest = 200, QuietOnSuccess = true)]
    public Property PBT05_ConsolidatedImmutableAfterPercentualChange(
        NonNegativeInt pctBeforeCents, NonNegativeInt pctAfterCents, NonNegativeInt wonValueInt)
    {
        // Converte para percentual em [0.00..100.00] com 2 decimais
        decimal pctBefore = (pctBeforeCents.Get % 10001) / 100m;
        decimal pctAfter = (pctAfterCents.Get % 10001) / 100m;
        long wonValue = wonValueInt.Get % 100_001;

        ICanonicalRoleProvider rp = BuildRoleProvider();

        // Estado ANTES da edição
        CommissionDefaults defaultsBefore = CommissionDefaults.Create(
            Percentage.Create(pctBefore),
            Percentage.Create(pctBefore));
        Partner partnerBefore = BuildPartner(rp, defaultsBefore);

        CommissionLine[] snapshotLines = [new CommissionLine(Guid.NewGuid(), wonValue, IsSnapshot: true, From)];
        CommissionViewResult before = BuildHandler(partnerBefore, snapshotLines).Handle(
            new GetPartnerCommissionViewQuery(partnerBefore.Id, partnerBefore.TenantId, From, To),
            CancellationToken.None).GetAwaiter().GetResult();

        // Estado DEPOIS da edição (novo percentual — read model não muda)
        CommissionDefaults defaultsAfter = CommissionDefaults.Create(
            Percentage.Create(pctAfter),
            Percentage.Create(pctAfter));
        Partner partnerAfter = BuildPartner(rp, defaultsAfter);

        CommissionViewResult after = BuildHandler(partnerAfter, snapshotLines).Handle(
            new GetPartnerCommissionViewQuery(partnerAfter.Id, partnerAfter.TenantId, From, To),
            CancellationToken.None).GetAwaiter().GetResult();

        // A consolidada (snapshot) deve ser idêntica independente do percentual editado
        return (before.ConsolidatedCommissionCents == after.ConsolidatedCommissionCents
            && before.ConsolidatedCommissionCents == wonValue).ToProperty();
    }

    // ========== Testes determinísticos dos extremos ==========

    [Fact(DisplayName = "PBT-01: conjunto vazio — projetada e consolidada são zero")]
    [Trait("Category", "PBT")]
    public async Task PBT01_EmptySet_BothZero()
    {
        // Arrange
        ICanonicalRoleProvider rp = BuildRoleProvider();
        Partner partner = BuildPartner(rp);
        CommissionLine[] lines = [];

        GetPartnerCommissionViewHandler sut = BuildHandler(partner, lines);

        // Act
        CommissionViewResult result = await sut.Handle(
            new GetPartnerCommissionViewQuery(partner.Id, partner.TenantId, From, To),
            CancellationToken.None);

        // Assert
        result.ProjectedCommissionCents.Should().Be(0L);
        result.ConsolidatedCommissionCents.Should().Be(0L);
        result.CommissionUnavailable.Should().BeFalse();
    }

    [Fact(DisplayName = "PBT-01: conjunto com apenas snapshots — projetada é zero")]
    [Trait("Category", "PBT")]
    public async Task PBT01_OnlySnapshots_ProjectedIsZero()
    {
        // Arrange
        ICanonicalRoleProvider rp = BuildRoleProvider();
        Partner partner = BuildPartner(rp);

        CommissionLine[] lines =
        [
            new CommissionLine(Guid.NewGuid(), 30000L, IsSnapshot: true, From),
            new CommissionLine(Guid.NewGuid(), 20000L, IsSnapshot: true, From)
        ];

        GetPartnerCommissionViewHandler sut = BuildHandler(partner, lines);

        // Act
        CommissionViewResult result = await sut.Handle(
            new GetPartnerCommissionViewQuery(partner.Id, partner.TenantId, From, To),
            CancellationToken.None);

        // Assert
        result.ProjectedCommissionCents.Should().Be(0L);
        result.ConsolidatedCommissionCents.Should().Be(50000L);
    }

    [Fact(DisplayName = "PBT-05: consolidada imutável mesmo com percentual de 100%")]
    [Trait("Category", "PBT")]
    public async Task PBT05_ConsolidatedUnchangedAtMaxPercentual()
    {
        // Arrange
        ICanonicalRoleProvider rp = BuildRoleProvider();
        CommissionLine[] snapshotLines = [new CommissionLine(Guid.NewGuid(), 75000L, IsSnapshot: true, From)];

        Partner partnerLow = BuildPartner(rp, CommissionDefaults.Create(
            Percentage.Create(1.00m), Percentage.Create(1.00m)));
        Partner partnerHigh = BuildPartner(rp, CommissionDefaults.Create(
            Percentage.Create(100.00m), Percentage.Create(100.00m)));

        GetPartnerCommissionViewHandler sutLow = BuildHandler(partnerLow, snapshotLines);
        GetPartnerCommissionViewHandler sutHigh = BuildHandler(partnerHigh, snapshotLines);

        // Act
        CommissionViewResult resultLow = await sutLow.Handle(
            new GetPartnerCommissionViewQuery(partnerLow.Id, partnerLow.TenantId, From, To),
            CancellationToken.None);
        CommissionViewResult resultHigh = await sutHigh.Handle(
            new GetPartnerCommissionViewQuery(partnerHigh.Id, partnerHigh.TenantId, From, To),
            CancellationToken.None);

        // Assert — consolidada idêntica independente do percentual
        resultLow.ConsolidatedCommissionCents.Should().Be(75000L);
        resultHigh.ConsolidatedCommissionCents.Should().Be(75000L);
    }

    [Fact(DisplayName = "PBT-05: consolidada imutável quando percentual muda de 0% para 50%")]
    [Trait("Category", "PBT")]
    public async Task PBT05_ConsolidatedUnchangedFromZeroToFiftyPercent()
    {
        // Arrange
        ICanonicalRoleProvider rp = BuildRoleProvider();
        CommissionLine[] snapshotLines = [new CommissionLine(Guid.NewGuid(), 48000L, IsSnapshot: true, From)];

        Partner partnerZero = BuildPartner(rp, CommissionDefaults.Create(
            Percentage.Create(0.00m), Percentage.Create(0.00m)));
        Partner partnerFifty = BuildPartner(rp, CommissionDefaults.Create(
            Percentage.Create(50.00m), Percentage.Create(50.00m)));

        // Act
        CommissionViewResult resultZero = await BuildHandler(partnerZero, snapshotLines).Handle(
            new GetPartnerCommissionViewQuery(partnerZero.Id, partnerZero.TenantId, From, To),
            CancellationToken.None);
        CommissionViewResult resultFifty = await BuildHandler(partnerFifty, snapshotLines).Handle(
            new GetPartnerCommissionViewQuery(partnerFifty.Id, partnerFifty.TenantId, From, To),
            CancellationToken.None);

        // Assert
        resultZero.ConsolidatedCommissionCents.Should().Be(48000L);
        resultFifty.ConsolidatedCommissionCents.Should().Be(48000L);
    }
}
