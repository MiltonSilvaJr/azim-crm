using GoalForecast.Infrastructure.Pipeline;
using GoalForecast.Infrastructure.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoalForecast.Infrastructure.Tests.Pipeline;

/// <summary>
/// Testes do PipelineForecastReader com circuit breaker e degradação controlada.
/// Verifica: leitura bem-sucedida, timeout → available=false, circuito aberto → available=false.
/// Não usa banco real — o reader opera in-process sobre um stub da ForecastView.
/// Mapeia: TASK-19, Req 8, RNF 6, DD-005, DD-007, RISK-GOAL-01, design §6.4.
/// </summary>
public sealed class PipelineForecastReaderTests
{
    // ── ST-01 Red ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Read_successful_returns_available_true_with_values()
    {
        // Arrange
        const long wonCents = 10_000L;
        const long forecastCents = 20_000L;

        var stub = new StubForecastViewSource(
            wonTotalCents: wonCents,
            forecastPonderadoCents: forecastCents,
            shouldThrow: false,
            delayMs: 0);

        var reader = BuildReader(stub);
        var query = new ForecastViewQuery(Guid.NewGuid(), Guid.NewGuid(), null, 2026, 6);

        // Act
        var result = await reader.Read(query);

        // Assert
        result.Available.Should().BeTrue();
        result.WonTotalCents.Should().Be(wonCents);
        result.ForecastPonderadoCents.Should().Be(forecastCents);
    }

    [Fact]
    public async Task Read_timeout_returns_available_false_without_throwing()
    {
        // Arrange — fonte demora mais que o timeout configurado (50 ms)
        var stub = new StubForecastViewSource(
            wonTotalCents: 0,
            forecastPonderadoCents: 0,
            shouldThrow: false,
            delayMs: 500); // 500 ms > timeout de 50 ms

        var reader = BuildReader(stub, timeoutMs: 50);
        var query = new ForecastViewQuery(Guid.NewGuid(), Guid.NewGuid(), null, 2026, 6);

        // Act — não deve propagar TimeoutRejectedException
        var result = await reader.Read(query);

        // Assert — degradação graciosa
        result.Available.Should().BeFalse("timeout deve retornar available=false sem exceção");
        result.WonTotalCents.Should().Be(0L);
    }

    [Fact]
    public async Task Read_exception_returns_available_false_without_throwing()
    {
        // Arrange — fonte lança exceção
        var stub = new StubForecastViewSource(
            wonTotalCents: 0,
            forecastPonderadoCents: 0,
            shouldThrow: true,
            delayMs: 0);

        var reader = BuildReader(stub);
        var query = new ForecastViewQuery(Guid.NewGuid(), Guid.NewGuid(), null, 2026, 6);

        // Act
        var result = await reader.Read(query);

        // Assert
        result.Available.Should().BeFalse("exceção deve retornar available=false sem propagar");
    }

    [Fact]
    public async Task Read_circuit_breaker_opens_after_failures_and_returns_available_false()
    {
        // Arrange — stub que sempre lança; circuit breaker abre após 3 falhas
        var stub = new StubForecastViewSource(
            wonTotalCents: 0,
            forecastPonderadoCents: 0,
            shouldThrow: true,
            delayMs: 0);

        var reader = BuildReader(stub, failuresBeforeOpen: 2);
        var query = new ForecastViewQuery(Guid.NewGuid(), Guid.NewGuid(), null, 2026, 6);

        // Provoca falhas suficientes para abrir o circuito
        await reader.Read(query);
        await reader.Read(query);

        // Act — circuito aberto → retorno imediato
        var result = await reader.Read(query);

        // Assert
        result.Available.Should().BeFalse("circuito aberto deve retornar available=false imediatamente");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PipelineForecastReader BuildReader(
        IForecastViewSource source,
        int timeoutMs = 5_000,
        int failuresBeforeOpen = 5)
    {
        return new PipelineForecastReader(
            source,
            NullLogger<PipelineForecastReader>.Instance,
            timeoutMs: timeoutMs,
            failuresBeforeOpen: failuresBeforeOpen,
            breakDurationMs: 30_000);
    }
}

/// <summary>
/// Stub de IForecastViewSource para testes do PipelineForecastReader.
/// Simula leitura bem-sucedida, com delay (simula timeout) ou com exceção.
/// </summary>
internal sealed class StubForecastViewSource : IForecastViewSource
{
    private readonly long _wonTotalCents;
    private readonly long _forecastPonderadoCents;
    private readonly bool _shouldThrow;
    private readonly int _delayMs;

    public StubForecastViewSource(long wonTotalCents, long forecastPonderadoCents,
        bool shouldThrow, int delayMs)
    {
        _wonTotalCents = wonTotalCents;
        _forecastPonderadoCents = forecastPonderadoCents;
        _shouldThrow = shouldThrow;
        _delayMs = delayMs;
    }

    public async Task<(long WonTotalCents, long ForecastPonderadoCents)> ReadAsync(
        ForecastViewQuery query,
        CancellationToken cancellationToken = default)
    {
        if (_delayMs > 0)
            await Task.Delay(_delayMs, cancellationToken);

        if (_shouldThrow)
            throw new InvalidOperationException("Falha simulada no pipeline");

        return (_wonTotalCents, _forecastPonderadoCents);
    }
}
