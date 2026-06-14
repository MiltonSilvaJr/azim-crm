using OpportunityPipeline.Domain.Opportunities.Ports;

namespace OpportunityPipeline.Infrastructure.Tenancy;

/// <summary>
/// Implementação concreta de IClock usando UTC real do sistema.
/// Injetada em produção. Em testes, substitua por FakeClock.
/// Mapeia: design §5.4, TASK-16.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc/>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    /// <inheritdoc/>
    public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}
