using Digest.Application.Models;
using Digest.Application.Services;
using Digest.Application.Tests.Stubs;
using Digest.Domain.ValueObjects;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Digest.Application.Tests.Services;

/// <summary>
/// Testes unitários e PBT de <see cref="AzimuteSectionBuilder"/> (TASK-11).
/// PBT-04: bloco de metas presente ⟺ existe meta no período; sem erro/placeholder quando null.
/// </summary>
public sealed class AzimuteSectionBuilderTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateOnly ReferenceDate = new(2026, 6, 8); // segunda-feira

    private static AzimuteSectionBuilder BuildSut(
        InMemoryOpportunityReadPort? oppPort = null,
        InMemoryForecastReadPort? forecastPort = null)
    {
        return new AzimuteSectionBuilder(
            oppPort ?? new InMemoryOpportunityReadPort(),
            forecastPort ?? new InMemoryForecastReadPort());
    }

    // ------------------------------------------------------------------
    // Testes unitários nominais
    // ------------------------------------------------------------------

    [Fact(DisplayName = "Bloco azimute_pipeline sempre presente mesmo sem oportunidades")]
    public async Task PipelineSection_AlwaysPresent()
    {
        var sut = BuildSut();

        var result = await sut.BuildAsync(TenantId, ReferenceDate);

        Assert.Contains(result, s => s.Key == AzimuteSectionBuilder.AzimutePipelineKey);
    }

    [Fact(DisplayName = "Bloco azimute_metas presente quando há meta configurada")]
    public async Task MetasSection_Present_WhenForecastExists()
    {
        var forecastPort = new InMemoryForecastReadPort();
        forecastPort.SetBlock(new ForecastBlock(
            new MoneyCents(100_000_00),
            new MoneyCents(5_000_00),
            new MoneyCents(50_000_00),
            new MoneyCents(80_000_00),
            WonCount: 3,
            LostCount: 1));
        var sut = BuildSut(forecastPort: forecastPort);

        var result = await sut.BuildAsync(TenantId, ReferenceDate);

        Assert.Contains(result, s => s.Key == AzimuteSectionBuilder.AzuimuteMetasKey);
    }

    [Fact(DisplayName = "Bloco azimute_metas completamente omitido quando IForecastReadPort retorna null (Req 5.4, RN-018)")]
    public async Task MetasSection_CompletelyOmitted_WhenForecastNull()
    {
        var forecastPort = new InMemoryForecastReadPort();
        forecastPort.SetBlock(null); // ausência de meta

        var sut = BuildSut(forecastPort: forecastPort);

        var result = await sut.BuildAsync(TenantId, ReferenceDate);

        Assert.DoesNotContain(result, s => s.Key == AzimuteSectionBuilder.AzuimuteMetasKey);
        // Deve ainda ter o pipeline
        Assert.Contains(result, s => s.Key == AzimuteSectionBuilder.AzimutePipelineKey);
    }

    [Fact(DisplayName = "Sem exceção quando forecast é null — sem placeholder — sem erro (PBT-04 nominal)")]
    public async Task NoException_WhenForecastNull()
    {
        var forecastPort = new InMemoryForecastReadPort();
        forecastPort.SetBlock(null);
        var sut = BuildSut(forecastPort: forecastPort);

        // Não deve lançar exceção
        var exception = await Record.ExceptionAsync(() =>
            sut.BuildAsync(TenantId, ReferenceDate));

        Assert.Null(exception);
    }

    [Fact(DisplayName = "Pipeline items usam MoneyCents (sem double/float em cálculos — DD-010)")]
    public async Task PipelineItems_UseMoneyCents()
    {
        var oppPort = new InMemoryOpportunityReadPort();
        oppPort.SetPipeline(new[]
        {
            new OpportunityItem(Guid.NewGuid(), Guid.NewGuid(), "Opp A", null, new MoneyCents(150_000_00)),
        });
        var sut = BuildSut(oppPort);

        var result = await sut.BuildAsync(TenantId, ReferenceDate);
        var pipelineSection = result.First(s => s.Key == AzimuteSectionBuilder.AzimutePipelineKey);

        Assert.Single(pipelineSection.Items);
        Assert.Contains("150000", pipelineSection.Items[0]); // valor em centavos convertido
    }

    // ------------------------------------------------------------------
    // PBT-04: Omissão graciosa do bloco de metas — ausência de meta nunca causa erro/placeholder
    // ------------------------------------------------------------------

    [Property(DisplayName = "PBT-04: bloco de metas omitido sem erro/placeholder quando forecast é null")]
    public Property PBT04_MetasOmitted_WhenForecastNull()
    {
        return Prop.ForAll(
            Arb.From(Gen.Elements(true, false)), // hasForecast: arbitrário
            hasForecast =>
            {
                var forecastPort = new InMemoryForecastReadPort();
                ForecastBlock? block = hasForecast
                    ? new ForecastBlock(
                        new MoneyCents(Gen.Sample(1, 1, Gen.Choose(0, 10_000_000))[0]),
                        new MoneyCents(Gen.Sample(1, 1, Gen.Choose(0, 1_000_000))[0]),
                        new MoneyCents(Gen.Sample(1, 1, Gen.Choose(0, 5_000_000))[0]),
                        new MoneyCents(Gen.Sample(1, 1, Gen.Choose(0, 8_000_000))[0]),
                        WonCount: 0,
                        LostCount: 0)
                    : null;

                forecastPort.SetBlock(block);
                var sut = BuildSut(forecastPort: forecastPort);

                // Nunca lança exceção
                var sections = sut.BuildAsync(TenantId, ReferenceDate).GetAwaiter().GetResult();

                if (hasForecast)
                {
                    // Meta presente → bloco deve existir
                    return sections.Any(s => s.Key == AzimuteSectionBuilder.AzuimuteMetasKey).ToProperty();
                }
                else
                {
                    // Meta ausente → bloco NÃO deve existir (sem placeholder)
                    return sections.All(s => s.Key != AzimuteSectionBuilder.AzuimuteMetasKey).ToProperty();
                }
            });
    }
}
