using System.Diagnostics.Metrics;

namespace TenantAdministration.Infrastructure.Tests.Fixtures;

/// <summary>
/// Implementação de <see cref="IMeterFactory"/> para uso em testes unitários e de integração.
/// Cria meters reais mas descartados, sem exportação de métricas.
/// </summary>
internal sealed class FakeMeterFactory : IMeterFactory
{
    private readonly List<Meter> _meters = [];

    /// <inheritdoc/>
    public Meter Create(MeterOptions options)
    {
        var meter = new Meter(options);
        _meters.Add(meter);
        return meter;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var meter in _meters)
            meter.Dispose();
        _meters.Clear();
    }
}
