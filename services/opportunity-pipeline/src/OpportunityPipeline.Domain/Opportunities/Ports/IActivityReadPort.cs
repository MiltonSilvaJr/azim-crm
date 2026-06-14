namespace OpportunityPipeline.Domain.Opportunities.Ports;

/// <summary>
/// Porta de leitura do serviço activity-management (upstream conformist).
/// Consulta last_activity_at para detecção de estagnação (Req 17, DD-005).
/// Mapeia: Req 17, design §6.4.
/// </summary>
public interface IActivityReadPort
{
    /// <summary>
    /// Retorna a data/hora da última atividade relacionada à oportunidade.
    /// Degradação graciosa: null quando activity-management indisponível.
    /// </summary>
    Task<DateTimeOffset?> GetLastActivityAtAsync(
        Guid tenantId,
        Guid opportunityId,
        CancellationToken cancellationToken = default);
}
