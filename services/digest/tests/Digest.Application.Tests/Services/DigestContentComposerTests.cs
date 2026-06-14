using Digest.Application.Models;
using Digest.Application.Services;
using Digest.Application.Tests.Stubs;
using Digest.Domain.Enums;
using Digest.Domain.Policies;
using Digest.Domain.ValueObjects;
using FsCheck;
using FsCheck.Xunit;
using NodaTime;
using Xunit;

namespace Digest.Application.Tests.Services;

/// <summary>
/// Testes unitários e PBT de <see cref="DigestContentComposer"/> (TASK-11).
/// PBT-06: azimute presente ⟺ dia local = segunda-feira E papel é de gestão.
/// </summary>
public sealed class DigestContentComposerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    // Datas fixas para testes nominais
    private static readonly DigestDate Monday = new(new LocalDate(2026, 6, 8));   // segunda
    private static readonly DigestDate Tuesday = new(new LocalDate(2026, 6, 9));  // terça
    private static readonly DigestDate Wednesday = new(new LocalDate(2026, 6, 10)); // quarta

    private static DigestContentComposer BuildSut(
        InMemoryActivityReadPort? actPort = null,
        InMemoryOpportunityReadPort? oppPort = null,
        InMemoryForecastReadPort? forecastPort = null)
    {
        var opp = oppPort ?? new InMemoryOpportunityReadPort();
        var forecast = forecastPort ?? new InMemoryForecastReadPort();
        var azimute = new AzimuteSectionBuilder(opp, forecast);
        return new DigestContentComposer(
            actPort ?? new InMemoryActivityReadPort(),
            opp,
            azimute);
    }

    private static RecipientCandidate MakeCandidate(
        RecipientPapel papel,
        bool receivePendencias,
        bool receiveAzimute,
        Guid? userId = null) =>
        new(userId ?? UserId, TenantId, papel,
            new RecipientSelectionResult(receivePendencias, receiveAzimute));

    // ------------------------------------------------------------------
    // Testes nominais
    // ------------------------------------------------------------------

    [Fact(DisplayName = "TAdmin na segunda-feira recebe bloco de azimute (PBT-06 nominal)")]
    public async Task TAdmin_Monday_ReceivesAzimute()
    {
        var oppPort = new InMemoryOpportunityReadPort();
        oppPort.SetPipeline(new[]
        {
            new OpportunityItem(Guid.NewGuid(), UserId, "Opp A", null, new MoneyCents(100_000_00)),
        });
        var candidate = MakeCandidate(RecipientPapel.TAdmin,
            receivePendencias: false, receiveAzimute: true);
        var sut = BuildSut(oppPort: oppPort);

        var content = await sut.ComposeAsync(TenantId, candidate, Monday);

        Assert.True(content.HasContent()); // azimute_pipeline com item -> não vazio
        Assert.Contains(content.Sections, s => s.Key == AzimuteSectionBuilder.AzimutePipelineKey);
    }

    [Fact(DisplayName = "TAdmin na terça-feira não recebe azimute")]
    public async Task TAdmin_Tuesday_NoAzimute()
    {
        // Policy na terça para TAdmin sem pendências → receiveAzimute = false
        var candidate = MakeCandidate(RecipientPapel.TAdmin,
            receivePendencias: false, receiveAzimute: false);
        var sut = BuildSut();

        var content = await sut.ComposeAsync(TenantId, candidate, Tuesday);

        Assert.DoesNotContain(content.Sections, s => s.Key == AzimuteSectionBuilder.AzimutePipelineKey);
        Assert.DoesNotContain(content.Sections, s => s.Key == AzimuteSectionBuilder.AzuimuteMetasKey);
    }

    [Fact(DisplayName = "Vendedor com pendências recebe blocos de pendências — sem azimute")]
    public async Task Vendedor_WithPendencias_NoPendenciasKey()
    {
        var actPort = new InMemoryActivityReadPort();
        actPort.SetOverdue(new[]
        {
            new ActivityItem(Guid.NewGuid(), UserId, new DateOnly(2026, 6, 7), "Tarefa vencida"),
        });

        var candidate = MakeCandidate(RecipientPapel.Vendedor,
            receivePendencias: true, receiveAzimute: false);
        var sut = BuildSut(actPort);

        var content = await sut.ComposeAsync(TenantId, candidate, Tuesday);

        Assert.True(content.HasContent());
        Assert.Contains(content.Sections, s => s.Key == DigestContentComposer.OverdueActivitiesKey);
        Assert.DoesNotContain(content.Sections, s => s.Key == AzimuteSectionBuilder.AzimutePipelineKey);
    }

    [Fact(DisplayName = "GestorBU com pendências e azimute na segunda recebe ambos os blocos")]
    public async Task GestorBU_Monday_ReceivesBothBlocks()
    {
        var actPort = new InMemoryActivityReadPort();
        actPort.SetOverdue(new[]
        {
            new ActivityItem(Guid.NewGuid(), UserId, new DateOnly(2026, 6, 7), "Tarefa"),
        });

        var candidate = MakeCandidate(RecipientPapel.GestorBU,
            receivePendencias: true, receiveAzimute: true);
        var sut = BuildSut(actPort);

        var content = await sut.ComposeAsync(TenantId, candidate, Monday);

        Assert.Contains(content.Sections, s => s.Key == DigestContentComposer.OverdueActivitiesKey);
        Assert.Contains(content.Sections, s => s.Key == AzimuteSectionBuilder.AzimutePipelineKey);
    }

    [Fact(DisplayName = "Candidato sem nenhum bloco → HasContent retorna false")]
    public async Task NoContent_HasContent_ReturnsFalse()
    {
        // Candidato que não recebe nada (ShouldReceiveAny = false)
        var candidate = MakeCandidate(RecipientPapel.Vendedor,
            receivePendencias: false, receiveAzimute: false);
        var sut = BuildSut();

        var content = await sut.ComposeAsync(TenantId, candidate, Tuesday);

        Assert.False(content.HasContent());
    }

    // ------------------------------------------------------------------
    // PBT-06: Azimute presente ⟺ dia local = segunda E papel gestão
    // ------------------------------------------------------------------

    [Property(DisplayName = "PBT-06: azimute presente se e somente se segunda-feira e papel gestão")]
    public Property PBT06_AzimutePresent_IffMondayAndGestao()
    {
        // Geradores
        var dayGen = Gen.Elements(
            IsoDayOfWeek.Monday,
            IsoDayOfWeek.Tuesday,
            IsoDayOfWeek.Wednesday,
            IsoDayOfWeek.Thursday,
            IsoDayOfWeek.Friday);

        var papelGen = Gen.Elements(
            RecipientPapel.Vendedor,
            RecipientPapel.GestorBU,
            RecipientPapel.TAdmin,
            RecipientPapel.Viewer);

        return Prop.ForAll(
            Arb.From(dayGen),
            Arb.From(papelGen),
            (isoDayOfWeek, papel) =>
            {
                // Mapeia IsoDayOfWeek para uma data concreta
                // Segunda (1) = 2026-06-08; Terça (2) = 2026-06-09; ... Sexta (5) = 2026-06-12
                var baseMonday = new LocalDate(2026, 6, 8);
                var offset = (int)isoDayOfWeek - (int)IsoDayOfWeek.Monday;
                var localDate = baseMonday.PlusDays(offset);
                var digestDate = new DigestDate(localDate);

                var isMonday = isoDayOfWeek == IsoDayOfWeek.Monday;
                var isGestao = papel is RecipientPapel.GestorBU or RecipientPapel.TAdmin;
                var receiveAzimute = isMonday && isGestao;

                // Candidate com resultado correto da policy
                var candidate = new RecipientCandidate(
                    UserId, TenantId, papel,
                    new RecipientSelectionResult(
                        ShouldReceivePendencias: false,
                        ShouldReceiveAzimute: receiveAzimute));

                var sut = BuildSut();
                var content = sut.ComposeAsync(TenantId, candidate, digestDate).GetAwaiter().GetResult();

                var hasAzimuteSection = content.Sections
                    .Any(s => s.Key == AzimuteSectionBuilder.AzimutePipelineKey);

                // PBT-06: azimute ⟺ segunda + gestão
                return (hasAzimuteSection == receiveAzimute).ToProperty()
                    .Label($"day={isoDayOfWeek}, papel={papel}, receiveAzimute={receiveAzimute}, hasSection={hasAzimuteSection}");
            });
    }
}
