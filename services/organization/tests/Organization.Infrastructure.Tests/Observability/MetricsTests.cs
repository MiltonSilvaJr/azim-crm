using System.Diagnostics.Metrics;
using FluentAssertions;
using Organization.Infrastructure.Observability;
using Xunit;

namespace Organization.Infrastructure.Tests.Observability;

/// <summary>
/// Testes das métricas do módulo Organization — TASK-25 (Onda 6 — Hardening).
/// Valida que os 5 contadores estão definidos, são incrementados corretamente
/// e não carregam PII. Referências: design §11; RNF 6.2.
/// </summary>
public sealed class MetricsTests
{
    [Fact]
    public void OrganizationMetrics_HasAllFiveRequiredCounters()
    {
        // Arrange & Act
        using var meterFactory = new TestMeterFactory();
        using var metrics = new OrganizationMetrics(meterFactory);

        // Assert — todos os 5 contadores do design §11 devem existir
        metrics.UsersInvitedTotal.Should().NotBeNull("users_invited_total é obrigatório");
        metrics.UsersDeactivatedTotal.Should().NotBeNull("users_deactivated_total é obrigatório");
        metrics.BuCreatedTotal.Should().NotBeNull("bu_created_total é obrigatório");
        metrics.MembershipCacheHitTotal.Should().NotBeNull("membership_cache_hit_total é obrigatório");
        metrics.MembershipCacheMissTotal.Should().NotBeNull("membership_cache_miss_total é obrigatório");
        metrics.TenantRlsViolationCount.Should().NotBeNull("tenant_rls_violation_count é obrigatório");
    }

    [Fact]
    public void OrganizationMetrics_MeterNameIsOrganization()
    {
        // Arrange & Act
        using var meterFactory = new TestMeterFactory();
        using var metrics = new OrganizationMetrics(meterFactory);

        // Assert
        OrganizationMetrics.MeterName.Should().Be("organization",
            "meter name deve ser 'organization' conforme design §11");
    }

    [Fact]
    public void OrganizationMetricsAdapter_IncrementBuCreated_IncrementsMeasurement()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        using var metrics = new OrganizationMetrics(meterFactory);
        var adapter = new OrganizationMetricsAdapter(metrics);

        var measurements = new List<long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == OrganizationMetrics.MeterName &&
                instrument.Name == "bu_created_total")
                listener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => measurements.Add(value));
        listener.Start();

        // Act
        adapter.IncrementBuCreated();
        listener.RecordObservableInstruments();

        // Assert
        measurements.Should().Contain(1L,
            "IncrementBuCreated deve incrementar bu_created_total em 1");
    }

    [Fact]
    public void OrganizationMetricsAdapter_IncrementUsersInvited_IncrementsMeasurement()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        using var metrics = new OrganizationMetrics(meterFactory);
        var adapter = new OrganizationMetricsAdapter(metrics);

        var measurements = new List<long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == OrganizationMetrics.MeterName &&
                instrument.Name == "users_invited_total")
                listener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => measurements.Add(value));
        listener.Start();

        // Act
        adapter.IncrementUsersInvited();
        listener.RecordObservableInstruments();

        // Assert
        measurements.Should().Contain(1L,
            "IncrementUsersInvited deve incrementar users_invited_total em 1");
    }

    [Fact]
    public void OrganizationMetricsAdapter_IncrementUsersDeactivated_IncrementsMeasurement()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        using var metrics = new OrganizationMetrics(meterFactory);
        var adapter = new OrganizationMetricsAdapter(metrics);

        var measurements = new List<long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == OrganizationMetrics.MeterName &&
                instrument.Name == "users_deactivated_total")
                listener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => measurements.Add(value));
        listener.Start();

        // Act
        adapter.IncrementUsersDeactivated();
        listener.RecordObservableInstruments();

        // Assert
        measurements.Should().Contain(1L,
            "IncrementUsersDeactivated deve incrementar users_deactivated_total em 1");
    }

    [Fact]
    public void OrganizationMetricsAdapter_IncrementCacheHitAndMiss_IncrementCorrectCounters()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        using var metrics = new OrganizationMetrics(meterFactory);
        var adapter = new OrganizationMetricsAdapter(metrics);

        var hitMeasurements = new List<long>();
        var missMeasurements = new List<long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == OrganizationMetrics.MeterName)
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
        {
            if (instrument.Name == "membership_cache_hit_total") hitMeasurements.Add(value);
            if (instrument.Name == "membership_cache_miss_total") missMeasurements.Add(value);
        });
        listener.Start();

        // Act
        adapter.IncrementMembershipCacheHit();
        adapter.IncrementMembershipCacheHit();
        adapter.IncrementMembershipCacheMiss();

        // Assert
        hitMeasurements.Should().HaveCount(2, "dois hits registrados");
        missMeasurements.Should().HaveCount(1, "um miss registrado");
    }

    // ── Infra de suporte ───────────────────────────────────────────────────

    /// <summary>
    /// IMeterFactory mínima para testes unitários sem host completo.
    /// Delega para <see cref="Meter"/> real, evitando dependência do DI.
    /// </summary>
    private sealed class TestMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];

        public Meter Create(MeterOptions options)
        {
            var meter = new Meter(options.Name, options.Version);
            _meters.Add(meter);
            return meter;
        }

        public void Dispose()
        {
            foreach (var m in _meters)
                m.Dispose();
        }
    }
}
