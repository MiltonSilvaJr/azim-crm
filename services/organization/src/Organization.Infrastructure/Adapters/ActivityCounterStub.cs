using Organization.Application.Ports;

namespace Organization.Infrastructure.Adapters;

/// <summary>
/// Stub MVP de <see cref="IActivityCounter"/>.
/// Para MVP, retorna 0 (sem atividades futuras), permitindo que a desativação de usuário prossiga.
/// Em produção, substituir por adapter que consulta o módulo <c>activity</c>
/// via chamada in-process ou HTTP (monólito modular, §6.4).
/// </summary>
public sealed class ActivityCounterStub : IActivityCounter
{
    /// <inheritdoc/>
    public Task<int> CountFutureAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        // MVP stub: sem atividades futuras. Desativação de usuário sempre permitida.
        // TODO (pós-MVP): integrar com módulo activity.
        return Task.FromResult(0);
    }
}
