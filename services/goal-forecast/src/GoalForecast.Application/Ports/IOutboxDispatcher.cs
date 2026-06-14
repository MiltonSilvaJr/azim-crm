using GoalForecast.Domain.Common;

namespace GoalForecast.Application.Ports;

/// <summary>
/// Porta de saída para despacho atômico de domain events via padrão outbox.
/// O dispatcher grava o evento serializado em <c>outbox_events</c> na mesma
/// transação da escrita do aggregate (RNF 5, design §6.6).
///
/// Contrato:
/// <list type="bullet">
///   <item>O evento é gravado na mesma transação do DbContext do chamador.</item>
///   <item>Rollback reverte tanto o aggregate quanto o evento no outbox.</item>
///   <item>Serialização usa <c>long</c> para valores monetários — nunca <c>double</c> (RNF 4).</item>
/// </list>
///
/// Mapeia: Req 10, RNF 5, design §6.6, TASK-20.
/// </summary>
public interface IOutboxDispatcher
{
    /// <summary>
    /// Grava <paramref name="domainEvent"/> serializado em <c>outbox_events</c>
    /// usando a transação corrente do DbContext.
    /// </summary>
    /// <param name="domainEvent">Evento de domínio a persistir.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}
