using Authentication.Infrastructure.Metrics;
using FluentAssertions;
using System.Diagnostics.Metrics;

namespace Authentication.Infrastructure.Tests.Metrics;

/// <summary>
/// Testes das métricas de autenticação.
///
/// Verifica que:
///   - auth_token_validation_success_total é incrementado em validação bem-sucedida
///   - auth_token_validation_failure_total é incrementado com o label `causa` correto
///   - auth_rate_limit_block_total é incrementado quando rate limit bloqueia
///   - auth_latency_ms registra a latência da operação
///   - Nenhuma métrica emite tenant_id ou identity_uid como valor sensível
///
/// Mapeia: TASK-23, RNF 5, RNF 2.1, RNF 8.3, design.md § 11.
/// </summary>
public sealed class AuthMetricsTests : IDisposable
{
    private readonly MeterListener _listener;
    private readonly Dictionary<string, (long Value, KeyValuePair<string, object?>[] Tags)> _counters = new();
    private readonly List<(double Value, KeyValuePair<string, object?>[] Tags)> _histograms = new();

    public AuthMetricsTests()
    {
        _listener = new MeterListener();
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == AuthMetrics.MeterName)
                listener.EnableMeasurementEvents(instrument);
        };

        _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            _counters[instrument.Name] = (measurement, tags.ToArray());
        });

        _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            _histograms.Add((measurement, tags.ToArray()));
        });

        _listener.Start();
    }

    public void Dispose() => _listener.Dispose();

    [Fact]
    public void RecordTokenValidationSuccess_IncrementsSuccessCounter()
    {
        // Arrange
        var metrics = new AuthMetrics();

        // Act
        metrics.RecordTokenValidationSuccess(tenantId: "tenant-abc");

        // Assert
        _counters.Should().ContainKey(AuthMetrics.TokenValidationSuccessTotal);
        _counters[AuthMetrics.TokenValidationSuccessTotal].Value.Should().Be(1);
    }

    [Fact]
    public void RecordTokenValidationFailure_WithExpiredCause_IncrementsFailureCounterWithCauseLabel()
    {
        // Arrange
        var metrics = new AuthMetrics();

        // Act
        metrics.RecordTokenValidationFailure(tenantId: "tenant-abc", cause: "expired");

        // Assert
        _counters.Should().ContainKey(AuthMetrics.TokenValidationFailureTotal);
        var (value, tags) = _counters[AuthMetrics.TokenValidationFailureTotal];
        value.Should().Be(1);

        // Verificar que o label causa está presente
        tags.Should().Contain(t => t.Key == "causa" && t.Value!.ToString() == "expired");
    }

    [Fact]
    public void RecordTokenValidationFailure_WithInvalidSignatureCause_IncrementsWithCorrectLabel()
    {
        // Arrange
        var metrics = new AuthMetrics();

        // Act
        metrics.RecordTokenValidationFailure(tenantId: "tenant-abc", cause: "invalid_signature");

        // Assert
        _counters.Should().ContainKey(AuthMetrics.TokenValidationFailureTotal);
        var (_, tags) = _counters[AuthMetrics.TokenValidationFailureTotal];
        tags.Should().Contain(t => t.Key == "causa" && t.Value!.ToString() == "invalid_signature");
    }

    [Fact]
    public void RecordTokenValidationFailure_WithTenantMismatchCause_IncrementsWithCorrectLabel()
    {
        // Arrange
        var metrics = new AuthMetrics();

        // Act
        metrics.RecordTokenValidationFailure(tenantId: "tenant-abc", cause: "tenant_mismatch");

        // Assert
        _counters.Should().ContainKey(AuthMetrics.TokenValidationFailureTotal);
        var (_, tags) = _counters[AuthMetrics.TokenValidationFailureTotal];
        tags.Should().Contain(t => t.Key == "causa" && t.Value!.ToString() == "tenant_mismatch");
    }

    [Fact]
    public void RecordRateLimitBlock_IncrementsBlockCounter()
    {
        // Arrange
        var metrics = new AuthMetrics();

        // Act
        metrics.RecordRateLimitBlock(tenantId: "tenant-abc");

        // Assert
        _counters.Should().ContainKey(AuthMetrics.RateLimitBlockTotal);
        _counters[AuthMetrics.RateLimitBlockTotal].Value.Should().Be(1);
    }

    [Fact]
    public void RecordLatency_RecordsHistogramWithPositiveValue()
    {
        // Arrange
        var metrics = new AuthMetrics();

        // Act
        metrics.RecordLatency(elapsedMs: 42.5);

        // Assert
        _histograms.Should().ContainSingle(h => Math.Abs(h.Value - 42.5) < 0.001);
    }

    [Fact]
    public void RecordTokenValidationSuccess_TenantIdLabelIsNotSensitive()
    {
        // Arrange — tenant_id como label é aceito; identity_uid como label é proibido
        var metrics = new AuthMetrics();

        // Act
        metrics.RecordTokenValidationSuccess(tenantId: "tenant-abc");

        // Assert — label deve ser tenant_id, nunca identity_uid
        var (_, tags) = _counters[AuthMetrics.TokenValidationSuccessTotal];
        tags.Should().NotContain(t => t.Key == "identity_uid");
        tags.Should().NotContain(t => t.Key == "email");
    }
}
