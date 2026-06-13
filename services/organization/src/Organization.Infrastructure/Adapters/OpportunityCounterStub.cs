using Organization.Application.Ports;

namespace Organization.Infrastructure.Adapters;

/// <summary>
/// Stub MVP de <see cref="IOpportunityCounter"/>.
/// Para MVP, retorna 0 (sem oportunidades ativas), permitindo que a inativação de BU prossiga.
/// Em produção, substituir por adapter que consulta o módulo <c>opportunity-pipeline</c>
/// via chamada in-process ou HTTP (monólito modular, §6.4).
/// </summary>
public sealed class OpportunityCounterStub : IOpportunityCounter
{
    /// <inheritdoc/>
    public Task<int> CountActiveAsync(
        Guid tenantId,
        Guid buId,
        CancellationToken cancellationToken = default)
    {
        // MVP stub: sem oportunidades ativas. Inativação de BU sempre permitida.
        // TODO (pós-MVP): integrar com módulo opportunity-pipeline.
        return Task.FromResult(0);
    }
}
